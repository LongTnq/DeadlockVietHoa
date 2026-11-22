#!/usr/bin/env python
"""Kiem tra fonts.conf trong VPK: font fallback cho tieng Viet."""
import re
import sys
from collections import Counter

f = sys.argv[1] if len(sys.argv) > 1 else \
    'workspace/vpkupload/out/pak01/panorama/fonts/fonts.conf'

t = open(f, encoding='utf-8', errors='replace').read()

print('File:', f)
print('Noto Sans JP:', t.count('Noto Sans JP'))

langs = re.findall(r'lang="([^"]+)"', t)
print('lang values:', Counter(langs))

for name in ('Noto Sans JP', 'NotoSans-Vietnamese'):
    if name in t:
        i = t.find(name)
        print()
        print('--- context around %r ---' % name)
        print(t[max(0, i - 400):i + 200])
        break
