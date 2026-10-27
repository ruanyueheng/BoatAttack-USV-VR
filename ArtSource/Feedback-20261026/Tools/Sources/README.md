# Feedback tool source assets

This folder contains only the unmodified models needed by
`Tools/Art/build_feedback_tools.py` and their external palette images.
The two kits keep separate `Textures/colormap.png` files because their palettes
are different. No downloaded ZIP or complete asset library is required.

- `Kenney-Survival/`: Survival Kit 2.0, `tool-axe.glb` and `tool-hammer.glb`.
  Source: <https://kenney.nl/assets/survival-kit>.
- `Kenney-Pirate/`: Pirate Kit 2.1, `tool-shovel.glb` and `chest.glb`.
  Source: <https://kenney.nl/assets/pirate-kit>.
- Each kit includes its original `License.txt`: Creative Commons Zero
  (CC0 1.0). The Pirate licence identifies the actual source revision as 2.1.
- The rake and saw are project originals constructed by the script.

Run the script with Blender from a checkout containing this folder. It retains
the existing model sizing, transforms, vertex-colour bake and chest lid hinge;
it writes generated FBX files to `../Exports` and editable Blender files to
the parent `Tools` folder. This source-path repair did not regenerate them.

Character actions use a separate pipeline:
`Tools/Art/import_feedback_interactions.py` reads the already tracked
`ArtSource/Characters/BeachCharacter.glb` and
`Assets/Resources/FishingCharacter/BeachCharacter.fbx`. The source attribution
is already tracked at `ArtSource/Characters/ATTRIBUTION.md`. Include that new
script, its generated `Animations/InteractionAnimations.fbx` and manifest,
the runtime `InteractionAnimations.fbx` with its Unity metadata, and
`FishingInteractionAnimationBaker.cs` with its metadata when packaging this
change. The Unity baker produces the compatible `.anim` files used by the
9S character; those files and their metadata must also be included.
