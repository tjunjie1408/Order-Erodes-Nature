# Month 2: M1 Wrap-up + First Half of M2 — Proper Character and Circuit Data Model/Compiler Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Pre-start calibration (required reading)**: This plan was written in advance. First step before starting work: check every Interface and file path in this plan against the actual code in the repository (especially the `Simulation`/`SimDriver`/`BuildController` signatures produced in Month 1); if anything has drifted, revise this plan first before starting work. The monthly goals and acceptance criteria must not be downgraded.

**Goal:** Replace the gray-box capsule with a proper animated character; build the circuit graph data model, node catalog, and compiler (validation + flat instruction array output) in SimCore, all test-driven and containing no UI whatsoever.

**Architecture:** The circuit system is split into three layers per the design doc: a pure-data graph description (`CircuitGraphData`, the sole interchange format between editor and simulation) → compiler (validates and produces the `CompiledCircuit` instruction array) → VM (Month 3). This month covers only the first two layers; the compiler can be tested entirely outside Godot. The character replacement is pure presentation-layer work and does not touch SimCore.

**Tech Stack:** Same as Month 1; new asset sources: KayKit/Mixamo (character and animations)

**Spec source:** `docs/superpowers/specs/2026-07-03-magic-automation-game-design.md` §5 (circuit execution model), §6 (character assets)

## Global Constraints

- SimCore forbids `using Godot`; subsystems interact only through events or pure data
- Commands are consumed only at tick boundaries; the presentation layer never writes simulation state; the tick hot path is zero-allocation
- Save state contains only trivial values; a running circuit's instruction stream is read-only
- Commit messages must not include Co-Authored-By; only accept CC0 / explicitly commercially usable assets and record them in CREDITS.md
- The compiler is not on the hot path (invoked only at inscribe time), so allocations and LINQ are allowed

## File Structure (new this month)

```
SimCore/Circuits/PortDef.cs            Port/type definitions
SimCore/Circuits/NodeDef.cs            Node definition
SimCore/Circuits/NodeCatalog.cs        MVP node catalog (data table)
SimCore/Circuits/CircuitGraphData.cs   Graph description (pure data, editor⇄simulation interchange format)
SimCore/Circuits/Instruction.cs        Instructions and OpCode
SimCore/Circuits/CompiledCircuit.cs    Compilation output
SimCore/Circuits/CompileError.cs       Errors (with node location)
SimCore/Circuits/CircuitCompiler.cs    Compiler
SimCore.Tests/Circuits/NodeCatalogTests.cs
SimCore.Tests/Circuits/CompilerValidationTests.cs
SimCore.Tests/Circuits/CompilerCodegenTests.cs
game/assets/characters/...             Character model and animations (Task 1)
game/scenes/Player.tscn                Character promoted to a scene file
game/scripts/PlayerAnimator.cs         Animation state driver
```

---

### Task 1: Replace the Gray-box Capsule with the Proper Character

**Files:**
- Create: `game/assets/characters/` (model + animation files), `game/scenes/Player.tscn`, `game/scripts/PlayerAnimator.cs`
- Modify: `game/scripts/Main.cs` (change to instantiating Player.tscn), `game/scripts/PlayerController.cs` (expose movement state), `CREDITS.md`

**Interfaces:**
- Consumes: `PlayerController` (Month 1)
- Produces: `PlayerController` gains read-only properties `public bool IsMoving => ...` and `public bool IsAirborne => !IsOnFloor();`; `PlayerAnimator : Node` reads these two properties to switch between idle/run/jump animations

- [ ] **Step 1: Acquire character assets**

First choice: KayKit Adventurers (itch.io, CC0, ships with idle/run/jump animations, GLB format). Fallback: any humanoid GLB + Mixamo animation retargeting (Mixamo requires an Adobe account; export FBX → convert to GLB in Blender). Place the GLB in `game/assets/characters/` and add one line to `CREDITS.md` (asset name / source / license / usage).

