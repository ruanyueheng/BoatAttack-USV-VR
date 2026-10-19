"""Small original packed texture atlas for the harbour handcraft assets."""
from pathlib import Path
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parents[2]/'Assets/Resources/FishingV034';root.mkdir(parents=True,exist_ok=True)
rng=np.random.default_rng(3402);s=256;y,x=np.mgrid[0:s,0:s]/s
math_pi=np.pi
colors=[(119,76,47),(169,127,82),(40,70,71),(185,149,85),(183,161,120),(213,203,173),(232,222,194),(202,155,139)]
atlas=np.zeros((512,1024,3),dtype=np.uint8);ms=np.zeros((512,1024,4),dtype=np.uint8);mr=np.zeros((512,1024,3),dtype=np.uint8);norm=np.zeros((512,1024,3),dtype=np.uint8)
for tile,color in enumerate(colors):
    noise=rng.normal(0,1,(s,s));grain=np.zeros((s,s))
    if tile in (0,1,2):
        wave=y+.010*np.sin(x*13)+.006*np.sin(x*31+y*5)
        grain=3*np.sin(wave*math_pi*36)+1.4*np.sin(wave*math_pi*112)+noise*.7
        if tile!=2:grain-=3.5*np.exp(-((x-.38)**2/.015+(y-.57)**2/.003))*np.sin(np.sqrt((x-.38)**2*4+(y-.57)**2)*120)
        else:grain*=.35
    elif tile in (4,5):grain=1.3*np.sin(x*math_pi*160)*np.sin(y*math_pi*160)+noise*.5
    elif tile==3:grain=noise*.8+np.sin(x*8)*2
    else:grain=noise*.45+np.sin(x*6+y*4)
    patch=np.clip(np.array(color)[None,None,:]+grain[:,:,None],0,255).astype(np.uint8)
    r,c=tile//4*256,tile%4*256;atlas[r:r+s,c:c+s]=patch
    metallic=255 if tile==3 else 0;smooth=135 if tile==3 else 62 if tile in (0,1,2) else 25
    ms[r:r+s,c:c+s]=(metallic,0,0,smooth);mr[r:r+s,c:c+s]=(255,255-smooth,metallic)
    dy,dx=np.gradient(grain);normal=np.dstack((-dx*.015,-dy*.015,np.ones_like(dx)));normal/=np.linalg.norm(normal,axis=2,keepdims=True)
    norm[r:r+s,c:c+s]=np.clip((normal*.5+.5)*255,0,255).astype(np.uint8)
for name,data in [('HandcraftAlbedo',atlas),('HandcraftMetalSmooth',ms),('HandcraftMetalRough',mr),('HandcraftNormal',norm)]:Image.fromarray(data).save(root/(name+'.png'))
assert atlas.shape==(512,1024,3)
print('[V034-TEXTURES] PASS original wood, paint, brass, flax and linen atlas')
