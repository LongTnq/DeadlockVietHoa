"""Doi 'anh hùng' -> 'tướng' trong tat ca file staging (giu hoa/thuong)."""
import io
import os
import re

ST = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\staging\localization'

# thu tu: cap chu hoa dac biet truoc
PAIRS = [
    (re.compile(r'\bANH HÙNG\b'), 'TƯỚNG'),
    (re.compile(r'\bAnh Hùng\b'), 'Tướng'),
    (re.compile(r'\banh hùng\b'), 'tướng'),
]

total = 0

for d in sorted(os.listdir(ST)):
    p = os.path.join(ST, d)

    if not os.path.isdir(p):
        continue

    for fn in sorted(os.listdir(p)):
        if not fn.endswith('.txt'):
            continue

        f = os.path.join(p, fn)
        raw = io.open(f, encoding='utf-8-sig').read()
        bom = raw != io.open(f, encoding='utf-8').read()
        out = raw
        n = 0

        for rx, rep in PAIRS:
            out, k = rx.subn(rep, out)
            n += k

        if n:
            enc = 'utf-8-sig' if bom else 'utf-8'
            io.open(f, 'w', encoding=enc, newline='').write(out)
            print(f'{d}/{fn}: {n} the')
            total += n

print(f'TONG: {total} the')
