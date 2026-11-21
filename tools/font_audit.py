"""Kiem tra font nao cua game ho tro day du dau tieng Viet."""
import os
from fontTools.ttLib import TTFont

d = r'E:\Steam\steamapps\common\Deadlock\game\citadel\panorama\fonts'

# Day du bo dau tieng Viet (gom ca 2 dau va cac bien the)
tests = {
    'Aacute': 0xC1, 'Etilde': 0xC9, 'Ograve': 0xD2, 'uhorn': 0xDD,
    'yacute': 0xFD, 'ydieresis': 0xFF, 'ecircumflex': 0xEA,
    'ocircumflex': 0xF4, 'acircumflex': 0xE2, 'ohorn': 0x1ED,
    'uocircumflex_tilde': 0x1EE6, 'a_breve': 0x103,
}

rows = []
for fn in sorted(os.listdir(d)):
    if not fn.lower().endswith(('.ttf', '.otf')):
        continue
    try:
        f = TTFont(os.path.join(d, fn), fontNumber=0)
        cm = f.getBestCmap()
        miss = [k for k, v in tests.items() if v not in cm]
        rows.append((len(miss), fn, miss))
    except Exception:
        pass

rows.sort()
print(f'{"MISS":>4}  FILE')
full = [r for r in rows if r[0] == 0]
print(f'--- HOAN HAO ({len(full)} font) ---')
for r in full:
    print(f'{r[0]:>4}  {r[1]}')
print(f'--- THIEU ({len(rows)-len(full)} font) ---')
for r in rows:
    if r[0] > 0:
        print(f'{r[0]:>4}  {r[1]:38s} {r[2][:5]}')
