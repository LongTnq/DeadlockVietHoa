"""Tim cac entry bi hong do tim-thay the nham cua nguoi dung."""
import io
import json
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'

edits = json.load(io.open(ROOT + r'\tools\_user_edits.json', encoding='utf-8'))

# Cac mau nghi do "Xep hang" (tu tieng Anh "rank"?) chen vao giua tu
PATTERNS = [
    (re.compile(r'darkoXếp hạnge'), 'darkorange'),
    (re.compile(r'tXếp hạngrí'), 'trang trí'),
    (re.compile(r'TXếp hạng'), 'Trang'),
    (re.compile(r'\bhoá đổi\b'), 'hoán đổi'),
]

print('=== ENTRY BI HONG (ung vien de sua) ===\n')

for e in edits:
    new = e['new']
    fixed = new
    notes = []

    for pat, repl in PATTERNS:
        if pat.search(fixed):
            fixed = pat.sub(repl, fixed)
            notes.append(f'{pat.pattern} -> {repl}')

    # "viet pham them" -> "trang bi them" (verb bi nham bien thanh noun)
    if 'đã vật phẩm thêm' in fixed:
        fixed = fixed.replace('đã vật phẩm thêm', 'đã trang bị thêm')
        notes.append("'đã vật phẩm thêm' -> 'đã trang bị thêm'")

    if fixed != new:
        print(f'[d{e["line"]}] {e["key"]}')
        print(f'  EN  : {e["en"][:120]}')
        print(f'  HONG: {new[:160]}')
        print(f'  FIX : {fixed[:160]}')
        print(f'  ({", ".join(notes)})')
        print()
