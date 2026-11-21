"""Dich 604 entry con lai trong citadel_main bang API, giu nguyen placeholder.

Dung provider OpenRouter (key trong .env), model gia re, tach lo nho de
tranh rate-limit. Ket qua ghi vao tools/_translated_main.json.
"""
import io
import json
import os
import re
import sys
import time
import urllib.request
import urllib.error

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
OUT = ROOT + r'\tools\_translated_main.json'

ENDPOINT = 'https://openrouter.ai/api/v1/chat/completions'
MODEL = 'qwen/qwen3.7-flash'

# Load .env
api_key = os.environ.get('DEADLOCKVN_API_KEY', '')

if not api_key:
    for line in io.open(os.path.join(ROOT, '.env'), encoding='utf-8'):
        line = line.strip()
        if line.startswith('DEADLOCKVN_API_KEY='):
            api_key = line.split('=', 1)[1].strip().strip('"').strip("'")
            break

if not api_key:
    print('THIEU API KEY!')
    sys.exit(1)

# Load danh sach can dich
items = json.load(io.open(ROOT + r'\tools\_to_translate.json', encoding='utf-8'))

# Load ban da dich (neu co, de resume)
if os.path.exists(OUT):
    done = json.load(io.open(OUT, encoding='utf-8'))
else:
    done = {}

todo = [e for e in items if e['key'] not in done]

print(f'Tong can dich : {len(items)}')
print(f'Da dich       : {len(done)}')
print(f'Con lai       : {len(todo)}')
print()

# ---------------------------------------------------------------- PROMPT
GLOSSARY = """
BANG THUAT NGU VIET NAM (bat buoc tuyet doi):
- Health / Base Health = Mau / Mau goc  (KHONG dung "suc khoe")
- Spirit / Spirit Power / Spirit Damage = Phep / Sat thuong Phep
  (KHONG dung "noi luc")
- Boons / Boon = An hue (KHONG dung "thuan loi", "phuoc lanh")
- Hero / Heroes = Tuong (KHONG dung "anh hung", "hero")
- Item / Items = vat pham
- Build = Loi len vat pham
- Corrupted / Corrupt = tha hoa (KHONG dung "o ue")
- Enhanced = Thang hoa
- Ranked / Rank / Subrank = Xep hang / Xep hang / phan hang
- Calibration / Calibrating = phan hang
- Soul / Souls = Linh hon
- Quickbuy = Mua nhanh
- Lane = duong (la)
- Strand (map) = khu vuc (vi du "Broadway", "York", "Greenwich")
- Street Brawl = Au da Duong pho
- Guardian = Guardian (giu nguyen tieng Anh)
- Walker = Walker (giu nguyen tieng Anh)
- Patron = Patron (giu nguyen tieng Anh)
- Trooper = Linh
- Haunt = Linh ma
- Mid Boss = Trum boss
- Urn = Binh linh hon
- Shrine = Miếu
- Zipline = Day du
- Unstick = Go ket
- Parry = Chan
- Immobilize = Teimm
- Haste = Tang toc
- Jump = Nhay
- Dash = Lurch
- Wall Climb = Leo tuong
- Stomp = Danh bang
- Laser = Laser
- Teleport = Dinh vi
- Stealth = Am tho
- Grounding = Chan dia
- Crowd Control = Khong che
- Lifesteal = Hut mau
- Anti-Healing = Giam hoi mau
- Bullet Resist = Khanh dan
- Spirit Resist = Khanh phép
- Burst Damage = Soc sat thuong
- Ability Duration = Thoi gian duy tri ky nang
- Ability Charges = So lan su dung ky nang
- Cooldown = Hoi chieu
- Melee = Can chien
- Slowing Magic = Phep lam cham
- Magic Vulnerability = Xuyen khang phép
- Crowd Control = Khong che dien rong
- Anti-Gun = Giam ban tay
- Miscellaneous Spirit = Phep da dung
- Innate = Bam sinh
- Passive = Bi dong
- Active = Kich hoat
- Weapon = Vu khi
- Ammo = Dan
- Magazine = Bang dan
- Clip = Ban dan
- Reload = Nap dan
- Melee Lifesteal = Hut mau can chien
- Spirit Slinger = Xa Thu Linh Hon
- Killstreak = Chuoi chien thang
- First Blood = Mau dau
- MVP = Nhat vien xuat sac
- Scoreboard = Bang xep hang
- Net Worth = Tai san
- Demotion Protection = Bao ve truot hang
- Streak = Chuoi
- Unranked / Standard = Khong xep hang / Tieu chuan
- Party = Nhom
- Spectator = Khung gia
- Focused = Tap trung
"""

