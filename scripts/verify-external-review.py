import json,math,hashlib
from pathlib import Path
import pypdfium2 as pdf
root=Path('artifacts/external-only/examples'); results=[]
for folder in [root/'flat',root/'single-face-14']:
 run=json.loads((folder/'calculation.json').read_text(encoding='utf-8-sig')); schedule=json.loads((folder/'frame-schedule.json').read_text(encoding='utf-8-sig')); model=json.loads((folder/'model.json').read_text(encoding='utf-8-sig'))
 frames={f['Id']:f for f in model['Frames']}; pressures=0;balances=0
 def v(d):return (d['X'],d['Y'],d['Z'])
 def add(a,b):return tuple(x+y for x,y in zip(a,b))
 def scale(a,c):return tuple(x*c for x in a)
 def cross(a,b):return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
 def length(a):return math.sqrt(sum(x*x for x in a))
 for c in run['Cases']:
  speed=40 if c['State']==0 else 20; cp=.7 if c['SurfaceId']=='wall' else -.6 if c['Alternative']=='A' else .15
  expected=.6*speed*speed*cp
  assert abs(c['PressurePa']-expected)<1e-8;pressures+=1
 for plan in schedule['Plans']:
  F=(0,0,0);M=(0,0,0)
  for s in plan['Segments']:
   frame=frames[s['FrameId']];origin=v(frame['Start']);delta=add(v(frame['End']),scale(origin,-1));a=add(origin,scale(delta,s['StartFraction']));b=add(origin,scale(delta,s['EndFraction']));L=length(delta)*(s['EndFraction']-s['StartFraction']);w0=v(s['StartNPerM']);w1=v(s['EndNPerM']);F=add(F,scale(add(w0,w1),L/2));M=add(M,scale(add(cross(a,add(scale(w0,2),w1)),cross(b,add(w0,scale(w1,2)))),L/6))
  sf=(0,0,0);sm=(0,0,0)
  for check in plan['Checks']:sf=add(sf,v(check['SurfaceForceN']));sm=add(sm,v(check['SurfaceMomentNm']))
  assert length(add(F,scale(sf,-1)))<.01 and length(add(M,scale(sm,-1)))<.01;balances+=1
 docs=[]
 for f in folder.glob('*.pdf'):
  doc=pdf.PdfDocument(str(f));text='\n'.join(p.get_textpage().get_text_range() for p in doc)
  assert 'EXTERNAL ONLY' in text and 'ENGINEER CONTROLLED SEPARATELY' in text
  if f.stem=='audit':
   for c in run['Cases']:assert c['Id'] in text
  docs.append(dict(file=str(f),pages=len(doc),sha256=hashlib.sha256(f.read_bytes()).hexdigest()))
 results.append(dict(example=folder.name,pressureChecks=pressures,forceMomentChecks=balances,documents=docs))
(root/'independent-arithmetic.json').write_text(json.dumps(results,indent=2));print(json.dumps(results,indent=2))
