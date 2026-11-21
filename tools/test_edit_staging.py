"""Sua tay mot entry trong file staging de test import-from-staging."""
import io
import sys

path = sys.argv[1] if len(sys.argv) > 1 else (
    r'workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt')

old = sys.argv[2] if len(sys.argv) > 2 else '"Xem Tr\u1eadn \u0110\u1ea5u"'
new = sys.argv[3] if len(sys.argv) > 3 else '"Xem Tr\u1eadn \u0110\u1ea5u (TEST)"'

with io.open(path, encoding='utf-8') as f:
    text = f.read()

if old not in text:
    print(f'KHONG tim thay "{old}" trong {path}')
    sys.exit(1)

text = text.replace(old, new, 1)

with io.open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(text)

print(f'Da sua: {old}  ->  {new}')
print(f'File: {path}')
