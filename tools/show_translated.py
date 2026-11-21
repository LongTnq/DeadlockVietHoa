"""Xem mau ket qua dich de kiem tra chat luong."""
import io
import json
import sys

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'

done = json.load(io.open(ROOT + r'\tools\_translated_main.json', encoding='utf-8'))
items = json.load(io.open(ROOT + r'\tools\_to_translate.json', encoding='utf-8'))

en_map = {e['key']: e['en'] for e in items}

n = int(sys.argv[1]) if len(sys.argv) > 1 else 40
start = int(sys.argv[2]) if len(sys.argv) > 2 else 0

keys = list(done.keys())[start:start + n]

for k in keys:
    print(f'[{k}]')
    print(f'  EN: {en_map.get(k, "?")}')
    print(f'  VI: {done[k]}')
    print()
