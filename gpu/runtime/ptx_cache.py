"""Optional, bounded on-disk NVRTC module cache (PTX or CUBIN); diagnostics never use stdout."""
import base64
import binascii
import hashlib
import json
import os
from pathlib import Path
import tempfile

SCHEMA = 1
MAX_PTX = 64 * 1024 * 1024
MAX_ENTRY = 96 * 1024 * 1024
KINDS = ('ptx', 'cubin')


def digest(data):
    return hashlib.sha256(data).hexdigest()


def _local_directory(path):
    """Accept local absolute directories only (no network shares)."""
    if os.name != 'nt':
        return path.is_absolute()
    import ctypes
    if not path.drive or path.drive.startswith('\\\\'):
        return False
    # Windows drive types: removable, fixed, and RAM disk; exclude remote drives.
    return ctypes.windll.kernel32.GetDriveTypeW(str(path.anchor)) in (2, 3, 6)


def cache_path(identity):
    directory = os.environ.get('INU_GPU_CACHE_DIR')
    if not directory:
        return None, 'disabled'
    path = Path(directory)
    if not path.is_absolute() or not _local_directory(path):
        return None, 'invalid_directory'
    path = path.resolve()
    if not _local_directory(path):
        return None, 'invalid_directory'
    key = digest(json.dumps(identity, sort_keys=True, separators=(',', ':')).encode('utf-8'))
    return path / (key + '.json'), key


def valid_image(image, kind):
    if not 1 < len(image) <= MAX_PTX:
        return False
    if kind == 'ptx':
        return image.endswith(b'\0') and b'\0' not in image[:-1]
    return kind == 'cubin' and image.startswith(b'\x7fELF')


def read(path, key, kind='ptx'):
    try:
        with path.open('rb') as stream:
            raw = stream.read(MAX_ENTRY + 1)
        if len(raw) > MAX_ENTRY:
            raise ValueError('entry too large')
        entry = json.loads(raw)
        if not isinstance(entry, dict) or type(entry.get('schema')) is not int or entry['schema'] != SCHEMA or entry.get('key') != key:
            raise ValueError('schema or key mismatch')
        if entry.get('kind', 'ptx') != kind:
            raise ValueError('binary kind mismatch')
        size = entry.get('size')
        if type(size) is not int or not 1 < size <= MAX_PTX:
            raise ValueError('invalid image size')
        image = base64.b64decode(entry['ptx'], validate=True)
        if len(image) != size or digest(image) != entry.get('sha256') or not valid_image(image, kind):
            raise ValueError('invalid image content')
        return image, 'hit'
    except FileNotFoundError:
        return None, 'miss'
    except (OSError, ValueError, TypeError, KeyError, binascii.Error, UnicodeError, RecursionError, OverflowError):
        return None, 'read_error'


def write(path, key, image, kind='ptx'):
    if kind not in KINDS or not valid_image(image, kind):
        return 'write_error'
    temp_path = None
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        entry = {'schema': SCHEMA, 'key': key, 'kind': kind, 'size': len(image), 'sha256': digest(image),
                 'ptx': base64.b64encode(image).decode('ascii')}
        with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', dir=path.parent,
                                         prefix='.ptx-', suffix='.tmp', delete=False) as stream:
            temp_path = Path(stream.name)
            json.dump(entry, stream, separators=(',', ':'))
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temp_path, path)
        return 'written'
    except (OSError, ValueError):
        return 'write_error'
    finally:
        if temp_path is not None:
            try:
                temp_path.unlink(missing_ok=True)
            except OSError:
                pass
