import ctypes as c
import importlib.util
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

CUDA_ERROR_OUT_OF_MEMORY = 2
CUDA_ERROR_DEVICE_UNAVAILABLE = 46
# Module images the driver cannot use: invalid image, no binary for GPU, invalid PTX, unsupported PTX version.
# 221/223: the driver cannot JIT PTX (missing JIT library or CUDA_DISABLE_PTX_JIT); a CUBIN may still load.
REJECTED_IMAGE_CODES = (200, 209, 218, 221, 222, 223)
ATTR_KERNEL_EXEC_TIMEOUT = 17
ATTR_CAN_MAP_HOST_MEMORY = 19
ATTR_COMPUTE_MODE = 20
ATTR_TCC_DRIVER = 35
ATTR_CC_MAJOR = 75
ATTR_CC_MINOR = 76
ATTR_SINGLE_TO_DOUBLE_PERF_RATIO = 87
COMPUTE_MODES = {0: 'default', 2: 'prohibited', 3: 'exclusive_process'}


class CudaError(RuntimeError):
    def __init__(self, code, operation, name):
        self.code = code
        self.operation = operation
        super().__init__(f'{operation}: {code} {name!r}')


# Project sizes (slices, receptors) are runtime array shapes, so they never enter the
# compile options; one compiled module serves every project and keeps the cache key stable.
NVRTC_OPTIONS = (b'--std=c++11', b'--fmad=false')
NVRTC_PROGRAM = b'kernel.cu'


# ---------------------------------------------------------------------------
# Platform switch. Only the Windows branch has been run; the Linux branch follows the
# CUDA driver/NVRTC library naming and is kept deliberately small.
# ---------------------------------------------------------------------------
WINDOWS = os.name == 'nt'


class _NoDirectory:
    def close(self):
        pass


if WINDOWS:
    DRIVER_LIBRARY = 'nvcuda.dll'
    NVRTC_GLOB = 'nvrtc64_*.dll'
    NVRTC_NAME = r'nvrtc64_[0-9]+_[0-9]+\.dll'
    WHEEL_SUBDIRS = ('bin', 'bin/x86_64', 'bin/x64')
    TOOLKIT_ENV = ('CUDA_PATH',)

    def _builtins_names(major, minor):
        return [f'nvrtc-builtins64_{major}{minor}.dll']

    def _load_library(path):
        return c.WinDLL(str(path))

    def _free_library(handle):
        import _ctypes
        _ctypes.FreeLibrary(handle)

    def _library_directory(path):
        return os.add_dll_directory(str(path))

    def _toolkit_dirs(root):
        return [root / 'bin' / 'x64', root / 'bin']
else:
    DRIVER_LIBRARY = 'libcuda.so.1'
    NVRTC_GLOB = 'libnvrtc.so*'
    NVRTC_NAME = r'libnvrtc\.so(\.[0-9]+)*'
    WHEEL_SUBDIRS = ('lib',)
    TOOLKIT_ENV = ('CUDA_PATH', 'CUDA_HOME')

    def _builtins_names(major, minor):
        return [f'libnvrtc-builtins.so.{major}.{minor}', f'libnvrtc-builtins.so.{major}', 'libnvrtc-builtins.so']

    def _load_library(path):
        # RTLD_GLOBAL lets NVRTC find the preloaded builtins library.
        return c.CDLL(str(path), mode=c.RTLD_GLOBAL)

    def _free_library(handle):
        import _ctypes
        _ctypes.dlclose(handle)

    def _library_directory(path):
        return _NoDirectory()

    def _toolkit_dirs(root):
        return [root / 'lib64', root / 'lib', root / 'targets' / 'x86_64-linux' / 'lib']


def _bind(lib, name, argtypes, restype=I):
    f = getattr(lib, name)
    f.argtypes, f.restype = argtypes, restype
    return f


def _wheel_directories():
    """NVRTC from pip wheels: nvidia-cuda-nvrtc-cu12 (nvidia/cuda_nvrtc) or the CUDA 13 wheel (nvidia/cu13)."""
    result = []
    for package in ('nvidia.cuda_nvrtc', 'nvidia.cu13'):
        try:
            spec = importlib.util.find_spec(package)
        except (ImportError, ValueError):
            continue
        for location in (spec.submodule_search_locations or ()) if spec is not None else ():
            result.extend(Path(location) / sub for sub in WHEEL_SUBDIRS)
    return result


