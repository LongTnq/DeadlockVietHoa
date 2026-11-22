#!/usr/bin/env python
"""Patch `gameinfo.gi` cua Deadlock de VPK trong `citadel/addons` duoc game load.

Van hoa/Valve ship `gameinfo.gi` trong `game/citadel/`. Steam khoi phuc file
nay khi verify integrity -> moi dong them bi mat. Mod dinh nghia dua VPK vao
`game/citadel/addons/`, nhung thu muc do **khong duoc mount** neu gameinfo.gi
khong khai bao `Game citadel/addons` trong `SearchPaths`.

Script nay:
  - them `Game citadel/addons` vao `SearchPaths` (truoc `Game citadel`)
  - them `Mod citadel` va `Write citadel` neu thieu
  - giu nguyen `SupportedLanguages` (khong them `vietnamese`)
  - backup file goc truoc khi sua

Chay lai an toan: neu da co dong can them thi bo qua.
"""
import os
import re
import shutil
import sys

GAME = r'E:\Steam\steamapps\common\Deadlock\game'
GI = os.path.join(GAME, 'citadel', 'gameinfo.gi')

ADDONS_LINE = '\t\t\tGame\t\t\t\tcitadel/addons\n'
MOD_LINE = '\t\t\tMod\t\t\t\tcitadel\n'
WRITE_LINE = '\t\t\tWrite\t\t\t\tcitadel\n'

# `Game` / `Mod` / `Write` + tab(s) + duong dan. Khoang trang giua key va
# duong dan khong co quy tac (Valve dung 2 hoac 4 tab).
# File dung CRLF nen dung `[ \t]*\r?$` de match ca `Game citadel`.
RE_GAME_CITADEL = re.compile(r'^[ \t]*Game[ \t]+citadel[ \t]*\r?$', re.M)
RE_MOD_CITADEL = re.compile(r'^[ \t]*Mod[ \t]+citadel[ \t]*\r?$', re.M)
RE_WRITE_CITADEL = re.compile(r'^[ \t]*Write[ \t]+citadel[ \t]*\r?$', re.M)


def log(msg):
    print(msg)


def find_search_paths(text):
    """Tra ve (start, end) cua khoi `SearchPaths { ... }` trong FileSystem."""
    m = re.search(r'\bSearchPaths\s*\{', text)
    if not m:
        return None

    i = m.end() - 1
    depth = 0
    j = i
    while j < len(text):
        if text[j] == '{':
            depth += 1
        elif text[j] == '}':
            depth -= 1
            if depth == 0:
                return (i + 1, j)
        j += 1

    return None


def main():
    if not os.path.exists(GI):
        log('[LOI] Khong tim thay: %s' % GI)
        return 1

    with open(GI, 'r', encoding='utf-8', errors='replace', newline='') as f:
        text = f.read()

    original = text

    if 'citadel/addons' in text:
        log('  [OK] Game citadel/addons da co trong gameinfo.gi.')
    else:
        span = find_search_paths(text)
        if not span:
            log('[LOI] Khong tim thay khoi SearchPaths trong gameinfo.gi.')
            return 1

        start, end = span
        block = text[start:end]

        m = RE_GAME_CITADEL.search(block)
        if not m:
            log('[LOI] Khong khop duoc dong `Game citadel` trong SearchPaths.')
            return 1

        # Chen `Game citadel/addons` ngay TRUOC `Game citadel`.
        block_new = (block[:m.start()]
                     + ADDONS_LINE
                     + block[m.start():])
        text = text[:start] + block_new + text[end:]
        log('  [OK] Da them `Game citadel/addons` truoc `Game citadel`.')

    if RE_MOD_CITADEL.search(text):
        log('  [OK] `Mod citadel` da co.')
    else:
        m = RE_GAME_CITADEL.search(text)
        if m:
            text = text[:m.start()] + MOD_LINE + text[m.start():]
            log('  [OK] Da them `Mod citadel`.')

    if RE_WRITE_CITADEL.search(text):
        log('  [OK] `Write citadel` da co.')
    else:
        m = RE_MOD_CITADEL.search(text)
        if m:
            text = text[:m.start()] + WRITE_LINE + text[m.start():]
            log('  [OK] Da them `Write citadel`.')

    if text == original:
        log('')
        log('  Khong co gi thay doi.')
        return 0

    backup = GI + '.bak'
    if not os.path.exists(backup):
        shutil.copy2(GI, backup)
        log('  [OK] Backup: %s' % backup)

    with open(GI, 'w', encoding='utf-8', errors='replace', newline='') as f:
        f.write(text)

    log('')
    log('  Da ghi gameinfo.gi.')
    log('  KHONG them `vietnamese` vao SupportedLanguages (dung bien the English).')
    log('')

    return 0


if __name__ == '__main__':
    sys.exit(main())
