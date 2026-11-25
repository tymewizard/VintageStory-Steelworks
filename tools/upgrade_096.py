"""Incremental upgrade of the 0.9.5 source tree using the exact 1.22.7 survival archive.
Run once on a clean 0.9.5 tree; generated assets are packaged, no Python needed to build.
"""
import copy, fnmatch, io, json, json5, re, sys, zipfile, collections
from pathlib import Path
from PIL import Image
from native_cupronickel import native_palette
root=Path(__file__).resolve().parents[1]; a=root/'content/assets/steelworks'; game=root/'content/assets/game'; z=zipfile.ZipFile(sys.argv[1]); names=set(z.namelist())
pf=a/'patches/native-content.json'; patches=json.loads(pf.read_text()); initial=len(patches); lp=a/'lang/en.json'; lang=json.loads(lp.read_text())
mat={x['key']:x for x in json.loads((root/'design/materials.json').read_text())}; smith=[m for m in mat if m!='castiron']; steels=['nickelsteel','stainlesssteel','chromesteel']; pretty=dict(zip(mat,['Cupronickel','Cast Iron','Nickel Steel','Stainless Steel','Chrome Steel','Titanium'])); tier={'cupronickel':2,'nickelsteel':4,'stainlesssteel':4,'chromesteel':4,'titanium':5}; melts={'cupronickel':1171,'castiron':1200,'nickelsteel':1500,'stainlesssteel':1530,'chromesteel':1530,'titanium':1668}; colors={'cupronickel':(181,164,155),'nickelsteel':(139,148,156),'stainlesssteel':(170,186,208),'chromesteel':(96,107,120),'titanium':(228,232,238)}
base={}; state={}; bymaps=collections.defaultdict(set); tabcodes=collections.defaultdict(set); generated=[]; report={'new_equipment':[],'armor':[],'recipes':[],'reordered_maps':[],'heat_treatment':[],'shape_assets':[],'texture_assets':[]}
def write(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,indent=2,ensure_ascii=False)+'\n')
def raw(f):return json5.loads(z.read('survival/'+f).decode('utf-8-sig'))
def get(f):
 if f not in state:base[f]=raw(f);state[f]=copy.deepcopy(base[f])
 return state[f]
def esc(s):return s.replace('~','~0').replace('/','~1')
def split(s):return [k.replace('~1','/').replace('~0','~') for k in s.split('/')[1:]]
def at(d,path):
 for k in split(path):d=d[int(k)] if isinstance(d,list) else d[k]
 return d
def apply(p):
 f=p['file'].split(':',1)[1];d=get(f);keys=split(p['path']);parent=at(d,'/'+ '/'.join(esc(k) for k in keys[:-1])) if len(keys)>1 else d;k=keys[-1];op=p['op']
 if op=='move':
  fr=split(p['fromPath']); fp=at(d,'/'+ '/'.join(esc(k) for k in fr[:-1])) if len(fr)>1 else d;v=fp.pop(fr[-1]);parent[k]=v;return
 if op=='remove':
  if isinstance(parent,list):del parent[int(k)]
  else:del parent[k]
  return
 v=copy.deepcopy(p['value'])
 if isinstance(parent,list):
  if op=='add':parent.insert(len(parent) if k=='-' else int(k),v)
  else:parent[int(k)]=v
 else:
  if op=='replace':assert k in parent,(f,p)
  parent[k]=v
 if len(keys)>1 and keys[-2].lower().endswith('bytype') and isinstance(parent,dict) and any(k.endswith('-'+m) for m in mat) and not k.startswith(('*','@')):bymaps[(f,'/'+ '/'.join(esc(x) for x in keys[:-1]))].add(k)
def patch(f,path,v=None,op='add',frompath=None):
 p={'file':'game:'+f,'op':op,'path':path}
 if op=='move':p['fromPath']=frompath
 elif op!='remove':p['value']=v
 apply(p);patches.append(p)
def append(f,path,v):
 if v not in at(get(f),path):patch(f,path+'/-',v)
def put(f,path,v):patch(f,path,v)
for p in patches:apply(p)
def matches(pat,code):
 if pat.startswith('@'):
  try:return re.fullmatch(pat[1:],code) is not None
  except re.error:return False
 return fnmatch.fnmatchcase(code,pat)
