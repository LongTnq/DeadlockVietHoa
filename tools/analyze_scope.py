"""Xac dinh pham vi thay doi theo dong, va tim cac entry con sot tieng Anh."""
import io
import json
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'
DB = ROOT + r'\workspace\translations\vietnamese.json'
SRC = ROOT + r'\workspace\source\original\citadel_main\citadel_main_english.txt'

LINE_RE = re.compile(r'^\s*"([^"]+)"\s+"((?:[^"\\]|\\.)*)"\s*(?://.*)?$')


def unescape(v):
    v = v.replace('\\"', '"').replace('\\n', '\n').replace('\\t', '\t')
    return v.replace('\\\\', '\\')


def parse_kv_with_lines(path):
    out = []
    with io.open(path, encoding='utf-8-sig') as f:
        for i, raw in enumerate(f, 1):
            m = LINE_RE.match(raw.rstrip('\n'))
            if m:
                out.append((i, m.group(1), unescape(m.group(2))))
    return out


def main():
    rows = parse_kv_with_lines(STAGING)
    dbrows = parse_kv_with_lines(SRC)
    src = {k: v for _i, k, v in dbrows}

    with io.open(DB, encoding='utf-8') as f:
        doc = json.load(f)

    entries = doc['entries'] if isinstance(doc, dict) and 'entries' in doc else doc

    db = {}
    for e in entries:
        if e.get('sourceFile', '').endswith('citadel_main/citadel_main_english.txt'):
            db[e.get('key', '')] = e.get('translatedText', '')

    changed = []
    untranslated = []

    for line_no, key, val in rows:
        old = db.get(key)

        if old is None:
            continue

        if old != val:
            changed.append((line_no, key, old, val))

        # Entry ma DB co bang '' hoac bang chinh tieng Anh
        if not old.strip() or old.strip() == src.get(key, '').strip():
            untranslated.append((line_no, key, src.get(key, ''), val))

    print(f'Tong dong co key : {len(rows):,}')
    print(f'BI SUA DOI      : {len(changed):,}')
    print(f'CON SOT ANH     : {len(untranslated):,}')

    max_line = max((c[0] for c in changed), default=0)
    print(f'Dong sua doi lon nhat: {max_line}')

    print()
    print('--- Thay doi theo phan (dinh ky tuong doi 500 dong) ---')
    buckets = {}
    for line_no, key, old, new in changed:
        b = line_no // 500 * 500
        buckets[b] = buckets.get(b, 0) + 1

    for b in sorted(buckets):
        print(f'  dong {b:>5}-{b+499:<5}: {buckets[b]:>4}')

    print()
    print('--- Entry con sot tieng Anh theo phan ---')
    buckets = {}
    for line_no, key, en, cur in untranslated:
        b = line_no // 500 * 500
        buckets[b] = buckets.get(b, 0) + 1

    for b in sorted(buckets):
        print(f'  dong {b:>5}-{b+499:<5}: {buckets[b]:>4}')

    with io.open(ROOT + r'\tools\_untranslated.json', 'w', encoding='utf-8') as f:
        json.dump(
            [{'line': l, 'key': k, 'en': e, 'cur': c} for l, k, e, c in untranslated],
            f, ensure_ascii=False, indent=1)

    with io.open(ROOT + r'\tools\_user_edits.json', 'w', encoding='utf-8') as f:
        json.dump(
            [{'line': l, 'key': k, 'en': src.get(k, ''), 'old': o, 'new': n}
             for l, k, o, n in changed],
            f, ensure_ascii=False, indent=1)

    print('\nDa luu -> tools/_untranslated.json, tools/_user_edits.json')


if __name__ == '__main__':
    main()
