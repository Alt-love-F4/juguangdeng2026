"""Build isolated empty/invalid authoring fixtures; never replace production workbooks.
Run from repository root: python Assets/ConfigTables/create_cafe_visit_fixtures.py
"""
from pathlib import Path
import shutil
import openpyxl

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Library/Locus/tmp/cafe-visit-fixtures'

def main():
    for name in ('empty', 'invalid'):
        target = OUTPUT / name
        target.mkdir(parents=True, exist_ok=True)
        shutil.copytree(ROOT / 'Assets/ConfigTables/Data', target / 'Data', dirs_exist_ok=True)
        path = target / 'Data/Narrative/Narrative.xlsx'
        book = openpyxl.load_workbook(path)
        if name == 'empty':
            for sheet in ('CafeVisit', 'CafeVisitHook'):
                ws = book[sheet]
                ws.delete_rows(5, max(0, ws.max_row - 4))
        else:
            ws = book['CafeVisit']
            ws.append([None, 'fixture_invalid', 'adele', 'missing_seat', 'missing/visual', '-4', 'NaN', 'invalid'])
            book['CafeVisitHook'].append([None, 'fixture_invalid', 'served', 'auto', 1, 'MISSING_EVENT'])
        book.save(path)
        print(path)

if __name__ == '__main__':
    main()
