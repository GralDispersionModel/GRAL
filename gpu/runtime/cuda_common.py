import ctypes as c
import os
import time
from pathlib import Path

P = c.c_void_p
I = c.c_int
U = c.c_uint
Z = c.c_size_t
D = c.c_uint64
NVRTC_OPTIONS = (b'--gpu-architecture=compute_120', b'--std=c++11', b'--fmad=false')
NVRTC_PROGRAM = b'kernel.cu'


def _bind(lib, name, argtypes, restype=I):
    f = getattr(lib, name)
    f.argtypes, f.restype = argtypes, restype
    return f


class Cuda:
    def __init__(self):
        self.ctx = P()
        self.dll_dir = None
        self.modules = []
        self.buffers = []
        self.events = []
        try:
            self._initialize()
        except BaseException:
            self.close()
            raise

    def _initialize(self):
        self.cuda = c.WinDLL('nvcuda.dll')
        dll_dir = Path(os.environ.get('INU_GPU_NVRTC_DIR',
                       Path(__file__).parent / 'nvrtc' / 'nvidia' / 'cuda_nvrtc' / 'bin')).resolve()
        if not (dll_dir / 'nvrtc64_120_0.dll').is_file():
            raise FileNotFoundError('Set INU_GPU_NVRTC_DIR to the CUDA NVRTC 12.9 DLL directory')
        self.dll_dir = os.add_dll_directory(str(dll_dir))
        os.environ['PATH'] = str(dll_dir) + os.pathsep + os.environ.get('PATH', '')
        self.nvrtc_path = dll_dir / 'nvrtc64_120_0.dll'
        self.nvrtc = c.WinDLL(str(self.nvrtc_path))
        driver = self.cuda
        self.init = _bind(driver, 'cuInit', [U])
        self.device_get = _bind(driver, 'cuDeviceGet', [c.POINTER(I), I])
        self.ctx_create = _bind(driver, 'cuCtxCreate_v2', [c.POINTER(P), U, I])
        self.ctx_destroy = _bind(driver, 'cuCtxDestroy_v2', [P])
        self.module_load = _bind(driver, 'cuModuleLoadData', [c.POINTER(P), P])
        self.module_unload = _bind(driver, 'cuModuleUnload', [P])
        self.get_fn = _bind(driver, 'cuModuleGetFunction', [c.POINTER(P), P, c.c_char_p])
        self.alloc = _bind(driver, 'cuMemAlloc_v2', [c.POINTER(D), Z])
        self.free = _bind(driver, 'cuMemFree_v2', [D])
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
        self.nv_version = _bind(self.nvrtc, 'nvrtcVersion', [c.POINTER(I), c.POINTER(I)])
        major, minor = I(), I()
        if self.nv_version(c.byref(major), c.byref(minor)) or (major.value, minor.value) != (12, 9):
            raise RuntimeError(f'Expected NVRTC 12.9, got {major.value}.{minor.value}')
        self.nvrtc_version = (major.value, minor.value)
        self.nvrtc_builtins_path = dll_dir / f'nvrtc-builtins64_{major.value}{minor.value}.dll'
        self.nv_create = _bind(self.nvrtc, 'nvrtcCreateProgram', [c.POINTER(P),c.c_char_p,c.c_char_p,I,P,P])
        self.nv_compile = _bind(self.nvrtc, 'nvrtcCompileProgram', [P,I,c.POINTER(c.c_char_p)])
        self.nv_log_size = _bind(self.nvrtc, 'nvrtcGetProgramLogSize', [P,c.POINTER(Z)])
        self.nv_log = _bind(self.nvrtc, 'nvrtcGetProgramLog', [P,c.c_char_p])
        self.nv_ptx_size = _bind(self.nvrtc, 'nvrtcGetPTXSize', [P,c.POINTER(Z)])
        self.nv_ptx = _bind(self.nvrtc, 'nvrtcGetPTX', [P,c.c_char_p])
        self.nv_destroy = _bind(self.nvrtc, 'nvrtcDestroyProgram', [c.POINTER(P)])
        start = time.perf_counter()
        self.check(self.init(0), 'cuInit')
        dev=I(); self.check(self.device_get(c.byref(dev),0),'cuDeviceGet')
        self.check(self.ctx_create(c.byref(self.ctx),0,dev),'cuCtxCreate')
        self.context_ms = (time.perf_counter() - start) * 1000

    def check(self, code, operation):
        if code:
            name=c.c_char_p(); self.error_name(code,c.byref(name))
            raise RuntimeError(f'{operation}: {code} {name.value!r}')

    def compile_ptx(self, source):
        prog=P()
        rc=self.nv_create(c.byref(prog),source.encode('utf-8'),NVRTC_PROGRAM,0,None,None)
        if rc: raise RuntimeError(f'nvrtcCreateProgram: {rc}')
        try:
            options=(c.c_char_p*len(NVRTC_OPTIONS))(*NVRTC_OPTIONS)
            rc=self.nv_compile(prog,len(NVRTC_OPTIONS),options)
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
        mod=P(); self.check(self.module_load(c.byref(mod),c.cast(data,P)),'cuModuleLoadData')
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
        for module in self.modules: self.module_unload(module)
        self.modules.clear()
        if self.ctx.value:
            self.ctx_destroy(self.ctx)
            self.ctx = P()
        if self.dll_dir is not None:
            self.dll_dir.close()
            self.dll_dir = None
