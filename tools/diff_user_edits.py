"""So sanh file staging (da sua tay) voi DB de trich xuat thay doi cua nguoi dung."""
import io
import json
import re
import sys

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'
DB = ROOT + r'\workspace\translations\vietnamese.json'
SRC = ROOT + r'\workspace\source\original\citadel_main\citadel_main_english.txt'

# Regex dong:  "key"   "value"   (co the co comment)
LINE_RE = re.compile(r'^\s*"([^"]+)"\s+"((?:[^"\\]|\\.)*)"\s*(?://.*)?$')


def parse_kv(path):
    """Parse file KeyValues thanh dict key -> value (gia tri da unescape)."""
    out = {}
    order = []

    for raw in io.open(path, encoding='utf-8-sig'):
        m = LINE_RE.match(raw.rstrip('\n'))

        if not m:
            continue

        key = m.group(1)
        val = m.group(2)

        val = val.replace('\\"', '"').replace('\\n', '\n').replace('\\t', '\t')
        val = val.replace('\\\\', '\\')

        out[key] = val
        order.append(key)

    return out, order


def main():
    staged, order = parse_kv(STAGING)

    with io.open(DB, encoding='utf-8') as f:
        doc = json.load(f)

    entries = doc['entries'] if isinstance(doc, dict) and 'entries' in doc else doc

    db = {}
    for e in entries:
        sf = e.get('sourceFile', '')
        if sf.endswith('citadel_main/citadel_main_english.txt'):
            db[e.get('key', '')] = e.get('translatedText', '')

    src = {}
    if io.open(SRC, encoding='utf-8').read(1) != '':
        src, _ = parse_kv(SRC)

    changed = []
    for key in order:
        new = staged[key]
        old = db.get(key)

        if old is None:
            continue

        if old != new:
            changed.append((key, old, new, src.get(key, '')))

    print(f'Tong key trong staging : {len(staged):,}')
    print(f'Key co trong DB        : {sum(1 for k in order if k in db):,}')
    print(f'BI SUA DOI             : {len(changed):,}')
    print()

    if len(sys.argv) > 1 and sys.argv[1] == '--full':
        for key, old, new, en in changed:
            print(f'--- {key}')
            print(f'  EN : {en[:150]}')
            print(f'  CU : {old[:150]}')
            print(f'  MOI: {new[:150]}')
            print()
    else:
        # Chi in cac thay doi dau tien de xem mau
        for key, old, new, en in changed[:40]:
            print(f'--- {key}')
            print(f'  EN : {en[:110]}')
            print(f'  CU : {old[:110]}')
            print(f'  MOI: {new[:110]}')
            print()

    # Luu toan bo danh sach de phan tich tiep
    with io.open(ROOT + r'\tools\_user_edits.json', 'w', encoding='utf-8') as f:
        json.dump(
            [{'key': k, 'en': e, 'old': o, 'new': n} for k, o, n, e in changed],
            f, ensure_ascii=False, indent=1)

    print(f'\nDa luu {len(changed)} thay doi -> tools/_user_edits.json')


if __name__ == '__main__':
    main()
