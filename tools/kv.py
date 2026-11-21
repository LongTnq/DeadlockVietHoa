"""Parser KeyValues dung: doc file .txt thanh danh sach (key, value).

Quy tac:
- value co the chua dau nhay escaped: \\"
- placeholder {..} co dau nhay ben trong: {g:citadel_inline_attribute:'SpiritDamage'}
"""
import io
import re

Q = chr(34)
BS = chr(92)


def parse_kv_text(text):
    """-> list of (key, value_escaped) theo thu tu xuat hien."""
    out = []
    i = 0
    n = len(text)

    while i < n:
        # tim dau nhay mo
        while i < n and text[i] != Q:
            i += 1

        if i >= n:
            break

        i += 1
        ks = i

        while i < n and text[i] != Q:
            i += 1

        key = text[ks:i]

        if i < n:
            i += 1

        # tim dau nhay value
        while i < n and text[i] != Q:
            i += 1

        if i >= n:
            break

        i += 1
        vs = i
        buf = []

        while i < n:
            c = text[i]

            if c == BS and i + 1 < n and text[i + 1] == Q:
                buf.append(Q)
                i += 2
                continue

            if c == BS and i + 1 < n:
                buf.append(c)
                buf.append(text[i + 1])
                i += 2
                continue

            if c == Q:
                break

            buf.append(c)
            i += 1

        val = ''.join(buf)
        i += 1
        out.append((key, val))

    return out


def read_pairs(path):
    """-> list of (lineno, key, raw_value_with_escapes) cho file staging."""
    rows = []
    text = io.open(path, encoding='utf-8-sig').read()
    lines = text.split('\n')

    # Dung bang vi tri -> de biet line nao
    pos = 0

    for ln, line in enumerate(lines, 1):
        m = re.match(r'^(\s*"([^"]+)"\s+)"((?:[^"\\]|\\.)*)"',
                     line.rstrip('\r'))

        if not m:
            continue

        raw = m.group(3)
        key = m.group(2)
        rows.append((ln, key, raw))

    return rows


def unescape(v):
    return v.replace(BS + Q, Q).replace(BS + 'n', '\n').replace(
        BS + 't', '\t').replace(BS + BS, BS)


def escape(v):
    return v.replace(BS, BS + BS).replace(Q, BS + Q).replace(
        '\n', BS + 'n').replace('\t', BS + 't')
