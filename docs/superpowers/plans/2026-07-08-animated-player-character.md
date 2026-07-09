# Animated Player Character Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the gray-box player capsule with a CC0 animated ranger while preserving all existing camera and build interactions.

**Architecture:** Merge Quaternius animations into the matching ranger glTF at build time, then instance that asset from a dedicated `Player.tscn`. Keep animation selection pure and unit-tested while `PlayerController` owns movement state and model-only facing.

**Tech Stack:** Godot 4.7 .NET, C# 12/.NET 8, xUnit 2.5.3, Python 3 standard library for deterministic glTF processing.

## Global Constraints

- Preserve `Player/Yaw/SpringArm3D/Camera3D` for `BuildController`.
- Use only CC0 or explicitly commercially usable assets and record them in `CREDITS.md`.
- Use non-root-motion animations.
- Do not add Godot dependencies to `SimCore`.
- Do not add a `Co-Authored-By` trailer.
- Commit message includes `TEO-14`.

---

### Task 1: Deterministic animated glTF asset

**Files:**
- Create: `tools/merge_gltf_animations.py`
- Create: `tools/tests/test_merge_gltf_animations.py`
- Create: `game/assets/characters/Male_Ranger_Animated.gltf`
- Create: `game/assets/characters/Male_Ranger_Animated.bin`
- Create: `game/assets/characters/*.png` (six referenced textures)
- Create: `game/assets/characters/licenses/*.txt`

**Interfaces:**
- Consumes: Quaternius `Male_Ranger.gltf`, `Male_Ranger.bin`, and `UAL1_Standard.glb`.
- Produces: `Male_Ranger_Animated.gltf` with `Idle_Loop`, `Jog_Fwd_Loop`, and `Jump_Loop` among its 43 animations.

- [ ] **Step 1: Write merge tests**

Test exact bone-name mapping, accessor and buffer-view offsets, required animation names, and byte-identical repeated output using synthetic glTF/GLB fixtures.

- [ ] **Step 2: Run tests to verify failure**

Run: `python -m unittest tools.tests.test_merge_gltf_animations -v`

Expected: FAIL because `tools.merge_gltf_animations` does not exist.

- [ ] **Step 3: Implement the merger**

Parse glTF JSON and GLB JSON/BIN chunks, append the donor binary on a four-byte boundary, offset donor buffer views and accessors, map animation channel targets by node name, and emit stable compact JSON. Raise `ValueError` for duplicate or missing target names.

- [ ] **Step 4: Generate the shipped asset**

Run the tool against the selected ranger and non-root-motion animation library. Copy only the six URI-referenced textures and both original CC0 license files.

- [ ] **Step 5: Verify tests and generated metadata**

Run: `python -m unittest tools.tests.test_merge_gltf_animations -v`

Expected: all tests pass and required animation names are present.

### Task 2: Animation state contract

**Files:**
- Create: `game/scripts/PlayerAnimationSelector.cs`
- Create: `game.Tests/game.Tests.csproj`
- Create: `game.Tests/PlayerAnimationSelectorTests.cs`
- Modify: `dev_game.sln`

**Interfaces:**
- Produces: `PlayerAnimationSelector.Select(bool isMoving, bool isAirborne)` returning `Idle_Loop`, `Jog_Fwd_Loop`, or `Jump_Loop`.

- [ ] **Step 1: Add failing selector tests**

Cover idle, moving, airborne, and airborne-plus-moving priority.

- [ ] **Step 2: Verify failure**

Run: `dotnet test game.Tests/game.Tests.csproj`

Expected: FAIL because `PlayerAnimationSelector` is missing.

- [ ] **Step 3: Implement the selector**

Return `Jump_Loop` when airborne, otherwise `Jog_Fwd_Loop` when moving, otherwise `Idle_Loop`.

- [ ] **Step 4: Verify green**

Run: `dotnet test game.Tests/game.Tests.csproj`

Expected: all selector tests pass.

### Task 3: Player scene and runtime animation

**Files:**
- Create: `game/scenes/Player.tscn`
- Create: `game/scripts/PlayerAnimator.cs`
- Create: `game.Tests/PlayerSceneContractTests.cs`
- Modify: `game/scripts/PlayerController.cs`
- Modify: `game/scripts/Main.cs`

**Interfaces:**
- `PlayerController.IsMoving` is publicly readable with a private setter.
- `PlayerController.IsAirborne` returns `!IsOnFloor()`.
- `PlayerAnimator` reads both properties and plays the selected name with a 0.15-second blend.

- [ ] **Step 1: Add a failing scene-contract test**

Assert `Player.tscn` contains `Player`, `ModelRoot`, the imported animated ranger, `PlayerAnimator`, and `Yaw/SpringArm3D/Camera3D`, and that `Main.cs` loads `res://scenes/Player.tscn` without `BuildPlayer()`.

- [ ] **Step 2: Verify failure**

Run: `dotnet test game.Tests/game.Tests.csproj`

Expected: FAIL because `Player.tscn` and `PlayerAnimator` are missing.

- [ ] **Step 3: Build `Player.tscn` and load it from `Main`**

Use the hierarchy in the approved design. Keep the node name `Player`, its initial position `(0, 1, 0)`, capsule collision, and camera settings from the current programmatic player.

- [ ] **Step 4: Add movement state and model-only facing**

Update `IsMoving` each physics tick. Rotate `ModelRoot` smoothly toward non-zero world-space direction without changing the body or `Yaw`.

- [ ] **Step 5: Implement `PlayerAnimator`**

Validate node and animation presence in `_Ready()`. Play only on state changes with `customBlend: 0.15`.

- [ ] **Step 6: Verify tests**

Run: `dotnet test game.Tests/game.Tests.csproj`

Expected: all selector and scene-contract tests pass.

### Task 4: Credits and full verification

**Files:**
- Modify: `CREDITS.md`
- Include: `AGENTS.md`

- [ ] **Step 1: Record asset provenance**

Add rows for Universal Base Characters, Modular Character Outfits - Fantasy, and Universal Animation Library with official URLs, CC0 1.0, and precise usage.

- [ ] **Step 2: Run full automated verification**

Run: `dotnet test`

Run: `dotnet build`

Run: `python -m unittest tools.tests.test_merge_gltf_animations -v`

Expected: every command exits 0 with no failures.

- [ ] **Step 3: Run Godot verification when available**

Run: `godot --headless --path game --editor --quit`

Run: `godot --headless --path game --quit-after 5`

Expected: project imports and starts without errors.

- [ ] **Step 4: Manual acceptance check**

Run F5 and verify idle/run/jump switching, 0.15-second blending, model-facing movement, unchanged camera behavior, and working left/right-click build interactions.

- [ ] **Step 5: Commit all issue files with AGENTS.md**

Run: `git commit -m "feat(game): add animated ranger player TEO-14"`
