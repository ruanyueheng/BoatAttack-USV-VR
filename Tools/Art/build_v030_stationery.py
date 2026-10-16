"""Deterministic kraft fibres and original coastal postage art for world-space letters."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np

ROOT=Path(__file__).resolve().parents[2]; OUT=ROOT/'Assets/Resources/FishingStationery';OUT.mkdir(parents=True,exist_ok=True)
rng=np.random.default_rng(30)
for name,w,h,base in [('KraftPaper',768,1024,(225,197,150)),('EnvelopePaper',1024,576,(201,166,116))]:
    y,x=np.mgrid[:h,:w]; edge=np.minimum.reduce([x/(w*.08),(w-1-x)/(w*.08),y/(h*.06),(h-1-y)/(h*.06),np.ones_like(x)])
    grain=rng.normal(0,1.8,(h,w))+1.8*np.sin(y*.024)+1.1*np.sin(x*.013+y*.004)-7*(1-edge)
    fold=2*np.exp(-((y-h*.51)/2.8)**2)-2*np.exp(-((y-h*.514)/3)**2)
    data=np.clip(np.array(base)+grain[:,:,None]+fold[:,:,None],0,255).astype('uint8')
    image=Image.fromarray(data);draw=ImageDraw.Draw(image,'RGBA')
    for i in range(w*h//250):
        px=int(rng.integers(w));py=int(rng.integers(h));length=int(rng.integers(2,12))
        draw.line((px,py,px+length,py+int(rng.integers(-2,3))),fill=(110,75,39,int(rng.integers(8,24))),width=1)
    if name=='KraftPaper':
        draw.rectangle((23,23,w-24,h-24),outline=(108,77,43,70),width=1)
        for px in [28,w-29]:
            for py in [28,h-29]:draw.ellipse((px-3,py-3,px+3,py+3),fill=(117,73,37,120))
    image.save(OUT/(name+'.png'))

image=Image.new('RGBA',(640,320));d=ImageDraw.Draw(image)
ink=(72,95,84,240);red=(136,64,48,235)
d.rectangle((330,14,608,302),fill=(239,222,182,255))
for px in range(330,609,19):
    for py in [14,302]:d.ellipse((px-5,py-5,px+5,py+5),fill=(0,0,0,0))
for py in range(14,303,19):
    for px in [330,608]:d.ellipse((px-5,py-5,px+5,py+5),fill=(0,0,0,0))
d.rectangle((347,31,591,285),outline=ink,width=3)
d.rectangle((357,64,581,220),fill=(190,202,174,255))
d.ellipse((513,77,553,117),fill=(224,182,101,255))
d.polygon([(365,186),(413,145),(435,159),(462,127),(504,183),(578,201),(578,218),(365,218)],fill=ink)
for py in [231,239]: d.line((361,py,577,py),fill=ink,width=2)
font=ImageFont.truetype(str(ROOT/'Assets/Resources/FishingUIPrototype/TideHandSC.ttf'),25)
small=ImageFont.truetype(str(ROOT/'Assets/Resources/FishingUIPrototype/TideHandSC.ttf'),18)
d.text((392,33),'回 潮 海',font=font,fill=ink);d.text((388,252),'航海邮政  30',font=small,fill=ink)
for r in [105,96]:d.ellipse((162-r,158-r,162+r,158+r),outline=red,width=3)
d.text((105,87),'海风邮局',font=font,fill=red);d.text((100,177),'平 安 抵 达',font=small,fill=red)
d.text((106,137),'TIDE • 30',font=small,fill=red)
for dy in [-32,-13,6,25]:
    line=[(x,int(158+dy+7*np.sin(x/23))) for x in range(254,487)]
    d.line(line,fill=red,width=3)
image.save(OUT/'CoastalPostmark.png')
assert np.asarray(Image.open(OUT/'KraftPaper.png')).std()>10
assert image.getbbox() is not None
print('V030 STATIONERY PASS')
