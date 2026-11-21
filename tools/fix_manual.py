"""Sua thu cong cac entry bi sai sau khi ap dung thuat ngu tu dong."""
import io
import re

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'
STAGING = ROOT + r'\workspace\staging\localization\citadel_main\citadel_main_vietnamese.txt'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?://.*)?)$')


def unescape(v):
    v = v.replace('\\"', '"').replace('\\n', '\n').replace('\\t', '\t')
    return v.replace('\\\\', '\\')


def escape(v):
    return (v.replace('\\', '\\\\').replace('"', '\\"')
             .replace('\n', '\\n').replace('\\t', '\\t'))


# key -> gia tri moi ( sua thu cong)
MANUAL = {
    # --- "Tướng Labs" -> giu nguyen "Hero Labs" ---
    'Citadel_StartMatchmaking_HeroLabsMMNotOpen': 'Hero Labs matchmaking hiện không có sẵn.',
    'Citadel_StartMatchmaking_HeroLabsNotUnlocked': 'Bạn chưa mở khóa matchmaking Hero Labs.',
    'Citadel_StartMatchmaking_NoHeroLabsWhileInLowPri': 'Matchmaking Hero Labs bị cấm khi ở mức ưu tiên thấp.',
    'Citadel_Dashboard_Status_Mode_HeroLabs': 'Hero Labs Match',
    'Citadel_MatchmakingStopped_Reason_HeroLabsClosed': 'Quá trình tìm kiếm Hero Labs hiện đang đóng cửa.',
    'Citadel_PartySetMode_HeroLabsMMNotOpen': 'Chế độ ghép cặp Hero Labs hiện đang không khả dụng.',
    'Citadel_PartySetMode_HeroLabsNotUnlocked': 'Người chơi trong nhóm của bạn {g:citadel_persona:nl:account_id} hiện chưa đủ điều kiện tham gia ghép cặp Hero Labs.',
    'Citadel_PartySetMode_NoHeroLabsWhileInLowPri': 'Người chơi trong nhóm của bạn {g:citadel_persona:nl:account_id} đang ở mức ưu tiên thấp và vì vậy không thể xếp hàng vào matchmaking của Hero Labs.',

    # --- "Tướng Damage" -> "Sát thương Tướng" ---
    'Citadel_Profile_Stats_HeroDamage:f': 'Sát thương Tướng',
    'Citadel_HUD_DamageReport_DamageByHero': 'Sát thương Theo Tướng',

    # --- "Test Tướng" -> "Tướng Thử Nghiệm" ---
    'Citadel_HeroPage_Sandbox': 'Tướng Thử Nghiệm',
    'Citadel_HeroPage_Sandbox_Solo': 'Tướng Thử Nghiệm',

    # --- "Duplicate Tướng Disabled" -> dich lai ---
    'Citadel_PrivateLobby_Duplicate_Heroes_Off': 'Đã Tắt Cho Phép Trùng Tướng',
    'Citadel_PrivateLobby_Duplicate_Heroes_On': 'Đã Bật Cho Phép Trùng Tướng',
    'Citadel_PrivateLobby_Experimental_Heroes_Off': 'Đã Tắt Tướng Thử Nghiệm',
    'Citadel_PrivateLobby_Experimental_Heroes_On': 'Đã Bật Tướng Thử Nghiệm',

    # --- "Show Experimental Tướng" -> dich lai ---
    'Citadel_Dashboard_ShowHeroLabHeroes': 'Hiện Tướng Thử Nghiệm',

    # --- "Phép sát thương" -> "Sát thương Phép" ---
    'Citadel_HUD_LevelReward_SpiritDamage': 'Sát thương Phép',

    # --- "Sức mạnh phép" -> "Sức mạnh Phép" ---
    'CITADEL_ABILITY_INVALID_FAILED_DASH_JUMP_NEEDS_STAMINA': 'Nhảy trượt: Không đủ sức mạnh Phép',

    # --- "Phép Ranh Giới" -> "Ngưỡng Phép" ---
    'EShopFilterSpiritVuln': 'Ngưỡng Phép',

    # --- "Sát thương phép" -> "Sát thương Phép" ---
    'EShopFilterSpiritAdditionalDamage': 'Sát thương Phép',

    # --- "Thăng hoa sức mạnh" -> nghinh nga ---
    'Citadel_HUD_CorruptedItemShopSpawn_Desc': 'Thăng hoa sức mạnh các vật phẩm của bạn... nếu dám',

    # --- "Sát thương Phép thăng hoa hiệu quả" -> sai nghia ---
    'CitadelHeroStats_SpiritPower_Desc': 'Sát thương Phép tăng cường hiệu quả của Kỹ năng và Đồ vật.',

    # --- "Thăng hoa kỹ năng" -> "Nâng cấp kỹ năng" ---
    'CitadelShopFilterCategory_AbilityImprovements': 'Nâng cấp kỹ năng',

    # --- "Thăng hoa súng" -> "Nâng cấp súng" ---
    'CitadelShopFilterCategory_GunImprovements': 'Nâng cấp súng',

    # --- "mộtHero" -> "một Tướng" ---
    'citadel_settings_show_all_off_notice_body': 'Khi duyệt thông tin về một Tướng cụ thể, chỉ những cài đặt có thể tùy chỉnh cho Tướng đó mới được hiển thị<br><br>To change this or any other global settings, select Base Settings in the hero selector.',

    # --- "điểm máu" -> "điểm Máu" ---
    'hp_permanent_pickup_label': '+15 điểm Máu',

    # --- "kiểm chứng" ---
    'citadel_settings_share_missing_binds_caption_scopes': 'Sau khi áp dụng, các lệnh này cần thiết cho trò chơi sẽ không có phím gán. Đảm bảo bạn gán chúng cho những tướng được hiển thị, hoặc trong cài đặt cơ sở nếu biểu tượng cài đặt cơ bản (icon) được hiển thị:{g:citadel_icon:\'BASE_SETTINGS\'}) is shown:',
}


def main():
    with io.open(STAGING, encoding='utf-8-sig') as f:
        lines = f.readlines()

    applied = 0
    missing = []

    for i, raw in enumerate(lines):
        m = LINE_RE.match(raw.rstrip('\n'))

        if not m:
            continue

        key = re.match(r'\s*"([^"]+)"', m.group(1)).group(1)

        if key not in MANUAL:
            continue

        new = MANUAL[key]
        old = unescape(m.group(2))

        if old != new:
            lines[i] = m.group(1) + '"' + escape(new) + m.group(3) + '\n'
            applied += 1

    with io.open(STAGING, 'w', encoding='utf-8', newline='') as f:
        f.writelines(lines)

    print(f'Sua thu cong: {applied}/{len(MANUAL)}')

    for k in MANUAL:
        if k not in [re.match(r'\s*"([^"]+)"', LINE_RE.match(l.rstrip('\n')).group(1)).group(1)
                     for l in lines if LINE_RE.match(l.rstrip('\n'))]:
            missing.append(k)

    if missing:
        print('Khong tim thay key:')

        for k in missing:
            print(f'  {k}')


if __name__ == '__main__':
    main()
