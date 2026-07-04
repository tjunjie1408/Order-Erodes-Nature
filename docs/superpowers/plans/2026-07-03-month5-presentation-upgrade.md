# Month 5: M4 — Presentation Upgrade (shaders, glitch aesthetics, sound effects, UI theme) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Pre-start calibration (must read)**: This month's work operates on the actual output of the previous four months; the visual baseline is the real scene as it exists at that time. What this plan locks down is the **deliverables list, the palette contract, and the acceptance criteria**; the concrete shader code and numeric values are filled in at calibration time. The spec states explicitly: Codex is good at shader algorithms but bad at tuning visual parameters — all numeric tuning (glow strength, color curves, distortion amplitude) is done personally by the developer, and the plan steps are already split according to this division of labor.

**Goal:** Upgrade the graybox game to the target art style of the design document: the *Order Erodes Nature* contrast aesthetic — natural continent (free assets) + cold-glowing geometric megastructure (self-made) + glitch aesthetics + sound effects + UI theme.

**Architecture:** The visual systems all live in `game/`, with zero changes to SimCore (sole exception: emit additional events if any are missing). Establish three reusable asset layers: `game/shaders/` (shader suite), `game/theme/` (palette constants and UI Theme resource), `game/audio/` (event → sound-effect binding table). All visual bindings follow the existing "listen to SimEvent → drive shader parameters/particles/sound effects" pattern.

**Spec source:** Design document §2 (art theme / atmosphere mechanics), §6 (asset strategy / unification discipline)

## Global Constraints

- All prior iron rules carry over. New this month (from spec §6 unification discipline, hard-coded as the asset-layer contract):
  - **Palette locked**: nature's warm colors ≤8 colors; megastructure is black/white/gray + a single unique cold glow color `#4FC3F7` (ice blue) + a single unique warning color `#FF1744` (scarlet). All defined as constants in `game/theme/Palette.cs`; any material/UI/particle is forbidden from hard-coding other colors
  - External assets are "washed" on entry: recolored into the palette and unified onto the base materials
  - Every asset is logged in `CREDITS.md` the moment it enters; only accept CC0 / explicitly commercially usable licenses

## Task List

### Task 1: Palette and Theme Infrastructure

**Files:** Create `game/theme/Palette.cs` (all color constants), `game/theme/main_theme.tres` (Godot Theme: fonts, buttons, panels, GraphEdit color scheme — high-contrast grid, dark background, ice blue as the primary color, scarlet used only for errors)
- Fonts: Source Han Sans / Noto Sans SC (SIL OFL, Chinese support) + JetBrains Mono (numbers/labels in the circuit editor, SIL OFL), logged in CREDITS.md
- Apply the theme to the entire project's UI; change the circuit editor's connection wire colors to: Exec=white, Number=ice blue, Bool=amber, Vector=teal (amber/teal are picked from the 8-color nature band and written into Palette)
- Acceptance: no default gray Godot skin remnants anywhere in the UI; Commit `feat(theme): locked palette and ui theme`

### Task 2: Natural Continent Scene Upgrade

