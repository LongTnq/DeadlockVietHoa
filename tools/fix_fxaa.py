"""Phan biet 2 entry trung nhau: fxaa (Antialiasing) vs antialiasing (Anti-aliasing)."""
import io
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?:\s*//.*)?)$')

MANUAL = {
    # "fxaa" la lựa chon cu the (ten phien ban anti-aliasing)
    'citadel_settings_fxaa': 'FXAA',
    # "antialiasing" la muc tong quat
    'citadel_settings_antialiasing': 'Khử răng cưa',
}


def unescape(v):
    v = v.replace('\\"', '"').replace('\\n', '\n').replace('\\t', '\t')
    return v.replace('\\\\', '\\')


def escape(v):
    return (v.replace('\\', '\\\\').replace('"', '\\"')
             .replace('\n', '\\n').replace('\t', '\\t'))


def main():
    with io.open(STAGING, encoding='utf-8') as f:
        lines = f.readlines()

    applied = 0

    for i, raw in enumerate(lines):
        m = LINE_RE.match(raw.rstrip('\n'))

        if not m:
            continue

        key = re.match(r'\s*"([^"]+)"', m.group(1)).group(1)

        if key not in MANUAL:
            continue

        new = MANUAL[key]

        if unescape(m.group(2)) != new:
            lines[i] = m.group(1) + '"' + escape(new) + m.group(3) + '\n'
            applied += 1

    with io.open(STAGING, 'w', encoding='utf-8', newline='') as f:
        f.writelines(lines)

    print(f'Sua: {applied}/{len(MANUAL)}')


if __name__ == '__main__':
    main()
