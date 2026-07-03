# 第 2 个月：M1 收尾 + M2 前半——正式角色与回路数据模型/编译器 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **开工前校准（必读）**：本计划提前撰写。开工第一步：对照仓库实际代码核对本计划全部 Interfaces 与文件路径（尤其第 1 个月产出的 `Simulation`/`SimDriver`/`BuildController` 签名），有漂移先修订本计划再动工。月度目标与验收标准不可降级。

**Goal:** 用带动画的正式角色替换灰盒胶囊；在 SimCore 中建立回路图数据模型、节点目录与编译器（校验 + 扁平指令数组产出），全部测试驱动、不含任何 UI。

**Architecture:** 回路系统按设计文档的三层切分：纯数据的图描述（`CircuitGraphData`，编辑器与仿真的唯一交换格式）→ 编译器（校验并产出 `CompiledCircuit` 指令数组）→ VM（第 3 个月）。本月只做前两层，编译器可完全脱离 Godot 测试。角色替换是纯表现层工作，不触碰 SimCore。

**Tech Stack:** 同第 1 个月；新增素材来源 KayKit/Mixamo（角色与动画）

**规格来源:** `docs/superpowers/specs/2026-07-03-magic-automation-game-design.md` §5（回路执行模型）、§6（角色素材）

## Global Constraints

- SimCore 禁止 `using Godot`；子系统间只通过事件或纯数据交互
- 指令只在 tick 边界消费；表现层不写仿真状态；Tick 热路径零分配
- 存档状态只含平凡值；运行中的回路指令流只读
- commit 信息不写 Co-Authored-By；素材只收 CC0/明确可商用并记入 CREDITS.md
- 编译器不在热路径（仅刻录时调用），允许分配与 LINQ

## 文件结构（本月新建）

```
SimCore/Circuits/PortDef.cs            端口/类型定义
SimCore/Circuits/NodeDef.cs            节点定义
SimCore/Circuits/NodeCatalog.cs        MVP 节点目录（数据表）
SimCore/Circuits/CircuitGraphData.cs   图描述（纯数据，编辑器⇄仿真交换格式）
SimCore/Circuits/Instruction.cs        指令与 OpCode
SimCore/Circuits/CompiledCircuit.cs    编译产物
SimCore/Circuits/CompileError.cs       错误（带节点定位）
SimCore/Circuits/CircuitCompiler.cs    编译器
SimCore.Tests/Circuits/NodeCatalogTests.cs
SimCore.Tests/Circuits/CompilerValidationTests.cs
SimCore.Tests/Circuits/CompilerCodegenTests.cs
game/assets/characters/...             角色模型与动画（Task 1）
game/scenes/Player.tscn                角色升级为场景文件
game/scripts/PlayerAnimator.cs         动画状态驱动
```

---

### Task 1: 正式角色替换灰盒胶囊

**Files:**
- Create: `game/assets/characters/`（模型+动画文件）、`game/scenes/Player.tscn`、`game/scripts/PlayerAnimator.cs`
- Modify: `game/scripts/Main.cs`（改为实例化 Player.tscn）、`game/scripts/PlayerController.cs`（暴露移动状态）、`CREDITS.md`

**Interfaces:**
- Consumes: `PlayerController`（第 1 个月）
- Produces: `PlayerController` 新增只读属性 `public bool IsMoving => ...`、`public bool IsAirborne => !IsOnFloor();`；`PlayerAnimator : Node` 读这两个属性切换 idle/run/jump 动画

- [ ] **Step 1: 获取角色素材**

首选 KayKit Adventurers（itch.io，CC0，自带 idle/run/jump 动画，GLB 格式）。备选：任意人形 GLB + Mixamo 动画重定向（Mixamo 需 Adobe 账号，导出 FBX→Blender 转 GLB）。将 GLB 放入 `game/assets/characters/`，在 `CREDITS.md` 记一行（素材名/来源/许可/用途）。

- [ ] **Step 2: 组装 Player.tscn**

在 Godot 编辑器中把第 1 个月代码构造的玩家结构固化为场景：根 `CharacterBody3D`（挂 PlayerController.cs）→ `CollisionShape3D`(胶囊) + 角色模型实例（替换 MeshInstance3D 胶囊）+ `Yaw/SpringArm3D/Camera3D` + `AnimationPlayer`（GLB 自带）+ `PlayerAnimator` 节点。`Main.cs` 删除 `BuildPlayer()`，改为 `AddChild(GD.Load<PackedScene>("res://scenes/Player.tscn").Instantiate());`（保持节点名 `Player` 不变，BuildController 的路径依赖不破）。

- [ ] **Step 3: 写 PlayerAnimator**

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

动画名以素材实际导出名为准（校准点：打开 AnimationPlayer 查看动画列表并改代码中的字符串）。`PlayerController` 增加：