def select(mapping,code):
 for k,v in mapping.items():
  if matches(k,code):return copy.deepcopy(v)
 return None
def scale(v,f,integer=False):
 if isinstance(v,dict):return {k:scale(x,f,integer) for k,x in v.items()}
 if isinstance(v,(float,int)) and not isinstance(v,bool):return max(1,round(v*f)) if integer else round(v*f,3)
 return v
def replace_metal(x,m):
 if isinstance(x,str):return re.sub(r'(?<=/)steel(?=\d*(?:$|\.png))',m,x)
 if isinstance(x,dict):return {k:replace_metal(v,m) for k,v in x.items()}
 if isinstance(x,list):return [replace_metal(v,m) for v in x]
 return x
def extend_groups(f,materials=smith):
 d=get(f);g=next(i for i,v in enumerate(d['variantgroups']) if 'steel' in v.get('states',[]))
 for m in materials:append(f,f'/variantgroups/{g}/states',m)
def clone_bytype(f,tree,old,new,m,path='',armor=False):
 for k,v in tree.items():
  p=path+'/'+esc(k)
  if isinstance(v,dict) and k.lower().endswith('bytype'):
   val=select(v,old)
   if val is None:continue
   kl=k.lower()
   if kl=='durabilitybytype':val=scale(val,mat[m]['durabilityMultiplier'],True)
   elif kl=='miningspeedbytype':val=scale(val,mat[m]['miningMultiplier'])
   elif kl in ('attackpowerbytype','damagebytype'):val=scale(val,mat[m]['attackMultiplier'] or .6)
   elif kl=='tooltierbytype':val=mat[m]['tier']
   elif kl=='behaviorsbytype' and isinstance(val,list) and m not in steels:val=[x for x in val if x.get('name')!='Quenchable']
   elif kl=='texturesbytype':val=replace_metal(val,m)
   elif kl=='protectionmodifiersbytype' and armor:
    if m=='cupronickel':val=select(v,old.removesuffix('steel')+'blackbronze') or val
    val['protectionTier']=tier[m]
    if m=='titanium':val['relativeProtection']=min(.95,round(val['relativeProtection']+.015,3));val['flatDamageReduction']=round(val['flatDamageReduction']*1.1,3)
   elif kl=='statmodifiersbytype' and armor and m=='cupronickel':val=select(v,old.removesuffix('steel')+'blackbronze') or val
   # Native ByType dictionaries with wildcard fallbacks must be reordered below.
   put(f,p+'/'+esc(new),val)
  elif isinstance(v,dict) and k in ('attributes','attachableToEntity','client','server'):clone_bytype(f,v,old,new,m,p,armor)
def name(f,code,display):
 lang['game:'+('block-' if f.startswith('blocktypes/') else 'item-')+code]=display;tabcodes[f].add(code)
def add_equipment(f,display,basecode=None):
 d=copy.deepcopy(get(f));extend_groups(f);old=basecode or d['code']+'-steel'
 for m in smith:
  new=old.removesuffix('steel')+m;clone_bytype(f,d,old,new,m);name(f,new,pretty[m]+' '+display);report['new_equipment'].append('game:'+new)
# Fix model paths for metal-specific native shape filenames. Use the supplied vanilla steel geometry and correct actual face texture keys.
def newshape(src,dest,m):
 d=raw('shapes/'+src+'.json');d['textures']=replace_metal(d.get('textures',{}),m);out=game/'shapes'/ (dest+'.json');write(out,d);report['shape_assets'].append(str(out.relative_to(game)))
for m in smith:
 for src,dest in [('item/tool/spear/metal-steel','item/tool/spear/metal-'+m),('item/tool/blade/falx/steel','item/tool/blade/falx/'+m),('item/tool/wrench/steel1','item/tool/wrench/'+m+'1')]:newshape(src,dest,m)
 for f,code in [('itemtypes/tool/spear.json','spear-generic-'+m),('itemtypes/toolhead/spearhead.json','spearhead-'+m)]:
  d=get(f);p='/texturesByType/'+code;val=copy.deepcopy(at(d,p));val['steel']={'base':'block/metal/ingot/'+m};put(f,p,val)
 f='itemtypes/toolhead/bladehead.json';code='bladehead-falx-'+m;p='/texturesByType/'+code;val=copy.deepcopy(at(get(f),p));val.update({'steel':{'base':'block/metal/ingot/'+m},'handguard':{'base':'block/transparent'},'rivet':{'base':'block/transparent'}});put(f,p,val)
 # Explicit complete falx geometry is generic, with per-metal texture overrides already in the patch.
 put('itemtypes/tool/blade.json','/shapeByType/blade-falx-'+m,{'base':'item/tool/blade/falx'})
