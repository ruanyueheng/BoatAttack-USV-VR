# Supplied hunting rifle

`ak-47.blend`, `diffuse.png` and `diffuse.tiff` are the user's supplied originals.
The original Blender file is not modified. Run Blender with `--disable-autoexec`
and `Tools/Art/import_hunting_rifle.py` to export `Rifle.fbx`, then run
`BoatAttack.EditorTools.FishingHuntingAssets.Build` in Unity batch mode.

The model is presented as the game's single-shot hunting rifle. UVs and the
supplied diffuse PNG are retained. The authored mesh is 0.66 m long, with a
slightly wider silhouette for readability; runtime uses a consistent 0.82 scale
in first and third person to fit the existing character's short arms. The
resource prefab owns its URP material, and the original textures remain here.
