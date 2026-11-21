"""Loc entry VAN CON TIENG ANH trong file staging (chua duoc dich)."""
import io
import json
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'
SRC = ROOT + r'\workspace\source\original\citadel_main\citadel_main_english.txt'

LINE_RE = re.compile(r'^\s*"([^"]+)"\s+"((?:[^"\\]|\\.)*)"\s*(?://.*)?$')


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

still_en = []
for key, (line, val) in staged.items():
    en = src.get(key, '')

    if not en.strip():
        continue

    if val.strip() != en.strip():
        continue

    still_en.append({'line': line, 'key': key, 'en': en})

still_en.sort(key=lambda e: e['line'])

# Phan loai: placeholder / so / ky tu dac biet -> KHONG can dich
TOKEN_ONLY = re.compile(r'^[\s\W\d]*$')

meaningful = []
trivial = []

for e in still_en:
    en = e['en']

    if TOKEN_ONLY.match(en):
        trivial.append(e)
    else:
        meaningful.append(e)

print(f'Van con tieng Anh (nghia nguyen): {len(meaningful)}')
print(f'Bo qua (chi so/ky tu dac biet)   : {len(trivial)}')
print()

print('--- BO QUA (khong can dich) ---')
for e in trivial:
    print(f'  [d{e["line"]}] {e["key"]} = {e["en"]!r}')

print()
print('--- CAN DICH ---')
for e in meaningful:
    print(f'[d{e["line"]}] {e["key"]}')
    print(f'  EN: {e["en"]}')

io.open(ROOT + r'\tools\_still_english.json', 'w', encoding='utf-8').write(
    json.dumps(meaningful, ensure_ascii=False, indent=1))
