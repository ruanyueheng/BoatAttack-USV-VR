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
# One closed skin connects exterior, gunwale, inward-facing cockpit and floor.
skin=shell('Continuous thick hull',[(.70,-.28),(.86,.10),(1,.68),(.94,.68),(.75,.24)],'teal',True)
import bmesh
bm=bmesh.new();bm.from_mesh(skin.data)
assert all(e.is_manifold for e in bm.edges), 'Hull skin must be watertight'
inner=[p for p in skin.data.polygons if p.center.z>.25 and p.normal.dot(Vector((p.center.x,p.center.y,0)))<-.1]
assert len(inner)==len(outline), 'Cockpit wall normals must face inward'
bm.free()
attr=skin.data.color_attributes.active_color
for poly in skin.data.polygons:
    if poly.index>=2*len(outline) and poly.index!=4*len(outline):
        for i in poly.loop_indices: attr.data[i].color_srgb=(*PALETTE['paint'][:3],1)
shell('Rubber rubbing strake',[(1,.59),(1.015,.65),(1,.70)],'iron')
tube('Rounded gunwale',[(x,.72,z) for x,z in outline],.034,'ivory',8,True)
for i in range(9):
    x=(i-4)*.15
    box('Oiled floor board',(x,.26,-.15),(.143,.045,2.6),'wood_light',.005)
for z,w in [(-1.15,.87),(-.05,.91),(.9,.79)]:
    for side in [-1,1]:
        tube('Cream cockpit rib',[(side*w*.78,.29,z),(side*w*.96,.61,z)],.025,'ivory',6)
box('Bow storage deck',(0,.65,1.62),(.52,.06,.57),'wood_light',.01)
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
box('Outboard enamel', (0,.93,-1.94),(.35,.06,.33),'teal',.014)
tube('Tiller arm',[(0,.71,-1.8),(.24,.74,-1.3)],.022,'iron',8)
tube('Outboard shaft',[(0,.50,-1.96),(0,-.95,-1.96)],.06,'edge',8)
for side in [-1,1]:
    box('Outboard transom bracket',(side*.16,.57,-1.83),(.055,.25,.18),'edge',.009)
box('Outboard propeller',(0,-.93,-2.04),(.28,.065,.055),'iron',.008)
tube('Deck lantern post',[(.6,.68,-.85),(.6,1.52,-.85)],.022,'brass',8)
tube('Lantern hanging arm',[(.6,1.52,-.85),(.6,1.52,-1.0)],.021,'brass',8)
box('Lantern glass',(.6,1.35,-.85),(.115,.20,.115),'ivory',.012)
for y in [1.23,1.47]:box('Lantern cap',(.6,y,-.85),(.18,.035,.18),'iron',.01)
for dx in [-.065,.065]:
    for dz in [-.065,.065]:tube('Lantern protective cage',[(.6+dx,1.24,-.85+dz),(.6+dx,1.46,-.85+dz)],.008,'brass',6)
export('SoloSkiff')
assert STATS['SoloSkiff']['triangles'] < 6500, STATS
begin('SkiffHull')
shell('Closed buoyancy hull',[(.70,-.28),(.86,.10),(1,.68)],'teal',True)
export('SkiffHull')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'SoloSkiff.blend'))
(SOURCE/'mesh-budget.json').write_text(json.dumps(STATS,indent=2)+'\n',encoding='utf-8')
print('SOLO_SKIFF_PASS '+json.dumps(STATS))
