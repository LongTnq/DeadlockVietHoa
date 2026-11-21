"""Liệt kê vị trí các file bản dịch và file đã deploy vào game."""
import io
import os

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
GAME = r'E:\Steam\steamapps\common\Deadlock\game'

TRANSLATION_FILES = [
    (r'workspace\translations\vietnamese.json', 'GOP - tat ca entry'),
    (r'workspace\translations\ui\ui.json', 'UI'),
    (r'workspace\translations\abilities\abilities.json', 'Abilities'),
    (r'workspace\translations\heroes\heroes.json', 'Heroes'),
    (r'workspace\translations\items\items.json', 'Items'),
    (r'workspace\translations\misc\misc.json', 'Misc'),
]

STAGING_FILES = [
    r'workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt',
    r'workspace\staging\localization\citadel_heroes\citadel_heroes_vietnamese.txt',
    r'workspace\staging\localization\citadel_attributes\citadel_attributes_vietnamese.txt',
    r'workspace\staging\localization\citadel_mods\citadel_mods_vietnamese.txt',
    r'workspace\staging\localization\citadel_gc\citadel_gc_vietnamese.txt',
    r'workspace\staging\localization\citadel_gc_hero_names\citadel_gc_hero_names_vietnamese.txt',
    r'workspace\staging\localization\citadel_gc_mod_names\citadel_gc_mod_names_vietnamese.txt',
    r'workspace\staging\localization\citadel_patch_notes\citadel_patch_notes_vietnamese.txt',
    r'workspace\staging\localization\citadel_seasonal\citadel_seasonal_vietnamese.txt',
    r'workspace\staging\localization\citadel_generated_vo\citadel_generated_vo_vietnamese.txt',
    r'workspace\staging\localization\citadel_vdata\accolades\accolades_vietnamese.txt',
]

GAME_FILES = [
    r'citadel\resource\localization\citadel_main\citadel_main_vietnamese.txt',
    r'citadel\resource\localization\citadel_heroes\citadel_heroes_vietnamese.txt',
    r'citadel\resource\localization\citadel_attributes\citadel_attributes_vietnamese.txt',
    r'citadel\resource\localization\citadel_mods\citadel_mods_vietnamese.txt',
    r'citadel\resource\localization\citadel_gc\citadel_gc_vietnamese.txt',
    r'citadel\resource\localization\citadel_gc_hero_names\citadel_gc_hero_names_vietnamese.txt',
    r'citadel\resource\localization\citadel_gc_mod_names\citadel_gc_mod_names_vietnamese.txt',
    r'citadel\resource\localization\citadel_patch_notes\citadel_patch_notes_vietnamese.txt',
    r'citadel\resource\localization\citadel_seasonal\citadel_seasonal_vietnamese.txt',
    r'citadel\resource\localization\citadel_generated_vo\citadel_generated_vo_vietnamese.txt',
    r'citadel\resource\localization\citadel_vdata\accolades\accolades_vietnamese.txt',
]


def count_entries(path):
    """Dem so entry da duoc dich trong file JSON tach nhom."""
    try:
        import json
        with io.open(path, encoding='utf-8') as f:
            doc = json.load(f)
    except Exception:
        return None

    entries = doc['entries'] if isinstance(doc, dict) and 'entries' in doc else doc

    if not isinstance(entries, list):
        return None

    return sum(1 for e in entries if e.get('translatedText'))


print('=' * 78)
print('1. BAN DICH GOC — sua o day (nguon su cho moi thay doi)')
print('=' * 78)

for rel, label in TRANSLATION_FILES:
    p = os.path.join(ROOT, rel)
    if not os.path.exists(p):
        print(f'  [KHONG CO] {rel}')
        continue

    size = os.path.getsize(p)
    n = count_entries(p)

    extra = f'{n:,} entry da dich' if n is not None else ''

    print(f'  {label:8s} {rel}')
    print(f'           {size:>12,} bytes   {extra}')
    print()

print('=' * 78)
print('2. FILE DA EXPORT — dung truc tiep trong game (sinh tu, khong sua tay)')
print('=' * 78)

for rel in STAGING_FILES:
    p = os.path.join(ROOT, rel)
    if os.path.exists(p):
        print(f'  {os.path.getsize(p):>12,}  {rel}')

print()
print('=' * 78)
print('3. DA DEPLOY VAO GAME')
print('=' * 78)

print('  A. File loose (Steam co the ghi de khi verify):')
for rel in GAME_FILES:
    p = os.path.join(GAME, rel)
    if os.path.exists(p):
        print(f'    {os.path.getsize(p):>12,}  game\\{rel}')

print()
print('  B. VPK (kieu chuan cua Valve, Steam khong quan ly):')
lang_dir = os.path.join(GAME, 'citadel_vietnamese')
for name in ('gameinfo.gi', 'pak01_dir.vpk', 'pak01_000.vpk'):
    p = os.path.join(lang_dir, name)
    if os.path.exists(p):
        print(f'    {os.path.getsize(p):>12,}  game\\citadel_vietnamese\\{name}')

print()
print('  C. File cau hinh da patch:')
for rel in (r'citadel\gameinfo.gi',
            r'citadel\panorama\fonts\fonts.conf'):
    p = os.path.join(GAME, rel)
    if os.path.exists(p):
        print(f'    {os.path.getsize(p):>12,}  game\\{rel}')
