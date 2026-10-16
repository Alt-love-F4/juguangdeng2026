"""Cooperative source-write guard for B tools; never removes another owner's lock.

Other windows must use the same path for serialization. This is not protection
against writers that ignore the protocol; callers must also compare source hashes.
"""
from contextlib import contextmanager
from datetime import datetime
import json
import os
from pathlib import Path
import uuid

ROOT = Path(__file__).resolve().parents[2]
LOCK = ROOT / 'Library/Locus/tmp/narrative-source-write.lock'


@contextmanager
def source_write_guard(operation):
    LOCK.parent.mkdir(parents=True, exist_ok=True)
    token = uuid.uuid4().hex
    owner = {'owner': 'B-rose', 'operation': operation, 'pid': os.getpid(),
             'token': token, 'time': datetime.now().astimezone().isoformat()}
    try:
        with LOCK.open('x', encoding='utf-8') as stream:
            json.dump(owner, stream, ensure_ascii=False)
    except FileExistsError:
        raise RuntimeError(f'Shared narrative source is occupied: {LOCK}. Wait for its owner; do not delete the lock.') from None
    try:
        yield owner
    finally:
        # If ownership changed, preserve the new owner's lock and report it.
        if not LOCK.exists():
            raise RuntimeError('B source lock disappeared during the operation; inspect concurrent writers.')
        if LOCK.read_text(encoding='utf-8') != json.dumps(owner, ensure_ascii=False):
            raise RuntimeError('B source lock ownership changed; preserving the current lock.')
        LOCK.unlink()
