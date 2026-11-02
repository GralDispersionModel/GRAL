"""Task-local persistent CUDA process for original GRAL trajectories; binary stdout only."""
import ctypes as c
import json
import math
import os
from pathlib import Path
import re
import struct
import sys
import time
import traceback

ROOT = Path(__file__).parent
from cuda_common import Cuda, P, D, Z, I, NVRTC_OPTIONS, NVRTC_PROGRAM
import ptx_cache

CPP_TYPES = {'bool': c.c_bool, 'unsigned char': c.c_ubyte, 'short': c.c_short,
             'int': c.c_int, 'unsigned int': c.c_uint, 'long long': c.c_longlong,
             'unsigned long long': c.c_ulonglong, 'float': c.c_float, 'double': c.c_double}
DTYPE_SIZE = {'Boolean': 1, 'Byte': 1, 'Int16': 2, 'Int32': 4, 'UInt32': 4,
              'Int64': 8, 'UInt64': 8, 'Single': 4, 'Double': 8}
OUTPUTS = ('Conz3dDouble', 'Conz5d', 'ReceptorConc', 'ReceptorParticleMaxConc', 'EmissionPerSG')
INT32_MAX = 2**31 - 1
PARTICLE_ARRAYS = ('SourceType', 'Xcoord', 'YCoord', 'ZCoord', 'ParticleMass',
                   'ParticleSG', 'ParticleSource', 'ParticleMode', 'ParticleVdep', 'ParticleVsed')
RESIDUAL_ARRAYS = ('_resI', '_resJ', '_resK', '_resSG', '_resConc', '_resSplit')
# Nonempty data for these unsupported branches is rejected. ObsWindU/V are allocated in V2701.
UNSUPPORTED_ACTIVE_ARRAYS = ('PS_TimeSerTempValues', 'PS_TimeSerVelValues', 'TS_TimeSerTempValues',
                 'TS_TimeSerVelValues', 'TransientDepo')
MANIFEST_BYTES = (ROOT / 'fields_manifest.json').read_bytes()
MANIFEST = json.loads(MANIFEST_BYTES)
ARR_TYPES = {}


def ctype(cpp):
    if cpp in CPP_TYPES:
        return CPP_TYPES[cpp]
    match = re.fullmatch(r'Arr<(.+),(\d+)>', cpp)
    if not match:
        raise ValueError('Unsupported state type: ' + cpp)
    rank = int(match[2])
    if rank not in ARR_TYPES:
        ARR_TYPES[rank] = type(f'Arr{rank}', (c.Structure,), {'_fields_': [('p', c.c_uint64), ('d', c.c_int * rank)]})
    return ARR_TYPES[rank]


class State(c.Structure):
    _fields_ = [(f['name'], ctype(f['cpp'])) for f in MANIFEST['fields'] if not f['constant']] + [
        ('RnGSeed1', c.c_uint), ('RnGSeed2', c.c_uint),
        ('GridRecStart', ctype('Arr<int,1>')), ('GridRecList', ctype('Arr<int,1>')), ('GridRecRow', c.c_int),
        ('TraceEnabled',c.c_bool),('Trace',ctype('Arr<double,2>')),
        ('TraceIds',ctype('Arr<int,1>')),('StepTrace',ctype('Arr<double,3>')),
        ('ResidualTrace',ctype('Arr<double,2>')),
        ('DeviceError', c.c_int), ('DeviceErrorPhase', c.c_int), ('DeviceErrorParticle', c.c_int)]


CPP_DTYPE = {'bool': 'Boolean', 'unsigned char': 'Byte', 'short': 'Int16',
             'int': 'Int32', 'unsigned int': 'UInt32', 'long long': 'Int64',
             'unsigned long long': 'UInt64', 'float': 'Single', 'double': 'Double'}
ARRAY_CPP = {f['name']: f['cpp'] for f in MANIFEST['fields'] if f['cpp'].startswith('Arr<')}
ARRAY_CPP.update(GridRecStart='Arr<int,1>', GridRecList='Arr<int,1>', Trace='Arr<double,2>',
                 TraceIds='Arr<int,1>', StepTrace='Arr<double,3>', ResidualTrace='Arr<double,2>')