- [ ] **Step 2: Assemble Player.tscn**

In the Godot editor, solidify the player structure that Month 1 built in code into a scene: root `CharacterBody3D` (with PlayerController.cs attached) → `CollisionShape3D` (capsule) + character model instance (replacing the MeshInstance3D capsule) + `Yaw/SpringArm3D/Camera3D` + `AnimationPlayer` (bundled with the GLB) + `PlayerAnimator` node. `Main.cs` deletes `BuildPlayer()` and instead does `AddChild(GD.Load<PackedScene>("res://scenes/Player.tscn").Instantiate());` (keep the node name `Player` unchanged so BuildController's path dependency does not break).

- [ ] **Step 3: Write PlayerAnimator**

```csharp
using Godot;

public partial class PlayerAnimator : Node
{
    private PlayerController _player = null!;
    private AnimationPlayer _anim = null!;
    private string _current = "";

    public override void _Ready()
    {
        _player = GetParent<PlayerController>();
        _anim = _player.GetNode<AnimationPlayer>("%AnimationPlayer");
    }

    public override void _Process(double delta)
    {
        var next = _player.IsAirborne ? "jump"
                 : _player.IsMoving ? "run"
                 : "idle";
        if (next == _current) return;
        _current = next;
        _anim.Play(next, customBlend: 0.15);
    }
}
```

Animation names must match the actual exported names in the asset (calibration point: open the AnimationPlayer, inspect the animation list, and update the strings in the code). Add to `PlayerController`:

```csharp
    public bool IsMoving { get; private set; }
    public bool IsAirborne => !IsOnFloor();
    // At the end of _PhysicsProcess: IsMoving = dir != Vector3.Zero;
```

Model facing: in `_PhysicsProcess`, when `dir != Vector3.Zero`, apply a smoothed `LookAt` turn to the model node (rotate only the model child node, not the whole Body, to avoid affecting the camera Yaw).

- [ ] **Step 4: Manual verification**

Run: F5
Expected: character idle/run/jump animations switch correctly with 0.15s blending; the model turns to face the movement direction while moving; camera and build interactions all work exactly as before.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat(game): animated character replaces gray-box capsule"
```

---

### Task 2: Circuit Type System and Node Catalog

**Files:**
- Create: `SimCore/Circuits/PortDef.cs`, `SimCore/Circuits/NodeDef.cs`, `SimCore/Circuits/NodeCatalog.cs`
- Test: `SimCore.Tests/Circuits/NodeCatalogTests.cs`

**Interfaces:**
- Produces (every subsequent circuit task and the Month 3 editor depend on these; signatures are locked down here):

```csharp
namespace SimCore.Circuits;

public enum PortKind { Exec, Data }
public enum DataType { None, Number, Bool, Vector }   // Vector = 3D coordinate; Exec ports use None

public sealed record PortDef(string Name, PortKind Kind, DataType Type, bool Required);

public sealed record NodeDef(
    string TypeId,                       // stable identifier, e.g. "event_start"
    string DisplayName,                  // editor display name (player-facing localized string; Chinese)
    IReadOnlyList<PortDef> Inputs,
    IReadOnlyList<PortDef> Outputs,
    bool IsEvent);                       // event node = execution entry point, no Exec input

public static class NodeCatalog
{
    public static IReadOnlyDictionary<string, NodeDef> All { get; }
}
```

- [ ] **Step 1: Write failing tests**

```csharp
using SimCore.Circuits;
using Xunit;

namespace SimCore.Tests.Circuits;

public class NodeCatalogTests
{
    [Theory]
    [InlineData("event_start")]      // On Start
    [InlineData("action_move_to")]   // Move To
    [InlineData("action_harvest")]   // Harvest
    [InlineData("action_load")]      // Load
    [InlineData("action_unload")]    // Unload
    [InlineData("flow_branch")]      // Conditional branch
    [InlineData("action_wait")]      // Wait
    [InlineData("data_const_number")]// Constant
    [InlineData("data_compare")]     // Compare
    [InlineData("sensor_cargo")]     // Read own cargo count
    public void Catalog_ContainsMvpNodes(string typeId)
        => Assert.True(NodeCatalog.All.ContainsKey(typeId));

    [Fact]
    public void EventNodes_HaveNoExecInput()
    {
        foreach (var def in NodeCatalog.All.Values)
            if (def.IsEvent)
                Assert.DoesNotContain(def.Inputs, p => p.Kind == PortKind.Exec);
    }

    [Fact]
    public void Branch_HasTrueAndFalseExecOutputs()
    {
        var branch = NodeCatalog.All["flow_branch"];
        Assert.Contains(branch.Outputs, p => p is { Name: "true", Kind: PortKind.Exec });
        Assert.Contains(branch.Outputs, p => p is { Name: "false", Kind: PortKind.Exec });
        Assert.Contains(branch.Inputs, p => p is { Name: "condition", Type: DataType.Bool, Required: true });
    }
}
```

- [ ] **Step 2: Run tests, confirm they fail** (compile errors)

- [ ] **Step 3: Implement the node catalog**

Complete port definitions for this month's 10 nodes (Month 3 expands to 15 as needed):

| TypeId | Exec in | Exec out | Data in | Data out |
|---|---|---|---|---|
| event_start | none | out | none | none |
| action_move_to | in | out | target:Vector (required) | none |
| action_harvest | in | out | target:Vector (required) | none |
| action_load | in | out | none | none |
| action_unload | in | out | none | none |
| action_wait | in | out | ticks:Number (required) | none |
| flow_branch | in | true,false | condition:Bool (required) | none |
| data_const_number | none | none | none (inline param value) | value:Number |
| data_compare | none | none | a:Number (required), b:Number (required) | result:Bool |
| sensor_cargo | none | none | none | count:Number |

Implement as a dictionary initialization in the `NodeCatalog` static constructor, filling in each `NodeDef` per the table. The execution output of `event_start` is uniformly named `"out"`; regular action nodes' Exec input is named `"in"`.

- [ ] **Step 4: Run tests, confirm they pass** → **Step 5: Commit** `feat(circuits): port type system and mvp node catalog`

---

### Task 3: Graph Description Data Structure (editor⇄simulation interchange format)

**Files:**
- Create: `SimCore/Circuits/CircuitGraphData.cs`

**Interfaces:**
- Produces (the target format for Month 3 GraphEdit serialization; all trivial values, JSON-serializable):

```csharp
namespace SimCore.Circuits;

public sealed class CircuitGraphData
{
    public List<CircuitNodeData> Nodes { get; set; } = new();
    public List<CircuitConnectionData> Connections { get; set; } = new();
}

public sealed class CircuitNodeData
{
    public int NodeId { get; set; }
    public string TypeId { get; set; } = "";
    public Dictionary<string, double> InlineParams { get; set; } = new(); // e.g. a constant node's value
    public float EditorX { get; set; }   // editor layout only, ignored by the compiler
    public float EditorY { get; set; }
}

public sealed class CircuitConnectionData
{
    public int FromNode { get; set; }
    public string FromPort { get; set; } = "";
    public int ToNode { get; set; }
    public string ToPort { get; set; } = "";
}
```

- [ ] **Step 1: Write the types + a small JSON round-trip test** (place at the top of `CompilerValidationTests.cs` or in a standalone file: construct a two-node graph, `JsonSerializer` serialize → deserialize → node count and connections are equal)
- [ ] **Step 2: Run tests, they pass** → **Step 3: Commit** `feat(circuits): graph data interchange format`

---

### Task 4: Compiler — Validation Layer

**Files:**
- Create: `SimCore/Circuits/CompileError.cs`, `SimCore/Circuits/CircuitCompiler.cs` (this task covers validation only; output is left for Task 5)
- Test: `SimCore.Tests/Circuits/CompilerValidationTests.cs`

**Interfaces:**
- Produces:

```csharp
public sealed record CompileError(int NodeId, string Code, string Message);

public sealed class CompileResult
{
    public CompiledCircuit? Circuit { get; init; }          // null when validation fails
    public List<CompileError> Errors { get; init; } = new();
    public bool Success => Errors.Count == 0;
}

public static class CircuitCompiler
{
    public static CompileResult Compile(CircuitGraphData graph);
}
```

- The full set of error codes (the editor highlights errors in red based on these; consumed in Month 3): `unknown_node_type`, `no_event_node`, `type_mismatch`, `required_input_missing`, `exec_input_from_data` (Exec/Data cross-wiring), `multi_exec_out` (multiple lines connected to the same Exec output), `multi_data_in` (multiple sources into the same Data input), `data_cycle` (data lines form a cycle), `unreachable_node`

- [ ] **Step 1: Write failing tests** (at least one test case per error code; the key cases are below, write the rest following the same shape)

```csharp
using SimCore.Circuits;
using Xunit;

namespace SimCore.Tests.Circuits;

public class CompilerValidationTests
{
    private static CircuitGraphData Graph(params CircuitNodeData[] nodes) =>
        new() { Nodes = nodes.ToList() };

    private static CircuitNodeData Node(int id, string type) =>
        new() { NodeId = id, TypeId = type };

    private static void Connect(CircuitGraphData g, int from, string fp, int to, string tp) =>
        g.Connections.Add(new CircuitConnectionData
            { FromNode = from, FromPort = fp, ToNode = to, ToPort = tp });

    [Fact]
    public void EmptyGraph_FailsWithNoEventNode()
    {
        var result = CircuitCompiler.Compile(new CircuitGraphData());
        Assert.Contains(result.Errors, e => e.Code == "no_event_node");
    }

    [Fact]
    public void TypeMismatch_NumberIntoBool_IsRejectedWithNodeId()
    {
        var g = Graph(Node(1, "event_start"), Node(2, "flow_branch"),
                      Node(3, "data_const_number"));
        Connect(g, 1, "out", 2, "in");
        Connect(g, 3, "value", 2, "condition");   // Number → Bool input
        var result = CircuitCompiler.Compile(g);
        var err = Assert.Single(result.Errors, e => e.Code == "type_mismatch");
        Assert.Equal(2, err.NodeId);
    }

    [Fact]
    public void RequiredInput_Unconnected_IsRejected()
    {
        var g = Graph(Node(1, "event_start"), Node(2, "action_move_to"));
        Connect(g, 1, "out", 2, "in");            // target:Vector not connected
        var result = CircuitCompiler.Compile(g);
        Assert.Contains(result.Errors, e => e is { Code: "required_input_missing", NodeId: 2 });
    }

    [Fact]
    public void DataCycle_IsRejected()
    {
        // Two compare nodes feeding each other's results (a Bool output into a Number input
        // would itself be a type_mismatch, so this case would need two hypothetical Numbers
        // wired directly into a cycle: compare.a <- compare2.?? is not feasible —
        // data_compare has no Number output; a data cycle within the MVP node set would
        // require the arithmetic nodes coming later. This month, build the cycle using a
        // test-only node outside the catalog, "test_passthrough" (Number in value /
        // Number out value). Add this node to NodeCatalog and mark it internal test only.
        var g = Graph(Node(1, "event_start"), Node(2, "test_passthrough"),
                      Node(3, "test_passthrough"));
        Connect(g, 2, "value", 3, "value");
        Connect(g, 3, "value", 2, "value");
        var result = CircuitCompiler.Compile(g);
        Assert.Contains(result.Errors, e => e.Code == "data_cycle");
    }

    [Fact]
    public void MinimalValidProgram_Compiles()
    {
        // On Start → wait (constant 10 ticks)
        var wait = Node(2, "action_wait");
        var g = Graph(Node(1, "event_start"), wait, Node(3, "data_const_number"));
        g.Nodes[2].InlineParams["value"] = 10;
        Connect(g, 1, "out", 2, "in");
        Connect(g, 3, "value", 2, "ticks");
        var result = CircuitCompiler.Compile(g);
        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.NotNull(result.Circuit);
    }
}
```

For the remaining error codes (`unknown_node_type`, `exec_input_from_data`, `multi_exec_out`, `multi_data_in`, `unreachable_node`), write one test case each with the same construction shape.

- [ ] **Step 2: Run tests, confirm they fail** (compile errors)

- [ ] **Step 3: Implement validation**

`CircuitCompiler.Compile` validation order (collect all errors then return; do not stop at the first error):
1. Every node's TypeId exists in `NodeCatalog.All` → otherwise `unknown_node_type`
2. At least one `IsEvent` node → otherwise `no_event_node`
3. Every connection: both endpoint ports exist; Kind is the same (`exec_input_from_data`); both ends of a Data line have equal `DataType` (`type_mismatch`, error located at the To-end node)
4. Every Exec output has ≤1 outgoing edge (`multi_exec_out`); every Data input has ≤1 incoming edge (`multi_data_in`)
5. Every Required Data input must have an incoming edge, or the node's `InlineParams` must contain a parameter of the same name (`required_input_missing`)
6. Run Kahn topological sort on the data-line subgraph; if it cannot complete → each remaining node reports `data_cycle`
7. BFS from every event node along Exec output edges; any node not covered and that is not a pure data supplier (i.e. its data outputs are not consumed by any reachable node) → `unreachable_node`

Add the `test_passthrough` node to the catalog (its DisplayName annotated as "test only"; the Month 3 editor's node palette filters it out). When validation fully passes, return `Circuit = new CompiledCircuit()` as an empty shell for now (Task 5 fills in the real output), so this task's tests can pass independently.

- [ ] **Step 4: Run tests, confirm they pass** → **Step 5: Commit** `feat(circuits): compiler validation with located error codes`

---

### Task 5: Compiler — Instruction Output

**Files:**
- Create: `SimCore/Circuits/Instruction.cs`, `SimCore/Circuits/CompiledCircuit.cs`
- Modify: `SimCore/Circuits/CircuitCompiler.cs`
- Test: `SimCore.Tests/Circuits/CompilerCodegenTests.cs`

**Interfaces:**
- Produces (the execution format for the Month 3 VM; locked down):

```csharp
public enum OpCode : byte
{
    Halt = 0,
    Jump,          // A = target instruction index
    JumpIfFalse,   // A = register index (condition), B = target index
    LoadConst,     // A = target register, Imm = value
    Compare,       // A = left register, B = right register, C = target register (Bool), Imm = compare mode (0:> 1:= 2:<)
    ReadSensor,    // A = sensor id (0=cargo), C = target register
    MoveTo,        // A = x register, B = y register (0 for now), C = z register — suspendable
    Harvest,       // takes its target the same way as above — suspendable
    Load,          // suspendable
    Unload,        // suspendable
    Wait,          // A = tick-count register — suspendable
}

public readonly record struct Instruction(OpCode Op, int A, int B, int C, double Imm);

public sealed class CompiledCircuit
{
    public Instruction[] Instructions { get; init; } = Array.Empty<Instruction>();
    public int RegisterCount { get; init; }
    public int StartEntry { get; init; } = -1;   // entry index of event_start, -1 if absent
}
```

- Code generation rules: data nodes are evaluated into registers in topological order (each data output port gets one register); the Exec chain is linearized depth-first; `flow_branch` generates `JumpIfFalse` + two branch arms + a convergence `Jump`; at the end of the `event_start` program body, generate a `Jump` back to the entry (**the program loops by nature**, spec §5); Vector parameters are passed via three Number registers (MVP simplification: `action_move_to`'s target is supplied by three `data_const_number` nodes or by inline parameters — this month the inline-parameter form is `InlineParams["target_x"]` etc.; the Month 3 editor provides coordinate picking)

- [ ] **Step 1: Write failing tests**

```csharp
public class CompilerCodegenTests
{
    // Reuse ValidationTests' Graph/Node/Connect helpers (extract into a shared static class GraphBuilder)

    [Fact]
    public void StartWait_LoopsBackToEntry()
    {
        var circuit = CompileMinimalStartWait();   // the previous task's minimal valid program
        Assert.NotEqual(-1, circuit.StartEntry);
        Assert.Equal(OpCode.Jump, circuit.Instructions[^1].Op);
        Assert.Equal(circuit.StartEntry, circuit.Instructions[^1].A); // tail jumps back to the entry
        Assert.Contains(circuit.Instructions, i => i.Op == OpCode.Wait);
    }

    [Fact]
    public void Branch_GeneratesJumpIfFalse_WithBothArmsReachable()
    {
        // On Start → branch(condition = compare(cargo, const 5))
        //   true → wait(1)  false → wait(2) → converge → loop
        var circuit = CompileBranchProgram();
        Assert.Contains(circuit.Instructions, i => i.Op == OpCode.JumpIfFalse);
        Assert.Contains(circuit.Instructions, i => i.Op == OpCode.Compare);
        Assert.Contains(circuit.Instructions, i => i.Op == OpCode.ReadSensor);
    }

    [Fact]
    public void SameGraph_CompilesToIdenticalInstructions()   // compilation determinism
    {
        var a = CompileBranchProgram();
        var b = CompileBranchProgram();
        Assert.Equal(a.Instructions, b.Instructions);
    }

    [Fact]
    public void DataDependency_EvaluatedBeforeConsumer()
    {
        var circuit = CompileBranchProgram();
        int compareIdx = Array.FindIndex(circuit.Instructions, i => i.Op == OpCode.Compare);
        int branchIdx = Array.FindIndex(circuit.Instructions, i => i.Op == OpCode.JumpIfFalse);
        Assert.True(compareIdx < branchIdx);
    }
}
```

`CompileMinimalStartWait`/`CompileBranchProgram` are static helpers inside the tests; they build the graph with GraphBuilder and return `CircuitCompiler.Compile(...).Circuit!`.

- [ ] **Step 2: Run tests, confirm they fail** → **Step 3: Implement code generation** (per the rules above; register allocation = incrementing sequence numbers over data output ports; the branch evaluation strategy is MVP-simplified to "before entering an Exec node, re-evaluate its data dependency closure" — i.e. every time execution reaches the branch, recompute compare/sensor, guaranteeing the freshest sensor values are read)

- [ ] **Step 4: Run tests, confirm they pass** (`dotnet test` all green) → **Step 5: Commit** `feat(circuits): compile graphs to flat jump-threaded instruction arrays`

---

### Task 6: End-of-month Cleanup — Circuit Subsystem Boundary Self-check

**Files:**
- Modify: `AGENTS.md` (add `SimCore/Circuits/` to the structure map)

- [ ] **Step 1: Boundary check**: confirm `SimCore/Circuits/` references neither `Simulation` nor `Structure` (the compiler is an independent subsystem; its integration with the simulation happens in Month 3, and only through the pure-data `CompiledCircuit`). `grep -r "using Godot" SimCore/` returns nothing.
- [ ] **Step 2: Full regression**: `dotnet test` all green + F5 playtest shows no regression in character or building.
- [ ] **Step 3: Commit** `docs: update AGENTS.md structure map for circuits subsystem`

## End-of-month definition of done

- The proper animated character walks and builds in the scene
- `CircuitGraphData` → `CircuitCompiler.Compile` → the full set of validation errors is testable and carries node locations; valid graphs produce a deterministic instruction array (including branch jumps and the loop-back jump)
- The circuit subsystem has zero Godot dependencies and zero Simulation dependencies

## Explicitly out of scope (this month)

- VM execution (Month 3)
- GraphEdit editor UI (Month 3)
- Golem entities and actions (Month 3)
