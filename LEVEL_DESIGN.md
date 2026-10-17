# Ember Ascent — Freerunning Course Design

## 1. Theme & Setting

**"Ember Ascent" — an abandoned harbour foundry district at night.**

A working port district gutted by fire and left to rot: container yards, warehouse
roofs, foundry halls, gantry cranes, smelting floors. Everything is massive and
industrial — shipping containers, furnace housings, concrete silos, steel pipework.
Fire still burns in grate openings and along ruptured gas lines, which is the only
light source besides a low moon. Every structural edge catches that firelight.

**Readability rule:** the route is always the *lit* route. Fire, ember-lit edges and
torches mark the way forward. Everything off-route is cold and dark, so the player can
read the path at a sprint without ever stopping to look.

**Why this setting:** it gives you enormous flat roofs (run surfaces), enormous flat
walls (wallrun surfaces), container heights that map exactly onto the vault heights,
and industrial pits that read as lethal without needing a visual explainer.

---

## Movement budget (the numbers everything is checked against)

| Ability | Value |
|---|---|
| Character height | 1.8 m |
| Sprint speed | 8.5 m/s |
| Jump apex | 1.30 m |
| Jump airtime | 0.72 s |
| Flat sprint-jump reach | ~6.1 m |
| Slide clearance | 0.85 m (1.0 m opening is comfortable) |
| Step-up | 0.4 m |

**How gaps beyond 6 m work:** a jump *downhill* extends reach substantially — airtime
grows with the drop. So the rule used throughout is:

- **4–6 m gap, flat** → direct sprint jump.
- **6.5–9 m gap with a 3–5 m drop** → fair sprint jump, because the fall carries you.
- **>9 m, or flat** → crossed by wallrun or pole, never by jumping.

**Ability dependencies.** Three required elements need movement abilities that do not
exist yet. These are flagged in the checklist rather than designed around:

| Element | Depends on |
|---|---|
| Full-body vault (1.5–2 m) | a **vault/climb-up** ability — the 1.30 m jump apex cannot clear it |
| Hanging poles | a **swing** ability |
| Wallrun | exists, currently unfinished |

---

## 2. Section-by-section

### Section 1 — Dock Approach
*Tutorial. Teaches slide, half-vault, first gap, first wallrun. Deliberately forgiving,
generous approach distances, no penalty for hesitation.*

**Length ~90 m · ~26 s**

| # | Element | Dimensions |
|---|---|---|
| 1 | Start deck (container roof) | **24 × 18 m** at y 0 |
| 2 | Run-up | **12 m** of clear sprint |
| 3 | **Slide under** | Container wall spanning the full **14 m** width, opening **1.0 m** high × **3 m** deep (a short tunnel) |
| 4 | Landing | **12 m** flat, then three **half-body vaults** — railings **1.0 m** high, **12 m** wide, spaced **6 m** apart |
| 5 | Runway | **10 m** |
| 6 | **Pit / gap** | **5.0 m**, landing **2 m lower** — **20 × 20 m** roof |
| 7 | **Wallrun #1** | Wall **18 m long × 7 m tall**, **7 m pit** below. Exit ledge **8 × 8 m** |
| 8 | Exit | **6 m** drop onto the Section 2 roof |

Flow out: the 6 m drop is the first "leap of faith" — the player sees the drop and the
next roof together, so committing is a decision, not a guess.

---

### Section 2 — Container Stack
*Build-up. First combination of moves. Introduces the pole.*

**Length ~110 m · ~28 s**

| # | Element | Dimensions |
|---|---|---|
| 1 | Entry roof | **20 × 14 m** at y −6 |
| 2 | **Half-body vault ×5** | Container tops **1.0 m** high, **10 m** wide, spaced **5 m** — a five-beat rhythm |
| 3 | **Full-body vault** | Shipping container **2.4 m** tall, **12 m** wide — needs the vault/climb ability |
| 4 | Runway | **12 m** |
| 5 | **Slide under** | Pipe gantry, **1.0 m** clearance, **4 m** deep, spanning **16 m** |
| 6 | **Pit / gap** | **6.5 m** with a **3 m** drop → **24 × 18 m** landing |
| 7 | **Hanging poles ×4** | Poles **4 m** above the landing, **4 m** apart, spanning an **18 m pit** |
| 8 | Far landing | **16 × 12 m** |
| 9 | Exit | **Wallrun #2** — **24 m × 8 m** wall along a foundry face, landing into Section 3 |

