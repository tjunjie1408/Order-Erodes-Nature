# 第 3 个月：M2 收尾——可挂起 VM、魔像与 GraphEdit 编辑器 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **开工前校准（必读）**：开工第一步对照实际代码核对 Interfaces（尤其第 2 个月的 `Instruction`/`OpCode`/`CompiledCircuit`/`CircuitGraphData` 签名与错误码全集），有漂移先修订本计划。本月是整个项目的核心体验验证点——月末必须能"给魔像编程让它干活"，此目标不可降级。

**Goal:** 实现可挂起的回路 VM、SimCore 中的魔像/资源点/储存碑实体、GraphEdit 定制编辑器，端到端打通"连节点→刻录→魔像执行采集循环"。

**Architecture:** VM 与魔像之间以纯数据结构 `VmIo` 交互（ISP 铁律）：机器 tick 时先填传感值 → VM 执行若干指令 → 机器读出动作请求并推进动作 → 动作完成回写状态。编辑器只做 UI：GraphEdit 拓扑 ⇄ `CircuitGraphData` 互转，编译与错误显示走第 2 个月的编译器。VM 执行位置经事件（`VmNodeEntered`，节流）流向编辑器做高亮。

**Tech Stack:** 同前；Godot `GraphEdit`/`GraphNode` 控件

**规格来源:** 设计文档 §5（回路执行模型、运行时只读铁律、VM 状态可序列化）、§4（符文回路系统/回路编译器）

## Global Constraints

- 同前两月全部铁律。本月新增两条来自规格的红线：
  - **运行中的回路指令流只读**：修改回路重新刻录、或魔像模块变更 → 强制 `Vm.Reset()` 从入口重跑
  - VM 运行态只含平凡值（`ProgramCounter`/`WaitTicksRemaining`/寄存器数组/动作状态枚举），禁止委托入档
- VM 每 tick 指令预算上限 256 条——超出即判死循环，VM 进入 `Crashed` 状态并发事件（故障美学的机制钩子，M4 消费）

## 文件结构（本月新建）

```
SimCore/Circuits/VmIo.cs               VM⇄宿主的纯数据桥
SimCore/Circuits/CircuitVm.cs          可挂起解释器
SimCore/Machines/GolemState.cs         魔像仿真实体
SimCore/Machines/ResourceNode.cs       资源点
SimCore/Machines/StorageState.cs       储存碑
SimCore/SimVec3.cs                     仿真用三维向量（float，零 Godot）
SimCore/Simulation.cs                  接入魔像/资源点/储存的 tick 与指令（Modify）
SimCore/Commands.cs                    新增刻录/生成魔像指令（Modify）
SimCore/SimEvents.cs                   新增 VM/魔像事件（Modify）
SimCore.Tests/Circuits/CircuitVmTests.cs
SimCore.Tests/Machines/GolemTests.cs
SimCore.Tests/Machines/EndToEndHarvestTests.cs
game/scenes/CircuitEditor.tscn         GraphEdit 编辑器界面
game/scripts/CircuitEditor.cs          UI⇄CircuitGraphData 互转、编译错误标红
game/scripts/GolemView.cs              魔像插值视图
game/scripts/BuildController.cs        资源点/储存/魔像的放置与视图（Modify）
```

---

### Task 1: VmIo 与可挂起 VM

**Files:**
- Create: `SimCore/Circuits/VmIo.cs`, `SimCore/Circuits/CircuitVm.cs`
- Test: `SimCore.Tests/Circuits/CircuitVmTests.cs`

**Interfaces:**
- Produces（锁死）:

