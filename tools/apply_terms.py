"""Buoc 1+2: sua entry hong + ap dung thuat ngu dung cho file staging.

Thay the duoc bao ve bang regex nen "HeroLabs" khong bi doi thanh "TuongLabs".
"""
import io
import json
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?://.*)?)$')


def unescape(v):
    v = v.replace('\\"', '"').replace('\\n', '\n').replace('\\t', '\t')
    return v.replace('\\\\', '\\')


def escape(v):
    return (v.replace('\\', '\\\\').replace('"', '\\"')
             .replace('\n', '\\n').replace('\t', '\\t'))


# ---------------------------------------------------------------- BƯỚC 1
# Sua entry hong do tim-thay "Rank" -> "Xep hang" toan cuc.
CORRUPT_FIXES = [
    ('darkoXếp hạnge', 'darkorange'),
    ('TXếp hạng trí', 'Trang trí'),
    ('TXếp hạng', 'Trang'),
    ('hoá đổi', 'hoán đổi'),
    ('đã vật phẩm thêm', 'đã trang bị thêm'),
]

# ---------------------------------------------------------------- BƯỚC 2
# (mau sai, mau dung). Regex, nen \b la ranh gioi tu.
# Thu tu: cum dai truoc, cum ngan sau.
TERM_FIXES = [
    # --- Ân huệ (Boons) ---
    (r'Quà phước lành', 'Thưởng Ân huệ'),
    (r'phước lành', 'ân huệ'),
    (r'Phước lành', 'Ân huệ'),
    (r'thuận lợi nhận được', 'âm huệ đã nhận được'),
    (r'Thuận lợi nhận được', 'Ân huệ đã nhận được'),
    (r'thuận lợi', 'âm huệ'),
    (r'Thuận lợi', 'Ân huệ'),

    # --- Phép (Spirit) ---
    (r'Lực lượng Nội lực', 'Sát thương Phép'),
    (r'Đầu tư Nội lực', 'Đầu tư Phép'),
    (r'đầu tư Nội lực', 'đầu tư Phép'),
    (r'cây Nội lực', 'cây Phép'),
    (r'tăng cường Nội lực', 'tăng cường Phép'),
    (r'Tăng cường Nội lực', 'Tăng cường Phép'),
    (r'\bNội lực\b', 'Phép'),
    (r'\bnội lực\b', 'phép'),

    # --- Máu (Health) ---
    (r'Khôi phục sức khỏe', 'Hồi máu'),
    (r'Sức khỏe cơ sở', 'Máu gốc'),
    (r'sức khỏe cơ sở', 'máu gốc'),
    (r'\bSức khỏe\b', 'Máu'),
    (r'\bsức khỏe\b', 'máu'),

    # --- Tha hoá (Corrupt) ---
    (r'\blàm ô uế\b', 'tha hoá'),
    (r'\bLàm ô uế\b', 'Tha hoá'),
    (r'\bô uế\b', 'tha hoá'),
    (r'\bÔ uế\b', 'Tha hoá'),

    # --- Thăng hoa (Enhanced) ---
    (r'\bđược tăng cường\b', 'được thăng hoa'),
    (r'\bđã tăng cường\b', 'đã thăng hoa'),
    (r'\btăng cường chỉ số\b', 'thăng hoa chỉ số'),
    (r'\bTăng cường\b', 'Thăng hoa'),
    (r'\btăng cường\b', 'thăng hoa'),

    # --- Tướng (Hero) ---
    (r'\banh hùng\b', 'tướng'),
    (r'\bAnh hùng\b', 'Tướng'),
    (r'\bHero Builds\b', 'Lối lên vật phẩm'),
    (r'\bHero Build\b', 'Lối lên vật phẩm'),
    (r'\bhero build\b', 'lối lên vật phẩm'),
    (r'\bHeroes\b', 'Tướng'),
    (r'\bheroes\b', 'tướng'),
    (r'\bHero\b', 'Tướng'),
    (r'\bhero\b', 'tướng'),
]

TERM_REGEX = [(re.compile(p), r) for p, r in TERM_FIXES]


def apply_terms(text):
    for rx, right in TERM_REGEX:
        text = rx.sub(right, text)

    return text


def main():
    with io.open(STAGING, encoding='utf-8-sig') as f:
        lines = f.readlines()

    user_fixed = {
        e['key'] for e in json.load(
            io.open(ROOT + r'\tools\_user_edits.json', encoding='utf-8'))}

    fixed_corrupt = 0
    fixed_terms = 0
    changed_lines = []

    for i, raw in enumerate(lines):
        m = LINE_RE.match(raw.rstrip('\n'))

        if not m:
            continue

        key = re.match(r'\s*"([^"]+)"', m.group(1)).group(1)
        val = unescape(m.group(2))
        original = val

        # BUOC 1: sua entry hong (ap dung cho moi entry)
        for wrong, right in CORRUPT_FIXES:
            if wrong in val:
                val = val.replace(wrong, right)

        # BUOC 2: thuat ngu — chi ap dung cho entry user CHUA sua
        if key not in user_fixed:
            val = apply_terms(val)

        if val != original:
            if any(w in original for w, _ in CORRUPT_FIXES):
                fixed_corrupt += 1
            else:
                fixed_terms += 1

            changed_lines.append((i + 1, key, original, val))

            # group(1) = phan truoc, group(2) = noi dung, group(3) = dau
            # nhay dong + comment. Cau truc la: group(1) + '"' + value + group(3)
            lines[i] = m.group(1) + '"' + escape(val) + m.group(3) + '\n'

    with io.open(STAGING, 'w', encoding='utf-8', newline='') as f:
        f.writelines(lines)

    print(f'Sua entry hong      : {fixed_corrupt}')
    print(f'Sua thuat ngu sai   : {fixed_terms}')
    print(f'Tong dong thay doi  : {len(changed_lines)}')

    out = io.open(ROOT + r'\tools\_applied_changes.txt', 'w', encoding='utf-8')

    for line, key, old, new in changed_lines:
        out.write(f'[d{line}] {key}\n  CU : {old}\n  MOI: {new}\n\n')

    out.close()


if __name__ == '__main__':
    main()
