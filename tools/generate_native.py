#!/usr/bin/env python3
"""Generate Steelworks native asset patches from the supplied 1.22.7 survival ZIP.
Requires json5 and Pillow. Output is checked in; Windows build does not require Python.
"""
import sys, json, json5, zipfile, re, fnmatch, copy, shutil, io
from pathlib import Path
from PIL import Image
from native_cupronickel import native_palette
root=Path(__file__).resolve().parents[1]; z=zipfile.ZipFile(sys.argv[1]); files=set(z.namelist())
a=root/'content/assets/steelworks'; game=root/'content/assets/game'; preview=root/'visual-preview/assets/steelworks'
mat={x['key']:x for x in json.loads((root/'design/materials.json').read_text())}; smith=[m for m in mat if m!='castiron']; steels=['nickelsteel','stainlesssteel','chromesteel']; names=dict(zip(mat,['Cupronickel','Cast Iron','Nickel Steel','Stainless Steel','Chrome Steel','Titanium'])); patches=[]; report={'tools':[],'heads':[],'smithing':[],'grid':[],'textures':[],'notes':[]}
def read(path):return json5.loads(z.read('survival/'+path).decode('utf-8-sig'))
def write(path,d):path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(d,indent=2,ensure_ascii=False)+'\n')
def esc(p):return str(p).replace('~','~0').replace('/','~1')
def patch(file,path,value=None,op='add'):
 d={'file':'game:'+file,'op':op,'path':path}
 if op!='remove':d['value']=value
 patches.append(d)
def append(file,path,val):patch(file,path+'/-',val)
def mapadd(file,path,key,val):patch(file,path+'/'+esc(key),val)
lang=json.loads((a/'lang/en.json').read_text()); glang=json.loads((root/'design/materials.json').read_text())
def text(key,name,desc=None):
 lang['game:'+key]=name
 if desc: lang['game:'+key.replace('item-','itemdesc-',1).replace('block-','blockdesc-',1)]=desc
# Native property tiers are not mining tiers: vanilla bronze=2, iron=3, steel=4.
native={'cupronickel':2,'castiron':3,'nickelsteel':4,'stainlesssteel':4,'chromesteel':4,'titanium':5}
melts={'cupronickel':1171,'castiron':1200,'nickelsteel':1500,'stainlesssteel':1530,'chromesteel':1530,'titanium':1668}
work={'cupronickel':600,'castiron':700,'nickelsteel':800,'stainlesssteel':900,'chromesteel':850,'titanium':850}
required={'cupronickel':2,'castiron':2,'nickelsteel':3,'stainlesssteel':3,'chromesteel':3,'titanium':4}
colors={'cupronickel':(181,164,155),'castiron':(77,77,79),'nickelsteel':(139,148,156),'stainlesssteel':(170,186,208),'chromesteel':(96,107,120),'titanium':(228,232,238)}
metalfile='worldproperties/block/metal.json'; existing={v['code']:v for v in read(metalfile)['variants']}; idx={v['code']:i for i,v in enumerate(read(metalfile)['variants'])}
for m in mat:
 if m not in existing:
  v={'code':m,'meltPoint':melts[m],'boilPoint':2800,'density':7200 if m=='castiron' else 7800,'specificHeatCapacity':0.46,'elemental':False,'tier':native[m]}
  if m in steels:v.update(quenchMinTemp=815,quenchMaxTemp=1050,temperMinTemp=550,temperMaxTemp=700)
  append(metalfile,'/variants',v)
 else:
  # Existing melt points and vanilla alloy recipes are not changed. Native tier is changed only to enable the approved anvil progression.
  if existing[m]['tier']!=native[m]:patch(metalfile,f'/variants/{idx[m]}/tier',native[m],'replace')
  if m in steels:
   for k,v in {'quenchMinTemp':815,'quenchMaxTemp':1050,'temperMinTemp':550,'temperMaxTemp':700}.items():patch(metalfile,f'/variants/{idx[m]}/{k}',v,'replace' if k in existing[m] else 'add')
 text('material-'+m,names[m]);
 for prefix in ['ingot','metalbit','workitem','metalplate']:
  text('item-'+prefix+'-'+m,names[m]+{'ingot':' Ingot','metalbit':' Bits','workitem':' Work Item','metalplate':' Plate'}[prefix],f'{names[m]}. '+(f'100 metal units. Melting point {melts[m]} °C. Smithing starts at {work[m]} °C.' if prefix=='ingot' else ''))
