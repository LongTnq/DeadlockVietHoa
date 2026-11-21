"""Shared translation helper: OpenRouter + glossary + placeholder guard."""
import io
import json
import os
import re
import time
import urllib.request

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
ENDPOINT = 'https://openrouter.ai/api/v1/chat/completions'
MODEL = 'qwen/qwen3.7-flash'

GLOSSARY = """- Health / Base Health = Máu / Máu gốc
- Spirit / Spirit Power / Spirit Damage = Phép / Sát thương Phép
- Boons / Boon = Ân huệ
- Hero / Heroes = Tướng / tướng
- Item / Items = vật phẩm
- Build = Lối lên vật phẩm
- Corrupted = Tha hoá
- Enhanced = Thăng hoa
- Ranked / Rank / Subrank = Xếp hạng / Xếp hạng / phân hạng
- Calibration = phân hạng
- Soul / Souls = Linh hồn
- Quickbuy = Mua nhanh
- Lane = đường (lạ)
- Strand = khu vực
- Street Brawl = Au đá Đường phố
- Guardian, Walker, Patron, Deadlock, Steam, Source2 = giữ nguyên tiếng Anh
- Trooper = Linh
- Haunt = Linh ma
- Urn = Bình linh hồn
- Shrine = Miếu
- Zipline = Dây du
- Unstick = Gỡ kẹt
- Parry = Chặn
- Immobilize = Tê imm
- Haste = Tăng tốc
- Dash = Lướt
- Wall Climb = Leo tường
- Stomp = Đánh bằng
- Teleport = Dịch vị
- Stealth = Ám tháo
- Grounding = Chân địa
- Crowd Control = Khống chế diện rộng
- Lifesteal = Hút máu
- Anti-Healing = Giảm hồi máu
- Bullet Resist = Kháng đạn
- Spirit Resist = Kháng phép
- Burst Damage = Sốc sát thương
- Ability Duration = Thời gian duy trì kỹ năng
- Ability Charges = Số lần sử dụng kỹ năng
- Cooldown = Hồi chiêu
- Melee = Cận chiến
- Slowing Magic = Phép làm chậm
- Magic Vulnerability = Xuyên kháng phép
- Anti-Gun = Giảm bắn tay
- Innate = Bẩm sinh
- Passive = Bị động
- Active = Kích hoạt
- Weapon = Vũ khí
- Ammo = Đạn
- Magazine = Băng đạn
- Clip = Băn đạn
- Reload = Nạp đạn
- Killstreak = Chuỗi chiến thắng
- First Blood = Máu đầu
- MVP = Nhân viên xuất sắc
- Scoreboard = Bảng xếp hạng
- Net Worth = Tài sản
- Streak = Chuỗi
- Unranked = Không xếp hạng
- Party = Nhóm
- Spectator = Khung giả
- Focused = Tập trung
- Tank = Tank, Brawler = Bổ, Support = Hỗ trợ, Disruption = Phong tán
- Duelist = Đơn đấu, Protector = Bảo vệ, Assassin = Sát thủ, Sniper = Bắn tỉa
- Damage = Sát thương, Duration = Thời gian, Radius = Bán kính, Range = Tầm
- Speed = Tốc độ, Stun = Choáng, Slow = Làm chậm, Heal = Hồi máu
- Tech = Công nghệ, Trap = Bẫy, Wall = Tường, Zone = Vùng
- Ability = Kỹ năng, Upgrade = Nâng cấp, Item = Vật phẩm, Soul = Linh hồn
- Gold = Vàng, Bounty = Treo thưởng, Mark = Ấn, Stacks = Lớp
- Mũi tên / hướng = hướng, Wave = Sóng, Charge = Xung, Blast = Nổ
- Turret = Tháp pháo, Wall = Tường, Bomb = Bom, Missile = Tên lửa
- Damage Reduction = Giảm sát thương, Move Speed = Tốc độ di chuyển
- Attack Range = Tầm đánh, Cooldown = Hồi chiêu, Charges = Số lần dùng"""

SYSTEM = (
    'Ban la nguoi dich game Deadlock sang tieng Viet. '
    'Dich CHINH XAC, giu nguyen moi placeholder (token {..} va {.., plural, ..}) '
    'va moi the HTML (<span>, <br>, <b>). '
    'KHONG them, KHONG bo, KHONG doi placeholder. Giu nguyen so lieu. '
    'Voi ten rieng (Broadway, York, Greenwich, Guardian, Walker, Patron, '
    'Deadlock, Steam, Source2, Ultimates) thi giu nguyen tieng Anh. '
    'Voi phim tat (ESC, Tab, Shift, Ctrl, Alt, Win, Space, Enter, M1..M5, '
    'KDA, AP, FPS) va don vi (s, m, m/s, HP, DPS) thi giu nguyen tieng Anh. '
    'Tra ve JSON duy nhat: {"items": [{"id": "<id>", "text": "<ban dich>"}]}. '
    'QUAN TRONG: ban dich phai co DAU TIENG VIET day du '
    '(ví dụ "Hút máu", "Sát thương Phép", "Tướng"), khong duoc bo dau. '
    'Neu chi la placeholder/ky tu thi tra ve nguyen ban goc.'
)

USER_TMPL = (
    'Dich cac chuoi sau sang tieng Viet.\n\n{glossary}\n'
    'Danh sach (id | English):\n{items}\n\n'
    'Tra ve JSON: {{"items": [{{"id": "...", "text": "..."}}]}} cho TAT CA id, '
    'cung thu tu.'
)


def load_key():
    key = os.environ.get('DEADLOCKVN_API_KEY', '')

    if not key:
        p = os.path.join(ROOT, '.env')

        if os.path.exists(p):
            for line in io.open(p, encoding='utf-8'):
                line = line.strip()

                if line.startswith('DEADLOCKVN_API_KEY='):
                    key = line.split('=', 1)[1].strip().strip('"').strip("'")
                    break

    return key


def call_api(api_key, prompt, max_tokens=16000, temperature=0.2):
    payload = {
        'model': MODEL,
        'messages': [
            {'role': 'system', 'content': SYSTEM},
            {'role': 'user', 'content': prompt},
        ],
        'temperature': temperature,
        'max_tokens': max_tokens,
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

    content = body['choices'][0]['message']['content'].strip()

    if content.startswith('```'):
        content = re.sub(r'^```(?:json)?\s*', '', content)
        content = re.sub(r'\s*```$', '', content)

    start = content.find('{')
    end = content.rfind('}')

    if start < 0 or end < 0:
        raise ValueError('Khong tim thay JSON trong response')

    return json.loads(content[start:end + 1])


def build_prompt(batch):
    lines = [f'{e["key"]} | {e["en"]}' for e in batch]

    return USER_TMPL.format(glossary=GLOSSARY, items='\n'.join(lines))


def translate_batch(api_key, batch, depth=0):
    try:
        result = call_api(api_key, build_prompt(batch))
        out = {}

        for it in result.get('items', []):
            k = it.get('id')
            t = it.get('text')

            if k and t is not None:
                out[k] = t

        return out, None
    except Exception as ex:
        if len(batch) > 2 and depth < 5:
            mid = len(batch) // 2
            a, e1 = translate_batch(api_key, batch[:mid], depth + 1)
            b, e2 = translate_batch(api_key, batch[mid:], depth + 1)
            a.update(b)

            return a, e1 or e2

        return {}, str(ex)


def wait_retry(n):
    time.sleep(min(3 * n, 20))
