"""Quét mọi file staging tìm entry còn tiếng Anh (so với file gocc tieng Anh)."""
import io
import json
import os
import re
import sys

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization'
ORIGINAL = ROOT + r'\workspace\source\original'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?:\s*//.*)?)$')

# Chi chua placeholder / dau cham -> bo qua
TOKEN_RE = re.compile(r'\{[^{}]*\}')
ONLY_TOKEN_RE = re.compile(r'^[\s\W\d]*$')


def unescape(v):
    v = v.replace('\\"', '"').replace('\\n', '\n').replace('\\t', '\t')
    return v.replace('\\\\', '\\')


def parse(path):
    """Parse file KeyValues: key -> (line, value)."""
    out = {}
    order = []

    with io.open(path, encoding='utf-8-sig') as f:
        for i, raw in enumerate(f, 1):
            m = LINE_RE.match(raw.rstrip('\n'))

            if not m:
                continue

            key = re.match(r'\s*"([^"]+)"', m.group(1)).group(1)
            out[key] = (i, unescape(m.group(2)))
            order.append(key)

    return out, order


def find_original(staged_file):
    """Tim file gocc tieng Anh tuong ung (ten ..._english.txt)."""
    base = os.path.basename(staged_file).replace('_vietnamese', '').replace(
        '.txt', '')

    for dirpath, _dirnames, filenames in os.walk(ORIGINAL):
        for fn in filenames:
            if fn.startswith(base) and fn.endswith('.txt'):
                return os.path.join(dirpath, fn)

    return None


def main():
    only = sys.argv[1] if len(sys.argv) > 1 else None

    report = {}
    total_still = 0
    total_real = 0

    for d in sorted(os.listdir(STAGING)):
        sdir = os.path.join(STAGING, d)

        if not os.path.isdir(sdir):
            continue

        if only and d != only:
            continue

        for fn in sorted(os.listdir(sdir)):
            if not fn.endswith('.txt'):
                continue

            sfile = os.path.join(sdir, fn)
            ofile = find_original(sfile)

            if not ofile:
                print(f'!! Khong tim thay file gocc: {d}/{fn}')
                continue

            staged, order = parse(sfile)
            orig, _ = parse(ofile)

            still = []
            real = []

            for key in order:
                if key not in orig:
                    continue

                sval = staged[key][1]
                oval = orig[key][1]

                if not oval.strip():
                    continue

                if sval.strip() == oval.strip():
                    still.append((staged[key][0], key, oval))

                    # Khong con placeholder -> that su hien thi
                    if not ONLY_TOKEN_RE.match(TOKEN_RE.sub('', oval)):
                        real.append((staged[key][0], key, oval))

            report[d] = {'still': still, 'real': real}
            total_still += len(still)
            total_real += len(real)

            flag = ' <<<' if real else ''
            print(f'{d:24s} con EN: {len(still):>6}   (hien thi that: {len(real):>5}){flag}')

    print()
    print(f'TONG: con EN {total_still}, hien thi that {total_real}')

    with io.open(ROOT + r'\tools\_all_english.json', 'w', encoding='utf-8') as f:
        json.dump(
            {d: {'still': v['still'], 'real': v['real']}
             for d, v in report.items()},
            f, ensure_ascii=False, indent=1)

    print('Da luu -> tools/_all_english.json')


if __name__ == '__main__':
    main()
