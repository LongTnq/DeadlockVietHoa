"""Kiem tra phong Windows co ho tro day du dau tieng Viet khong."""
from fontTools.ttLib import TTFont

tests = {
    'Aacute': 0xC1, 'Etilde': 0xC9, 'Ograve': 0xD2, 'uhorn': 0xDD,
    'yacute': 0xFD, 'ydieresis': 0xFF, 'ecircumflex': 0xEA,
    'ocircumflex': 0xF4, 'acircumflex': 0xE2, 'ohorn': 0x1ED,
    'uocircumflex_tilde': 0x1EE6, 'a_breve': 0x103,
}

for fn in ['arial.ttf', 'arialbd.ttf', 'segoeui.ttf', 'seguisb.ttf',
           'calibri.ttf', 'tahoma.ttf', 'verdana.ttf', 'DejaVuSans.ttf']:
    try:
        f = TTFont(r'C:\Windows\Fonts\\' + fn, fontNumber=0)
        cm = f.getBestCmap()
        miss = [k for k, v in tests.items() if v not in cm]
        print(f'{fn:20s} miss={len(miss)} {miss}')
    except Exception as e:
        print(f'{fn:20s} ERR {e}')
