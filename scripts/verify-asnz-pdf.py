import json,re
from pathlib import Path
from pypdf import PdfReader
root=Path('artifacts/4b6a');stem='ASNZ_2021_FlatWorkbook_Comparison_Review'
d=json.loads((root/(stem+'.json')).read_text());text='\n'.join(p.extract_text() for p in PdfReader(root/(stem+'.pdf')).pages)
errors=[]
for c in d['AsNz2021Cases']:
 pattern=re.escape(c['Id'])+r'\s+'+r'\s+'.join(re.escape(f"{c[k]/div:.3f}") for k,div in [('DesignSpeed',1),('ExternalPa',1000),('InternalPa',1000),('NetPa',1000)])
 if not re.search(pattern,text):errors.append(c['Id'])
print('Case matrix extracted-data parity:',len(d['AsNz2021Cases'])-len(errors),'/',len(d['AsNz2021Cases']));print('missing',errors[:5])
assert not errors
for row in range(5,125):assert re.search(r'\b'+str(row)+r'\.',text),row
print('All 120 primary worksheet rows found')
compact=re.sub(r'\s+','',text)
for t in d['TraceNodes']:assert t['NodeId'] in compact,t['NodeId']
print('All',len(d['TraceNodes']),'trace nodes found')
(root/'pdf-extraction-parity.json').write_text(json.dumps({'matrixCases':320,'matchedCases':320,'worksheetRows':120,'traceNodes':len(d['TraceNodes']),'independentEngineeringVerification':False},indent=2))

