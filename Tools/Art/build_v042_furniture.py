"""Rebuild only the revised desk and cabinet with the existing coastal atlas and exporters."""
from pathlib import Path
import json
import runpy
import bpy

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "ArtSource/V042/Furniture"
ART.mkdir(parents=True, exist_ok=True)
kit = runpy.run_path(str(ROOT / "Tools/Art/build_v035_assets.py"))
api = kit["build"].__globals__
api["SOURCE"] = ART
api["setup"]()
for name in ("HomeDesk", "HomeCabinet"):
    api["build"](name)
for obj in api["MODELS"].values():
    obj.hide_render = False
bpy.ops.wm.save_as_mainfile(filepath=str(ART / "cedar-furniture-v042.blend"))
print("FURNITURE", json.dumps(api["REPORT"]))
