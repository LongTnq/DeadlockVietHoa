"""Debug: xem raw line co match regex khong."""
import io
import re

p = r'workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?://.*)?)$')

lines = io.open(p, encoding='utf-8').readlines()

n = 0
for l in lines:
    if 'HeroLabsMMNotOpen' in l or 'Duplicate_Heroes_Off' in l:
        print(repr(l[:160]))
        print('  MATCH:', bool(LINE_RE.match(l.rstrip('\n'))))
        n += 1

    if n >= 4:
        break
