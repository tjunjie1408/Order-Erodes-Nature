# Month 7: M5 Wrap-up — Blueprint Library, Content Fill-out, and MVP Acceptance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Pre-start calibration (required reading):** Verify against the actual interfaces from the previous six months; this month the "MVP definition of done" is the sole north star — any new idea unrelated to acceptance goes straight to the backlog.

**Goal:** Rune blueprint library (write once, deploy a whole fleet), core limit, content fill-out (≥10 modules / ≥15 nodes / all 3 unlock tiers fully traversable), wordless tutorial, and finally complete MVP acceptance item-by-item against the spec.

**Architecture:** The blueprint library lives in SimCore (`SimCore/Blueprints/`): a blueprint = a named `CircuitGraphData` + compilation cache (BlueprintId → CompiledCircuit, compiled once and shared across machines — this is where spec §5 "instruction arrays statically bound to blueprint IDs" is fulfilled). Golem save data migrates from "embedded GraphJson" to "reference by BlueprintId" (anonymous inscription auto-generates an implicit blueprint; old saves are handled via version migration v2→v3).

**Spec sources:** Design doc §5 (blueprint library / core limit), §8 (MVP definition of done), §9 (testing strategy)

## Global Constraints

- All prior ironclad rules still apply. New (spec: "blueprint updates are lazy and safe"): the compilation cache is keyed by BlueprintId; **direct inscription onto a single machine = immediate Reset (the editor's explicit intent); blueprint edits = subscribers are only marked `PendingUpdate`**, silently swapping to the new instruction stream when the PC returns to the program entry (a natural loop boundary), and also swapping on crash state / manual Reset — mass-resetting a fleet instantaneously is forbidden (mid-flight transport golems would freeze in place, cargo-laden ones would re-run their initial logic while holding goods)

## Task List

### Task 1: Blueprint Library (TDD)

**Files:** Create `SimCore/Blueprints/BlueprintLibrary.cs`; Modify the inscription command family (`SaveBlueprintCommand(name, graph)`, `InscribeBlueprintCommand(golemId, blueprintId)`), `SimCore/Persistence` (v3: blueprint table + golems referencing BlueprintId; v2→v3 migration); Test `BlueprintTests.cs`

Test list:
- `SaveBlueprint_CompilesOnce_SharedAcrossGolems` (two golems inscribed with the same blueprint share a reference-equal `CompiledCircuit`)
- `EditBlueprint_MarksSubscribersPending_WithoutImmediateReset` (subscribers currently executing an action are not interrupted)
- `PendingSubscriber_SwapsAtLoopBoundary` (swaps to the new instruction stream when the PC returns to the entry point; cargo and position are preserved)
- `CrashedSubscriber_SwapsOnReset`
- `PendingUpdate_SurvivesSaveRoundTrip` (half-swapped state round-trips through the save)
- `DeleteBlueprintInUse_IsRejected` (error code `blueprint_in_use`)
- `V2Save_MigratesGolemGraphsToImplicitBlueprints`

UI: Add a "save as blueprint / load from blueprint" dropdown to the editor + a blueprint management panel (rename / delete / reference count display).

Commit: `feat(blueprints): named circuit library with shared compilation`

### Task 2: Core Limit (TDD)

**Files:** Modify `SimCore/Progression` (active golem limit: initially 2, Tier2 → 4, Tier4 → 8; SpawnGolem over the limit is rejected with error code `core_limit`); Tier4 = blueprint library + core limit + advanced modules, threshold 20 truth shards
- Test: `GolemSpawn_RejectedAtCoreLimit`, `Tier4_RaisesLimit`
- Commit: `feat(progression): core limit and tier4`

### Task 3: Content Fill-out (data-table work)

**Files:** Modify `NodeCatalog`/`ModuleCatalog`/`RecipeCatalog` — fill up to the spec quantities:
- Fill nodes up to the target quantity: 16 already exist (Month 2's 10 + Month 3's 2 relative-addressing sensors + Month 6's 4; test-only nodes not counted), so ≥15 is already met — this month, add nodes based on experience gaps: `data_arith` (add/subtract/multiply), `data_counter` (counter, VM-local state), `action_toggle_structure` (start/stop a target structure), `sensor_detect_items` (detect item count within range), for a total of 20
- Fill modules up to ≥10: 9 already exist → add `module_beacon` (beacon, configurable broadcast) and the advanced module `module_overclock_core` (Tier4: speed ×2, energy cost ×3 — gives Tier4 a reason worth saving up 20 shards for)
- For each new node/module: catalog definition + compiler/VM support (new OpCode if needed) + unit tests + graybox → final model (reuse the M4 pipeline) + sound binding
- Recipe fine-tuning: ensure the shard curve for all three unlock tiers can be completed in 3-5 hours (calibrated via playtesting)

Commits batched per node/module.

### Task 4: Wordless Tutorial

**Files:** Create `game/scripts/TutorialDirector.cs` (a state machine that listens to SimEvent to advance stages) + guidance visuals (glowing guide lines, highlighted outline of the target structure, a half-finished circuit pre-placed in the editor on first open — one connection missing, the player completes it and the first lesson is done)
- Stages: ① walk up to the initial golem → ② open the editor → ③ complete the pre-placed circuit and inscribe it → ④ the golem starts working → ⑤ guide to the first mana spring → end (from then on the unlock tree carries the player)
- Spec constraint: no narrative, no text — guidance uses only visual language (light rays, highlights, icons); UI hints are limited to input-key icons
- Acceptance: find a friend who has never seen the project to playtest; without hints, they complete the first gathering circuit within 10 minutes

Commit: `feat(game): wordless tutorial director`

### Task 5: MVP Acceptance (item-by-item against the spec)

Accept each item of the spec §8 MVP definition of done, with one piece of evidence per item (a test or a playtest recording):

| Acceptance item | Evidence form |
|---|---|
| Write a gathering program for a golem | Playtest recording of the tutorial flow |
| Assemble the first automated production line | `FullChain_RawToTruthShard` evergreen + playtest |
| Use signals to make two golems cooperate | `CooperationTests` evergreen + playtest |
| Unlock at least one tech tier | `ProgressionTests` + playtest |
| Save, quit, then load and continue | Migration-chain tests + playtest (including closing and reopening the process) |
| A new player can accomplish the above without guidance | Real test with external playtesters (≥2 people) |

Additional engineering acceptance:
- Performance smoke test: scripted placement of 500 structures + 8 golems + a full-load production line, tick time < 10ms, frame rate ≥ 60 (if not met → apply the spec's contingency plan of MultiMesh / data-layout optimization; measure first, then act)
- `dotnet test` all green; `grep -r "using Godot" SimCore/` is empty; CREDITS.md corresponds one-to-one with the actual assets

Commit: `chore: mvp acceptance evidence and performance smoke`

### Task 6: Ship a Distributable Demo Build

**Files:** Godot export preset (Windows), packaged as a zip and sent to playtesters
- Icon / window title / version number 0.1.0; export template installation and a one-click export script
- Commit: `chore: v0.1.0 demo export preset`

## End-of-month definition of done (= MVP achieved)

All six items of spec §8 pass + performance smoke test meets targets + distributable zip. Once achieved, pop the champagne, then return to the design doc §2 backlog (vegetation withering, truth core matrix, lunar-phase mana...) to plan the next phase.

## Explicitly out of scope (this month)

- Steam page / marketing (revisit after MVP validation), localization, any backlog items
