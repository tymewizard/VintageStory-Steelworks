import json,json5,zipfile,copy,sys,re
from pathlib import Path
r=Path(__file__).resolve().parents[1]; a=r/'content/assets'; pp=a/'steelworks/patches/native-content.json'; p=json.loads(pp.read_text()); ms=['nickelsteel','stainlesssteel','chromesteel','titanium']; metals=['cupronickel']+ms
co=zipfile.ZipFile(sys.argv[1]); cb=zipfile.ZipFile(sys.argv[2]); load=lambda z,n:json5.loads(z.read('assets/'+n).decode('utf-8-sig'))
def write(f,d):f.parent.mkdir(parents=True,exist_ok=True);f.write_text(json.dumps(d,indent=2,ensure_ascii=False)+'\n')
# Copy native treatment behavior only where steel supports it; preserve native assembly and storage.
lookup={(x['file'],x['path']):x for x in p}; n=0
for x in p:
 if 'titanium' in x['path'] and '/behaviorsByType/' in x['path']:
  y=lookup.get((x['file'],x['path'].replace('titanium','nickelsteel')))
  if y and isinstance(y.get('value'),list) and any(b.get('name')=='Quenchable' for b in y['value']):x['value']=json.loads(json.dumps(y['value']).replace('nickelsteel','titanium'));n+=1
for x in list(p):
 if 'claycovering' in x['file'] and isinstance(x.get('value'),str) and x['value'].endswith('nickelsteel'):
  y=copy.deepcopy(x);y['value']=y['value'].replace('nickelsteel','titanium');p.append(y)
for k,v in dict(quenchMinTemp=815,quenchMaxTemp=1050,temperMinTemp=550,temperMaxTemp=700).items():p.append(dict(file='game:worldproperties/block/metal.json',op='add',path='/variants/20/'+k,value=v))
write(pp,p)
q=[]
def add(f,path,v,deps,op='add'):q.append(dict(file=f,op=op,path=path,value=v,dependsOn=[dict(modid=m) for m in deps]))
C=['combatoverhaulfork']; B=['crossbowsfork']; esc=lambda s:s.replace('~','~0').replace('/','~1')
def clone(v,m):return json.loads(json.dumps(v).replace('steel',m)) # only for selected leaf values; avoid namespace replacements
# Extend maps with exact variants ahead of wildcard fallback via replacement of full original map (preserve original entries).
def extendmap(d, mlist, code, old):
 out={}
 for m in mlist:
  if old in d:out[code.replace('steel',m)]=clone(d[old],m)
 out.update(d);return out
# recursively extend steel-specific selector dictionaries, not arbitrary strings or animation names.
def recurse(v,mlist):
 if isinstance(v,list):return [recurse(x,mlist) for x in v]
 if not isinstance(v,dict):return v
 out={}
 for k,x in v.items():
  if k.endswith('-steel'):
   for m in mlist:
    val=copy.deepcopy(x)
    if isinstance(val,dict) and 'Tier' in val:val['Tier']=val['Tier']+(1 if m=='titanium' else 0)
    out[k[:-5]+m]=val
 for k,x in v.items():out[k]=recurse(x,mlist)
 return out
