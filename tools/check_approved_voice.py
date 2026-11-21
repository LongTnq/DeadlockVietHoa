"""Kiem tra Approved co bang VO/English khong (nghi dam hong DB)."""
import json
from collections import Counter

p = r'workspace\translations\vietnamese.json'
d = json.load(open(p, encoding='utf-8'))
es = d['entries'] if isinstance(d, dict) and 'entries' in d else d

# Nhom entry Approved
cat_status = Counter()
voice_approved = []

for e in es:
    c = e.get('category')
    s = e.get('status')
    cat_status[(c, s)] += 1
    if s == 'Approved' and c == 'Voice':
        voice_approved.append(e)

print('Phan bo (category, status):')
for (c, s), n in sorted(cat_status.items()):
    print(f'  {str(c):10s} {str(s):10s} {n:>7,}')

print()
print(f'Approved trong Voice: {len(voice_approved):,}')

if voice_approved:
    print()
    print('Vi du 5 entry Voice bi danh Approved:')
    for e in voice_approved[:5]:
        print(f'  {e.get("key")}')
        print(f'     orig: {e.get("originalText","")[:80]!r}')
        print(f'     tran: {e.get("translatedText","")[:80]!r}')
