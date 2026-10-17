# NasCon'26 — *Ember Ascent*

A third-person free-running action prototype in **Unity 6.2 (URP)**. You play a runner crossing a
burning harbour-foundry at night: sprint across wide rooftops, wall-run, swing from poles, climb
ledges, slide under gates, and fight through combat arenas with a combo system.

Built on **Character Controller Pro**, with a Mixamo rig and hand-built course.

**Status: prototype.** The systems are in place and the project compiles clean, but this has had
very little time in front of a human. Treat everything below as *implemented*, not *polished*.

---

## Requirements

| | |
|---|---|
| Unity | **6.2** (6000.2.x) |
| Render pipeline | Universal RP |
| Input | **Input System only** (`activeInputHandler: 1`) — legacy `Input.GetAxis` will throw |
| Rig | Humanoid |

Open the project, then `File → Open Scene → Assets/Scenes/MainMenu.unity` and press Play.

---

## Controls

| Action | Input |
|---|---|
| Move | `WASD` |
| Look | Mouse |
| Sprint | `Shift` (hold) |
| Jump | `Space` |
| Slide | `C` while sprinting |
| Punch combo | `LMB` |
| Kick combo | `RMB` |
| Wall-run | Jump into a `WallRun` layer surface while airborne and moving |
| Wall-jump | `Space` while wall-running |
| Ledge climb | Run into a ledge 0.55–2.4 m tall |
| Pole swing | Jump toward a swing bar |
| Roll | `Ctrl` |

---

## Features

### Locomotion

- **CCP kinematic capsule** driving a Humanoid Mixamo rig. Slope handling, step-up, crouch and
  collision come from Character Controller Pro; everything else is custom.
- **4-tier speed blend tree** — Idle `0` / Walk `3.2` / Jog `5.2` / Boost `8.0` m/s.
  The pack's "Boost" clip is used as *sprint*; there is no separate dash tier.
- **Jump** — `jumpSpeed 7.2`, `gravity 20` → apex **1.30 m**, airtime **0.72 s**.
  Airborne jump budget is deliberately `0`: you cannot re-arm mid-rise by spamming the key.
- **Slide** (`PlayerSlideState`) — sprint-only, `C`. Duration **1.4 s**, ~**6.7 m** of travel at
  8.5 m/s entry, driven by a speed curve from 1.0 → 0.12. Capsule height is delegated to CCP's
  collision-checked `StandUp()`, so popping up under a low ceiling is automatically blocked.
- **Roll** (`PlayerRollState`) — dodge roll with i-frames window and movement continuity.
- **Movement states forward the simulation events** (`Pre/PostCharacterSimulation`) back to
  `PlayerNormalMovement`, so `PlanarSpeed` stays fresh while the animator is out of Locomotion.
  Without this, attack/roll/slide leave the character stuck in a stale sprint pose.

### Wall-run (`PlayerWallRunState`)

- 360° probe for a wall within **1.8 m**; entry requires ≥ **3.5 m/s** planar speed.
- Reduced gravity (`0.22`), entry lift (**3.2**), stick force (**2.2**), max run **2.2 s**.
- **Wall-jump** — `jumpUp 8.5`, `jumpAway 4.0`, `jumpForward 6.0`, capped at `13 m/s`.
- **Anti-re-grab is structural, not temporal.** A short cooldown only delays the problem; the real
  fix is a *wall block* that stores the launched-from normal and refuses any wall within
  `Dot > 0.8` until you land. Coupling rule: `cooldown > probeDistance / jumpAwaySpeed`.
- **`WallRun` layer (6)** gates which surfaces are runnable. Trade-off worth knowing: a layer
  cannot tell *which face* you hit, so a surface you can both run along and stand on top of is
  impossible under this scheme. `|Dot(contact.normal, up)| < 0.1` is the alternative.
- Camera rolls to **15°** (lerped, never snapped) and FOV widens by **10**.
- Exit handoff: `WallRunL/R → Locomotion` on the `WallRunEnd` trigger; wall-jump plays
  `Wall_Jump_Cross_All_L/R` then hands to `AirDown` at 80% of the clip.

### Ledge climb (`PlayerClimbState`)