# Use fork's own definitions, and remove duplicate vanilla variants conditionally.
for family,base,group,van in [('sword','blade-falx-steel',1,'blade'),('spear','spear-generic-steel',1,'spear'),('arrow','arrow-steel',0,'arrow')]:
 d=load(co,'game/itemtypes/combatoverhaul/'+family+'.json'); f='game:itemtypes/combatoverhaul/'+family+'.json'
 for m in metals:
  code=base.replace('steel',m);add(f,f'/variantgroups/{group}/states/-',m,C)
  add('game:itemtypes/tool/'+van+'.json','/skipVariants/-',code,C) if van!='arrow' else None
 for k in ['attributes','attributesByType','behaviors','behaviorsByType','durabilitybytype','shapeByType','texturesByType']:
  if k in d:
   v=recurse(d[k],metals)
   if k=='behaviorsByType':
    for m in metals:
     key='*-'+m
     if key in v:
      v[key]=recurse(v[key],metals)
      for b in v[key]:
       if b.get('name')=='CombatOverhaul:TexturesFromAttributes':
        for t in b['properties']['Textures']:t['Default']='game:block/metal/ingot/'+m
   if k=='shapeByType' and family=='sword':
    v={**{base.replace('steel',m):{'base':'game:item/tool/blade/falx/'+m} for m in metals},**v}
   if k=='texturesByType' and family=='spear':
    steel=next(x for key,x in d[k].items() if 'steel)' in key);v={**{base.replace('steel',m):copy.deepcopy(steel) for m in metals},**v}
   if v!=d[k]:add(f,'/'+k,v,C,'replace')
 if family=='sword':
  for m in metals:
   # Native fork model is material-specific; adapt its reference textures without redistributing its original shape.
   src=load(co,'combatoverhaul/shapes/weapons/falx/falx-steel.json');write(a/'game/shapes/item/tool/blade/falx'/f'{m}.json',clone(src,m))
 if family=='spear':
  for m in metals:
   for sub in ['', 'attached/left/','attached/right/']:
    src=load(co,'combatoverhaul/shapes/weapons/spears/'+sub+'spear-steel.json');write(a/'combatoverhaul/shapes/weapons/spears'/sub/f'spear-{m}.json',clone(src,m))
 if 'creativeinventory' in d:add(f,'/creativeinventory/steelworks',[base.replace('steel',m) for m in metals],C)
for family,group in [('spear',1),('arrow',0)]:
 d=load(co,'combatoverhaul/entities/'+family+'.json');f='combatoverhaul:entities/'+family+'.json'
 for m in metals:add(f,f'/variantgroups/{group}/states/-',m,C)
 for k in ['shapeByType','texturesByType']:
  v=recurse(d['client'][k],metals)
  if family=='spear' and k=='texturesByType':
   for key,x in d['client'][k].items():
    if 'steel)' in key:v={**{'spear-generic-'+m:copy.deepcopy(x) for m in metals},**v};break
  if v!=d['client'][k]:add(f,'/client/'+k,v,C,'replace')
# Crossbow ammunition and corresponding projectile entities; preserve fork's steel stats and native recipe format.
for kind in ['itemtypes','entities']:
 d=load(cb,'maltiezcrossbows/'+kind+'/bolt.json');f='maltiezcrossbows:'+kind+'/bolt.json'
 for m in metals:add(f,'/variantgroups/0/states/-',m,B)
 if kind=='itemtypes':
  for k in ['shapeByType','behaviors']:
   v=recurse(d[k],metals);add(f,'/'+k,v,B,'replace')
  add(f,'/creativeinventory/steelworks',['bolt-'+m for m in metals],B)
 else:add(f,'/client/shapeByType',recurse(d['client']['shapeByType'],metals),B,'replace')
recipes=load(cb,'maltiezcrossbows/recipes/grid/bolt.json');old=next(x for x in recipes if x['output']['code'].endswith('-steel'))
for m in metals:add('maltiezcrossbows:recipes/grid/bolt.json','/-',clone(old,m),B)
write(a/'steelworks/patches/optional-combat.json',q)
lang=a/'steelworks/lang/en.json';l=json.loads(lang.read_text());
for m in metals:l['maltiezcrossbows:item-bolt-'+m]={'cupronickel':'Cupronickel','nickelsteel':'Nickel Steel','stainlesssteel':'Stainless Steel','chromesteel':'Chrome Steel','titanium':'Titanium'}[m]+' Bolt'
for k,v in list(l.items()):
 if isinstance(v,str):l[k]=v.replace('titanium, cupronickel and cast iron do not','titanium also supports heat treatment; cupronickel and cast iron do not').replace('Cupronickel, cast iron and titanium','Cupronickel and cast iron')
write(lang,l)
print('Titanium treatment behavior entries',n,'conditional operations',len(q))
