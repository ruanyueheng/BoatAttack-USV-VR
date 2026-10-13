# v022 source art

## Base heads and hairstyles

- Author: Quaternius.
- Pack: [Universal Base Characters Standard](https://quaternius.com/packs/universalbasecharacters.html).
- Licence: CC0 1.0, preserved in `../V019/Characters/License_Standard.txt`.
- Original body/head and hair FBX: `../V019/Characters/` (unchanged); original texture subset: `Textures/`.
- Source archive SHA-256: `fdbf1804c90dfc1ea03e992bff7da2dfd1a79318e13270a660180f9308455f40`.
- Exact texture archive entries and hashes: `texture-sources.json`.

## Authored clothing

- Author: Quaternius.
- Pack: [Modular Character Outfits – Fantasy Standard](https://quaternius.itch.io/modular-character-outfits-fantasy).
- Retrieved: 2026-10-13, public free Standard download, no purchase.
- Licence: CC0 1.0, preserved verbatim in `Outfits/License_Standard.txt`.
- Selected complete FBX: Female/Male Peasant and Female/Male Ranger; source colour/normal maps and readme: `Outfits/`.
- Archive CRC was verified before extracting the selected files. Exact archive SHA-256, selected paths and per-file SHA-256: `Outfits/source-manifest.json`.

## Derivatives

`Tools/Art/build_v022_residents.py` combines source heads with authored outfits. It removes unused hood, alternative belt and pauldrons, poses source arms, preserves UVs, packs the source maps into 2048 atlases, limits clothing geometry while retaining head topology, and exports static near/far FBX. One material per active LOD; far LOD around5500 triangles. `residents-manifest.json` records the final budgets and chosen variants.

The engineering scene `Residents-v022.blend` embeds its used atlas images. Source files are not replaced by this scene. Runtime positions, conversations and rewards remain in the existing resident system.

`procedural-fish-snapshots.json` is derived from the project's existing `FishingMeshBuilder.Fish` geometry. `Tools/Art/render_v022_fish.py` renders its actual vertex-coloured meshes into provisional transparent catalog portraits. The supplied eight Hunyuan models and their originals are unchanged. Four additional final models are requested from the user through `Docs/HUNYUAN_V022_PROMPTS.md`.