- Forward ray finds a vertical face, then a downward ray from **inside** the ledge finds its top.
- Accepts ledges **0.55 – 2.4 m** above the feet — so the 1 m and 1.25 m course obstacles work.
- Lift over **1.25 s** using the `HalfLedgeClimb` clip (whose pose rises **0.96 m**, matching a
  half-ledge). Movement is driven by `Teleport`, not velocity: a capsule physically cannot be
  pushed up a vertical face, so the rise is interpolated and the animation covers it.
- **0.6 s cooldown** on exit. Without it the state re-enters the instant it leaves and you
  ratchet up the level into the sky.

### Pole swing (`PlayerSwingState`)

- Real pendulum integration, not a canned animation:
  ```
  alpha = -(g / L) * sin(theta)
  omega += alpha * dt    (damped, clamped)
  theta += omega * dt
  ```
- **Entry speed becomes angular velocity** (`omega = v / L`), so momentum carries through a chain
  of bars instead of resetting at each one.
- Release converts it back: `tangent * (omega * L) * 1.2`, plus a 4 m/s upward kick.
- Swing plane is chosen from the side you approached on, so either direction works.
- **Release blocks the bar you just left** until you land, plus a 0.25 s cooldown — otherwise the
  release instantly re-grabs and you can never reach the next pole.
- Animator speed is set to **1.151** so the clip's loop period matches the pendulum's (2.80 s at
  `ropeLength 1.95`). Mismatched, the body visibly slides against its own arc.

### Combat (`PlayerAttackState`)

- **3-hit punch combo** — `Attack_Hand_1R` → `2L` → `PwR` (`LMB`)
- **3-hit kick combo** — `Attack_Kick_A` → `B` → `C` (`RMB`)
- Combo window opens on each hit and closes after a reset period; miss it and the chain restarts
  at hit 1.
- Cancel windows are measured from each clip's **real impact frame**, not guessed.
- Attacks forward simulation events so movement doesn't freeze mid-combo.
- `ComboEnd` trigger is reset on state entry — stale animator triggers cause the next state to be
  cut instantly, and this bit the project three separate times.

### Camera (`ThirdPersonOrbitCamera`)

- Orbit with mouse, sphere-cast collision that snaps in and eases out.
- **Speed-driven FOV** — `minSpeedForFov 4`, `fullFovSpeed 8`, boost `10`.
- Wall-run roll + FOV boost, both shared via statics so the character states can drive them.

### Level — *Ember Ascent* (`Assets/Scenes/Freerun.unity`)

Built by `Assets/Editor/FreerunBuilder.cs` (`Tools → Freerun → Rebuild Course`).

A **linear free-running route** with turns — 7 sections, roughly **666 m**, ~4.7 minutes.

| Section | |
|---|---|
| S1 Dock Approach | Entry, teaches sprint and slide |
| S2 Container Stack | Vertical movement, half vaults |
| S3 Container Yard | Open traversal, first arena |
| S4 Foundry Wall | Long wall-run corridors |
| S5 Smelter Hall | Interior, slide gates |
| S6 Gantry Run | Highest point, longest pole chain |
| S7 Crucible | Boss arena |

- **Designed for free-running, not precision parkour.** Surfaces are ≥ 8×8 m (smallest route
  surface 82 m²); height comes from ramps rather than floating platforms; obstacles sit *on* the
  path. A 4 m catwalk is a sprint *lane*, not a landing.
- **Three combat arenas** — 44×44, 52×52 and a 90×90 boss pit.
- **Traversal elements**: 4 slide gates, 14 half vaults, 2 full-body vaults, 5 wall-run walls,
  15 swing poles, 6 pit recovery floors.
- **Atmosphere**: night lighting, ember edge-glow shaders on every cube, 22 torches, 3 tornadoes
  placed off-route as landmarks.
- **Pit recovery floors** under every gap — a missed jump costs time, not a restart.

### Presentation

- **Post-processing** (`Assets/Settings/NightParkourProfile.asset`) — ACES tonemap, bloom 0.9,
  exposure +0.45, contrast 7, saturation 12, vignette 0.24, grain 0.10. DoF and motion blur
  deliberately dropped.
- **Pipeline** (`PC_RPAsset`) — MSAA 4×, HDR grading, 64³ LUT, shadow distance 60, 4096 shadow
  atlas, per-object light limit raised to **8** and shadow-casting fire lights cut to 3.
