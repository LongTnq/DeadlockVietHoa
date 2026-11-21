"""Sua 5 entry con thieu dau cuoi cung trong citadel_heroes."""
import io
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import translate_file as tf

ROOT = tf.ROOT
Q = chr(34)

FIX = {
    'citadel_heroes': {
        'modifier_void_sphere': 'Vòng quyền lượng tử',
        'citadel_ability_self_vacuum': 'Điểm đơn độc',
        'modifier_warden_riot_protocol': 'Phong tỏa cuối cùng',
        'ability_wrecking_ball': 'Binh hoa',
        'modifier_wrecking_ball_auto_throw': 'Binh hoa',
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