# Work items must have matching native properties; keep original copper/nickel/steel unchanged.
for p in ['itemtypes/resource/ingot.json','itemtypes/resource/workitem.json','itemtypes/resource/metalbit.json']:
 d=read(p); stem=d['code']
 for m in mat:
  key=('*-' if stem=='metalbit' else stem+'-')+m
  c=d['combustiblePropsByType']; val={'meltingPoint':melts[m],'meltingDuration':30,'smeltedRatio':20 if stem=='metalbit' else 1,'smeltedStack':{'type':'item','code':'game:ingot-'+m}}
  if key not in c:mapadd(p,'/combustiblePropsByType',key,val)
  if stem=='ingot':
   mapadd(p,'/attributes/workableTemperatureByType','ingot-'+m,work[m]);mapadd(p,'/attributes/requiresAnvilTierByType','ingot-'+m,required[m]);
   if '*-'+m not in d['materialDensityByType']:mapadd(p,'/materialDensityByType','*-'+m,4510 if m=='titanium' else 7200 if m=='castiron' else 7800)
  if stem=='workitem':
   mapadd(p,'/attributesByType','workitem-'+m,{'workableTemperature':work[m],'requiresAnvilTier':required[m]})
  if stem in ['ingot','metalbit'] and '*-'+m in d['attributes']['handbook']['excludeByType']:patch(p,'/attributes/handbook/excludeByType/'+esc('*-'+m),False,'replace')
# Native tool classes, models, and variant identifiers. Select the actual allowed steel variant, then copy per-type maps.
selected=['pickaxe','prospectingpick','axe-metal','shovel','hammer','chisel','saw','knife','hoe','scythe','shears','blade','spear','arrow','cleaver']
heads=['pickaxehead','prospectingpickhead','axehead','shovelhead','hammerhead','sawblade','knifeblade','hoehead','scythehead','bladehead','spearhead','arrowhead','cleaverhead']
cast_tools={'shovel','hammer','hoe','shovelhead','hammerhead','hoehead'}
base_variants={'axe-metal':'axe-felling-steel','blade':'blade-falx-steel','knife':'knife-generic-steel','spear':'spear-generic-steel','bladehead':'bladehead-falx-steel'}
def select(mapping,code):
 if code in mapping:return mapping[code]
 for k,v in mapping.items():
  try:
   if k.startswith('@') and re.search(k[1:],code) or not k.startswith('@') and fnmatch.fnmatchcase(code,k):return v
  except re.error:pass
 return None
def texture_replace(v,m):
 if isinstance(v,str):return re.sub(r'(?<=/)steel(?=\d*(?:$|\.png))',m,v)
 if isinstance(v,dict):return {k:texture_replace(x,m) for k,x in v.items()}
 if isinstance(v,list):return [texture_replace(x,m) for x in v]
 return v
def scale(v,f,integer=False):
 if isinstance(v,dict):return {k:scale(x,f,integer) for k,x in v.items()}
 if isinstance(v,(int,float)) and not isinstance(v,bool):return max(1,round(v*f)) if integer else round(v*f,3)
 return v
def pertype(file,d,base,m,new,path=''):
 for k,v in d.items():
  pp=path+'/'+esc(k)
  if isinstance(v,dict) and k.lower().endswith('bytype'):
   val=select(v,base)
   if val is None:continue
   # Avoid copying generic wildcard-only mappings, except if the resolved steel value differs by material or requires balancing.
   kl=k.lower(); newval=copy.deepcopy(val)
   if kl=='durabilitybytype':newval=scale(val,mat[m]['durabilityMultiplier'],True)
   elif kl=='miningspeedbytype':newval=scale(val,mat[m]['miningMultiplier'])
   elif kl in ('attackpowerbytype','damagebytype'):newval=scale(val,mat[m]['attackMultiplier'] or .6)
   elif kl=='tooltierbytype':newval=mat[m]['tier']
   elif kl=='breakchanceonimpactbytype':newval=round(val/max(.1,mat[m]['durabilityMultiplier']),3)
   elif kl=='behaviorsbytype' and isinstance(newval,list) and m not in steels:newval=[x for x in newval if x.get('name')!='Quenchable']
   elif kl=='texturesbytype':newval=texture_replace(newval,m)
   # Explicit steel shape, animation, class, and texture map reused; exact-match rules take priority.
   if new not in v:mapadd(file,pp,new,newval)
  elif isinstance(v,dict) and k=='attributes':pertype(file,v,base,m,new,pp)