# The thrown spear uses its own native entity registration, not just the held item.
f='entities/nonliving/projectile/spear.json';d=copy.deepcopy(get(f));extend_groups(f)
for m in smith:
 code='spear-generic-'+m;clone_bytype(f,d,'spear-generic-steel',code,m);p='/client/texturesByType/'+code;v=copy.deepcopy(at(get(f),p));v['steel']={'base':'block/metal/ingot/'+m};put(f,p,v)
# Native arrow entities were already registered in 0.9.5. Keep vanilla projectile renderer and texture substitution; no duplicate registrations.
for f,label,code in [('itemtypes/tool/tongsmetal.json','Metal Tongs','tongsmetal-standard-steel'),('itemtypes/tool/crowbar.json','Crowbar',None),('itemtypes/tool/wrench.json','Wrench',None),('itemtypes/part/helvehammer.json','Helve Hammer',None),('itemtypes/part/helvehammerhead.json','Helve Hammer Head',None),('itemtypes/part/boss.json','Shield Boss',None),('itemtypes/part/hoop.json','Hoop',None),('itemtypes/resource/metalchain.json','Chain',None),('itemtypes/resource/metalscale.json','Scales',None)]:add_equipment(f,label,code)
# Wrench actual mesh uses #steel rather than #metal; the copied shape supplies it, also expose direct texture override.
for m in smith:
 f='itemtypes/tool/wrench.json';p='/texturesByType/wrench-'+m;v=copy.deepcopy(at(get(f),p));v['steel']={'base':'block/metal/ingot/'+m};put(f,p,v)
# Native smithing patterns and grid assembly/repair recipes, no replacement recipes needed.
for stem in ['tongs','crowbar','wrench','helvehammer','boss','hoop','chain','scale']:
 f='recipes/smithing/'+stem+'.json'
 for m in smith:append(f,'/ingredient/allowedVariants',m);report['recipes'].append(stem+':'+m)
# Native shield attributes use internal stack variants; retain the original shield item codes and all wood/color combinations.
f='itemtypes/tool/roundshield.json'
for c in ['woodmetal','woodmetalleather','metal']:
 for m in smith:append(f,'/attributes/variantGroupsByType/shield-'+c+'/metal',m)
for m in smith:
 put(f,'/attributes/durabilityGains/metal/'+m,round(120*mat[m]['durabilityMultiplier']))
# Explicit Steelworks tab stacks for native round shields. Their actual item codes remain unchanged.
shieldstacks=[]
for m in smith:
 for c,attrs in [('woodmetal',{'wood':'oak','deco':'none'}),('woodmetalleather',{'wood':'generic','color':'brown','deco':'none'}),('metal',{'deco':'none'})]:
  shieldstacks.append({'type':'item','code':'game:shield-'+c,'attributes':{'metal':m,**attrs}})
# Native class may generate its own creative stacks; C# finalizer below supplements if necessary, using documented API after load.
put(f,'/creativeInventoryStacks',[{'tabs':['steelworks'],'stacks':shieldstacks}])
# Native armor metal variants: use steel model geometry (including head shapes) and native wearable behavior.
f='itemtypes/wearable/seraph/armor.json';d=copy.deepcopy(get(f));extend_groups(f)
for m in smith:
 for construction in ['brigandine','chain','scale','plate']:
  for part,label in [('head','Helmet'),('body','Body Armor'),('legs','Leg Armor')]:
   old=f'armor-{part}-{construction}-steel';code=old.removesuffix('steel')+m
   clone_bytype(f,d,old,code,m,armor=True)
   sh=select(d['shapeByType'],old);assert sh,(old,'shape')
   sh['base']=sh['base'].replace('{construction}',construction).replace('{bodypart}',part).replace('{material}','steel');assert 'survival/shapes/'+sh['base']+'.json' in names,(old,sh)
   put(f,'/shapeByType/'+code,sh)
   # Armor texture path is also used by the native attachment behavior. Preserve transparent seraph skin.
   put(f,'/texturesByType/'+code,{'seraph':{'base':'block/transparent'},'material':{'base':f'entity/humanoid/serapharmor/{construction}/{m}'}})
   name(f,code,pretty[m]+' '+construction.title()+' '+label);report['armor'].append('game:'+code)
