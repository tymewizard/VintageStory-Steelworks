"""Refresh intake UVs and 0.9.5 guide text. Mold contents are rendered dynamically in C#."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]/'content/assets'; a=root/'steelworks'; shapes=a/'shapes/block'; blocks=a/'blocktypes'; langfile=a/'lang/en.json'; lang=json.loads(langfile.read_text())
def read(p): return json.loads(p.read_text())
def write(p,d): p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,separators=(',',':'))+'\n')
# Coordinates in shape UV units, scaled to the declared texture width/height.
for p in shapes.glob('air-intake-*.json'):
 d=read(p)
 for e in d['elements']:
  if e['name'].startswith('pipe-'): continue
  x,y,z=e['from'];X,Y,Z=e['to']; w=d['textureWidth']/16;h=d['textureHeight']/16
  uvs={'north':(16-X,16-Y,16-x,16-y),'south':(x,16-Y,X,16-y),'east':(16-Z,16-Y,16-z,16-y),'west':(z,16-Y,Z,16-y),'up':(x,z,X,Z),'down':(x,16-Z,X,16-z)}
  for face,f in e['faces'].items():
   if f['texture']=='#brick':
    u,v,U,V=uvs[face];f['uv']=[u*w,v*h,U*w,V*h]
 write(p,d)
lang['steelworks-guide-operation-text']='Open the fuel hatch. Shift + right-click with coke to load, then ignite with a firestarter; no blends required for preheating. Close both hatches and aim bellows directly into the intake. Open the loading hatch and Shift + right-click with blends to load, including while burning. Load multiples of 20, up to 160. Close both hatches to process. The eight cavities fill in order, one ingot volume per 20 blends. The renderer shows the actual blend texture, then native heat glow and metal surfaces during conversion. Finished ingots cool through liquid, soft and hardened states. Default processing hours: blister steel 8, nickel steel 12, stainless steel 16, chrome steel 20, titanium 24. Heating and cooling add time. Fuel stops automatically at batch completion; to stop manually, open the fuel hatch and Shift + right-click with an empty hand. Metal is liquid above 80% of its melting point and hardened below 30%. Once hardened, Shift + right-click with a hammer once, then collect with tongs at 200 °C or hotter or by hand below 200 °C. Reheat for smithing if necessary. Server settings can change durations.'
lang['steelworks-guide-maintenance-text']='Refractory bricks, gratings and air intakes degrade. Default wear check every 168 operating game hours above 1340 °C. Per check: Tier 1 10%, Tier 2 5%, Tier 3 0.1%. Replace damaged blocks. The container does not degrade. Processing pauses on inadequate heat or invalid structure. Opening hatches limits furnace temperature. Stored inputs and finished ingots remain on the controller until retrieved.'
write(langfile,lang)
