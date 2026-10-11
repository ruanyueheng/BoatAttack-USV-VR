# FantasyGirl cashier — user-supplied asset

The user supplied `FantasyGirl.blend` and `FantasyGirl.fbm.zip` for use as the shop cashier. The source blend is copied without modification; texture JPGs are extracted unchanged. No asset licence or author attribution was supplied. This is not an original project-created character. Confirm redistribution/commercial-use terms before uploading source or derived files to any GitHub repository or shipping them.

## Reproduction

Open this source via Blender background mode with auto-execution disabled and run `Tools/Art/import_fantasy_cashier.py`. Run the Unity menu **BoatAttack > Fishing > Build FantasyGirl cashier assets** afterwards. The exporter only selects the character mesh, not source cameras or floor. It creates posed FBX LODs and textures under `Assets/Resources/FishingCashier/FantasyGirl`; the Unity builder creates the URP material, reusable `Cashier.prefab`, and a nested instance under the existing first clerk marker in `BeachTackleShop.prefab`. The original scene and shop layout are not regenerated.

- Original: 997,058 triangles, unrigged T-pose, one UV-mapped material.
- Derived static pose: shoulder axes calibrated to the original deltoid centres, lowered upper arms, separate upper-arm/forearm segments with localized elbow creases, and hands gently overlapping at the lower abdomen, 1.98 m tall (about 15% larger than the previous 1.72 m), feet at local Y=0, Unity-forward +Z. This is mesh posing, not skeletal rigging or an animation system.
- Near/far meshes: about 70k / 22k triangles (exact figures in import-report.json); one renderer/material per active LOD. VR performance has not been certified.
- Four supplied 4096px textures: base colour, metallic, tangent-space normal, roughness. Unity uses 2048px mipmapped imports; normal is imported as Normal Map, metallic+smoothness as linear data. Smoothness alpha is `1 - roughness`; metalness is the red channel. Original JPGs remain available here.
- No runtime mesh generation, colliders, NavMesh agent, separate economy or fabricated humanoid Animator. Purchase and saves continue through the existing shop controller.

## v0.13 integration (2026-10-10)

Restored derived Resources assets from `origin/feature/v011-cashier-integration` commit `8f609ab50b94aed9a35a708eaa11ae9520f02152`. The source Blender project and legacy importer scripts were not copied into this working tree. All five binary Resources assets were verified against their upstream Git LFS SHA-256 IDs. The prefab script reference alone was adapted to the existing v13 `FishingShopCashier` GUID (`14b852fc1852a22670fed277a200e2b6`). The v13 explicit builder/validator is `Assets/Scripts/Editor/FishingCashierValidation.cs`; the original shop layout, trading and v8 saves remain in use. This integration does not establish a licence for redistribution.

## Local v0.13 merge

The local integration retains the original Blender source, texture sources and legacy import/preview tools from the cashier branch. Runtime assets and the cashier script GUID follow main v0.13; the existing shop's nested cashier is preserved and creation remains deduplicated.
