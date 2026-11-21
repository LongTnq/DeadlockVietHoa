"""Dat entry ve Pending de kiem tra .env duoc doc dung (test ket noi that)."""
import json
import sys

db_path = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'

with open(db_path, 'r', encoding='utf-8') as f:
    doc = json.load(f)

entries = doc['entries'] if isinstance(doc, dict) and 'entries' in doc else doc

target = None

for e in entries:
    if e.get('key', '').endswith('Citadel_PlayerContextMenu_SpectateGame'):
        target = e
        break

if target is None:
    print('KHONG tim thay entry test')
    sys.exit(1)

print(f"Truoc : status={target['status']}  {target.get('translatedText','')!r}")

target['status'] = 'Pending'
target['translatedText'] = ''

with open(db_path, 'w', encoding='utf-8') as f:
    json.dump(doc, f, ensure_ascii=False, indent=2)

print(f"Sau  : status={target['status']}")
