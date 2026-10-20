"""Focused append-only deployment; never regenerates unrelated Text rows."""
from pathlib import Path
import json
import openpyxl
from cafe_orders import build


def apply(project_root):
    root = Path(project_root)
    data = build(root)
    books = {
        'Assets/ConfigTables/Data/Narrative/Narrative.xlsx': ('OpeningPresentation', 'OpeningLine'),
        'Assets/ConfigTables/Data/Shared/Text.xlsx': ('Text',),
    }
    counts = {}
    for relative, tables in books.items():
        path = root / relative
        wb = openpyxl.load_workbook(path)
        before = {s.title: list(s.values) for s in wb}
        changed = False
        for table in tables:
            sheet = wb[table]
            header = [c.value for c in sheet[1]][1:]
            primary = header[0]
            existing = {r[1]: dict(zip(header, r[1:])) for r in sheet.values if len(r) > 1}
            added = 0
            for row in data[table]:
                key = row[primary]
                if key in existing:
                    if any(existing[key].get(k) != v for k, v in row.items()):
                        raise ValueError(f'Refusing to overwrite existing authored row: {key}')
                    continue
                sheet.append([None] + [row.get(k) for k in header])
                added += 1
            counts[table] = added
            changed |= bool(added)
        if changed:
            wb.save(path)
        wb.close()
        check = openpyxl.load_workbook(path)
        for sheet in check:
            assert list(sheet.values)[:len(before[sheet.title])] == before[sheet.title], sheet.title
        check.close()
    for table, rows in data.items():
        path = root / f'Assets/Settings/Resources/Luban/tb{table.lower()}.json'
        raw = path.read_bytes()
        source = raw.decode('utf-8-sig')
        existing = json.loads(source)
        primary = next(iter(rows[0]))
        keys = {row[primary]: row for row in existing}
        new = []
        for row in rows:
            # Match Luban's defaults for the optional source cells.
            generated = dict(row)
            if table == 'OpeningPresentation':
                generated['centerNameKey'] = ''
            elif table == 'OpeningLine':
                generated['autoAdvanceSeconds'] = 0
            if row[primary] in keys:
                if keys[row[primary]] != generated:
                    raise ValueError(f'Refusing JSON overwrite: {row[primary]}')
            else:
                new.append(generated)
        if new:
            # Append at the array terminator, preserving all existing JSON bytes.
            end = raw.rfind(b']')
            start = end
            while start and raw[start-1:start] in (b' ', b'\r', b'\n', b'\t'):
                start -= 1
            newline = '\r\n' if b'\r\n' in raw else '\n'
            suffix = json.dumps(new, ensure_ascii=False, indent=2)[1:-1].strip('\n')
            suffix = suffix.replace('\n', newline).encode('utf-8')
            updated = raw[:start] + (b',' if existing else b'') + newline.encode() + suffix + raw[start:]
            assert json.loads(updated.decode('utf-8-sig')) == existing + new
            path.write_bytes(updated)
        counts[table + 'Json'] = len(new)
    return counts


if __name__ == '__main__':
    print(json.dumps(apply(Path(__file__).resolve().parents[3]), ensure_ascii=False))