ARRAY_CPP.update({name: 'Arr<float,1>' if name == '_resConc' else 'Arr<int,1>'
                  for name in ('_resI', '_resJ', '_resK', '_resSG', '_resConc', '_resSplit')})
INPUT_SCALARS = {f['name'] for f in MANIFEST['fields'] if not f['constant'] and not f['cpp'].startswith('Arr<')}
INPUT_SCALARS.update(('RnGSeed1', 'RnGSeed2', 'GridRecRow', 'TraceEnabled'))


def read_exact(stream, size):
    parts = bytearray(size)
    view = memoryview(parts)
    pos = 0
    while pos < size:
        count = stream.readinto(view[pos:])
        if not count:
            raise EOFError(f'Unexpected frame EOF at {pos}/{size}')
        pos += count
    return parts


def read_header(stream):
    length = stream.read(8)
    if not length:
        return None
    if len(length) != 8:
        length += read_exact(stream, 8-len(length))
    size = struct.unpack('<Q', length)[0]
    if size > 16 * 1024 * 1024:
        raise ValueError('Header too large')
    return json.loads(read_exact(stream, size))


def write_frame(header, chunks=()):
    raw = json.dumps(header, separators=(',', ':'), allow_nan=False).encode()
    sys.stdout.buffer.write(struct.pack('<Q', len(raw)))
    sys.stdout.buffer.write(raw)
    for chunk in chunks:
        sys.stdout.buffer.write(chunk)
    sys.stdout.buffer.flush()