**Files:** Modify terrain construction; Create `game/assets/nature/`
- Replace the gray-green flat slab with the Kenney Nature Kit / Quaternius low-poly kits: an undulating terrain mesh (a 200×200 hand-crafted terrain sculpted in Blender or inside Godot, not procedurally generated), tree/rock/grass scattering (using Godot's MultiMeshInstance3D scatter), and a natural form for the mana spring (glowing pool + rising particles)
- Wash the assets: recolor all models into the 8-color nature band, unified flat materials
- WorldEnvironment first pass: sky gradient, ambient light, light fog (values tuned by the developer)
- Acceptance: screenshot comparison confirms the design document's "warm low-poly nature" intent holds; Commit `feat(game): nature terrain with washed cc0 assets`

### Task 3: Megastructure Module Modeling (developer Blender week + integration)

**Files:** Create `game/assets/monolith/*.glb` (one per module: base / harvesting prism / capacity crystal / amplification rune / inscription slot / floating core / rail / track / magic circle / storage stele / mana spring intake / golem body)
- Modeling discipline (spec §6): only cubes/cylinders/cones + bevels, no organic surfaces, ≤500 tris per module, two base color schemes — obsidian / white concrete — plus glowing groove faces (emission material slot reserved for shaders)
- The bulk of this task is **the developer's own hands-on Blender work** (first week: tutorials + modeling each module one by one); Codex is responsible for the import pipeline script (glb → unified material replacement → collision generation) and the BuildController view replacement
- Acceptance: all graybox blocks are replaced with the official module models, floating modules have procedural animation (self-rotation/hovering, generalizing the Month 3 GolemView pattern); Commits batched per module

### Task 4: Cold Emission Shader Suite

**Files:** Create `game/shaders/`: `emission_pulse.gdshader` (energy pulse, brightness driven by satisfaction ratio — consumes the M3 EnergyReport binding), `fresnel_rim.gdshader` (rim lighting), `rune_flow.gdshader` (flowing rune texture for rails/tracks, UV scrolling), `hologram_ghost.gdshader` (ghost preview upgrade: holographic mesh feel)
- Division of labor: Codex writes the first-draft algorithms from references (godotshaders.com, license noted) + exposes uniforms; the developer tunes all numeric values
- WorldEnvironment second pass: Glow enabled (the source of the glowing geometry's material quality), volumetric fog, tonemapping (tuned by the developer)
- Acceptance: while the production line runs, rails have flowing light, machines breathe brighter/dimmer with satisfaction ratio, and the cold/warm contrast between the megastructure and nature holds; Commit `feat(shaders): cold emission suite`

### Task 5: Glitch Aesthetics (visualizing VmCrashed)

**Files:** Create `game/shaders/glitch_corruption.gdshader` (garbled-noise corruption eroding the surface — per-entity material, safe), `game/shaders/space_distortion.gdshader` (gravitational lensing spatial distortion — **spec implementation red line: one globally unique post-processing Pass**, per-entity screen-space materials are forbidden (30 machines crashing simultaneously = 30 layers of fullscreen sampling = an Overdraw disaster); crash coordinates are passed in via `uniform vec3 crash_positions[16]` + `uniform int crash_count`, sorted by distance to the camera and truncated, and all lenses are computed in a single fullscreen pass), `game/scripts/CrashVfx.cs` (listens to the `VmCrashed`/`Reset` events: maintains the crash-coordinate array fed to the global Pass + attaches glitch_corruption per entity)
- Editor side: on crash, the node corresponding to CrashPc flashes scarlet continuously + an error explanation ("the circuit has fallen into infinite recursion")
- Acceptance: inscribe an infinite-loop circuit → the golem hovers in place, its surface garbles, space distorts, and the editor pinpoints the crashed node; everything recovers after Reset; Commit `feat(vfx): rational collapse glitch aesthetics`

### Task 6: Sound Effects System

**Files:** Create `game/audio/AudioDirector.cs` (event → sound-effect binding table + AudioBus layout: Master/SFX/Ambient), `game/assets/audio/`
- Sourcing: Sonniss GDC packs + Freesound (CC0) + Kenney — machine hum (low-frequency loop, volume follows satisfaction ratio), crystal overtones (harvest complete), mechanical-keyboard-like UI sounds (node snapping/wiring/inscription — the spec's "decisive/assured feel"), glitch sound (digital tearing sound on crash), ambient bed (wind + distant low drone)
- Binding pattern: `AudioDirector` listens to `SimDriver.SimEventEmitted` and plays from a lookup table; 3D sound effects are attached at the corresponding view positions
- Acceptance: play for ten minutes with headphones on — not a single action is silent, and not a single sound effect is harsh or annoyingly repetitive; Commit `feat(audio): event-driven sfx and ambient beds`

### Task 7: Feel Polish and InputMap Formalization

**Files:** Modify `project.godot` (formal InputMap replaces physical-key polling), `PlayerController`/`BuildController` (read actions)
- Polish checklist (go through item by item, each item accepted by the developer): build-grid snapping is visually unambiguous, editor wire-snapping radius feels right, camera collision never clips through walls, placement/removal confirmed at 60fps with imperceptible latency, Esc hierarchy (editor → mouse release → pause)
- Acceptance: checked item by item against design document §2 "interaction feedback is decisive, no squash-and-stretch animations, zero sluggishness"; Commit `feat(game): input map and interaction polish`

## End-of-month definition of done (= M4 acceptance)

- Show a 3-minute gameplay recording to someone who has never seen the project, and they can accurately articulate the "warm nature vs. cold geometry" theme
- The running production line has flowing light, breathing glow, and sound effects; crashes produce a glitch spectacle; no graybox remnants anywhere
- `dotnet test` all green (there should be no SimCore changes this month, other than emitting additional events)

## Explicitly out of scope (this month)

- No new gameplay mechanics whatsoever (signals/blueprints belong to M5); music procurement (end of M5); performance optimization still requires measurement first
