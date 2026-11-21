"""Kiem tra .env co key nao duoc dat (khong in gia tri)."""
import io

for line in io.open('.env', encoding='utf-8'):
    line = line.strip()

    if not line or line.startswith('#') or '=' not in line:
        continue

    k, v = line.split('=', 1)
    v = v.strip()

    state = f'SET ({len(v)} chars)' if v else 'EMPTY'

    print(f'{k.strip()}: {state}')
