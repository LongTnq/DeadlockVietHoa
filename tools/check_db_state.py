"""Kiem tra phan bo TranslatedText va Status trong DB."""
import json
from collections import Counter

p = r'workspace\translations\vietnamese.json'
d = json.load(open(p, encoding='utf-8'))
es = d['entries'] if isinstance(d, dict) and 'entries' in d else d

non_empty = 0
empty = 0
status = Counter()
for e in es:
    t = e.get('translatedText', '')
    if t:
        non_empty += 1
    else:
        empty += 1
    status[e.get('status')] += 1

print(f'tong: {len(es)}   TranslatedText khac rong: {non_empty}   rong: {empty}')
print('status:', dict(status))
print()
# Xem vai du entry co TranslatedText khac rong nhung status Pending
n = 0
for e in es:
    if e.get('translatedText') and e.get('status') == 'Pending':
        n += 1
        if n <= 3:
            print(f'  [{e.get("category")}] {e.get("key")}')
            print(f'     orig: {e.get("originalText","")[:70]!r}')
            print(f'     tran: {e.get("translatedText","")[:70]!r}')
print(f'\ntong so Pending nhung co TranslatedText: {n}')