# Recolor vanilla native armor texture atlas, preserving alpha and pattern details, not a solid-color replacement.
def recolor_texture(src,dest,m):
 if 'survival/'+dest in names:
  if m=='cupronickel': (game/dest).unlink(missing_ok=True)
  return
 if m=='cupronickel':
  out=game/dest;out.parent.mkdir(parents=True,exist_ok=True);native_palette(z,'survival/'+src,True).save(out);report['texture_assets'].append(dest);return
 im=Image.open(io.BytesIO(z.read('survival/'+src))).convert('RGBA');p=im.load();tr,tg,tb=colors[m]
 for y in range(im.height):
  for x in range(im.width):
   r,g,b,alpha=p[x,y]
   if not alpha:continue
   # Preserve colored leather/cloth elements; recolor mostly neutral metal texels.
   if max(r,g,b)-min(r,g,b)>40:continue
   lum=.299*r+.587*g+.114*b;factor=lum/145;highlight=.15+.2*max(0,(lum-150)/105)
   p[x,y]=tuple(round(min(255,max(0,c*factor*(1-highlight)+255*highlight))) for c in (tr,tg,tb))+(alpha,)
 out=game/dest;out.parent.mkdir(parents=True,exist_ok=True);im.save(out);report['texture_assets'].append(dest)
for m in smith:
 for c in ['brigandine','chain','scale','plate']:
  recolor_texture(f'textures/entity/humanoid/serapharmor/{c}/steel.png',f'textures/entity/humanoid/serapharmor/{c}/{m}.png',m)
 recolor_texture('textures/item/tool/shield/metal/steel.png',f'textures/item/tool/shield/metal/{m}.png',m)
# Native resources need density, remelting and handbook visibility. Copy steel yield/ratio, use actual material temperature and ingot.
for stem in ['metalplate','metalchain','metalscale']:
 f='itemtypes/resource/'+stem+'.json';d=get(f)
 for m in smith:
  code=stem+'-'+m
  if code not in d['combustiblePropsByType']:
   v=copy.deepcopy(d['combustiblePropsByType'].get(stem+'-steel',d['combustiblePropsByType'][stem+'-iron']));v['meltingPoint']=melts[m];v['smeltedStack']['code']='game:ingot-'+m;put(f,'/combustiblePropsByType/'+code,v)
  if code not in d['materialDensityByType'] and '*-'+m not in d['materialDensityByType']:
   put(f,'/materialDensityByType/'+code,4510 if m=='titanium' else 8940 if m=='cupronickel' else 7800)
  exclude=d['attributes']['handbook'].get('excludeByType',{})
  if '*-'+m in exclude:put(f,'/attributes/handbook/excludeByType/*-'+m,False)
  tabcodes[f].add(code)
# All appropriate assembly, armor, repair, and shield ingredient allowlists; limit to known output families.
files=['recipes/grid/helvehammer.json','recipes/grid/tool/roundshield.json']+[f'recipes/grid/{directory}/{c}.json' for directory in ('armor','armorrepair') for c in ('brigandine','chain','scale','plate')]
for f in files:
 d=get(f);rows=d if isinstance(d,list) else [d]
 for i,r in enumerate(rows):
  for key,ig in r.get('ingredients',{}).items():
   if isinstance(ig,dict) and 'steel' in ig.get('allowedVariants',[]) and any(ig.get('code','').startswith(x) for x in ('helvehammerhead-','boss-','hoop-','metalplate-','metalchain-','metalscale-','armor-')):
    for m in smith:append(f,(f'/{i}' if isinstance(d,list) else '')+'/ingredients/'+esc(key)+'/allowedVariants',m)
# Clay covering, native Quenchable behavior, and vanilla buff transfer. Extend only those steel items vanilla explicitly supports.
f='recipes/grid/claycovering.json'; d=get(f); allowed=list(d['ingredients']['T']['allowedVariants'])
for old in allowed:
 if not old.endswith('-steel'):continue
 for m in steels:
  code=old.removesuffix('steel')+m
  # All these families are present in the generated assets, including newly added tongs/crowbars/wrenches.
  append(f,'/ingredients/T/allowedVariants',code);report['heat_treatment'].append(code)
