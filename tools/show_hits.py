"""In chi tiet cac entry theo tung thuat ngu sai de review thu cong."""
import io
import json
import sys

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'

hits = json.load(io.open(ROOT + r'\tools\_glossary_hits.json', encoding='utf-8'))

term = sys.argv[1] if len(sys.argv) > 1 else None

for w, lst in hits.items():
    if term and w != term:
        continue

    print(f'=== {w} ({len(lst)}) ===')

    for e in sorted(lst, key=lambda x: x['line']):
        print(f'  [d{e["line"]}] {e["key"]}')
        print(f'      {e["val"][:200]}')

    print()
