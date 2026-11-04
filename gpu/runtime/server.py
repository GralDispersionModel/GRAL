"""Task-local persistent CUDA process for original GRAL trajectories; binary stdout only."""
import ctypes as c
from array import array
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


def write_diagnostic(name, text):
    """Write a diagnostic file next to the INU_GPU_STDERR log, never into the install folder."""
    stderr_path = os.environ.get('INU_GPU_STDERR')
    folder = Path(stderr_path).parent if stderr_path else Path(os.environ.get('TEMP', '.'))
    try:
        (folder / name).write_text(text, encoding='utf-8')
    except OSError as exc:
        print(f'Could not write diagnostic file {name}: {exc}', file=sys.stderr)
from cuda_common import Cuda, CudaError, P, D, Z, I, NVRTC_PROGRAM, MAX_SLICES, MAX_RECEPTORS
import ptx_cache

CPP_TYPES = {'bool': c.c_bool, 'unsigned char': c.c_ubyte, 'short': c.c_short,
             'int': c.c_int, 'unsigned int': c.c_uint, 'long long': c.c_longlong,
             'unsigned long long': c.c_ulonglong, 'float': c.c_float, 'double': c.c_double}
DTYPE_SIZE = {'Boolean': 1, 'Byte': 1, 'Int16': 2, 'Int32': 4, 'UInt32': 4,
              'Int64': 8, 'UInt64': 8, 'Single': 4, 'Double': 8}
OUTPUTS = ('Conz3dDouble', 'Conz5d', 'ReceptorConc', 'ReceptorParticleMaxConc', 'EmissionPerSG')
ALL_OUTPUTS = OUTPUTS + ('Depo_conz', 'Conz3dp', 'Conz3dm', 'ConzSsum', 'LogReflexions', 'Log_Timesteps')
INT32_MAX = 2**31 - 1
INT32_MIN = -(2**31)
PARTICLE_ARRAYS = ('SourceType', 'Xcoord', 'YCoord', 'ZCoord', 'ParticleMass',
                   'ParticleSG', 'ParticleSource', 'ParticleMode', 'ParticleVdep', 'ParticleVsed')
RESIDUAL_ARRAYS = ('_resI', '_resJ', '_resK', '_resSG', '_resConc', '_resSplit')
SCRATCH_ARRAYS = ('ParticleReceptorScratch', 'ParticleSliceScratch')
TRANSFER_BYTES = 1024 * 1024
MAX_MEMORY_PARTICLES = 65536
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
        ('ParticleReceptorScratch', ctype('Arr<double,2>')),
        ('ParticleSliceScratch', ctype('Arr<int,2>')),
        ('ParticleWindowFirst', c.c_int),
        ('DeviceError', c.c_int), ('DeviceErrorPhase', c.c_int), ('DeviceErrorParticle', c.c_int)]


CPP_DTYPE = {'bool': 'Boolean', 'unsigned char': 'Byte', 'short': 'Int16',
             'int': 'Int32', 'unsigned int': 'UInt32', 'long long': 'Int64',
             'unsigned long long': 'UInt64', 'float': 'Single', 'double': 'Double'}
ARRAY_CPP = {f['name']: f['cpp'] for f in MANIFEST['fields'] if f['cpp'].startswith('Arr<')}
ARRAY_CPP.update(GridRecStart='Arr<int,1>', GridRecList='Arr<int,1>', Trace='Arr<double,2>',
                 TraceIds='Arr<int,1>', StepTrace='Arr<double,3>', ResidualTrace='Arr<double,2>')
ARRAY_CPP.update({name: 'Arr<float,1>' if name == '_resConc' else 'Arr<int,1>'
                  for name in ('_resI', '_resJ', '_resK', '_resSG', '_resConc', '_resSplit')})
WIRE_DTYPES = {f['name']: f.get('wire_dtype') for f in MANIFEST['fields'] if f.get('wire_dtype')}
DOUBLE_STORAGE_FIELDS = frozenset(name for name, dtype in WIRE_DTYPES.items()
                                  if dtype == 'Single' and ARRAY_CPP[name].startswith('Arr<double,'))
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


class FrameWriteError(RuntimeError):
    """A started response cannot be followed by another frame after failure."""