SYSTEM = (
    'Ban la nguoi dich game Deadlock sang tieng Viet. '
    'Dich CHINH XAC, giu nguyen moi placeholder (token {..} va {.., plural, ..}) '
    'va moi the HTML (<span>, <br>, <b>, <child id=..>). '
    'KHONG them, KHONG bo, KHONG doi placeholder. '
    'Giu nguyen so lieu. Voi ten rieng (Broadway, York, Greenwich, Guardian, '
    'Walker, Patron, Deadlock, Steam, Source2) thi giu nguyen tieng Anh. '
    'Voi phim tat (ESC, Tab, Shift, Ctrl, Alt, Win, Space, Enter, M1..M5, '
    'KDA, AP, FPS) thi giu nguyen tieng Anh. '
    'Tra ve JSON duy nhat: {"items": [{"id": "<id>", "text": "<ban dich>"}]}. '
    'Neu khong dich duoc (chi la placeholder/ky tu) thi tra ve nguyen ban goc.'
)

USER_TMPL = (
    'Dich cac chuoi sau sang tieng Viet.\n\n'
    '{glossary}\n'
    'Danh sach (id | English):\n{items}\n\n'
    'Tra ve JSON: {{"items": [{{"id": "...", "text": "..."}}]}} cho TAT CA id, '
    'cung thu tu.'
)


def build_prompt(batch):
    lines = []

    for e in batch:
        lines.append(f'{e["key"]} | {e["en"]}')

    return USER_TMPL.format(
        glossary=GLOSSARY,
        items='\n'.join(lines))


def call_api(prompt):
    payload = {
        'model': MODEL,
        'messages': [
            {'role': 'system', 'content': SYSTEM},
            {'role': 'user', 'content': prompt},
        ],
        'temperature': 0.2,
        'max_tokens': 16000,
        'response_format': {'type': 'json_object'},
    }

    req = urllib.request.Request(
        ENDPOINT,
        data=json.dumps(payload).encode('utf-8'),
        headers={
            'Authorization': f'Bearer {api_key}',
            'Content-Type': 'application/json',
            'HTTP-Referer': 'https://github.com/deadlockvn',
            'X-Title': 'Deadlock Vietnamese Localization',
        },
        method='POST')

    with urllib.request.urlopen(req, timeout=300) as resp:
        body = json.loads(resp.read().decode('utf-8'))

    content = body['choices'][0]['message']['content']

    # Strip markdown fence
    content = content.strip()

    if content.startswith('```'):
        content = re.sub(r'^```(?:json)?\s*', '', content)
        content = re.sub(r'\s*```$', '', content)

    start = content.find('{')
    end = content.rfind('}')

    if start < 0 or end < 0:
        raise ValueError('Khong tim thay JSON trong response')

    return json.loads(content[start:end + 1])


def main():
    batch_size = int(sys.argv[1]) if len(sys.argv) > 1 else 40

    total = len(todo)
    failed_batches = []

    def translate_batch(batch, depth=0):
        """Dich mot lot. Neu loi JSON, thu lai voi lot nho hon."""
        try:
            result = call_api(build_prompt(batch))
            items_out = result.get('items', [])
            saved = 0

            for it in items_out:
                key = it.get('id')
                text = it.get('text')

                if key and text is not None:
                    done[key] = text
                    saved += 1

            return saved, None

        except Exception as ex:
            if len(batch) > 3 and depth < 4:
                mid = len(batch) // 2
                s1, e1 = translate_batch(batch[:mid], depth + 1)
                s2, e2 = translate_batch(batch[mid:], depth + 1)
                return s1 + s2, e1 or e2

            return 0, str(ex)

    for i in range(0, total, batch_size):
        batch = todo[i:i + batch_size]

        print(f'[{i + 1}/{total}] dang dich {len(batch)} entry...', end=' ',
              flush=True)

        saved, err = translate_batch(batch)

        if err:
            print(f'LOI: {err[:120]}')
            failed_batches.append((i, err))
            time.sleep(3)
        else:
            print(f'OK ({saved}/{len(batch)})')

        with io.open(OUT, 'w', encoding='utf-8') as f:
            json.dump(done, f, ensure_ascii=False, indent=1)

    print()
    print(f'=== KET QUA ===')
    print(f'Da dich trong file nay: {len(done)}')
    print(f'Batch loi: {len(failed_batches)}')

    for i, err in failed_batches:
        print(f'  tu {i}: {err[:120]}')


if __name__ == '__main__':
    main()
