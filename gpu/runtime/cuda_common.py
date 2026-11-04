import ctypes as c
import os
import re
import sys
import time
from pathlib import Path

P = c.c_void_p
I = c.c_int
U = c.c_uint
Z = c.c_size_t
D = c.c_uint64


class CudaError(RuntimeError):
    def __init__(self, code, operation, name):
        self.code = code
        self.operation = operation
        super().__init__(f'{operation}: {code} {name!r}')


# Project sizes (slices, receptors) are runtime array shapes, so they never enter the
# compile options; one compiled module serves every project and keeps the cache key stable.
NVRTC_OPTIONS = (b'--std=c++11', b'--fmad=false')
NVRTC_PROGRAM = b'kernel.cu'


def _bind(lib, name, argtypes, restype=I):
    f = getattr(lib, name)
    f.argtypes, f.restype = argtypes, restype
    return f


def _nvrtc_candidates(explicit, bundled, cuda_path):
    if explicit is not None:
        if not explicit or not Path(explicit).is_absolute():
            raise ValueError('INU_GPU_NVRTC_DIR must be a nonempty absolute path')
        directories = [Path(explicit)]
    else:
        directories = [Path(bundled)]
        if cuda_path:
            if not Path(cuda_path).is_absolute():
                raise ValueError('CUDA_PATH must be absolute')
            directories += [Path(cuda_path) / 'bin' / 'x64', Path(cuda_path) / 'bin']
    result = []
    for directory in directories:
        # NVRTC DLL names are versioned; exclude builtins and unrelated DLLs.
        candidates = sorted((p for p in directory.glob('nvrtc64_*.dll') if p.is_file() and re.fullmatch(r'nvrtc64_[0-9]+_[0-9]+\.dll', p.name, re.IGNORECASE)), reverse=True)
        result.extend(candidates)
    if not result:
        if explicit is not None:
            raise FileNotFoundError(f'INU_GPU_NVRTC_DIR contains no NVRTC DLL: {explicit}')
        raise FileNotFoundError('NVRTC DLL not found in bundled directory or CUDA_PATH/bin; set INU_GPU_NVRTC_DIR')
    return result


def _select_arch(device_arch, supported):
    choices = [arch for arch in supported if 0 < arch <= device_arch]
    if not choices:
        raise RuntimeError(f'No compatible NVRTC virtual architecture for device compute_{device_arch}; supported: {supported}')
    return max(choices)


def _options_for_arch(arch):
    return (f'--gpu-architecture=compute_{arch}'.encode('ascii'),) + NVRTC_OPTIONS


def _supported_archs(nvrtc):
    try:
        count_fn = _bind(nvrtc, 'nvrtcGetNumSupportedArchs', [c.POINTER(I)])
        list_fn = _bind(nvrtc, 'nvrtcGetSupportedArchs', [c.POINTER(I)])
    except AttributeError:
        return None
    count = I()
    rc = count_fn(c.byref(count))
    if rc or not 0 < count.value < 1024:
        raise RuntimeError(f'nvrtcGetNumSupportedArchs failed: {rc}, count={count.value}')
    archs = (I * count.value)()
    rc = list_fn(archs)
    if rc:
        raise RuntimeError(f'nvrtcGetSupportedArchs failed: {rc}')
    return list(archs)


def _open_nvrtc_candidate(path):
    import _ctypes
    directory = os.add_dll_directory(str(path.parent))
    library = None
    try:
        library = c.WinDLL(str(path))
        version_fn = _bind(library, 'nvrtcVersion', [c.POINTER(I), c.POINTER(I)])
        major, minor = I(), I()
        rc = version_fn(c.byref(major), c.byref(minor))
        if rc:
            raise RuntimeError(f'nvrtcVersion failed: {rc}')
        builtins = path.parent / f'nvrtc-builtins64_{major.value}{minor.value}.dll'
        if not builtins.is_file():
            raise FileNotFoundError(f'Matching NVRTC builtins not found: {builtins}')
        # NVRTC loads builtins lazily; AddDllDirectory alone does not affect that lookup.
        library._inu_builtins = c.WinDLL(str(builtins))
        return library, directory, builtins, (major.value, minor.value), _supported_archs(library)
    except BaseException:
        if library is not None:
            _close_nvrtc_candidate((library, directory))
        else:
            directory.close()
        raise


