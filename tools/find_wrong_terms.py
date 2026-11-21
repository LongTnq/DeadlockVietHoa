"""Tim cac entry trong file staging van dung thuat ngu SAI (theo glossary tu user sua)."""
import io
import json
import re
from collections import Counter

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'
SRC = ROOT + r'\workspace\source\original\citadel_main\citadel_main_english.txt'

LINE_RE = re.compile(r'^\s*"([^"]+)"\s+"((?:[^"\\]|\\.)*)"\s*(?://.*)?$')

# Glossary: thuat ngu SAI (trong ban may dich) -> thuat ngu DUNG (user da sua)
# Chi lay cac cap do dai >= 3 de tranh bao dong.
GLOSSARY = {
    # --- Health / Sinh luc ---
    'Sức khỏe': 'Máu',
    'sức khỏe': 'máu',
    'Khôi phục sức khỏe': 'Hồi máu',
    'Sinh lực': 'Máu',
    # --- Boons ---
    'Phước lành': 'Ân huệ',
    'phước lành': 'ân huệ',
    'Thuận lợi': 'Ân huệ',
    'thuận lợi': 'ân huệ',
    'Quà phước lành': 'Thưởng Ân huệ',
    'Boons': 'Ân huệ',
    # --- Spirit ---
    'Nội lực': 'Phép',
    'nội lực': 'phép',
    'Nội Lực': 'Phép',
    'Tỷ Lệ Phép': 'Tỷ lệ Phép',
    # --- Items ---
    'Trang bị': 'vật phẩm',
    # --- Hero ---
    'Anh hùng': 'Tướng',
    'anh hùng': 'tướng',
    'Hero': 'Tướng',
    'herо': 'Tướng',
    'Nhân vật': 'Tướng',
    # --- Rank ---
    'Đấu Hạng': 'Xếp hạng',
    'Rang cấp': 'Xếp hạng',
    'rạng cấp': 'xếp hạng',
    'Subrank': 'phân hạng',
    'subrank': 'phân hạng',
    'Subrang': 'phân hạng',
    'calibrating': 'phân hạng',
    'Calibrating': 'Phân hạng',
    'calibration': 'phân hạng',
    'Calibration': 'Phân hạng',
    # --- Corrupt ---
    'ô uế': 'tha hoá',
    'Ô uế': 'Tha hoá',
    'làm ô uế': 'tha hoá',
    # --- Enhanced ---
    'tăng cường': 'thăng hoa',
    'Tăng cường': 'Thăng hoa',
    # --- Street Brawl ---
    'Đấu Trường Đường phố': 'Ẩu đả Đường phố',
    # --- Items plural ---
    'Vật phẩm': 'vật phẩm',
}


def unescape(v):
    v = v.replace('\\"', '"').replace('\\n', '\n').replace('\\t', '\t')
    return v.replace('\\\\', '\\')


def parse(path):
    out = {}
    with io.open(path, encoding='utf-8-sig') as f:
        for i, raw in enumerate(f, 1):
            m = LINE_RE.match(raw.rstrip('\n'))
            if m:
                out[m.group(1)] = (i, unescape(m.group(2)))
    return out


staged = parse(STAGING)
src = {k: v for k, (_i, v) in parse(SRC).items()}

# User da sua o dau -> nhung entry do KHONG sua them
user_fixed = {e['key'] for e in json.load(
    io.open(ROOT + r'\tools\_user_edits.json', encoding='utf-8'))}

hits = Counter()
affected = {}

for key, (line, val) in staged.items():
    if key in user_fixed:
        continue

    for wrong in GLOSSARY:
        if wrong in val:
            hits[wrong] += 1
            affected.setdefault(wrong, []).append((line, key, val))

print('=== SO ENTRY DUNG THUAT NGU SAI ===\n')

total = 0

for wrong, n in hits.most_common():
    total += n
    print(f'  {wrong:22s} -> {GLOSSARY[wrong]:14s}  {n:>4} entry')

print(f'\nTong so entry can sua: {total}')

with io.open(ROOT + r'\tools\_glossary_hits.json', 'w', encoding='utf-8') as fp:
    json.dump(
        {w: [{'line': l, 'key': k, 'val': v} for l, k, v in lst]
         for w, lst in affected.items()},
        fp, ensure_ascii=False, indent=1)
