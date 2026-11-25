import json,json5,zipfile,re,copy
from pathlib import Path
r=Path(__file__).resolve().parents[1];a=r/'content/assets';z=zipfile.ZipFile('/mnt/data/armoryfork_1.11.4.zip');ms={'cupronickel':'Cupronickel','nickelsteel':'Nickel steel','stainlesssteel':'Stainless steel','chromesteel':'Chrome steel','titanium':'Titanium'}
def load(n):return json5.loads(z.read(n).decode('utf-8-sig'))
def write(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,indent=2,ensure_ascii=False))
# Supply material-dependent models, keeping the original geometry and replacing only metal texture references.
shapes=0
for n in z.namelist():
 if not n.startswith('assets/armory/shapes/') or not n.endswith('.json') or 'compatibility/' in n or 'steel' not in n:continue
 if 'meteoricsteel' in n and n.replace('meteoricsteel','steel') in z.namelist():continue
 if 'meteoricsteel' not in n and not re.search(r'(?<![a-z])steel(?![a-z])',n):continue
 src=load(n)
 for m in ms:
  dest=re.sub(r'(?<![a-z])(?:meteoricsteel|steel)(?![a-z])',m,n.removeprefix('assets/'))
  def tex(v):
   if isinstance(v,dict):return {k:tex(x) for k,x in v.items()}
   if isinstance(v,list):return [tex(x) for x in v]
   if isinstance(v,str) and ('metal/' in v or 'armor/' in v):return re.sub(r'(?<=/)(?:meteoricsteel|steel)(?=$|\.png$)',m,v)
   return v
  write(a/dest,tex(src));shapes+=1
# Exact localized names from existing Armory steel entries, including wildcard entries.
langp=a/'steelworks/lang/en.json';lang=json.loads(langp.read_text()); added=0
for k,v in load('assets/armory/lang/en.json').items():
 if not isinstance(v,str) or not k.startswith(('item-','itemdesc-','block-','entity-')):continue
 if not re.search(r'(?<=-)(?:meteoricsteel|steel)(?=-|$)',k):continue
 for m,label in ms.items():
  nk='armory:'+re.sub(r'(?<=-)(?:meteoricsteel|steel)(?=-|$)',m,k)
  nv=re.sub(r'(?i)\b(?:meteoric steel|steel)\b',lambda x:label if x.group()[0].isupper() else label.lower(),v)
  if nv==v and k.startswith('item-'):nv=f'{v} ({label})'
  if nk not in lang:lang[nk]=nv;added+=1
write(langp,lang)
# Repair shape selectors that were generated from a regex containing multiple metals.
p=a/'steelworks/patches/optional-armory.json';q=json.loads(p.read_text());deps=[{'modid':'armoryfork'},{'modid':'combatoverhaulfork'}]
# use the steel selector for each newly registered variant, before any wildcard; preserve original native keys
fixes=0
for n in z.namelist():
 if not n.startswith('assets/armory/itemtypes/') or '/compatibility/' in n or not n.endswith('.json'):continue
 d=load(n); f=n.replace('assets/armory/','armory:'); shape=d.get('shapeByType');
 if not isinstance(shape,dict):continue
 if not any(x['file']==f and '/states/-' in x['path'] for x in q):continue
 extra={}
 for k,v in shape.items():
  if k.startswith('@') and re.search(r'\bsteel\b',k):
   # for pike heads, use the game's existing material-specific spear shape
   for m in ms:extra['*-'+m]=copy.deepcopy(v)
  elif re.search(r'(?<=-)steel(?=-|$)',k):
   for m in ms:extra[re.sub(r'(?<=-)steel(?=-|$)',m,k)]=copy.deepcopy(v)
 if not extra:continue
 old=next((x for x in q if x['file']==f and x['path']=='/shapeByType'),None)
 if old:old['value']={**extra,**old['value']}
 else:q.append(dict(file=f,op='replace',path='/shapeByType',value={**extra,**shape},dependsOn=deps))
 fixes+=1
write(p,q)
print('model files',shapes,'localization entries',added,'shape selector maps',fixes)
