"""Khoi phuc entry bi danh Approved nham (translatedText == originalText).

Lenh import-from-staging lan dau chay sai da danh Approved 26.661 entry
Voice (chua dich) vi gia tri trong file staging trung nguyen voi ban goc.
Dieu kien nhan dang: translatedText == originalText -> thu hoi ve Pending.
"""
import json

p = r'workspace\translations\vietnamese.json'
with open(p, encoding='utf-8') as f:
    d = json.load(f)

es = d['entries'] if isinstance(d, dict) and 'entries' in d else d

fixed = 0
kept = 0
examples = []

for e in es:
    if e.get('status') != 'Approved':
        continue

    orig = e.get('originalText', '')
    tran = e.get('translatedText', '')

    if tran == orig:
        e['status'] = 'Pending'
        e['translatedText'] = ''
        fixed += 1
        if len(examples) < 5:
            examples.append(e.get('key'))
    else:
        kept += 1

with open(p, 'w', encoding='utf-8') as f:
    json.dump(d, f, ensure_ascii=False, indent=2)

print(f'Da thu hoi (Approved -> Pending): {fixed:,}')
print(f'Giu nguyen Approved that su:        {kept:,}')
if examples:
    print('Vi du bi thu hoi:')
    for k in examples:
        print(f'  {k}')