Flow out: the wallrun ends directly on the raised edge of Combat Arena 1, so the player
arrives already moving and above the fight.

---

### Section 3 — Combat Arena 1: The Container Yard
**44 × 44 m.** See §3 for layout.
**Combat time 35–45 s**

Flow out: north-east, a **5 m gap** onto a warehouse roof.

---

### Section 4 — The Foundry Wall
*Hardest traversal section. Everything at once, and the only place where a mistake
costs you real height.*

**Length ~150 m · ~50 s**

| # | Element | Dimensions |
|---|---|---|
| 1 | Entry roof | **26 × 20 m** |
| 2 | **Pit / gap** | **8 m** with a **4 m** drop → **20 × 16 m** roof (fair because of the drop) |
| 3 | **Wallrun #3 — building to building** | Wall **26 m long × 9 m tall** running between two warehouse blocks, **12 m pit** below. Entry ledge **10 × 8 m**, exit ledge **10 × 8 m** |
| 4 | **Pit / gap** | **5 m** immediately off the wallrun exit onto a catwalk |
| 5 | **Wallrun #4 — into a jump** | Wall **18 m × 7 m**, **7 m pit** below; the wall ends in mid-air and the player launches into a **6 m gap** onto a landing |
| 6 | Catwalk | **20 m long × 4 m wide**, two **half-body vaults** (railings, 1.0 m) |
| 7 | **Long pit** | **9 m** with a **5 m** drop → **24 × 18 m** landing. The biggest direct jump in the course |
| 8 | **Hanging poles ×5** | **4 m** apart, **4 m** above, spanning a **22 m pit** |
| 9 | Exit | **Slide under** on the landing — **1.0 m** clearance, **14 m** wide |

Flow out: the slide spits the player out directly into Combat Arena 2's south entrance.

---

### Section 5 — Combat Arena 2: The Smelter Hall
**52 × 52 m.** See §3.
**Combat time 45–55 s**

Flow out: a **6 m gap** from the mezzanine across to the climax section.

---

### Section 6 — The Gantry Run
*Climax traversal. Fastest section, every obstacle chained, no rest.*

**Length ~130 m · ~40 s**

| # | Element | Dimensions |
|---|---|---|
| 1 | Entry mezzanine | **18 × 10 m** at y +6 |
| 2 | **Wallrun #5 → jump → slide** (3-move chain) | Wall **20 m × 8 m** → launch into a **5 m gap** → landing **14 × 10 m** → immediate **slide under**, **1.0 m** clearance |
| 3 | **Hanging poles ×6** | The longest pole run: **4 m** apart, **4 m** above, spanning a **26 m pit**, slightly downhill so momentum builds |
| 4 | **Half-body vaults ×4** | Railings on a catwalk, **1.0 m** high, **4 m** apart |
| 5 | **Full-body vault** | A **1.8 m** barrier, **10 m** wide |
| 6 | **Sprint jump** | **6 m flat** — the only flat maximum-range jump in the course, and it is on the final approach |
| 7 | Descent ramp | **40 m long × 8 m wide**, dropping **14 m** into the boss approach. No obstacles — the one deliberate "breathing" moment, used to build scale before the arena |
| 8 | Threshold | Monumental doorway, **10 m wide × 14 m tall** |

Flow out: the ramp frames the boss arena through the doorway before the player reaches it.

---

### Section 7 — Boss Arena: The Crucible
**90 × 90 m.** See §4.
**Combat time 60–75 s**

**Total course: ~666 m · ~4.7 min** (movement ~2.4 min, combat ~2.3 min).

---

## 3. Combat arenas (1 & 2)

Both follow the same principle: **large clear floor, very few large cover pieces, and
multiple entrances so the player never feels cornered.** No clutter.

### Arena 1 — The Container Yard (44 × 44 m)

- **Entrances:** 3.
  - North-west: the Section 2 wallrun exit, arriving at **+5 m** above the floor.
  - East: a slide-under opening at floor level.
  - South: a ramp down.
- **Cover — 5 pieces only:**
  - 2 container stacks, **12 × 6 × 4 m**, offset diagonally (not symmetrical).
  - 2 concrete pillars, **3 m diameter × 9 m**, one either side of the exit line.
  - 1 central raised platform, **10 × 10 m × 1.2 m high**, with ramps on two sides —
    this is the fight's focus point.
