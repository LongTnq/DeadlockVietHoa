"""Dich cac file gc_* bang API.

- citadel_gc_mod_names: ten nang cap (upgrades) - dich phai de chu ky tim kiem
- citadel_gc_hero_names: ten tuong - giu nguyen (la ten rieng)
"""
import io
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import tr_common

ROOT = tr_common.ROOT
ST = ROOT + r'\workspace\staging\localization'

# Nhung key co "_search" - phai chua duoi chuoi goc + phai chua
# chuoi tieng Viet de tim kiem hoat dong
def pick(value):
    return value


def main():
    folder = sys.argv[1]
    batch_size = int(sys.argv[2]) if len(sys.argv) > 2 else 30
    only_search = '--search' in sys.argv

    sfile = os.path.join(ST, folder, folder + '_vietnamese.txt')
    ofile = os.path.join(ROOT, 'workspace', 'source', 'original', folder,
                         folder + '_english.txt')

    import translate_file as tf

    rows = tf.parse_file(sfile)
    orig = dict((k, v) for _i, k, _r, v in tf.parse_file(ofile))

    todo = []

    for i, key, raw, val in rows:
        if key not in orig:
            continue

        if only_search and '_search' not in key:
            continue

        if not orig[key].strip():
            continue

        if val.strip() == orig[key].strip():
            todo.append({'line': i, 'key': key, 'en': orig[key]})

    cache_path = os.path.join(ROOT, 'tools', '_tr_' + folder + '.json')
    done = {}

    if os.path.exists(cache_path):
        done = json.load(io.open(cache_path, encoding='utf-8'))

    todo = [e for e in todo if e['key'] not in done]
    print(f'[{folder}] can dich {len(todo)}', flush=True)

    if not todo:
        return

    api_key = tr_common.load_key()

    if not api_key:
        print('THIEU API KEY')
        sys.exit(1)

    for i in range(0, len(todo), batch_size):
        batch = todo[i:i + batch_size]
        out, err = tr_common.translate_batch(api_key, batch)

        if err:
            print(f'  [{i}] LOI {err[:100]}', flush=True)
            tr_common.wait_retry(i // batch_size + 1)
        else:
            print(f'  [{i + 1}/{len(todo)}] ok {len(out)}/{len(batch)}',
                  flush=True)

        done.update(out)
        json.dump(done, io.open(cache_path, 'w', encoding='utf-8'),
                  ensure_ascii=False, indent=1)

    lines = io.open(sfile, encoding='utf-8-sig').read().split('\n')
    changed = 0
    rejected = []

    for e in todo:
        vi = done.get(e['key'])

        if not vi:
            continue

        if vi.strip() == e['en'].strip():
            continue

        g = tf.guard(e['en'], vi)

        if g:
            rejected.append((e['key'], g, vi[:80]))
            continue

        ln = e['line']
        m = tf.LINE_RE.match(lines[ln - 1])

        if not m:
            continue

        lines[ln - 1] = m.group(1) + '"' + tf.escape(vi) + m.group(3)
        changed += 1

    io.open(sfile, 'w', encoding='utf-8', newline='').write('\n'.join(lines))
    print(f'[{folder}] da ghi {changed} dong; bi loai {len(rejected)}')

    for k, g, v in rejected[:20]:
        print(f'   LOAI {g}: {k} -> {v}')


if __name__ == '__main__':
    main()
