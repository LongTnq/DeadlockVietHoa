#!/usr/bin/env python
"""Kiem tra noi dung tieng Viet trong mot VPK da giai nen.

Dung:  python tools/verify_vpk.py <thu_muc_giai_nen>
"""
import os
import re
import sys

VN_CHARS = re.compile(
    '[àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệ'
    'ìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụ'
    'ưừứửữựỳýỷỹỵđÀÁẢÃẠĂÂÈÉÊÌÍÒÓÔƠÙÚÝĐ]'
)


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else '.'
    total_vn = 0
    files = 0
    no_bom = []

    for dirpath, _dirs, names in os.walk(root):
        for name in sorted(names):
            if not name.endswith('.txt'):
                continue

            path = os.path.join(dirpath, name)
            raw = open(path, 'rb').read()

            if not raw.startswith(b'\xef\xbb\xbf'):
                no_bom.append(path)

            text = raw.decode('utf-8-sig', errors='replace')
            n = len(VN_CHARS.findall(text))
            total_vn += n
            files += 1

            lang = re.search(r'"Language"\s+"([^"]*)"', text)
            lang = lang.group(1) if lang else '?'

            rel = os.path.relpath(path, root)
            print('%-70s lang=%-12s dau=%d' % (rel, lang, n))

    print('')
    print('Tong: %d file, %d ky tu co dau' % (files, total_vn))

    if no_bom:
        print('')
        print('[CANH BAO] %d file thieu BOM:' % len(no_bom))
        for p in no_bom:
            print('  ', p)

    if total_vn == 0:
        print('')
        print('[LOI] Khong co ky tu co dau nao -> VPK khong chua ban dich tieng Viet!')
        return 1

    return 0


if __name__ == '__main__':
    sys.exit(main())
