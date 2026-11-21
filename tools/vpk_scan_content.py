"""Quet VPK chunk de xac nhan co noi dung tieng Viet that."""
import re
import sys

path = sys.argv[1]
data = open(path, 'rb').read()
print(f'file: {path}')
print(f'size: {len(data):,} bytes')

# Tim cac chuoi tieng Viet theo byte UTF-8
text = data.decode('utf-8', errors='ignore')
viet = re.findall(r'[\u00c0-\u1ef9]+', text)
print(f'ky tu co dau: {len(viet):,}')

uniq = {}
for w in viet:
    uniq[w] = uniq.get(w, 0) + 1

top = sorted(uniq.items(), key=lambda kv: -kv[1])[:25]
print('\ntu xuat nhieu nhat:')
for w, c in top:
    print(f'  {w:30s} {c}')

# Kiem tra cac cum tieng Viet dac trung
print('\nkiem tra cum dac trung:')
for probe in ['Huỷ', 'Đóng', 'Không hiển thị lại', 'Đang tải']:
    print(f'  {probe:25s} -> {"CO" if probe in text else "KHONG"}')
