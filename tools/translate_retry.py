"""Dich lai rieng cac entry bi quality guard loai (thieu dau / sai placeholder).

Lay danh sach tu cache _tr_<folder>.json, chi nhung gia tri bi loai, dich lai
voi prompt nhung manh yeu cau dau tieng Viet, roi ghi vao file staging.
"""
import io
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import tr_common
import translate_file as tf

ROOT = tf.ROOT

STRICT_SYSTEM = tr_common.SYSTEM + (
    '\nQUAN TRONG BO PHAN: ban dich phai CO DAU tieng Viet day du. '
    'Vi du "Thua" phai la "Thua", "Ba Lan" phai la "Ba Lan". '
    'Khong duoc bo dau. Neu ban khong chắc, hay giu nguyen chuoi goc.'
)


def build_prompt(batch):
    lines = []

    for e in batch:
        lines.append('%s | %s' % (e['key'], e['en']))

    return (
        'Dich cac chuoi sau sang tieng Viet.\n\n%s\n'
        'Danh sach (id | English):\n%s\n\n'
        'Tra ve JSON: {"items": [{"id": "...", "text": "..."}]} cho TAT CA id, '
        'cung thu tu.'
        % (tr_common.GLOSSARY, '\n'.join(lines)))


def translate_strict(api_key, batch, depth=0):
    try:
        payload = {
            'model': tr_common.MODEL,
            'messages': [
                {'role': 'system', 'content': STRICT_SYSTEM},
                {'role': 'user', 'content': build_prompt(batch)},
            ],
            'temperature': 0.3,
            'max_tokens': 16000,
            'response_format': {'type': 'json_object'},
        }
        import urllib.request

        req = urllib.request.Request(
            tr_common.ENDPOINT,
            data=json.dumps(payload).encode('utf-8'),
            headers={
                'Authorization': 'Bearer ' + api_key,
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

        s = content.find('{')
        e = content.rfind('}')

        if s < 0 or e < 0:
            raise ValueError('Khong tim thay JSON')

        result = json.loads(content[s:e + 1])
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
            a, e1 = translate_strict(api_key, batch[:mid], depth + 1)
            b, e2 = translate_strict(api_key, batch[mid:], depth + 1)
            a.update(b)

            return a, e1 or e2

        return {}, str(ex)


def main():
    folder = sys.argv[1]
    batch_size = 20

    cache_path = os.path.join(ROOT, 'tools', '_tr_' + folder + '.json')
    done = json.load(io.open(cache_path, encoding='utf-8'))

    sfile = (ROOT + '\\workspace\\staging\\localization\\' + folder + '\\' +
             folder + '_vietnamese.txt')
    ofile = (ROOT + '\\workspace\\source\\original\\' + folder + '\\' +
             folder + '_english.txt')

    rows = tf.parse_file(sfile)
    orig = dict((k, v) for _i, k, _r, v in tf.parse_file(ofile))

    # Entry trong cache nhung gia tri hien tai chua duoc ghi (bi loai)
    todo = []
    line_of = dict((k, i) for i, k, _r, _v in rows)

    for key, vi in done.items():
        if key not in orig or key not in line_of:
            continue

        if tf.guard(orig[key], vi):
            todo.append({'line': line_of[key], 'key': key, 'en': orig[key]})

    print('[%s] can dich lai %d entry bi loai' % (folder, len(todo)),
          flush=True)

    if not todo:
        return

    api_key = tr_common.load_key()

    if not api_key:
        print('THIEU API KEY')
        sys.exit(1)

    out_all = {}

    for i in range(0, len(todo), batch_size):
        batch = todo[i:i + batch_size]
        out, err = translate_strict(api_key, batch)

        if err:
            print('  [%d] LOI %s' % (i, err[:100]), flush=True)
            tr_common.wait_retry(i // batch_size + 1)
        else:
            print('  [%d/%d] ok %d/%d' % (i + 1, len(todo), len(out),
                                           len(batch)), flush=True)

        out_all.update(out)

    # Ghi lai cache + file staging
    for k, v in out_all.items():
        done[k] = v

    json.dump(done, io.open(cache_path, 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)

    lines = io.open(sfile, encoding='utf-8-sig').read().split('\n')
    changed = 0
    still = []

    for e in todo:
        vi = out_all.get(e['key'])

        if not vi:
            continue

        if vi.strip() == e['en'].strip():
            continue

        g = tf.guard(e['en'], vi)

        if g:
            still.append((e['key'], g, vi[:70]))
            continue

        ln = e['line']
        m = tf.LINE_RE.match(lines[ln - 1])

        if not m:
            continue

        lines[ln - 1] = m.group(1) + '"' + tf.escape(vi) + m.group(3)
        changed += 1

    io.open(sfile, 'w', encoding='utf-8', newline='').write('\n'.join(lines))
    print('[%s] da ghi them %d dong; van bi loai %d' %
          (folder, changed, len(still)))

    for k, g, v in still:
        print('   LOAI %s: %s -> %s' % (g, k, v))


if __name__ == '__main__':
    main()
