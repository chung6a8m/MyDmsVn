"""Repin only after reviewing an intentional corpus edit. No runtime use."""
from pathlib import Path
import json,hashlib
p=Path(__file__).resolve().parent
# Normalize authoring tools' Windows output to the directory-local corpus byte policy.
for f in p.iterdir():
 if f.is_file():f.write_text(f.read_text(encoding='utf-8-sig'),encoding='utf-8',newline='\n')
manifest={'contractVersion':'web4-v1','sourceBaseline':'48e9cd7ae096ecadd184e6ea6a372306d77f41b1','fixtures':json.loads((p/'fixture-index.json').read_text(encoding='utf-8')),'artifacts':[]}
for f in sorted(p.iterdir(),key=lambda x:x.name):
 if f.name!='manifest.json' and f.is_file():manifest['artifacts'].append({'file':f.name,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
(p/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
