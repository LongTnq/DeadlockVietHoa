"""Chen khoi font fallback tieng Viet vao fonts.conf (idempotent).

Steam periodically overwrites the game's fonts.conf, so this script can be
re-run at any time to restore the Vietnamese fallback block.
"""
import re
import sys

path = sys.argv[1] if len(sys.argv) > 1 else (
    r'E:\Steam\steamapps\common\Deadlock\game\citadel\panorama\fonts\fonts.conf')

BLOCK = '''
\t<!--
\t\tDeadlock Vietnamese Localization: font fallback.

\t\tTat ca font cua game deu THIEU nhieu glyph tieng Viet — "ơ" (U+01ED),
\t\t"ư" (U+01B0), "ữ" (U+1EE6), "đ" (U+0111)… Noto Sans JP co du hau
\t\tcac dau khac nhung van thieu hai glyph nay.

\t\tArial va Segoe UI cua Windows ho tro day du bo chu Viet, nen duoc
\t\tthem vao cuoi danh sach fallback: fontconfig chi dung chung cho
\t\tky tu ma font chinh khong co, nen phan lon van ban van giu dung
\t\tfont goc cua game.

\t\tKhong dung <test name="lang">vi-vn</test> de dieu kien hoa: game
\t\tkhong chắc dat locale vi-vn cho process, nen dieu kien do co the
\t\tkhong bao gio thoa. Che do "append_last" chi them vao CUOI danh
\t\tsach, nen anh huong chi xuat hien voi ky tu bi thieu o font goc.
\t-->
\t<match>
\t\t<edit name="family" mode="append_last" binding="strong">
\t\t\t<string>Arial</string>
\t\t</edit>
\t</match>
\t<match>
\t\t<edit name="family" mode="append_last" binding="strong">
\t\t\t<string>Segoe UI</string>
\t\t</edit>
\t</match>
'''

ANCHOR = '\t<!-- Run infctl.sh or change the symlink in current directory instead of modifying this -->'

# Khoi fallback co the ban cu (co dieu kien lang=vi-vn) — can xoa de
# thay bang phien ban moi, khong dieu kien.
# Regex phai gom ca cac <match> con lai cua khoi cu, ke ca <test lang>.
OLD_BLOCK_RE = re.compile(
    r'\n\t<match>\n\t\t<test name="lang">\n\t\t\t<string>vi-vn</string>\n'
    r'\t\t</test>\n\t\t<edit name="family" mode="append_last" binding="strong">\n'
    r'\t\t\t<string>[A-Za-z ]+</string>\n\t\t</edit>\n\t</match>\n')

with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

if 'Deadlock Vietnamese Localization: font fallback' in content:
    content = OLD_BLOCK_RE.sub('\n', content)

    # Xoa comment + 2 <match> cua phien ban co dieu kien lang.
    content = re.sub(
        r'\n\t<!--\n\t\tDeadlock Vietnamese Localization: font fallback\..*?'
        r'\n\t</match>\n',
        '\n',
        content,
        flags=re.DOTALL)

    if 'Deadlock Vietnamese Localization: font fallback' not in content:
        content = content.replace(ANCHOR, BLOCK + ANCHOR, 1)
        print('Da thay khoi fallback cu (co dieu kien lang) bang phien ban moi.')
    else:
        print('Da loai bo phien ban cu, giu nguyen phien ban hien tai.')
elif ANCHOR in content:
    content = content.replace(ANCHOR, BLOCK + ANCHOR, 1)
    print(f'Da chen khoi fallback tieng Viet vao {path}')
else:
    print('Khong tim thay diem chen.')
    sys.exit(1)

# Quet lai: khong duoc con <test name="lang">vi-vn</test> nao.
if 'vi-vn' in content:
    print('CANH BAO: con "vi-vn" trong fonts.conf — kiem tra lai.')

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

print(f'Kich thuoc moi: {len(content):,} bytes')
