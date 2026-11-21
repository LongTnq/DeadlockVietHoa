"""Phan loai 762 entry con tieng Anh: can dich vs nen giu nguyen."""
import io
import json
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'

items = json.load(io.open(ROOT + r'\tools\_still_english.json', encoding='utf-8'))

# NEN GIU NGUYEN: ten phim, ma bo phim, placeholder, ten bo cuc, ten preset do hoa
KEEP_PATTERNS = [
    # Ten phim / bom
    r'^citadel_keybind_', r'^Citadel_Key_', r'^citadel_keybind_map_',
    # Placeholder thuan
    r'^Citadel_Dashboard_Build', r'^citadel_clientversion$',
    # Ten layout bo phim
    r'^citadel_keybind_map_layout_',
    # Ten preset do hoa / phien ban
    r'^citadel_settings_(videopreset|ssao_quality|dfao_quality|fogquality|'
    r'texturequality|shadow_quality|upscaling|antialiasing|fsr|fsr2|fsr3|dlss|'
    r'dlsstechnology|low_latency|gsync|mboit|vsync|fsr2scaling|dlssscaling|'
    r'subtitles_background_style|subtitles_font_stylized)',
    # Chuoi tim kiem (khong hien thi)
    r'_search$',
    # Don vi / ky tu
    r'^citadel_settings_(milliseconds|seconds|fps|pixels|multiplier|degrees)_postfix$',
    r'^Citadel_OrbClaimTime_postfix$', r'^citadel_ping_cooldown_seconds_postfix$',
    # Ten hanh dong co the giu nguyen (ky thuat Valve)
    r'^citadel_settings_(display_option_label|display_refresh_rate|aspectratio)',
    r'^citadel_settings_color_picker_', r'^citadel_settings_crosshair_',
    r'^citadel_settings_volume$', r'^citadel_settings_about$',
    r'^citadel_settings_social$', r'^citadel_settings_communication$',
    r'^citadel_settings_game$', r'^citadel_settings_video$', r'^citadel_settings_audio$',
    r'^citadel_settings_hotkeys$', r'^citadel_settings_hero$',
    r'^citadel_settings_steaminput$', r'^citadel_settings_english_hero_names$',
    r'^Citadel_GunTag_', r'^item_info_tier_', r'^Citadel_Level_test$',
    r'^HeroTesting_Bot', r'^Citadel_Hero_Stats_Tooltip',
    # Tam tien / ky tu dac biet
    r'^citadel_settings_share_contents_preview_tooltip$',
]

KEEP_RE = re.compile('|'.join(KEEP_PATTERNS))

# Chi chua PLACEHOLDER / dau cham: giu nguyen
TOKEN_RE = re.compile(r'^[\s\W\d]*$')

# Chi chua so / ky tu + chu
SHORT_NUM_RE = re.compile(r'^\s*[\d\.\,\:\-\+\%\sxX×°¥/!\?#\[\]]+\s*$')

keep = []
translate = []

for e in items:
    en = e['en'].strip()
    key = e['key']

    if TOKEN_RE.match(en) or SHORT_NUM_RE.match(en):
        keep.append(e)
        continue

    if KEEP_RE.match(key):
        keep.append(e)
        continue

    translate.append(e)

print(f'Tong              : {len(items)}')
print(f'NEN GIU NGUYEN    : {len(keep)}')
print(f'CAN DICH          : {len(translate)}')
print()

print('--- CAN DICH (theo dong) ---')

for e in sorted(translate, key=lambda x: x['line']):
    print(f'[d{e["line"]}] {e["key"]}  ::  {e["en"]}')

with io.open(ROOT + r'\tools\_to_translate.json', 'w', encoding='utf-8') as f:
    json.dump(translate, f, ensure_ascii=False, indent=1)