# Register creative tab for all canonical Steelworks material items and native equipment, not vanilla steel/copper items.
for p in patches:
 f=p['file'].split(':',1)[1]
 if f.startswith(('itemtypes/','blocktypes/')) and '/variantgroups/' in p['path'] and p.get('value') in mat:
  pass
for f,d in list(state.items()):
 if not f.startswith(('itemtypes/','blocktypes/')) or f.endswith('/roundshield.json'):continue
 if not isinstance(d,dict) or 'variantgroups' not in d:continue
 # Restrict to generated material variants in these known files. Use group cross product, native allowed/skip selectors.
 import itertools
 groups=d['variantgroups']; options=[]
 for g in groups:
  vals=g.get('states',[])
  if g.get('loadFromProperties')=='block/metal':vals=list(dict.fromkeys(vals+list(mat)))
  options.append(vals)
 if not all(options):continue
 for combo in itertools.product(*options):
  if not any(x in mat for x in combo):continue
  code=d['code']+'-'+'-'.join(combo)
  if d.get('allowedVariants') and not any(matches(k,code) for k in d['allowedVariants']):continue
  if any(matches(k,code) for k in d.get('skipVariants',[])):continue
  # Do not add empty unconfigured native workitems; player receives these from smithing, not creative.
  if d['code']=='workitem':continue
  tabcodes[f].add(code)
# Add creative tab without losing vanilla tabs. Handle the blade's native by-type creative dictionary separately.
for f,codes in tabcodes.items():
 d=get(f)
 if 'creativeinventory' in d:put(f,'/creativeinventory/steelworks',sorted(codes))
 elif 'creativeinventoryByType' in d:
  oldmap=d['creativeinventoryByType']
  for code in sorted(codes):
   v=select(oldmap,code) or select(oldmap,code.rsplit('-',1)[0]+'-steel') or {'general':['*'],'items':['*'],'tools':['*']}
   v['steelworks']=['*'];put(f,'/creativeinventoryByType/'+code,v)
 elif 'creativeInventory' in d:put(f,'/creativeInventory/steelworks',sorted(codes))
 else:put(f,'/creativeinventory',{'steelworks':sorted(codes)})
lang['game:tabname-steelworks']='Steelworks'
# Remove generated boilerplate descriptions; native classes provide native numerical stats and heat-treatment hints.
for k,v in list(lang.items()):
 if k.startswith('game:itemdesc-') and (' equipment; mining tier ' in v or v=='Cast in a tool mold.'):del lang[k]
# Restore first-match semantics without replacing vanilla or other mods' map values. Move the original shadowing suffix to the end.
# This preserves the original relative order and values, and does not overwrite the whole ByType object.
for (f,path),keys in sorted(bymaps.items()):
 cur=at(get(f),path);original=at(base[f],path);oldkeys=list(original)
 if not isinstance(original,dict):continue
 keylist=list(cur)
 # Only new exact material entries that would otherwise be shadowed by an original selector.
 indices=[i for i,k in enumerate(oldkeys) if any(matches(k,code) and keylist.index(k)<keylist.index(code) for code in keys if code in cur and k in cur and k!=code)]
 if not indices:continue
 for k in oldkeys[min(indices):]:
  if k not in cur:continue
  origin=path+'/'+esc(k);tmp=path+'/__steelworks_096_move__';patch(f,tmp,op='move',frompath=origin);patch(f,origin,op='move',frompath=tmp)
 report['reordered_maps'].append(f+path)
# Assert the exact heat-treatment selector now wins, for each compatible head or directly forged tool.
for code in report['heat_treatment']:
 candidates=[f for f,d in state.items() if f.startswith('itemtypes/') and isinstance(d,dict) and code.startswith(d.get('code','\0')+'-') and 'behaviorsByType' in d]
 assert any(any(b.get('name')=='Quenchable' for b in select(state[f]['behaviorsByType'],code) or []) for f in candidates),code
