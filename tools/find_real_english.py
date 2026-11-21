"""Loc entry con tieng Anh ma KHONG chua placeholder -> that su hien thi ra game."""
import io
import json
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'
SRC = ROOT + r'\workspace\source\original\citadel_main\citadel_main_english.txt'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?://.*)?)$')
TOKEN_RE = re.compile(r'\{[^{}]*\}')


def unescape(v):
    v = v.replace('\\"', '"').replace('\\n', '\n').replace('\\t', '\t')
    return v.replace('\\\\', '\\')


def parse(path):
    out = {}
    with io.open(path, encoding='utf-8') as f:
        for i, raw in enumerate(f, 1):
            m = LINE_RE.match(raw.rstrip('\n'))
            if m:
                key = re.match(r'\s*"([^"]+)"', m.group(1)).group(1)
                out[key] = (i, unescape(m.group(2)))
    return out


staged = parse(STAGING)
src = {k: v for k, (_i, v) in parse(SRC).items()}

real = []

for key, (line, val) in staged.items():
    en = src.get(key, '')

    if not en.strip():
        continue

    if val.strip() != en.strip():
        continue

    # Bo qua neu chi con placeholder / ky tu
    if not TOKEN_RE.sub('', en).strip():
        continue

    real.append((line, key, en))

real.sort()

print(f'Entry con tieng Anh, KHONG co placeholder: {len(real)}\n')

for line, key, en in real:
    print(f'[d{line}] {key}  ::  {en}')
