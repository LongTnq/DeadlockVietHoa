"""Thu cac endpoint OpenRouter de tim cai dung."""
import io
import json
import os
import urllib.error
import urllib.request

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'

api_key = ''

for line in io.open(os.path.join(ROOT, '.env'), encoding='utf-8'):
    line = line.strip()
    if line.startswith('DEADLOCKVN_API_KEY='):
        api_key = line.split('=', 1)[1].strip().strip('"').strip("'")
        break

URLS = [
    'https://openrouter.ai/api/v1/chat/completions',
    'https://openrouter.ai/api/v1/models',
]

for url in URLS:
    req = urllib.request.Request(
        url,
        data=json.dumps({'model': 'google/gemini-2.0-flash-001',
                         'messages': [{'role': 'user', 'content': 'hi'}]}).encode()
        if 'chat' in url else None,
        headers={
            'Authorization': f'Bearer {api_key}',
            'Content-Type': 'application/json',
            'HTTP-Referer': 'https://github.com/deadlockvn',
            'X-Title': 'Deadlock VN',
        },
        method='POST' if 'chat' in url else 'GET')

    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            body = resp.read().decode('utf-8')
            print(f'OK   {url}  -> {body[:200]}')

    except urllib.error.HTTPError as ex:
        detail = ex.read().decode('utf-8', 'replace')[:300]
        print(f'FAIL {url}  -> {ex.code} {detail}')

    except Exception as ex:
        print(f'FAIL {url}  -> {type(ex).__name__}: {ex}')
