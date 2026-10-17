# Open issues — to fix later

Parked deliberately. The sword was built first.

---

## 1. Performance / stuttering (unresolved)

The game stuttered heavily in the editor. Three real fixes landed:

- Removed 3 point-light shadow casters — each point light renders 6 shadow maps, so that was 24
  extra scene passes per frame. Down to 1 shadow caster (the directional moon).
- Removed static batching — it broke `EmberEdgeGlow`'s edge mask and flooded every surface with glow.
  SRP Batcher handles batching instead; the shader does declare `UnityPerMaterial`.
- Halved the torch light count (22 → 11) and dropped per-object light loop from 4 to 3.

**Still unverified.** A WebGL build was produced but never run in a browser. That is the only
honest measurement — the editor runs several times slower. If it still stutters in a build, open
DevTools and check whether the main thread is blocked by script or the GPU is saturated; that
determines whether to look at scripts or rendering, which are different problems.

**Next suspect if it persists:** 384 shader variants are shipped. A Shader Variant Collection would
cut build time and download size substantially.

## 2. Rewind — unverified behaviour

Built and compiling, never observed working. Specific unknowns:

- Does health roll back correctly? Damage after the rewound-to moment should be undone, damage
  before it should stay.
- Does the volume shift actually read as green now the profiles are wired?
- `rewindSpeed 1.25` / `slowMotionScale 0.85` / `cooldown 2.0` are tuning guesses. `cooldown 2.0`
  is a test value — raise it before shipping.
- The animator is restored by state hash and normalised time, so it **snaps** rather than blends.
  Invisible at speed, visible at 1x.
- Traversal states are not resumed — you exit a rewind in normal movement even if you were
  wall-running or sliding when it started.
- Enemies are frozen via `FindObjectsByType` on every rewind. Fine at this scale, allocates.

## 3. Orb pickup — fixed but unconfirmed

The orb now deactivates outright on pickup (`SetActive(false)`) rather than relying on renderers,
because the halo was a sibling of the visual child and survived the first fix. All three orbs have
respawn disabled, so they are permanently consumed.

**The previous "can't collect" was `TimeRewindAbility` never being on the Player prefab.** That is
fixed. Unclear whether the smoke test was run after the fix.

## 4. WebGL build — created, never run

`Builds/WebGL` exists: 54.6 MB, 46.01 MB data + 8.48 MB wasm, Gzip, Linear, stripped, MainMenu and
Freerun only.

**Never loaded in a browser.** The 404 the user hit was a relative-path mistake in the Python server
command, not a build fault.

Build size is large. Biggest contributors: 150 s music track, 2048px enemy textures, 93.8 MB Ch44
FBX. `City Rooftop Night Sphere.fbx` (92.4 MB) is **unused** — only the PNG beside it is used.

## 5. Repo size — 1.1 GB

Over GitHub's recommended 1 GB. Not broken, but clones are slow and it should not grow much more.
The unused skybox source FBX is the cheapest 92 MB to reclaim.

## 6. Ledge climb — retimed, unverified

The clip had 0.9 s of dead tail after the climb finished; trimmed to 1.07 s and the animator state
retimed to 1.5x. `climbTime` and the animator speed **must stay in sync** or the drift returns.

## 7. Swing poles — fixed, unverified

Eight of fifteen bars were missing the `SwingPole` marker, so `FindPole` could not see them.

## 8. Hit feedback — implemented, unverified

Hit-stop, shake, spark, flash, SFX. Enemies always react; the player rolls per-channel chances
(30/30/35%) with reduced shake. `hitStopScale 0.55` is deliberately gentle — at 0.05 it read as a
freeze rather than impact.

## 9. Enemy death

Fixed: the death trigger was losing a race with the hurt trigger on the same frame, and nothing was
cleaned up. `OnDied` now plays the death state directly and destroys after 1.92 s.

## 10. Builder coverage

`FreerunBuilder` now creates the HUD, the skybox and both volume profiles. It does **not** create:

- the `NavMeshSurface` on the level root
- the enemies
- the rewind orbs

A course rebuild would silently lose all three, and lose the NavMesh in a way that produces no error
— enemies simply stop moving.

## 11. Assets worth trimming

- `Assets/city-rooftop-night-skybox/source/City Rooftop Night Sphere.fbx` — 92 MB, unreferenced
- `Assets/Prefabs/Enemy/Textures/*` — 77 MB extracted duplicate of what is inside the Ch44 FBX
- `Assets/Animations/P4_CLazyAttack/Attack_Wp_*` — empty folders with orphaned `Controller.meta`
- `Assets/Prefabs/Shadow Enemy/` — still named after `shadow.fbx`, which is an unrigged static mesh
  and is not used by anything