for folder,lst in [('tool',selected),('toolhead',heads)]:
 for n in lst:
  f=f'itemtypes/{folder}/{n}.json';d=read(f);base=base_variants.get(n,d['code']+'-steel'); group=next(i for i,g in enumerate(d['variantgroups']) if 'steel' in g.get('states',[])); use=smith+(['castiron'] if n in cast_tools else [])
  for m in use:
   append(f,f'/variantgroups/{group}/states',m);new=base.removesuffix('steel')+m;pertype(f,d,base,m,new)
   # Vanilla tool tier properties are sometimes constant or absent (non-mining tools); do not overwrite vanilla defaults.
   if folder=='tool' and not any(k.lower()=='tooltierbytype' for k in d):
    # add one new map with vanilla constant/default as fallback; accumulate below outside loop
    pass
   pretty={'axe-metal':'Axe','blade':'Falx','knife':'Knife','spear':'Spear','arrow':'Arrow','prospectingpick':'Prospecting Pick','pickaxehead':'Pickaxe Head','prospectingpickhead':'Prospecting Pick Head','axehead':'Axe Head','shovelhead':'Shovel Head','hammerhead':'Hammer Head','sawblade':'Saw Blade','knifeblade':'Knife Blade','hoehead':'Hoe Head','scythehead':'Scythe Head','bladehead':'Falx Blade','spearhead':'Spear Head','arrowhead':'Arrowhead','cleaverhead':'Cleaver Head'}.get(n,n.capitalize())
   text('item-'+new,names[m]+' '+pretty,('Cast in a tool mold.' if m=='castiron' else f'{names[m]} equipment; mining tier {mat[m]["tier"]} where applicable.')+(' Heat treatment supported.' if m in steels else ''))
   report['tools' if folder=='tool' else 'heads'].append('game:'+new)
  if folder=='tool' and not any(k.lower()=='tooltierbytype' for k in d) and d.get('tool'):
   mapping={base.removesuffix('steel')+m:mat[m]['tier'] for m in use};mapping['*']=d.get('tooltier',0);patch(f,'/tooltierbytype',mapping)
# Arrow entity variants (the item resolves arrow-{material}).
f='entities/nonliving/projectile/arrow.json';d=read(f)
for i,g in enumerate(d.get('variantgroups',[])):
 if 'steel' in g.get('states',[]):
  for m in smith:append(f,f'/variantgroups/{i}/states',m);pertype(f,d,'arrow-steel',m,'arrow-'+m)
# Anvils and forged halves. Use native two-section geometry and native weld process.
for f in ['blocktypes/metal/anvil.json','blocktypes/metal/anvilpart.json']:
 d=read(f);gi=next(i for i,g in enumerate(d['variantgroups']) if 'steel' in g.get('states',[]))
 for m in mat:
  append(f,f'/variantgroups/{gi}/states',m)
  if d['code']=='anvil':
   if m not in ['castiron','cupronickel']:mapadd(f,'/shapeByType','anvil-'+m,{'base':'block/metal/anvil/iron'})
   text('block-anvil-'+m,names[m]+' Anvil',f'Native anvil tier {native[m]}. Forged in two sections'+('; may also be cast in an anvil mold.' if m in ['cupronickel','castiron'] else '.'))
  else:
   for part in ['base','top']:text('block-anvilpart-'+part+'-'+m,names[m]+' Anvil '+part.title())
# Native smithing recipe patterns, only intended equipment. Reuse vanilla recipe instances by appending their allowedVariants.
recipes=['6arrowhead','9arrowhead','anvilbase','anviltop','axe','blade-falx','chisel','cleaver','hammer','hoe','knife','pickaxe','prospectingpick','saw','scythe','shears','shovel','spear-iron','plate','rod']
for n in recipes:
 f='recipes/smithing/'+n+'.json';d=read(f);use=list(mat) if n in ['anvilbase','anviltop'] else smith
 for m in use:
  if m not in d['ingredient']['allowedVariants']:append(f,'/ingredient/allowedVariants',m);report['smithing'].append(n+':'+m)
