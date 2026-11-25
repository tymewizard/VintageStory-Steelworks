"""Repair defense balance and enumerate Armory durability targets from supplied assets."""
import argparse, itertools, json, zipfile, fnmatch
from pathlib import Path
import json5
root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('archives',type=Path);args=parser.parse_args()
patchdir=root/'content/assets/steelworks/patches'
native=patchdir/'native-content.json'; patches=json.loads(native.read_text())
factors={'cupronickel':.8,'nickelsteel':1.35,'stainlesssteel':1.5,'chromesteel':1.65,'titanium':2}
armor={p['path'].split('/')[-1]:p for p in patches if p['file']=='game:itemtypes/wearable/seraph/armor.json' and p['path'].startswith('/attributes/protectionModifiersByType/armor-') and isinstance(p['value'],dict)}
changes=[]
for code,p in armor.items():
 metal=code.rsplit('-',1)[-1]
 if metal not in ('stainlesssteel','chromesteel','titanium'):continue
 base=armor[code.rsplit('-',1)[0]+'-nickelsteel']['value']; old=p['value'].copy()
 extra,flat={'stainlesssteel':(.005,1.04),'chromesteel':(.01,1.08),'titanium':(.015,1.10)}[metal]
 p['value']['relativeProtection']=round(min(.985,base['relativeProtection']+extra),4)
 p['value']['flatDamageReduction']=round(base['flatDamageReduction']*flat,4)
 if p['value']!=old:changes.append({'code':code,'before':old,'after':p['value']})
with zipfile.ZipFile(args.archives/'survival.zip') as z:
 shield=json5.loads(z.read('survival/itemtypes/tool/roundshield.json').decode('utf-8-sig'))
 steel=shield['attributes']['durabilityGains']['metal']['steel']
for p in patches:
 if p['file']=='game:itemtypes/tool/roundshield.json' and p['path'].startswith('/attributes/durabilityGains/metal/'):
  metal=p['path'].split('/')[-1]
  if metal not in factors:continue
  new=round(steel*factors[metal]);old=p['value'];p['value']=new
  if old!=new:changes.append({'code':'shield-metal-gain-'+metal,'before':old,'after':new})
native.write_text(json.dumps(patches,indent=2)+'\n')
# Enumerate only concrete Armory item codes whose supplied definitions have a durability map.
optional=json.loads((patchdir/'optional-armory.json').read_text())
path=root/'content/assets/steelworks/config/steelworks/equipment-overrides.json'; targets=json.loads(path.read_text());targets={k:v for k,v in targets.items() if not k.startswith('armory:')}
for code,t in targets.items():
 if code.startswith('game:armor-'):t['Armor']=True
with zipfile.ZipFile(args.archives/'armoryfork_1.11.4.zip') as z:
 for p in optional:
  if p['path'].lower()!='/durabilitybytype':continue
  d=json5.loads(z.read('assets/'+p['file'].replace(':','/')).decode('utf-8-sig'))
  groups=d['variantgroups']; assert all('states' in g for g in groups)
  mi=next(i for i,g in enumerate(groups) if g['code']=='material')
  for metal in factors:
   states=[g['states'] if i!=mi else [metal] for i,g in enumerate(groups)]
   for variant in itertools.product(*states):
    code=d['code']+'-'+'-'.join(variant)
    if not any(fnmatch.fnmatchcase(code,k) and isinstance(v,int) and v>0 for k,v in p['value'].items() if metal in k.split('-')):continue
    targets['armory:'+code]={'Metal':metal,'Durability':True,'AttackPower':False,'MiningSpeed':False,'Armor':'/armor/' in p['file'],'Shield':d['code']=='shield'}
path.write_text(json.dumps(targets,indent=2)+'\n')
report={'changes':changes,'armoryTargets':sum(k.startswith('armory:') for k in targets),'nativeTargets':sum(k.startswith('game:') for k in targets),'shieldSteelReinforcement':steel,'scope':'Static asset values and exact Armory durability target candidates; runtime registration and behavior untested.'}
(root/'DEFENSE_AUDIT_0.9.8.json').write_text(json.dumps(report,indent=2)+'\n');print({k:v for k,v in report.items() if k!='changes'},'changed',len(changes))
