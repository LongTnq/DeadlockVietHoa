"""Dich nhom HeroTag + ten bo vu khi (nhung giá tri tieng Anh con lai).

Nhom KHONG dich: Guardian/Walker/Patron/Amber/Sapphire (ten phe),
Deadlock, Valve, ten tuong (Ava, Ira Domini...), phim tat, format %s1.
"""
import io
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import translate_file as tf

ROOT = tf.ROOT
Q = chr(34)

FIX = {
    'citadel_heroes': {
        # HeroTag - vai tro
        'Citadel_Abrams_HeroTag_1': 'Tank',
        'Citadel_Abrams_HeroTag_2': 'Đấu sĩ',
        'Citadel_Geist_HeroTag_3': 'Sát thủ tuyệt đối',
        'Citadel_Tengu_HeroTag_3': 'Vững như đá',
        'Citadel_Punkgoat_HeroTag_1': 'Nổi loạn',
        'Citadel_Punkgoat_HeroTag_3': 'G.O.A.T.',
        'Citadel_Familiar_HeroTag_3': 'zZzzZ',
        # Ten bo / ky nang
        'citadel_ability_shiv_defer_damage': 'Rút ruột',
        'citadel_ability_shiv_killing_blow': 'Đòn kết liễu',
        'synth_barrage': 'Bão đạn',
        # Ten bo vu khi
        'citadel_weapon_slork_set': 'Riptide',
        'citadel_weapon_punkgoat_set': 'Biquette',
        'citadel_weapon_frank_set': 'Memento Mori',
        'citadel_weapon_vampirebat_set': 'New Money',
        'citadel_weapon_priest_set': '51:20',
        'citadel_weapon_unicorn_set': 'Carny Life',
        'citadel_weapon_opera_set': 'Opera Gun',
        'citadel_weapon_necro_set': 'The Teacher',
        'citadel_weapon_werewolf_wolf_set': 'Loose Muzzle',
        'citadel_weapon_familiar_set': 'Long Night',
        'citadel_weapon_fencer_set': 'Pride of Ixia',
        'citadel_weapon_targetdummy_set': 'Dummy Gun',
    },
    'citadel_attributes': {
        'upgrade_berserker/modifier_berserker/modifier_berserker_damage_stack':
            'Cuồng nộ',
        'berserker_stack': 'Cuồng nộ',
    },
    'citadel_gc_mod_names': {
        'upgrade_berserker': 'Cuồng nộ',
        'upgrade_berserker_search': 'Cuồng nộ Zerk',
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
