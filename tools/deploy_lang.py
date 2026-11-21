"""
Deploy bang dich tieng Viet theo dung co che cua game.

Game Deadlock luu file localization theo hai noi:

1. LOOSE trong `game/citadel/resource/localization/...` — giong nhu
   `citadel_main_thai.txt` (692 KB) hay `citadel_main_english.txt` (418 KB).
   Day la noi game doc truc tiep. Nhung Steam kiem tra file (verify) va
   ghi de moi thay doi — file rong 60 byte cua Valve se quay lai.

2. VPK trong thu muc rieng `game/citadel_<lang>/` — dung kieu cua Valve.
   `gameinfo.gi` cua game khai bao `Game_UILanguage citadel_*LANGUAGE*`,
   nen engine se mount `citadel_vietnamese` TRUOC cac thu muc khac khi
   nguoi choi chon tieng Viet. Steam khong quan ly thu muc nay.

Script nay lam CA HAI:
- chep file .txt loose vao `citadel/resource/localization/`
- dong goi VPK vao `citadel_vietnamese/`
- them "vietnamese" vao SupportedLanguages cua gameinfo.gi

Va them ca font fallback vao `fonts.conf`.
"""
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
GAME = r'E:\Steam\steamapps\common\Deadlock\game'
CITADEL = os.path.join(GAME, 'citadel')
LANG_DIR = os.path.join(GAME, 'citadel_vietnamese')
LOOSE_ROOT = os.path.join(CITADEL, 'resource', 'localization')
VPKEDIT = r'C:\Program Files\VPKEdit\vpkeditcli.exe'

LANGUAGE = 'vietnamese'

GAMEINFO_TEMPLATE = '''"GameInfo"
{{
\tLayeredOnMod\tcitadel

\tFileSystem
\t{{
\t\tSearchPaths
\t\t{{
\t\t\tGame\t\t\t\tcitadel_{lang}
\t\t\tGame\t\t\t\tcitadel
\t\t\tGame\t\t\t\tcore

\t\t\tMod\t\t\t\t\tcitadel_{lang}
\t\t\tMod\t\t\t\t\tcitadel
\t\t}}
\t}}
}}
'''