- **Custom shaders** — `Custom/EmberEdgeGlow` (edge glow; takes `_ObjectScale` because
  `CreatePrimitive` cubes are unit meshes), `Custom/EmberAdditiveMap` (colour × opacity additive,
  for tornadoes and torch flames), `Custom/EmberParticle`.

### Audio (`CharacterAudio.cs`)

Driven from character state rather than animation events:

- Footsteps by distance travelled (4 variants), jump on actual launch, land above a fall-speed
  threshold, swing/roll/slide on action edges.
- `PlayHit()` is public and waiting for a combat caller.
- Background music + placeholder synthesised SFX in `Assets/Audio/`.

### Main menu (`Assets/Scenes/MainMenu.unity`)

Background, title, Start, best-time row (from `PlayerPrefs` key `EmberAscent.BestTime`), Quit, a
fire VFX strip across the bottom 11% of the screen, and a bloom volume.

---

## Project structure

```
Assets/
  Animations/              Runner animation pack (trimmed to what's used)
  Audio/                   Music + SFX
  Character Controller Pro/Vendor — do not modify
  Prefabs/
    Character/Prefabs/     Player.prefab
    Character/Animations/  PC_Locomotion.controller
    Sword/ Torch/ Shadow Enemy/ skybox DEMO/
  Scenes/                  MainMenu, Freerun, NightParkour, SampleScene
  Scripts/
    Audio/                 CharacterAudio
    Camera/                ThirdPersonOrbitCamera
    Character/             PlayerNormalMovement + 6 movement states
    Input/                 CCPInputSystemHandler (Input System → CCP bridge)
    Level/                 LevelGoal, LevelRespawn, SlowSpin
    UI/                    MainMenu
  Editor/                  FreerunBuilder, EmberAscentBuilder, SfxGenerator
  Shaders/                 EdgeGlow, EmberAdditiveMap, EmberParticle
  Settings/                URP assets + post-processing profiles
```

**Scenes in Build Settings:** `MainMenu` (0), `SampleScene`, `NightParkour`, `Freerun`.

`NightParkour` is the earlier ring level, kept deliberately for reference — do not delete.
`SampleScene` is the untouched URP template scene. Both should eventually be dropped from the
build list so they don't ship, but the assets stay in the project.

---

## Technical notes

Hard-won gotchas. All of these caused real bugs.

- **`CharacterActions.<x>.Started` latches `true` forever** in CCP — it's only cleared on
  enable/disable. Never use it for per-frame checks. Do rising-edge detection on `.value` and
  snapshot on state entry. This bit the project twice (attacks, then wall-run).
- **Changing a script's field initialiser does not update an existing component.** New values must
  be pushed to the prefab via `SerializedObject` and read back to verify. This silently defeated
  fixes at least four times.
- **Stale animator triggers** cause the next state to be cut instantly. `ResetTrigger` on state
  entry. Hit three times (`ComboEnd`, `WallRunEnd`, `WallRun`).
- **Exit-time transitions on looping clips are a bug class.** They caused both the attack freeze
  and the wall-run animation dropping out. Use condition-driven exits.
- **1D blend trees need `m_UseAutomaticThresholds = false`** before writing thresholds.
- **`AssetDatabase.SaveAssets()` does not write `.inputactions` JSON.** Use `asset.ToJson()` →
  write file → reimport. The `Dash` and `Kick` actions were silently lost this way once.
- **Editor play mode is frozen between tool calls** — `Time.time` doesn't advance, so anything
  physics-driven needs a human to verify.
- **`Renderer.bounds` are stale right after `Instantiate`** — measuring immediately gives garbage.
- **Sketchfab/Fab models ship their author's camera and lights.** Check imported hierarchies for
  stray `Camera` and `Light` components; the sword alone was bringing a 27° camera and two point
  lights at intensity 3000 into the scene.
- **Mixamo downloads arrive as Generic/NoAvatar.** Set `Rig → Animation Type = Humanoid` or the
  clip won't drive the rig.
- **`NormalMovement.cs` lives in `Character Controller Pro/Demo/`, not Core.** It is the base
  class the entire player controller derives from. Do not treat the Demo folder as disposable.
- Do not modify `Assets/Character Controller Pro/` beyond the two Unity-6 compatibility patches
  (`CharacterDetector.cs` `EntityId`, `CharacterActions.cs` `attack`/`kick`).