def write_frame(header, chunks=()):
    expected = header.get('payload_bytes')
    if type(expected) is not int or not 0 <= expected <= sys.maxsize:
        raise ValueError('Invalid response payload length')
    raw = json.dumps(header, separators=(',', ':'), allow_nan=False).encode()
    iterator = iter(chunks)
    started = False
    stream = sys.stdout.buffer

    def write_all(data):
        view = memoryview(data).cast('B')
        while view:
            count = stream.write(view)
            if type(count) is not int or not 0 < count <= len(view):
                raise OSError('Response write made no progress')
            view = view[count:]

    try:
        try:
            # A zero-length response has no final payload byte to hold until iterator validation.
            if expected == 0:
                for chunk in iterator:
                    if memoryview(chunk).nbytes:
                        raise ValueError('Response payload exceeds declared length')
            started = True
            write_all(struct.pack('<Q', len(raw)))
            write_all(raw)
            remaining = expected
            final_chunk = b''
            for chunk in iterator:
                view = memoryview(chunk).cast('B')
                size = len(view)
                if size > remaining:
                    raise ValueError('Response payload exceeds declared length')
                remaining -= size
                if remaining:
                    write_all(view)
                elif size:
                    # A late iterator failure must leave an incomplete payload at the client.
                    final_chunk = bytes(view)
            if remaining:
                raise EOFError(f'Response payload ended {remaining} bytes early')
        finally:
            close = getattr(iterator, 'close', None)
            if close is not None:
                close()
        write_all(final_chunk)
        stream.flush()
    except Exception as exc:
        if started:
            raise FrameWriteError('Response stream failed; connection must close') from exc
        raise


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
        self.mapped_buffers = {}
        self.host_arrays = {}
        self.validation_arrays = {}
        self.descriptors = {}
        self.active_deposition_arrays = {}
        self.kernel_total_ms = 0.0
        self.phase_counts = {'fresh': 0, 'residual': 0}
        self.residual_trace_base = 0
        self.residual_error_mapping = None
        self.execution_failed = False
        self.metrics = {'pipe_read_wait_ms':0.0,'h2d_ms':0.0,'d2h_ms':0.0,'kernel_device_ms':0.0,'kernel_launches':0,'kernel_max_device_ms':0.0,
                        'buffer_allocation_count':0,'buffer_reuse_count':0,'buffer_free_count':0,'device_buffer_bytes':0,'peak_device_buffer_bytes':0,
                        'zero_fill_bytes':0,'zero_fill_count':0,'zero_fill_ms':0.0,'h2d_bytes':0,'d2h_bytes':0,'status_d2h_bytes':0,'status_d2h_ms':0.0,
                        'state_d2h_bytes':0,'state_d2h_ms':0.0,'init_count':0}
        self.metrics.update(mapped_host_bytes=0, peak_mapped_host_bytes=0,
                            device_allocation_oom=0, particle_window_retries=0,
                            fresh_memory_windows=0, residual_memory_windows=0,
                            mapped_host_copy_bytes=0, mapped_host_copy_ms=0.0,
                            mapped_output_bytes=0, peak_mapped_output_bytes=0,
                            mapped_scratch_bytes=0, peak_mapped_scratch_bytes=0,
                            mapped_buffer_names=[])
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
                            'options': [option.decode('ascii') for option in self.cuda.nvrtc_options],
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
            write_diagnostic('server_compile.log', log)
            if cache_path is not None:
                self.metrics['cache_write_status'] = ptx_cache.write(cache_path, key, ptx)
        else:
            write_diagnostic('server_compile.log', 'PTX cache hit; NVRTC compile skipped.\n')
        write_diagnostic('state_layout.json', json.dumps({'bytes': c.sizeof(State), 'fields': {
            name: {'offset': getattr(State, name).offset, 'bytes': c.sizeof(typ)} for name, typ in State._fields_}}, indent=2))
        self.prewarm_kernels()
        self.event_start,self.event_end=P(),P()
        for event in (self.event_start,self.event_end):
            self.cuda.check(self.cuda.event_create(c.byref(event),0),'cuEventCreate')
            self.cuda.events.append(event)
        self.startup_ms=(time.perf_counter()-self.start)*1000
        self.refresh_memory_budget()

    @staticmethod
    def memory_setting(name, default, minimum):
        raw = os.environ.get(name, str(default))
        if not raw.isascii() or not raw.isdecimal() or not minimum <= int(raw) <= sys.maxsize:
            raise ValueError('Invalid GPU memory setting: ' + name)
        return int(raw)

    def refresh_memory_budget(self):
        free, total = self.cuda.memory_info()
        reserve = self.memory_setting('INU_GPU_MEMORY_RESERVE_BYTES', max(512 * 1024**2, total // 10), 0)
        requested = self.memory_setting('INU_GPU_MEMORY_BUDGET_BYTES', total, 4096)
        owned = self.metrics['device_buffer_bytes']
        self.device_budget = min(requested, owned + max(0, free - reserve))
        # Keep room for one fresh or residual window when placing full fields.
        self.window_reserve = min(MAX_MEMORY_PARTICLES * 43 + 43,
                                  max(43, self.device_budget // 4))
        self.metrics.update(device_free_bytes=free, device_total_bytes=total,
                            device_reserve_bytes=reserve, device_budget_bytes=self.device_budget,
                            particle_window_reserved_bytes=self.window_reserve)

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

    def prewarm_kernels(self):
        # Launch the actual entrypoints with no active particles before field allocation.
        # This forces their fixed resource footprint without touching trajectory state.
        start = time.perf_counter()
        self.kernel_resources = {}
        self.metrics['kernel_prewarm_count'] = 0
        self.metrics['device_free_before_prewarm_bytes'] = self.cuda.memory_info()[0]
        for phase, function, args in (
                ('fresh', self.fn_fresh, [I(0), I(0)]),
                ('residual', self.fn_residual, [I(0), I(0), *[D(0) for _ in RESIDUAL_ARRAYS]])):
            attributes = self.cuda.function_attributes(function)
            self.kernel_resources[phase] = attributes
            block = min(32, attributes['max_threads_per_block'])
            values = (P * len(args))(*(c.cast(c.byref(arg), P) for arg in args))
            self.cuda.check(self.cuda.launch(function, 1, 1, 1, block, 1, 1, 0, None, values, None),
                            'prewarm ' + phase)
            self.cuda.check(self.cuda.sync(), 'synchronize prewarm ' + phase)
            self.metrics['kernel_prewarm_count'] += 1
        self.metrics['kernel_resources'] = self.kernel_resources
        self.metrics['device_free_after_prewarm_bytes'] = self.cuda.memory_info()[0]
        self.metrics['kernel_prewarm_ms'] = (time.perf_counter() - start) * 1000

    def release(self, name):
        old = self.buffers.pop(name, None)
        if old is None:
            return
        host = self.mapped_buffers.pop(name, None)
        if host is None:
            self.cuda.release_buffer(old[0])
            self.metrics['device_buffer_bytes'] -= old[1]
        else:
            self.cuda.release_mapped_buffer(host)
            self.metrics['mapped_host_bytes'] -= old[1]
        self.metrics['buffer_free_count'] += 1
        self.update_mapped_metrics()

    def update_mapped_metrics(self):
        self.metrics['mapped_buffer_names'] = sorted(self.mapped_buffers)
        total = sum(self.buffers[name][1] for name in self.mapped_buffers if name in ALL_OUTPUTS)
        self.metrics['mapped_output_bytes'] = total
        self.metrics['peak_mapped_output_bytes'] = max(self.metrics.get('peak_mapped_output_bytes', 0), total)
        scratch = sum(self.buffers[name][1] for name in self.mapped_buffers if name in SCRATCH_ARRAYS)
        self.metrics['mapped_scratch_bytes'] = scratch
        self.metrics['peak_mapped_scratch_bytes'] = max(self.metrics.get('peak_mapped_scratch_bytes', 0), scratch)

    def memory(self, name, size, *, window=False, force_mapped=False):
        size = max(size, 1)
        old = self.buffers.get(name)
        limit = self.device_budget if window else max(0, self.device_budget - self.window_reserve)
        mapped = name in self.mapped_buffers
        needed = 0 if old is not None and old[1] >= size else size - (old[1] if old and not mapped else 0)
        if (not window and not mapped and not force_mapped and name not in PARTICLE_ARRAYS + RESIDUAL_ARRAYS + SCRATCH_ARRAYS
                and self.metrics['device_buffer_bytes'] + needed > limit):
            # Completed windows must not force persistent fields into mapped storage.
            if old is not None:
                self.release(name)
                old = None
                needed = size
            for idle in RESIDUAL_ARRAYS + SCRATCH_ARRAYS:
                self.release(idle)
                if idle in SCRATCH_ARRAYS:
                    getattr(self.state, idle).p = 0
            restore = []
            for idle in PARTICLE_ARRAYS:
                if self.metrics['device_buffer_bytes'] + needed + sum(width for _, width in restore) <= limit:
                    break
                if idle not in self.buffers or idle not in self.host_arrays:
                    continue
                width = DTYPE_SIZE[self.descriptors[idle]['dtype']]
                if self.buffers[idle][1] <= width:
                    continue
                self.release(idle)
                restore.append((idle, width))
            for idle, width in restore:
                pointer = self.memory(idle, width)
                getattr(self.state, idle).p = pointer.value
                self.upload(idle, memoryview(self.host_arrays[idle])[:width])
        if old is not None and old[1] >= size and (mapped or not force_mapped and self.metrics['device_buffer_bytes'] <= limit):
            self.metrics['buffer_reuse_count']+=1
            return old[0]
        if old is not None:
            self.release(name)
        pointer = None
        if not force_mapped and self.metrics['device_buffer_bytes'] + size <= limit:
            try:
                pointer = self.cuda.buffer(size)
            except CudaError as exc:
                if exc.code != 2:
                    raise
                self.metrics['device_allocation_oom'] += 1
                if window:
                    raise
        elif window and not force_mapped:
            raise CudaError(2, 'particle window exceeds device memory budget', b'CUDA_ERROR_OUT_OF_MEMORY')
        if pointer is None:
            host, pointer = self.cuda.mapped_buffer(size)
            self.mapped_buffers[name] = host
            self.metrics['mapped_host_bytes'] += size
            self.metrics['peak_mapped_host_bytes'] = max(self.metrics['peak_mapped_host_bytes'], self.metrics['mapped_host_bytes'])
        else:
            self.metrics['device_buffer_bytes'] += size
            self.metrics['peak_device_buffer_bytes'] = max(self.metrics['peak_device_buffer_bytes'], self.metrics['device_buffer_bytes'])
        self.buffers[name] = (pointer, size)
        self.metrics['buffer_allocation_count']+=1
        self.update_mapped_metrics()
        return pointer

    def upload(self, name, data, offset=0):
        size = len(data)
        if not size:
            return
        host = (c.c_byte * size).from_buffer(data)
        start = time.perf_counter()
        if name in self.mapped_buffers:
            # One GPU owns updates between launches; the host touches this memory only after synchronization.
            c.memmove(self.mapped_buffers[name].value + offset, host, size)
            self.metrics['mapped_host_copy_bytes'] += size
            self.metrics['mapped_host_copy_ms'] += (time.perf_counter() - start) * 1000
        else:
            self.cuda.check(self.cuda.h2d(D(self.buffers[name][0].value + offset), c.cast(host, P), size), 'cuMemcpyHtoD ' + name)
            self.metrics['h2d_bytes'] += size
            self.metrics['h2d_ms'] += (time.perf_counter() - start) * 1000

    def zero(self, name, size):
        start = time.perf_counter()
        if name in self.mapped_buffers:
            c.memset(self.mapped_buffers[name].value, 0, size)
        else:
            self.cuda.check(self.cuda.memset_d8(self.buffers[name][0], 0, size), 'cuMemsetD8 ' + name)
        self.metrics['zero_fill_bytes'] += size
        self.metrics['zero_fill_count'] += 1
        self.metrics['zero_fill_ms'] += (time.perf_counter() - start) * 1000

    def prepare_particle_windows(self, names, count, fresh):
        # All allocations finish before a trajectory can mutate shared output.
        self.refresh_memory_budget()
        window_names = PARTICLE_ARRAYS + RESIDUAL_ARRAYS + SCRATCH_ARRAYS
        retained = sum(size for name, (_, size) in self.buffers.items()
                       if name in window_names and name not in self.mapped_buffers)
        zero_bytes = 0 if fresh else sum(DTYPE_SIZE[self.descriptors[name]['dtype']]
                                        for name in PARTICLE_ARRAYS if name in self.host_arrays)
        if not fresh:
            self.spill_for_window(43 + 24 - retained)
        unit = sum(DTYPE_SIZE[self.descriptors[name]['dtype']] for name in names)
        columns = (self.state.ReceptorNumber + 1, self.state.NS)
        scratch_unit = columns[0] * 8 + columns[1] * 4
        if any(n < 1 or n > INT32_MAX for n in columns) or scratch_unit > sys.maxsize:
            raise ValueError('Invalid particle scratch dimensions')
        extra = unit * int(fresh)
        minimum = unit + extra
        mapped_scratch = minimum + scratch_unit + (43 if not fresh else 0) > self.device_budget
        per_row = unit + (0 if mapped_scratch else scratch_unit)
        target = min(self.window_reserve, min(count, MAX_MEMORY_PARTICLES) * per_row + extra)
        # Plan the same rows as releasing all windows; actual allocations still count retained bytes.
        self.spill_for_window(max(per_row + extra, target) + zero_bytes - retained)
        available = max(0, self.device_budget - self.metrics['device_buffer_bytes'] + retained - zero_bytes)
        capacity = min(MAX_MEMORY_PARTICLES, count, max(0, (available - extra) // per_row))
        if mapped_scratch:
            capacity = min(capacity, 1)
        required = {name: ((capacity + int(fresh)) * DTYPE_SIZE[self.descriptors[name]['dtype']], False)
                    for name in names}
        required.update({name: (capacity * column_count * width, mapped_scratch)
                         for name, width, column_count in zip(SCRATCH_ARRAYS, (8, 4), columns)})
        if not fresh:
            required.update({name: (DTYPE_SIZE[self.descriptors[name]['dtype']], False)
                             for name in PARTICLE_ARRAYS if name in self.host_arrays})
        projected = self.metrics['device_buffer_bytes']
        replace = []
        for name, (size, mapped) in required.items():
            old = self.buffers.get(name)
            was_mapped = name in self.mapped_buffers
            if old is not None and old[1] >= size and was_mapped == mapped:
                continue
            replace.append(name)
            projected += (0 if mapped else size) - (old[1] if old and not was_mapped else 0)
        for name in window_names if projected > self.device_budget else replace:
            self.release(name)
        while capacity > 0:
            try:
                if not fresh:
                    for name in PARTICLE_ARRAYS:
                        if name not in self.host_arrays:
                            continue
                        width = DTYPE_SIZE[self.descriptors[name]['dtype']]
                        pointer = self.memory(name, width, window=True)
                        getattr(self.state, name).p = pointer.value
                        self.upload(name, memoryview(self.host_arrays[name])[:width])
                for name in names:
                    size = (capacity + int(fresh)) * DTYPE_SIZE[self.descriptors[name]['dtype']]
                    pointer = self.memory(name, size, window=True)
                    if hasattr(self.state, name):
                        getattr(self.state, name).p = pointer.value
                for name, width, column_count in zip(SCRATCH_ARRAYS, (8, 4), columns):
                    size = capacity * column_count * width
                    if size > sys.maxsize:
                        raise ValueError('Particle scratch exceeds host addressable size')
                    pointer = self.memory(name, size, window=True, force_mapped=mapped_scratch)
                    scratch = getattr(self.state, name)
                    scratch.p = pointer.value
                    scratch.d[:] = (capacity, column_count)
                self.metrics['particle_scratch_bytes_per_row'] = scratch_unit
                self.metrics['particle_scratch_capacity'] = capacity
                self.metrics['particle_scratch_mapped'] = mapped_scratch
                return capacity
            except CudaError as exc:
                if exc.code != 2:
                    raise
                for name in window_names:
                    self.release(name)
                if capacity == 1 and not mapped_scratch:
                    mapped_scratch = True
                else:
                    capacity //= 2
                self.metrics['particle_window_retries'] += 1
        raise RuntimeError('GPU memory cannot hold one particle window; results unavailable; no CPU trajectory fallback')

    def spill_for_window(self, needed):
        candidates = sorted(((size, name) for name, (_, size) in self.buffers.items()
                             if name not in self.mapped_buffers and name not in PARTICLE_ARRAYS + RESIDUAL_ARRAYS + SCRATCH_ARRAYS), reverse=True)
        for size, name in candidates:
            if self.metrics['device_buffer_bytes'] + needed <= self.device_budget:
                break
            old = self.buffers[name][0]
            host, device = self.cuda.mapped_buffer(size)
            try:
                start = time.perf_counter()
                self.cuda.check(self.cuda.d2h(host, old, size), 'spill ' + name)
                self.metrics['d2h_ms'] += (time.perf_counter() - start) * 1000
                self.metrics['d2h_bytes'] += size
            except BaseException:
                self.cuda.release_mapped_buffer(host)
                raise
            self.cuda.release_buffer(old)
            self.buffers[name] = (device, size)
            self.mapped_buffers[name] = host
            self.metrics['device_buffer_bytes'] -= size
            self.metrics['mapped_host_bytes'] += size
            self.metrics['peak_mapped_host_bytes'] = max(self.metrics['peak_mapped_host_bytes'], self.metrics['mapped_host_bytes'])
            self.metrics['buffer_allocation_count'] += 1
            self.metrics['buffer_free_count'] += 1
            if hasattr(self.state, name):
                getattr(self.state, name).p = device.value
            self.update_mapped_metrics()

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
            zero_fill = 'zero_fill' in descriptor
            if zero_fill and (descriptor['zero_fill'] is not True or header.get('command') != 'init' or
                              name != 'Conz5d' or descriptor.get('dtype') != 'Single' or
                              descriptor.get('is_null') is not False or size == 0):
                raise ValueError('Invalid zero_fill descriptor: '+name)
            wire_size = 0 if zero_fill else size
            if cursor + wire_size > total:
                raise ValueError('Payload array exceeds frame length: '+name)
            shape = descriptor.get('shape', [])
            is_null = descriptor.get('is_null', False)
            match = re.fullmatch(r'Arr<(.+),(\d+)>', ARRAY_CPP[name])
            if not is_null and (descriptor['dtype'] != WIRE_DTYPES.get(name, CPP_DTYPE.get(match[1])) or
                                len(shape) != int(match[2])):
                raise ValueError('Array dtype/rank mismatch: '+name)
            if any(type(d) is not int or not 0 <= d <= INT32_MAX for d in shape) or (is_null and size != 0):
                raise ValueError('Invalid array shape/null payload: '+name)
            if not is_null and (size * (2 if name in DOUBLE_STORAGE_FIELDS else 1) > sys.maxsize or
                                math.prod(shape)*DTYPE_SIZE[descriptor['dtype']] != size):
                raise ValueError('Shape/bytes mismatch: '+name)
            cursor += wire_size
        if cursor != total:
            raise ValueError('Payload length mismatch')
        for name, value in header.get('scalars', {}).items():
            if name not in INPUT_SCALARS:
                raise ValueError('Unknown input scalar: '+name)
            scalar_type = dict(State._fields_)[name]
            if scalar_type is c.c_bool:
                valid = type(value) is bool
            elif scalar_type in (c.c_float, c.c_double):
                valid = type(value) in (int, float) and math.isfinite(value) and math.isfinite(scalar_type(value).value)
            else:
                valid = type(value) is int and scalar_type(value).value == value
            if not valid:
                raise ValueError('Invalid scalar value: '+name)
        for descriptor in descriptors:
            name, size = descriptor['name'], descriptor['bytes']
            shape = descriptor.get('shape', [])
            is_null = descriptor.get('is_null', False)
            if 'zero_fill' in descriptor:
                pointer = self.memory(name, size)
                self.zero(name, size)
                self.descriptors[name] = {k: v for k, v in descriptor.items() if k != 'zero_fill'}
                arr = getattr(self.state, name)
                arr.p = pointer.value
                arr.d[:] = shape
                continue
            retained = name in PARTICLE_ARRAYS + RESIDUAL_ARRAYS
            validated = name in ('TransientDepoMode', 'ParticleMode', 'DecayRate', 'TransientDepoVdep',
                                'TransientDepoVsed', 'ParticleVdep', 'ParticleVsed', 'WetDepoDropVelocity', 'DZK')
            if size and not is_null and not retained and not validated:
                factor = 2 if name in DOUBLE_STORAGE_FIELDS else 1
                pointer = self.memory(name, size * factor)
                transfer_size = TRANSFER_BYTES // factor
                for offset in range(0, size, transfer_size):
                    read_start = time.perf_counter()
                    data = read_exact(sys.stdin.buffer, min(transfer_size, size - offset))
                    self.metrics['pipe_read_wait_ms'] += (time.perf_counter() - read_start) * 1000
                    if factor == 2:
                        values = memoryview(data).cast('f')
                        if any(not math.isfinite(value) for value in values):
                            raise ValueError('nonfinite Single input: ' + name)
                        data = bytearray(array('d', values).tobytes())
                    self.upload(name, data, offset * factor)
                self.descriptors[name] = descriptor
                arr = getattr(self.state, name)
                arr.p = pointer.value
                arr.d[:] = shape
                continue
            read_start=time.perf_counter()
            data = read_exact(sys.stdin.buffer, size)
            self.metrics['pipe_read_wait_ms']+=(time.perf_counter()-read_start)*1000
            if size:
                if name in ('TransientDepoMode', 'ParticleMode'):
                    code = 'i' if name == 'TransientDepoMode' else 'B'
                    modes = memoryview(data).cast(code)
                    if any(value not in (0, 1, 2) for value in modes):
                        raise ValueError('Invalid deposition mode: '+name)
                    self.active_deposition_arrays[name] = any(modes)
                if name in ('DecayRate', 'TransientDepoVdep', 'TransientDepoVsed', 'ParticleVdep', 'ParticleVsed'):
                    code = 'd' if descriptor['dtype'] == 'Double' else 'f'
                    if any(not math.isfinite(value) or (name != 'DecayRate' and value < 0)
                           for value in memoryview(data).cast(code)):
                        raise ValueError('Invalid physical input: '+name)
            if is_null or size == 0:
                if name in ('TransientDepoMode', 'ParticleMode'):
                    self.active_deposition_arrays[name] = False
                if hasattr(self.state, name):
                    arr = getattr(self.state, name)
                    arr.p = 0
                    arr.d[:] = [0] * len(arr.d)
                self.descriptors[name] = descriptor
                if hasattr(self, 'mapped_buffers'):
                    self.release(name)
                    self.host_arrays.pop(name, None)
                    self.validation_arrays.pop(name, None)
                continue
            self.descriptors[name] = descriptor
            if name in PARTICLE_ARRAYS or name in RESIDUAL_ARRAYS:
                self.host_arrays[name] = data
                if name in RESIDUAL_ARRAYS:
                    continue
                element_size = DTYPE_SIZE[descriptor['dtype']]
                pointer = self.memory(name, element_size, window=True)
                self.upload(name, memoryview(data)[:element_size])
            else:
                if name in ('WetDepoDropVelocity', 'DZK'):
                    self.validation_arrays[name] = data
                pointer = self.memory(name, size)
                self.upload(name, data)
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
        s = self.state
        if s.Topo not in (0, 1) or s.IStatistics not in (0, 2, 3, 4):
            raise ValueError('Unsupported topography or meteorology mode')
        if self.state.ISTATIONAER not in (0, 1):
            raise ValueError('Unsupported runtime mode')
        if not 0 <= s.ReceptorNumber <= MAX_RECEPTORS or not 0 < s.NS <= MAX_SLICES:
            raise ValueError('Receptor/slice local capacity exceeded')
        if not s.UseFixedRndSeedVal and (not s.RnGSeed1 or not s.RnGSeed2):
            raise ValueError('Nonfixed GPU execution requires nonzero master seeds')
        if s.WetDeposition and s.WetDopoDropDrift:
            descriptor = self.descriptors.get('WetDepoDropVelocity', {})
            raw = getattr(self, 'validation_arrays', {}).get('WetDepoDropVelocity')
            if descriptor.get('shape') != [10, 9] or raw is None:
                raise ValueError('Wet droplet velocity table must have shape [10, 9]')
            values = memoryview(raw).cast('f' if descriptor['dtype'] == 'Single' else 'd')
            rows = [values[i * 9:(i + 1) * 9] for i in range(10)]
            if (any(not math.isfinite(v) for v in values) or
                any(row[0] <= 0 or any(v <= 0 for v in row[1:5]) or any(v < 0 for v in row[5:]) for row in rows) or
                any(rows[i][0] >= rows[i + 1][0] for i in range(9))):
                raise ValueError('Invalid wet droplet velocity table values')
            if not math.isfinite(s.WetDepoPrecipitation) or s.WetDepoPrecipitation < 0 or not 0 <= s.WetDepoRW <= 1:
                raise ValueError('Invalid wet droplet precipitation or removal probability')
            dz = getattr(self, 'validation_arrays', {}).get('DZK')
            if dz is None:
                raise ValueError('Wet droplet drift requires DZK')
            layers = memoryview(dz).cast('f')
            if len(layers) <= s.NKK or any(not math.isfinite(v) or v <= 0 for v in layers[1:s.NKK + 1]):
                raise ValueError('Invalid positive wet droplet layer thickness')
        if s.LogLevel not in (0, 1, 2, 3):
            raise ValueError('Unsupported diagnostic log level')
        if any(getattr(self, 'active_deposition_arrays', {}).values()) and not s.DepositionExist:
            raise ValueError('Active dry-deposition mode requires DepositionExist')
        for name in ('NII', 'NJJ', 'NKK', 'NXL', 'NYL'):
            if getattr(s, name) <= 0:
                raise ValueError('Invalid grid size: '+name)
        for name in ('DXK', 'DYK', 'GralDx', 'GralDy', 'GralDz', 'TAUS'):
            if not math.isfinite(getattr(s, name)) or getattr(s, name) <= 0:
                raise ValueError('Invalid positive physical scalar: '+name)
        for name in ('AdaptiveRoughnessMax', 'WetDepoRW', 'VegetationDepoGasFactor', 'VegetationDepoPMFactor'):
            if not math.isfinite(getattr(s, name)) or getattr(s, name) < 0:
                raise ValueError('Invalid nonnegative physical scalar: '+name)

        def require(name, shape):
            array = getattr(s, name)
            if not array.p or any(actual < expected for actual, expected in zip(array.d, shape)):
                raise ValueError('Missing or undersized required array: '+name)

        require('SourceGroups', (1,))
        groups = s.SourceGroups.d[0]
        flow = (s.NII + 2, s.NJJ + 2)
        concentration = (s.NXL + 2, s.NYL + 2)
        require('AHK', flow)
        require('KKART', flow)
        require('HorSlices', (s.NS,))
        require('SourceType', (1,))  # Residual IntStandCalculate reads the upstream inactive index zero.
        require('Conz3dDouble', (*concentration, s.NS, groups))
        if s.Topo == 1 or s.BuildingsExist:
            for name in ('UK', 'VK'):
                require(name, (*flow, s.NKK + 1))
            require('WK', (*flow, s.NKK + 2))
            require('HOKART', (s.NKK + 1,))
            require('DZK', (s.NKK + 1,))
            require('CUTK', flow)
        if s.Topo == 0 and not s.BuildingsExist or s.IStatistics in (0, 3):
            if s.MetProfileNumb < 1:
                raise ValueError('Invalid meteorology profile count')
            require('MeasurementHeight', (s.MetProfileNumb + 1,))
        if s.Topo == 0 and not s.BuildingsExist:
            require('ObsWindU', (s.MetProfileNumb + 1,))
            require('ObsWindV', (s.MetProfileNumb + 1,))
        if s.IStatistics in (0, 3):
            require('U0', (s.MetProfileNumb + 1,))
            require('V0', (s.MetProfileNumb + 1,))
        if s.ISTATIONAER == 0:
            if s.NKK_Transient < 1:
                raise ValueError('Invalid transient layer count')
            require('Conz5d', (*flow, s.NKK_Transient + 2, groups))
            require('HoKartTrans', (s.NKK_Transient + 1,))
            require('DZK_Trans', (s.NKK_Transient + 1,))
            for name in ('TransientDepoMode', 'TransientDepoVdep', 'TransientDepoVsed'):
                require(name, (groups,))
        if s.DepositionExist:
            require('COV', flow)
        if s.DepositionExist or s.WetDeposition:
            require('Depo_conz', (*concentration, groups))
        if s.Odour:
            for name in ('Conz3dp', 'Conz3dm'):
                require(name, (*concentration, s.NS, groups))
        if s.WriteVerticalConcentration:
            if s.ISTATIONAER != 0:
                raise ValueError('Vertical concentration output requires transient mode')
            require('ConzSsum', (*flow, s.NKK_Transient + 2))
        if s.TunnelEntr:
            require('TUN_ENTR', flow)
        if s.TunnelOppLane:
            require('OPP_LANE', flow)
        if s.AdaptiveRoughnessMax > 0:
            for name in ('OLGral', 'USternGral', 'Z0Gral'):
                require(name, flow)
        if s.LogLevel:
            require('LogReflexions', (6,))
            require('Log_Timesteps', (6,))
        if s.ReceptorsAvailable:
            for name in ('ReceptorX', 'ReceptorY', 'ReceptorZ', 'ReceptorIInd', 'ReceptorJInd', 'ReceptorNearbyBuilding'):
                require(name, (s.ReceptorNumber + 1,))
            for name in ('ReceptorConc', 'ReceptorParticleMaxConc'):
                require(name, (s.ReceptorNumber + 1, groups))
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
            detail = 'droplet state invalid' if self.state.DeviceError == 2 else 'nonfinite trajectory'
            particle = self.state.DeviceErrorParticle
            if self.state.DeviceErrorPhase == 2 and particle < 0:
                mapping = self.residual_error_mapping
                relative = particle - INT32_MIN
                if mapping is None or not 0 <= relative < mapping[1]:
                    raise RuntimeError('Invalid residual device error mapping; whole run aborted; results unavailable')
                particle = mapping[0] + relative
            raise RuntimeError(f'GPU {detail}; phase={self.state.DeviceErrorPhase}, '
                               f'particle={particle}; whole run aborted; results unavailable')

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
        self.execution_failed = True
        count = header['count']
        first = header['first']
        if type(count) is not int or not 0 <= count <= INT32_MAX:
            raise ValueError('Particle count must be an integer in [0, INT32_MAX]')
        if type(first) is not int or not 0 <= first <= INT32_MAX:
            raise ValueError('First particle must be an integer in [0, INT32_MAX]')
        phase = header['phase']
        if phase not in self.phase_counts:
            raise ValueError('Unknown phase')
        if 'UseFixedRndSeedVal' in header.get('scalars', {}):
            raise ValueError('Execute request cannot change seed mode')
        if not self.state.UseFixedRndSeedVal:
            seeds = header.get('scalars', {})
            if any(type(seeds.get(name)) is not int or not 0 < seeds[name] < 2**32 for name in ('RnGSeed1', 'RnGSeed2')):
                raise ValueError('Nonfixed execute request requires two nonzero master seeds')
        self.receive(header)
        self.guard()
        if phase == 'residual' and self.state.ISTATIONAER != 0:
            raise ValueError('Residual execution requires transient mode')
        if phase == 'fresh':
            for name in PARTICLE_ARRAYS:
                descriptor = self.descriptors.get(name, {})
                if first < 1 or name not in self.host_arrays or first + count > descriptor.get('shape', [0])[0]:
                    raise ValueError('Fresh particle range exceeds '+name)
        else:
            base = self.residual_trace_base
            if type(base) is not int or base < 0:
                raise ValueError('Invalid residual trace base')
            for name in RESIDUAL_ARRAYS:
                descriptor = self.descriptors.get(name, {})
                if descriptor.get('is_null') or descriptor.get('shape') != [count]:
                    raise ValueError('Residual batch length mismatch: '+name)
                data = self.host_arrays.get(name)
                if count and (data is None or len(data) != count * 4):
                    raise ValueError('Residual batch buffer too small: '+name)
        elapsed = 0.0
        # Start conservatively; a single trajectory can still be long. Actual timing is recorded.
        chunk = min(65536, max(1, int(header.get('chunk', 4096))))
        block=int(header.get('block',128))
        if block not in (128,256):
            raise ValueError('Supported block sizes: 128, 256')
        block = min(block, self.kernel_resources[phase]['max_threads_per_block'])
        self.metrics[phase + '_block_size'] = block
        capacity = self.prepare_particle_windows(PARTICLE_ARRAYS if phase == 'fresh' else RESIDUAL_ARRAYS, count, phase == 'fresh') if count else 0
        for offset in range(0, count, max(1, capacity)):
            window_count = min(capacity, count - offset)
            if phase == 'fresh':
                self.state.ParticleWindowFirst = first + offset
                for name in PARTICLE_ARRAYS:
                    width = DTYPE_SIZE[self.descriptors[name]['dtype']]
                    data = memoryview(self.host_arrays[name])
                    self.upload(name, data[:width])
                    begin = (first + offset) * width
                    self.upload(name, data[begin:begin + window_count * width], width)
            else:
                for name in RESIDUAL_ARRAYS:
                    self.upload(name, memoryview(self.host_arrays[name])[offset * 4:(offset + window_count) * 4])
            self.bind()
            local_offset = 0
            while local_offset < window_count:
                part = min(chunk, window_count - local_offset)
                if phase == 'fresh':
                    elapsed += self.launch(self.fn_fresh, part, [I(first + offset + local_offset), I(part)], block)
                else:
                    global_base = self.residual_trace_base + offset + local_offset
                    self.check_device_error()
                    if global_base <= INT32_MAX:
                        part = min(part, INT32_MAX - global_base + 1)
                        encoded_base = global_base
                        self.residual_error_mapping = None
                    else:
                        encoded_base = INT32_MIN
                        self.residual_error_mapping = (global_base, part)
                    pointers = [D(self.buffers[name][0].value + local_offset * 4) for name in RESIDUAL_ARRAYS]
                    elapsed += self.launch(self.fn_residual, part, [I(part), I(encoded_base), *pointers], block)
                local_offset += part
            self.metrics[phase + '_memory_windows'] += 1
            # Preserve scalar changes before the next window rebinds the pointer state.
            self.read_state()
        if phase == 'residual':
            self.residual_trace_base += count
        self.phase_counts[phase]+=count
        self.execution_failed = False
        return {'status':'ok','kernel_ms':elapsed,'phase':phase,'count':count,'payload_bytes':0,
                'memory_window_capacity':capacity,'memory_windows':(count+capacity-1)//capacity if capacity else 0,
                'block_size':block,
                'rng_seed1':self.state.RnGSeed1,'rng_seed2':self.state.RnGSeed2,
                'metrics_cumulative':dict(self.metrics)}

    def read_state(self):
        start = time.perf_counter()
        self.cuda.check(self.cuda.d2h(c.cast(c.byref(self.state), P), self.state_address, c.sizeof(State)), 'read State')
        self.metrics['state_d2h_ms'] += (time.perf_counter() - start) * 1000
        self.metrics['state_d2h_bytes'] += c.sizeof(State)

    def result(self):
        self.check_device_error()
        if getattr(self, 'execution_failed', False):
            raise RuntimeError('Failed GPU execution; partial results unavailable')
        descriptors=[]
        cursor=0
        trace_outputs=tuple(name for name in ('Trace', 'StepTrace', 'ResidualTrace')
                            if self.state.TraceEnabled and getattr(self.state, name).p)
        outputs = OUTPUTS if self.state.ISTATIONAER == 0 else tuple(n for n in OUTPUTS if n not in ('Conz5d', 'EmissionPerSG'))
        if self.state.DepositionExist or self.state.WetDeposition:
            outputs += ('Depo_conz',)
        if self.state.Odour:
            outputs += ('Conz3dp', 'Conz3dm')
        if self.state.WriteVerticalConcentration:
            outputs += ('ConzSsum',)
        if self.state.LogLevel:
            outputs += ('LogReflexions', 'Log_Timesteps')
        for name in outputs+trace_outputs:
            descriptor=dict(self.descriptors[name])
            descriptor['offset'] = cursor
            descriptors.append(descriptor)
            cursor += descriptor['bytes']
        def chunks():
            for name in outputs+trace_outputs:
                size=self.descriptors[name]['bytes']
                factor = 2 if name in DOUBLE_STORAGE_FIELDS else 1
                for offset in range(0, size * factor, TRANSFER_BYTES):
                    length = min(TRANSFER_BYTES, size * factor - offset)
                    start = time.perf_counter()
                    mapped = getattr(self, 'mapped_buffers', {}).get(name)
                    if mapped is not None:
                        data = c.string_at(mapped.value + offset, length)
                        self.metrics['mapped_host_copy_ms'] += (time.perf_counter() - start) * 1000
                        self.metrics['mapped_host_copy_bytes'] += length
                    else:
                        data = bytearray(length)
                        host = (c.c_byte * length).from_buffer(data)
                        self.cuda.check(self.cuda.d2h(c.cast(host, P), D(self.buffers[name][0].value + offset), length), 'read ' + name)
                        self.metrics['d2h_ms'] += (time.perf_counter() - start) * 1000
                        self.metrics['d2h_bytes'] += length
                    if factor == 2:
                        values = memoryview(data).cast('d')
                        if any(not math.isfinite(value) for value in values):
                            raise ValueError('nonfinite Double output: ' + name)
                        converted = array('f', values)
                        if any(not math.isfinite(value) for value in converted):
                            raise ValueError('Single output overflow: ' + name)
                        data = converted.tobytes()
                    yield data
        return {'status':'ok','scalars':{},
                'arrays':descriptors,'payload_bytes':cursor,'kernel_total_ms':self.kernel_total_ms,
                'phase_counts':self.phase_counts,'metrics_cumulative':dict(self.metrics),
                'metrics_scope':'before_result_transfer','metrics_follow':True},chunks()


def main():
    server=None
    try:
        server=Server()
        while True:
            header=read_header(sys.stdin.buffer)
            if header is None:
                break
            command=header['command']
            if command != 'init' and any('zero_fill' in d for d in header.get('arrays', [])):
                raise ValueError('zero_fill is only valid for init')
            if command=='close':
                write_frame({'status':'ok','payload_bytes':0})
                break
            if command=='init':
                server.metrics['init_count']+=1
                server.residual_trace_base = 0
                server.refresh_memory_budget()
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
                write_frame({'status':'ok','frame':'result_metrics','payload_bytes':0,
                             'metrics_cumulative':dict(server.metrics)})
            else:
                raise ValueError('Unknown command: '+command)
    except FrameWriteError:
        traceback.print_exc(file=sys.stderr)
        return 1
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
