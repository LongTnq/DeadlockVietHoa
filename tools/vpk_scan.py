"""Quet pak01_dir.vpk de tim file localization."""
import re
import sys

path = r'E:\Steam\steamapps\common\Deadlock\game\citadel\pak01_dir.vpk'
data = open(path, 'rb').read()

# Tim cac chuoi ASCII co the la duong dan / ten file
strings = re.findall(rb'[\x20-\x7e]{5,200}', data)

targets = [b'localization', b'vietnam', b'_english.txt', b'citadel_main', b'fonts.conf']
for t in targets:
    hits = [s for s in strings if t.lower() in s.lower()]
    print(f'=== {t.decode()}: {len(hits)} hits')
    for h in sorted(set(hits))[:15]:
        print('   ', h.decode('utf-8', 'replace'))
    print()
