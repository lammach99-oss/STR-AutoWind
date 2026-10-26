import sys,json,csv,zipfile,xml.etree.ElementTree as E,re
from pathlib import Path
from pypdf import PdfReader
root=Path(sys.argv[1] if len(sys.argv)>1 else 'artifacts/4b6b');z=zipfile.ZipFile(root/'workbook.xlsm');ns={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'};sheet=E.fromstring(z.read('xl/worksheets/sheet2.xml'));src={c.attrib['r']:c for c in sheet.findall('.//s:c',ns)}
legacy=json.loads((root/'ASNZ_2021_FlatWorkbook_Review.json').read_text());explicit=json.loads((root/'ASNZ_2021_Explicit_Review.json').read_text());rows=[]
def add(cell,field,get,unit=''):
 c=src[cell];v=float(c.find('s:v',ns).text);formula=c.find('s:f',ns)
 for name,run in [('LEGACY_WORKBOOK_COMPARISON',legacy),('EXPLICIT_SYNTHETIC_REVIEW',explicit)]:
  b=get(run);delta=b-v;match=abs(delta)<1e-8
  rows.append(['Flat roof',cell,field,unit,v,b,delta,delta/abs(v)*100 if v else '', 'MATCH' if match else 'SOURCE_UNVERIFIED',name,'Cached workbook arithmetic only; not normative qualification' if match else 'Selected synthetic Cpe differs; engineer must verify 2021 height/roof/zone applicability', 'Review method / source evidence; not approved',formula.text if formula is not None else ''])
for cell,key in [('K29','Vsit_Uls_0'),('K30','Vsit_Sls_0')]:add(cell,key,lambda r,k=key:r['ComputedQuantities'][k],'m/s')
for cell,state in [('K34',0),('K35',1)]:add(cell,'q_basis',lambda r,s=state:next(c['PressureBasisPa'] for c in r['AsNz2021Cases'] if c['Direction']==0 and c['LimitState']==s)/1000,'kN/m²')
for direction,shift in [(0,0),(2,44)]:
 for first,surface in [(44,'Windward'),(50,'Leeward')]:
  for offset,state in [(2,0),(3,1)]:
   add('J'+str(first+offset+shift),surface,lambda r,d=direction,s=state,f=surface:next(c['ExternalPa'] for c in r['AsNz2021Cases'] if c['Direction']==d and c['LimitState']==s and c['Surface']==f and c['Id'].endswith('Positive'))/1000,'kN/m²')
 for first,surface,case,count in [(57,'SideLeft',0,4),(67,'Roof',1,5),(76,'Roof',2,5)]:
  for zone in range(count):
   add('I'+str(first+zone+shift),f'{surface}_Case{case}_Zone{zone}',lambda r,d=direction,f=surface,c=case,z=zone:next(a['ExternalPa'] for a in r['AsNz2021Cases'] if a['Direction']==d and a['LimitState']==0 and a['Surface']==f and a['RoofCase']==c and a['Zone']==z and a['Id'].endswith('Positive'))/1000,'kN/m²')
with (root/'workbook-vs-engine.csv').open('w',newline='',encoding='utf-8') as f:
 w=csv.writer(f);w.writerow(['Sheet','Cell','Field','Unit','WorkbookCached','EngineResult','SignedDelta','PercentDifference','Classification','Fixture','Reason','ReviewerAction','OriginalFormula']);w.writerows(rows)
print('comparison rows',len(rows),'legacy matches',sum(r[8]=='MATCH' and r[9]=='LEGACY_WORKBOOK_COMPARISON' for r in rows))
for stem in ['ASNZ_2021_FlatWorkbook_Review','ASNZ_2021_Blocked_Diagnostic']:
 run=json.loads((root/(stem+'.json')).read_text());pdf=PdfReader(root/(stem+'_Detailed_Audit.pdf'));text='\n'.join(p.extract_text() for p in pdf.pages);compact=re.sub(r'\s+','',text)
 for c in run['AsNz2021Cases']:assert c['Id'] in compact,c['Id']
 for t in run['TraceNodes']:assert t['NodeId'] in compact,t['NodeId']
 print(stem,len(pdf.pages),'audit pages',len(run['AsNz2021Cases']),'cases',len(run['TraceNodes']),'traces')
 main=PdfReader(root/(stem+'_MAIN.pdf'));print('main pages',len(main.pages));assert len(main.pages)<=4
 assert all(len(p.extract_text())>300 for p in main.pages)
 if run['NumericalComputationPossible']:
  text='\n'.join(p.extract_text() for p in main.pages)
  for c in run['AsNz2021Cases']:
   if c['Direction'] in [0,2] and c['LimitState']==0 and c['Surface'] in ['Windward','Leeward','SideLeft','Roof'] and c['Id'].endswith('Positive'):
    assert f"{c['ExternalPa']/1000:.3f}" in text,(c['Id'],c['ExternalPa'])
print('PDF extraction parity PASS')
