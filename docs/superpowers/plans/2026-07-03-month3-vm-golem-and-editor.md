# Month 3: M2 Wrap-up — Suspendable VM, Golems, and GraphEdit Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Pre-start calibration (must read)**: As the very first step, verify the Interfaces against the actual code (especially Month 2's `Instruction`/`OpCode`/`CompiledCircuit`/`CircuitGraphData` signatures and the full set of error codes); if there is drift, revise this plan first. This month is the core-experience validation point of the entire project — by month's end you must be able to "program a golem and have it do work"; this goal must not be downgraded.

**Goal:** Implement the suspendable circuit VM, the golem/resource node/storage obelisk entities in SimCore, and the customized GraphEdit editor, wiring up end-to-end "connect nodes → inscribe → golem executes a harvest loop".

**Architecture:** The VM and the golem interact via the pure data structure `VmIo` (ISP iron rule): on each machine tick, the host first fills in sensor values → the VM executes some number of instructions → the machine reads out the action request and advances the action → on action completion, the status is written back. The editor is UI only: GraphEdit topology ⇄ `CircuitGraphData` conversion; compilation and error display go through Month 2's compiler. The VM's execution position flows to the editor via events (`VmNodeEntered`, throttled) for highlighting.

**Tech Stack:** Same as before; Godot `GraphEdit`/`GraphNode` controls

**Spec sources:** Design doc §5 (circuit execution model, runtime read-only iron rule, VM state serializability), §4 (rune circuit system / circuit compiler)

## Global Constraints

- All iron rules from the previous two months. This month adds two new red lines from the spec:
  - **The instruction stream of a running circuit is read-only**: modifying a circuit and re-inscribing it, or changing a golem's modules → force `Vm.Reset()` and rerun from the entry point
  - The VM's runtime state contains only plain values (POD) (`ProgramCounter`/`WaitTicksRemaining`/register array/action state enums); delegates are forbidden in save data
- The VM's per-tick instruction budget cap is 256 instructions — exceeding it is judged an infinite loop; the VM enters the `Crashed` state and emits an event (the mechanical hook for the malfunction aesthetic, consumed in M4)

## File Structure (new this month)

```
SimCore/Circuits/VmIo.cs               Pure data bridge between VM and host
SimCore/Circuits/CircuitVm.cs          Suspendable interpreter
SimCore/Machines/GolemState.cs         Golem simulation entity
SimCore/Machines/ResourceNode.cs       Resource node
SimCore/Machines/StorageState.cs       Storage obelisk
SimCore/SimVec3.cs                     3D vector for the simulation (float, zero Godot)
SimCore/Simulation.cs                  Wire golem/resource node/storage into tick and commands (Modify)
SimCore/Commands.cs                    Add inscribe/spawn-golem commands (Modify)
SimCore/SimEvents.cs                   Add VM/golem events (Modify)
SimCore.Tests/Circuits/CircuitVmTests.cs
SimCore.Tests/Machines/GolemTests.cs
SimCore.Tests/Machines/EndToEndHarvestTests.cs
game/scenes/CircuitEditor.tscn         GraphEdit editor UI
game/scripts/CircuitEditor.cs          UI⇄CircuitGraphData conversion, compile errors highlighted in red
game/scripts/GolemView.cs              Golem interpolation view
game/scripts/BuildController.cs        Placement and views for resource nodes/storage/golems (Modify)
```

---

### Task 1: VmIo and the Suspendable VM

**Files:**
- Create: `SimCore/Circuits/VmIo.cs`, `SimCore/Circuits/CircuitVm.cs`
- Test: `SimCore.Tests/Circuits/CircuitVmTests.cs`

**Interfaces:**
- Produces (locked):