```csharp
namespace SimCore.Circuits;

public enum VmStatus : byte { Idle, Running, Suspended, Crashed }
public enum ActionKind : byte { None, MoveTo, Harvest, Load, Unload, Wait }
public enum ActionResult : byte { InProgress, Done, Failed }

/// <summary>VM 与宿主机器之间唯一的交互面。全平凡值。</summary>
public sealed class VmIo
{
    // 宿主 → VM（tick 前由宿主填写）
    public double SensorCargo;
    public ActionResult PendingActionResult = ActionResult.Done;
    // VM → 宿主（tick 后由宿主读取）
    public ActionKind RequestedAction = ActionKind.None;
    public double ActionX, ActionY, ActionZ;   // MoveTo/Harvest 目标或 Wait tick 数(存 ActionX)
}

public sealed class CircuitVm
{
    public VmStatus Status { get; private set; } = VmStatus.Idle;
    public int ProgramCounter { get; private set; }
    public int CrashPc { get; private set; } = -1;
    public double[] Registers { get; private set; } = Array.Empty<double>();

    public void LoadProgram(CompiledCircuit circuit);   // 引用只读程序 + Reset
    public void Reset();                                 // PC=入口, 寄存器清零, Status=Running
    /// <summary>执行至挂起/预算耗尽。返回本 tick 进入过的指令下标（高亮用，复用传入 list）。</summary>
    public void Tick(VmIo io, List<int> enteredPcs);
    public const int InstructionBudgetPerTick = 256;
}
```

- **相对寻址节点（规格红线：玩家不手输绝对坐标）**：`NodeCatalog` 本月新增 `sensor_find_nearest_resource`（输出 nearest:Vector + found:Bool）与 `sensor_nearest_storage`（同构）；对应 OpCode `FindNearestResource`/`FindNearestStorage`（C=目标寄存器组基址，连续三个寄存器存 xyz，C+3 存 found）。VmIo 增加宿主填写的传感目标：`SensorNearestResX/Y/Z, SensorNearestResFound` 等字段（宿主每 tick 从仿真空间索引查询后填入）。`data_const_number` 拼坐标与编辑器坐标拾取保留为后备路径
- 语义：可挂起指令（MoveTo/Harvest/Load/Unload/Wait）执行时把请求写入 `io` 并转 `Suspended`；下一 tick 开头若 `io.PendingActionResult == Done` 则 PC+1 继续，`InProgress` 则维持挂起，`Failed` 亦 PC+1 继续（MVP 简化：失败不崩溃，行为由回路自己用传感器判断）。非挂起指令连续执行，单 tick 超 256 条 → `Crashed`（记录 `CrashPc`）。`Wait` 由 VM 自己倒数（`WaitTicksRemaining` 内部状态），不经宿主。

