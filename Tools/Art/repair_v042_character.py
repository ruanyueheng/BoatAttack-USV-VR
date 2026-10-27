"""Repair supplied sleeve skin weights without changing the proven rig, glove surface or topology."""
from pathlib import Path
import hashlib
import json
import shutil
import bpy
from collections import Counter

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "ArtSource/V042/Character"
RUNTIME = ROOT / "Assets/Resources/FishingCharacter/9SCharacter.fbx"
ART.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT / "ArtSource/V030/9s-adapted.blend"), load_ui=False)
body = bpy.data.objects["Beach_Body"]
rig = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
cloth = {v for face in body.data.polygons if body.data.materials[face.material_index].name == "9SClothes" for v in face.vertices}
positions = [tuple(v.co) for v in body.data.vertices]
before = [{body.vertex_groups[g.group].name: g.weight for g in v.groups} for v in body.data.vertices]
edge_counts = Counter(edge for face in body.data.polygons for edge in face.edge_keys)
rim = {v for edge, count in edge_counts.items() if count == 1 for v in edge}
fitted = set()
for side in (".L", ".R"):
    wrist = rig.matrix_world @ rig.data.bones["Wrist" + side].head_local
    elbow = rig.matrix_world @ rig.data.bones["LowerArm" + side].head_local
    shoulder = rig.matrix_world @ rig.data.bones["UpperArm" + side].head_local
    upper_axis = (elbow - shoulder).normalized()
    upper_length = (elbow - shoulder).length
    for vertex in body.data.vertices:
        arm_weight = sum(w for n, w in before[vertex.index].items() if n.endswith(side) and n.startswith(("Shoulder", "UpperArm", "LowerArm", "Wrist", "Thumb", "Index", "Middle", "Ring", "Pinky")))
        if vertex.index not in cloth or arm_weight < .25:
            continue
        point = body.matrix_world @ vertex.co
        along = (point - shoulder).dot(upper_axis)
        radial = point - shoulder - upper_axis * along
        if -.025 < along < upper_length and radial.length > .065:
            vertex.co = body.matrix_world.inverted() @ (shoulder + upper_axis * along + radial.normalized() * .065)
            fitted.add(vertex.index)
    axis = (wrist - elbow).normalized()
    length = (wrist - elbow).length
    for vertex in body.data.vertices:
        if vertex.index not in cloth or sum(w for n, w in before[vertex.index].items() if n.endswith(side) and n.startswith(("LowerArm", "Wrist", "Thumb", "Index", "Middle", "Ring", "Pinky"))) < .55:
            continue
        point = body.matrix_world @ vertex.co
        along = (point - wrist).dot(axis)
        if not -length * 1.1 < along < .04:
            continue
        radial = point - wrist - axis * along
        if vertex.index in rim and -.08 < along:
            along = -.025
        radius = .037 + .018 * min(1, max(0, -along / length))
        if radial.length > radius:
            radial = radial.normalized() * radius
        vertex.co = body.matrix_world.inverted() @ (wrist + axis * along + radial)
        fitted.add(vertex.index)
repaired = []
for vertex in body.data.vertices:
    if vertex.index not in cloth:
        continue
    changes = {}
    for group in list(vertex.groups):
        name = body.vertex_groups[group.group].name
        if name.startswith(("Wrist", "Thumb", "Index", "Middle", "Ring", "Pinky")):
            target = "LowerArm" + name[-2:]
            assert target in body.vertex_groups, name
            changes[target] = changes.get(target, 0) + group.weight
            body.vertex_groups[group.group].remove([vertex.index])
    for name, weight in changes.items():
        body.vertex_groups[name].add([vertex.index], weight, "ADD")
    if sum(changes.values()) > 1e-8:
        repaired.append(vertex.index)
assert len(repaired) > 100, len(repaired)
assert all(tuple(v.co) == positions[v.index] for v in body.data.vertices if v.index not in fitted)
for vertex in body.data.vertices:
    current = {body.vertex_groups[g.group].name: g.weight for g in vertex.groups}
    if vertex.index not in cloth:
        assert current == before[vertex.index], "Calibrated gloves must stay unchanged"
    else:
        assert abs(sum(current.values()) - sum(before[vertex.index].values())) < 1e-5
        assert not any(n.startswith(("Wrist", "Thumb", "Index", "Middle", "Ring", "Pinky")) for n in current)
backup = ROOT / "Logs/V042/9SCharacter-v041.fbx"
if not backup.exists():
    shutil.copy2(RUNTIME, backup)
for obj in bpy.context.scene.objects:
    obj.select_set(obj == rig or obj.type == "MESH")
bpy.context.view_layer.objects.active = rig
rig.animation_data.action = next(a for a in bpy.data.actions if a.name.endswith("Idle"))
bpy.context.scene.frame_set(1)
bpy.ops.export_scene.fbx(filepath=str(RUNTIME), use_selection=True, object_types={"MESH", "ARMATURE", "EMPTY"},
    apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False, bake_anim=True,
    bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True, axis_forward="-Z", axis_up="Y", path_mode="STRIP")
bpy.ops.wm.save_as_mainfile(filepath=str(ART / "9s-sleeve-fixed.blend"))
report = {"source": "ArtSource/V030/9s-adapted.blend", "repaired_cloth_vertices": len(repaired),
          "gloves_unchanged": True, "fitted_cuff_vertices": len(fitted), "vertices": len(body.data.vertices),
          "runtime_sha256": hashlib.sha256(RUNTIME.read_bytes()).hexdigest()}
(ART / "sleeve-repair.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print("SLEEVE REPAIR", json.dumps(report))
