"""Dich cac entry con tieng Anh trong 1 file staging va ghi lai.

Usage: python tools/translate_file.py <folder> [batch_size]
Output cache: tools/_tr_<folder>.json (resume duoc)
"""
import io
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import tr_common

ROOT = tr_common.ROOT
ST = ROOT + r'\workspace\staging\localization'

LINE_RE = re.compile(r'^(\s*"[^"]+"\s+)"((?:[^"\\]|\\.)*)("\s*(?:\s*//.*)?)$')
TOKEN_RE = re.compile(r'\{[^{}]*\}')
ONLY_TOKEN_RE = re.compile(r'^[\s\W\d]*$')


def unescape(v):
    return v.replace('\\"', '"').replace('\\n', '\n').replace('\\t', '\t')


def escape(v):
    return v.replace('\\', '\\\\').replace('"', '\\"').replace(
        '\n', '\\n').replace('\t', '\\t')


def parse_file(path):
    """-> list of (lineno, key, raw_value, value_unescaped)"""
    rows = []

    with io.open(path, encoding='utf-8-sig') as f:
        for i, raw in enumerate(f, 1):
            s = raw.rstrip('\n')
            m = LINE_RE.match(s)

            if not m:
                continue

            key = re.match(r'\s*"([^"]+)"', m.group(1)).group(1)
            rows.append((i, key, m.group(2), unescape(m.group(2))))

    return rows


def tokens_of(s):
    return sorted(TOKEN_RE.findall(s))


def has_viet_diacritics(s):
    """Co dau tieng Viet hay khong (ky tu dac biet tieng Viet)."""
    return bool(re.search(
        '[ăâđêÔơƯĂÂĐêôơư'
        'àáạảãâầấậẩẫằắặẳẵặ'
        'èéẹẻẽêềếệểễệ'
        'ìíịỉĩ'
        'òóọỏõôồốộổỗộ'
        'ờớợởỡợ'
        'ùúụủũưừứựửữự'
        'ỳýỵỷỹỵ'
        'ÁÀẢÃẠẮẰẲẴẶẦẤẨẪẬ'
        'ÉÈẺẼẸỀẾỂỄỆ'
        'ÍÌỈĨỊ'
        'ÓÒỎÕỌỐỒỔỖỘỚỜỞỠỢ'
        'ÚÙỦŨỤỨỪỬỮỰ'
        'ỲÝỶỸỴ'
        'Đđ]', s))


WORD_RE = re.compile(r'[A-Za-z]{2,}')


def guard(en, vi):
    """Kiem tra bao dam an toan: placeholder giong het, so HTML giong het,
    va ban dich phai co dau tieng Viet (neu la cau tieng Anh)."""
    if tokens_of(en) != tokens_of(vi):
        return 'placeholder'

    if en.count('<') != vi.count('<') or en.count('>') != vi.count('>'):
        return 'html'

    # Ban dich co dau tieng Viet?
    words = WORD_RE.findall(vi)

    if len(words) >= 1 and len(vi) >= 4:
        if not has_viet_diacritics(vi):
            # thu lai: co the la tu rieng (Tank, Guardian...) -> cho qua
            if vi.strip() in KEEP_WORDS:
                return None

            return 'no-diacritics'

    return None


# Don vi / ky tu dac biet: giu nguyen, khong dich
UNIT_VALUES = {
    's', 'm', 'x', '/s', '%/s', 'm/s', 'hp/s', 'HP/s', 'HP', 'DPS', 'dps',
    'deg', '°', '%', 'OK', 'AP', 'K', 'D', 'A', 'ESC', 'Tab', 'Caps', 'Enter',
    'Shift', 'R.Shift', 'Ctrl', 'Alt', 'LShift', 'Space', 'Win', 'M1', 'M2',
    'M3', 'M4', 'M5', 'L1', 'R1', 'FPS', 'SS', 'ping', 'n/a',
}


def needs_translation(orig_val, stage_val):
    """True neu gia tri hien tai can dich lai:
    - con nguyen tieng Anh (chua duoc dich)
    - hoac da dich nhung thieu dau tieng Viet
    """
    if not orig_val.strip():
        return False

    if stage_val.strip() == orig_val.strip():
        # gia tri == ban goc: con tieng Anh?
        if orig_val.strip() in UNIT_VALUES:
            return False

        stripped = TOKEN_RE.sub('', orig_val)

        if ONLY_TOKEN_RE.match(stripped):
            return False

        return True

    # Da duoc dich -> co thieu dau khong?
    if not stage_val.strip():
        return False

    s = re.sub(r'<[^>]*>', ' ', stage_val)
    s = TOKEN_RE.sub('', s)

    # co dau tieng Viet -> ok
    if has_viet_diacritics(stage_val):
        return False

    # chi con chu cai ASCII >=2 -> kha nang mat dau
    if not re.search(r'[A-Za-z]{2,}', s):
        return False

    # nhung neu gia tri chi la don vi/phim tat -> giu nguyen
    if stage_val.strip() in UNIT_VALUES:
        return False

    if stage_val.strip().lower() in KEEP_WORDS:
        return False

    return True


