#!/usr/bin/env python
"""Sinh ban phat hanh cho mod tieng Viet Deadlock (tuong thich DMM).

Goi y kien thuc:
  Game doc file localization theo ngon ngu hien tai, tu thu muc
  `resource/localization/<folder>/<file>_<ngon ngu>.txt`.

  - Loose file (`.txt`) la cach Valve ship ban ngon ngu.
  - Game mount theo thu tu trong `gameinfo.gi`:
        Game_UILanguage  citadel_*LANGUAGE*   <- UATIEN, ghi de moi thu
        Game              citadel/addons      <- VPK cua mod
        Game              citadel             <- file goc cua game

  => Mot VPK trong `citadel/addons/` co the ghi de file `.txt` loose,
     vi engine luon tim trong VPK truoc loose file cung duong dan.

Vi du ban phat hanh:
  vpk/pak01_dir.vpk + pak01_000.vpk
      chua `resource/localization/<folder>/<file>_english.txt` (da dich)
      -> dung khi nguoi dung giu ngon ngu English, DMM quan ly toan bo.

  vpk/pak01_dir.vpk + pak01_000.vpk
      chua `resource/localization/<folder>/<file>_vietnamese.txt` (da dich)
      -> dung khi nguoi dung chon ngon ngu "Tieng Viet" trong game.
"""
import io
import os
import re
import shutil
import subprocess
import sys
import zipfile

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = os.path.join(ROOT, 'workspace', 'staging', 'mod', 'resource', 'localization')
FONTS_SRC = os.path.join(ROOT, 'workspace', 'staging', 'mod', 'panorama', 'fonts',
                         'fonts.conf')
RELEASE = os.path.join(ROOT, 'release')
PKG = os.path.join(RELEASE, 'deadlock-vietnamese')
ZIP = os.path.join(RELEASE, 'deadlock-vietnamese.zip')
VPKEDIT = r'C:\Program Files\VPKEdit\vpkeditcli.exe'

# Chi mot bien the: English.
#
# Bien the "Tieng Viet" (file *_vietnamese.txt + `"vietnamese" "3"` trong
# `gameinfo.gi`) bi loai bo vi:
#   1. Game chi hien thi ngon ngữ khai bao trong `SupportedLanguages` cua
#      `gameinfo.gi`. Steam khoi phuc file nay khi Verify integrity -> mat
#      "Tieng Viet" trong Settings > Language.
#   2. VPK trong `citadel/addons` chi duoc load neu `gameinfo.gi` co
#      `Game citadel/addons` trong `SearchPaths` - cung bi Steam xoa.
#
# Bien the English giai quyet ca hai: nguoi dung giu ngon ngữ game la English
# (da co san, khong can khai bao them), ma noi dung hien thi la tieng Viet.
# `tools/patch_gameinfo.py` them lai dong `Game citadel/addons` khi can.
VARIANTS = [
    ('english', '_english.txt', 'english'),
]

# Dieu kien `lang` trong fonts.conf cho tung bien the.
FONT_LANG = {
    'english': 'en-us',
    'vietnamese': 'vi-vn',
}


def log(msg):
    print(msg, flush=True)


def collect():
    """Tra ve [(duong dan nguon, thu muc)] cho moi file da dich."""
    items = []

    if not os.path.isdir(STAGING):
        log('[LOI] Khong thay staging: %s' % STAGING)
        return items

    for folder in sorted(os.listdir(STAGING)):
        fdir = os.path.join(STAGING, folder)

        if not os.path.isdir(fdir):
            continue

        for name in sorted(os.listdir(fdir)):
            if name.endswith('_vietnamese.txt'):
                items.append((os.path.join(fdir, name), folder))

    return items


def write_variant(src, dst, lang_value):
    """Copy file dich va doi truong `Language` cho khop bien the.

    Giu nguyên BOM: game doc file localization dang UTF-8 co BOM.
    """
    raw = open(src, 'rb').read()
    has_bom = raw.startswith(b'\xef\xbb\xbf')
    text = raw.decode('utf-8-sig', errors='replace')
    text = re.sub(r'("Language"\s+")[^"]*(")',
                  lambda m: m.group(1) + lang_value + m.group(2),
                  text, count=1)
    data = text.encode('utf-8')
    if has_bom:
        data = b'\xef\xbb\xbf' + data
    with open(dst, 'wb') as f:
        f.write(data)