```csharp
    public bool IsMoving { get; private set; }
    public bool IsAirborne => !IsOnFloor();
    // _PhysicsProcess 末尾：IsMoving = dir != Vector3.Zero;
```

模型朝向：`_PhysicsProcess` 中当 `dir != Vector3.Zero` 时对模型节点做 `LookAt` 平滑转向（只转模型子节点，不转整个 Body，避免影响相机 Yaw）。

- [ ] **Step 4: 手动验证**

Run: F5
Expected: 角色待机/跑动/跳跃动画正确切换且有 0.15s 混合；移动时模型朝移动方向转身；相机、建造交互一切照旧。

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat(game): animated character replaces gray-box capsule"
```

---

### Task 2: 回路类型系统与节点目录

**Files:**
- Create: `SimCore/Circuits/PortDef.cs`, `SimCore/Circuits/NodeDef.cs`, `SimCore/Circuits/NodeCatalog.cs`
- Test: `SimCore.Tests/Circuits/NodeCatalogTests.cs`

**Interfaces:**
- Produces（后续所有回路任务与第 3 个月的编辑器都依赖，签名在此锁死）:

```csharp
namespace SimCore.Circuits;

public enum PortKind { Exec, Data }
public enum DataType { None, Number, Bool, Vector }   // Vector=三维坐标；Exec 端口用 None

public sealed record PortDef(string Name, PortKind Kind, DataType Type, bool Required);

public sealed record NodeDef(
    string TypeId,                       // 稳定标识，如 "event_start"
    string DisplayName,                  // 编辑器显示名（中文）
    IReadOnlyList<PortDef> Inputs,
    IReadOnlyList<PortDef> Outputs,
    bool IsEvent);                       // 事件节点=执行入口，无 Exec 输入

public static class NodeCatalog
{
    public static IReadOnlyDictionary<string, NodeDef> All { get; }
}
```

- [ ] **Step 1: 写失败测试**

```csharp
using SimCore.Circuits;
using Xunit;

namespace SimCore.Tests.Circuits;

