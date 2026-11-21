"""Sua 545 dong bi escape sai (gia tri ket thuc bang hai dau nhay).

Dang loi:  "key"   "value""       ->  "key"   "value"
Nguyen nhan: ham escape() cua script dich dung chuoi nhay escape 2 lan.
"""
import io
import os
import re
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import translate_file as tf

ROOT = tf.ROOT
Q = chr(34)


def fix_line(s):
    """'key"  "val""' -> 'key"  "val"'

    Gia tri co dau nhai escape ben trong (\") nen phai dung thu tu
    dem dau nhay chu ra thay vi dung rfind.
    """
    # tach key
    i = s.find(Q)

    if i < 0:
        return None

    j = s.find(Q, i + 1)

    if j < 0:
        return None

    head = s[:j + 1]
    rest = s[j + 1:]

    k = rest.find(Q)

    if k < 0:
        return None

    mid = rest[:k]
    tail = rest[k + 1:]

    # mid phai la khoang trang giua key va value
    if not mid or mid.strip():
        return None

    # dem dau nhay chu ra trong tail (bo qua \")
    nq = 0
    x = 0

    while x < len(tail):
        if tail[x] == chr(92) and x + 1 < len(tail):
            x += 2
            continue

        if tail[x] == Q:
            nq += 1

        x += 1

    # phai co 2 dau nhay: 1 dong value + 1 thua
    if nq != 2:
        return None

    # vi tri dau nhay thu 2 (khong escape)
    pos = []
    x = 0

    while x < len(tail):
        if tail[x] == chr(92) and x + 1 < len(tail):
            x += 2
            continue

        if tail[x] == Q:
            pos.append(x)

        x += 1

    p1, p2 = pos[0], pos[1]
    body = tail[:p1]
    after = tail[p2 + 1:]

    if after.strip() and not after.strip().startswith('//'):
        return None

    return head + mid + Q + body + Q + after


def main():
    total = 0

    for d in sorted(os.listdir(ROOT + '\\workspace\\staging\\localization')):
        p = (ROOT + '\\workspace\\staging\\localization\\' + d + '\\' +
             d + '_vietnamese.txt')

        if not os.path.exists(p):
            continue

        lines = io.open(p, encoding='utf-8-sig').read().split('\n')
        changed = 0

        for i, l in enumerate(lines):
            s = l.rstrip('\r')

            if s.count(Q) % 2 == 1 and s.rstrip().endswith(Q * 2):
                fixed = fix_line(s)

                if fixed is not None:
                    lines[i] = fixed
                    changed += 1

        if changed:
            shutil.copyfile(p, p + '.bak-dq')
            io.open(p, 'w', encoding='utf-8', newline='').write('\n'.join(lines))
            print('%s: sua %d dong' % (d, changed))
            total += changed

    print('TONG:', total)


if __name__ == '__main__':
    main()
