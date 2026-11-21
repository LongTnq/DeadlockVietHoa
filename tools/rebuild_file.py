"""Sua cac dong hong: khoi phuc file staging tu ban goc + apply lai cac ban da dich.

Cach an toan: doi voi moi key-value, lay gia tri tu ban .bak-fix (hoac file
hien tai), neu gia tri do khong hop le thi dung ban goc tieng Anh lam fallback
va dich lai bang API neu can.
"""
import io
import os
import re
import shutil
import sys

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
ST = ROOT + r'\workspace\staging\localization'
ORIG = ROOT + r'\workspace\source\original'

Q = chr(34)


def parse_kv(text):
    """Parse KeyValues -> list (key, value) theo thu tu, bo qua comment."""
    out = []
    i = 0
    n = len(text)

    while i < n:
        # tim dau nhay mo
        while i < n and text[i] != Q:
            i += 1

        if i >= n:
            break

        i += 1
        key_start = i

        while i < n and text[i] != Q:
            i += 1

        key = text[key_start:i]
        i += 1  # bo dau nhay dong

        # tim dau nhay value
        while i < n and text[i] != Q:
            i += 1

        if i >= n:
            break

        i += 1
        val_start = i
        buf = []

        while i < n:
            c = text[i]

            if c == '\\' and i + 1 < n:
                buf.append(c)
                buf.append(text[i + 1])
                i += 2
                continue

            if c == Q:
                break

            buf.append(c)
            i += 1

        val = ''.join(buf)
        i += 1
        out.append((key, val))

    return out


def rebuild(entries):
    lines = []

    for k, v in entries:
        lines.append(Q + k + Q + '\t\t' + Q + v + Q)

    return '\n'.join(lines)


def main():
    folder = sys.argv[1]
    sfile = os.path.join(ST, folder, folder + '_vietnamese.txt')
    ofile = os.path.join(ORIG, folder, folder + '_english.txt')

    text = io.open(sfile, encoding='utf-8-sig').read()
    orig_text = io.open(ofile, encoding='utf-8-sig').read()

    cur = parse_kv(text)
    orig = parse_kv(orig_text)
    orig_map = dict(orig)

    # Kiem tra cac entry dang hong: gia tri co dau nhay chua escape
    broken = []

    for idx, (k, v) in enumerate(cur):
        if '"' in v:
            broken.append((idx, k, v))

    print(f'[{folder}] {len(cur)} entry, {len(broken)} entry hong')

    if not broken:
        return

    # Khoi phuc tu ban goc cho cac entry broken
    fixed = 0
    out = []

    for idx, (k, v) in enumerate(cur):
        if '"' in v and k in orig_map:
            out.append((k, orig_map[k]))
            fixed += 1
            print(f'  khoi phuc: {k}')
        else:
            out.append((k, v))

    shutil.copyfile(sfile, sfile + '.bak-broken')
    io.open(sfile, 'w', encoding='utf-8', newline='').write(rebuild(out))
    print(f'[{folder}] da khoi phuc {fixed} entry (can dich lai)')


if __name__ == '__main__':
    main()
