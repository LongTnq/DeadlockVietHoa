"""Sua tay nhom HeroTag con thieu dau + vai ky nang con tieng Anh."""
import io
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import translate_file as tf

ROOT = tf.ROOT
Q = chr(34)

FIX = {
    'citadel_heroes': {
        'citadel_ability_shieldedsentry': 'Tháp pháo nhỏ',
        'citadel_ability_chrono_pulse_grenade': 'Lựu đạn xung kích',
        'citadel_ability_void_sphere': 'Vòng quyền lượng tử',
        'Citadel_Haze_HeroTag_3': 'Sát thủ',
        'Citadel_Dynamo_HeroTag_2': 'Khởi xướng',
        'Citadel_Dynamo_HeroTag_3': 'Quyết đoán',
        'Citadel_Kelvin_HeroTag_2': 'Khám phá',
        'Citadel_Bebop_HeroTag_1': 'Câu móc',
        'Citadel_Bebop_HeroTag_2': 'Trâu bom',
        'Citadel_Bebop_HeroTag_3': 'Trâu bò',
        'Citadel_Astro_HeroTag_3': 'Nắm bắt',
        'Citadel_Nano_HeroTag_1': 'Xảo quyệt',
        'Citadel_Tengu_HeroTag_2': 'Phong tán',
        'Citadel_VampireBat_HeroTag_1': 'Nguy hiểm',
        'Citadel_Fencer_HeroTag_2': 'Cơ động',
        'Citadel_Unicorn_HeroTag_1': 'Biểu diễn',
        'Citadel_Deadpack_HeroTag_2': 'Bay cao',
        # Sai nghia - sua lai cho dung
        'Citadel_Geist_HeroTag_1': 'Hút máu',
        'Citadel_Doorman_HeroTag_1': 'Hóa loạn',
        'Citadel_Doorman_HeroTag_2': 'Kiểm soát bản đồ',
        'Citadel_Bookworm_HeroTag_1': 'Hữu ích',
        'Citadel_Nano_HeroTag_2': 'Trượt trơn',
        'Citadel_Wraith_HeroTag_3': 'Thôi miên',
        'Citadel_Chrono_HeroTag_2': 'Phá hoại',
        'Citadel_Engineer_HeroTag_3': 'Gây rối',
        'Citadel_Vindicta_HeroTag_3': 'Một phát hạ gục',
        'Citadel_Warden_HeroTag_1': 'Khởi xướng',
        'Citadel_Digger_HeroTag_2': 'Khởi xướng',
        'Citadel_Geist_HeroTag_2': 'Sát thương bản thân',
        'Citadel_Synth_HeroTag_3': 'Ếch nhái',
        'Citadel_Doorman_HeroTag_3': 'Trò chơi tâm trí',
        'Citadel_Priest_HeroTag_1': 'Sùng đạo',
        'Citadel_Mirage_HeroTag_1': 'Hộ vệ',
        'Citadel_Chrono_HeroTag_1': 'Tính toán',
    },
    'citadel_gc_mod_names': {
        'upgrade_inhibitor': 'Ức chế',
        'upgrade_inhibitor_search': 'Ức chế IH',
        'modifier_inhibitor_debuff': 'Ức chế',
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