```csharp
namespace SimCore.Circuits;

public enum VmStatus : byte { Idle, Running, Suspended, Crashed }
public enum ActionKind : byte { None, MoveTo, Harvest, Load, Unload, Wait }
public enum ActionResult : byte { InProgress, Done, Failed }

/// <summary>The sole interaction surface between the VM and the host machine. All plain values (POD).</summary>
public sealed class VmIo
{
    // Host → VM (filled in by the host before the tick)
    public double SensorCargo;
    public ActionResult PendingActionResult = ActionResult.Done;
    // VM → Host (read by the host after the tick)
    public ActionKind RequestedAction = ActionKind.None;
    public double ActionX, ActionY, ActionZ;   // MoveTo/Harvest target, or Wait tick count (stored in ActionX)
}

public sealed class CircuitVm
{
    public VmStatus Status { get; private set; } = VmStatus.Idle;
    public int ProgramCounter { get; private set; }
    public int CrashPc { get; private set; } = -1;
    public double[] Registers { get; private set; } = Array.Empty<double>();

    public void LoadProgram(CompiledCircuit circuit);   // references the read-only program + Reset
    public void Reset();                                 // PC=entry, registers zeroed, Status=Running
    /// <summary>Execute until suspension or budget exhaustion. Returns the instruction indices entered this tick (for highlighting; reuses the passed-in list).</summary>
    public void Tick(VmIo io, List<int> enteredPcs);
    public const int InstructionBudgetPerTick = 256;
}
```

