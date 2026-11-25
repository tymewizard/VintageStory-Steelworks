"""Generate conditional Recycling Tools 1.5.0 patches from the exact installed archive.
Usage: python tools/integrate_recycling_098.py <RecyclingTools-v1.5.0.zip>
No vanilla or third-party assets are overwritten. Recovery yield follows the
existing steel chisel recipe; only equipment explicitly listed in our audits
is added. Unknown optional equipment is deliberately excluded.
"""
import copy, fnmatch, json, sys, zipfile
from pathlib import Path
root = Path(__file__).resolve().parents[1]
assets = root/'content/assets/steelworks'
materials = dict(cupronickel='Cupronickel', castiron='Cast iron', nickelsteel='Nickel steel', stainlesssteel='Stainless steel', chromesteel='Chrome steel', titanium='Titanium')
base = json.loads((root/'NATIVE_ASSET_REPORT.json').read_text())
extra = json.loads((root/'EQUIPMENT_AUDIT_0.9.6.json').read_text())
codes = set(base['tools']+base['heads']+extra['new_equipment'])
patches, coverage = [], []
def append(file, path, val):
    patches.append(dict(file=file,op='add',path=path,value=val,dependsOn=[dict(modid='recyclingtools')]))
with zipfile.ZipFile(sys.argv[1]) as z:
    assert json.loads(z.read('modinfo.json'))['modid']=='recyclingtools'
    def asset(path): return json.loads(z.read('assets/recyclingtools/'+path))
    for metal in materials:
        assert metal not in asset('itemtypes/metalscrap.json')['variantgroups'][0]['states']
        append('recyclingtools:itemtypes/metalscrap.json','/variantgroups/0/states/-',metal)
        append('recyclingtools:recipes/grid/scrap_to_metalbit.json','/0/ingredients/S/allowedVariants/-',metal)
    # Add explicit recipes rather than widening the original low-tier chisel recipes.
    for f in sorted(z.namelist()):
        if not f.endswith('.json') or not any('/'+d+'/' in f for d in ('metal_recycling','tool_dismantling')): continue
        if f.endswith('/tool_dismantling/knifeblade_recycling.json'): continue # direct head recovery already covers these
        original=json.loads(z.read(f)); source=original[-1]
        if source['ingredients']['C'].get('allowedVariants')!=['steel']: raise ValueError(f)
        pat=source['ingredients']['B']['code']
        valid=[m for m in materials if pat.replace('*',m) in codes]
        if not valid: continue
        recipe=copy.deepcopy(source)
        recipe['ingredients']['B']['allowedVariants']=valid
        recipe['ingredients']['C']['allowedVariants']=['steel','nickelsteel','stainlesssteel','chromesteel','titanium']
        recipe['name']='steelworks-recycling-'+Path(f).stem
        append(f.replace('assets/recyclingtools/','recyclingtools:'),'/-',recipe)
        coverage.append(dict(target=f,materials=valid,inputs=[pat.replace('*',m) for m in valid],quantity=recipe['output']['quantity'],output=recipe['output']['code']))
        # Let the same chisels recycle ordinary metals too, preserving existing yields and restrictions.
        for m in ['nickelsteel','stainlesssteel','chromesteel','titanium']:
            append(f.replace('assets/recyclingtools/','recyclingtools:'),f'/{len(original)-1}/ingredients/C/allowedVariants/-',m)
    f='recipes/grid/tool_dismantling.json'; base_helve=asset(f)
    valid=[m for m in materials if 'game:helvehammer-'+m in codes and 'game:helvehammerhead-'+m in codes]
    for m in valid: append('recyclingtools:'+f,'/0/ingredients/T/allowedVariants/-',m)
    # Validate actual append paths on original JSON documents without changing originals.
    simulated={}
    for patch in patches:
        f=patch['file'].split(':',1)[1]
        if f not in simulated: simulated[f]=asset(f)
        obj=simulated[f]
        for p in patch['path'].strip('/').split('/')[:-1]: obj=obj[int(p)] if isinstance(obj,list) else obj[p]
        assert isinstance(obj,list) and patch['path'].endswith('/-')
        obj.append(patch['value'])
    # Names use the same explicit-domain convention as the working 0.9.7 Armory hotfix.
    langfile=assets/'lang/en.json'; lang=json.loads(langfile.read_text())
    for m,name in materials.items(): lang['recyclingtools:item-metalscrap-'+m]=name+' scrap'
    langfile.write_text(json.dumps(lang,indent=2,ensure_ascii=False)+'\n')
    (assets/'patches/optional-recycling.json').write_text(json.dumps(patches,indent=2)+'\n')
    report=dict(version='1.5.0',patches=len(patches),recipe_families=len(coverage),equipment_variants=sum(len(r['inputs']) for r in coverage),helve_materials=valid,materials=list(materials),coverage=coverage,limitations=['Base equipment only. Armory, armor, crossbow bolts and other optional equipment not covered by this pass.','Static paths validated; recipe loader and runtime compatibility not tested.','Recovery yields inherited from Recycling Tools steel recipes, not a separate balance system.'])
    (root/'RECYCLING_AUDIT_0.9.8.json').write_text(json.dumps(report,indent=2)+'\n')
    print({k:v for k,v in report.items() if k not in ('coverage','limitations')})
