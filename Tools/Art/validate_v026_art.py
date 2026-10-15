"""Check the exported V026 assets and make a contact sheet for visual review."""
from pathlib import Path
import hashlib, json
from PIL import Image, ImageDraw

root=Path(__file__).resolve().parents[2]
rows=json.loads((root/'ArtSource/V026/manifest.json').read_text(encoding='utf-8'))
expected={'Workbench','Furnace','Forge','Driftwood','Fiber','Stone','IronOre','Coal','Clay','Sand'}
assert len(rows)==10 and {row['name'] for row in rows}==expected
assert (root/'ArtSource/V026/Coastal-props-v026.blend').stat().st_size>1000
canvas=Image.new('RGB',(1280,600),(20,37,48))
draw=ImageDraw.Draw(canvas)
folder=root/'Assets/Resources/FishingV026/Items'
for i,row in enumerate(rows):
    name=row['name']
    model=folder/(name+'.fbx')
    assert hashlib.sha256(model.read_bytes()).hexdigest()==row['sha256'],name
    assert 20<row['triangles']<=7500 and -.035<=row['unity_min'][1]<=.035,name
    image=Image.open(folder/(name+'Thumbnail.png')).convert('RGBA')
    alpha=image.getchannel('A')
    bbox=alpha.point(lambda value:255 if value>32 else 0).getbbox()
    assert image.size==(256,256) and bbox and min(bbox[:2])>2 and max(bbox[2:])<254,(name,bbox)
    assert sum(value>32 for value in alpha.getdata())>256*256*.025,(name,'empty photograph')
    canvas.paste(image,((i%5)*256,(i//5)*300),image)
    draw.text(((i%5)*256+20,(i//5)*300+265),name+' / '+str(row['triangles'])+' triangles',fill=(231,221,199))
preview=root/'Docs/Images/V026/coastal-props.png'
preview.parent.mkdir(parents=True,exist_ok=True)
canvas.save(preview)
print('V026 art: 10 models, dimensions, hashes and unclipped matching photographs passed.')