def rewrite_fallback_lang(text, font_lang):
    """Doi dieu kien `lang` trong block fallback cua mod.

    Block cua mod nam giua marker `Deadlock Vietnamese Localization` va
    marker `Run infctl.sh`. Chi doi trong do, de khong pha quy tac `en-us`
    cua chinh Valve (Radiance -> Retail Demo).
    """
    start_marker = 'Deadlock Vietnamese Localization'
    end_marker = 'Run infctl.sh'

    i = text.find(start_marker)

    if i < 0:
        return text

    j = text.find(end_marker, i)

    if j < 0:
        return text

    block = text[i:j]
    block = block.replace('<string>vi-vn</string>',
                          '<string>%s</string>' % font_lang)
    block = block.replace('<string>en-us</string>',
                          '<string>%s</string>' % font_lang)

    return text[:i] + block + text[j:]


def build_vpk(items, variant_dir, suffix, lang_value, out_vpk):
    """Dong goi mot bien the thanh VPK voi duong dan resource/localization/..."""
    tmp = os.path.join(ROOT, 'workspace', 'staging', '_vpkbuild')

    if os.path.exists(tmp):
        shutil.rmtree(tmp)

    for src, folder in items:
        dst_dir = os.path.join(tmp, 'resource', 'localization', folder)
        os.makedirs(dst_dir, exist_ok=True)

        base = os.path.basename(src)[:-len('_vietnamese.txt')]
        write_variant(src, os.path.join(dst_dir, base + suffix), lang_value)

    # Font fallback: bo sung font tieng Viet cho cac font cua game.
    # Dieu kien `lang` phai khop voi ngon ngu game dang chay.
    if os.path.exists(FONTS_SRC):
        dst_font = os.path.join(tmp, 'panorama', 'fonts', 'fonts.conf')
        os.makedirs(os.path.dirname(dst_font), exist_ok=True)
        raw = io.open(FONTS_SRC, encoding='utf-8', errors='replace').read()
        raw = rewrite_fallback_lang(raw, FONT_LANG[lang_value])
        with io.open(dst_font, 'w', encoding='utf-8', newline='') as f:
            f.write(raw)

    if os.path.exists(out_vpk):
        os.remove(out_vpk)

    if not os.path.exists(VPKEDIT):
        log('[LOI] Khong thay VPKEdit: %s' % VPKEDIT)
        return False

    cmd = [VPKEDIT, '--no-progress', '--type', 'vpk', '--version', '2',
           '--chunksize', '200', '--output', out_vpk, tmp]
    r = subprocess.run(cmd, capture_output=True, text=True)

    if r.returncode != 0:
        log('[LOI] VPKEdit that bai:\n%s\n%s' % (r.stdout, r.stderr))
        return False

    shutil.rmtree(tmp, ignore_errors=True)

    return True


README_MD = """# Deadlock - Vietnamese Localization

Ban dich tieng Viet cho game Deadlock (Valve).

## Cai dat

### Cach 1 - Deadlock Mod Manager (khuyen nghi)

1. Mo Deadlock Mod Manager -> **Mods Store** -> tim "Vietnamese Localization".
2. Bam **Download Mod**.
3. Mo **Mods Library** -> bat mod "Vietnamese Localization" -> **Enable**.
4. Bam **Launch modded**.

### Cach 2 - Cai tay

Giai nen `.zip` nay, ban duoc 2 file `.vpk`. Copy ca 2 vao
`<game>/game/citadel/addons/`:

```text
pak01_dir.vpk
pak01_000.vpk
```

Sau do mo `<game>/game/citadel/gameinfo.gi` va them dong
`Game citadel/addons` vao trong khoi `SearchPaths`, **truoc** dong
`Game citadel`:

```text
		SearchPaths
		{
			Game_UILanguage		citadel_*LANGUAGE*
			Game_LowViolence	citadel_lv
			Game				citadel/addons
			Game				citadel
			Game				core
		}
```

> Neu Steam da **Verify integrity of game files**, dong nay se bi xoa. them
> lai hoac chay `tools/patch_gameinfo.py` tu ban phat hanh.

Mo game. Hieu luc ngay.

## Luu y

- File localization tieng Anh cua game (`*_english.txt`) bi thay bang ban
  dich. Game van hien thi "English" trong Settings > Language nhung **noi
  dung la tieng Viet**.
- Do do khong can them `"vietnamese"` vao `SupportedLanguages`, va khong co
  muc "Tieng Viet" trong Settings > Language. Day la biet canh.
- Mod khong sua file game goc. Hay **Verify integrity of game files** cua
  Steam de khoi phuc neu can (nho them lai dong `Game citadel/addons`).

## Noi dung da dich

| Phan | Trang thai |
|---|---|
| Giao dien (UI) | ~99% |
| Mo ta hero + lore | ~100% |
| Ky nang (abilities) | ~100% |
| Vat pham (items) | 100% |
| Chi so (attributes) | 100% |
| Giong noi (voice lines) | Chua dich |

> Mot so hinh anh trong game (banner hero, bieu tuong thanh tich) chi co ban
> tieng Anh - day la gioi han cua ban dich phan chu.

## Credits

- **Key Authors**: nickname GameBanana cua ban - ban dich tieng Viet, dong goi VPK
- **Original Authors**: Valve - Deadlock, chuoi localization tieng Anh goc
- **Contributors**: nguoi da giup sua hoac tham dinh ban dich
- **Special Thanks**: cong dong Deadlock Viet

## Source

- **Ma nguon**: https://github.com/LongTnq/DeadlockVietHoa
"""


