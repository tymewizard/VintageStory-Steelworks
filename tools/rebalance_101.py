"""One-time 1.0.0 -> 1.0.1 durability rebalance. Exact paired entries only."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
pdir=root/'content/assets/steelworks/patches'
changes=[]
def write(p,d):p.write_text(json.dumps(d,indent=2,ensure_ascii=False)+'\n')
for name in ('native-content','optional-armory','optional-combat'):
 p=pdir/(name+'.json'); ops=json.loads(p.read_text()); by={(x['file'],x['path']):x for x in ops}
 for x in ops:
  path=x['path']; v=x.get('value')
  if 'durability' not in path.lower():continue
  if 'stainlesssteel' in path and isinstance(v,(int,float)):
   other=by[(x['file'],path.replace('stainlesssteel','chromesteel'))]; assert other['value']>=v
   changes.append([name,x['file'],path,v,other['value']]); x['value'],other['value']=other['value'],v
  elif isinstance(v,dict):
   for k in list(v):
    if 'stainlesssteel' not in k or not isinstance(v[k],(int,float)):continue
    other=k.replace('stainlesssteel','chromesteel');assert other in v and v[other]>=v[k]
    changes.append([name,x['file'],path+'/'+k,v[k],v[other]]);v[k],v[other]=v[other],v[k]
 write(p,ops)
p=root/'content/assets/steelworks/config/steelworks/materials.json'; d=json.loads(p.read_text()); factors={'cupronickel':(.8,.75),'castiron':(.9,None),'nickelsteel':(1.35,1.05),'stainlesssteel':(1.65,1.1),'chromesteel':(1.5,1.15),'titanium':(2,1.25)}
for x in d:
 x['durabilityMultiplier'],x['attackMultiplier']=factors[x['key']]
 x['referenceScope']='Nominal equipment balance target relative to equivalent vanilla steel; concrete item patches and server overrides govern actual values.'
 # Existing mining multipliers in the reference were not validated for every tool family.
 x.pop('miningMultiplier',None)
write(p,d)
p=root/'content/assets/steelworks/lang/en.json';d=json.loads(p.read_text());d['steelworks-guide-materials-text']+='\n<strong>Equipment balance</strong>\nNominal durability relative to equivalent vanilla steel: cupronickel 80%, cast iron 90% (limited equipment), nickel steel 135%, chrome steel 150%, stainless steel 165%, titanium 200%. Chrome steel retains higher weapon damage than stainless steel. Actual item values depend on equipment family, optional mods and server overrides.\n';write(p,d)
write(root/'BALANCE_AUDIT_1.0.1.json',{'scope':'Swapped paired durability entries only; damage, mining, armor protection, heat treatment and production unchanged.','pairedChanges':changes,'pairCount':len(changes)})
print('pairs',len(changes))