def _nvrtc_candidates(explicit, bundled, cuda_path, wheel_dirs=()):
    if explicit is not None:
        if not explicit or not Path(explicit).is_absolute():
            raise ValueError('INU_GPU_NVRTC_DIR must be a nonempty absolute path')
        directories = [Path(explicit)]
    else:
        directories = [Path(bundled)] + [Path(p) for p in wheel_dirs]
        for root in cuda_path if isinstance(cuda_path, (list, tuple)) else [cuda_path]:
            if root:
                if not Path(root).is_absolute():
                    raise ValueError('CUDA_PATH must be absolute')
                directories += _toolkit_dirs(Path(root))
    result, seen = [], set()
    for directory in directories:
        # NVRTC library names are versioned; exclude builtins and unrelated libraries.
        if not directory.is_dir():
            continue
        candidates = sorted((p for p in directory.glob(NVRTC_GLOB) if p.is_file() and re.fullmatch(NVRTC_NAME, p.name, re.IGNORECASE)), reverse=True)
        for path in candidates:
            key = os.path.normcase(str(path.resolve()))
            if key not in seen:
                seen.add(key)
                result.append(path)
    if not result:
        if explicit is not None:
            raise FileNotFoundError(f'INU_GPU_NVRTC_DIR contains no NVRTC library: {explicit}')
        raise FileNotFoundError('NVRTC library not found in the bundled directory, the nvidia-cuda-nvrtc pip wheel or '
                                'CUDA_PATH; install it (pip install nvidia-cuda-nvrtc-cu12) or set INU_GPU_NVRTC_DIR')
    return result


def nvrtc_requirement(arch):
    """Which NVRTC release line can target a device of compute capability arch (e.g. 86)."""
    if arch < 50:
        return 'compute capability below 5.0 is not supported by NVRTC 12.x or 13.x'
    if arch < 75:
        return 'this device needs NVRTC 12.x (pip install nvidia-cuda-nvrtc-cu12); NVRTC 13.x requires compute capability 7.5 or newer'
    return 'this device needs NVRTC 12.x or 13.x (pip install nvidia-cuda-nvrtc-cu12, or the CUDA 13 nvidia-cuda-nvrtc wheel)'


def _select_arch(device_arch, supported):
    choices = [arch for arch in supported if 0 < arch <= device_arch]
    if not choices:
        raise RuntimeError(f'No compatible NVRTC virtual architecture for device compute_{device_arch} '
                           f'(CC {device_arch // 10}.{device_arch % 10}); this NVRTC supports {supported}; '
                           + nvrtc_requirement(device_arch))
    return max(choices)


def _options_for_arch(arch, kind='ptx'):
    prefix = 'compute' if kind == 'ptx' else 'sm'
    return (f'--gpu-architecture={prefix}_{arch}'.encode('ascii'),) + NVRTC_OPTIONS


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
    directory = _library_directory(path.parent)
    library = None
    try:
        library = _load_library(path)
        version_fn = _bind(library, 'nvrtcVersion', [c.POINTER(I), c.POINTER(I)])
        major, minor = I(), I()
        rc = version_fn(c.byref(major), c.byref(minor))
        if rc:
            raise RuntimeError(f'nvrtcVersion failed: {rc}')
        names = _builtins_names(major.value, minor.value)
        builtins = next((path.parent / name for name in names if (path.parent / name).is_file()), None)
        if builtins is None:
            raise FileNotFoundError(f'Matching NVRTC builtins not found: {path.parent / names[0]}')
        # NVRTC loads builtins lazily; AddDllDirectory alone does not affect that lookup.
        library._inu_builtins = _load_library(builtins)
        return library, directory, builtins, (major.value, minor.value), _supported_archs(library)
    except BaseException:
        if library is not None:
            _close_nvrtc_candidate((library, directory))
        else:
            directory.close()
        raise


def _close_nvrtc_candidate(candidate):
    library, directory = candidate[:2]
    builtins = getattr(library, '_inu_builtins', None)
    try:
        _free_library(library._handle)
    finally:
        try:
            if builtins is not None:
                _free_library(builtins._handle)
        finally:
            directory.close()


