# NasCon'26 — *Embers Edge*

A third-person free-running action game in **Unity 6.2 (URP)**, targeting a **WebGL browser build**.

You play a runner crossing a burning harbour-foundry at night: sprint across rooftops, wall-run,
swing from gantry poles, mantle ledges, slide under gates, and fight through combat arenas to a
final five-enemy stand — with a **time-rewind** ability that pulls you back five seconds.

Built on **Character Controller Pro**, with a hand-built course and a full HUD, audio and VFX pass.

---

## Core game loop

```
    in-scene start menu          hero shot, world frozen, one enemy visible ahead
             |
         press START             menu fades, camera eases to third person, control at 85%
             |
      run the course             slide / wall-run / wall-jump / pole swing / ledge climb
             |
      encounter enemies          staged along the route, density rising as you descend
             |
      fight                      punch combo, kick combo, or draw the sword for a blade combo
             |
      collect orbs               rewind orbs grant charges, heal orbs restore health
             |
      take damage                health drains, damage vignette, hit feedback
             |
      die?                       death animation, world freezes, Retry reloads the level
      fall?                      respawn at your last safe footing, no restart
             |
      clear every enemy          COURSE CLEAR -> Win scene
```

A run is roughly **4–5 minutes**. There is one level; the menu lives inside it, so a build launches
straight into the game with no boot scene.

---

## Requirements

| | |
|---|---|
| Unity | **6.2** (6000.2.x) |
| Render pipeline | Universal RP |
| Input | **Input System only** (`activeInputHandler: 1`) — legacy `Input.GetAxis` will throw |
| Rig | Humanoid |
| Cinemachine | installed (6.6.0) but **unused** — the camera is custom |

Open `Assets/Scenes/Freerun.unity` and press Play. **Do not** open `MainMenu.unity` — that scene is
legacy and is no longer in the build.

---

## Controls

| Action | Input |
|---|---|
| Move | `WASD` |
| Look | Mouse |
| Sprint | `Shift` (hold) |
| Jump | `Space` |
| Slide | `C` while sprinting |
| Roll | `Ctrl` |
| Punch combo | `LMB` |
| Kick combo | `RMB` |
| Draw / sheathe sword | `X` |
| Time rewind | `R` |
| Wall-run | jump into a runnable surface while airborne and moving |
| Wall-jump | `Space` while wall-running |
| Ledge climb | run into a ledge 0.55–2.4 m tall |
| Pole swing | jump toward a swing bar |
| Pause | `Esc` |
| Start menu | `Enter` / `Space` / gamepad South |

---

## Systems

### Locomotion
- **CCP kinematic capsule** driving a Humanoid rig. Slope, step-up and collision come from
  Character Controller Pro; everything else is custom.
- **4-tier speed blend** — Idle / Walk 3.2 / Jog 5.2 / Boost 8.0 m/s.
- **Jump** — apex ~1.3 m, ~0.72 s airtime, no mid-air re-arm.
- **Slide** — sprint-only, ~1.4 s and ~6.7 m, with a speed curve. Capsule height goes through CCP's
  collision-checked `StandUp()`, so popping up under a ceiling is blocked automatically.
- **Roll** — dodge roll with an i-frame window.
- **States forward `Pre/PostCharacterSimulation`** to `PlayerNormalMovement`, so `PlanarSpeed` stays
  fresh while the animator is out of Locomotion. Without this, attacks leave a stale sprint pose.

### Traversal
- **Wall run** — 360° probe, reduced gravity, entry lift, camera roll 15°. Re-grab prevention is
  **structural**: a wall block stores the launched-from normal and refuses any wall within
  `Dot > 0.8` until you land. Looped scrape sound while on the wall.
- **Ledge climb** — the rise is driven by `Teleport`, not velocity, because a capsule cannot be
  pushed up a vertical face. A 0.6 s cooldown stops it re-entering and ratcheting you into the sky.
