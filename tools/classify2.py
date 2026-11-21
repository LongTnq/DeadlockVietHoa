"""Phan loai 212 entry con lai thanh 3 nhom va xuat danh sach can dich that su."""
import io
import json
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'

items = json.load(io.open(ROOT + r'\tools\_still2.json', encoding='utf-8'))

# 1) PLACEHOLDER thuan: chi co token + dau cham -> giu nguyen
PLACEHOLDER_RE = re.compile(r'^[\s\W\d]*$')

# 2) TEN RIENG giu nguyen (tieng Anh)
KEEP_EN = {
    'Guardian', 'Guardians', 'Walker', 'Walkers', 'Patron', 'Patrons',
    'Sapphire Guardian', 'Amber Walker', 'Sapphire Walker',
    'The Archmother', 'THE HIDDEN KING', 'THE ARCHMOTHER',
    'Deadlock', 'Valve', 'AMBER', 'SAPPHIRE',
    'DPS', 'MVP', 'Laser', 'Patron Laser', 'Boss Laser',
    'Tincan Gun', 'Mystic Reverb', 'Rejuvenator',
    'Singapore', 'Chile', 'Argentina', 'Peru',
    'OK', 'AP', 'K', 'D', 'A', 'VS', 'Email', 'Email:', 'Loading...',
    'Menu', 'DPS',
}

# 3) Con lai -> CAN DICH
to_translate = []
keep = []

for e in items:
    en = e['en'].strip()

    if PLACEHOLDER_RE.match(en):
        keep.append(e)
    elif en in KEEP_EN:
        keep.append(e)
    else:
        to_translate.append(e)

print(f'Giu nguyen      : {len(keep)}')
print(f'CAN DICH        : {len(to_translate)}')
print()

for e in sorted(to_translate, key=lambda x: x['line']):
    print(f'[d{e["line"]}] {e["key"]}  ::  {e["en"]}')

with io.open(ROOT + r'\tools\_to_translate2.json', 'w', encoding='utf-8') as f:
    json.dump(to_translate, f, ensure_ascii=False, indent=1)
