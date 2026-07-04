# Month 4: M3 — Multi-Block Assembly, Energy Networks, and the Production System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Pre-start calibration (must read):** The interfaces in this plan are inferred from the deliverables of the Month 1-3 plans. As the very first step, verify the actual shape of `Simulation`/`Structure`/golems — in particular, "structure" needs to be upgraded from single-block to multi-block. If Month 3's actual implementation differs from these assumptions, revise this plan first. Step-level code is filled in at calibration time according to this document's interface contracts and test checklists.

**Goal:** Structures upgrade from single-block to multi-block module assembly (connectivity + capability aggregation); energy networks (mana springs / conduits / proportional throttling); hover-rail item transport; transmutation-circle recipe processing; a first draft of the unlock tree.

**Architecture:** All three new subsystems live in SimCore and are isolated from one another per ISP: **build topology** (connected-component management, producing "structure = module set + capability table" as pure data), **energy network** (an independent graph, producing a "per-network satisfaction ratio" table), and **items/recipes** (consumes the capability table and satisfaction ratios). No subsystem references another subsystem's types; interaction happens only through pure data tables aggregated by `Simulation` (spec §7 ISP iron rule).

**Spec sources:** Design document §5 (multi-block assembly, energy system, machine system, items and recipes, progression), §4 (performance design: topology is only recomputed on build/demolish ticks)

## Global Constraints