- [ ] **Step 1: 写失败测试**（手工构造 `Instruction[]` 直测 VM，不依赖编译器）

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
        vm.Tick(io, pcs);                       // 进入 Wait，挂起
        Assert.Equal(VmStatus.Suspended, vm.Status);
        vm.Tick(io, pcs); vm.Tick(io, pcs);     // 倒数
        vm.Tick(io, pcs);                       // 结束，走到 Halt
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
        Assert.Equal(VmStatus.Suspended, vm.Status);   // 仍在移动
        io.PendingActionResult = ActionResult.Done;
        vm.Tick(io, pcs);
        Assert.Equal(VmStatus.Idle, vm.Status);        // 完成后走到 Halt
    }

    [Fact]
    public void TightJumpLoop_CrashesOnBudget()
    {
        var vm = Load(new(OpCode.Jump, 0, 0, 0, 0));   // 自跳死循环
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
        // r0=0(false); JumpIfFalse r0 -> 3; Halt(不应到达); LoadConst r1=9; Halt
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

另补 `Compare`（三种模式）与 `ReadSensor`（读 `io.SensorCargo`）各一个用例。

- [ ] **Step 2: 确认失败** → **Step 3: 实现 VM**（单 while 循环 + switch(op)；挂起=写请求置状态返回；预算计数器每指令 -1）→ **Step 4: `dotnet test` 全绿** → **Step 5: Commit** `feat(circuits): suspendable vm with instruction budget and crash state`

---

### Task 2: 魔像、资源点、储存碑（SimCore 实体与动作推进）

**Files:**
- Create: `SimCore/SimVec3.cs`, `SimCore/Machines/GolemState.cs`, `SimCore/Machines/ResourceNode.cs`, `SimCore/Machines/StorageState.cs`
- Modify: `SimCore/Simulation.cs`, `SimCore/Commands.cs`, `SimCore/SimEvents.cs`
- Test: `SimCore.Tests/Machines/GolemTests.cs`

**Interfaces:**
- Produces:

```csharp
public readonly record struct SimVec3(float X, float Y, float Z);

// 指令新增
public sealed record SpawnGolemCommand(SimVec3 Position) : ICommand;
public sealed record PlaceResourceNodeCommand(GridPos Position, string ResourceType, int Amount) : ICommand;
public sealed record PlaceStorageCommand(GridPos Position) : ICommand;
public sealed record InscribeCircuitCommand(int GolemId, CircuitGraphData Graph) : ICommand;  // 内部编译，失败发 CircuitRejected 事件

// 事件新增
public sealed record GolemSpawned(int GolemId, SimVec3 Position) : SimEvent;
public sealed record CircuitRejected(int GolemId, List<CompileError> Errors) : SimEvent;
public sealed record CircuitInscribed(int GolemId) : SimEvent;
public sealed record VmCrashed(int GolemId, int CrashPc) : SimEvent;

// GolemState：Id、Position(SimVec3，含 PrevPosition 供插值)、CargoCount、CargoType、
//   Vm(CircuitVm)、Io(VmIo)、CurrentAction 推进状态
// 魔像 tick：填 Io 传感 → Vm.Tick → 读请求 → 推进动作：
//   MoveTo: 以 3格/秒(0.15/tick) 向目标平面直线移动，距离<0.1 判 Done
//   Harvest: 目标 1.5 格内有资源点则 20 tick 后 Cargo+1、资源 Amount-1，否则 Failed
//   Load/Unload: 1.5 格内有储存碑则瞬时转移全部货物，否则 Failed
```

- [ ] **Step 1: 写失败测试**（关键用例）

```csharp
[Fact] public void Golem_MovesTowardTarget_AtFixedSpeedPerTick()
[Fact] public void Harvest_NearNode_TakesTwentyTicks_AndDecrementsNode()
[Fact] public void Harvest_FarFromNode_ReportsFailed_VmContinues()
[Fact] public void Unload_TransfersCargoToStorage()
[Fact] public void InscribeInvalidGraph_EmitsCircuitRejected_GolemUnchanged()
[Fact] public void InscribeWhileRunning_ResetsVm()          // 运行时只读铁律
[Fact] public void CrashedVm_EmitsVmCrashedEvent_Once()
```

每个用例用指令/命令驱动 `Simulation`，tick 若干次断言状态与事件（写法同第 1 个月 PlacementTests 的 TickAndDrain 模式）。

- [ ] **Step 2: 确认失败** → **Step 3: 实现**（`Simulation.Tick` 顺序：消费指令 → 各魔像 tick（含 VM）→ TickCount++；魔像存储用 `List<GolemState>` 平铺遍历；`PrevPosition` 在每 tick 开头快照）→ **Step 4: 全绿** → **Step 5: Commit** `feat(sim): golems with vm-driven actions, resource nodes, storage`

---

### Task 3: 端到端采集循环（纯 SimCore 验收测试）

**Files:**
- Test: `SimCore.Tests/Machines/EndToEndHarvestTests.cs`

**Interfaces:**
- Consumes: 全部前序接口。**本任务是 M2 的机器可读验收**：用 GraphBuilder 构造真实回路图（非手写指令）——"启动时 → 移动到资源点 → 采集 → 移动到储存碑 → 卸货 →（循环）"，经 `InscribeCircuitCommand` 刻录，跑 2000 tick。

- [ ] **Step 1: 写测试**

```csharp
[Fact]
public void GolemProgrammedViaGraph_FillsStorage_OverTime()
{
    // 布置：魔像(0,0,0)、资源点(5,0,0, amount=10)、储存碑(0,0,5)
    // 构图（相对寻址，不硬编码坐标）：
    //   event_start → move_to(target=sensor_find_nearest_resource.nearest)
    //   → harvest(同上) → move_to(target=sensor_nearest_storage.nearest) → unload → (自动回跳)
    // 断言：2000 tick 后储存碑内货物 ≥ 3 且资源点 Amount 相应减少；VM 从未 Crashed
}

[Fact]
public void ResourceExhausted_GolemFindsNextNode_Automatically()
{
    // 两个资源点，近的 amount=2 采完后，同一回路应自动转向远的那个——相对寻址的核心收益
}

[Fact]
public void NoResourceLeft_GolemKeepsCycling_WithFailedHarvests_NoCrash()
```

- [ ] **Step 2: 跑测试**——大概率暴露编译器/VM/魔像联动 bug，逐个修复直至绿。这是本月最重要的调试期，预留充足时间。
- [ ] **Step 3: Commit** `test(sim): end-to-end programmed harvest loop`

---

### Task 4: GraphEdit 回路编辑器（UI）

**Files:**
- Create: `game/scenes/CircuitEditor.tscn`, `game/scripts/CircuitEditor.cs`
- Modify: `game/scripts/Main.cs`（打开/关闭编辑器的组装）、`game/scripts/BuildController.cs`（对准魔像按 E 打开其编辑器）

**Interfaces:**
- Consumes: `NodeCatalog.All`、`CircuitGraphData`、`CircuitCompiler.Compile`、`InscribeCircuitCommand`、`CircuitRejected`/`CircuitInscribed` 事件
- Produces: `CircuitEditor : Control`——`void OpenFor(int golemId, CircuitGraphData current)`、`event Action<int, CircuitGraphData>? InscribeRequested`。UI 契约：右键画布弹节点面板（来自 NodeCatalog，过滤测试专用节点）；GraphNode 端口 slot 类型 = DataType 映射的 int（GraphEdit 原生阻止类型不符的连线）；Exec 与 Data 端口用不同颜色；"刻录"按钮先本地 `Compile`，有错则对应 GraphNode 标红 + 底部错误列表（错误码→中文文案表），无错才发 `InscribeRequested`

- [ ] **Step 1: 场景骨架**：CanvasLayer → Panel → VBox：GraphEdit（expand）+ HBox（错误 Label + 刻录 Button）。Esc/E 关闭并恢复鼠标捕获。
- [ ] **Step 2: NodeCatalog → GraphNode 生成**：每个 NodeDef 一个构造函数式方法——标题=DisplayName，逐端口 `SetSlot`（左入右出，Exec=白色/type 0，Number=蓝/1，Bool=橙/2，Vector=绿/3）；`data_const_number` 等内联参数用 SpinBox 嵌入。
- [ ] **Step 3: 双向序列化**：`ToGraphData()`（遍历 GraphEdit 子节点与 connections → CircuitGraphData，含 EditorX/Y）与 `LoadGraphData()`（反向重建）。写一个 Godot 侧手动往返检查：构图→序列化→清空→加载→肉眼一致。
- [ ] **Step 4: 编译错误显示**：`Compile` 失败 → 按 `CompileError.NodeId` 找 GraphNode 加红色 StyleBox 覆盖 + 错误列表显示 `Message`；成功 → 发 `InscribeRequested`，Main 转发为 `InscribeCircuitCommand`。
- [ ] **Step 5: 手动验证**：F5 → 对准魔像按 E → 搭"启动时→移动到→采集→移动到→卸载" → 故意断开必填输入看到标红 → 修好后刻录成功（魔像开始动作即 Task 5 完成后可见）。
- [ ] **Step 6: Commit** `feat(game): grapheedit circuit editor with compile-error highlighting`

---

### Task 5: 魔像视图与执行高亮

**Files:**
- Create: `game/scripts/GolemView.cs`
- Modify: `game/scripts/BuildController.cs`（资源点/储存碑/魔像的放置模式与视图生成——放置模式按 1/2/3 键切换 base_block/资源点/储存碑/魔像）、`game/scripts/CircuitEditor.cs`（高亮）
- Modify: `SimCore/SimEvents.cs` + `SimCore/Machines/GolemState.cs`（新增节流事件 `GolemProgress(int GolemId, int CurrentPc, SimVec3 Position, SimVec3 PrevPosition)`——每魔像每 tick 一条）

**Interfaces:**
- Produces: `GolemView : Node3D`——监听 `GolemProgress`，在 `_Process` 中用 `prev→current` 按帧内 alpha 插值位置（规格 §4 插值机制的首次真实应用）；悬浮几何体程序动画（缓慢自旋 + 上下浮动）。编辑器打开时按 `CurrentPc` 反查指令→节点映射（编译器在 `CompiledCircuit` 增加 `int[] InstructionToNodeId` 调试映射表，本任务加入并补编译器测试）高亮当前 GraphNode。

- [ ] **Step 1: 编译器补调试映射**（TDD：`InstructionToNodeId.Length == Instructions.Length`，branch 程序中 Wait 指令映射回 action_wait 的 NodeId）
- [ ] **Step 2: GolemView 插值移动**（灰盒：发光八面体 = 两个错位 BoxMesh + emission）
- [ ] **Step 3: 编辑器高亮**（打开状态下当前节点加蓝色发光 StyleBox，随 GolemProgress 刷新）
- [ ] **Step 4: 手动验证——M2 核心验收**：F5 → 放资源点/储存碑 → 放魔像 → E 打开编辑器 → 搭采集循环 → 刻录 → 魔像平滑地往返搬运，编辑器里当前执行节点发光流转；拆掉储存碑看到采集失败后回路仍循环（不崩）；搭一个纯 Jump 死循环图刻录 → VM 崩溃事件（暂用日志输出确认，视觉故障归 M4）
- [ ] **Step 5: 回归 `dotnet test` 全绿** → **Step 6: Commit** `feat(game): golem view with interpolation and live node highlighting`

---

### Task 6: VM 状态入档

**Files:**
- Modify: `SimCore/Persistence/SaveData.cs`（新增 `List<GolemData>`：Id/位置/货物/`GraphJson`(刻录的图)/`ProgramCounter`/`WaitTicksRemaining`/`Status`/寄存器数组）、`SimCore/Simulation.cs`（快照含魔像；`FromSnapshot` 重编译 GraphJson 恢复程序——规格 §5：编译产物不进存档）
- Test: `SimCore.Tests/SaveRoundTripTests.cs` 追加

- [ ] **Step 1: 写失败测试**：`SuspendedGolem_SurvivesRoundTrip_AndResumesMidAction`（魔像移动中途存档→恢复→继续 tick 最终到达目标）；`RoundTrip_SnapshotJsonIdentical`（含魔像的快照 json 往返一致）；`AdversarialFloats_RoundTripBitExact`（把魔像位置/寄存器设为 0.1f、1.0/3.0、1e-17 等对抗值 → json 往返位级一致。规格红线：**禁止用小数位截断"修"这个测试**——截断即存档改变仿真状态）
- [ ] **Step 2: 实现** → **Step 3: 全绿** → **Step 4: Commit** `feat(sim): golem vm state survives save round-trip`

## 月末完成定义（= M2 验收）

- 端到端：世界里放置资源点/储存碑/魔像，打开编辑器连节点、错误标红、刻录、魔像执行采集循环、当前节点实时高亮
- 死循环回路触发 VM Crashed 事件；刻录/模块变更强制 Reset
- 挂起中的 VM 状态可存档往返恢复
- `dotnet test` 全绿（预计 ≥35 个测试）

## 明确不做（本月）

- 能量约束（魔像暂不耗能，M3）、多方块拼装（M3）、信号节点（M5）
- 故障美学视觉（M4，本月只发事件）、编辑器交互打磨（M4）
