"""Kiem tra file staging con hop le (parse KeyValues) + thong ke thuat ngu con sai."""
import io
import re
import sys
from collections import Counter

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'
SRC = ROOT + r'\workspace\source\original\citadel_main\citadel_main_english.txt'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?://.*)?)$')

# Kiem tra: moi dong co key phai co dau nhay mo + dong
PAT = re.compile(r'^\s*"[^"]+"\s+"')


def unescape(v):
    v = v.replace('\\"', '"').replace('\\n', '\n').replace('\\t', '\t')
    return v.replace('\\\\', '\\')


def main():
    with io.open(STAGING, encoding='utf-8') as f:
        raw = f.read()

    lines = raw.split('\n')

    bad = []
    staged = {}

    for i, l in enumerate(lines, 1):
        s = l.rstrip('\r')

        if not s.strip():
            continue

        if s.strip().startswith('//') or s.strip() in ('{', '}'):
            continue

        if '"lang"' in s:
            continue

        if not PAT.match(s):
            bad.append((i, s))
            continue

        m = LINE_RE.match(s)
        if not m:
            bad.append((i, s))
            continue

        key = re.match(r'\s*"([^"]+)"', m.group(1)).group(1)
        staged[key] = (i, unescape(m.group(2)))

    print(f'Tong dong co key  : {len(staged):,}')
    print(f'Dong hong         : {len(bad)}')

    for i, s in bad[:20]:
        print(f'  d{i}: {s[:150]}')

    # --- Thuat ngu con sai ---
    print()
    print('=== THUAT NGU CON SAI ===')

    WRONG = ['Nội lực', 'nội lực', 'Sức khỏe', 'sức khỏe',
             'Phước lành', 'phước lành', 'ô uế', 'Ô uế',
             'Tăng cường', 'tăng cường', 'Anh hùng', 'anh hùng']

    c = Counter()
    samples = {}

    for key, (line, val) in staged.items():
        for w in WRONG:
            if w in val:
                c[w] += 1
                samples.setdefault(w, []).append((line, key, val))

    if not c:
        print('  (khong con)')

    for w, n in c.most_common():
        print(f'  {w:14s} {n:>4} entry')

    for w, lst in samples.items():
        print(f'\n--- {w} ---')
        for line, key, val in sorted(lst)[:10]:
            print(f'  [d{line}] {key}')
            print(f'      {val[:150]}')

    # --- Van con tieng Anh ---
    print()
    print('=== VAN CON TIENG ANH ===')

    with io.open(SRC, encoding='utf-8') as f:
        src_lines = f.read().split('\n')

    src = {}
    for l in src_lines:
        m = LINE_RE.match(l.rstrip('\r'))
        if m:
            src[re.match(r'\s*"([^"]+)"', m.group(1)).group(1)] = unescape(m.group(2))

    still = []
    for key, (line, val) in staged.items():
        en = src.get(key, '')

        if not en.strip():
            continue

        if val.strip() == en.strip():
            still.append((line, key, en))

    print(f'  Con nguyen tieng Anh: {len(still)}')


if __name__ == '__main__':
    main()