# Ensure every existing model path with steel-specific filename has a generated asset. Native model values are resolved with placeholders.
for f,d in state.items():
 if f.startswith(('itemtypes/','blocktypes/','entities/')) and isinstance(d,dict):
  assert '__steelworks_096_move__' not in json.dumps(d)
write(pf,patches);write(lp,lang)
# Non-overlapping intake geometry. Brick body cutout is 8x8, exactly matches pipe exterior. Internal shared faces omitted.
for p in (a/'shapes/block').glob('air-intake-*.json'):
 d=json.loads(p.read_text());w=d['textureWidth']/16;h=d['textureHeight']/16
 def element(n,lo,hi,faces,tex):
  x,y,z=lo;X,Y,Z=hi;uv={'north':(16-X,16-Y,16-x,16-y),'south':(x,16-Y,X,16-y),'east':(16-Z,16-Y,16-z,16-y),'west':(z,16-Y,Z,16-y),'up':(x,z,X,Z),'down':(x,16-Z,X,16-z)}
  return {'name':n,'from':lo,'to':hi,'faces':{f:{'texture':'#'+tex,'uv':[u*w,v*h,U*w,V*h]} for f in faces for u,v,U,V in [uv[f]]}}
 ns=['north','south'];d['elements']=[element('left',[0,0,0],[4,16,16],ns+['west','up','down'],'brick'),element('right',[12,0,0],[16,16,16],ns+['east','up','down'],'brick'),element('top',[4,12,0],[12,16,16],ns+['up'],'brick'),element('bottom',[4,0,0],[12,4,16],ns+['down'],'brick'),element('pipe-left',[4,6,0],[6,10,16],ns+['east'],'pipe'),element('pipe-right',[10,6,0],[12,10,16],ns+['west'],'pipe'),element('pipe-top',[4,10,0],[12,12,16],ns,'pipe'),element('pipe-bottom',[4,4,0],[12,6,16],ns,'pipe'),element('pipe-inner-ceiling',[6,10,0],[10,12,16],['down'],'pipe'),element('pipe-inner-floor',[6,4,0],[10,6,16],['up'],'pipe')]
 # Extra inner-only elements have no coplanar rendered faces with strips. Avoid coincident geometry entirely by using the same strip split into three widths.
 d['elements']=d['elements'][:6]+[element('pipe-top-left',[4,10,0],[6,12,16],ns,'pipe'),element('pipe-top-center',[6,10,0],[10,12,16],ns+['down'],'pipe'),element('pipe-top-right',[10,10,0],[12,12,16],ns,'pipe'),element('pipe-bottom-left',[4,4,0],[6,6,16],ns,'pipe'),element('pipe-bottom-center',[6,4,0],[10,6,16],ns+['up'],'pipe'),element('pipe-bottom-right',[10,4,0],[12,6,16],ns,'pipe')]
 write(p,d)
# All normal player-facing Steelworks blocks/items receive their custom tab; hidden state/compatibility blocks remain hidden.
for directory in ('blocktypes','itemtypes'):
 for p in (a/directory).rglob('*.json'):
  d=json.loads(p.read_text());inv=d.get('creativeinventory')
  if isinstance(inv,dict) and any(inv.values()):inv['steelworks']=['*'];write(p,d)
# Native cursor text: show preview hints on intake only; keep normal hatch toggle/load/release actions.
p=root/'native/SteelworksMod.cs';s=p.read_text();s=re.sub(r'        if\(NativeParts\.Is\(this,"loading-hatch"\)\)\{hints\.Add\(new\(\)\{ActionLangCode="steelworks:help-preview-hide"[^\n]*\n','',s)
s=s.replace('+(NativeParts.Is(this,"fuel-hatch")?"\\nOpen to load coke and ignite.":"\\nSneak + empty hand: structure preview.")','+(NativeParts.Is(this,"fuel-hatch")?"\\nOpen to load coke and ignite.":"")')
# Retain existing preview hotkey behavior. Use the native modifier array for the intake hint.
s=s.replace('ActionLangCode="steelworks:help-preview-show", HotKeyCode="shift",','ActionLangCode="steelworks:help-preview-show", HotKeyCodes=new[]{"shift"},')
p.write_text(s)
# Native-style handbook VTML sections, links, and inline item icons. No custom GUI.
lang=json.loads(lp.read_text())
for k in list(lang):
 if k.startswith('blockdesc-loading-hatch-'):lang[k]='Open to insert blends and retrieve finished ingots.'
