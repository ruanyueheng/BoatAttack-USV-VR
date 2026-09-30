"""Original articulated gloves and sea-green spinning tackle; Blender background generator."""
from pathlib import Path
import warnings
warnings.filterwarnings('ignore', category=DeprecationWarning)
# Reuse geometry primitives without executing/re-exporting the harbor library.
base=Path(__file__).with_name('generate_harbor_props.py')
exec(compile(base.read_text(encoding='utf-8-sig').split('\ncrate()\n')[0],str(base),'exec'))
OUT=ROOT/'Assets/Resources/FishingGear';SOURCE=ROOT/'ArtSource/FishingGear'
OUT.mkdir(parents=True,exist_ok=True);SOURCE.mkdir(parents=True,exist_ok=True)
PALETTE.update({'carbon':(.045,.072,.083,.5),'cork':(.59,.40,.22,.05),'glove':(.14,.24,.26,.2),'leather':(.085,.12,.14,.1)})
# Rod authored along +Z, origin at the dominant-hand grip.
begin('RodBody')
tube('Tapered carbon blank',[(0,0,-.25),(0,0,.3)],.022,'carbon',12)
for i in range(7):
 z=.28+i*.25;tube('Carbon section',[(0,0,z),(0,0,z+.25)],.014-i*.0016,'carbon',12)
for start,end in [(-.25,-.035),(.065,.18)]:
 tube('Contoured cork grip',[(0,0,start),(0,0,start+.022),(0,0,end-.015),(0,0,end)],.028,'cork',16)
 for i in range(8):
  z=start+(end-start)*i/8
  tube('Cork grain',[(.022,.013,z),(.024,.014,z+.011)],.0018,'wood',4)
for z in (-.265,-.03,.05,.18,.25):ring('Anodised lock collar',(0,0,z),.025,.004,'teal','xy',20)
tube('Reel seat',[(0,0,-.027),(0,0,.054)],.024,'iron',16)
for z in (.31,.56,.81,1.06,1.31,1.56,1.81,2.03):
 r=.037-(z-.31)*.013
 tube('Guide foot',[(0,0,z-.028),(0,-r,z)],.0035,'edge',6)
 ring('Ceramic line guide',(0,-r,z),r,.003,'edge','xy',16)
 for dz in (-.035,.017):ring('Thread binding',(0,0,z+dz),max(.004,.015-(z-.28)*.0064),.002,'teal','xy',12)
# Detailed forward-facing spinning reel hangs below the seat.
tube('Reel stem',[(0,-.015,.015),(0,-.08,-.025),(0,-.13,-.005)],.015,'teal',10)
box('Gear case',(0,-.14,-.015),(.095,.085,.10),'teal',.018)
for x in (-.05,.05):
 ring('Gear cover',(x,-.14,-.015),.032,.006,'brass','yz',20)
 bolt((x,-.14,-.015),'x',.013)
tube('Spool axle',[(0,-.14,.015),(0,-.14,.15)],.014,'iron',12)
for z in (.07,.145):ring('Machined spool lip',(0,-.14,z),.058,.006,'brass','xy',24)
for i in range(13):ring('Line wound on spool',(0,-.14,.077+i*.005),.049,.0025,'ivory','xy',12)
ring('Rotor bail',(0,-.14,.115),.075,.004,'edge','xy',32)
tube('Bail arm',[(-.073,-.14,.11),(-.065,-.14,.03),(0,-.14,.03)],.007,'teal',8)
export('RodBody')
begin('ReelCrank')
tube('Folding reel handle',[(0,0,0),(-.025,0,0),(-.045,-.06,0),(-.065,-.06,0)],.008,'brass',10)
tube('Rubber crank knob',[(-.067,-.06,0),(-.106,-.06,0)],.017,'leather',12)
export('ReelCrank')
# Neutral open glove, fingers are skinned at runtime from a reusable phalanx.
begin('GlovePalm')
box('Tailored glove palm',(0,0,.008),(.083,.036,.105),'glove',.016)
box('Palm leather pad',(0,-.018,.012),(.069,.008,.071),'leather',.009)
box('Backhand protection',(0,.019,.012),(.062,.010,.07),'teal',.008)
for x in (-.025,0,.025):tube('Backhand seam',[(x,.025,-.011),(x,.025,.038)],.0015,'ivory',5)
box('Cuff', (0,0,-.058),(.079,.046,.03),'leather',.009)
box('Wrist strap',(0,.028,-.056),(.075,.009,.016),'teal',.004)
box('Strap buckle',(.025,.035,-.056),(.018,.008,.021),'brass',.002)
# Sleeve tapers into cuff and stops short of the elbow; no fake camera-connected shoulders.
sleeve=lathe('Tapered jacket forearm',[(-.46,.073),(-.42,.073),(-.32,.059),(-.17,.044),(-.073,.035)],'teal',16)
sleeve.rotation_euler.x=math.pi/2
tube('Sleeve piping',[(-.041,.028,-.21),(-.035,.024,-.079)],.002,'ivory',6)
export('GlovePalm')
begin('GloveFinger')
box('Rounded leather phalanx',(0,0,.0155),(.018,.024,.031),'glove',.007)
box('Knuckle protection',(0,.012,.012),(.016,.004,.016),'leather',.003)
export('GloveFinger')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'FishingGear.blend'))
(SOURCE/'mesh-budget.json').write_text(json.dumps(STATS,indent=2)+'\n',encoding='utf-8')
print('FISHING_GEAR_PASS '+json.dumps(STATS))