- **Pole swing** — real pendulum integration, not a canned clip:
  `alpha = -(g/L)·sin(theta)`. Entry speed becomes angular velocity, so momentum carries across a
  chain. Animator speed is matched to the pendulum period or the body slides against its own arc.

### Combat
- **3-hit punch combo** (`LMB`) and **3-hit kick combo** (`RMB`).
- **2-hit sword combo** (`X` to draw, then `LMB`). Three swing samples, one per step.
- Cancel windows are measured from each clip's real impact frame, not guessed.
- **Movement cancels the recovery tail** once the hit has landed — a full clip lock felt like a
  one-second freeze.
- **Soft aim** snaps the swing toward the nearest hostile in a forward cone, so running past a target
  still connects.
- **Hit feedback** is asymmetric by design: enemies always spark, the player only sometimes.

### Time rewind
- 150-slot ring buffer, 5 s at 30 samples/sec.
- **Single press** — holding was removed, it let the rewind run indefinitely.
- Costs a **charge**; charges come from green orbs. Starts at **0**.
- Needs 0.5 s of history before it will fire, so it cannot trigger on the first frame with nothing
  to rewind to.
- Restores **position, velocity and health only** — animation is deliberately *not* rewound, or
  attacks and combat poses would replay.
- Enemies and projectiles freeze for the duration.

### Enemies
- NavMesh FSM: **Idle → Chase → Melee / Throw → Stagger → Dead**.
- Throws fire projectiles that travel on a swept sphere-cast, not a rigidbody — a fast small object
  tunnels straight through railings otherwise.
- Death plays a Dead state directly by name; an animator trigger races with the Hurt transition and
  silently loses.
- **14 placed** on a difficulty ramp: 1 early, then 2, 3, 3, and **5 together in the final arena**.

### Pickups
- **7 rewind orbs** and **8 heal orbs** spread across the course.
- Heals are placed deliberately — one inside the final arena and two on the approach, not scattered
  where they would be spent long before the fight that needs them.
- Each snaps on walk-through, plays a VFX and sound, and stays collected.

### Death, fall and win
- **Death** — plays a Dead state, freezes enemies and projectiles, then stops the world after the
  animation. The cursor is released so the panel is clickable.
- **Fall** — respawn at the last *safe* grounded position. The safe spot is only sampled while
  grounded **and** moving under ~20 m/s, because sliding off an edge is still "grounded" on that
  frame and would record the ledge you just fell from.
- **Win** — when no Enemy-team `Health` remains alive, after a delay for the death animation.

### HUD
- Health bar with a lagging damage trail and a green → amber → red ramp.
- **Radial damage vignette** — edges only, not a full-screen red wash.
- Run timer `mm:ss.cc`, starts at the moment control is given; best time in `PlayerPrefs`.
- Rewind counter, hidden until an orb is collected; cooldown state; green ready state.
- Weapon icons that swap between fist and sword.
- Boss bar — event-driven, hidden until `Show()` is called.

### In-scene start menu
- No separate menu scene. The menu lives in `Freerun` and flows into gameplay.
- Character frozen in the intro pose, particles and enemies paused, hero camera with a slow sway.
- On Start: menu fades, camera eases to the third-person pose on a smoothstep, HUD fades in, control
  at 85% of the blend.
- **No `Time.timeScale = 0`** anywhere — that would also stop the UI fades and the camera blend.
  Everything is frozen individually.
- A `static` flag skips the menu on a reload, so dying and retrying goes straight into gameplay.

### Audio
- **Pooled `Sfx` player** — 12 sources on a hidden object. Not `PlayClipAtPoint`, which allocates a
  GameObject per sound and would show up as GC stutter on WebGL.
- Clips load by name from `Resources/Audio`, so nothing needs inspector wiring.
- **`SfxDirector`** drives hurt and death sounds off `Health` events, re-scanning for enemies spawned
  at runtime. **19 of 21 clips wired.**

### VFX
- `FXV` spawner, same Resources-by-name approach, self-cleaning by particle lifetime.
- Rewind burst and aura, heal, hit impacts, fire impact, meteor shower, respawn, blade trail.
- **Legacy particle shaders were converted to URP** — several would otherwise render magenta.