def link(label,page):return f'<a href="handbook://{page}">{label}</a>'
def search(label):return f'<a href="handbooksearch://{label}">{label}</a>'
def section(h,t):return '<strong>'+h+'</strong>\n'+t+'\n\n'
def icon(code,type='block'):return f'<itemstack type="{type}" code="{code}" rsize="1" floattype="inline">'
links={k:link(v,'steelworks:steelworks-guide-'+k) for k,v in [('getting-started','Getting started'),('construction','Furnace construction'),('operation','Furnace operation'),('materials','Metal production'),('smithing','Smithing and heat treatment'),('maintenance','Furnace maintenance')]}
lang['advanced-steelmaking-text']='<i>From crushed ore to advanced alloys</i>\n\n'+section('Getting started','Steelworks requires Expanded Matter². Begin with vanilla steelmaking, then construct an advanced alloy furnace to produce specialty steels and titanium.')+section('Construction',icon('steelworks:air-intake-good-t3-north')+' '+icon('steelworks:loading-hatch-closed-north')+' '+icon('steelworks:alloy-container-t3-m0')+'\nBuild a 3×3×6 refractory furnace, using an air intake, fuel hatch, grating, alloy container and loading hatch. '+links['construction']+' explains each layer.')+section('Operation','Use coke as fuel and crushed coal in blends. Preheat the chamber, deliver airflow through a bellows, and load up to 160 blends. See '+links['operation']+'.')+section('Metal production',links['materials']+' covers the recipes, temperature limits and processing durations.')+section('Working the metal',links['smithing']+' explains forging, casting, quenching, tempering, armor and shields.')+section('Maintenance',links['maintenance']+' explains refractory wear and troubleshooting.')
lang['steelworks-guide-getting-started-text']=section('Getting started','Begin with '+search('steel ingot')+' and Expanded Matter² crushed ore. A '+search('pulverizer')+' is needed for many blend ingredients. The advanced furnace produces blister steel and specialty metals in eight reusable cavities.')+section('Earlier metals','Cupronickel uses normal alloy smelting. Iron Blend also produces cast iron in a crucible. Cast iron tools are limited to the previously supported cast tools and anvil.')+section('Next steps',links['construction']+' • '+links['materials'])
lang['steelworks-guide-construction-text']=section('Construction','Build a 3×3×6 structure using intact refractory bricks. The following layers are ordered from bottom to top.')+section('Layer 1 — Foundation','A complete 3×3 refractory brick base.')+section('Layer 2 — Lower fuel chamber',icon('steelworks:air-intake-good-t1-north')+' Place an outward-facing refractory air intake in at least one side center, with coke in the center. Fill the remaining wall positions with bricks.')+section('Layer 3 — Upper fuel chamber',icon('steelworks:fuel-hatch-closed-north')+' Place a steel fuel hatch at any side center. Coke occupies the center.')+section('Layer 4 — Grating',icon('game:refractorybrickgrating-good-tier1')+' Place native refractory grating in the center; surround it with bricks.')+section('Layer 5 — Alloy container',icon('steelworks:alloy-container-t1-m0')+' Place the reusable alloy container in the center; surround it with bricks.')+section('Layer 6 — Loading hatch',icon('steelworks:loading-hatch-closed-north')+' Place the loading hatch in the center; surround it with bricks.')+section('Structure preview','On the intake, use <hk>shift</hk> + right-click to show missing or incorrect blocks, and <hk>shift</hk> + <hk>ctrl</hk> + right-click to hide the preview.')+section('Temperature limits','Tier 1: 1500°C\nTier 2: 1650°C\nTier 3: 1800°C\nThe weakest refractory component sets the limit. '+links['operation'])
lang['steelworks-guide-operation-text']=section('Firing','Open the fuel hatch. Sneak + right-click with coke to load fuel, then ignite with a firestarter. The furnace can preheat without blends. Aim bellows directly into a refractory air intake. Close both hatches to retain heat.')+section('Loading','Open the loading hatch. Sneak + right-click with the desired blend. Only one blend type per batch; load in multiples of 20, up to 160. Each mold holds the equivalent of 20 blends. Close both hatches to process.')+section('Watching the batch','The native-style dynamic renderer shows the blend, temperature glow, molten metal and hardened ingots. Processing advances only above the recipe temperature. Fuel automatically stops at completion; to stop manually, open the fuel hatch and sneak + right-click with an empty hand.')+section('Retrieving ingots','When the metal has hardened, sneak + right-click with a hammer once to release the batch. Use metal tongs to retrieve ingots at 200°C or hotter; collect cooler ingots by hand. Reheat for smithing if necessary.')+section('Related information',links['materials']+' • '+links['maintenance'])
lang['steelworks-guide-materials-text']=section('Blends','Each recipe yields four blends; each blend represents five metal units. Twenty blends produce one ingot, up to eight ingots per batch. Crushed coal is an additive; coke is the furnace fuel.')+section('Iron Blend','4 crushed iron + 1 crushed coal. Crucible: cast iron at 1320°C. Furnace: blister steel at 1450°C for 8 hours, tier 1. Refine blister steel on an anvil.')+section('Nickel steel','3 crushed iron + 2 crushed nickel + 1 crushed coal. 1550°C for 12 hours, tier 2.')+section('Stainless steel','3 crushed iron + 1 crushed nickel + 1 crushed chromite + 1 crushed coal. 1600°C for 16 hours, tier 2.')+section('Chrome steel','4 crushed iron + 1 crushed chromite + 1 crushed coal. 1650°C for 20 hours, tier 2.')+section('Titanium','4 powdered titanium dioxide + 1 crushed coal. 1750°C for 24 hours, tier 3. This is simplified gameplay refining.')+section('Processing time','These are default in-game hours at operating temperature. Heating and cooling take additional time; the server configuration can change processing durations.')
lang['steelworks-guide-smithing-title']='Steelworks: Smithing and Heat Treatment'
lang['steelworks-guide-smithing-text']=section('Smithing','Use a native forge and anvil to shape the ingots into heads and equipment. Cupronickel requires bronze or better; specialty steels require iron or better; titanium requires steel or equivalent. Cast iron anvil sections are an exception to the limited cast-tool policy.')+section('Quenching','Nickel steel, stainless steel and chrome steel use the native Quenchable behavior on eligible tool heads and directly forged tools. Heat the item into its displayed quenching range (default 815–1050°C), use tongs, and cool in water. Native shatter chance and accumulated treatment effects apply. Use fire clay coverage where the native recipe permits it.')+section('Tempering','After quenching, eligible pieces display their tempering range (default 550–700°C). Reheat and temper using the native game interaction. Assemble the finished head with its handle; native recipes transfer supported treatment buffs to the tool. Titanium, cast iron and cupronickel do not use these steel heat-treatment rules.')+section('Equipment','Metal tongs, crowbars, wrenches, helve hammer heads, arrowheads, armor components and shield components are forged on an anvil. Assemble them with the normal native recipes. Armor and round shields use the native protection system.')+section('Anvils','All six Steelworks anvils can be forged in two parts. Cast iron and cupronickel may also be cast in an anvil mold. Use the normal native weld process for forged parts.')
lang['steelworks-guide-maintenance-text']=section('Refractory wear','Default check interval is 168 operating game hours above 1340°C. Per check: tier 1 10%, tier 2 5%, tier 3 0.1%. Bricks, gratings and air intakes may degrade; replace damaged parts. The alloy container does not degrade.')+section('Troubleshooting','No heat: verify fuel, structure, hatches and airflow. No progress: check recipe temperature, tier and loaded blend quantity. No release: allow the metal to harden first. Use the intake preview to diagnose missing or incorrect blocks.')
write(lp,lang)
# Metadata, build script and report.
for p in [root/'content/modinfo.json',root/'BUILD_WINDOWS.ps1',root/'README.md']:
 s=p.read_text().replace('0.9.5','0.9.6');p.write_text(s)
report['base_patch_operations']=initial;report['total_patch_operations']=len(patches);report['creative_tab_files']=len(tabcodes);report['native_round_shield_tab_stacks']=len(shieldstacks);report['status']='STATIC_SOURCE_CANDIDATE_NATIVE_BUILD_AND_IN_GAME_TEST_REQUIRED';write(root/'EQUIPMENT_AUDIT_0.9.6.json',report)
print({k:len(v) if isinstance(v,list) else v for k,v in report.items()})
