"""Optional, bounded on-disk NVRTC PTX cache; diagnostics never use stdout."""
import base64
import binascii
import ctypes
import hashlib
import json
import os
from pathlib import Path
import tempfile

SCHEMA = 1
MAX_PTX = 64 * 1024 * 1024
MAX_ENTRY = 96 * 1024 * 1024


def digest(data):
    return hashlib.sha256(data).hexdigest()


def cache_path(identity):
    directory = os.environ.get('INU_GPU_CACHE_DIR')
    if not directory:
        return None, 'disabled'
    path = Path(directory)
    if not path.is_absolute() or not path.drive or path.drive.startswith('\\\\'):
        return None, 'invalid_directory'
    path = path.resolve()
    if not path.drive or path.drive.startswith('\\\\'):
        return None, 'invalid_directory'
    # Windows drive types: removable, fixed, and RAM disk; exclude remote drives.
    if ctypes.windll.kernel32.GetDriveTypeW(str(path.anchor)) not in (2, 3, 6):
        return None, 'invalid_directory'
    key = digest(json.dumps(identity, sort_keys=True, separators=(',', ':')).encode('utf-8'))
    return path / (key + '.json'), key


def read(path, key):
    try:
        with path.open('rb') as stream:
            raw = stream.read(MAX_ENTRY + 1)
        if len(raw) > MAX_ENTRY:
            raise ValueError('entry too large')
        entry = json.loads(raw)
        if not isinstance(entry, dict) or type(entry.get('schema')) is not int or entry['schema'] != SCHEMA or entry.get('key') != key:
            raise ValueError('schema or key mismatch')
        size = entry.get('size')
        if type(size) is not int or not 1 < size <= MAX_PTX:
            raise ValueError('invalid PTX size')
        ptx = base64.b64decode(entry['ptx'], validate=True)
        if len(ptx) != size or digest(ptx) != entry.get('sha256') or not ptx.endswith(b'\0') or b'\0' in ptx[:-1]:
            raise ValueError('invalid PTX content')
        return ptx, 'hit'
    except FileNotFoundError:
        return None, 'miss'
    except (OSError, ValueError, TypeError, KeyError, binascii.Error, UnicodeError, RecursionError, OverflowError):
        return None, 'read_error'


def write(path, key, ptx):
    if not 1 < len(ptx) <= MAX_PTX or not ptx.endswith(b'\0') or b'\0' in ptx[:-1]:
        return 'write_error'
    temp_path = None
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        entry = {'schema': SCHEMA, 'key': key, 'size': len(ptx), 'sha256': digest(ptx),
                 'ptx': base64.b64encode(ptx).decode('ascii')}
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