def main():
    log('=== DONG GOI BAN PHAT HANH ===')
    log('')

    items = collect()

    if not items:
        log('[LOI] Khong tim thay file dich nao.')
        return 1

    log('  Tim thay %d file da dich.' % len(items))
    log('')

    if os.path.exists(PKG):
        shutil.rmtree(PKG)

    os.makedirs(PKG, exist_ok=True)

    for variant_dir, suffix, lang_value in VARIANTS:
        log('  [%s] Dong goi VPK...' % variant_dir)

        vdir = os.path.join(PKG, 'vpk-' + variant_dir)
        os.makedirs(vdir, exist_ok=True)
        out_vpk = os.path.join(vdir, 'pak01_dir.vpk')

        if not build_vpk(items, vdir, suffix, lang_value, out_vpk):
            return 1

        chunk = os.path.join(vdir, 'pak01_000.vpk')
        log('    pak01_dir.vpk : %s bytes' % f'{os.path.getsize(out_vpk):,}')

        if os.path.exists(chunk):
            log('    pak01_000.vpk : %s bytes' % f'{os.path.getsize(chunk):,}')
        else:
            log('    [LOI] Thieu chunk pak01_000.vpk!')
            return 1

        log('')

    log('  Viet README...')

    with io.open(os.path.join(PKG, 'README.md'), 'w', encoding='utf-8') as f:
        f.write(README_MD)

    log('  Nen ZIP...')

    if os.path.exists(ZIP):
        os.remove(ZIP)

    with zipfile.ZipFile(ZIP, 'w', zipfile.ZIP_DEFLATED) as z:
        for dirpath, _dirs, files in os.walk(PKG):
            for name in files:
                full = os.path.join(dirpath, name)
                arc = os.path.relpath(full, PKG).replace('\\', '/')
                z.write(full, arc)

    log('    ZIP gop: %s bytes -> %s' % (f'{os.path.getsize(ZIP):,}', ZIP))

    # GameBanana khuyen nghi tach moi bien the thanh mot archive rieng
    # (variants cung slot gay loi cho 1-click installer).
    log('  Nen ZIP tung bien the...')

    for variant_dir, _suffix, _lang in VARIANTS:
        vdir = os.path.join(PKG, 'vpk-' + variant_dir)
        out_zip = os.path.join(RELEASE,
                               'deadlock-vietnamese-%s.zip' % variant_dir)

        if os.path.exists(out_zip):
            os.remove(out_zip)

        with zipfile.ZipFile(out_zip, 'w', zipfile.ZIP_DEFLATED) as z:
            for name in sorted(os.listdir(vdir)):
                z.write(os.path.join(vdir, name), name)

            z.write(os.path.join(PKG, 'README.md'), 'README.md')

        log('    %s: %s bytes' % (os.path.basename(out_zip),
                                  f'{os.path.getsize(out_zip):,}'))

    log('')
    log('=== HOAN TAT ===')
    log('  Upload len GameBanana (2 file rieng, de vao Files):')
    for variant_dir, _suffix, _lang in VARIANTS:
        log('    - deadlock-vietnamese-%s.zip' % variant_dir)
    log('')
    log('  GameBanana -> Deadlock -> Mods -> Submit a new mod')
    log('')

    return 0


if __name__ == '__main__':
    sys.exit(main())
