"""Sau khi ap dung, xem entry VAN CON tieng Anh va phan loại lai."""
import io
import json
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'
SRC = ROOT + r'\workspace\source\original\citadel_main\citadel_main_english.txt'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?://.*)?)$')


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

still = []
for key, (line, val) in staged.items():
    en = src.get(key, '')

    if not en.strip():
        continue

    if val.strip() == en.strip():
        still.append({'line': line, 'key': key, 'en': en})

still.sort(key=lambda e: e['line'])

# Nhom theo loai
TOKEN_RE = re.compile(r'^[\s\W\d]*$')
SHORT_RE = re.compile(r'^\s*[\d\.\,\:\-\+\%\sxX×°¥/!\?#\[\]\']+\s*$')

PLACEHOLDER = []
TECHNICAL = []
NEEDS_TRANSLATION = []

TECH_KEY_RE = re.compile(
    r'^citadel_keybind|^Citadel_Key_|^citadel_keybind_map_layout|'
    r'^citadel_settings_(videopreset|ssao_quality|dfao_quality|fogquality|'
    r'texturequality|shadow_quality|upscaling|antialiasing|fsr|fsr2|fsr3|dlss|'
    r'dlsstechnology|low_latency|gsync|mboit|vsync|fsr2scaling|dlssscaling|'
    r'subtitles_background_style|subtitles_font_stylized)|'
    r'^citadel_settings_(milliseconds|seconds|fps|pixels|multiplier|degrees)_postfix$|'
    r'^citadel_settings_color_picker_|^citadel_settings_crosshair_|'
    r'^Citadel_GunTag_|^item_info_tier_|^HeroTesting_Bot|^citadel_settings_share_contents_preview_tooltip$')

for e in still:
    en = e['en'].strip()

    if TOKEN_RE.match(en) or SHORT_RE.match(en):
        PLACEHOLDER.append(e)
    elif TECH_KEY_RE.match(e['key']):
        TECHNICAL.append(e)
    else:
        NEEDS_TRANSLATION.append(e)

print(f'Van con tieng Anh   : {len(still)}')
print(f'  - Placeholder/so   : {len(PLACEHOLDER)}')
print(f'  - Ky thuat (giu)   : {len(TECHNICAL)}')
print(f'  - CAN DICH         : {len(NEEDS_TRANSLATION)}')
print()

print('--- CAN DICH ---')

for e in NEEDS_TRANSLATION:
    print(f'[d{e["line"]}] {e["key"]}  ::  {e["en"]}')

with io.open(ROOT + r'\tools\_still2.json', 'w', encoding='utf-8') as f:
    json.dump(NEEDS_TRANSLATION, f, ensure_ascii=False, indent=1)
