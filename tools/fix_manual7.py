"""Sua tay cac entry con thieu dau (pass cuoi)."""
import io
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import translate_file as tf

ROOT = tf.ROOT
Q = chr(34)

FIX = {
    'citadel_heroes': {
        'citadel_ability_chrono_kinetic_carbine_charged': 'NẠP NĂNG LƯỢNG!',
        'mirage_fire_beetles': 'Scarab Lửa',
        'modifier_mirage_fire_scarabs_health_gain': 'Scarab Lửa',
        'modifier_mirage_fire_scarabs_health_loss': 'Scarab Lửa',
        'AbilityButtonHint_MagicianBigBoltFire': 'Bolt Lửa',
        'ability_trapper_spiderwave/trapper_stealspirit_buff': 'Biển Scurab',
        'ability_trapper_spiderwave/trapper_stealspirit_debuff': 'Biển Scurab',
        'modifier_drifter_hunger_stealth': 'Ẩm Tho',
        'ability_ratking_standard_bearer_charge_label': 'Hào Quang Biểu Ngữ',
        'ability_chessmaster_confirm_move_tip': 'Di chuyển',
        'ability_chessmaster_knightbuff_trigger': 'Đi nước vua',
        'ability_chessmaster_queen/lift': 'Kiểm tra...',
    },
    'citadel_main': {
        'Cancel': 'Huỷ',
        'Citadel_Party_CancelMM': 'Huỷ',
        'Citadel_RecommendFriend_Region_SA': 'Nam Mỹ',
        'Citadel_Region_NA': 'Bắc Mỹ',
        'Citadel_Region_SAmerica': 'Nam Mỹ',
        'Citadel_PrivateLobby_Region_SAmerica': 'Nam Mỹ',
        'Citadel_ServerRegion_10': 'Nam Mỹ',
        'Citadel_Hud_ToNext': 'KẾ TIẾP',
        'Citadel_Hud_CaptureZipLine':
            "NHẤN GIỮ {g:citadel_binding:'HeldItem'}",
        'Citadel_Hud_Denied': 'TỪ CHỐI',
        'Citadel_ShopFilter_Movement': 'Di chuyển',
        'CitadelShopFilterCategory_Movement': 'Di chuyển',
        'CitadelShopFilterCategory_Mobility': 'Cơ động',
        'citadel_build_tag_mobility': 'Cơ động',
        'movement_powerup_pickup': 'Di chuyển',
        'citadel_settings_movementkeybinds': 'Di chuyển',
        'Citadel_PrivateLobby_Difficulty_Easy': 'Bot Dễ',
        'Objective_MidBoss': 'Boss giữa',
        'Citadel_PlayerContextMenu_AudioVolume': 'Âm lượng',
        'Citadel_HeroPage_PlayerStats': 'THÔNG TIN NGƯỜI CHƠI',
        'Citadel_Graph_Team1': 'Vua Ẩn Danh',
        'Citadel_Steamshare_Title_Death_Player': 'Tử vong',
        'CitadelShopFilter_FilterOr': 'HOẶC',
        'citadel_settings_audio': 'Âm thanh',
        'citadel_settings_audiokeybinds': 'Âm thanh',
        'citadel_settings_gamevolume': 'Âm thanh gameplay',
        'citadel_settings_gamevolume_search': 'Hiệu ứng âm thanh',
        'menu_news': 'TIN TỨC',
        'menu_control_zoo': 'SỞ THÚ',
        'MyRosterAssignedOpenTesting': 'Thử nghiệm mở',
        'Citadel_Roster_TestHero': 'Thử',
        'HeroTesting_HoldAlt':
            "Giữ {g:citadel_binding:'Scoreboard'}",
        'HeroTesting_BotTestMode': 'Chế độ thử',
        'HeroTesting_BotMoveLeftRight': 'Sang bên',
        'Sandbox_Tutorial_Controls_HeavyMelee':
            "Giữ {g:citadel_binding:1st:'AbilityMelee'}",
        'Sandbox_Tutorial_Controls_Mantle':
            "Giữ {g:citadel_binding:1st:'Mantle'}",
        'Sandbox_Tutorial_Controls_task_Move': 'Di chuyển',
        'npc_male_patron': 'Vua Ẩn Danh',
        'Citadel_CombatLog_Deaths': 'Số lần tử vong',
        'Citadel_Minimap_Radial_Ping_Going': 'Đang di chuyển',
    },
    'citadel_attributes': {
        'Citadel_StatusEffectDisplacement': 'Dịch chuyển',
    },
}


def main():
    for folder, mapping in FIX.items():
        f = (ROOT + '\\workspace\\staging\\localization\\' + folder + '\\' +
             folder + '_vietnamese.txt')
        lines = io.open(f, encoding='utf-8-sig').read().split('\n')

        for key, new in mapping.items():
            hit = False

            for i, l in enumerate(lines):
                m = tf.LINE_RE.match(l.rstrip('\r'))

                if not m:
                    continue

                if m.group(1).strip().strip(Q) != key:
                    continue

                lines[i] = m.group(1) + Q + tf.escape(new) + Q + m.group(3)
                hit = True
                break

            if not hit:
                print('KHONG TIM THAY %s %s' % (folder, key))

        io.open(f, 'w', encoding='utf-8', newline='').write('\n'.join(lines))
        print('%s: da sua %d entry' % (folder, len(mapping)))


if __name__ == '__main__':
    main()
