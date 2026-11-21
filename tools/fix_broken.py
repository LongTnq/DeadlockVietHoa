"""Sua cac dong hong trong file staging.

3 loai:
1. 2 entry bi ghep vao mot dong
2. escape dau nhay bi nhan doi: ket thuc bang hai dau nhay
3. gia tri chua newline that
"""
import io
import os
import re
import shutil

ST = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\staging\localization'

Q = chr(34)
LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?:\s*//.*)?)$')

# 2 entry ghep: "k1" <ws> "v1" <ws> "k2" <ws> "v2"
MERGED_RE = re.compile(
    r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)"\s+("[^"]+"\s+)"((?:[^"\\]|\\.)*)"(\s*)$')


def fix_double(s):
    """value ket thuc bang hai dau nhay -> bo mot dau."""
    # tach key va value bang dau nhay
    idx = s.find(Q)

    if idx < 0:
        return None

    key_part = s[:idx + 1]
    rest = s[idx + 1:]
    # value = rest[:-1] neu ket thuc bang dau nhay
    r = rest.rfind(Q)

    if r < 0:
        return None

    value = rest[:r]
    tail = rest[r + 1:]

    # gia tri ket thuc bang '""' -> rest = value + '"' + tail
    if not tail.startswith(Q):
        return None

    tail2 = tail[1:]

    # tail phai la whitespace hoac comment
    if tail2.strip() and not tail2.strip().startswith('//'):
        return None

    return key_part + Q + value + Q + tail2


def fix_merged(s):
    """Tach 2 entry ghep thanh 2 dong."""
    m = MERGED_RE.match(s)

    if not m:
        return None

    pre = m.group(1)
    v1 = m.group(2)
    pre2 = m.group(3)
    v2 = m.group(4)
    post = m.group(5)
    return [pre + Q + v1 + Q, pre2 + Q + v2 + Q + post]


total = 0

for d in sorted(os.listdir(ST)):
    p = os.path.join(ST, d)

    if not os.path.isdir(p):
        continue

    for fn in sorted(os.listdir(p)):
        if not fn.endswith('.txt'):
            continue

        f = os.path.join(p, fn)
        lines = io.open(f, encoding='utf-8-sig').read().split('\n')
        out = []
        changed = 0

        for l in lines:
            s = l.rstrip()

            if not s.strip() or s.strip().startswith('//'):
                out.append(l)
                continue

            if LINE_RE.match(s):
                out.append(l)
                continue

            d2 = fix_double(s)

            if d2 is not None:
                out.append(d2)
                changed += 1
                continue

            mg = fix_merged(s)

            if mg is not None:
                out.extend(mg)
                changed += 1
                continue

            out.append(l)

        if changed:
            shutil.copyfile(f, f + '.bak-fix')
            io.open(f, 'w', encoding='utf-8', newline='').write('\n'.join(out))
            print(f'{d}/{fn}: sua {changed}')
            total += changed

print('TONG sua:', total)
