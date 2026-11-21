"""Sua 5 entry thuat ngu con sot (khong dua vao entry user da sua)."""
import io
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?://.*)?)$')

MANUAL = {
    'Citadel_Hud_OpenCorruptedShop':
        "Nhấn {g:citadel_binding:'OpenHeroSheet'} để vào Shop tha hoá",
    'Citadel_Hero_Stats_Tooltip_HealthMult':
        'Tổng thưởng cho Máu gốc của bạn từ việc mua các vật phẩm trong cây Sinh Lực',
    'Citadel_StreetBrawl_Overtime_Desc':
        'Tăng cường máu cho Lính<br>Tăng thời gian hồi sinh',
    'Citadel_Shop_PickCorruptItem_StreetBrawlDesc':
        'Miễn phí tha hoá một trong những vật phẩm không được thăng hoa của bạn. '
        'Vật phẩm bị tha hoá sẽ mạnh mẽ hơn mãi mãi, nhưng mỗi vật phẩm cũng mang '
        'theo một nhược điểm.',
    'CitadelHeroStats_SpiritPower_Desc':
        'Sát thương Phép tăng cường hiệu quả của Kỹ năng và Đồ vật.',
}


def unescape(v):
    v = v.replace('\\"', '"').replace('\\n', '\n').replace('\\t', '\t')
    return v.replace('\\\\', '\\')


def escape(v):
    return (v.replace('\\', '\\\\').replace('"', '\\"')
             .replace('\n', '\\n').replace('\t', '\\t'))


def main():
    with io.open(STAGING, encoding='utf-8') as f:
        lines = f.readlines()

    applied = 0

    for i, raw in enumerate(lines):
        m = LINE_RE.match(raw.rstrip('\n'))

        if not m:
            continue

        key = re.match(r'\s*"([^"]+)"', m.group(1)).group(1)

        if key not in MANUAL:
            continue

        new = MANUAL[key]

        if unescape(m.group(2)) != new:
            lines[i] = m.group(1) + '"' + escape(new) + m.group(3) + '\n'
            applied += 1

    with io.open(STAGING, 'w', encoding='utf-8', newline='') as f:
        f.writelines(lines)

    print(f'Sua: {applied}/{len(MANUAL)}')


if __name__ == '__main__':
    main()
