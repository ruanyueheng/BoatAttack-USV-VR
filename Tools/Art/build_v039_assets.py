"""Run selected stages in the live Blender MCP session. Preserve all other scenes."""
from pathlib import Path
import bpy
import importlib.util
import sys

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'ArtSource/V039'
ART.mkdir(parents=True, exist_ok=True)
SCENERY = ['HomeLandscaping', 'SanctuaryWind', 'SanctuaryGrove', 'SanctuaryDrydock',
           'SanctuaryStars', 'WindBridge0', 'WindBridge1', 'WindBridge2', 'RootBridge',
           'Galleon', 'Windmill', 'AncientTree', 'ObservatoryExterior']


def scenery_session():
    if 'v039_scenery' in sys.modules:
        return sys.modules['v039_scenery']
    spec = importlib.util.spec_from_file_location('v039_scenery', Path(__file__).with_name('build_v035_assets.py'))
    kit = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = kit
    spec.loader.exec_module(kit)
    kit.SOURCE = ART / 'Scenery'
    kit.SOURCE.mkdir(exist_ok=True)
    kit.SCENE = 'V039 grounded coastal scenery'
    kit.setup()
    return kit


def scenery(names):
    kit = scenery_session()
    bpy.context.window.scene = bpy.data.scenes[kit.SCENE]
    for name in names:
        assert name in SCENERY
        kit.build(name)
    bpy.ops.wm.save_as_mainfile(filepath=str(ART / 'v039-development.blend'))
