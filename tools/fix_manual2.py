"""Sua cac entry con lai thu cong (phan 2).

Nguyen tac:
- Ten rieng (Guardian, Walker, Patron, Amber, Sapphire, Broadway, York,
  Greenwich, Archmother, Hidden King, rank names, Bo, Bot) -> GIU NGUYEN
  tieng Anh, consistent voi phan user da sua.
- Placeholder thuan -> giu nguyen.
- Chuoi giao dien -> dich.
"""
import io
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?://.*)?)$')

MANUAL = {
    # --- Cai dat ---
    'citadel_settings_video': 'Video',
    'citadel_settings_audio': 'Âm thanh',
    'citadel_settings_about': 'Giới thiệu',
    'citadel_settings_social': 'Mạng xã hội',
    'citadel_settings_communication': 'Giao tiếp',
    'citadel_settings_hotkeys': 'Phím tắt',
    'citadel_settings_steaminput': 'Tay cầm',
    'citadel_settings_communication_allow_pings': 'Tín hiệu',
    'citadel_settings_volume': 'Âm lượng',
    'citadel_settings_game': 'Trò chơi',
    'citadel_settings_subtitles_filter_pings': 'Tín hiệu',
    'citadel_settings_camera_preset_1': 'Camera 1',
    'citadel_settings_camera_preset_2': 'Camera 2',
    'citadel_settings_camera_preset_3': 'Camera 3',
    'citadel_settings_share_reticle': 'Tâm ngắm',
    'citadel_settings_crosshair': 'Tâm ngắm',
    'citadel_settings_chat_ptt': 'Nhấn để nói',

    # --- Huong dan ---
    'guide_the_map_core_header': 'Lõi',
    'guide_the_map_core_alt_header': 'Căn cứ',

    # --- Menu ---
    'menu_bots_coop': 'Chơi chung Online',
    'citadel_voicelines_category_kills': 'Hạ gục',
    'citadel_voicelines_category_pings': 'Tín hiệu',

    # --- Chat wheel ---
    'citadel_chatwheel_label_Rejuv_Drop': 'Hồi máu',

    # --- Combat log ---
    'Citadel_CombatLog_Modifiers': 'Hiệu ứng cộng',

    # --- Ban do ---
    'map_district_building_yorkside': 'York',
    'map_district_building_uptown': 'Khu trên',
    'map_district_building_downtown': 'Khu dưới',
    'map_district_building_brownstones': 'Nhà gạch nâu',
    'map_district_building_park': 'Công viên',
    'map_district_building_factory': 'Nhà máy',
    'map_district_building_stock_exchange': 'Sàn giao dịch',
    'map_district_building_belltower': 'Tháp chuông',
    'map_district_building_campus': 'Khuôn viên trường',
    'map_district_building_upsidedown': 'Lật ngược',

    # --- Khac ---
    'Citadel_HUD_DamageReport_Time30': '30 giây',
    'Citadel_HeroBuilds_BuildID': 'ID Lối lên: {i:r:selected_hero_build_id}',
    'Citadel_LaneExpress': 'Tốc hành',
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
    found = set()

    for i, raw in enumerate(lines):
        m = LINE_RE.match(raw.rstrip('\n'))

        if not m:
            continue

        key = re.match(r'\s*"([^"]+)"', m.group(1)).group(1)

        if key not in MANUAL:
            continue

        found.add(key)
        new = MANUAL[key]

        if unescape(m.group(2)) != new:
            lines[i] = m.group(1) + '"' + escape(new) + m.group(3) + '\n'
            applied += 1

    with io.open(STAGING, 'w', encoding='utf-8', newline='') as f:
        f.writelines(lines)

    print(f'Sua: {applied}/{len(MANUAL)}')

    missing = set(MANUAL) - found

    if missing:
        print('Khong tim thay key:')

        for k in sorted(missing):
            print(f'  {k}')


if __name__ == '__main__':
    main()