# Rod variants support cross-mod ingredients; no cast-iron rods by design.
f='itemtypes/part/rod.json';d=read(f)
for m in smith:
 if m not in d['variantgroups'][0]['states']:append(f,'/variantgroups/0/states',m)
 text('item-rod-'+m,names[m]+' Rod')
# Extend explicit allowedVariants in native assembly recipes; unrestricted recipes already resolve new heads.
newtools=set(x.split(':')[1] for x in report['tools']);newheads=set(x.split(':')[1] for x in report['heads'])
for f in sorted(files):
 if not f.startswith('survival/recipes/grid/') or not f.endswith('.json'):continue
 try:d=read(f.removeprefix('survival/'))
 except Exception:continue
 ds=d if isinstance(d,list) else [d]
 for i,r in enumerate(ds):
  if not isinstance(r,dict) or not isinstance(r.get('output'),dict):continue
  out=r['output'].get('code','');ings=r.get('ingredients',{})
  if not isinstance(out,str) or not isinstance(ings,dict):continue
  for key,ig in ings.items():
   if not isinstance(ig,dict) or not isinstance(ig.get('allowedVariants'),list) or 'steel' not in ig['allowedVariants']:continue
   name=ig.get('name');code=ig.get('code','')
   if not name or not isinstance(code,str):continue
   for m in mat:
    output=out.replace('{'+name+'}',m).split(':')[-1];inp=code.replace('*',m).split(':')[-1]
    if output in newtools and inp in newheads and m not in ig['allowedVariants']:
     path=(f'/{i}' if isinstance(d,list) else '')+'/ingredients/'+esc(key)+'/allowedVariants';append(f.removeprefix('survival/'),path,m);report['grid'].append(output)
# New metals are enumerated by vanilla plates/sheets/blocks, so all referenced native textures need coverage.
def recolor(src,m):
 im=Image.open(io.BytesIO(z.read(src))).convert('RGBA'); pix=im.load(); target=colors[m]
 tr,tg,tb=target
 tint_bias=(tr-tb)/255 if tr and tb else 0
 for y in range(im.height):
  for x in range(im.width):
   r,g,b,al=pix[x,y]
   if al==0: continue
   lum=.299*r+.587*g+.114*b
   gain=lum/145
   shine=max(0,(lum-150)/105)
   highlight=0.18+0.18*shine
   nr=tr*gain*(1-highlight)+255*highlight
   ng=tg*gain*(1-highlight)+255*highlight
   nb=tb*gain*(1-highlight)+min(255,245+int(10*tint_bias))*highlight
   if lum>210:
    nr=min(255,nr+18); ng=min(255,ng+18); nb=min(255,nb+22)
   pix[x,y]=(round(nr),round(ng),round(nb),al)
 return im
for m in mat:
 for part,suffixes in [('ingot',['']),('anvil',['']),('plate',['']),('sheet',[str(i) for i in range(1,6)]),('sheet-plain',[str(i) for i in range(1,5)]),('tarnished',[''])]:
  for suffix in suffixes:
   dest=f'textures/block/metal/{part}/{m}{suffix}.png'; src=f'survival/textures/block/metal/{part}/steel{suffix}.png'
   if 'survival/'+dest in files:
    if m=='cupronickel': (game/dest).unlink(missing_ok=True)
    continue
   if src not in files:src='survival/textures/block/metal/ingot/steel.png'
   out=game/dest;out.parent.mkdir(parents=True,exist_ok=True);(native_palette(z,src) if m=='cupronickel' else recolor(src,m)).save(out);report['textures'].append('game:'+dest)
# Use actual native refractory and steel surfaces for the furnace components, not preview ceramic swatches.
for path in (preview/'shapes/block').glob('*.json'):
 if path.name.startswith('sample-'):continue
 shape=json.loads(path.read_text());n=path.stem
 if n.startswith('alloy-container-'):
  tier=int(re.search(r'-t([123])',n).group(1));shape['textures']['ceramic']=f'game:block/clay/refractory/tier{tier}/front1';shape['textures']['metal']='game:block/metal/ingot/steel'
 else:shape['textures']['metal']='game:block/metal/ingot/steel'
 write(a/'shapes/block'/path.name,shape)
