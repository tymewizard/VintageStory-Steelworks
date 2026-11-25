#!/usr/bin/env python3
"""Inventory real game/EM2 files without assuming item codes or modifying inputs."""
import argparse,json,zipfile,pathlib,re
p=argparse.ArgumentParser();p.add_argument('game_assets');p.add_argument('em_zip');p.add_argument('-o',default='asset-audit.json');a=p.parse_args()
root=pathlib.Path(a.game_assets); assert root.is_dir(), 'Supply installed assets directory'
terms=re.compile(r'cupronickel|castiron|nickelsteel|stainlesssteel|chromesteel|titanium|crushed|metalproperties|quenchable',re.I)
rows=[]
def inspect(origin,name,data):
 if not name.lower().endswith('.json'): return
 s=data.decode('utf-8-sig',errors='replace'); hits=[]
 for i,line in enumerate(s.splitlines(),1):
  if terms.search(line): hits.append({'line':i,'text':line.strip()[:500]})
 if hits or re.search(r'itemtypes/(tool|toolhead)|recipes/smithing|resource/(ingot|metalbit)',name): rows.append({'origin':origin,'path':name,'matches':hits})
for f in root.rglob('*.json'): inspect('game',str(f.relative_to(root)),f.read_bytes())
with zipfile.ZipFile(a.em_zip) as z:
 for n in z.namelist():
  if n.endswith('.json'): inspect('em',n,z.read(n))
pathlib.Path(a.o).write_text(json.dumps(rows,indent=2));print(f'{len(rows)} candidate assets; report: {a.o}')
