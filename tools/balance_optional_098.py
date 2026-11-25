"""Apply material balance to existing conditional combat patch maps, from steel baselines."""
import json,re,copy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
P=ROOT/'content/assets/steelworks/patches'
D={'cupronickel':(.8,.75),'nickelsteel':(1.35,1.05),'stainlesssteel':(1.5,1.10),'chromesteel':(1.65,1.15),'titanium':(2,1.25)}
counts={}
for name in ('optional-combat','optional-armory'):
 path=P/(name+'.json'); patches=json.loads(path.read_text()); changes=[]
 def walk(v,context,loc):
  if isinstance(v,list):
   for i,x in enumerate(v):walk(x,context,loc+'/'+str(i))
  elif isinstance(v,dict):
   for k,x in list(v.items()):walk(x,k,loc+'/'+k)
   ctx=context.lower()
   if ctx not in ('durabilitybytype','damagebytype','damagestatsbytype','damagetierbonusbytype','attackpowerbytype'):return
   for k,x in list(v.items()):
    for m,(dur,dmg) in D.items():
     if not re.search(r'(?<![a-z])'+m+r'(?![a-z])',k):continue
     base=v.get(k.replace(m,'steel'))
     if base is None:continue
     new=copy.deepcopy(x)
     if isinstance(base,(int,float)) and not isinstance(base,bool):
      if ctx=='durabilitybytype':new=round(base*dur)
      elif ctx=='damagetierbonusbytype':new=base+(1 if m=='titanium' else -2 if m=='cupronickel' else 0)
      else:new=round(base*dmg,4)
     elif isinstance(base,dict) and isinstance(base.get('Damage'),(int,float)) and isinstance(x,dict):
      new['Damage']=round(base['Damage']*dmg,4)
      if isinstance(base.get('Tier'),(int,float)):new['Tier']=base['Tier']+(1 if m=='titanium' else -2 if m=='cupronickel' else 0)
     if new!=x:v[k]=new;changes.append({'path':loc+'/'+k,'before':x,'after':new})
 for p in patches:walk(p['value'],p['path'].split('/')[-1],p['file']+p['path'])
 path.write_text(json.dumps(patches,indent=2)+'\n');counts[name]=changes
(ROOT/'OPTIONAL_BALANCE_AUDIT_0.9.8.json').write_text(json.dumps(counts,indent=2)+'\n')
print({k:len(v) for k,v in counts.items()})
