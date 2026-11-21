"""Sua nhom chat luong do hoa + mot so setting con tieng Anh.

Quy tac: Low=Thap, Med=Trung binh, High=Cao, Ultra=Sieu cao, Off=Tat.
Giu nguyen: ten cong nghe (FSR, DLSS, FXAA, CMAA, MBOIT, VSync, ...).
"""
import io
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?:\s*//.*)?)$')


MANUAL = {
    # --- Chat luong do hoa ---
    'citadel_settings_dfao_quality_low': 'Thấp',
    'citadel_settings_dfao_quality_med': 'Trung bình',
    'citadel_settings_dfao_quality_high': 'Cao',
    'citadel_settings_dfao_quality_ultra': 'Siêu cao',
    'citadel_settings_fogquality': 'Chất lượng sương mù',
    'citadel_settings_fogquality_low': 'Thấp',
    'citadel_settings_fogquality_high': 'Cao',
    'citadel_settings_texturequality': 'Chất lượng kết cấu',
    'citadel_settings_texturequality_low': 'Thấp',
    'citadel_settings_texturequality_med': 'Trung bình',
    'citadel_settings_texturequality_high': 'Cao',
    'citadel_settings_shadow_quality': 'Chất lượng bóng đổ',
    'citadel_settings_shadow_quality_low': 'Thấp',
    'citadel_settings_shadow_quality_med': 'Trung bình',
    'citadel_settings_shadow_quality_high': 'Cao',
    'citadel_settings_shadow_quality_ultra': 'Siêu cao',
    'citadel_settings_videopreset_ultra': 'Siêu cao',
    'citadel_settings_ssao_quality_ultra': 'Siêu cao',
    'citadel_settings_upscaling': 'Công nghệ nâng cấp hình ảnh',
    'citadel_settings_upscaling_none': 'Kéo giãn',
    'citadel_settings_antialiasing': 'Khử răng cưa',
    'citadel_settings_dlsstechnology': 'Mô hình DLSS',

    # --- Phu de ---
    'citadel_settings_subtitles_background_style_solid': 'Đặc',
    'citadel_settings_subtitles_background_style_outline': 'Viền',
    'citadel_settings_subtitles_font_stylized': 'Phong cách',

    # --- Cai dat chung ---
    'citadel_settings_video': 'Hình ảnh',
    'citadel_settings_crosshair_search': 'Crosshair tâm ngắm',
    'citadel_settings_crosshair_color_search': 'color màu',
    'citadel_settings_crosshair_outline_color_search': 'color màu',
    'citadel_keybind_map_new_setting': 'Sơ đồ phím tắt',
    'citadel_keybind_teleport_to_objective': 'Dịch chuyển',
    'citadel_keybind_cursorcontext': 'Menu',

    # --- Chat wheel ---
    'citadel_chatwheel_label_enemy_low_health': 'Máu thấp nghiêm trọng',
    'citadel_chatwheel_label_lane_yellow': 'York',
    'citadel_chatwheel_label_lane_blue': 'Broadway',
    'citadel_chatwheel_label_lane_green': 'Greenwich',

    # --- Ten bo cuc / khu vuc ---
    'map_district_sapphire': 'Archmother : ',
    'map_district_york': 'York : ',
    'map_district_broadway': 'Broadway : ',
    'map_district_park': 'Greenwich : ',
    'map_district_building_yorkside': 'York',

    # --- Khac ---
    'Citadel_DiscordInvite_Loading': 'Đang tải...',
    'Citadel_RecommendFriend_Email': 'Email:',
    'Citadel_JoinForum_Email': 'Email:',
    'Citadel_JoinForum_FieldEmail': 'Email',
    'Citadel_Menu': 'Menu',
    'Citadel_PrivateLobby_Versus': 'VS',
    'citadel_bot_name': 'Bot',
    'citadel_bot_name_prefix': '[Bot]',
    'Citadel_HUD_DamageReport_Time30': '30 giây',
    'Citadel_Profile_Stats_Value_Networth_High': '{i:game_networth}k',
    'citadel_ability_tier2boss_laser_beam': 'Laser',
    'citadel_ability_tier3boss_laser_beam': 'Patron Laser',
    'citadel_ability_tier3boss_aoe_wave': 'Patron',
    'citadel_ability_tier3boss_drop_bombs': 'Patron',
    'citadel_ability_tier3boss_rocket_barrage': 'Patron',
    'citadel_ability_drone_boss_laser_beam': 'Boss Laser',
    'modifier_citadel_dps_tracker': 'DPS',
    'Citadel_Post_Game_MVP': 'MVP',
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
        print('Khong tim thay:')

        for k in sorted(missing):
            print(f'  {k}')


if __name__ == '__main__':
    main()