- All previous iron rules still apply. New this month:
  - Connectivity/energy topology recomputation happens only on the tick of a build/demolish command, using a local BFS — it must never enter the per-tick hot path
  - Per-structure module cap of **64** (a concretization of the spec's "on the order of tens of cells"; placements exceeding the cap are rejected with error code `structure_size_limit`)
  - **Interface-oriented prompting is strictly enforced starting this month** (SimCore is starting to bloat): every task fed to Codex gets only the current subsystem's code + the adjacent subsystems' interface contracts (the contract block at the top of this plan exists precisely for this purpose), never the adjacent subsystems' implementation source; if an interface file shows up in the diff, that is a red light — reject and send back

## Subsystem Interface Contracts (lock the signatures at calibration time; implement to these first)

```csharp
// —— Build topology subsystem (SimCore/Assembly/) ——
// Module type catalog (data table): module_base (base module), module_harvest_prism (harvest prism),
//   module_capacity_crystal (capacity crystal), module_amplifier (amplifier rune),
//   module_inscription_slot (inscription slot), module_hover_core (hover core)
public sealed class StructureInfo          // The topology subsystem's sole output (pure data)
{
    public int StructureId;
    public List<GridPos> Cells;
    public Capabilities Caps;              // Aggregated capability table
}
public struct Capabilities                 // All-trivial values
{
    public bool CanHarvest, CanMove, HasInscriptionSlot;
    public int CargoCapacity;              // Base 10 + 20 per capacity crystal
    public float SpeedMultiplier;          // 1.0 + 0.25 per amplifier rune
    public int EnergyDemandPerTick;        // Module count × 1
}
// Placing an adjacent module → merge into the structure and recompute Caps; demolishing → local BFS
// to determine whether it splits; a split produces new StructureIds, each recomputing its own Caps;
// a golem = a structure containing hover_core, with its VM/cargo attached to the StructureId

// —— Energy network subsystem (SimCore/Energy/) ——
// Entities: mana spring (source, produces 20/tick), energy conduit (transmission), structures join the network via conduit adjacency
public sealed class EnergyReport           // The energy subsystem's sole per-tick output (reused instance, zero allocation)
{
    // structureId → satisfaction ratio [0,1]; absent from the table = not connected = 0
    public Dictionary<int, float> Satisfaction;
}
// Satisfaction ratio = min(1, network total supply / network total demand); effect: action speed and recipe progress × satisfaction ratio
// (spec: throttle, never halt). A golem not connected to a network runs at 0.2× speed ("residual mana",
// avoiding an early-game deadlock — the starting golem must be able to work even with no energy network;
// this is the safety-net rule for the early-game experience)

// —— Items/recipes subsystem (SimCore/Items/) ——
// Items: glimstone (glimstone, raw material), aether_dust (aether dust, raw material),
//       glim_ingot (glim ingot), aether_lens (aether lens), logic_matrix (logic matrix),
//       truth_shard (truth shard)
// Recipes (transmutation circle; all require satisfaction ratio > 0, duration scales with satisfaction ratio):
//   glimstone×2 → glim_ingot (100 tick)
//   aether_dust×3 → aether_lens (150 tick)
//   glim_ingot×1 + aether_lens×1 → logic_matrix (200 tick)
//   logic_matrix×2 → truth_shard (400 tick)

// —— Unlock tree (SimCore/Progression/) ——
// UnlockState: truth_shard count held + set of unlocked tiers;
// Tier thresholds: Tier1 (fixed structures + hover rail) = 2 shards, Tier2 (transmutation circle + compute node + amplifier) = 5,
// Tier3/4 deferred to M5. Locked modules/nodes: placement/inscription commands are rejected with error code "locked"
```

---

### Task 1: Build Topology — Module Merging and Splitting (TDD)

**Files:** Create `SimCore/Assembly/` (ModuleCatalog/StructureInfo/AssemblyTopology); Modify `Simulation` (placement commands routed through the module catalog); Test `AssemblyTopologyTests.cs`

Test checklist (write all as failing tests first, then implement):
- `PlaceAdjacentModules_MergeIntoOneStructure`
- `Capabilities_AggregateFromModules` (base module + 2 capacity crystals → CargoCapacity=50)
- `RemoveBridgeModule_SplitsIntoTwoStructures_EachWithOwnCaps`
- `RemoveLeafModule_KeepsSingleStructure`
- **Tricky graph-algorithm cases (spec: distrust-the-graph-algorithm principle — never trust an AI-written-in-one-shot graph algorithm; write tests to lock the edge cases first, then implement):**
  - `RingStructure_RemoveOneSide_DoesNotSplit` (3×3 hollow ring, remove one side — the two paths around the ring remain connected; the case most likely to expose a BFS missing visited marking, causing infinite loops/false splits)
  - `CrossJunction_RemoveCenter_SplitsIntoFour` (cross shape, remove the center, splits into four)
  - `TwoRings_SharedEdge_RemoveSharedEdge_StaysConnected` (figure-eight shape, remove the shared edge)
  - `Bfs_Terminates_OnMaxSizeStructure` (add/remove on a 64-module structure at the full cap, assert completion in a finite number of steps)
- `SplitUsesLocalBfs_DoesNotTouchDistantStructures` (demolishing structure A does not change structure B's StructureId — locks in incrementality)
- `StructureExceeding64Modules_RejectsPlacement`
- `GolemIdentity_SurvivesModuleAddition` (adding a capacity crystal to a golem — does the VM not reset? — **No**: the spec says "any module change always resets the VM"; assert that the Reset happens and cargo is preserved)

Commit: `feat(assembly): multi-block structures with merge/split topology`

### Task 2: Energy Network (TDD)

**Files:** Create `SimCore/Energy/`; Modify `Simulation` (mana springs as world entities placed at map generation + `PlaceConduitCommand`); Test `EnergyNetworkTests.cs`

Test checklist:
- `IsolatedStructure_HasZeroSatisfaction_GolemRunsAtFloorSpeed`
- `ConnectedToSpring_FullSupply_SatisfactionOne`
- `DemandExceedsSupply_ProportionalThrottle` (supply 20, demand 40 → 0.5)
- `TwoSeparateNetworks_IndependentSatisfaction`
- `RemovingConduit_SplitsNetwork_RecalculatedOnce` (recomputation happens only on the demolish tick)
- `HarvestDuration_ScalesInverselyWithSatisfaction` (0.5 satisfaction ratio → harvest completes in 40 ticks)

Commit: `feat(energy): mana networks with proportional throttling`

### Task 3: Hover-Rail Item Transport (TDD)

**Files:** Create `SimCore/Items/` (RailSegment/ItemInTransit); Test `RailTransportTests.cs`

Design: a rail is a chain of adjacent cells; an item is represented as "rail segment + within-segment progress float", and each tick progress += speed × satisfaction ratio; on reaching the end, if adjacent to a storage obelisk/circle input it is handed over, otherwise it blocks (items behind queue up without overlapping). Test checklist:
- `ItemTraversesRail_AtSatisfactionScaledSpeed`
- `ItemsQueue_WithoutOverlap_WhenOutputBlocked`
- `RailEndAtStorage_DepositsItem`
- `BrokenRail_ItemsHalt_NoLoss` (demolish a middle segment; items stop in place and do not disappear)

Commit: `feat(items): hover-rail transport with blocking queues`

### Task 4: Transmutation Circles and Recipes (TDD)

**Files:** Create `SimCore/Items/RecipeCatalog.cs`; circle state folded into Assembly's capabilities (a `module_transmute_core` module grants CanTransmute); Test `TransmutationTests.cs`

Test checklist:
- `RecipeCompletes_AfterScaledDuration_ConsumingInputs`
- `MissingIngredient_Idles_WithoutConsuming`
- `OutputBlocked_HoldsFinishedItem_UntilSpace`
- `FullChain_RawToTruthShard` (integration: two harvest lines + rails + two-stage circles → produce a truth_shard within 5000 ticks — **M3's machine-readable acceptance**)

Commit: `feat(items): transmutation recipes and full production chain test`

### Task 5: Unlock Tree First Draft (TDD)

**Files:** Create `SimCore/Progression/UnlockState.cs`; Modify `Simulation` (placement/inscription validates unlocks; `SpendShardsCommand`); Test `ProgressionTests.cs`

Test checklist: `LockedModule_PlacementRejected`, `SpendShards_UnlocksTier_EmitsEvent`, `UnlockState_SurvivesSaveRoundTrip`

Commit: `feat(progression): shard-gated unlock tiers`

### Task 6: Presentation Layer — Module Building, Conduits, Circles, Rail Items (Gray-Box)

**Files:** Modify `BuildController` (build bar expanded into a module-list UI — bottom hotkey bar 1-9; gray-box views for modules/conduits/rails; items on rails rendered via MultiMesh or simple MeshInstance interpolation); first-pass energy satisfaction visual: structure emission brightness = satisfaction ratio (shader parameter; polish deferred to M4)

Manual acceptance (M3 gameplay acceptance): lay out a production line of "harvest prism structure → hover rail → transmutation circle → rail → storage obelisk", hook it up to a mana spring for power, observe throttling under full load; cut a conduit and watch items halt in place; demolish a bridge module and watch the structure split; accumulate 2 shards and unlock Tier1.

Commit: `feat(game): gray-box production line building and views`

### Task 7: Save Extension + Regression

Multi-block structures / energy networks / rail items / circle progress / unlock state all go through the save round-trip (add a SaveRoundTripTests case: `FullFactory_SurvivesRoundTrip_AndKeepsProducing`). `dotnet test` all green + playtest regression.

Commit: `feat(sim): full factory state in save round-trip`

## End-of-month definition of done (= M3 acceptance)

- A fully automated production line from raw materials to truth shards can be built, can be saved, throttles on power loss, and splits correctly on demolition — all correct
- The three new subsystems do not reference one another (confirmed by code review + AGENTS.md structure map updated)
- Integration tests `FullChain_RawToTruthShard` and `FullFactory_SurvivesRoundTrip` stay green

## Explicitly out of scope (this month)

- Signals/blueprint library (M5), all polish (M4), performance optimization (unless playtests show obvious frame drops — measure first, then act)
