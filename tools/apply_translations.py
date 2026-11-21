"""Áp dụng bản dịch mới vào file staging citadel_main.

Chỉ ghi đè các entry có trong _translated_main.json.
Bảo toàn: placeholder, thẻ HTML, BOM, thụt lề.
"""
import io
import json
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'
TRANSLATED = ROOT + r'\tools\_translated_main.json'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?://.*)?)$')

# Token Valve: {..}, {a, plural, one{} other{}}
TOKEN_RE = re.compile(r'\{[^{}]*\}')


def unescape(v):
    v = v.replace('\\"', '"').replace('\\n', '\n').replace('\\t', '\t')
    return v.replace('\\\\', '\\')


def escape(v):
    return (v.replace('\\', '\\\\').replace('"', '\\"')
             .replace('\n', '\\n').replace('\t', '\\t'))


def tokens_of(text):
    """Trả về tập token tìm thấy (dùng để so khớp placeholder)."""
    return set(TOKEN_RE.findall(text))


def main():
    translated = json.load(io.open(TRANSLATED, encoding='utf-8'))

    with io.open(STAGING, encoding='utf-8') as f:
        lines = f.readlines()

    applied = 0
    skipped = 0
    token_mismatch = []
    empty = []

    for i, raw in enumerate(lines):
        m = LINE_RE.match(raw.rstrip('\n'))

        if not m:
            continue

        key = re.match(r'\s*"([^"]+)"', m.group(1)).group(1)

        if key not in translated:
            continue

        new = translated[key]
        old = unescape(m.group(2))

        if not new or not new.strip():
            empty.append(key)
            continue

        # Kiem tra placeholder khong bi mat
        old_tokens = tokens_of(old)
        new_tokens = tokens_of(new)

        if old_tokens != new_tokens:
            token_mismatch.append((key, old, new))
            continue

        if new != old:
            lines[i] = m.group(1) + '"' + escape(new) + m.group(3) + '\n'
            applied += 1

    with io.open(STAGING, 'w', encoding='utf-8', newline='') as f:
        f.writelines(lines)

    print(f'Ap dung      : {applied}')
    print(f'Bo qua (placeholder lech): {len(token_mismatch)}')
    print(f'Bo qua (rong): {len(empty)}')

    if token_mismatch:
        print('\n--- PLACEHOLDER LECH ---')
        out = io.open(ROOT + r'\tools\_token_mismatch.txt', 'w', encoding='utf-8')

        for key, old, new in token_mismatch:
            print(f'  {key}')
            print(f'    CU : {old[:150]}')
            print(f'    MOI: {new[:150]}')
            out.write(f'{key}\n  CU : {old}\n  MOI: {new}\n\n')

        out.close()

    if empty:
        print('\n--- RONG ---')

        for k in empty[:20]:
            print(f'  {k}')


if __name__ == '__main__':
    main()
