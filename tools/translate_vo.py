"""Dich citadel_generated_vo (78k dong VO) - chay nen, co resume.

Vo duong dai nen gui nhieu dong moi lan (tiet kiem token).
"""
import io
import json
import os
import re
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import translate_file as tf
import tr_common

ROOT = tr_common.ROOT
FOLDER = 'citadel_generated_vo'
ST = ROOT + r'\workspace\staging\localization'
SFILE = os.path.join(ST, FOLDER, FOLDER + '_vietnamese.txt')
OFILE = os.path.join(ROOT, 'workspace', 'source', 'original', FOLDER,
                     FOLDER + '_english.txt')
CACHE = os.path.join(ROOT, 'tools', '_tr_' + FOLDER + '.json')
STATE = os.path.join(ROOT, 'tools', '_vo_state.json')

BATCH = 60
PAUSE = 0.4


def main():
    rows = tf.parse_file(SFILE)
    orig = dict((k, v) for _i, k, _r, v in tf.parse_file(OFILE))

    todo = []

    for i, key, raw, val in rows:
        if key not in orig:
            continue

        if tf.is_real_english(orig[key], val):
            todo.append({'line': i, 'key': key, 'en': orig[key]})

    done = {}

    if os.path.exists(CACHE):
        done = json.load(io.open(CACHE, encoding='utf-8'))

    todo = [e for e in todo if e['key'] not in done]
    print(f'[VO] can dich {len(todo)} (da co {len(done)})', flush=True)

    api_key = tr_common.load_key()

    if not api_key:
        print('THIEU API KEY')
        sys.exit(1)

    t0 = time.time()

    for i in range(0, len(todo), BATCH):
        batch = todo[i:i + BATCH]
        out, err = tr_common.translate_batch(api_key, batch)

        if err:
            print(f'  [{i}] LOI {err[:100]}', flush=True)
            tr_common.wait_retry(i // BATCH + 1)
            continue

        done.update(out)
        json.dump(done, io.open(CACHE, 'w', encoding='utf-8'),
                  ensure_ascii=False, indent=1)

        # Luu ghi vao file ngay de khong mat khi may tat
        json.dump({'done': len(done), 'idx': i + len(batch)},
                  io.open(STATE, 'w', encoding='utf-8'), ensure_ascii=False)

        el = time.time() - t0
        rate = (i + len(batch)) / max(el, 1) * 60
        eta = (len(todo) - i - len(batch)) / max(rate / 60, 0.01) / 60
        print(f'  [{i + len(batch)}/{len(todo)}] {rate:.1f}/ph ETA {eta:.1f}h '
              f'({time.strftime("%H:%M:%S")})', flush=True)
        time.sleep(PAUSE)

    print(f'[VO] xong. tong {len(done)}')


if __name__ == '__main__':
    main()
