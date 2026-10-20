# V035 production notes

The task began with two tabletop animation defects and visually repetitive islands/rooms, then expanded to a finer home island and a playable farming loop. The final spaces were rebuilt around different traversal and puzzle logic: ascending chalk islets, a root gorge with connected sunlight, a draining two-deck wreck with held cargo movement, and a raised observatory with continuous star aiming.

## References and authored assets

[The Little Prince game website](https://littleprince.dev/) and the user's earlier home/king screenshots were style and craft-quality references. No source mesh or texture was extracted from that game. Built-in imagegen produced the four-scene redesign board, four separate multiview sheets, a home/garden multiview sheet, and the refined material atlas. Every selected image and its exact prompt is saved here. Some earlier concept renders and the discarded courtyard layout are kept as history and do not describe the final game.

Farming design follows the daily soil/seed/water/harvest loop documented by [STORY OF SEASONS](https://www.storyofseasons.com/PoOT/buildyourfarm/) and its [official farming manual](https://www.storyofseasons.com/awl/manuals/switch/09/). This implementation deliberately uses one hardy crop, three watered nights, twelve plots, a refillable can, renewable harvest seeds and a produce crate. These numbers are original balancing choices, not copied claims about those games.

Blender 5.2.2 and the live Blender MCP produced the actual geometry locally; no paid Tripo or Hunyuan membership was used. The create-3d-model production workflow guided modeling, materials, export and visual repair. Jev `jev-latest` resolved to `jev-1.13.0` for a textual check of spatial/puzzle distinctness; that check did not certify model beauty or gameplay.

## Geometry and integration

The portable kit has 77 original meshes and 1,030,912 unique triangles. The Unity FBX files total about23MB; the GLB is80,545,328bytes with three embedded PBR images. These are asset-library counts; scene instancing changes the rendered workload and these counts do not certify PICO performance.

Coordinates are metre-scale, Y up in authoring helpers. Core assets convert to Blender with `(x,-z,y)`; this project's FBX import mirrors X, so exported gameplay markers determine room positions. The home module cancels that reflection explicitly to retain existing home/furniture coordinates. The home heightfield forces upward faces after normal reconstruction. Hard surfaces have filtered persistent collision meshes where appropriate; small flowers and decorative rope do not block walking. Home door, room floor, bedside and permanent facilities retain their existing gameplay coordinates.

The refined diffuse atlas is native1254×1254; normal and packed metal/roughness maps are2048×2048. Unity uses its configured import sizing and separate packed metallic/smoothness map. All visible mesh surfaces have UVs. The shared material covers masonry, wood, slate, brass, fabric and foliage; runtime foliage wind weights live in FBX vertex alpha. The GLB is a static portable model, without the Unity shader animation.

The cottage has individual raised slate shingles, curved bargeboards, supported trusses, shuttered windows, flower boxes, a masonry chimney and plank floors. New furniture adds shaped bedding/cushions, quilt seams, book spines, drawers, handles, sink plumbing, stove rings and a woven rug. The garden is physically connected to the porch and jetty; its gates and plot paths were walked using the same character controller as production.

## Reproduction and evidence

`Tools/Art/build_v035_assets.py` loads the level and home geometry modules. Build only in the owned V035 scene; never clear unrelated Blender work. The source library is exported to FBX for Unity and to an embedded GLB. `validate_v035_roundtrip.py` imports the actual portable file into a temporary owned scene, compares each mesh, then removes only its own imported data. `validate_v035_exports.py` validates the real GLB and every FBX hash. Current roundtrip bounds error is0metres.

Unity installation: `Tools/Run-UnityValidation.ps1 -Suite FishingV035Art`. Physical farming/home validation: suite `FishingV035Farm`; four complete island/room routes: `FishingV035Levels` or the V019 regression. `Tools/Run-V035Validation.ps1` runs the release regression set. `Tools/Run-FishingBuilds.ps1` creates and checks Windows and ARM64 PICO artifacts. Standalone capture verifies80 full-HUD cases, including the new garden and mature-crop states.

Actual game images belong in `Docs/Images/V035`. Concept images and Blender review images are never presented as proof of the game's final appearance. Physical PICO comfort, tracking calibration and long-duration performance remain outside desktop/structural checks.

The final V035 UI uses a day/night-tinted neutral reading mask, the original OFL Ma Shan Zheng brush-script font, actual glyph-contour gold strokes and separate five-theme fill colours. Filled catch cards follow the same light/day and dark/night palette. Font provenance is in `../Fonts/V035/`; actual screenshots and pixel checks are in the validation document.