---

## Roadmap

Everything still to do, roughly in dependency order.

### Gameplay

- [ ] **Enemy + combat** — model ready at `Assets/Prefabs/Shadow Enemy/shadow.fbx` (2.0 m,
      textured). Needs health, an AI (chase / attack / stagger), damage exchange, and hit
      detection. **Reuse the impact frames already measured in `PlayerAttackState`** as the damage
      windows — the timings exist, they're just not wired to anything yet.
      `CharacterAudio.PlayHit()` is waiting for a caller. The pack has attack and death sets.
- [ ] **Health** — player and enemy health, damage on hit, death handling.
- [ ] **Fire gem** (health regen) — needs a pickup system (trigger volume, float, rotate, SFX).
      No model in the project yet.
- [ ] **Green gem** (time-rewind charge) — same pickup system. No model yet.
- [ ] **Time rewind** — the biggest single item. Ring buffer of snapshots (position, rotation,
      velocity, grounded, health) sampled per `FixedUpdate`. Use `CharacterActor.Teleport()` to
      move back — it raises CCP's `OnTeleport`, which `NormalMovement` listens to, so state resets
      cleanly instead of leaving stale velocity. **Cap the buffer** (a few seconds). Decide
      explicitly what is *not* rewound — enemies and pickups shouldn't be, or the level becomes
      farmable.
- [ ] **Vault** — the obstacles exist (`HV_*`, `FV_*`) but there's no vault state; the player jumps
      them. Needs a detection volume plus a state.
- [ ] **Sword holster / draw** — currently visual only, parented to `mixamorig:Spine2`. Needs a
      hand attachment point, a sheathe/draw toggle, and draw/sheathe clips. The hard part is
      solved: the rest pose and animated pose differ a lot, so the local rotation was derived as
      `Quaternion.Inverse(bone.rotation) * desiredWorldRotation` and baked.
- [ ] **Wall-run tuning** — `gravityScale`, `entryLift`, `stickForce`, `maxDuration` are all
      first-guess values. There's also no *entry* animation; `Wall_Vertical_Enter` is in the pack
      unused, so the run currently snaps straight into the loop clip.

### Content

- [ ] **New animation pack** — the current one was deliberately trimmed to only what the
      controller references. Future moves will need new clips supplied.
- [ ] **Level polish** — the course has never been visually verified end to end at the time of
      writing. Walk it before building more on top.
- [ ] **Main menu improvement** — settings, credits, level select, animated title, camera drift.
      Note: the fire strip covers the bottom 11%, so anything placed there gets hidden.
      There was an unexplained bug where four UI elements vanished after a save; watch for it.

### Shipping

- [ ] **Remove leftover scenes** from Build Settings — `SampleScene` (URP template) and
      `NightParkour` (superseded by `Freerun`).
- [ ] **Trailer** — clean frames can be captured through a temp camera + RenderTexture, but
      gameplay needs a human playing.
- [ ] **WebGL build** — check in this order:
      - WebGL build target module may not be installed.
      - Compression: Brotli needs server headers; Gzip is safer for GitHub Pages.
      - The music track is a large download — consider a shorter loop.
      - Cut MSAA, HDR grading, shadow atlas, SSAO, bloom for a WebGL quality level.
      - Audit anything touching `System.IO` — file writes don't work on WebGL.
        (`SfxGenerator.cs` does, but it's under `Assets/Editor/` so it's stripped from builds.)
      - `PlayerPrefs` does work (IndexedDB-backed).
- [ ] **Deployment** — GitHub Pages, build output on its own branch or `/docs`, with `.gz` handling.

---

## Third-party

- **Character Controller Pro** (Lightbug) — character controller. Patched for Unity 6 only.
- **Runner Action Animation Pack** — locomotion, combat, wall-run, jump, escape, collision clips.
- **Mixamo** — Medea character rig; `Swing` and `HalfLedgeClimb` clips.
- **Vefects Free Fire VFX** — fire and heat-haze effects.
- **skybox DEMO** (mooham3d) — night skybox.

---

## Repository

```
https://github.com/HasnainAli-12/NasCon26
```

`Library/`, `Temp/`, `Logs/` and `UserSettings/` are gitignored. The repo tracks source and assets
(186 MB); Unity regenerates the rest on first open.
