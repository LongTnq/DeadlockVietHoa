"""Chuan hoa thuat ngu trong tat ca file staging (Héro -> Tuong, Tuong -> Tuong
co dau, v.v.). Bao dam khong hong placeholder/HTML."""
import io
import os
import re

ST = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\staging\localization'

# (regex, replacement) - ap dung theo thu tu
RULES = [
    # Tieng Phap / sai dau
    (re.compile(r'\bHéros\b'), 'Tướng'),
    (re.compile(r'\bHéro\b'), 'Tướng'),
    (re.compile(r'\bhéros\b'), 'tướng'),
    (re.compile(r'\bHéros?\b'), 'Tướng'),
    # Thieu dau
    (re.compile(r'\bTuong\b'), 'Tướng'),
    (re.compile(r'\btuong\b'), 'tướng'),
    (re.compile(r'\bTuongs\b'), 'Tướng'),
    # Tu viet -> viet co dau
    (re.compile(r'\bXep hang\b'), 'Xếp hạng'),
    (re.compile(r'\bXep hang\b'), 'Xếp hạng'),
    (re.compile(r'\bAn hue\b'), 'Ân huệ'),
    (re.compile(r'\ban hue\b'), 'ân huệ'),
    (re.compile(r'\bMau goc\b'), 'Máu gốc'),
    (re.compile(r'\bLinh hon\b'), 'Linh hồn'),
    (re.compile(r'\bLoi len vat pham\b'), 'Lối lên vật phẩm'),
    (re.compile(r'\bTha hoa\b'), 'Tha hoá'),
    (re.compile(r'\bThang hoa\b'), 'Thăng hoa'),
    (re.compile(r'\bMua nhanh\b'), 'Mua nhanh'),
    (re.compile(r'\bHoi chieu\b'), 'Hồi chiêu'),
    (re.compile(r'\bKy nang\b'), 'Kỹ năng'),
    (re.compile(r'\bNuoc loi\b'), 'Nước lợi'),
    (re.compile(r'\bHuy dong\b'), 'Huy động'),
    (re.compile(r'\bKhoi tao\b'), 'Khởi tạo'),
    (re.compile(r'\bTang toc\b'), 'Tăng tốc'),
    (re.compile(r'\bDinh vi\b'), 'Dịch vị'),
    (re.compile(r'\bAm tho\b'), 'Ám tháo'),
    (re.compile(r'\bTe imm\b'), 'Tê imm'),
    (re.compile(r'\bVat pham\b'), 'Vật phẩm'),
    (re.compile(r'\bSat thuong\b'), 'Sát thương'),
    (re.compile(r'\bSoc sat thuong\b'), 'Sốc sát thương'),
    (re.compile(r'\bBau dia\b'), 'Báu địa'),
    (re.compile(r'\bTrung tam\b'), 'Trung tâm'),
]

total = 0

for d in sorted(os.listdir(ST)):
    p = os.path.join(ST, d)

    if not os.path.isdir(p):
        continue

    for fn in sorted(os.listdir(p)):
        if not fn.endswith('.txt'):
            continue

        f = os.path.join(p, fn)
        raw8 = io.open(f, encoding='utf-8', newline='').read()
        raw = raw8.lstrip('\ufeff')
        out = raw
        n = 0

        for rx, rep in RULES:
            out, k = rx.subn(rep, out)
            n += k

        if n:
            io.open(f, 'w', encoding='utf-8', newline='').write(out)
            print(f'{d}/{fn}: {n}')
            total += n

print(f'TONG: {total}')