# One top hatch controller, two states and four rotations; one fuel hatch; three reusable containers.
for component in ['loading','fuel']:
 for side,rot in [('north',0),('east',270),('south',180),('west',90)]:
  for state in ['closed','open']:
   code=f'{component}-hatch-{state}-{side}';d={'code':code,'class':'SteelworksHatch','drawtype':'json','shape':{'base':f'steelworks:block/{component}-hatch-{state}','rotateY':rot},'textures':{'metal':{'base':'game:block/metal/ingot/steel'}},'blockmaterial':'Metal','sideopaque':{'all':False},'sidesolid':{'all':False},'resistance':3,'attributes':{'steelworksPart':component,'steelworksSide':side,'steelworksOpen':state=='open'},'creativeinventory':{'general':['*']} if state=='closed' and side=='north' else {},'drops':[{'type':'block','code':f'steelworks:{component}-hatch-closed-north'}]}
   if component=='loading':d['entityClass']='SteelworksController'
   write(a/'blocktypes'/f'{code}.json',d);lang['block-'+code]='Steel Loading Hatch' if component=='loading' else 'Steel Fuel Hatch';lang['blockdesc-'+code]='Sneak + right-click with empty hand: structure preview. Open hatch to insert blends; hammer to release cooled ingots.' if component=='loading' else 'Right-click to open. With coke, sneak + right-click to fill physical fuel piles. Ignite the open hatch using a firestarter.'
for tier in (1,2,3):
 for count in range(9):
  # Native dynamic container appearance: only filled cavities represented. Use steel texture initially (per-metal tint is deferred).
  empty=json.loads((a/'shapes/block'/f'alloy-container-t{tier}-empty.json').read_text());filled=json.loads((a/'shapes/block'/f'alloy-container-t{tier}-filled.json').read_text())
  extra=[e for e in filled['elements'] if e['name'] not in {x['name'] for x in empty['elements']}];empty['elements']+=extra[:count]; shape=f'alloy-container-t{tier}-m{count}';write(a/'shapes/block'/f'{shape}.json',empty)
  d={'code':shape,'class':'SteelworksContainer','drawtype':'json','shape':{'base':'steelworks:block/'+shape},'textures':{'ceramic':{'base':f'game:block/clay/refractory/tier{tier}/front1'},'metal':{'base':'game:block/metal/ingot/steel'}},'blockmaterial':'Ceramic','sideopaque':{'all':False},'sidesolid':{'all':False},'resistance':3,'attributes':{'steelworksPart':'container','steelworksTier':tier},'creativeinventory':{'general':['*']} if count==0 else {},'drops':[{'type':'block','code':f'steelworks:alloy-container-t{tier}-m0'}]}
  write(a/'blocktypes'/f'{shape}.json',d);lang['block-'+shape]=f'Tier {tier} Alloy Container';lang['blockdesc-'+shape]=f'{[1500,1650,1800][tier-1]} °C maximum. Eight molds (800 metal units). Reusable. Hammer to loosen the cooled batch.'
# Component recipes use native steel plates/rods, ingots, and fired refractory bricks.
def craft(code,pattern,items,typ='block'):
 rows=pattern.split(',');write(a/'recipes/grid'/f'{code}.json',{'ingredientPattern':pattern,'width':len(rows[0]),'height':len(rows),'ingredients':{k:{'type':'item','code':v} for k,v in items.items()},'output':{'type':typ,'code':'steelworks:'+code,'quantity':1}})
