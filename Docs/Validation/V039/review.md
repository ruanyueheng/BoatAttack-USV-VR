What this change does: It makes the shipwreck bridge state visible and its rails physical, fixes swimming exits and repeated winder use, and grounds home props. It adds the generated Hunyuan roast, generated Meowa sizzle, and bounded coastal particles.

Looks good. Ship.

Assumed load: the existing single-player desktop/VR game. Smoke and ember pools are capped at 64 and 20 particles and stop when distant or paused.

Checked: shared ground-query callers, relocation and save guards, closed/filled/drained/cleared bridge states, actual character collision at joins and rails, full island routes, tool/material/icon callers, repeated craft cancellation, audio mix registration, resource cleanup, generated asset provenance and source integrity. The late unlocked-appearance regression first failed on the previous implementation, then passed after the bridge was kept submerged until cargo release. Incomplete cargo progress no longer rounds to 100%.

Not checked: physical PICO controller tracking, wearer comfort, and long-running device frame time. The remote PR review is requested separately after publication.
