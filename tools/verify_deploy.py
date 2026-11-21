import io
import os

GAME = r'E:\Steam\steamapps\common\Deadlock\game'
CITADEL = os.path.join(GAME, 'citadel')

checks = [
    (os.path.join(CITADEL, 'resource', 'localization', 'citadel_main',
                  'citadel_main_vietnamese.txt'), 'Phá hủy lõi'),
    (os.path.join(CITADEL, 'resource', 'localization', 'citadel_heroes',
                  'citadel_heroes_vietnamese.txt'), 'Trợ Thủ'),
    (os.path.join(CITADEL, 'resource', 'localization', 'citadel_main',
                  'citadel_main_vietnamese.txt'), 'Xem Trận Đấu'),
]

for path, needle in checks:
    if not os.path.exists(path):
        print(f'THIEU: {path}')
        continue
    t = io.open(path, encoding='utf-8', errors='replace').read()
    idx = t.find(needle)
    size = os.path.getsize(path)
    print(f'{os.path.basename(path)}  ({size:,} bytes)')
    if idx >= 0:
        print(f'   FOUND "{needle}" -> ...{t[max(0,idx-80):idx+80]}...')
    else:
        print(f'   MISSING "{needle}"')
    print()

# Dem so dong da dich trong file main
p = os.path.join(CITADEL, 'resource', 'localization', 'citadel_main',
                 'citadel_main_vietnamese.txt')
lines = io.open(p, encoding='utf-8', errors='replace').read().splitlines()
vi = [l for l in lines if l.startswith('"') and any(
    '\u00e0' <= c <= '\u1ef9' for c in l)]
print(f'citadel_main_vietnamese.txt: {len(lines)} dong, {len(vi)} dong co dau tieng Viet')

# Kiem tra fonts.conf
fc = os.path.join(CITADEL, 'panorama', 'fonts', 'fonts.conf')
t = io.open(fc, encoding='utf-8', errors='replace').read()
print(f'fonts.conf co Arial: {"Arial" in t}, Segoe UI: {"Segoe UI" in t}')

# gameinfo.gi
gi = os.path.join(CITADEL, 'gameinfo.gi')
t = io.open(gi, encoding='utf-8', errors='replace').read()
print(f'gameinfo.gi co "vietnamese": {"vietnamese" in t}')
