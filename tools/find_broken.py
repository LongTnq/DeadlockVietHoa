"""Tim dong bi hong do escape sai (dau nhay bi nhan doi / khong parse duoc)."""
import io
import os
import re

ST = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\staging\localization'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?:\s*//.*)?)$')

bad = 0

for d in sorted(os.listdir(ST)):
    p = os.path.join(ST, d)

    if not os.path.isdir(p):
        continue

    for fn in sorted(os.listdir(p)):
        if not fn.endswith('.txt'):
            continue

        f = os.path.join(p, fn)

        for i, l in enumerate(
                io.open(f, encoding='utf-8-sig').read().split('\n'), 1):
            s = l.rstrip()

            if not s.strip() or s.strip().startswith('//'):
                continue

            if LINE_RE.match(s):
                continue

            # Khong phai dong key-value hop le
            if '"' not in s:
                continue

            print(f'{d}/{fn}:{i}: {s.strip()[:160]}')
            bad += 1

print('BAD:', bad)
