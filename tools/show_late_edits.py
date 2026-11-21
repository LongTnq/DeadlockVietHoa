"""In cac thay doi cua nguoi dung ngoai vung 0-999 (de xac dinh pham vi that su)."""
import io
import json

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'

edits = json.load(io.open(ROOT + r'\tools\_user_edits.json', encoding='utf-8'))

late = [e for e in edits if e['line'] >= 1000]

print(f'Thay doi ngoai dong 1000: {len(late)}\n')

for e in late:
    print(f'[d{e["line"]}] {e["key"]}')
    print(f'  EN : {e["en"][:140]}')
    print(f'  CU : {e["old"][:140]}')
    print(f'  MOI: {e["new"][:140]}')
    print()
