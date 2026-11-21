"""Liet ke model OpenRouter kha dung (uu tien re + tot tieng Viet)."""
import io
import json
import os
import urllib.request

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'

api_key = ''

for line in io.open(os.path.join(ROOT, '.env'), encoding='utf-8'):
    line = line.strip()
    if line.startswith('DEADLOCKVN_API_KEY='):
        api_key = line.split('=', 1)[1].strip().strip('"').strip("'")
        break

req = urllib.request.Request(
    'https://openrouter.ai/api/v1/models',
    headers={'Authorization': f'Bearer {api_key}'})

with urllib.request.urlopen(req, timeout=60) as resp:
    data = json.loads(resp.read().decode('utf-8'))

# Cac model thuong dung cho viec dich, tim theo ten
KEYWORDS = ['gemini', 'llama-3.3', 'llama-3.1', 'qwen', 'mistral', 'gemma',
            'deepseek', 'glm', 'ministral', 'exaone', 'devstral']

found = []

for m in data['data']:
    mid = m['id']
    low = mid.lower()

    if any(k in low for k in KEYWORDS):
        pricing = m.get('pricing', {})
        prompt = float(pricing.get('prompt', '1') or 0) * 1_000_000
        found.append((prompt, mid, m.get('name', '')))

found.sort()

print(f'Tong model: {len(data["data"])}, khop tu khoa: {len(found)}\n')

for prompt, mid, name in found[:45]:
    print(f'  ${prompt:>8.3f}/M tok   {mid}')

print('\n--- Tiem kiem gia re nhat ---')

for prompt, mid, name in found[:15]:
    print(f'  ${prompt:>8.3f}  {mid}')
