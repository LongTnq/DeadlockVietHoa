"""Xem cac thuat ngu trong vung user da sua (dong 1-909) de chuan hoa."""
import io
import re

p = r'workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?://.*)?)$')

lines = io.open(p, encoding='utf-8').readlines()

TERMS = ['Vua', 'ẩn', 'Archmother', 'Thần mẹ', 'Mẹ', 'Amber', 'Sapphire',
         'Broadway', 'York', 'Greenwich', 'Guardian', 'Walker', 'Patron',
         'Đội', 'đội', 'Trụ', 'Lính', 'Quân']

for i, raw in enumerate(lines[:909], 1):
    m = LINE_RE.match(raw.rstrip('\n'))

    if not m:
        continue

    key = re.match(r'\s*"([^"]+)"', m.group(1)).group(1)
    val = m.group(2)

    if any(t in val for t in TERMS):
        print(f'[d{i}] {key}\n      {val[:170]}')