def _select_toolchain(candidates, device_arch, open_candidate=None, driver_version=None, probe_candidate=None):
    """Return (path, candidate, target_arch, binary_kind) for the first NVRTC the driver accepts.

    Every candidate, including the last one, is probed with a tiny module when a probe is given,
    so an NVRTC newer than the driver is rejected here instead of failing on the real module.
    """
    open_candidate = open_candidate or _open_nvrtc_candidate
    failures = []
    for path in candidates:
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
        kind = 'ptx'
        if probe_candidate is not None:
            try:
                result, kind = probe_candidate(candidate, target)
            except BaseException:
                _close_nvrtc_candidate(candidate)
                raise
            if result in REJECTED_IMAGE_CODES:
                failures.append(f'{path}: NVRTC {version[0]}.{version[1]} output rejected by driver '
                                f'{driver_version} ({result})')
                print(f'NVRTC candidate rejected: {failures[-1]}', file=sys.stderr)
                _close_nvrtc_candidate(candidate)
                continue
            if result:
                _close_nvrtc_candidate(candidate)
                raise RuntimeError(f'Unexpected module probe error: {result}')
        return path, candidate, target, kind
    raise RuntimeError('No compatible NVRTC toolchain: ' + '; '.join(failures) +
                       '; ' + nvrtc_requirement(device_arch) + ' and an NVIDIA driver at least as new as that NVRTC')


def _device_ordinal(raw, count):
    if raw is None or raw == '':
        ordinal = 0
    elif raw.isascii() and raw.isdecimal():
        ordinal = int(raw)
    else:
        raise ValueError(f'INU_GPU_DEVICE must be a nonnegative integer, got {raw!r}')
    if count < 1:
        raise RuntimeError('No CUDA device is visible (check the NVIDIA driver and CUDA_VISIBLE_DEVICES)')
    if ordinal >= count:
        raise ValueError(f'INU_GPU_DEVICE={ordinal} is out of range: {count} CUDA device(s) visible '
                         f'(valid 0..{count - 1}; CUDA_VISIBLE_DEVICES renumbers devices)')
    return ordinal


