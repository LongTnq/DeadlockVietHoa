"""Kiem tra coverage glyph tieng Viet cho font cua game + font Windows."""
import io
import os
import struct
import unicodedata

BASE = r'E:\Steam\steamapps\common\Deadlock\game\citadel\resource\localization'
FONTS_DIR = r'E:\Steam\steamapps\common\Deadlock\game\citadel\panorama\fonts'

WIN_FONTS = [
    r'C:\Windows\Fonts\arial.ttf',
    r'C:\Windows\Fonts\segoeui.ttf',
    r'C:\Windows\Fonts\tahoma.ttf',
    r'C:\Windows\Fonts\verdana.ttf',
]


def read_cmap(path):
    try:
        data = io.open(path, 'rb').read()
    except OSError:
        return None

    if len(data) < 12:
        return None

    num_tables = struct.unpack('>H', data[4:6])[0]
    cmap_off = None

    for i in range(num_tables):
        rec = 12 + i * 16
        if rec + 16 > len(data):
            break
        if data[rec:rec + 4] == b'cmap':
            cmap_off = struct.unpack('>I', data[rec + 8:rec + 12])[0]
            break

    if cmap_off is None or cmap_off + 4 > len(data):
        return None

    chars = set()
    n = struct.unpack('>H', data[cmap_off + 2:cmap_off + 4])[0]

    for i in range(n):
        rec = cmap_off + 4 + i * 8
        if rec + 8 > len(data):
            break
        sub_off = struct.unpack('>I', data[rec + 4:rec + 8])[0]
        sub = cmap_off + sub_off
        if sub + 4 > len(data):
            continue
        fmt = struct.unpack('>H', data[sub:sub + 2])[0]

        if fmt == 4:
            segx2 = struct.unpack('>H', data[sub + 6:sub + 8])[0]
            seg = segx2 // 2
            ends = sub + 14
            starts = ends + segx2 + 2
            deltas = starts + segx2
            ranges = deltas + segx2
            for s in range(seg):
                e = struct.unpack('>H', data[ends + s * 2:ends + s * 2 + 2])[0]
                st = struct.unpack('>H', data[starts + s * 2:starts + s * 2 + 2])[0]
                if st == 0xFFFF:
                    continue
                delta = struct.unpack('>h', data[deltas + s * 2:deltas + s * 2 + 2])[0]
                ro = struct.unpack('>H', data[ranges + s * 2:ranges + s * 2 + 2])[0]
                for c in range(st, min(e, 0xFFFE) + 1):
                    if ro == 0:
                        g = (c + delta) & 0xFFFF
                    else:
                        gi = ranges + s * 2 + ro + (c - st) * 2
                        if gi + 2 > len(data):
                            continue
                        g = struct.unpack('>H', data[gi:gi + 2])[0]
                        if g:
                            g = (g + delta) & 0xFFFF
                    if g:
                        chars.add(c)
        elif fmt == 12:
            ngroups = struct.unpack('>I', data[sub + 12:sub + 16])[0]
            for g in range(ngroups):
                go = sub + 16 + g * 12
                if go + 12 > len(data):
                    break
                sc, ec, _sg = struct.unpack('>III', data[go:go + 12])
                for c in range(sc, min(ec, sc + 0x1000) + 1):
                    chars.add(c)

    return chars


used = {}

for dp, _dn, fn in os.walk(BASE):
    for f in fn:
        if not f.endswith('.txt'):
            continue
        t = io.open(os.path.join(dp, f), encoding='utf-8', errors='replace').read()
        for ch in t:
            if ord(ch) > 127:
                used[ch] = used.get(ch, 0) + 1

# Chỉ lấy ký tự Latin mở rộng (tiếng Việt dùng)
viet = sorted(c for c in used
               if 0x00C0 <= ord(c) <= 0x024F or 0x1E00 <= ord(c) <= 0x1EFF)

print(f'KY TU LATIN-MO-RONG dang dung: {len(viet)} loai')
print(' '.join(f'{c}({used[c]})' for c in viet))
print()

game_fonts = []

for dp, _dn, fn in os.walk(FONTS_DIR):
    for f in fn:
        if f.lower().endswith(('.ttf', '.otf')):
            game_fonts.append(os.path.join(dp, f))

print(f'=== FONT GAME ({len(game_fonts)}) ===')
all_game = set()

for f in game_fonts:
    cs = read_cmap(f)
    if not cs:
        print(f'  {os.path.basename(f):45s} (doc that bai)')
        continue
    all_game |= cs
    miss = [c for c in viet if ord(c) not in cs]
    print(f'  {os.path.basename(f):45s} thieu {len(miss):3d}  {"".join(miss)}')

print()
print('=== FONT WINDOWS ===')
all_win = set()

for f in WIN_FONTS:
    if not os.path.exists(f):
        print(f'  {os.path.basename(f):45s} (khong ton tai)')
        continue
    cs = read_cmap(f)
    if not cs:
        print(f'  {os.path.basename(f):45s} (doc that bai)')
        continue
    all_win |= cs
    miss = [c for c in viet if ord(c) not in cs]
    print(f'  {os.path.basename(f):45s} thieu {len(miss):3d}  {"".join(miss)}')

print()
missing_game = [c for c in viet if ord(c) not in all_game]
missing_win = [c for c in viet if ord(c) not in all_win]

print(f'THIEU TRONG TAT CA FONT GAME: {len(missing_game)}')
for c in missing_game:
    print(f'   {c}  U+{ord(c):04X}  {unicodedata.name(c, "?")}  ({used[c]} lan)')

print()
print(f'THIEU TRONG TAT CA FONT WINDOWS: {len(missing_win)}')
for c in missing_win:
    print(f'   {c}  U+{ord(c):04X}  {unicodedata.name(c, "?")}')
