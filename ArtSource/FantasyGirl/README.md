# FantasyGirl cashier — user-supplied asset

The user supplied `FantasyGirl.blend` and `FantasyGirl.fbm.zip` for use as the shop cashier. The source blend is copied without modification; texture JPGs are extracted unchanged. No asset licence or author attribution was supplied. This is not an original project-created character. Confirm redistribution/commercial-use terms before uploading source or derived files to any GitHub repository or shipping them.

## Reproduction

Open this source via Blender background mode with auto-execution disabled and run `Tools/Art/import_fantasy_cashier.py`. Run the Unity menu **BoatAttack > Fishing > Build FantasyGirl cashier assets** afterwards. The exporter only selects the character mesh, not source cameras or floor. It creates posed FBX LODs and textures under `Assets/Resources/FishingCashier/FantasyGirl`; the Unity builder creates the URP material, reusable `Cashier.prefab`, and a nested instance under the existing first clerk marker in `BeachTackleShop.prefab`. The original scene and shop layout are not regenerated.

- Original: 997,058 triangles, unrigged T-pose, one UV-mapped material.
- Derived static pose: relaxed arms, 1.72 m tall, feet at local Y=0, Unity-forward +Z. This is mesh posing, not skeletal rigging or an animation system.
- Near/far meshes: about 70k / 22k triangles (exact figures in import-report.json); one renderer/material per active LOD. VR performance has not been certified.
- Four supplied 4096px textures: base colour, metallic, tangent-space normal, roughness. Unity uses 2048px mipmapped imports; normal is imported as Normal Map, metallic+smoothness as linear data. Smoothness alpha is `1 - roughness`; metalness is the red channel. Original JPGs remain available here.
- No runtime mesh generation, colliders, NavMesh agent, separate economy or fabricated humanoid Animator. Purchase and saves continue through the existing shop controller.
