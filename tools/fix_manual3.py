"""Sua tay cac entry bi quality guard loai (thieu dau) hoac con tieng Anh."""
import io
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import translate_file as tf

ROOT = tf.ROOT
Q = chr(34)

# folder -> {key: ban dich moi}
FIX = {
    'citadel_heroes': {
        'ability_nano_clustergrenade': 'Bộ Bom Gloom',
        'modifier_spin': 'Xoáy',
        'ability_magician_copyult/copyult_modifier': 'Ta-da!',
        'zombiewall_rotate_hint': 'Xoáy',
    },
    'citadel_attributes': {
        'Attribute_EWeaponAttribute_Bow': 'Cung',
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