public class NodeCatalogTests
{
    [Theory]
    [InlineData("event_start")]      // 启动时
    [InlineData("action_move_to")]   // 移动到
    [InlineData("action_harvest")]   // 采集
    [InlineData("action_load")]      // 装载
    [InlineData("action_unload")]    // 卸载
    [InlineData("flow_branch")]      // 条件分支
    [InlineData("action_wait")]      // 等待
    [InlineData("data_const_number")]// 常量
    [InlineData("data_compare")]     // 比较
    [InlineData("sensor_cargo")]     // 读自身载货量
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

- [ ] **Step 2: 跑测试确认失败**（编译错误）

- [ ] **Step 3: 实现节点目录**

本月 10 个节点的完整端口定义（第 3 个月按需补至 15）：

| TypeId | Exec入 | Exec出 | Data入 | Data出 |
|---|---|---|---|---|
| event_start | 无 | out | 无 | 无 |
| action_move_to | in | out | target:Vector(必填) | 无 |
| action_harvest | in | out | target:Vector(必填) | 无 |
| action_load | in | out | 无 | 无 |
| action_unload | in | out | 无 | 无 |
| action_wait | in | out | ticks:Number(必填) | 无 |
| flow_branch | in | true,false | condition:Bool(必填) | 无 |
| data_const_number | 无 | 无 | 无(内联参数value) | value:Number |
| data_compare | 无 | 无 | a:Number(必), b:Number(必) | result:Bool |
| sensor_cargo | 无 | 无 | 无 | count:Number |

实现为 `NodeCatalog` 静态构造中的字典初始化，每个 `NodeDef` 按表填写。`event_start` 的执行出口统一命名 `"out"`，普通动作节点 Exec 入口名 `"in"`。

- [ ] **Step 4: 跑测试确认通过** → **Step 5: Commit** `feat(circuits): port type system and mvp node catalog`

---

### Task 3: 图描述数据结构（编辑器⇄仿真交换格式）

**Files:**
- Create: `SimCore/Circuits/CircuitGraphData.cs`

**Interfaces:**
- Produces（第 3 个月 GraphEdit 序列化的目标格式，全平凡值可 JSON 化）:

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
    public Dictionary<string, double> InlineParams { get; set; } = new(); // 如常量节点的 value
    public float EditorX { get; set; }   // 仅编辑器布局，编译器忽略
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

- [ ] **Step 1: 写类型 + JSON 往返小测试**（放入 `CompilerValidationTests.cs` 顶部或独立文件：构造一个两节点图，`JsonSerializer` 序列化→反序列化→节点数与连线相等）
- [ ] **Step 2: 跑测试通过** → **Step 3: Commit** `feat(circuits): graph data interchange format`

---

### Task 4: 编译器——校验层

**Files:**
- Create: `SimCore/Circuits/CompileError.cs`, `SimCore/Circuits/CircuitCompiler.cs`（本任务只做校验，产出留 Task 5）
- Test: `SimCore.Tests/Circuits/CompilerValidationTests.cs`

**Interfaces:**
- Produces:

```csharp
public sealed record CompileError(int NodeId, string Code, string Message);

public sealed class CompileResult
{
    public CompiledCircuit? Circuit { get; init; }          // 校验失败为 null
    public List<CompileError> Errors { get; init; } = new();
    public bool Success => Errors.Count == 0;
}

public static class CircuitCompiler
{
    public static CompileResult Compile(CircuitGraphData graph);
}
```

- 错误码全集（编辑器据此标红，第 3 个月消费）：`unknown_node_type`、`no_event_node`、`type_mismatch`、`required_input_missing`、`exec_input_from_data`（Exec/Data 混接）、`multi_exec_out`（同一 Exec 出口连了多条）、`multi_data_in`（同一 Data 入口有多个来源）、`data_cycle`（数据线成环）、`unreachable_node`

- [ ] **Step 1: 写失败测试**（每个错误码至少一个用例；关键用例如下，其余按同构写全）

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
        Connect(g, 3, "value", 2, "condition");   // Number → Bool 入口
        var result = CircuitCompiler.Compile(g);
        var err = Assert.Single(result.Errors, e => e.Code == "type_mismatch");
        Assert.Equal(2, err.NodeId);
    }

    [Fact]
    public void RequiredInput_Unconnected_IsRejected()
    {
        var g = Graph(Node(1, "event_start"), Node(2, "action_move_to"));
        Connect(g, 1, "out", 2, "in");            // target:Vector 未接
        var result = CircuitCompiler.Compile(g);
        Assert.Contains(result.Errors, e => e is { Code: "required_input_missing", NodeId: 2 });
    }

    [Fact]
    public void DataCycle_IsRejected()
    {
        // 两个 compare 节点互相喂结果（Bool 出口接 Number 入口本身也是 type_mismatch，
        // 因此本用例用两个假想 Number 直连成环：compare.a <- compare2.?? 不可行——
        // 用 data_compare 无 Number 输出，MVP 节点里数据环需借助后续算术节点；
        // 本月先用目录外的测试专用节点 "test_passthrough"（Number入value/Number出value）构环。
        // NodeCatalog 中加入该节点并标注 internal test only。
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
        // 启动时 → 等待(常量10 tick)
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

其余错误码（`unknown_node_type`、`exec_input_from_data`、`multi_exec_out`、`multi_data_in`、`unreachable_node`）各写一个同构造型的用例。

- [ ] **Step 2: 跑测试确认失败**（编译错误）

- [ ] **Step 3: 实现校验**

`CircuitCompiler.Compile` 校验顺序（收集所有错误再返回，不首错即停）：
1. 每个节点的 TypeId 在 `NodeCatalog.All` 中 → 否则 `unknown_node_type`
2. 至少一个 `IsEvent` 节点 → 否则 `no_event_node`
3. 每条连线：两端端口存在；Kind 相同（`exec_input_from_data`）；Data 线两端 `DataType` 相等（`type_mismatch`，错误定位到 To 端节点）
4. 每个 Exec 出口出边 ≤1（`multi_exec_out`）；每个 Data 入口入边 ≤1（`multi_data_in`）
5. Required 的 Data 入口必须有入边，或节点 `InlineParams` 里有同名参数（`required_input_missing`）
6. 数据线子图做 Kahn 拓扑排序，排不完 → 剩余节点各报 `data_cycle`
7. 从每个事件节点沿 Exec 出边 BFS，未覆盖且非纯数据供给者（其数据输出未被任何可达节点消费）的节点 → `unreachable_node`

`test_passthrough` 节点加入目录（DisplayName 标注"测试专用"，第 3 个月编辑器的节点面板过滤掉它）。校验全过时先返回 `Circuit = new CompiledCircuit()` 空壳（Task 5 填真产物），让本任务测试可独立通过。

- [ ] **Step 4: 跑测试确认通过** → **Step 5: Commit** `feat(circuits): compiler validation with located error codes`

---

### Task 5: 编译器——指令产出

**Files:**
- Create: `SimCore/Circuits/Instruction.cs`, `SimCore/Circuits/CompiledCircuit.cs`
- Modify: `SimCore/Circuits/CircuitCompiler.cs`
- Test: `SimCore.Tests/Circuits/CompilerCodegenTests.cs`

**Interfaces:**
- Produces（第 3 个月 VM 的执行格式，锁死）:

```csharp
public enum OpCode : byte
{
    Halt = 0,
    Jump,          // A=目标指令下标
    JumpIfFalse,   // A=寄存器下标(条件), B=目标下标
    LoadConst,     // A=目标寄存器, Imm=值
    Compare,       // A=左寄存器, B=右寄存器, C=目标寄存器(Bool), Imm=比较模式(0:> 1:= 2:<)
    ReadSensor,    // A=传感器id(0=cargo), C=目标寄存器
    MoveTo,        // A=x寄存器, B=y寄存器(暂0), C=z寄存器 —— 可挂起
    Harvest,       // 同上取目标 —— 可挂起
    Load,          // 可挂起
    Unload,        // 可挂起
    Wait,          // A=tick数寄存器 —— 可挂起
}

public readonly record struct Instruction(OpCode Op, int A, int B, int C, double Imm);

public sealed class CompiledCircuit
{
    public Instruction[] Instructions { get; init; } = Array.Empty<Instruction>();
    public int RegisterCount { get; init; }
    public int StartEntry { get; init; } = -1;   // event_start 的入口下标，无则 -1
}
```

- 代码生成规则：数据节点按拓扑序求值进寄存器（每个数据输出端口分配一个寄存器）；Exec 链按深度优先线性化；`flow_branch` 生成 `JumpIfFalse`+两分支+汇合 `Jump`；`event_start` 程序体末尾生成 `Jump` 回入口（**程序天然循环**，规格 §5）；Vector 参数经三个 Number 寄存器传递（MVP 简化：`action_move_to` 的 target 由 `data_const_number` x3 或内联参数提供——本月内联参数形态为 `InlineParams["target_x"]` 等，第 3 个月编辑器提供坐标拾取）

- [ ] **Step 1: 写失败测试**

```csharp
public class CompilerCodegenTests
{
    // 复用 ValidationTests 的 Graph/Node/Connect 辅助（抽到共享静态类 GraphBuilder）

    [Fact]
    public void StartWait_LoopsBackToEntry()
    {
        var circuit = CompileMinimalStartWait();   // 上一任务的最小合法程序
        Assert.NotEqual(-1, circuit.StartEntry);
        Assert.Equal(OpCode.Jump, circuit.Instructions[^1].Op);
        Assert.Equal(circuit.StartEntry, circuit.Instructions[^1].A); // 尾部跳回入口
        Assert.Contains(circuit.Instructions, i => i.Op == OpCode.Wait);
    }

    [Fact]
    public void Branch_GeneratesJumpIfFalse_WithBothArmsReachable()
    {
        // 启动时 → branch(condition = compare(cargo, const 5))
        //   true → wait(1)  false → wait(2) → 汇合 → 循环
        var circuit = CompileBranchProgram();
        Assert.Contains(circuit.Instructions, i => i.Op == OpCode.JumpIfFalse);
        Assert.Contains(circuit.Instructions, i => i.Op == OpCode.Compare);
        Assert.Contains(circuit.Instructions, i => i.Op == OpCode.ReadSensor);
    }

    [Fact]
    public void SameGraph_CompilesToIdenticalInstructions()   // 编译确定性
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

`CompileMinimalStartWait`/`CompileBranchProgram` 为测试内静态辅助，用 GraphBuilder 构图后 `CircuitCompiler.Compile(...).Circuit!` 返回。

- [ ] **Step 2: 跑测试确认失败** → **Step 3: 实现代码生成**（按上述规则；寄存器分配 = 数据输出端口序号递增；分支求值策略 MVP 简化为"进入 Exec 节点前，重新求值其数据依赖闭包"——即每次执行到 branch 前重算 compare/sensor，保证读到最新传感值）

- [ ] **Step 4: 跑测试确认通过**（`dotnet test` 全绿）→ **Step 5: Commit** `feat(circuits): compile graphs to flat jump-threaded instruction arrays`

---

### Task 6: 月末整理——回路子系统边界自查

**Files:**
- Modify: `AGENTS.md`（结构地图加 `SimCore/Circuits/`）

- [ ] **Step 1: 边界检查**：确认 `SimCore/Circuits/` 不引用 `Simulation`/`Structure`（编译器是独立子系统，与仿真的对接发生在第 3 个月、且只通过 `CompiledCircuit` 纯数据）。`grep -r "using Godot" SimCore/` 为空。
- [ ] **Step 2: 全量回归**：`dotnet test` 全绿 + F5 试玩角色与建造无回归。
- [ ] **Step 3: Commit** `docs: update AGENTS.md structure map for circuits subsystem`

## 月末完成定义

- 正式角色带动画在场景中行走建造
- `CircuitGraphData` → `CircuitCompiler.Compile` → 校验错误全集可测且带节点定位；合法图产出确定性的指令数组（含分支跳转与循环回跳）
- 回路子系统零 Godot 依赖、零 Simulation 依赖

## 明确不做（本月）

- VM 执行（第 3 个月）
- GraphEdit 编辑器 UI（第 3 个月）
- 魔像实体与动作（第 3 个月）
