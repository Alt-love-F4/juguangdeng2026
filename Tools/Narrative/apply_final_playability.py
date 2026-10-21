"""Apply content compiler deltas to current XLSX, preserving unrelated authored cells.
Baseline compiler files must be copied to --baseline before worker changes are integrated.
"""
import argparse, importlib.util, sys, hashlib, json, shutil
from pathlib import Path
import openpyxl
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
from final_playability import apply as finalize

def build(path,name):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);sys.modules[name]=m;spec.loader.exec_module(m)
    # The baseline compiler predates the verified P089 directive-only correction.
    # Reuse the reviewed single-file pin, not a blanket hash bypass; both versions compile identical anchors.
    if name == 'baseline_church':
        m.SOURCE_HASHES['RY70.11.15-S1.docx']='e85f90d1b6b2b4b65b0a31b2c2c325d836fda9dbef8ef28339152e5f45d2ee91'
    return m.build(ROOT)

def apply(baseline):
    paths=[ROOT/'Assets/ConfigTables/Data/Narrative/Narrative.xlsx',ROOT/'Assets/ConfigTables/Data/Shared/Text.xlsx']
    books=[openpyxl.load_workbook(p) for p in paths]; tables={}; owners={}
    for wb in books:
        for ws in wb:
            h=[c.value for c in ws[1]]
            while h and h[-1] is None:h.pop()
            tables[ws.title]={'rows':[dict(zip(h[1:],v[1:])) for v in ws.iter_rows(min_row=5,max_col=len(h),values_only=True) if len(v)>1 and v[1] is not None and not str(v[0] or '').startswith('##')],'header':h}
            owners[ws.title]=ws
    def key(table,r):
        if table=='Bar':return (r.get('ownerId'),r.get('barId'))
        if table=='EventDate':return (r.get('eventId'),r.get('day'),r.get('timeSlot'),r.get('repeatRule'))
        return r.get(tables[table]['header'][1])
    changes=[]
    for name in ['church','rose','neutral']:
        old=build(baseline/(name+'.py'),'baseline_'+name);new=build(ROOT/'Tools/Narrative/Content'/(name+'.py'),'final_'+name)
        for table, incoming in new.items():
            if table.startswith('_'):continue
            before={key(table,r):r for r in old.get(table,[])};after={key(table,r):r for r in incoming}
            current={key(table,r):r for r in tables[table]['rows']}
            if table == 'Condition':
                # Generated condition IDs are sequence-based; replacing individual fields across
                # renumbered IDs mixes unrelated guards. Replace complete compiler-owned owners.
                owned={(r.get('ownerType'),r.get('ownerId')) for r in list(before.values())+list(after.values())}
                current={k:r for k,r in current.items() if (r.get('ownerType'),r.get('ownerId')) not in owned}
                current.update(after)
                tables[table]['rows']=list(current.values())
                changes.append((table,name,'replace complete owner guard sets'))
                continue
            for k,row in after.items():
                if k not in before:current[k]=dict(row);changes.append((table,str(k),'added'));continue
                if k not in current:current[k]=dict(row);changes.append((table,str(k),'restored'));continue
                for field in set(before[k])|set(row):
                    if before[k].get(field)!=row.get(field):
                        current[k][field]=row.get(field);changes.append((table,str(k),field))
            for k in before.keys()-after.keys():
                if k in current:del current[k];changes.append((table,str(k),'removed'))
            tables[table]['rows']=list(current.values())
    finalize(tables)
    backup=ROOT/'Library/Locus/tmp/final-playability/source-backup';backup.mkdir(parents=True,exist_ok=True)
    for p in paths:
        dest=backup/p.name
        if not dest.exists():shutil.copy2(p,dest)
    for name,t in tables.items():
        ws=owners[name];h=t['header']; rows=t['rows']
        for i,row in enumerate(rows,5):
            for j,col in enumerate(h,1):ws.cell(i,j).value=None if j==1 else row.get(col)
        for i in range(len(rows)+5,ws.max_row+1):
            for cell in ws[i]:cell.value=None
    for p,wb in zip(paths,books):wb.save(p);wb.close()
    (backup.parent/'source-changes.json').write_text(json.dumps(changes,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Applied changed rows/cells:',len(changes))
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--baseline',type=Path,required=True);args=parser.parse_args();apply(args.baseline)
