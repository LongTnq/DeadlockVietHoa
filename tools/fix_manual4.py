"""Sua tay cac entry bi quality guard loai do model khong them dau."""
import io
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import translate_file as tf

ROOT = tf.ROOT
Q = chr(34)

# folder -> {key: ban dich co dau day du}
FIX = {
    'citadel_main': {
        'Citadel_MatchHistory_Loss': 'Thua',
        'Citadel_Profile_Loss': 'Thua',
        'Citadel_Hud_Quickbuy': 'Mua nhanh',
        'Citadel_Hud_Quickbuy_Short': 'Mua nhanh',
        'Citadel_HeroBuilds_AddToQueue': 'Mua nhanh',
        'Citadel_ServerRegion_28': 'Ba Lan',
        'Citadel_ServerRegion_11': 'Nam Phi',
        'Citadel_Hud_KillFeedTrooper': 'Linh #{i:unit_num}',
        'Citadel_Hud_KillFeedNeutral': 'Linh ma #{i:unit_num}',
        'Citadel_TeamName_Neutral': 'Linh ma',
        'Citadel_Graph_Souls_Neutral': 'Linh ma',
        'guide_the_map_troopers_header': 'Linh',
        'Citadel_Playtest_DiscordButton': 'Tham gia Discord',
        'Citadel_Playtest_JoinDiscord': 'Tham gia Discord',
        'Citadel_JoinOrCreateParty_JoinParty': 'Tham gia',
        'Citadel_GraphGroup_General': 'Chung',
        'citadel_ability_tier2boss_laser_beam': 'Tia la-de',
        'citadel_settings_ping_wheel_slot1': 'Khe 1',
        'citadel_settings_ping_wheel_slot2': 'Khe 2',
        'citadel_settings_ping_wheel_slot3': 'Khe 3',
        'citadel_settings_ping_wheel_slot4': 'Khe 4',
        'citadel_settings_ping_wheel_slot5': 'Khe 5',
        'citadel_settings_ping_wheel_slot6': 'Khe 6',
        'citadel_settings_ping_wheel_slot7': 'Khe 7',
        'citadel_settings_ping_wheel_slot8': 'Khe 8',
        'Sandbox_Tutorial_Controls_PurchaseMods':
            'Mua {g:citadel_ability_name:\'upgrade_escalating_exposure\'}',
        'Citadel_GunTag_Bow': 'Cung',
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
                print('%s | %s -> %s' % (folder, key, new))
                hit = True
                break

            if not hit:
                print('KHONG TIM THAY %s %s' % (folder, key))

        io.open(f, 'w', encoding='utf-8', newline='').write('\n'.join(lines))


if __name__ == '__main__':
    main()
