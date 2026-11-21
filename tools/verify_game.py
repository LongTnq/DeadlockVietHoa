import sys
path = r'E:\Steam\steamapps\common\Deadlock\game\citadel\resource\localization\citadel_main\citadel_main_vietnamese.txt'
data = open(path, 'rb').read()
txt = data.decode('utf-8')
i = txt.index('"Cancel"')
print(txt[i:i+400])
print()
print('BOM:', data[:3] == b'\xef\xbb\xbf')
print('Total bytes:', len(data))
print('Vietnamese chars:', sum(1 for c in txt if '\u00c0' <= c <= '\u1ef9'))
