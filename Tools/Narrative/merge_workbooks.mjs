import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import {pathToFileURL} from 'node:url';
import os from 'node:os';
const modules=process.env.OAI_WORKSPACE_NODE_MODULES || path.join(os.homedir(),'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules');
const {FileBlob,SpreadsheetFile}=await import(pathToFileURL(path.join(modules,'@oai/artifact-tool/dist/artifact_tool.mjs')).href);

const root=path.resolve(import.meta.dirname,'../..');
const plan=JSON.parse(await fs.readFile(path.join(root,'output/narrative-integration/merge-plan.json'),'utf8'));
if(plan.errors.length) throw new Error('Content plan has unresolved references; refuse source edits.');
const backup=path.join(root,'output/narrative-integration/source-backup');
await fs.mkdir(backup,{recursive:true});
for(const target of plan.workbooks){
  const before=await fs.readFile(target.path);
  if(crypto.createHash('sha256').update(before).digest('hex')!==target.beforeSha256)throw new Error(`Source changed since merge plan: ${target.path}`);
  const backupPath=path.join(backup,path.basename(target.path));
  try{await fs.writeFile(backupPath,before,{flag:'wx'});}catch(e){if(e.code!=='EEXIST')throw e;}
  const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(target.path));
  for(const s of target.sheets){
    const sheet=wb.worksheets.getItem(s.name);
    const height=Math.max(s.originalRows,s.matrix.length);
    const matrix=s.matrix.concat(Array.from({length:height-s.matrix.length},()=>Array(s.width).fill(null)));
    sheet.getRangeByIndexes(0,0,height,s.width).values=matrix;
  }
  const blob=await SpreadsheetFile.exportXlsx(wb);
  const temp=target.path+'.integrating.xlsx';await blob.save(temp);
  await fs.rename(temp,target.path);
  // artifact-tool emits a sidecar during export; diagnostics belong outside Unity Assets.
  const sidecar=temp+'.inspect.ndjson';
  try{await fs.rename(sidecar,path.join(root,'output/narrative-integration',path.basename(target.path)+'.inspect.ndjson'));}catch(e){if(e.code!=='ENOENT')throw e;}
  console.log(`Updated source ${target.path}`);
}
console.log('Source Excel updated. Run official Assets/ConfigTables/GenerateAll.bat next.');
