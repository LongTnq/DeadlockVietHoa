"""In entry con sot tieng Anh, nhom theo dong, ra file de xu ly."""
import io
import json

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'

items = json.load(io.open(ROOT + r'\tools\_untranslated.json', encoding='utf-8'))
items.sort(key=lambda e: e['line'])

out = io.open(ROOT + r'\tools\_untranslated_report.txt', 'w', encoding='utf-8')

for e in items:
    out.write(f'[d{e["line"]}] {e["key"]}\n')
    out.write(f'  EN : {e["en"]}\n')
    out.write(f'  CU : {e["cur"]}\n\n')

out.close()

# Thong ke
empty_en = [e for e in items if not e['en'].strip()]
same = [e for e in items if e['cur'].strip() == e['en'].strip() and e['en'].strip()]
diff = [e for e in items if e['en'].strip() and e['cur'].strip() != e['en'].strip()]

print(f'Tong           : {len(items)}')
print(f'EN rong        : {len(empty_en)}')
print(f'Y het tieng Anh: {len(same)}')
print(f'Da dich sai ky (khac EN): {len(diff)}')
print()

print('--- EN rong (placeholder) ---')
for e in empty_en[:30]:
    print(f'  [d{e["line"]}] {e["key"]}  CU={e["cur"]!r}')