def _close_nvrtc_candidate(candidate):
    import _ctypes
    library, directory = candidate[:2]
    builtins = getattr(library, '_inu_builtins', None)
    try:
        _ctypes.FreeLibrary(library._handle)
    finally:
        try:
            if builtins is not None:
                _ctypes.FreeLibrary(builtins._handle)
        finally:
            directory.close()


def _select_toolchain(candidates, device_arch, open_candidate=_open_nvrtc_candidate,
                      driver_version=None, probe_candidate=None):
    failures = []
    for index, path in enumerate(candidates):
        try:
            candidate = open_candidate(path)
        except (OSError, RuntimeError, AttributeError) as exc:
            failures.append(f'{path}: {exc}')
            continue
        library, directory, builtins, version, supported = candidate
        try:
            target = device_arch if supported is None else _select_arch(device_arch, supported)
        except RuntimeError as exc:
            failures.append(f'{path}: {exc}')
            _close_nvrtc_candidate(candidate)
            continue
        needs_probe = (probe_candidate is not None and index + 1 < len(candidates) and
                       (supported is None or driver_version is None or
                        version[0] * 1000 + version[1] * 10 > driver_version))
        if needs_probe:
            try:
                result = probe_candidate(candidate, target)
            except BaseException:
                _close_nvrtc_candidate(candidate)
                raise
            if result in (209, 222):  # no binary for GPU, or unsupported PTX version
                failures.append(f'{path}: driver rejected probe PTX ({result})')
                print(f'NVRTC candidate rejected: {failures[-1]}', file=sys.stderr)
                _close_nvrtc_candidate(candidate)
                continue
            if result:
                _close_nvrtc_candidate(candidate)
                raise RuntimeError(f'Unexpected PTX probe error: {result}')
        return path, candidate, target
    raise RuntimeError('No compatible NVRTC toolchain: ' + '; '.join(failures))