# Tu tieng Anh duoc giu nguyen (ten rieng, don vi, chu viet tat)
KEEP_WORDS = {
    'guardian', 'walker', 'patron', 'deadlock', 'steam', 'source2', 'valve',
    'broadway', 'york', 'greenwich', 'archmother', 'hidden', 'king',
    'holliday', 'abrams', 'detective', 'yamato', 'haze', 'geist', 'wraith',
    'kelvin', 'bebop', 'nano', 'tengu', 'viper', 'magician', 'dynamo',
    'chrono', 'engineer', 'mirage', 'vindicta', 'grey', 'talon', 'lady',
    'geist', 'mccain', 'seven', 'gadget', 'man', 'sapper', 'glider',
    'warden', 'gunner', 'cowboy', 'phoenix', 'inferno', 'infernus',
    'test', 'hero', 'vampire', 'vampi', 'ghost', 'orion', 'hornet', 'gigawatt',
    'sentry', 'shockwave', 'seven', 'pocket', 'laura', 'mina', 'sinclair',
    'drifter', 'doorman', 'chessmaster', 'unicorn', 'fathom', 'viscous',
    'gunslinger', 'synth', 'mirage', 'wilder', 'toby', 'paradox', 'newt',
    'maddie', 'yoshi', 'shiv', 'magician', 'evan', 'kai', 'vestra', 'grey',
    'brutus', 'burrow', 'vex', 'kah-hor', 'forge', 'ghost',
    'dps', 'hp', 'ap', 'fps', 'ping', 'ss', 'ok', 'ui', 'id', 'hp/s',
    # ky tu dac biet / don vi
    's', 'm', 'x', 'esc', 'tab', 'shift', 'ctrl', 'alt', 'win', 'space',
    'enter', 'caps', 'lshift', 'rshift', 'l1', 'r1', 'm1', 'm2', 'm3', 'm4',
    'm5',
}

# Cac file bo qua hoan toan
SKIP_FOLDERS = {'citadel_gc_hero_names'}


def mixed_english(orig_val, stage_val):
    """Tra ve danh sach tu tieng Anh con lai trong chuoi da dich mot phan."""
    if stage_val.strip() == orig_val.strip():
        return []

    # tach thanh tu (bo HTML)
    s = re.sub(r'<[^>]*>', ' ', stage_val)
    s = re.sub(r'\{[^{}]*\}', ' ', s)
    hits = set()

    for w in re.findall(r"[A-Za-z][A-Za-z'\-]{2,}", s):
        lw = w.lower()

        if lw in KEEP_WORDS:
            continue

        # chi giu nhung tu that su xuat hien trong ban tieng Anh goc
        if not re.search(r'\b' + re.escape(w) + r'\b', orig_val):
            continue

        hits.add(w)

    return sorted(hits)


def main():
    folder = sys.argv[1]
    batch_size = int(sys.argv[2]) if len(sys.argv) > 2 else 30

    sdir = os.path.join(ST, folder)
    sfile = os.path.join(sdir, folder + '_vietnamese.txt')
    ofile = os.path.join(ROOT, 'workspace', 'source', 'original', folder,
                         folder + '_english.txt')

    rows = parse_file(sfile)
    orig = dict((k, v) for _i, k, _r, v in parse_file(ofile))

    # Cac dong can dich: gia tri staged == gia tri goc (con tieng Anh)
    todo = []
    stage_by_line = {}

    for i, key, raw, val in rows:
        stage_by_line[i] = (key, raw, val)

        if key not in orig:
            continue

        if needs_translation(orig[key], val):
            todo.append({'line': i, 'key': key, 'en': orig[key]})

    cache_path = os.path.join(ROOT, 'tools', '_tr_' + folder + '.json')
    done = {}

    if os.path.exists(cache_path):
        done = json.load(io.open(cache_path, encoding='utf-8'))

    todo = [e for e in todo if e['key'] not in done]
    print(f'[{folder}] can dich {len(todo)} (da co cache {len(done)})',
          flush=True)

    if not todo:
        return

    api_key = tr_common.load_key()

    if not api_key:
        print('THIEU API KEY')
        sys.exit(1)

    errors = []

    for i in range(0, len(todo), batch_size):
        batch = todo[i:i + batch_size]
        out, err = tr_common.translate_batch(api_key, batch)

        if err:
            print(f'  [{i}] LOI {err[:100]}', flush=True)
            errors.append((i, err))
            tr_common.wait_retry(i // batch_size + 1)
        else:
            print(f'  [{i + 1}/{len(todo)}] ok {len(out)}/{len(batch)}',
                  flush=True)

        # Ghi ngay cache
        for k, v in out.items():
            done[k] = v

        json.dump(done, io.open(cache_path, 'w', encoding='utf-8'),
                  ensure_ascii=False, indent=1)

    # ---- Ghi vao file staging ----
    lines = io.open(sfile, encoding='utf-8-sig').read().split('\n')
    changed = 0
    rejected = []

    for e in todo:
        vi = done.get(e['key'])

        if not vi:
            continue

        if vi.strip() == e['en'].strip():
            continue

        g = guard(e['en'], vi)

        if g:
            rejected.append((e['key'], g, vi[:80]))
            continue

        ln = e['line']
        m = LINE_RE.match(lines[ln - 1])

        if not m:
            continue

        lines[ln - 1] = m.group(1) + '"' + escape(vi) + m.group(3)
        changed += 1

    io.open(sfile, 'w', encoding='utf-8', newline='').write('\n'.join(lines))
    print(f'[{folder}] da ghi {changed} dong; bi loai {len(rejected)}')

    for k, g, v in rejected[:20]:
        print(f'   LOAI {g}: {k} -> {v}')


if __name__ == '__main__':
    main()
