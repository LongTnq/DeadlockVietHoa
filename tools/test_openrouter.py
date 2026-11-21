import json
import os
import time
import urllib.request

KEY = os.environ['DEADLOCKVN_API_KEY']
ENDPOINT = 'https://openrouter.ai/api'

# 1. Kiem tra /models
req = urllib.request.Request(
    f'{ENDPOINT}/v1/models',
    headers={'Authorization': f'Bearer {KEY}'})
try:
    with urllib.request.urlopen(req, timeout=30) as r:
        data = json.loads(r.read().decode())
    print(f'OK - {len(data.get("data", []))} models available')
except Exception as e:
    print(f'FAIL: {e}')
    raise SystemExit(1)

# 2. Thu model rat nhanh
model = os.environ.get('DLVN_MODEL', 'google/gemini-2.0-flash-001')

body = {
    'model': model,
    'messages': [
        {'role': 'system', 'content':
            'Ban la chuyen gia dich thuat tieng Viet cho tro choi Deadlock. '
            'Chi dich sang TIENG VIET. Giu nguyen placeholder {i:xxx} va '
            'the HTML <span>, <br>. Tra ve DUY NHAT mot JSON object.'},
        {'role': 'user', 'content':
            'Dich cac chuoi sau sang tieng Viet.\n\n'
            '[0] Spectate Game\n'
            '[1] Destroy the core to win.\n'
            '[2] Last Hits: {i:lastHits}\n\n'
            'JSON:'},
    ],
    'temperature': 0.2,
    'max_tokens': 1024,
    'response_format': {'type': 'json_object'},
}

req = urllib.request.Request(
    f'{ENDPOINT}/v1/chat/completions',
    data=json.dumps(body).encode('utf-8'),
    headers={
        'Authorization': f'Bearer {KEY}',
        'Content-Type': 'application/json',
    })

t0 = time.time()
with urllib.request.urlopen(req, timeout=120) as r:
    out = json.loads(r.read().decode('utf-8'))
elapsed = time.time() - t0

print(f'\nmodel: {model}   {elapsed:.2f}s')
print(json.dumps(out['choices'][0]['message']['content'], ensure_ascii=False))
print(f"tokens: {out.get('usage')}")
