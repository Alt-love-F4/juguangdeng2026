import json,sys
from pathlib import Path
sys.path.insert(0,'.unity'); import mcp
r=mcp.request('execute_code',{'action':'execute','code':Path(sys.argv[1]).read_text(encoding='utf-8-sig')})
if len(sys.argv)>2: Path(sys.argv[2]).write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(r,ensure_ascii=False,indent=2))
