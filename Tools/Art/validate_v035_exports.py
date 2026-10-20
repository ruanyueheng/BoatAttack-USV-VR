"""Inspect the actual portable kit and exact FBX files consumed by Unity."""
import hashlib
import json
import math
from pathlib import Path
import struct

root=Path(__file__).resolve().parents[2]
source=root/'ArtSource/V035'
data=(source/'coastal-craftsmanship-kit.glb').read_bytes()
magic,version,length=struct.unpack_from('<4sII',data)
assert magic==b'glTF' and version==2 and length==len(data)
size,kind=struct.unpack_from('<I4s',data,12)
assert kind==b'JSON'
doc=json.loads(data[20:20+size])
binary_offset=20+size+8
records=json.loads((source/'export-validation.json').read_text(encoding='utf-8'))
assert len(records)==len(doc['meshes'])
assert {'Windmill','AncientTree','Galleon','ObservatoryExterior','RootBridge','TelescopeTube'}.issubset({r['name'] for r in records})
assert sum(n.get('name','').startswith('V35_Entry') for n in doc['nodes'])==5
assert len(doc['images'])==3
assert not any(node.get('name')=='Cube' for node in doc['nodes'])
assert not doc.get('animations')
total=0
for mesh in doc['meshes']:
    for primitive in mesh['primitives']:
        assert 'NORMAL' in primitive['attributes'] and 'TEXCOORD_0' in primitive['attributes']
        count=doc['accessors'][primitive['indices']]['count'];assert count%3==0;total+=count//3
        accessor=doc['accessors'][primitive['attributes']['POSITION']]
        assert accessor['componentType']==5126 and accessor['type']=='VEC3'
        view=doc['bufferViews'][accessor['bufferView']]
        offset=binary_offset+view.get('byteOffset',0)+accessor.get('byteOffset',0)
        stride=view.get('byteStride',12)
        for i in range(accessor['count']):assert all(math.isfinite(v) for v in struct.unpack_from('<fff',data,offset+i*stride))
assert total==sum(r['triangles'] for r in records)
for record in records:
    path=root/'Assets/Resources/FishingV035'/(record['name']+'.fbx')
    assert hashlib.sha256(path.read_bytes()).hexdigest()==record['fbxSha256']
report={'passed':True,'models':len(records),'triangles':total,'embeddedImages':3,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest(),'unexpectedStartupObjects':0}
(source/'portable-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))
