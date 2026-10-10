"""Original 4 m open fishing skiff; Unity-metre authoring and the existing vertex palette."""
from pathlib import Path
base = Path(__file__).with_name('generate_harbor_props.py')
exec(compile(base.read_text(encoding='utf-8-sig').split('\ncrate()\n')[0], str(base), 'exec'))
# Bake Unity's FBX forward-axis conversion into the authoring frame.
def U(v): return Vector((-v[0],v[2],v[1]))
OUT = ROOT / 'Assets/Resources/FishingBoat'
SOURCE = ROOT / 'ArtSource/SoloSkiff'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
outline = [(-.78,-1.85),(-.96,-1.05),(-.98,.35),(-.78,1.25),(-.4,1.85),(0,2.1),(.4,1.85),(.78,1.25),(.98,.35),(.96,-1.05),(.78,-1.85)]

def shell(name, rings, color, caps=False):
    vs = [U((x*s,y,z*s)) for s,y in rings for x,z in outline]
    n=len(outline)
    faces=[(i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j) for i in range(len(rings)-1) for j in range(n)]
    if caps: faces += [tuple(reversed(range(n))),tuple((len(rings)-1)*n+j for j in range(n))]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vs,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);COL.objects.link(obj);PARTS.append(obj)
    return finish(obj,color)

begin('SoloSkiff')
shell('Teal clinker hull',[(.70,-.28),(.86,.10),(1,.68)],'teal',True)
# The cockpit floor is above the closed buoyancy shell; inner walls cover its top cap.
# Remove the upper cap so the actual cockpit remains open.
import bmesh
bm=bmesh.new();bm.from_mesh(PARTS[-1].data);bm.faces.ensure_lookup_table()
bmesh.ops.delete(bm,geom=[bm.faces[-1]],context='FACES');bm.to_mesh(PARTS[-1].data);bm.free()
shell('Cream inner hull',[(.75,.24),(.94,.67)],'paint')
shell('Rubber rubbing strake',[(1,.59),(1.015,.65),(1,.70)],'iron')
tube('Rounded gunwale',[(x,.72,z) for x,z in outline],.034,'ivory',8,True)
for i in range(9):
    x=(i-4)*.15
    box('Oiled floor board',(x,.26,-.15),(.143,.045,2.6),'wood_light',.005)
for z,w,y in [(-.7,1.65,.72),(1.13,1.3,.68)]:
    for dz in [-.11,0,.11]:box('Slatted bench',(0,y,z+dz),(w,.06,.103),'wood_light',.006)
    for x in [-w*.38,w*.38]:box('Bench support',(x,.47,z),(.055,.45,.25),'teal',.004)
box('Side fishing seat',(.5,.86,.18),(.42,.08,.4),'wood_light',.01)
box('Seat pedestal',(.5,.54,.18),(.09,.57,.09),'teal',.006)
for x,z in [(.55,-.28),(-.45,.55)]:
    box('Catch box shelf',(x,.68,z),(.7,.06,.5),'wood_light',.008)
    for dx in [-.27,.27]:box('Shelf leg',(x+dx,.46,z),(.045,.42,.37),'teal',.005)
tube('Rod holder socket',[(.8,.58,.25),(.8,.72,.25)],.04,'iron',8)
for side in [-1,1]:
    for z in [-1.35,.85]:
        tube('Cleat stem',[(side*.89,.71,z),(side*.89,.78,z)],.02,'iron',6)
        tube('Mooring cleat',[(side*.89,.78,z-.1),(side*.89,.78,z+.1)],.018,'iron',6)
    for z in [-.95,.6]:
        tube('Fender lanyard',[(side*.97,.70,z),(side*1.01,.44,z)],.009,'rope',6)
        tube('Cream fender',[(side*1.02,.20,z),(side*1.02,.49,z)],.065,'paint',8)
box('Compact outboard', (0,.69,-1.94),(.34,.42,.32),'iron',.035)
box('Outboard enamel', (0,.87,-1.94),(.35,.06,.33),'teal',.014)
tube('Tiller arm',[(0,.71,-1.8),(.24,.74,-1.3)],.022,'iron',8)
tube('Outboard shaft',[(0,.50,-1.96),(0,-.33,-1.96)],.06,'edge',8)
export('SoloSkiff')
assert STATS['SoloSkiff']['triangles'] < 6500, STATS
begin('SkiffHull')
shell('Closed buoyancy hull',[(.70,-.28),(.86,.10),(1,.68)],'teal',True)
export('SkiffHull')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'SoloSkiff.blend'))
(SOURCE/'mesh-budget.json').write_text(json.dumps(STATS,indent=2)+'\n',encoding='utf-8')
print('SOLO_SKIFF_PASS '+json.dumps(STATS))
