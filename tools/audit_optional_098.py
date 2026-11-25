"""Offline optional patch pointer audit; does not simulate other mods' patches."""
import argparse, collections, json, zipfile
from pathlib import Path
import json5
root=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('archives',type=Path);args=p.parse_args()
files=['survival.zip','combatoverhaulfork_0.18.9.zip','crossbowsfork_1.10.1.zip','armoryfork_1.11.4.zip','RecyclingTools-v1.5.0.zip']
assets={}; archives=[]
for filename in files:
 z=zipfile.ZipFile(args.archives/filename); archives.append(z)
 for n in z.namelist():
  if n.startswith('assets/'): key=n[7:].replace('/',':',1)
  elif n.startswith('survival/'): key='game:'+n[9:]
  else: continue
  if key.endswith('.json'):assets[key]=(z,n)
counts=collections.Counter(); issues=[]; loaded={}
# This exact prerequisite creates the array on the vanilla arrow definition.
arrow='game:itemtypes/tool/arrow.json'
z,n=assets[arrow]; loaded[arrow]=json5.loads(z.read(n).decode('utf-8-sig'))
co=archives[1]
prerequisite=json5.loads(co.read('assets/combatoverhaul/patches/weapons/arrow.json').decode('utf-8-sig'))
for op in prerequisite:
 if op['file']=='game:itemtypes/tool/arrow' and op['path']=='/skipVariants' and op['op']=='add':
  loaded[arrow]['skipVariants']=op['value']

for path in sorted((root/'content/assets/steelworks/patches').glob('optional*.json')):
 for i,op in enumerate(json.loads(path.read_text())):
  name=op['file']; key=name if name.endswith('.json') else name+'.json'
  try:
   if key not in loaded:
    z,n=assets[key]; loaded[key]=json5.loads(z.read(n).decode('utf-8-sig'))
   obj=loaded[key]; parts=[s.replace('~1','/').replace('~0','~') for s in op['path'].split('/')[1:]]
   for s in parts[:-1]: obj=obj[int(s)] if isinstance(obj,list) else obj[s]
   last=parts[-1]; action=op['op']; value=op.get('value')
   if isinstance(obj,list):
    if action=='add':
     idx=len(obj) if last=='-' else int(last)
     if not 0<=idx<=len(obj):raise IndexError(idx)
     obj.insert(idx,value)
    elif action=='replace':obj[int(last)]=value
    elif action=='remove':del obj[int(last)]
    else:raise ValueError('unsupported operation '+action)
   else:
    if action=='replace' and last not in obj:raise KeyError(last)
    if action in ('add','replace'):obj[last]=value
    elif action=='remove':del obj[last]
    else:raise ValueError('unsupported operation '+action)
   counts['passed']+=1
  except Exception as e:
   counts['unresolved']+=1;issues.append({'patch':path.name,'index':i,'file':name,'path':op['path'],'reason':str(e)})
report={'scope':'Original archive assets plus explicit Combat Overhaul arrow prerequisite; full engine patch order not simulated','counts':dict(counts),'issues':issues}
(root/'OPTIONAL_POINTER_AUDIT_0.9.8.json').write_text(json.dumps(report,indent=2)+'\n');print(counts);print(collections.Counter((x['file'],x['reason']) for x in issues).most_common(12))