FONT_BLOCK = '''
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

FONT_ANCHOR = ('\t<!-- Run infctl.sh or change the symlink in current directory '
               'instead of modifying this -->')


def log(msg):
    print(msg, flush=True)


def staging_txt_files():
    """Tra ve danh sach file .txt trong staging, kem duong dan tuong doi."""
    staging = os.path.join(ROOT, 'workspace', 'staging', 'mod')
    result = []

    for dirpath, _dirs, files in os.walk(staging):
        for name in files:
            if not name.lower().endswith('.txt'):
                continue
            src = os.path.join(dirpath, name)
            rel = os.path.relpath(src, staging).replace('\\', '/')
            result.append((src, rel))

    return result


def copy_loose(files):
    """Chep file .txt vao `citadel/resource/localization/` (giong game).

    LUU Y: duong dan trong staging/mod bat dau bang "resource/localization/",
    nhung thu muc dich cua game da la `citadel/resource/localization/`.
    Phai cat tien to "resource/localization/" di, neu khong file se bi chep
    vao `citadel/resource/localization/resource/localization/...` — sai.
    """
    log('[1/4] Chep file .txt vao thu muc game...')

    count = 0

    for src, rel in files:
        rel = rel.replace('\\', '/')

        prefix = 'resource/localization/'

        if rel.startswith(prefix):
            rel = rel[len(prefix):]

        dst = os.path.join(LOOSE_ROOT, rel.replace('/', os.sep))
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copy2(src, dst)
        count += 1

    log(f'  da chep {count} file')
    return count


def build_vpk(files, out_vpk):
    """Dong goi file .txt thanh VPK voi duong dan goc 'resource/...'."""
    tmp = tempfile.mkdtemp(prefix='dlvn_vpk_')

    try:
        for src, rel in files:
            dst = os.path.join(tmp, rel.replace('/', os.sep))
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.copy2(src, dst)

        args = [
            VPKEDIT, '--no-progress', '--type', 'vpk', '--version', '2',
            '--chunksize', '200', '--output', out_vpk, tmp,
        ]
        result = subprocess.run(
            args, capture_output=True, text=True, timeout=900)

        if result.returncode != 0:
            log(f'  VPKEdit that bai:\n{result.stdout}\n{result.stderr}')
            return False

        return True
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def install_vpk(files):
    log('[2/4] Dong goi VPK cho thu muc ngon ngu...')

    os.makedirs(LANG_DIR, exist_ok=True)

    out_vpk = os.path.join(LANG_DIR, 'pak01_dir.vpk')

    if not build_vpk(files, out_vpk):
        return False

    # VPKEdit tach du lieu sang file chunk <ten>_000.vpk.
    chunk = os.path.join(LANG_DIR, 'pak01_000.vpk')
    chunk_size = os.path.getsize(chunk) if os.path.exists(chunk) else 0

    if chunk_size == 0:
        log('  CANH BAO: thieu file chunk pak01_000.vpk!')

    log(f'  pak01_dir.vpk : {os.path.getsize(out_vpk):,} bytes')
    log(f'  pak01_000.vpk : {chunk_size:,} bytes')

    gi = os.path.join(LANG_DIR, 'gameinfo.gi')
    with open(gi, 'w', encoding='utf-8', newline='\r\n') as f:
        f.write(GAMEINFO_TEMPLATE.format(lang=LANGUAGE))

    log(f'  {gi}')
    return True


def ensure_language_supported():
    """Them "vietnamese" vao SupportedLanguages cua gameinfo.gi.

    BAT BUOC: game chi hien thi ngon ngu trong menu setting neu ten
    ngon ngu co trong khoi nay. Steam ghi de gameinfo.gi khi verify
    file, nen can chay lai sau moi lan update game.
    """
    log('[3/4] Cap nhat gameinfo.gi cua game...')

    gi = os.path.join(CITADEL, 'gameinfo.gi')

    with open(gi, 'r', encoding='utf-8', errors='replace') as f:
        text = f.read()

    if f'"{LANGUAGE}"' in text:
        log(f'  da co "{LANGUAGE}" trong SupportedLanguages.')
        return True

    m = re.search(r'SupportedLanguages\s*\{', text)
    if not m:
        log('  KHONG tim thay khoi SupportedLanguages!')
        return False

    depth = 0
    end = None
    for i in range(m.end() - 1, len(text)):
        if text[i] == '{':
            depth += 1
        elif text[i] == '}':
            depth -= 1
            if depth == 0:
                end = i
                break

    if end is None:
        log('  khong dong duoc khoi SupportedLanguages.')
        return False

    text = text[:end] + f'\t\t"{LANGUAGE}" "3"\r\n' + text[end:]

    with open(gi, 'w', encoding='utf-8', newline='') as f:
        f.write(text)

    log(f'  da them "{LANGUAGE}" "3" vao SupportedLanguages.')
    return True


def patch_fonts():
    log('[4/4] Cap nhat font fallback cho tieng Viet...')

    conf = os.path.join(CITADEL, 'panorama', 'fonts', 'fonts.conf')

    if not os.path.exists(conf):
        log('  KHONG tim thay fonts.conf')
        return False

    with open(conf, 'r', encoding='utf-8', errors='replace') as f:
        text = f.read()

    if 'Deadlock Vietnamese Localization: font fallback' in text:
        log('  da co khoi fallback.')
        return True

    if FONT_ANCHOR not in text:
        log('  KHONG tim thay diem chen trong fonts.conf')
        return False

    text = text.replace(FONT_ANCHOR, FONT_BLOCK + FONT_ANCHOR, 1)

    with open(conf, 'w', encoding='utf-8', newline='') as f:
        f.write(text)

    log('  da them Arial + Segoe UI vao chuoi fallback vi-vn.')
    return True


def verify(files):
    log('')
    log('=== KIEM TRA ===')

    # 1. File loose trong game
    ok = 0
    for _src, rel in files:
        rel = rel.replace('\\', '/')

        prefix = 'resource/localization/'

        if rel.startswith(prefix):
            rel = rel[len(prefix):]

        dst = os.path.join(LOOSE_ROOT, rel.replace('/', os.sep))
        if os.path.exists(dst) and os.path.getsize(dst) > 1000:
            ok += 1

    log(f'  file loose co noi dung : {ok}/{len(files)}')

    # 2. VPK
    for name in ('gameinfo.gi', 'pak01_dir.vpk', 'pak01_000.vpk'):
        p = os.path.join(LANG_DIR, name)
        if os.path.exists(p):
            log(f'  OK  {name:20s} {os.path.getsize(p):>12,} bytes')
        else:
            log(f'  THIEU {name}')

    # 3. gameinfo.gi
    gi = os.path.join(CITADEL, 'gameinfo.gi')
    with open(gi, 'r', encoding='utf-8', errors='replace') as f:
        if f'"{LANGUAGE}"' in f.read():
            log(f'  SupportedLanguages: co "{LANGUAGE}"')
        else:
            log(f'  SupportedLanguages: THIEU "{LANGUAGE}"')

    # 4. fonts.conf
    conf = os.path.join(CITADEL, 'panorama', 'fonts', 'fonts.conf')
    with open(conf, 'r', encoding='utf-8', errors='replace') as f:
        if 'Deadlock Vietnamese Localization' in f.read():
            log('  fonts.conf: co fallback vi-vn')
        else:
            log('  fonts.conf: THIEU fallback vi-vn')


def main():
    files = staging_txt_files()

    if not files:
        log('KHONG tim thay file .txt trong staging.')
        log('Chay export-to-game truoc.')
        return 1

    log(f'Tim thay {len(files)} file .txt can deploy.')
    log('')

    copy_loose(files)
    install_vpk(files)
    ensure_language_supported()
    patch_fonts()

    verify(files)

    log('')
    log('=== HOAN TAT ===')
    log('  Vao game, Settings > Language > Tieng Viet.')
    log('')
    log('  Neu game da mo, phai DONG GAME het roi mo lai.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