craft('loading-hatch-closed-north','RPR,PIP,PIP',{'R':'game:rod-steel','P':'game:metalplate-steel','I':'game:ingot-steel'})
craft('fuel-hatch-closed-north','RPR,_I_,_P_',{'R':'game:rod-steel','P':'game:metalplate-steel','I':'game:ingot-steel'})
for tier in (1,2,3):craft(f'alloy-container-t{tier}-m0','BBB,B_B,BBB',{'B':f'game:refractorybrick-fired-tier{tier}'})
# Attach the native bellows receiver to the existing refractory wall, no air inlet block.
f='blocktypes/clay/refractorybrick.json';d=read(f);p={'name':'SteelworksAirReceiver'}
if 'behaviors' in d:append(f,'/behaviors',p)
else:patch(f,'/behaviors',[p])
# Native handbook custom guide format from vanilla. Domain-qualified localization keys.
guides=[('getting-started','Steelworks: Getting Started','Requires Expanded Matter². Start with vanilla steel, then build an Advanced Alloy Furnace. Cupronickel uses vanilla smelting. Cast Iron uses Iron Blend in a coke-fired crucible. All blends contain 5 units; 20 blends make one ingot.'),('construction','Steelworks: Furnace Construction','Build a 3×3×6 structure from the Steel Loading Hatch. From bottom to top: refractory foundation; lower coke position with two access openings; upper coke position and front Steel Fuel Hatch; native refractory grating; alloy container; top hatch. Fill remaining positions with refractory brick blocks. Sneak + right-click with an empty hand on the loading hatch to highlight faults. Match the fuel hatch facing to the top hatch. Lowest tier brick, grating or container determines the cap: 1500, 1650 or 1800 °C.'),('operation','Steelworks: Furnace Operation','Open the top hatch. Load 20–160 blends of one alloy. Open the fuel hatch. Sneak + right-click while holding coke to load physical piles, then use a firestarter on the open fuel hatch. Close both hatches. Aim bellows directly at a lower refractory wall, not empty air. Sustained airflow and coke are required. Default hours at temperature: blister steel 8, nickel steel 12, stainless steel 16, chrome steel 20, titanium 24. Heating and cooling add time. Once cooled, sneak + right-click with a hammer to loosen the whole batch. Right-click to retrieve. Server settings can change durations.'),('materials','Steelworks: Metal Production','Iron Blend: 4 crushed iron and 1 coke yields 4. Crucible at 1320 °C makes cast iron; Tier 1 furnace at 1450 °C converts it to blister steel, which is refined on an anvil. Nickel steel: 3 crushed iron, 2 crushed nickel and coke; 1550 °C. Stainless steel: 3 crushed iron, crushed nickel, crushed chromite and coke; 1600 °C. Chrome steel: 4 crushed iron, crushed chromite and coke; 1650 °C. Titanium: powdered titanium dioxide and coke; 1750 °C in Tier 3. Titanium is simplified gameplay refining. Each advanced recipe yields 4 blends.'),('smithing','Steelworks: Smithing and Anvils','Use the vanilla forge, tool heads, smithing interface and assembly recipes. Mining tiers: cupronickel 3; cast iron 4 (limited cast tools); specialty steels 5; titanium 6. Bronze or better anvil for cupronickel and cast iron anvil sections; iron or better for specialty steels; steel or equivalent for titanium. All six anvils can be forged as two sections; cupronickel and cast iron can also be cast in a native anvil mold. Nickel steel, stainless steel and chrome steel use native quenching and tempering; titanium, cupronickel and cast iron do not.'),('maintenance','Steelworks: Furnace Maintenance','Only refractory brick blocks and gratings degrade. Default check every 168 game hours at high temperature. Per check: Tier 1 10%, Tier 2 5%, Tier 3 0.1%. Replace damaged blocks. The container does not degrade. Processing pauses on inadequate heat or invalid structure. Opening hatches causes heat loss. Stored inputs and finished ingots remain on the controller until retrieved.')]
for key,title,body in guides:
 k='steelworks-guide-'+key;write(a/'config/handbook'/f'90-{key}.json',{'pageCode':'steelworks:'+k,'title':'steelworks:'+k+'-title','text':'steelworks:'+k+'-text'});lang[k+'-title']=title;lang[k+'-text']=body
lang.update({'blockhelp-steelworks-release':'Loosen ingots with hammer','blockhelp-steelworks-preview':'Show furnace structure','blockhelp-steelworks-load':'Load alloy blend','blockhelp-steelworks-fuel':'Load coke','blockhelp-steelworks-toggle':'Open / close hatch','blockhelp-steelworks-retrieve':'Retrieve ingots'})
write(a/'lang/en.json',lang);write(a/'patches/native-content.json',patches)
report['patches']=len(patches);report['notes']=['Vanilla internal metal tiers are distinct from displayed tool tiers. Existing stainless steel melting point (1530 °C) retained. New native texture aliases are generated only where missing.','Cast iron supports only hammer, shovel, hoe and anvil forging; tool mold compatibility depends on native crucible and mold checks.','Assembly recipes with no variant restrictions need no patch; explicit restricted recipes extended.','New maps and recipe patches need a game startup test with EM2.']
write(root/'NATIVE_ASSET_REPORT.json',report)
print('Native asset report:',{k:len(v) if isinstance(v,list) else v for k,v in report.items()})