def _driver_text(version):
    return f'{version // 1000}.{version % 1000 // 10}'


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
        try:
            self.cuda = _load_library(DRIVER_LIBRARY)
        except OSError as exc:
            raise RuntimeError(f'NVIDIA driver library {DRIVER_LIBRARY} could not be loaded ({exc}); '
                               'install the NVIDIA display driver') from exc
        driver = self.cuda
        self.init = _bind(driver, 'cuInit', [U])
        self.driver_get_version = _bind(driver, 'cuDriverGetVersion', [c.POINTER(I)])
        self.device_get_count = _bind(driver, 'cuDeviceGetCount', [c.POINTER(I)])
        self.device_get = _bind(driver, 'cuDeviceGet', [c.POINTER(I), I])
        self.device_get_name = _bind(driver, 'cuDeviceGetName', [c.c_char_p, I, I])
        self.device_total_mem = _bind(driver, 'cuDeviceTotalMem_v2', [c.POINTER(Z), I])
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
        driver_version = I()
        self.check(self.driver_get_version(c.byref(driver_version),), 'cuDriverGetVersion')
        count = I()
        self.check(self.device_get_count(c.byref(count)), 'cuDeviceGetCount')
        self.device_ordinal = _device_ordinal(os.environ.get('INU_GPU_DEVICE'), count.value)
        dev = I(); self.check(self.device_get(c.byref(dev), self.device_ordinal), 'cuDeviceGet')
        name = c.create_string_buffer(256)
        self.check(self.device_get_name(name, 256, dev), 'cuDeviceGetName')
        total = Z(); self.check(self.device_total_mem(c.byref(total), dev), 'cuDeviceTotalMem')

        def attribute(code, label):
            value = I()
            self.check(self.device_attribute(c.byref(value), code, dev), 'device attribute ' + label)
            return value.value

        cc_major = attribute(ATTR_CC_MAJOR, 'compute capability major')
        cc_minor = attribute(ATTR_CC_MINOR, 'compute capability minor')
        self.device_arch = cc_major * 10 + cc_minor
        self.can_map_host_memory = bool(attribute(ATTR_CAN_MAP_HOST_MEMORY, 'canMapHostMemory'))
        compute_mode = attribute(ATTR_COMPUTE_MODE, 'compute mode')
        self.kernel_exec_timeout = bool(attribute(ATTR_KERNEL_EXEC_TIMEOUT, 'kernel exec timeout'))
        self.device_info = {
            'ordinal': self.device_ordinal, 'device_count': count.value,
            'name': name.value.decode('utf-8', 'replace'), 'compute_capability': f'{cc_major}.{cc_minor}',
            'total_memory_bytes': total.value, 'driver_version': driver_version.value,
            'driver_cuda': _driver_text(driver_version.value),
            'compute_mode': COMPUTE_MODES.get(compute_mode, str(compute_mode)),
            'kernel_exec_timeout': self.kernel_exec_timeout,
            'tcc_driver': bool(attribute(ATTR_TCC_DRIVER, 'TCC driver')),
            'fp32_to_fp64_perf_ratio': attribute(ATTR_SINGLE_TO_DOUBLE_PERF_RATIO, 'single/double perf ratio'),
            'can_map_host_memory': self.can_map_host_memory, 'platform': sys.platform}
        if compute_mode == 2:
            raise RuntimeError(f'CUDA device {self.device_ordinal} ({self.device_info["name"]}) is in prohibited '
                               'compute mode; select another device with INU_GPU_DEVICE or change it with nvidia-smi -c')
        rc = self.ctx_create(c.byref(self.ctx), 8, dev)
        if rc == CUDA_ERROR_DEVICE_UNAVAILABLE:
            raise RuntimeError(f'CUDA device {self.device_ordinal} ({self.device_info["name"]}) is unavailable '
                               f'(compute mode {self.device_info["compute_mode"]}; another process may own it)')
        self.check(rc, 'cuCtxCreate')
        bundled = Path(__file__).parent / 'nvrtc' / 'nvidia' / 'cuda_nvrtc' / 'bin'
        candidates = _nvrtc_candidates(os.environ.get('INU_GPU_NVRTC_DIR'), bundled,
                                       [os.environ.get(name) for name in TOOLKIT_ENV], _wheel_directories())
        self.nvrtc_path, candidate, self.target_arch, self.binary_kind = _select_toolchain(
            candidates, self.device_arch, driver_version=driver_version.value, probe_candidate=self._probe_toolchain)
        self.nvrtc, self.dll_dir, self.nvrtc_builtins_path, self.nvrtc_version, supported = candidate
        self.nvrtc_options = _options_for_arch(self.target_arch, self.binary_kind)
        self._bind_nvrtc(self.nvrtc)
        query = '' if supported is not None else ' (exact target unverified; NVRTC has no architecture query)'
        info = self.device_info
        self.device_info.update(nvrtc_version=f'{self.nvrtc_version[0]}.{self.nvrtc_version[1]}',
                                nvrtc_path=str(self.nvrtc_path), target_arch=self.target_arch,
                                binary_kind=self.binary_kind)
        print(f'CUDA device {self.device_ordinal} compute_{self.device_arch}; NVRTC {self.nvrtc_version[0]}.{self.nvrtc_version[1]}; '
              f'target {"compute" if self.binary_kind == "ptx" else "sm"}_{self.target_arch}{query}; '
              f'{info["name"]}; {info["total_memory_bytes"] // 1024**2} MiB; driver {info["driver_cuda"]} ({info["driver_version"]}); '
              f'kernel_exec_timeout={int(info["kernel_exec_timeout"])}; tcc={int(info["tcc_driver"])}; '
              f'fp32:fp64={info["fp32_to_fp64_perf_ratio"]}:1; compute_mode={info["compute_mode"]}', file=sys.stderr)
        self.context_ms = (time.perf_counter() - start) * 1000

    def _bind_nvrtc(self, library):
        self.nv_create = _bind(library, 'nvrtcCreateProgram', [c.POINTER(P),c.c_char_p,c.c_char_p,I,P,P])
        self.nv_compile = _bind(library, 'nvrtcCompileProgram', [P,I,c.POINTER(c.c_char_p)])
        self.nv_log_size = _bind(library, 'nvrtcGetProgramLogSize', [P,c.POINTER(Z)])
        self.nv_log = _bind(library, 'nvrtcGetProgramLog', [P,c.c_char_p])
        self.nv_ptx_size = _bind(library, 'nvrtcGetPTXSize', [P,c.POINTER(Z)])
        self.nv_ptx = _bind(library, 'nvrtcGetPTX', [P,c.c_char_p])
        try:
            self.nv_cubin_size = _bind(library, 'nvrtcGetCUBINSize', [P,c.POINTER(Z)])
            self.nv_cubin = _bind(library, 'nvrtcGetCUBIN', [P,c.c_char_p])
        except AttributeError:
            self.nv_cubin_size = self.nv_cubin = None
        self.nv_destroy = _bind(library, 'nvrtcDestroyProgram', [c.POINTER(P)])

    def _probe_toolchain(self, candidate, target):
        """Load a tiny module as PTX, then as a CUBIN when the driver rejects the PTX.

        A CUBIN (SASS) avoids driver-side PTX JIT, so it also runs on a driver that is older than
        NVRTC within the same CUDA major release; it needs the target major to match the device.
        """
        library, _, _, _, supported = candidate
        self._bind_nvrtc(library)
        kinds = ['ptx', 'cubin']
        if os.environ.get('INU_GPU_FORCE_CUBIN') == '1':
            kinds = ['cubin']
        result = 0
        for kind in kinds:
            if kind == 'cubin' and (self.nv_cubin is None or target // 10 != self.device_arch // 10):
                break
            self.nvrtc_options = _options_for_arch(target, kind)
            try:
                image, _ = self.compile_image('extern "C" __global__ void probe() {}', kind)
            except RuntimeError as exc:
                if supported is None and 'invalid value for --gpu-architecture' in str(exc).lower():
                    result = 209
                    continue
                raise
            data = c.create_string_buffer(image, len(image))
            module = P()
            rc = self.module_load(c.byref(module), c.cast(data, P))
            if rc in REJECTED_IMAGE_CODES:
                result = rc
                continue
            self.check(rc, 'cuModuleLoadData compatibility probe')
            self.check(self.module_unload(module), 'cuModuleUnload compatibility probe')
            return 0, kind
        return result or 209, None

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
            # Same code as a failed allocation, so the memory manager's OOM handlers treat it alike.
            raise CudaError(CUDA_ERROR_OUT_OF_MEMORY, 'cuMemHostAlloc mapped (device cannot map host memory)',
                            b'CUDA_ERROR_OUT_OF_MEMORY')
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

    def compile_image(self, source, kind=None):
        """Compile with the current options; returns (PTX text with trailing NUL, or CUBIN bytes; log)."""
        kind = kind or self.binary_kind
        prog=P()
        rc=self.nv_create(c.byref(prog),source.encode('utf-8'),NVRTC_PROGRAM,0,None,None)
        if rc: raise RuntimeError(f'nvrtcCreateProgram: {rc}')
        try:
            options=(c.c_char_p*len(self.nvrtc_options))(*self.nvrtc_options)
            rc=self.nv_compile(prog,len(self.nvrtc_options),options)
            log_size=Z(); self.nv_log_size(prog,c.byref(log_size))
            log=c.create_string_buffer(max(log_size.value,1)); self.nv_log(prog,log)
            if rc: raise RuntimeError(f'nvrtcCompileProgram: {rc}; {log.value.decode(errors="replace")}')
            size_fn, data_fn = (self.nv_ptx_size, self.nv_ptx) if kind == 'ptx' else (self.nv_cubin_size, self.nv_cubin)
            size=Z(); rc=size_fn(prog,c.byref(size))
            if rc: raise RuntimeError(f'nvrtcGet{kind.upper()}Size: {rc}')
            image=c.create_string_buffer(size.value); rc=data_fn(prog,image)
            if rc: raise RuntimeError(f'nvrtcGet{kind.upper()}: {rc}')
            return image.raw,log.value.decode(errors='replace')
        finally:
            self.nv_destroy(c.byref(prog))

    def compile_ptx(self, source):
        return self.compile_image(source)

    def load_image(self, image):
        data = c.create_string_buffer(image, len(image))
        mod=P(); self.check(self.module_load(c.byref(mod),c.cast(data,P)),
                            f'cuModuleLoadData {self.binary_kind} (driver must support the NVRTC output)')
        self.modules.append(mod)
        return mod

    load_ptx = load_image

    def unload_module(self, module):
        self.check(self.module_unload(module), 'cuModuleUnload')
        self.modules.remove(module)

    def compile(self, source, function):
        image, log = self.compile_image(source)
        mod = self.load_image(image)
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
