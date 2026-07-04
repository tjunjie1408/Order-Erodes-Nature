# Month 6: M5 First Half — Save Persistence to Disk and Signal System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Pre-start calibration (required reading):** Verify the actual interfaces from the previous five months before starting work; step-level code is to be filled in during calibration according to the interface contracts and test lists in this document.

**Goal:** Upgrade saves from in-memory snapshots to full file-based save/load (slots / autosave / version-migration skeleton); ship the signal system (beacons, named channels, signal nodes, sensor runes / trigger crystals), enabling multi-golem cooperation.

**Architecture:** The signal subsystem (`SimCore/Signals/`) is kept independent per ISP: it maintains a world signal board mapping "channel → this tick's value" (pure data, double-buffered — writes in this tick become readable next tick, eliminating same-tick read/write ordering dependencies and preserving determinism); the circuit VM reads/writes the board via newly added OpCodes; no other subsystem references Signals types. The save file layer lives on the presentation side (`game/scripts/SaveManager.cs`); SimCore is responsible only for producing and restoring snapshots (existing responsibilities unchanged).

**Spec sources:** Design doc §5 (signal system / multi-golem cooperation), §4 (saves), §8 (M5)

## Global Constraints

- All prior iron rules still apply. New additions:
  - **Signals have latched semantics (spec §5):** a channel value persists after being written, until it is overwritten or explicitly cleared — it does not disappear when the writer stops sending (a golem suspended in a coroutine cannot keep re-sending every tick; non-latched semantics would cause a signal cliff-drop). Double-buffered: written at tick N, readable at tick N+1; when multiple writers write the same tick, the **maximum value** wins; unwritten channels **retain their old value** (not zeroed)
  - The save file carries a `Version` field; when `FromJson` encounters a lower version it runs the migration function chain (this month only needs one real migration, v1→v2, as skeleton validation)

## Task List

### Task 1: Signal Board with Double Buffering (TDD)

**Files:** Create `SimCore/Signals/SignalBoard.cs`; Modify `Simulation` (call `SignalBoard.Flip()` at end of tick); Test `SignalBoardTests.cs`

Interface contract:
```csharp
public sealed class SignalBoard
{
    public void Write(int channel, double value);   // buffered on write (same-tick writes merge by max)
    public void Clear(int channel);                 // explicit zeroing (takes effect next tick)
    public double Read(int channel);                // read current latched value; never-written = 0
    public void Flip();                             // tick boundary: merge this tick's writes into the latched table
}
```
Test list: `WriteIsInvisibleUntilFlip`, `MultipleWriters_MaxWins`, `NeverWrittenChannel_ReadsZero`, `LatchedValue_PersistsWithoutRewrites` (after a single write, the value is still readable after 100 consecutive Flips — the core of latching), `Clear_ZeroesChannel_NextTick`, `Board_SurvivesSaveRoundTrip`

Commit: `feat(signals): double-buffered world signal board`

### Task 2: Signal Nodes and OpCodes (TDD)

**Files:** Modify `NodeCatalog` (+4 nodes: `event_on_signal` (On Signal Received; inline parameters channel + threshold), `data_read_signal` (Read Signal: channel → Number), `action_send_signal` (Send Signal: channel, value), `action_clear_signal` (Clear Signal: channel — the companion to latched semantics, preserving the "absolutely decisive tactile feel")), `OpCode` (+ReadSignal/SendSignal/ClearSignal), the compiler (multi-event entry-point support: `CompiledCircuit` gains `List<(int channel, double threshold, int entryPc)> SignalEntries`; when the VM is idle and a signal crosses its threshold, it starts from the corresponding entry point), `CircuitVm`
- Test: compiler multi-entry cases + VM signal-trigger cases (`IdleVm_StartsAtSignalEntry_WhenChannelCrossesThreshold`, `RunningVm_IgnoresSignalEntries` — a running VM is not preempted, an MVP simplification rule)

Commit: `feat(circuits): signal nodes with multi-entry programs`

### Task 3: Sensor Runes and Trigger Crystals (In-World Signal Entities) (TDD)

**Files:** Create two fixed structure modules under `SimCore/Signals/`: `module_sensor_rune` (detects the count of items/golems within a radius of 3 cells and writes it to the configured channel), `module_trigger_crystal` (reads the configured channel; when the threshold is crossed, starts/stops its owning structure); presentation-side configuration panel (aim and press E: channel number SpinBox + threshold)
- Test: `SensorRune_WritesGolemCount`, `TriggerCrystal_HaltsStructure_BelowThreshold`
- Manual acceptance: without opening the circuit editor, achieve "pause the harvesting structure when the warehouse is full" using only placed runes + crystals (spec: redstone-style intuitive automation)

Commit: `feat(signals): sensor runes and trigger crystals`

### Task 4: Multi-Golem Cooperation Acceptance Scenario

**Files:** Test `SimCore.Tests/Machines/CooperationTests.cs`
- **Machine-readable acceptance** (the "signal cooperation" item defined in the spec's MVP): golem A harvests and, when fully loaded, calls `send_signal(1, cargo)`; golem B idles with `event_on_signal(1, threshold 8)` armed and comes over to shuttle the cargo. Assert that within 5000 ticks the storage obelisk throughput > single-golem baseline
- Manual playtest of the same scenario to confirm the experience holds up (including M4's visual/audio feedback)

Commit: `test(sim): two-golem signal cooperation acceptance`

### Task 5: Save Persistence to Disk (Slots / Autosave / Version Migration)

**Files:** Create `game/scripts/SaveManager.cs` (`user://saves/slot_N.json`; F5 quick-save / F9 quick-load, autosave every 5 minutes, graceful error message on corrupted files), `game/scenes/SaveMenu.tscn` (minimal: three slots + timestamps); Modify `SimCore/Persistence` (Version=2: add the signal board; write the v1→v2 migration function + migration test `V1Save_MigratesAndLoads`)
- Note: loading = rebuilding the `Simulation` instance + a full presentation-side view rebuild (BuildController/GolemView provide `RebuildFromSim()` — walk the current simulation state and rebuild all views; this is also the general-purpose tool for fixing view-vs-simulation drift)
- Test: migration cases + `Autosave_DoesNotStallTick` (order-of-magnitude verification that save serialization on the main thread takes < 50ms; if exceeded, log it to the backlog rather than optimizing this month)

Commit: `feat(save): file persistence with slots, autosave, migration chain`

### Task 6: Tier 3 Unlock Wiring

**Files:** Modify `SimCore/Progression` (Tier 3 = the full signal system, threshold 10 shards; signal-related modules/nodes are rejected while locked)
- Test: `SignalNodes_LockedUntilTier3`
- Commit: `feat(progression): tier3 gates signal system`

## End-of-month definition of done

- Saves: three-slot save/load, autosave, v1 legacy saves migrate and load, and after loading, production lines keep running with views consistent
- Signals: both paths usable — pure entity-level (runes + crystals) and circuit-level (three nodes); the two-golem cooperation acceptance test stays green
- `dotnet test` fully green

## Explicitly out of scope (this month)

- Blueprint library, core cap, content fill-out, tutorial (Month 7)
