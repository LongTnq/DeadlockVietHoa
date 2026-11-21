"""Ghi toan bo thay doi cua nguoi dung ra file de doc."""
import io
import json

ROOT = r'd:\GAMEMODS\Deadlock\DeadlockVietnameseLocalization'

edits = json.load(io.open(ROOT + r'\tools\_user_edits.json', encoding='utf-8'))
edits.sort(key=lambda e: e['line'])

out = io.open(ROOT + r'\tools\_user_edits_report.txt', 'w', encoding='utf-8')

for e in edits:
    out.write(f'[d{e["line"]}] {e["key"]}\n')
    out.write(f'  EN : {e["en"]}\n')
    out.write(f'  CU : {e["old"]}\n')
    out.write(f'  MOI: {e["new"]}\n\n')

out.close()

print(f'{len(edits)} thay doi -> tools/_user_edits_report.txt')
