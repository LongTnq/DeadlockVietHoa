import json
import os
import urllib.request

KEY = os.environ['DEADLOCKVN_API_KEY']
ENDPOINT = 'https://openrouter.ai/api/v1'
MODEL = os.environ.get('DLVN_MODEL', 'google/gemini-2.5-flash')

db_path = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization\workspace\translations\vietnamese.json'
with open(db_path, 'r', encoding='utf-8') as f:
    doc = json.load(f)

entries = doc['entries'] if isinstance(doc, dict) and 'entries' in doc else doc

target = next(e for e in entries if e.get('key', '').endswith('hero_atlas_lore'))
orig = target['originalText']
print(f'do dai goc: {len(orig)} ky tu')

body = {
    'model': MODEL,
    'messages': [
        {'role': 'system', 'content':
            'Ban la chuyen gia dich thuat tieng Viet cho tro choi Deadlock. '
            'CHI dich sang TIENG VIET. Giu nguyen placeholder {i:xxx} va '
            'the HTML <span>, <br>. Tra ve DUY NHAT mot JSON object.'},
        {'role': 'user', 'content':
            f'Dich chuoi sau sang tieng Viet.\n\n[0] {orig}\n\nJSON:'},
    ],
    'temperature': 0.2,
    'max_tokens': 2048,
    'response_format': {'type': 'json_object'},
}

req = urllib.request.Request(
    f'{ENDPOINT}/chat/completions',
    data=json.dumps(body).encode('utf-8'),
    headers={'Authorization': f'Bearer {KEY}', 'Content-Type': 'application/json'})

with urllib.request.urlopen(req, timeout=180) as r:
    out = json.loads(r.read().decode('utf-8'))

content = out['choices'][0]['message']['content']
print(f'do dai ket qua: {len(content)}')
print(f'usage: {out.get("usage")}')
print()
print(content[:1500])
print('...')
print(content[-300:])