---

## Presentation

- **Post-processing** — `NormalProfile` (bloom threshold 1.60) and `RewindProfile` (green filter,
  chromatic aberration, heavier vignette), blended by `RewindVolumeBlend`.
- **Pipeline** — MSAA off, shadow distance 35, single cascade, 3 lights per object, render scale
  0.85. Tuned for WebGL, not fidelity.
- **Custom shaders** — `Custom/EmberEdgeGlow` (takes `_ObjectScale` because `CreatePrimitive` cubes
  are unit meshes), `Custom/EmberAdditiveMap`, `Custom/EmberParticle`.
- **Static batching is off** on the level objects — it breaks `EmberEdgeGlow`, which reads mesh local
  position against a per-material `_ObjectScale`.

---

## Project structure

```
Assets/
  Animations/              Runner animation pack
  Audio/MySFX/             21 supplied sound effects
  Character Controller Pro/ Vendor — do not modify
  Hovl Studio/             VFX packs (projectiles, RPG, magic circles, AOE)
  InsaneSystems/           HealthbarsKit (art used, its runtime system is not)
  Kevin Iglesias/          Animation packs (death, get-up, combat)
  Prefabs/                 Player, Shadow Enemy, Time Rewind Orb, Heal Orb
  Resources/
    Audio/                 Runtime-loaded SFX
    Vfx/                   Runtime-loaded effects
  Scenes/                  Freerun (0), Win (1)
  Scripts/
    Character/             PlayerNormalMovement + movement states, IntroSequence(Controller),
                           PlayerDeath, FallRespawn
    Combat/                Health, HitResolver, HitFeedback, FireProjectile
    Enemy/                 EnemyBrain
    FX/                    FXV, Sfx, SfxDirector, RewindVolumeBlend, MeteorEvent
    Level/                 LevelGoal, WinCondition
    Player/                TimeRewindAbility, PlayerWeapon, RewindOrbPickup, HealOrbPickup
    UI/                    PlayerHUD, RewindUI, ActiveSlotUI, BossBarUI, WeaponIconUI,
                           PauseMenu, WinScreen
  Settings/                URP assets + post-processing profiles
  Shaders/
  UI/                      Icons, fonts, radial vignette
```

**Scenes in Build Settings:** `Freerun` (0), `Win` (1). A build launches straight into Freerun.

`MainMenu.unity` still exists on disk but is **not in the build**. Its script `MainMenu.cs` is kept
only because `PlayerHUD` and `LevelGoal` both read `MainMenu.BestTimeKey` — deleting it breaks every
best-time display.

---

## Building for WebGL

```
File → Build Settings → WebGL → Build
```

- **Serve it over HTTP.** A WebGL build will not run from `file://` — the browser blocks the asset
  fetches. `python -m http.server 8000 --directory Builds/WebGL` is enough.
- **Audio must start from a user gesture.** Browsers block it until a click, which is why the music
  start lives in the Start button handler.
- `PlayerPrefs` works (IndexedDB-backed), so best times persist.
- Compression: Brotli needs server headers; Gzip is safer for GitHub Pages.

---

## Technical notes

Hard-won gotchas. All of these caused real bugs in this project.

- **`CharacterActions.<x>.Started` latches `true` forever** in CCP — it is only cleared on
  enable/disable. Never use it per-frame. Do rising-edge detection on `.value`. This bit twice.
- **Changing a script's field initialiser does not update an existing component.** Push new values
  to the prefab via `SerializedObject` and read them back. Silently defeated fixes at least four
  times.
- **Stale animator triggers** cut the next state instantly. `ResetTrigger` on state entry. Hit three
  times (`ComboEnd`, `WallRunEnd`, `WallRun`).
- **An `AnyState` transition with a trigger loses to a higher-priority one.** `animator.Play(name)`
  is the reliable path for anything that must happen — this fixed both the enemy and player death
  animations.