class Cuda:
    def __init__(self):
        self.ctx = P()
        self.dll_dir = None
        self.nvrtc = None
        self.modules = []
        self.buffers = []
        self.host_buffers = []
        self.events = []
        try:
            self._initialize()
        except BaseException:
            self.close()
            raise

    def _initialize(self):
        self.cuda = c.WinDLL('nvcuda.dll')
        driver = self.cuda
        self.init = _bind(driver, 'cuInit', [U])
        self.driver_get_version = _bind(driver, 'cuDriverGetVersion', [c.POINTER(I)])
        self.device_get = _bind(driver, 'cuDeviceGet', [c.POINTER(I), I])
        self.device_attribute = _bind(driver, 'cuDeviceGetAttribute', [c.POINTER(I), I, I])
        self.ctx_create = _bind(driver, 'cuCtxCreate_v2', [c.POINTER(P), U, I])
        self.ctx_destroy = _bind(driver, 'cuCtxDestroy_v2', [P])
        self.module_load = _bind(driver, 'cuModuleLoadData', [c.POINTER(P), P])
        self.module_unload = _bind(driver, 'cuModuleUnload', [P])
        self.get_fn = _bind(driver, 'cuModuleGetFunction', [c.POINTER(P), P, c.c_char_p])
        self.func_attribute = _bind(driver, 'cuFuncGetAttribute', [c.POINTER(I), I, P])
        self.alloc = _bind(driver, 'cuMemAlloc_v2', [c.POINTER(D), Z])
        self.free = _bind(driver, 'cuMemFree_v2', [D])
        self.mem_info = _bind(driver, 'cuMemGetInfo_v2', [c.POINTER(Z), c.POINTER(Z)])
        self.host_alloc = _bind(driver, 'cuMemHostAlloc', [c.POINTER(P), Z, U])
        self.host_free = _bind(driver, 'cuMemFreeHost', [P])
        self.host_device_pointer = _bind(driver, 'cuMemHostGetDevicePointer_v2', [c.POINTER(D), P, U])
        self.memset_d8 = _bind(driver, 'cuMemsetD8_v2', [D, c.c_ubyte, Z])
        self.h2d = _bind(driver, 'cuMemcpyHtoD_v2', [D, P, Z])
        self.d2h = _bind(driver, 'cuMemcpyDtoH_v2', [P, D, Z])
        self.sync = _bind(driver, 'cuCtxSynchronize', [])
        self.launch = _bind(driver, 'cuLaunchKernel', [P,U,U,U,U,U,U,U,P,c.POINTER(P),c.POINTER(P)])
        self.event_create = _bind(driver, 'cuEventCreate', [c.POINTER(P), U])
        self.event_record = _bind(driver, 'cuEventRecord', [P, P])
        self.event_sync = _bind(driver, 'cuEventSynchronize', [P])
        self.event_elapsed = _bind(driver, 'cuEventElapsedTime', [c.POINTER(c.c_float),P,P])
        self.event_destroy = _bind(driver, 'cuEventDestroy_v2', [P])
        self.error_name = _bind(driver, 'cuGetErrorName', [I,c.POINTER(c.c_char_p)])
        start = time.perf_counter()
        self.check(self.init(0), 'cuInit')
        dev=I(); self.check(self.device_get(c.byref(dev),0),'cuDeviceGet')
        cc_major, cc_minor = I(), I()
        self.check(self.device_attribute(c.byref(cc_major), 75, dev), 'device compute capability major')
        self.check(self.device_attribute(c.byref(cc_minor), 76, dev), 'device compute capability minor')
        self.device_arch = cc_major.value * 10 + cc_minor.value
        driver_version = I()
        self.check(self.driver_get_version(c.byref(driver_version),), 'cuDriverGetVersion')
        can_map = I()
        self.check(self.device_attribute(c.byref(can_map), 19, dev), 'canMapHostMemory')
        self.can_map_host_memory = bool(can_map.value)
        self.check(self.ctx_create(c.byref(self.ctx),8,dev),'cuCtxCreate')
        bundled = Path(__file__).parent / 'nvrtc' / 'nvidia' / 'cuda_nvrtc' / 'bin'
        candidates = _nvrtc_candidates(os.environ.get('INU_GPU_NVRTC_DIR'), bundled, os.environ.get('CUDA_PATH'))
        self.nvrtc_path, candidate, self.target_arch = _select_toolchain(
            candidates, self.device_arch, driver_version=driver_version.value, probe_candidate=self._probe_toolchain)
        self.nvrtc, self.dll_dir, self.nvrtc_builtins_path, self.nvrtc_version, supported = candidate
        self.nvrtc_options = _options_for_arch(self.target_arch)
        query = '' if supported is not None else ' (exact target unverified; NVRTC has no architecture query)'
        print(f'CUDA device 0 compute_{self.device_arch}; NVRTC {self.nvrtc_version[0]}.{self.nvrtc_version[1]}; '
              f'target compute_{self.target_arch}{query}', file=sys.stderr)
        self._bind_nvrtc(self.nvrtc)
        self.context_ms = (time.perf_counter() - start) * 1000

    def _bind_nvrtc(self, library):
        self.nv_create = _bind(library, 'nvrtcCreateProgram', [c.POINTER(P),c.c_char_p,c.c_char_p,I,P,P])
        self.nv_compile = _bind(library, 'nvrtcCompileProgram', [P,I,c.POINTER(c.c_char_p)])
        self.nv_log_size = _bind(library, 'nvrtcGetProgramLogSize', [P,c.POINTER(Z)])
        self.nv_log = _bind(library, 'nvrtcGetProgramLog', [P,c.c_char_p])
        self.nv_ptx_size = _bind(library, 'nvrtcGetPTXSize', [P,c.POINTER(Z)])
        self.nv_ptx = _bind(library, 'nvrtcGetPTX', [P,c.c_char_p])
        self.nv_destroy = _bind(library, 'nvrtcDestroyProgram', [c.POINTER(P)])

    def _probe_toolchain(self, candidate, target):
        library, _, _, _, supported = candidate
        self._bind_nvrtc(library)
        self.nvrtc_options = _options_for_arch(target)
        try:
            ptx, _ = self.compile_ptx('extern "C" __global__ void probe() {}')
        except RuntimeError as exc:
            if supported is None and 'invalid value for --gpu-architecture' in str(exc).lower():
                return 209
            raise
        data = c.create_string_buffer(ptx, len(ptx))
        module = P()
        rc = self.module_load(c.byref(module), c.cast(data, P))
        if rc in (209, 222):
            return rc
        self.check(rc, 'cuModuleLoadData PTX compatibility probe')
        self.check(self.module_unload(module), 'cuModuleUnload PTX compatibility probe')
        return 0

    def check(self, code, operation):
        if code:
            name=c.c_char_p(); self.error_name(code,c.byref(name))
            raise CudaError(code, operation, name.value)

    def memory_info(self):
        free, total = Z(), Z()
        self.check(self.mem_info(c.byref(free), c.byref(total)), 'cuMemGetInfo')
        return free.value, total.value

    def function_attributes(self, function):
        result = {}
        for name, attribute in (('max_threads_per_block', 0), ('local_size_bytes', 3)):
            value = I()
            self.check(self.func_attribute(c.byref(value), attribute, function), 'cuFuncGetAttribute ' + name)
            result[name] = value.value
        if result['max_threads_per_block'] < 1 or result['local_size_bytes'] < 0:
            raise RuntimeError('Invalid kernel resource attributes')
        return result

    def mapped_buffer(self, size):
        if not self.can_map_host_memory:
            raise RuntimeError('GPU cannot map pinned host memory; no CPU trajectory fallback')
        host, device = P(), D()
        self.check(self.host_alloc(c.byref(host), size, 2), 'cuMemHostAlloc mapped')
        try:
            self.check(self.host_device_pointer(c.byref(device), host, 0), 'cuMemHostGetDevicePointer')
        except BaseException:
            self.host_free(host)
            raise
        self.host_buffers.append(host)
        return host, device

    def release_buffer(self, pointer):
        self.check(self.free(pointer), 'cuMemFree')
        self.buffers.remove(pointer)

    def release_mapped_buffer(self, host):
        self.check(self.host_free(host), 'cuMemFreeHost')
        self.host_buffers.remove(host)

    def compile_ptx(self, source):
        prog=P()
        rc=self.nv_create(c.byref(prog),source.encode('utf-8'),NVRTC_PROGRAM,0,None,None)
        if rc: raise RuntimeError(f'nvrtcCreateProgram: {rc}')
        try:
            options=(c.c_char_p*len(self.nvrtc_options))(*self.nvrtc_options)
            rc=self.nv_compile(prog,len(self.nvrtc_options),options)
            log_size=Z(); self.nv_log_size(prog,c.byref(log_size))
            log=c.create_string_buffer(max(log_size.value,1)); self.nv_log(prog,log)
            if rc: raise RuntimeError(f'nvrtcCompileProgram: {rc}; {log.value.decode(errors="replace")}')
            size=Z(); rc=self.nv_ptx_size(prog,c.byref(size))
            if rc: raise RuntimeError(f'nvrtcGetPTXSize: {rc}')
            ptx=c.create_string_buffer(size.value); rc=self.nv_ptx(prog,ptx)
            if rc: raise RuntimeError(f'nvrtcGetPTX: {rc}')
            return ptx.raw,log.value.decode(errors='replace')
        finally:
            self.nv_destroy(c.byref(prog))

    def load_ptx(self, ptx):
        data = c.create_string_buffer(ptx, len(ptx))
        mod=P(); self.check(self.module_load(c.byref(mod),c.cast(data,P)),
                            'cuModuleLoadData (driver must support NVRTC-generated PTX)')
        self.modules.append(mod)
        return mod

    def unload_module(self, module):
        self.check(self.module_unload(module), 'cuModuleUnload')
        self.modules.remove(module)

    def compile(self, source, function):
        ptx, log = self.compile_ptx(source)
        mod = self.load_ptx(ptx)
        try:
            fn=P(); self.check(self.get_fn(c.byref(fn),mod,function.encode()),'cuModuleGetFunction')
            return fn, log
        except BaseException as primary:
            try:
                self.unload_module(mod)
            except Exception as cleanup:
                raise primary from cleanup
            raise

    def buffer(self, size):
        p=D(); self.check(self.alloc(c.byref(p),size),'cuMemAlloc'); self.buffers.append(p); return p

    def close(self):
        for event in self.events: self.event_destroy(event)
        self.events.clear()
        for buf in self.buffers: self.free(buf)
        self.buffers.clear()
        for host in self.host_buffers: self.host_free(host)
        self.host_buffers.clear()
        for module in self.modules: self.module_unload(module)
        self.modules.clear()
        if self.ctx.value:
            self.ctx_destroy(self.ctx)
            self.ctx = P()
        if self.dll_dir is not None:
            if self.nvrtc is not None:
                _close_nvrtc_candidate((self.nvrtc, self.dll_dir))
                self.nvrtc = None
            else:
                self.dll_dir.close()
            self.dll_dir = None
