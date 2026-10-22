# V036 hanging fixture source
Brass Diya Lantern by Bhargav Kubal, Poly Haven.
Source: https://polyhaven.com/a/brass_diya_lantern
License: CC0 1.0, https://polyhaven.com/license and https://creativecommons.org/publicdomain/zero/1.0/
Downloaded 2026-10-20 through the public files API; original URLs and MD5 values in Originals/brass-diya-files.json. Original glTF/bin and 1K diffuse/normal/ARM/opacity/emissive maps retained under Originals/BrassDiya.
This is a separate hanging fixture with chain suspension and star-shaped vents. No part of the player's handheld lantern is reused.
Adaptation: preserve geometry and UVs; repack opacity into diffuse alpha, roughness/metal into Unity metallic/smoothness. 12,742 triangles, three source objects, 1K maps. Shared alpha-cutout brass, transparent glass and emissive flame materials; real point lights use existing bounded home-light logic.
Reproduce: python Tools/Art/import_v036_lantern.py --textures; Blender5.2 background --python Tools/Art/import_v036_lantern.py; Unity suite FishingV036Art. Blender source and single-file GLB supplied as editable/portable source; the game consumes the validated FBX prefab.

V036 original starlight watering can: created in Blender from scratch with an ivory enamel shell, pale brass frame, sage glaze and original crescent/laurel relief. It replaces the old patinated can rather than recolouring it. Source: Tools/Art/build_v036_watering_can.py; 7956 triangles; four shared materials. Grip/spout/opening stay at existing measured interaction anchors. Editable blend and portable GLB retained.

V036 deep well: HomeLand now has a real circular opening; inward masonry shaft/floor reaches -5.42m local, water level -.35m local (1.23m below stone lip). Export only HomeLand/GardenFixtures with Tools/Art/rebuild_v036_well.py; matching game collision is refreshed by FishingV036Art. Bucket dwell submerges below the actual water before raising. No ground-coloured plane remains inside the bore.


## 保存的模型文件
- [原创星月水壶 GLB](starlight-watering-can.glb)、[可编辑 Blender 源文件](starlight-watering-can-v036.blend)、[模型预览](starlight-watering-can-preview.png)。
- [独立悬挂灯笼 GLB](hanging-lantern.glb)、[Blender 适配源文件](hanging-lantern-v036.blend)。
- [深井与开孔地面 GLB](Well/deep-well-and-land.glb)、[Blender 源文件](Well/deep-well-v036.blend)。
