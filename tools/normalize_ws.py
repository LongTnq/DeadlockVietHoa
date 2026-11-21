"""Chuan hoa KeyValues: them khoang trang giua key va value.

Parser cua du an (C#) yeu cau co whitespace giua key va value, trong khi
mot so dong dang co dang "key""value" (khong whitespace). Script nay them
khoang trang vao cho khop, khong doi gia tri.
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

# "key""value" -> "key" "value"
NO_SPACE = re.compile(r'^(\s*"[^"]+")(")')


def main():
    folders = sys.argv[1:]

    if not folders:
        folders = []

        for d in sorted(os.listdir(
                ROOT + '\\workspace\\staging\\localization')):
            p = (ROOT + '\\workspace\\staging\\localization\\' + d + '\\' +
                 d + '_vietnamese.txt')

            if os.path.exists(p):
                folders.append(d)

    total = 0

    for folder in folders:
        f = (ROOT + '\\workspace\\staging\\localization\\' + folder + '\\' +
             folder + '_vietnamese.txt')
        lines = io.open(f, encoding='utf-8-sig').read().split('\n')
        changed = 0

        for i, l in enumerate(lines):
            m = NO_SPACE.match(l)

            if not m:
                continue

            # "key""value" -> "key"\t\t"value"
            lines[i] = m.group(1) + '\t\t' + l[m.end(1):]
            changed += 1

        if changed:
            shutil.copyfile(f, f + '.bak-ws')
            io.open(f, 'w', encoding='utf-8', newline='').write('\n'.join(lines))
            print('%s: them khoang trang vao %d dong' % (folder, changed))
            total += changed

    print('TONG:', total)


if __name__ == '__main__':
    main()