class Server:
    def __init__(self):
        self.cuda = None
        try:
            self._initialize()
        except BaseException:
            if self.cuda is not None:
                self.cuda.close()
            raise

    def _initialize(self):
        self.cuda = Cuda()
        self.state = State()
        self.buffers = {}
        self.descriptors = {}
        self.kernel_total_ms = 0.0
        self.phase_counts = {'fresh': 0, 'residual': 0}
        self.residual_trace_base = 0
        self.metrics = {'pipe_read_wait_ms':0.0,'h2d_ms':0.0,'d2h_ms':0.0,'kernel_device_ms':0.0,'kernel_launches':0,'kernel_max_device_ms':0.0,
                        'buffer_allocation_count':0,'buffer_reuse_count':0,'buffer_free_count':0,'device_buffer_bytes':0,'peak_device_buffer_bytes':0,
                        'h2d_bytes':0,'d2h_bytes':0,'status_d2h_bytes':0,'status_d2h_ms':0.0,
                        'state_d2h_bytes':0,'state_d2h_ms':0.0,'init_count':0}
        self.start = time.perf_counter()
        self.metrics.update(context_ms=self.cuda.context_ms, cache_read_ms=0.0, nvrtc_compile_ms=0.0,
                            module_load_ms=0.0, post_load_abi_ms=0.0, cache_status='disabled',
                            cache_write_status='not_attempted')
        source = (ROOT/'generated.cu').read_text(encoding='utf-8')
        cache_path, key = None, None
        ptx = None
        read_start = time.perf_counter()
        try:
            if os.environ.get('INU_GPU_CACHE_DIR'):
                abi = [{'name': name, 'type': str(typ), 'offset': getattr(State, name).offset,
                        'size': c.sizeof(typ), 'alignment': c.alignment(typ)} for name, typ in State._fields_]
                identity = {'schema': ptx_cache.SCHEMA, 'source_sha256': ptx_cache.digest(source.encode('utf-8')),
                            'manifest_sha256': ptx_cache.digest(MANIFEST_BYTES),
                            'state_size': c.sizeof(State), 'state_alignment': c.alignment(State), 'fields': abi,
                            'options': [option.decode('ascii') for option in NVRTC_OPTIONS],
                            'program': NVRTC_PROGRAM.decode('ascii'), 'nvrtc_version': self.cuda.nvrtc_version,
                            'nvrtc_sha256': ptx_cache.digest(self.cuda.nvrtc_path.read_bytes()),
                            'nvrtc_builtins_sha256': ptx_cache.digest(self.cuda.nvrtc_builtins_path.read_bytes())}
                cache_path, key = ptx_cache.cache_path(identity)
                if cache_path is not None:
                    ptx, self.metrics['cache_status'] = ptx_cache.read(cache_path, key)
                else:
                    self.metrics['cache_status'] = key
        except (OSError, ValueError, TypeError, RuntimeError) as exc:
            self.metrics['cache_status'] = 'identity_error'
            print('PTX cache identity error: ' + str(exc), file=sys.stderr)
        self.metrics['cache_read_ms'] = (time.perf_counter() - read_start) * 1000
        if ptx is not None:
            try:
                self.load_and_validate(ptx)
            except Exception as exc:
                if self.module is not None and self.module in self.cuda.modules:
                    raise
                self.metrics['cache_status'] = 'hit_rejected'
                print('PTX cache entry rejected: ' + str(exc), file=sys.stderr)
                ptx = None
        if ptx is None:
            compile_start = time.perf_counter()
            try:
                ptx, log = self.cuda.compile_ptx(source)
            finally:
                self.metrics['nvrtc_compile_ms'] += (time.perf_counter() - compile_start) * 1000
            self.load_and_validate(ptx)
            (ROOT/'server_compile.log').write_text(log, encoding='utf-8')
            if cache_path is not None:
                self.metrics['cache_write_status'] = ptx_cache.write(cache_path, key, ptx)
        else:
            (ROOT/'server_compile.log').write_text('PTX cache hit; NVRTC compile skipped.\n', encoding='utf-8')
        (ROOT/'state_layout.json').write_text(json.dumps({'bytes': c.sizeof(State), 'fields': {
            name: {'offset': getattr(State, name).offset, 'bytes': c.sizeof(typ)} for name, typ in State._fields_}}, indent=2))
        self.event_start,self.event_end=P(),P()
        for event in (self.event_start,self.event_end):
            self.cuda.check(self.cuda.event_create(c.byref(event),0),'cuEventCreate')
            self.cuda.events.append(event)
        self.startup_ms=(time.perf_counter()-self.start)*1000

    def load_and_validate(self, ptx):
        self.module = None
        start = time.perf_counter()
        try:
            self.module = self.cuda.load_ptx(ptx)
        finally:
            self.metrics['module_load_ms'] += (time.perf_counter() - start) * 1000
        start = time.perf_counter()
        try:
            self.fn_size = self.function('gral_state_size')
            self.validate_module()
        except BaseException as primary:
            try:
                self.cuda.unload_module(self.module)
            except Exception as cleanup:
                raise primary from cleanup
            raise
        finally:
            self.metrics['post_load_abi_ms'] += (time.perf_counter() - start) * 1000

    def validate_module(self):
        self.fn_fresh = self.function('gral_fresh')
        self.fn_residual = self.function('gral_residual')
        get_global = self.cuda.cuda.cuModuleGetGlobal_v2
        get_global.argtypes = [c.POINTER(D), c.POINTER(Z), P, c.c_char_p]
        get_global.restype = I
        self.state_address, self.state_bytes = D(), Z()
        self.cuda.check(get_global(c.byref(self.state_address), c.byref(self.state_bytes), self.module, b'P'), 'cuModuleGetGlobal')
        if self.state_bytes.value != c.sizeof(State):
            raise ValueError(f'State layout size: CUDA {self.state_bytes.value}, Python {c.sizeof(State)}')

    def function(self, name):
        fn = P()
        self.cuda.check(self.cuda.get_fn(c.byref(fn), self.module, name.encode()), 'cuModuleGetFunction '+name)
        return fn

    def memory(self, name, size):
        old = self.buffers.get(name)
        if old is not None and old[1] >= size:
            self.metrics['buffer_reuse_count']+=1
            return old[0]
        if old is not None:
            self.cuda.check(self.cuda.free(old[0]), 'cuMemFree '+name)
            self.cuda.buffers.remove(old[0])
            self.metrics['buffer_free_count']+=1
            self.metrics['device_buffer_bytes']-=old[1]
        pointer = self.cuda.buffer(max(size, 1))
        self.buffers[name] = (pointer, max(size, 1))
        self.metrics['buffer_allocation_count']+=1
        self.metrics['device_buffer_bytes']+=max(size,1)
        self.metrics['peak_device_buffer_bytes']=max(self.metrics['peak_device_buffer_bytes'],self.metrics['device_buffer_bytes'])
        return pointer

    def receive(self, header):
        total = header.get('payload_bytes')
        if type(total) is not int or total < 0:
            raise ValueError('Payload length must be a nonnegative integer')
        descriptors = header.get('arrays', [])
        cursor = 0
        seen = set()
        # Validate the complete frame before consuming bytes or changing device state.
        for descriptor in descriptors:
            name, size = descriptor['name'], descriptor['bytes']
            if name not in ARRAY_CPP or name in seen:
                raise ValueError('Unknown input array: '+name)
            seen.add(name)
            if type(size) is not int or size < 0 or type(descriptor['offset']) is not int or descriptor['offset'] != cursor:
                raise ValueError('Array frame offset/length mismatch: '+name)
            if cursor + size > total:
                raise ValueError('Payload array exceeds frame length: '+name)
            shape = descriptor.get('shape', [])
            is_null = descriptor.get('is_null', False)
            match = re.fullmatch(r'Arr<(.+),(\d+)>', ARRAY_CPP[name])
            if not is_null and (descriptor['dtype'] != CPP_DTYPE.get(match[1]) or
                                len(shape) != int(match[2])):
                raise ValueError('Array dtype/rank mismatch: '+name)
            if any(type(d) is not int or not 0 <= d <= INT32_MAX for d in shape) or (is_null and size != 0):
                raise ValueError('Invalid array shape/null payload: '+name)
            if not is_null and math.prod(shape)*DTYPE_SIZE[descriptor['dtype']] != size:
                raise ValueError('Shape/bytes mismatch: '+name)
            if name in UNSUPPORTED_ACTIVE_ARRAYS and not is_null and size:
                raise ValueError('Unsupported active optional data: '+name)
            cursor += size
        if cursor != total:
            raise ValueError('Payload length mismatch')
        for name in header.get('scalars', {}):
            if name not in INPUT_SCALARS:
                raise ValueError('Unknown input scalar: '+name)
        for descriptor in descriptors:
            name, size = descriptor['name'], descriptor['bytes']
            shape = descriptor.get('shape', [])
            is_null = descriptor.get('is_null', False)
            read_start=time.perf_counter()
            data = read_exact(sys.stdin.buffer, size)
            self.metrics['pipe_read_wait_ms']+=(time.perf_counter()-read_start)*1000
            if is_null or size == 0:
                if hasattr(self.state, name):
                    arr = getattr(self.state, name)
                    arr.p = 0
                    arr.d[:] = [0] * len(arr.d)
                self.descriptors[name] = descriptor
                continue
            pointer = self.memory(name, size)
            host = (c.c_byte*size).from_buffer(data)
            upload_start=time.perf_counter()
            self.cuda.check(self.cuda.h2d(pointer, c.cast(host, P), size), 'cuMemcpyHtoD '+name)
            self.metrics['h2d_ms']+=(time.perf_counter()-upload_start)*1000
            self.metrics['h2d_bytes']+=size
            self.descriptors[name] = descriptor
            if hasattr(self.state, name):
                arr = getattr(self.state, name)
                if len(shape) != len(arr.d):
                    raise ValueError('Array rank mismatch: '+name)
                arr.p = pointer.value
                arr.d[:] = shape
        for name, value in header.get('scalars', {}).items():
            setattr(self.state, name, value)

    def check_init_fields(self, header):
        expected={f['name'] for f in MANIFEST['fields'] if not f['constant'] and not f['cpp'].startswith('Arr<')}
        expected.update(('RnGSeed1', 'RnGSeed2', 'GridRecRow'))
        missing=expected-set(header.get('scalars',{}))
        if missing:
            raise ValueError('Missing scalar fields: '+','.join(sorted(missing)))

    def guard(self):
        expected = {'Topo': 1, 'ISTATIONAER': 0, 'IStatistics': 4,
                    'UseFixedRndSeedVal': True, 'BuildingsExist': False,
                    'WetDeposition': False, 'Odour': False, 'WriteVerticalConcentration': False,
                    'TunnelEntr': False, 'TunnelOppLane': False, 'TS_Count': 0,
                    'AdaptiveRoughnessMax': 0, 'LogLevel': 0}
        for name, value in expected.items():
            if getattr(self.state, name) != value:
                raise ValueError(f'Unsupported runtime branch {name}={getattr(self.state,name)!r}; expected {value!r}')
        if not 0 <= self.state.ReceptorNumber <= 50 or not 0 < self.state.NS <= 9:
            raise ValueError('Receptor/slice local capacity exceeded')
        for name in ('UK','VK','WK','AHK','HOKART','Conz3dDouble','Conz5d','SourceGroups'):
            if not getattr(self.state,name).p:
                raise ValueError('Missing required grid: '+name)
        if self.state.GridRecRow:
            if self.state.GridRecRow != self.state.NYL + 1 or not self.state.GridRecStart.p:
                raise ValueError('Invalid receptor CSR row/start')
            if self.state.GridRecStart.d[0] != (self.state.NXL + 1) * self.state.GridRecRow + 1:
                raise ValueError('Invalid receptor CSR dimensions')
        if self.state.Trace.p and self.state.Trace.d[1] != 8:
            raise ValueError('Fresh trace requires 8 columns')
        if self.state.ResidualTrace.p and self.state.ResidualTrace.d[1] != 16:
            raise ValueError('Residual trace requires 16 columns')

    def bind(self):
        self.check_device_error()
        self.cuda.check(self.cuda.h2d(self.state_address, c.cast(c.byref(self.state), P), c.sizeof(State)), 'bind State')

    def check_device_error(self):
        if self.state.DeviceError:
            raise RuntimeError(f'GPU nonfinite trajectory; phase={self.state.DeviceErrorPhase}, '
                               f'particle={self.state.DeviceErrorParticle}; whole run aborted; results unavailable')

    def launch(self, function, count, args, block=128):
        values = (P * len(args))(*(c.cast(c.byref(a), P) for a in args))
        start = time.perf_counter()
        self.cuda.check(self.cuda.event_record(self.event_start,None),'record start')
        self.cuda.check(self.cuda.launch(function, (count+block-1)//block,1,1,block,1,1,0,None,values,None), 'cuLaunchKernel')
        self.cuda.check(self.cuda.event_record(self.event_end,None),'record end')
        self.cuda.check(self.cuda.event_sync(self.event_end), 'cuEventSynchronize')
        gpu_elapsed=c.c_float()
        self.cuda.check(self.cuda.event_elapsed(c.byref(gpu_elapsed),self.event_start,self.event_end),'cuEventElapsedTime')
        self.metrics['kernel_device_ms']+=gpu_elapsed.value
        self.metrics['kernel_launches']+=1
        self.metrics['kernel_max_device_ms']=max(self.metrics['kernel_max_device_ms'],gpu_elapsed.value)
        status_start = State.DeviceError.offset
        status_size = State.DeviceErrorParticle.offset + c.sizeof(c.c_int) - status_start
        download_start = time.perf_counter()
        self.cuda.check(self.cuda.d2h(c.cast(c.byref(self.state,status_start),P),D(self.state_address.value+status_start),status_size), 'read kernel status')
        self.metrics['status_d2h_ms']+=(time.perf_counter()-download_start)*1000
        self.metrics['status_d2h_bytes']+=status_size
        self.check_device_error()
        elapsed = (time.perf_counter()-start)*1000
        self.kernel_total_ms += elapsed
        return elapsed

    def execute(self, header):
        self.check_device_error()
        count = header['count']
        first = header['first']
        if type(count) is not int or not 0 <= count <= INT32_MAX:
            raise ValueError('Particle count must be an integer in [0, INT32_MAX]')
        if type(first) is not int or not 0 <= first <= INT32_MAX:
            raise ValueError('First particle must be an integer in [0, INT32_MAX]')
        phase = header['phase']
        if phase not in self.phase_counts:
            raise ValueError('Unknown phase')
        self.receive(header)
        self.guard()
        if phase == 'fresh':
            for name in PARTICLE_ARRAYS:
                array = getattr(self.state, name)
                if first < 1 or not array.p or first + count > array.d[0]:
                    raise ValueError('Fresh particle range exceeds '+name)
        else:
            # Keep the cumulative next trace index representable by the kernel's signed int ABI.
            base = self.residual_trace_base
            if type(base) is not int or base < 0 or base + count > INT32_MAX:
                raise ValueError('Residual trace range exceeds INT32_MAX')
            for name in RESIDUAL_ARRAYS:
                descriptor = self.descriptors.get(name, {})
                if descriptor.get('is_null') or descriptor.get('shape') != [count]:
                    raise ValueError('Residual batch length mismatch: '+name)
                buffer = self.buffers.get(name)
                if count and (buffer is None or not buffer[0].value or buffer[1] < count * 4):
                    raise ValueError('Residual batch buffer too small: '+name)
        elapsed = 0.0
        # Start conservatively; a single trajectory can still be long. Actual timing is recorded.
        chunk = min(65536, max(1, int(header.get('chunk', 4096))))
        block=int(header.get('block',128))
        if block not in (128,256):
            raise ValueError('Supported block sizes: 128, 256')
        self.bind()
        for offset in range(0,count,chunk):
            part=min(chunk,count-offset)
            if phase == 'fresh':
                elapsed += self.launch(self.fn_fresh, part, [I(first+offset), I(part)],block)
            else:
                pointers=[D(self.buffers[name][0].value+offset*4) for name in RESIDUAL_ARRAYS]
                elapsed += self.launch(self.fn_residual, part, [I(part), I(self.residual_trace_base + offset), *pointers],block)
        if phase == 'residual':
            self.residual_trace_base += count
        self.phase_counts[phase]+=count
        # Preserve device scalar mutations before the next request rebinds the State.
        download_start = time.perf_counter()
        self.cuda.check(self.cuda.d2h(c.cast(c.byref(self.state),P),self.state_address,c.sizeof(State)),'read State')
        self.metrics['state_d2h_ms']+=(time.perf_counter()-download_start)*1000
        self.metrics['state_d2h_bytes']+=c.sizeof(State)
        return {'status':'ok','kernel_ms':elapsed,'phase':phase,'count':count,'payload_bytes':0,'metrics_cumulative':dict(self.metrics)}

    def result(self):
        self.check_device_error()
        descriptors=[]
        chunks=[]
        cursor=0
        trace_outputs=tuple(name for name in ('Trace', 'StepTrace', 'ResidualTrace')
                            if self.state.TraceEnabled and getattr(self.state, name).p)
        for name in OUTPUTS+trace_outputs:
            descriptor=dict(self.descriptors[name])
            size=descriptor['bytes']
            if size == 0:
                descriptor['offset'] = cursor
                descriptors.append(descriptor)
                continue
            data=bytearray(size)
            host=(c.c_byte*size).from_buffer(data)
            download_start=time.perf_counter()
            self.cuda.check(self.cuda.d2h(c.cast(host,P), self.buffers[name][0],size),'read '+name)
            self.metrics['d2h_ms']+=(time.perf_counter()-download_start)*1000
            self.metrics['d2h_bytes']+=size
            descriptor['offset']=cursor
            descriptors.append(descriptor)
            chunks.append(data)
            cursor+=size
        return {'status':'ok','scalars':{},
                'arrays':descriptors,'payload_bytes':cursor,'kernel_total_ms':self.kernel_total_ms,
                'phase_counts':self.phase_counts,'metrics_cumulative':dict(self.metrics)},chunks


def main():
    server=None
    try:
        server=Server()
        while True:
            header=read_header(sys.stdin.buffer)
            if header is None:
                break
            command=header['command']
            if command=='close':
                write_frame({'status':'ok','payload_bytes':0})
                break
            if command=='init':
                server.metrics['init_count']+=1
                server.residual_trace_base = 0
                server.check_init_fields(header)
                server.receive(header)
                server.guard()
                server.bind()
                write_frame({'status':'ok','state_bytes':c.sizeof(State),'payload_bytes':0,'startup_ms':server.startup_ms,'metrics_cumulative':dict(server.metrics)})
            elif command=='execute':
                write_frame(server.execute(header))
            elif command=='result':
                h, chunks=server.result()
                write_frame(h,chunks)
            else:
                raise ValueError('Unknown command: '+command)
    except Exception as exc:
        traceback.print_exc(file=sys.stderr)
        write_frame({'status':'error','error':str(exc),'payload_bytes':0})
        return 1
    finally:
        if server is not None:
            server.cuda.close()
    return 0


if __name__=='__main__':
    raise SystemExit(main())