- **Relative addressing nodes (spec red line: players never hand-type absolute coordinates)**: `NodeCatalog` gains `sensor_find_nearest_resource` this month (outputs nearest:Vector + found:Bool) and `sensor_nearest_storage` (same shape); corresponding OpCodes `FindNearestResource`/`FindNearestStorage` (C = base address of the target register group; three consecutive registers hold xyz, C+3 holds found). VmIo gains host-filled sensor targets: fields such as `SensorNearestResX/Y/Z, SensorNearestResFound` (the host queries the simulation's spatial index each tick and fills them in). `data_const_number` coordinate assembly and the editor's coordinate picker are retained as a fallback path
- **Sensor usage mask (spec "sensor cost model")**: `CompiledCircuit` gains `SensorMask Sensors { get; init; }` (`[Flags] enum SensorMask : uint { None=0, Cargo=1, NearestResource=2, NearestStorage=4 }`, set automatically by the compiler scanning the instructions); the host checks the mask before filling VmIo, and expensive spatial queries the program never uses are skipped outright. Add a compiler test: `SensorMask_ReflectsUsedSensorsOnly`
- Semantics: suspendable instructions (MoveTo/Harvest/Load/Unload/Wait) write the request into `io` and transition to `Suspended` when executed; at the start of the next tick, if `io.PendingActionResult == Done` then PC+1 and continue, `InProgress` keeps it suspended, and `Failed` also does PC+1 and continues (MVP simplification: failure does not crash; the behavior is for the circuit itself to judge via sensors). Non-suspendable instructions execute consecutively; exceeding 256 in a single tick → `Crashed` (record `CrashPc`). `Wait` counts down inside the VM itself (`WaitTicksRemaining` internal state), not through the host.

- [ ] **Step 1: Write failing tests** (hand-construct `Instruction[]` to test the VM directly, without depending on the compiler)

```csharp
public class CircuitVmTests
{
    private static CircuitVm Load(params Instruction[] ins)
    {
        var vm = new CircuitVm();
        vm.LoadProgram(new CompiledCircuit
        {
            Instructions = ins, RegisterCount = 8, StartEntry = 0,
        });
        return vm;
    }

    [Fact]
    public void Wait_SuspendsForNTicks_ThenContinues()
    {
        // r0=3; Wait r0; Halt
        var vm = Load(
            new(OpCode.LoadConst, 0, 0, 0, 3),
            new(OpCode.Wait, 0, 0, 0, 0),
            new(OpCode.Halt, 0, 0, 0, 0));
        var io = new VmIo(); var pcs = new List<int>();
        vm.Tick(io, pcs);                       // enters Wait, suspends
        Assert.Equal(VmStatus.Suspended, vm.Status);
        vm.Tick(io, pcs); vm.Tick(io, pcs);     // counting down
        vm.Tick(io, pcs);                       // finishes, proceeds to Halt
        Assert.Equal(VmStatus.Idle, vm.Status);
    }

    [Fact]
    public void MoveTo_WritesRequest_AndResumesOnDone()
    {
        var vm = Load(
            new(OpCode.LoadConst, 0, 0, 0, 5),   // x
            new(OpCode.LoadConst, 1, 0, 0, 0),   // y
            new(OpCode.LoadConst, 2, 0, 0, 7),   // z
            new(OpCode.MoveTo, 0, 1, 2, 0),
            new(OpCode.Halt, 0, 0, 0, 0));
        var io = new VmIo(); var pcs = new List<int>();
        vm.Tick(io, pcs);
        Assert.Equal(ActionKind.MoveTo, io.RequestedAction);
        Assert.Equal(5, io.ActionX); Assert.Equal(7, io.ActionZ);
        io.PendingActionResult = ActionResult.InProgress;
        vm.Tick(io, pcs);
        Assert.Equal(VmStatus.Suspended, vm.Status);   // still moving
        io.PendingActionResult = ActionResult.Done;
        vm.Tick(io, pcs);
        Assert.Equal(VmStatus.Idle, vm.Status);        // proceeds to Halt after completion
    }

    [Fact]
    public void TightJumpLoop_CrashesOnBudget()
    {
        var vm = Load(new(OpCode.Jump, 0, 0, 0, 0));   // self-jump infinite loop
        vm.Tick(new VmIo(), new List<int>());
        Assert.Equal(VmStatus.Crashed, vm.Status);
        Assert.Equal(0, vm.CrashPc);
    }

    [Fact]
    public void Reset_RestartsFromEntry_AndClearsCrash()
    {
        var vm = Load(new(OpCode.Jump, 0, 0, 0, 0));
        vm.Tick(new VmIo(), new List<int>());
        vm.Reset();
        Assert.Equal(VmStatus.Running, vm.Status);
        Assert.Equal(0, vm.ProgramCounter);
        Assert.Equal(-1, vm.CrashPc);
    }

    [Fact]
    public void JumpIfFalse_TakesFalseBranch()
    {
        // r0=0(false); JumpIfFalse r0 -> 3; Halt(should not be reached); LoadConst r1=9; Halt
        var vm = Load(
            new(OpCode.LoadConst, 0, 0, 0, 0),
            new(OpCode.JumpIfFalse, 0, 3, 0, 0),
            new(OpCode.Halt, 0, 0, 0, 0),
            new(OpCode.LoadConst, 1, 0, 0, 9),
            new(OpCode.Halt, 0, 0, 0, 0));
        vm.Tick(new VmIo(), new List<int>());
        Assert.Equal(9, vm.Registers[1]);
    }
}
```

Additionally add one test case each for `Compare` (all three modes) and `ReadSensor` (reads `io.SensorCargo`).

- [ ] **Step 2: Confirm failure** → **Step 3: Implement the VM** (single while loop + switch(op); suspension = write request, set status, return; budget counter decrements by 1 per instruction) → **Step 4: `dotnet test` all green** → **Step 5: Commit** `feat(circuits): suspendable vm with instruction budget and crash state`

---

### Task 2: Golems, Resource Nodes, Storage Obelisks (SimCore entities and action advancement)

**Files:**
- Create: `SimCore/SimVec3.cs`, `SimCore/Machines/GolemState.cs`, `SimCore/Machines/ResourceNode.cs`, `SimCore/Machines/StorageState.cs`
- Modify: `SimCore/Simulation.cs`, `SimCore/Commands.cs`, `SimCore/SimEvents.cs`
- Test: `SimCore.Tests/Machines/GolemTests.cs`

**Interfaces:**
- Produces:

```csharp
// Spec "unified numeric types": floating point inside SimCore is always double; float exists only at the presentation layer boundary
public readonly record struct SimVec3(double X, double Y, double Z);

// New commands
public sealed record SpawnGolemCommand(SimVec3 Position) : ICommand;
public sealed record PlaceResourceNodeCommand(GridPos Position, string ResourceType, int Amount) : ICommand;
public sealed record PlaceStorageCommand(GridPos Position) : ICommand;
public sealed record InscribeCircuitCommand(int GolemId, CircuitGraphData Graph) : ICommand;  // compiles internally; on failure emits CircuitRejected event

// New events
public sealed record GolemSpawned(int GolemId, SimVec3 Position) : SimEvent;
public sealed record CircuitRejected(int GolemId, List<CompileError> Errors) : SimEvent;
public sealed record CircuitInscribed(int GolemId) : SimEvent;
public sealed record VmCrashed(int GolemId, int CrashPc) : SimEvent;

// GolemState: Id, Position (SimVec3, with PrevPosition for interpolation), CargoCount, CargoType,
//   Vm(CircuitVm), Io(VmIo), CurrentAction advancement state
// Golem tick: fill Io sensors → Vm.Tick → read request → advance action:
//   MoveTo: move in a straight line in the plane toward the target at 3 cells/second (0.15/tick); distance < 0.1 counts as Done
//   Harvest: if there is a resource node within 1.5 cells of the target, then after 20 ticks Cargo+1, node Amount-1; otherwise Failed
//   Load/Unload: if there is a storage obelisk within 1.5 cells, instantly transfer all cargo; otherwise Failed
```

- [ ] **Step 1: Write failing tests** (key test cases)

```csharp
[Fact] public void Golem_MovesTowardTarget_AtFixedSpeedPerTick()
[Fact] public void Harvest_NearNode_TakesTwentyTicks_AndDecrementsNode()
[Fact] public void Harvest_FarFromNode_ReportsFailed_VmContinues()
[Fact] public void Unload_TransfersCargoToStorage()
[Fact] public void InscribeInvalidGraph_EmitsCircuitRejected_GolemUnchanged()
[Fact] public void InscribeWhileRunning_ResetsVm()          // runtime read-only iron rule
[Fact] public void CrashedVm_EmitsVmCrashedEvent_Once()
```

Each test case drives `Simulation` with commands, ticks a number of times, and asserts state and events (same style as Month 1's PlacementTests TickAndDrain pattern).

- [ ] **Step 2: Confirm failure** → **Step 3: Implement** (`Simulation.Tick` order: consume commands → tick each golem (including its VM) → TickCount++; golem storage uses a flat `List<GolemState>` traversal; `PrevPosition` is snapshotted at the start of each tick) → **Step 4: All green** → **Step 5: Commit** `feat(sim): golems with vm-driven actions, resource nodes, storage`

---

### Task 3: End-to-End Harvest Loop (pure SimCore acceptance test)

**Files:**
- Test: `SimCore.Tests/Machines/EndToEndHarvestTests.cs`

**Interfaces:**
- Consumes: all preceding interfaces. **This task is M2's machine-readable acceptance**: use GraphBuilder to construct a real circuit graph (not hand-written instructions) — "on start → move to resource node → harvest → move to storage obelisk → unload → (loop)", inscribe it via `InscribeCircuitCommand`, and run 2000 ticks.

- [ ] **Step 1: Write the tests**

```csharp
[Fact]
public void GolemProgrammedViaGraph_FillsStorage_OverTime()
{
    // Layout: golem (0,0,0), resource node (5,0,0, amount=10), storage obelisk (0,0,5)
    // Graph (relative addressing, no hardcoded coordinates):
    //   event_start → move_to(target=sensor_find_nearest_resource.nearest)
    //   → harvest(same as above) → move_to(target=sensor_nearest_storage.nearest) → unload → (auto loop back)
    // Assertions: after 2000 ticks, cargo in the storage obelisk >= 3 and the resource node's Amount decreased accordingly; the VM never Crashed
}

[Fact]
public void ResourceExhausted_GolemFindsNextNode_Automatically()
{
    // Two resource nodes; after the nearer one (amount=2) is exhausted, the same circuit should automatically turn to the farther one — the core payoff of relative addressing
}

[Fact]
public void NoResourceLeft_GolemKeepsCycling_WithFailedHarvests_NoCrash()
```

- [ ] **Step 2: Run the tests** — this will very likely expose compiler/VM/golem interaction bugs; fix them one by one until green. This is the most important debugging period of the month; reserve ample time.
- [ ] **Step 3: Commit** `test(sim): end-to-end programmed harvest loop`

---

### Task 4: GraphEdit Circuit Editor (UI)

**Files:**
- Create: `game/scenes/CircuitEditor.tscn`, `game/scripts/CircuitEditor.cs`
- Modify: `game/scripts/Main.cs` (wiring for opening/closing the editor), `game/scripts/BuildController.cs` (aim at a golem and press E to open its editor)

**Interfaces:**
- Consumes: `NodeCatalog.All`, `CircuitGraphData`, `CircuitCompiler.Compile`, `InscribeCircuitCommand`, `CircuitRejected`/`CircuitInscribed` events
- Produces: `CircuitEditor : Control` — `void OpenFor(int golemId, CircuitGraphData current)`, `event Action<int, CircuitGraphData>? InscribeRequested`. UI contract: right-click on the canvas pops up the node panel (from NodeCatalog, filtering out test-only nodes); GraphNode port slot type = the int mapped from DataType (GraphEdit natively blocks connections between mismatched types); Exec and Data ports use different colors; the "Inscribe" button first runs a local `Compile` — on error, the corresponding GraphNode is highlighted red + a bottom error list (error code → Chinese message table); only when error-free does it fire `InscribeRequested`

- [ ] **Step 1: Scene skeleton**: CanvasLayer → Panel → VBox: GraphEdit (expand) + HBox (error Label + Inscribe Button). Esc/E closes and restores mouse capture.
- [ ] **Step 2: NodeCatalog → GraphNode generation**: one constructor-style method per NodeDef — title = DisplayName, `SetSlot` per port (inputs on the left, outputs on the right; Exec = white/type 0, Number = blue/1, Bool = orange/2, Vector = green/3); inline parameters such as `data_const_number` are embedded via SpinBox.
- [ ] **Step 3: Bidirectional serialization**: `ToGraphData()` (traverse GraphEdit child nodes and connections → CircuitGraphData, including EditorX/Y) and `LoadGraphData()` (rebuild in reverse). Write a manual Godot-side round-trip check: build a graph → serialize → clear → load → visually identical.
- [ ] **Step 4: Compile error display**: `Compile` fails → find the GraphNode by `CompileError.NodeId` and apply a red StyleBox overlay + show `Message` in the error list; success → fire `InscribeRequested`, which Main forwards as an `InscribeCircuitCommand`.
- [ ] **Step 5: Manual verification**: F5 → aim at a golem and press E → build "on start → move to → harvest → move to → unload" → deliberately disconnect a required input and see the red highlight → fix it and inscribe successfully (the golem starting to act becomes visible after Task 5 is done).
- [ ] **Step 6: Commit** `feat(game): grapheedit circuit editor with compile-error highlighting`

---

### Task 5: Golem View and Execution Highlighting

**Files:**
- Create: `game/scripts/GolemView.cs`
- Modify: `game/scripts/BuildController.cs` (placement modes and view generation for resource nodes/storage obelisks/golems — placement mode switches among base_block/resource node/storage obelisk/golem via the 1/2/3 keys), `game/scripts/CircuitEditor.cs` (highlighting)
- Modify: `SimCore/SimEvents.cs` + `SimCore/Machines/GolemState.cs` (add throttled event `GolemProgress(int GolemId, int CurrentPc, SimVec3 Position, SimVec3 PrevPosition)` — one per golem per tick)

**Interfaces:**
- Produces: `GolemView : Node3D` — listens for `GolemProgress`, and in `_Process` interpolates position from `prev→current` by the intra-frame alpha (the first real application of the spec §4 interpolation mechanism); hovering geometry with procedural animation (slow self-rotation + up-down bobbing). When the editor is open, `CurrentPc` is used to reverse-look-up the instruction→node mapping (the compiler adds an `int[] InstructionToNodeId` debug mapping table to `CompiledCircuit`; added in this task with an accompanying compiler test) to highlight the current GraphNode.

- [ ] **Step 1: Compiler debug mapping** (TDD: `InstructionToNodeId.Length == Instructions.Length`; in a branch program, the Wait instruction maps back to the NodeId of action_wait)
- [ ] **Step 2: GolemView interpolated movement** (gray-box: glowing octahedron = two offset BoxMeshes + emission)
- [ ] **Step 3: Editor highlighting** (while open, the current node gets a blue glowing StyleBox, refreshed with GolemProgress)
- [ ] **Step 4: Manual verification — M2 core acceptance**: F5 → place resource node/storage obelisk → place golem → E to open the editor → build the harvest loop → inscribe → the golem smoothly shuttles cargo back and forth, and the currently executing node glows and flows through the editor; tear down the storage obelisk and see that after harvest failures the circuit still cycles (no crash); build a pure-Jump infinite-loop graph and inscribe it → VM crash event (confirm via log output for now; visual malfunction belongs to M4)
- [ ] **Step 5: Regression `dotnet test` all green** → **Step 6: Commit** `feat(game): golem view with interpolation and live node highlighting`

---

### Task 6: VM State in Save Data

**Files:**
- Modify: `SimCore/Persistence/SaveData.cs` (add `List<GolemData>`: Id/position/cargo/`GraphJson` (the inscribed graph)/`ProgramCounter`/`WaitTicksRemaining`/`Status`/register array), `SimCore/Simulation.cs` (snapshot includes golems; `FromSnapshot` recompiles GraphJson to restore the program — spec §5: compiled artifacts do not go into save data)
- Test: append to `SimCore.Tests/SaveRoundTripTests.cs`

- [ ] **Step 1: Write failing tests**: `SuspendedGolem_SurvivesRoundTrip_AndResumesMidAction` (save mid-movement of a golem → restore → keep ticking → it eventually reaches the target); `RoundTrip_SnapshotJsonIdentical` (the snapshot json including golems round-trips identically); `AdversarialFloats_RoundTripBitExact` (set golem position/registers to adversarial float values such as 0.1f, 1.0/3.0, 1e-17 → the json round-trip is bit-exact. Spec red line: **Forbidden to "fix" this test via decimal truncation — truncation means every save mutates simulation state**)
- [ ] **Step 2: Implement** → **Step 3: All green** → **Step 4: Commit** `feat(sim): golem vm state survives save round-trip`

## End-of-month definition of done (= M2 acceptance)

- End-to-end: place resource nodes/storage obelisks/golems in the world, open the editor to connect nodes, errors highlighted in red, inscribe, golem executes the harvest loop, current node highlighted in real time
- An infinite-loop circuit triggers the VM Crashed event; inscribing/module changes force a Reset
- A suspended VM's state can round-trip through save data and be restored
- `dotnet test` all green (expected >= 35 tests)

## Explicitly out of scope (this month)

- Energy constraints (golems consume no energy yet; M3), multi-block assembly (M3), signal nodes (M5)
- Malfunction-aesthetic visuals (M4; this month only emits the event), editor interaction polish (M4)