- **Clear floor:** roughly 60% of the arena is empty. Nothing between you and movement.
- **Verticality:** the platform (1.2 m, vaultable) and the container tops (4 m, reachable
  from the platform) give two extra levels without blocking sightlines.

### Arena 2 — The Smelter Hall (52 × 52 m)

- **Entrances:** 4 — north, east, south (from Section 4's slide) and a mezzanine stair.
- **Cover — 6 pieces:**
  - 2 furnace blocks, **10 × 8 × 6 m**, on the long axis.
  - 1 **central crucible**: a **12 m diameter pit in the floor** with a **1 m rim**.
    A hazard rather than cover — it forces movement in the middle instead of camping.
  - 3 pipe clusters, **4 m diameter × 12 m tall**, in a broken line.
- **Verticality:** a **mezzanine ring at +6 m** around two sides, reached by two stairs.
  Gives a vantage point, an escape route, and a route for the exit gap.
- **Clear floor:** the floor between the crucible and the perimeter is ~15 m wide all
  round — enough to sprint a circle.

---

## 4. Boss arena — The Crucible (90 × 90 m)

The largest space in the course, and the only one open to the sky.

- **Setting:** the floor of a colossal foundry housing. The roof is gone; embers rise
  into the night. Four fire grates in the floor are the main light, so the arena floor
  is lit in pools with darkness between them.
- **Central space:** **50 × 50 m** entirely clear — this is where the fight happens.
- **Cover — 6 pieces only:** pillars **4 m diameter × 18 m tall**, arranged in a
  *broken* ring at 20 m radius (deliberately not symmetrical, so the space never feels
  like a generated arena).
- **Elevated edge:** a **ring catwalk at +8 m** around the entire perimeter, reached by
  four ramps. This is the "variety" element — the player can break line of sight, drop
  from the ring into the centre, or be knocked off it.
- **Hazards:** 4 fire grates, **6 m diameter**, set between the pillars. Telegraphed by
  light, avoidable, but they punish standing still.
- **Entrances:** the monumental south doorway (main), plus two catwalk-level side
  entrances — so the boss cannot simply hold the door.

**Build-up:** the course escalates 44 m → 52 m → 90 m in arena scale, and the
traversal before it escalates from single obstacles (S1) to chains of five (S6). The
descent ramp and doorway exist purely to make the player feel the size of the arena
before they step into it.

---

## 5. Route map (top-down, not to scale)

```
        BOSS ARENA "THE CRUCIBLE" (90 x 90)
     +--------------------------------------+
     |   o      [F]        o        [F]     |     o = pillar (18m)
     |        o      ring catwalk (+8m)     |     F = fire grate
     |   [F]        +-------------+         |
     |              |  50 x 50    |         |
     |   o          |   clear     |    o    |
     |              +-------------+         |
     |   [F]    o        [F]          o     |
     +------------------+-------------------+
                        |  monumental doorway (10 x 14)
                 RAMP   |  40m x 8m, -14m
                        |
   SECTION 6 "GANTRY RUN"  (130m)
   [mezz]--WALLRUN--jump--slide--POLES x6--vault x4--fullvault--6m jump--ramp
                        |
   SECTION 5  ARENA 2 "SMELTER HALL" (52 x 52)
   [furnace]  (o)  (12m crucible)  (o)  [furnace]
                    mezzanine +6m ring
                        |
   SECTION 4 "FOUNDRY WALL"  (150m)
   8m gap--WALLRUN 26m--5m--WALLRUN 18m--jump--catwalk--9m pit--POLES x5--slide
                        |
   SECTION 3  ARENA 1 "CONTAINER YARD" (44 x 44)
   [container]      [raised 10x10]      [container]
        (o)                                (o)
                        |
   SECTION 2 "CONTAINER STACK"  (110m)
   5 half-vaults--FULL VAULT--slide--6.5m gap--POLES x4--WALLRUN 24m
                        |
   SECTION 1 "DOCK APPROACH"  (90m)
   start--slide--3 half-vaults--5m gap--WALLRUN 18m--drop
                        |
                     [ START ]
```

Route is a single continuous line with one main path throughout. No branching, no
backtracking — the only optional content is which side of the arena cover you run past.

---

## 6. Checklist

### Required elements — all present

| Requirement | Where | Count |
|---|---|---|
| **Slide under** (1 m opening, 8 m+ run-up) | S1 (12 m run-up), S2, S4 exit, S6 | **4** |
| **Half-body vault** (~1 m) | S1 ×3, S2 ×5, S4 ×2, S6 ×4 | **14** |
| **Full-body vault** (1.5–2 m) | S2 (2.4 m container), S6 (1.8 m barrier) | **2** ⚠ needs vault ability |
| **Pits / sprint jumps** | S1 5 m, S2 6.5 m, S4 8 m + 5 m + 9 m, S5 6 m, S6 5 m + 6 m | **8** |
| **Wallrun** — long, pit below, clear exit | S1 18 m, S2 24 m, S4 26 m + 18 m, S6 20 m | **5** |
| **Wallrun connecting two buildings** | S4 wallrun #3, 12 m pit, ledges both ends | **1** |
| **Wallrun into a jump** | S4 wallrun #4 → 6 m gap | **1** |
| **Hanging poles** (3–4 m high, 3–5 m apart, over a drop) | S2 ×4, S4 ×5, S6 ×6 | **15** ⚠ needs swing ability |
| **Combat pits** (40–60 m, few large cover, multiple exits) | Arena 1 44 m (3 exits, 5 cover), Arena 2 52 m (4 exits, 6 cover) | **2** |
| **Boss arena** (70–100 m, elevated ring, central space) | The Crucible 90 m, +8 m ring catwalk | **1** |
| **Course build-up to boss** | Arena scale 44 → 52 → 90; obstacle chains 1 → 5 moves | ✓ |

### Scale audit — nothing below the 6 m rule

Every walkable surface in the design, smallest first:

```
wallrun exit ledges      8 x 8 m      S1
catwalk width            4 m wide     S4, S6   (corridor, not a platform)
mezzanine                18 x 10 m    S6
landing                  14 x 10 m    S6
pole far landing         16 x 12 m    S2
roofs                    20 x 14 m    S2
                         20 x 16 m    S4
                         24 x 18 m    S1, S2, S4
                         26 x 20 m    S4
arenas                   44 x 44 m    S3
                         52 x 52 m    S5
                         90 x 90 m    S7
raised platform          10 x 10 m     S3
```

**Smallest platform: 8 × 8 m.** Everything else is 10 m or larger. No platform under
6 m exists anywhere in the layout.

### Prohibited-content audit

- **No small floating platforms.** Every surface is a roof, a deck, an arena floor or a
  catwalk attached to architecture. Nothing floats in a void.
- **No cube/block chains.** The only repeated small elements are vault obstacles of
  identical purpose (railings, containers, barriers) placed as a *rhythm*, not as
  stepping stones.
- **No filler props.** Every object in the document is a run surface, a vault, a slide
  opening, a wallrun wall, a cover piece, a hazard or a landing.
- **No pixel-perfect jumps.** Every gap is 4–9 m, and every gap over 6.5 m has a 3–5 m
  drop to extend the jump. No gap requires landing accuracy.
- **No empty dead space.** The longest stretch without an obstacle is the 40 m boss
  descent ramp — deliberate, for pacing and spectacle.
- **Scale variety.** 24 m rooftops sit next to 4 m catwalks; the 90 m boss arena sits
  next to a 16 m corridor.

### Pacing audit

```
S1  easy teaching        26 s   movement   (breathing)
S2  build-up             28 s   movement   (building)
S3  COMBAT               40 s   fight      (spike)
S4  hardest traversal    50 s   movement   (peak)
S5  COMBAT               50 s   fight      (spike)
S6  climax traversal     40 s   movement   (peak 2)
S7  BOSS                 68 s   fight      (climax)
                          ---
                         302 s  =  ~5 min
```

Movement 144 s (2.4 min) · combat ~158 s (2.6 min) — inside the 4–5 min brief, with
movement at 2.4 min against the 2–2.5 min target.

---

## 7. Notes for implementation

- This replaces the current 11-stage ring layout. The existing `EmberAscentBuilder`
  generates from layout tables, so the geometry can be rebuilt from this document
  directly.
- **The three ability dependencies must be built before the sections that use them**, or
  those obstacles become impassable: full-body vault (2 obstacles), poles (15 poles
  across 3 sections), wallrun (5 walls, currently unfinished).
- Six gap sizes are load-bearing for fairness (all the 6.5–9 m ones exist because of
  their drops). If the drop is removed in the build, the gap must shrink to ≤6 m.
