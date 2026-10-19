# V035 authored coastal game kit

77 source meshes, 1,030,912 unique triangles. The kit contains original island landmarks, four structurally distinct sanctuary levels, tabletop details, an expanded home island, cottage furniture, garden fixtures and turnip growth stages. It is a reusable source gallery; Unity assembles and animates these assets at runtime.

- `coastal-craftsmanship-kit.glb`: portable geometry, UVs and embedded PBR images; static, no baked game animation.
- `coastal-craftsmanship-v035.blend`: editable owned scene; unrelated startup Scene is preserved.
- `export-validation.json`: exact FBX filenames, triangle counts and SHA256 hashes consumed by Unity.
- `portable-validation.json`: actual GLB header, attributes, finite coordinates, image count, triangle total and FBX identity checks.
- `roundtrip-validation.json`: actual reimported GLB against the Blender sources, including bounds, geometry totals, UVs and materials.
- `level-marker-manifest.json`: route, puzzle, entry and device markers used by the live scenes.

All concept and multiview sheets were generated with built-in imagegen; the exact prompts are adjacent text files. These are original design references, not finished gameplay renders. Earlier discarded layouts are retained locally and are excluded from the live rooms and release documentation.

See [production notes](BUILD_NOTES.md), [play guide](../../Docs/PLAY_V035.md) and [game validation](../../Docs/VALIDATION_V035.md).