- **`CanvasGroup.blocksRaycasts = false` kills raycasts for every child.** Disabling it on the HUD
  made the death panel's buttons unclickable while the object was still fully wired.
- **`writeDefaultValues = false` on an animator state** lets properties the clip does not key keep
  the previous state's values, which can make a full-body clip look like nothing happened.
- **Vector4 vs Color parameters.** `LiftGammaGain.lift/gamma/gain` are `Vector4Parameter` — writing
  `colorValue` is a silent no-op.
- **A `GameObject` with no sprite still draws a solid rect.** The vignette was a full-screen image
  with no sprite, so it washed the whole screen instead of darkening the edges.
- **A missing script leaves a permanent null slot**, which Unity keeps so component order stays
  stable. Three poles sat broken this way; `RemoveMonoBehavioursWithMissingScript` clears them.
- **Prefabs outside `Resources/` are stripped from a build** when nothing references them.
- **Sketchfab/Fab models ship their author's camera and lights.** The sword alone brought in a camera
  and two point lights at intensity 3000.
- **`NormalMovement.cs` lives in `Character Controller Pro/Demo/`, not Core.** It is the base class
  the entire player controller derives from. Do not treat the Demo folder as disposable — deleting it
  as "unreferenced" is what broke this project once.
- Do not modify `Assets/Character Controller Pro/` beyond the Unity-6 compatibility patches
  (`CharacterDetector.cs` `EntityId`, `CharacterActions.cs` `attack`/`kick`) and the kinematic
  velocity guard in `RigidbodyComponent3D.cs`.

---

## Roadmap

### Content
- [ ] **A wall-run scrape clip.** It currently borrows `slide.wav`, so wall runs and slides are
      indistinguishable.
- [ ] **Enemy attack clips.** The enemy improvises from the Kicking set.
- [ ] **Vault over low obstacles** — the `HV_*` and `FV_*` obstacles exist but the player jumps them.
- [ ] **More enemy variety** — one model, one behaviour set.
- [ ] **Boss fight** — the final arena is five regular enemies. `BossBarUI` is built and waiting for
      a single large `Health` to call `Show()` on.

### Polish
- [ ] **Ledge and pole climbing rework** — parked deliberately by the developer.
- [ ] **Tune `cameraBlendTime`** — the intro camera takes 3 s regardless of the stand-up clip length,
      so one finishes before the other.
- [ ] **Frame `MenuCamPose`** — currently placed at a computed starting guess, not judged visually.
- [ ] **Level polish** — never visually verified end to end.

### Shipping
- [ ] **Deployment** — GitHub Pages, build output on its own branch or `/docs`, with `.gz` handling.

### Cleanup
- [ ] **Delete `MainMenu.cs`** once `BestTimeKey` is moved into `PlayerHUD`.
- [ ] **Trim unused assets** — `Kevin Iglesias` is 692 MB and `Hovl Studio` 229 MB, but most unused
      clips share FBX files with used ones, so a scan cannot tell them apart. Do it in small batches
      with a build between each.

---

## Third-party

- **Character Controller Pro** (Lightbug) — character controller. Patched for Unity 6 only.
- **Runner Action Animation Pack** — locomotion, combat, wall-run, jump, escape, collision.
- **Kevin Iglesias** — death, get-up and combat animation clips.
- **Hovl Studio** — VFX: projectiles, RPG effects, magic circles, AOE spells.
- **Vefects Free Fire VFX** — fire and heat haze.
- **InsaneSystems HealthbarsKit** — health bar art only; its runtime spawning system is unused.
- **Kenney** — fantasy UI borders, skyboxes.
- **Mixamo** — character rig.

---

## Repository

```
https://github.com/HasnainAli-12/NasCon26
```

`Library/`, `Temp/`, `Logs/`, `Builds/`, `ProfilerCaptures/`, `obj/` and the generated `.csproj` /
`.slnx` files are gitignored. Unity regenerates them on first open. Large binaries are tracked with
Git LFS.
