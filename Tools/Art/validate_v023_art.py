"""Asset boundary and photo self-check, plus review contact sheets; never edits originals."""
from pathlib import Path
import json, hashlib, struct
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[2]
rows=json.loads((ROOT/'ArtSource/V023/manifest.json').read_text(encoding='utf-8'))
assert len(rows)==57 and len({r['name'] for r in rows})==57
marine={'SeaTurtle','Octopus','Crab','Lobster','Seahorse','Stingray','Jellyfish','Cuttlefish'}
items={'FieldGuide','Binoculars','Lantern','Shell','Driftwood','Stone','Fiber','Clay','IronOre','Coal','Sand','Plank','Stick','Rope','Charcoal','IronIngot','Brick','Glass','Workbench','Furnace','StonePickaxe','IronPickaxe','HuntingSpear','RawMeat','CookedMeat'}
for row in rows:
    name=row['name']; folder=ROOT/('Assets/Resources/FishingFish' if name in marine else 'Assets/Resources/FishingV023/'+('Items' if name in items else 'Home'))
    model=folder/(name+'.fbx');assert model.stat().st_size>200
    assert hashlib.sha256(model.read_bytes()).hexdigest()==row['sha256'],name
    assert row['triangles']<=5000 if name in marine else row['triangles']<=90000
    if row['source'].startswith('ArtSource/'):
        source=ROOT/row['source'];data=source.read_bytes();magic,version,length=struct.unpack_from('<III',data)
        assert magic==0x46546c67 and version==2 and length==len(data),source
        chunk_size,chunk_type=struct.unpack_from('<II',data,12);assert chunk_type==0x4e4f534a
        gltf=json.loads(data[20:20+chunk_size])
        for image in gltf.get('images',[]):
            uri=image.get('uri','')
            if uri and not uri.startswith('data:'):
                image_path=(source.parent/uri).resolve();assert image_path.is_relative_to(ROOT/'ArtSource/V023/Originals') and image_path.exists(),image_path
    if name=='HouseShell':continue
    photo=Image.open(folder/(name+'Thumbnail.png')).convert('RGBA');alpha=photo.getchannel('A');bbox=alpha.point(lambda v:255 if v>32 else 0).getbbox()
    assert bbox and bbox[0]>2 and bbox[1]>2 and bbox[2]<photo.width-2 and bbox[3]<photo.height-2,(name,'clipped or blank photo',bbox)
    assert sum(v>32 for v in alpha.getdata())>photo.width*photo.height*.01,(name,'empty photo')

preview=ROOT/'Docs/Images/V023';preview.mkdir(parents=True,exist_ok=True)
for name,models,cols in [('marine-gallery',list(marine),4),('craft-gallery',[r['name'] for r in rows if r['name'] in items],5)]:
    models=sorted(models);w,h=320,256;canvas=Image.new('RGB',(cols*w,((len(models)+cols-1)//cols)*h),(20,37,48));draw=ImageDraw.Draw(canvas)
    for i,model in enumerate(models):
        folder=ROOT/('Assets/Resources/FishingFish' if model in marine else 'Assets/Resources/FishingV023/Items')
        photo=Image.open(folder/(model+'Thumbnail.png')).convert('RGBA');photo.thumbnail((w-24,h-44))
        x=i%cols*w+(w-photo.width)//2;y=i//cols*h+8;canvas.paste(photo,(x,y),photo)
        draw.text((i%cols*w+14,(i//cols+1)*h-24),model,fill=(233,224,204))
    canvas.save(preview/(name+'.png'))
print('V023 ART PASS: 57 unique mesh exports, safe source textures, actual visible object photos, and marine budgets')
