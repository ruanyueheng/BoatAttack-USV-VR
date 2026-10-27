# Two-islet coastal art

This is project-original shelter geometry built with the already tracked V035 Coastal atlas and its geometry helpers. The runtime uses the same Coastal, Foliage and CoastGround materials as the detailed home island. No external asset or new texture is required.

Build: run `Tools/Art/build_islet_camp.py` in an independent background Blender 5.2 process. It produces the runtime `Assets/Resources/FishingV040/IsletCamp.fbx` and the editable `islet-camp-v001.blend` here. The Blender source packs the existing atlas images. FBX is the Unity deliverable; the source is not a replacement for runtime import verification.

The shelter has 4640 triangles, one material and atlas UVs. Its posts extend about 1.1 metres below the local ground origin to follow the sloped coastal site. The above-ground roof and awning are about 3.6 metres high. It is placed at local (-18.5,-15.5), outside the challenge arena. The runtime preserves the original island terrain vertices/triangles, height function, origin, northern berth and known White Sail marine approach.

`camp-validation.json` covers the new model. `reimport-validation.json` records a separate FBX re-import of the new shelter and the existing V035 props. The new model has no degenerate triangles. The reused, unchanged DuneGrass source has 512 zero-area ribbon-tip triangles; these are recorded rather than described as a new clean export. Both the original and re-imported shelter renders have been visually checked for shape, UV and material continuity.

All runtime/source/editor assemblies compile offline against Unity 2022.3.62f1 references, with zero errors and only existing warnings. This does not confirm Unity imports, static batching cost, final scene appearance, actual landing/treasure/combat/marine reachability, native frame rate or headset performance. Those remain integration acceptance gates.
