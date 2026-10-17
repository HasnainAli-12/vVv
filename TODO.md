# NasCon'26 — tomorrow's list

Ordered roughly by dependency: things later in the list lean on things earlier.
Anything marked **[blocked]** needs a decision or an asset first.

---

## 1. Fix wall run

`Assets/Scripts/Character/PlayerWallRunState.cs`

Fixed so far: gravity sign (was floating *up* the wall), 360° probe (the old fan
only looked right, so left-side walls were undetectable), `WallRun` layer gate,
camera tilt + FOV, anti-chaining cooldown.

Still wrong, ranked by likelihood:

- **The layer gate.** Requested, but the reference implementation argues against
  it — a layer can't tell *which face* you touched, so a wall you can also stand
  on top of is impossible. Alternative:
  `Mathf.Abs(Vector3.Dot(contact.normal, Vector3.up)) < 0.1f`. Not swapped in yet
  because it would make every platform edge runnable.
- **Feel values, all untuned.** On `Player > States > PlayerWallRunState`:
  `gravityScale` 0.22, `entryLift` 3.2, `stickForce` 2.2, `runSpeed` 8.5,
  `turnRate` 9, `maxDuration` 2.2.
- **Character rotation** is set directly on the transform during the run. CCP may
  fight this — check against `CharacterActor`'s own rotation handling.
- **No entry animation.** `Wall_Vertical_Enter` is in the pack and unused; the run
  snaps straight into the loop clip.

**Needs a human at the keyboard.** Nothing here has ever been seen in motion.

---

## 2. Enemy + combat

- Model ready: `Assets/Prefabs/Shadow Enemy/shadow.fbx` (2.0 m, textured, URP/Lit).
- Player side already exists: `PlayerAttackState` with 3-hit punch and kick combos,
  cancel windows measured from each clip's real impact frame.
- **Use those same impact frames as the damage windows** — the timings are already
  measured, they just are not wired to anything.
- `CharacterAudio.PlayHit()` is public and waiting for a combat caller.
- Needs: enemy health, an AI (chase / attack / stagger), damage exchange, and hit
  detection on the swing windows.
- Animation pack has attack + death sets (`P4_CLazyAttack`, `P5`) for the enemy.

## 3. Health

- Player health, enemy health, damage on hit, death handling.
- Simplest hook: `PlayerAttackState` already knows its impact frames; fire a
  sphere/box overlap there.

## 4. Fire gem — health regen

- No pickup system exists yet. Needs one (trigger volume, float/rotate, pickup SFX).
- Fire gem model: not in the project — **[blocked]** on an asset, or build from
  primitives with the existing `Custom/EmberEdgeGlow` shader.

## 5. Green gem — time rewind charge

- Same pickup system as the fire gem.
- Model: also **[blocked]**.

## 6. Time rewind

- Biggest single item. Needs a ring buffer of snapshots (position, rotation, velocity,
  grounded, health) sampled each FixedUpdate.
- `CharacterActor.Teleport()` is the correct way to move the character back — it
  raises CCP's `OnTeleport`, which `NormalMovement` listens to, so state resets
  cleanly instead of leaving stale velocity.
- Rewind should not resurrect enemies or undo pickups, or the level becomes
  trivially farmable. Decide what is and is not rewound.
- Eats memory fast — cap the buffer (a few seconds is plenty).

## 7. Level design fix

- The current layout was rebuilt as linear free-running: 11 stages, big decks and
  9 m walkways, 3 arenas (34/40/30 m), 3 tornadoes, 30 torches.
- **Never visually verified** — vision credits ran out partway through. Worth a
  look before building more on top.

## 8. Level design addition — swing, vault

- Vault: the move exists (`Tuck` obstacles are in the level), but there is no vault
  *state* — the player jumps them. A real vault needs a detection volume + state.
- Swing: needs geometry (bars / ropes) plus a state. No swing clips confirmed in the
  pack yet — **check `Assets/Animations` before designing around it.**
- Both are the kind of thing the reference wall-run article recommends: build the
  geometry *from* the move set, not the other way round.

## 9. Sword integration — holster / deholster

- Currently visual only: parented to `mixamorig:Spine2`, swing with the torso.
- The hard part is already solved — the rest pose and the animated pose differ a lot,
  so the local rotation was solved at runtime as
  `Quaternion.Inverse(bone.rotation) * desiredWorldRotation` and baked. Reuse that
  for the hand.
- Needs: a hand attachment point, a sheathe/draw toggle, and draw/sheathe clips.

## 10. Main menu improvement

- `Assets/Scenes/MainMenu.unity`: background, title, StartButton art, best time row,
  Quit, fire strip across the bottom 11%, bloom volume.
- **Unexplained bug:** four UI elements (Title, Subtitle, BestTime, QuitButton)
  vanished mid-session after a save. Rebuilt and verified from disk, but the cause is
  unknown — watch for it. If it recurs, that is a real bug.
- Fire covers the bottom band, so anything placed there gets hidden.
- Ideas: settings, credits, level select, animated title, camera drift.

## 11. Trailer

- Can capture clean frames through the render harness (temp camera + RenderTexture)
  faster than screen-recording, but it cannot drive gameplay — needs a human playing.

## 12. GitHub push

- **Nothing is committed yet.** The working tree has the whole project as untracked
  or added-but-uncommitted.
- `.gitignore` exists; check `Library/`, `Temp/`, `UserSettings/`, `Logs/` are excluded
  before the first commit — those are large and machine-specific.
- `Assets/Prefabs/Sword/*.blend` is 11 MB and depends on Blender for import — consider
  exporting an FBX before pushing.
- `Assets/Animations` is 866 FBX files; confirm the repo size is acceptable.

## 13. WebGL build + deployment

Needs checking, in this order:

- **Build target module** — WebGL support may not be installed in this Unity install.
- **Compression** — Brotli needs the right server headers; Gzip is safer for
  GitHub Pages.
- **The 150 s music track** is now Streaming Vorbis, but it is still a large download.
  Consider a shorter loop for the web build.
- **Quality is dialled up for PC and will not survive WebGL**: MSAA 4x, HDR colour
  grading, 4096 shadow atlas, SSAO, bloom, DoF, motion blur. Expect to cut these in a
  WebGL-specific quality level.
- **Check every asset touching `System.IO`** — file writes do not work on WebGL.
  `SfxGenerator.cs` does, but it is under `Assets/Editor/` so it is stripped from
  builds. Verify nothing else does.
- PlayerPrefs (best time) works on WebGL, backed by IndexedDB.
- Deployment: GitHub Pages is the usual route; needs the build output on its own
  branch or `/docs`, plus `.gz`/`.br` handling.

---

## Known project-wide gotchas

- `BoolAction.Started` in CCP **latches true forever** — never use it for per-frame
  checks. Track rising edges off `.value` instead.
- Changing a script's field initialiser does **not** update a component that already
  exists. New values must be pushed to the prefab/instance explicitly.
- `Renderer.bounds` are stale until the renderer updates — measuring right after
  `Instantiate` gives garbage.
- `.inputactions` is JSON written by the Input System's own save path;
  `AssetDatabase.SaveAssets()` does not write it.
- Editor play mode does not tick between tool calls, so anything needing real physics
  cannot be verified from a tool — it needs a human.
