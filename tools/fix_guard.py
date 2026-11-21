"""Sua 11 entry bi guard chat luong chan (ban dich khong dau)."""
import io
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?://.*)?)$')

MANUAL = {
    'Citadel_HeroPage_FatesTale': 'CÂU CHUYỆN ĐỊNH MỆNH:',
    'modifier_citadel_idol_pickup_disabled': 'Hết thời gian Bình linh hồn',
    'modifier_citadel_delayed_catapult_launch':
        'Bị sát thương - Bàn nhảy đang sẵn sàng',
    'modifier_citadel_secure_souls': 'Linh hồn trong trắng',
    'modifier_walker_resist_aura': 'Quang khuôn Kháng Walker',
    'modifier_citadel_capture_point_meteor/contributor': 'Đang chiếm phục điểm',
    'citadel_settings_sounddevice_search': 'loa tai nghe headphone âm thanh',
    'guide_upgrades_yellow_orbs_header': 'Chiếm thống Linh hồn',
    'menu_play_hero_labs_desc': 'Giúp kiểm tra các Tướng mới thử nghiệm',
    'AbilityButtonHint_AltCastStickyBomb': 'Gắn bom vào bản thân',
    'citadel_chatwheel_label_enemy_low_health': 'Máu thấp nghiêm trọng',
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
