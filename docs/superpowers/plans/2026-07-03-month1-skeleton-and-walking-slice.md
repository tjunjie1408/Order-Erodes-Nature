# 第 1 个月：M0 骨架 + M1 行走切片 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立 SimCore 仿真骨架（tick/指令/事件/存档往返）并打通 Godot 端到端：灰盒地形上第三人称角色行走，通过仿真指令放置/拆除网格方块。

**Architecture:** 纯 C# 类库 SimCore（net8.0，零 Godot 依赖）承载全部仿真逻辑，xUnit 测试驱动；Godot 4.4+ (.NET) 项目 `game/` 通过固定步长累加器驱动 `Simulation.Tick()`，表现层只发指令、读事件。本月全部视觉为灰盒（几何体占位），不引入任何素材。

**Tech Stack:** .NET 8 SDK、xUnit、Godot 4.4+ .NET 版、System.Text.Json

**规格来源:** `docs/superpowers/specs/2026-07-03-magic-automation-game-design.md`

## 月度路线图（全部 7 份计划见本目录 [README.md](README.md)）

| 月 | 对应里程碑 | 内容概要 |
|---|---|---|
| 1 | M0 + M1 前半 | 本计划 |
| 2 | M1 收尾 + M2 开始 | Mixamo 角色替换灰盒、回路数据模型与编译器、VM 雏形 |
| 3 | M2 | GraphEdit 编辑器、可挂起 VM、7 个节点、魔像端到端 |
| 4 | M3 | 多方块拼装、能量网络、浮运轨道、转化法阵 |
| 5 | M4 | shader 套件、故障美学、音效、UI 主题 |
| 6-7 | M5 | 存档完善、信号系统、蓝图库、内容补足 |

每月开始时基于上月实际进度写当月详细计划。

## Global Constraints

- SimCore 禁止 `using Godot`（任何形式的 Godot 依赖）
- SimCore 子系统之间禁止直接引用，只通过事件或纯数据结构交互
- 仿真固定 20 tps；指令只在 tick 边界被消费；表现层永不直接写仿真状态
- 热路径（Tick 内部）禁止分配：无 LINQ、无临时集合 new（本月骨架允许指令/事件对象本身的分配）
- 存档状态只含平凡值类型，禁止委托/Lambda/引用回调入档
- git 提交信息**不写 Co-Authored-By**
- 所有新素材/第三方内容必须 CC0 或明确可商用（本月无素材引入）
- Windows 环境，shell 为 PowerShell；`godot` 指 Godot 4.4+ .NET 版可执行文件

## 前置条件（Task 1 的 Step 0 验证）

- .NET 8 SDK 已安装（`dotnet --version` ≥ 8.0）
- Godot 4.4+ **.NET 版**已下载（godotengine.org/download，必须选 ".NET" 变体），解压后记下 exe 路径

## 文件结构（本月新建）

```
dev_game.sln
.gitignore
AGENTS.md                                Codex 项目说明书（架构铁律）
CREDITS.md                               素材许可台账（先建空表）
docs/backlog.md                          范围外点子停车场
SimCore/SimCore.csproj                   net8.0 类库
SimCore/GridPos.cs                       网格坐标值类型
SimCore/Commands.cs                      指令类型定义
SimCore/SimEvents.cs                     事件类型定义
SimCore/Structure.cs                     结构（占位方块）实体
SimCore/Simulation.cs                    tick 循环、指令队列、事件缓冲、网格状态
SimCore/Persistence/SaveData.cs          存档 DTO
SimCore/Persistence/SaveSerializer.cs    JSON 序列化 + 快照
SimCore.Tests/SimCore.Tests.csproj       xUnit
SimCore.Tests/SimulationTests.cs
SimCore.Tests/PlacementTests.cs
SimCore.Tests/SaveRoundTripTests.cs
game/project.godot                       Godot 项目（编辑器创建）
game/game.csproj                         Godot 生成，需加 SimCore 引用
game/scenes/Main.tscn                    主场景
game/scripts/Main.cs                     灰盒地形构建 + 组装
game/scripts/SimDriver.cs                固定步长累加器桥
game/scripts/PlayerController.cs         第三人称控制器
game/scripts/BuildController.cs          幽灵预览 + 放置/拆除 + 结构视图
```

---

### Task 1: 解决方案骨架与冒烟测试

**Files:**
- Create: `dev_game.sln`, `SimCore/SimCore.csproj`, `SimCore.Tests/SimCore.Tests.csproj`, `.gitignore`
- Create: `SimCore/GridPos.cs`, `SimCore.Tests/SimulationTests.cs`（先只放冒烟测试）

**Interfaces:**
- Produces: `SimCore.GridPos`（`readonly record struct GridPos(int X, int Y, int Z)`），后续所有任务使用

- [ ] **Step 0: 验证前置条件**

Run: `dotnet --version`
Expected: 8.x 或更高。若无，停下让用户安装 .NET 8 SDK。

- [ ] **Step 1: 创建 solution 与项目**

```powershell
dotnet new sln -n dev_game
dotnet new classlib -n SimCore -f net8.0
dotnet new xunit -n SimCore.Tests -f net8.0
dotnet sln add SimCore SimCore.Tests
dotnet add SimCore.Tests reference SimCore
Remove-Item SimCore/Class1.cs, SimCore.Tests/UnitTest1.cs
```

- [ ] **Step 2: 写 .gitignore**

```gitignore
# .NET
bin/
obj/
*.user

# Godot
game/.godot/
game/*.translation
.mono/

# OS
Thumbs.db
.DS_Store
```

- [ ] **Step 3: 写 GridPos 与冒烟测试**

`SimCore/GridPos.cs`:
```csharp
namespace SimCore;

public readonly record struct GridPos(int X, int Y, int Z);
```

`SimCore.Tests/SimulationTests.cs`:
```csharp
using SimCore;
using Xunit;

namespace SimCore.Tests;

public class SimulationTests
{
    [Fact]
    public void GridPos_ValueEquality()
    {
        Assert.Equal(new GridPos(1, 2, 3), new GridPos(1, 2, 3));
    }
}
```

- [ ] **Step 4: 跑测试确认通过**

Run: `dotnet test`
Expected: `Passed! - 1 test`

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "chore: solution skeleton with SimCore classlib and xunit tests"
```

---

### Task 2: 项目基建文档（AGENTS.md / CREDITS.md / backlog.md）

**Files:**
- Create: `AGENTS.md`, `CREDITS.md`, `docs/backlog.md`

**Interfaces:**
- Produces: 无代码接口；AGENTS.md 是之后所有 Codex 会话的行为契约

- [ ] **Step 1: 写 AGENTS.md**

```markdown
# AGENTS.md — 项目协作契约

## 项目是什么
《秩序侵蚀自然》：Godot 4 + C# 的 3D 自动化建造游戏。
设计文档：docs/superpowers/specs/2026-07-03-magic-automation-game-design.md（布置任务引用章节，不重新发明）。

## 架构铁律（违反即打回）
1. SimCore/ 禁止 `using Godot`。仿真逻辑不依赖引擎。
2. SimCore 内子系统（回路VM、能量网络、物品流、建造拓扑）禁止直接互相引用，
   只通过事件或纯数据结构交互。
3. 表现层（game/）只发指令、读状态与事件，永不直接写仿真状态。
4. `Simulation.Tick()` 及其调用链禁止分配：无 LINQ、无临时集合。
5. 存档状态只含平凡值类型，禁止委托/Lambda 入档。
6. 运行中的回路指令流只读；回路或结构模块变更一律复位 VM。

## 项目结构
- SimCore/ — 纯 C# 仿真核心（net8.0）
- SimCore.Tests/ — xUnit 测试
- game/ — Godot 项目（场景、shader、表现层脚本）
- docs/ — 设计文档、计划、backlog

## 工作方式
- 测试命令：`dotnet test`（改 SimCore 必跑）
- SimCore 采用 TDD：先写失败测试再实现。
- 每个任务一个 commit。commit 信息不写 Co-Authored-By。
- 不确定的 Godot API 先查 https://docs.godotengine.org/en/stable/ 再动手。
- 新增第三方依赖、跨模块重构、更改本文件规则：必须先经开发者批准。
- 范围外的点子写进 docs/backlog.md，不实现。

## 代码风格
- C# 标准命名（PascalCase 公有、_camelCase 私有字段）
- 文件小而聚焦：一个类型一个文件；场景小而多：一个模块一个场景
```

- [ ] **Step 2: 写 CREDITS.md**

```markdown
# CREDITS — 素材许可台账

每引入一个外部素材记一行。只收 CC0 或明确可商用许可，避开 NC 条款。

| 素材 | 来源 | 许可 | 用途 |
|---|---|---|---|
| （暂无） | | | |
```

- [ ] **Step 3: 写 docs/backlog.md**

```markdown
# Backlog — 范围外点子停车场

当前里程碑之外的点子一律记在这里，不实现。

- 崩溃结构周边植被枯萎沙化（后 MVP，来自设计文档氛围机制）
- 真理核心矩阵填装交互（后 MVP，MVP 用简单面板占位）
```

- [ ] **Step 4: Commit**

```powershell
git add AGENTS.md CREDITS.md docs/backlog.md
git commit -m "docs: add AGENTS.md contract, credits ledger, and backlog"
```

---

### Task 3: Simulation 核心——tick、指令队列、事件缓冲

**Files:**
- Create: `SimCore/Commands.cs`, `SimCore/SimEvents.cs`, `SimCore/Structure.cs`, `SimCore/Simulation.cs`
- Modify: `SimCore.Tests/SimulationTests.cs`

**Interfaces:**
- Consumes: `GridPos`（Task 1）
- Produces:
  - `interface ICommand`（标记接口）
  - `abstract record SimEvent`
  - `class Simulation`：`const int TicksPerSecond = 20`、`long TickCount { get; }`、`void EnqueueCommand(ICommand)`、`void Tick()`、`void DrainEvents(List<SimEvent> into)`
  - 指令在 **Tick 开头** 被消费；事件由 Tick 产生、由调用者 Drain

- [ ] **Step 1: 写失败测试**

在 `SimCore.Tests/SimulationTests.cs` 中追加：

```csharp
[Fact]
public void Tick_IncrementsTickCount()
{
    var sim = new Simulation();
    sim.Tick();
    sim.Tick();
    Assert.Equal(2, sim.TickCount);
}

[Fact]
public void EnqueueCommand_IsNotAppliedUntilTick()
{
    var sim = new Simulation();
    sim.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(0, 0, 0)));
    Assert.Empty(sim.Structures);          // 入队不生效
    sim.Tick();
    Assert.Single(sim.Structures);         // tick 边界生效
}

[Fact]
public void DrainEvents_ReturnsEventsOnceAndClears()
{
    var sim = new Simulation();
    sim.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(0, 0, 0)));
    sim.Tick();
    var events = new List<SimEvent>();
    sim.DrainEvents(events);
    Assert.Single(events);
    Assert.IsType<StructurePlaced>(events[0]);
    events.Clear();
    sim.DrainEvents(events);
    Assert.Empty(events);                  // 第二次 drain 为空
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test`
Expected: 编译错误（`Simulation`、`PlaceStructureCommand` 等类型不存在）——这是本步的"失败"形态。

- [ ] **Step 3: 最小实现**

`SimCore/Commands.cs`:
```csharp
namespace SimCore;

public interface ICommand { }

public sealed record PlaceStructureCommand(string StructureType, GridPos Position) : ICommand;

public sealed record RemoveStructureCommand(GridPos Position) : ICommand;
```

`SimCore/SimEvents.cs`:
```csharp
namespace SimCore;

public abstract record SimEvent;

public sealed record StructurePlaced(int StructureId, string StructureType, GridPos Position) : SimEvent;

public sealed record StructureRemoved(int StructureId, GridPos Position) : SimEvent;

public sealed record CommandRejected(ICommand Command, string Reason) : SimEvent;
```

`SimCore/Structure.cs`:
```csharp
namespace SimCore;

public sealed class Structure
{
    public required int Id { get; init; }
    public required string Type { get; init; }
    public required GridPos Position { get; init; }
}
```

`SimCore/Simulation.cs`:
```csharp
namespace SimCore;

public sealed class Simulation
{
    public const int TicksPerSecond = 20;

    public long TickCount { get; private set; }

    private readonly Queue<ICommand> _commands = new();
    private readonly List<SimEvent> _events = new();
    private readonly Dictionary<GridPos, Structure> _byPos = new();
    private readonly Dictionary<int, Structure> _byId = new();
    private int _nextStructureId = 1;

    public IReadOnlyCollection<Structure> Structures => _byId.Values;

    public void EnqueueCommand(ICommand command) => _commands.Enqueue(command);

    public void Tick()
    {
        while (_commands.Count > 0)
            Apply(_commands.Dequeue());
        TickCount++;
    }

    /// <summary>供表现层做幽灵预览的只读合法性校验（与 Apply 共用同一规则）。</summary>
    public bool CanPlace(GridPos pos) => !_byPos.ContainsKey(pos);

    public void DrainEvents(List<SimEvent> into)
    {
        into.AddRange(_events);
        _events.Clear();
    }

    private void Apply(ICommand command)
    {
        switch (command)
        {
            case PlaceStructureCommand place:
                if (!CanPlace(place.Position))
                {
                    _events.Add(new CommandRejected(command, "cell_occupied"));
                    return;
                }
                var s = new Structure
                {
                    Id = _nextStructureId++,
                    Type = place.StructureType,
                    Position = place.Position,
                };
                _byPos[s.Position] = s;
                _byId[s.Id] = s;
                _events.Add(new StructurePlaced(s.Id, s.Type, s.Position));
                break;

            case RemoveStructureCommand remove:
                if (!_byPos.TryGetValue(remove.Position, out var existing))
                {
                    _events.Add(new CommandRejected(command, "cell_empty"));
                    return;
                }
                _byPos.Remove(existing.Position);
                _byId.Remove(existing.Id);
                _events.Add(new StructureRemoved(existing.Id, existing.Position));
                break;
        }
    }
}
```

同时在测试文件顶部补 `using System.Collections.Generic;`（若隐式 using 已覆盖可省）。

- [ ] **Step 4: 跑测试确认通过**

Run: `dotnet test`
Expected: 全部 PASS（4 tests）

- [ ] **Step 5: Commit**

```powershell
git add SimCore SimCore.Tests
git commit -m "feat(sim): tick loop, command queue, event buffer with place/remove"
```

---

### Task 4: 放置规则测试补全（占用拒绝、拆除、事件内容）

**Files:**
- Create: `SimCore.Tests/PlacementTests.cs`

**Interfaces:**
- Consumes: Task 3 的全部类型。本任务只补测试锁行为，不加新实现（若测试暴露 bug 则修）。

- [ ] **Step 1: 写测试**

```csharp
using SimCore;
using Xunit;

namespace SimCore.Tests;

public class PlacementTests
{
    private static List<SimEvent> TickAndDrain(Simulation sim)
    {
        sim.Tick();
        var events = new List<SimEvent>();
        sim.DrainEvents(events);
        return events;
    }

    [Fact]
    public void PlaceOnOccupiedCell_EmitsCommandRejected_AndKeepsOriginal()
    {
        var sim = new Simulation();
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(1, 0, 1)));
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(1, 0, 1)));
        var events = TickAndDrain(sim);

        Assert.Single(sim.Structures);
        var rejected = Assert.Single(events.OfType<CommandRejected>());
        Assert.Equal("cell_occupied", rejected.Reason);
    }

    [Fact]
    public void RemoveEmptyCell_EmitsCommandRejected()
    {
        var sim = new Simulation();
        sim.EnqueueCommand(new RemoveStructureCommand(new GridPos(5, 0, 5)));
        var events = TickAndDrain(sim);
        var rejected = Assert.Single(events.OfType<CommandRejected>());
        Assert.Equal("cell_empty", rejected.Reason);
    }

    [Fact]
    public void PlaceThenRemove_RoundTrip_CellIsPlaceableAgain()
    {
        var sim = new Simulation();
        var pos = new GridPos(2, 0, 3);
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", pos));
        var placed = Assert.Single(TickAndDrain(sim).OfType<StructurePlaced>());

        sim.EnqueueCommand(new RemoveStructureCommand(pos));
        var removed = Assert.Single(TickAndDrain(sim).OfType<StructureRemoved>());

        Assert.Equal(placed.StructureId, removed.StructureId);
        Assert.True(sim.CanPlace(pos));
        Assert.Empty(sim.Structures);
    }

    [Fact]
    public void CanPlace_MatchesApplyRule()
    {
        var sim = new Simulation();
        var pos = new GridPos(0, 0, 0);
        Assert.True(sim.CanPlace(pos));
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", pos));
        sim.Tick();
        Assert.False(sim.CanPlace(pos));
    }
}
```

（测试代码允许 LINQ；铁律只约束 Tick 热路径。）

- [ ] **Step 2: 跑测试**

Run: `dotnet test`
Expected: 全部 PASS（8 tests）。若有失败，修 `Simulation.Apply` 直至通过。

- [ ] **Step 3: Commit**

```powershell
git add SimCore.Tests
git commit -m "test(sim): lock placement rules - occupancy, removal, preview parity"
```

---

### Task 5: 存档往返与确定性

**Files:**
- Create: `SimCore/Persistence/SaveData.cs`, `SimCore/Persistence/SaveSerializer.cs`
- Create: `SimCore.Tests/SaveRoundTripTests.cs`
- Modify: `SimCore/Simulation.cs`（追加 CreateSnapshot/FromSnapshot）

**Interfaces:**
- Consumes: `Simulation`、`Structure`、`GridPos`
- Produces:
  - `class SaveData`：`int Version`、`long TickCount`、`int NextStructureId`、`List<StructureData> Structures`
  - `class StructureData`：`int Id; string Type; int X; int Y; int Z;`（全平凡值）
  - `static class SaveSerializer`：`string ToJson(SaveData)`、`SaveData FromJson(string)`
  - `Simulation.CreateSnapshot() : SaveData` 与 `static Simulation.FromSnapshot(SaveData) : Simulation`

- [ ] **Step 1: 写失败测试**

`SimCore.Tests/SaveRoundTripTests.cs`:
```csharp
using SimCore;
using SimCore.Persistence;
using Xunit;

namespace SimCore.Tests;

public class SaveRoundTripTests
{
    private static Simulation BuildSampleSim()
    {
        var sim = new Simulation();
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(0, 0, 0)));
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(1, 0, 0)));
        sim.Tick();
        sim.EnqueueCommand(new RemoveStructureCommand(new GridPos(0, 0, 0)));
        sim.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(2, 0, 5)));
        sim.Tick();
        sim.Tick();
        return sim;
    }

    [Fact]
    public void SaveRoundTrip_RestoresIdenticalState()
    {
        var original = BuildSampleSim();
        var json = SaveSerializer.ToJson(original.CreateSnapshot());
        var restored = Simulation.FromSnapshot(SaveSerializer.FromJson(json));

        // 快照的 JSON 表示必须完全一致
        Assert.Equal(json, SaveSerializer.ToJson(restored.CreateSnapshot()));
        Assert.Equal(original.TickCount, restored.TickCount);
        Assert.Equal(original.Structures.Count, restored.Structures.Count);
    }

    [Fact]
    public void RestoredSim_ContinuesWithoutIdCollision()
    {
        var original = BuildSampleSim();
        var restored = Simulation.FromSnapshot(
            SaveSerializer.FromJson(SaveSerializer.ToJson(original.CreateSnapshot())));

        restored.EnqueueCommand(new PlaceStructureCommand("base_block", new GridPos(9, 0, 9)));
        restored.Tick();
        var events = new List<SimEvent>();
        restored.DrainEvents(events);
        var placed = Assert.Single(events.OfType<StructurePlaced>());

        // 新 id 不与任何已有结构冲突
        Assert.DoesNotContain(restored.Structures,
            s => s.Id == placed.StructureId && s.Position != placed.Position);
    }

    [Fact]
    public void SameCommandSequence_ProducesIdenticalSnapshots()
    {
        var a = BuildSampleSim();
        var b = BuildSampleSim();
        Assert.Equal(SaveSerializer.ToJson(a.CreateSnapshot()),
                     SaveSerializer.ToJson(b.CreateSnapshot()));
    }
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test`
Expected: 编译错误（`SaveSerializer`、`CreateSnapshot` 不存在）

- [ ] **Step 3: 实现**

`SimCore/Persistence/SaveData.cs`:
```csharp
namespace SimCore.Persistence;

public sealed class SaveData
{
    public int Version { get; set; } = 1;
    public long TickCount { get; set; }
    public int NextStructureId { get; set; }
    public List<StructureData> Structures { get; set; } = new();
}

public sealed class StructureData
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}
```

`SimCore/Persistence/SaveSerializer.cs`:
```csharp
using System.Text.Json;

namespace SimCore.Persistence;

public static class SaveSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
    };

    public static string ToJson(SaveData data) => JsonSerializer.Serialize(data, Options);

    public static SaveData FromJson(string json) =>
        JsonSerializer.Deserialize<SaveData>(json, Options)
        ?? throw new InvalidDataException("save file deserialized to null");
}
```

在 `SimCore/Simulation.cs` 中追加（`using SimCore.Persistence;`）：
```csharp
    public SaveData CreateSnapshot()
    {
        var data = new SaveData
        {
            TickCount = TickCount,
            NextStructureId = _nextStructureId,
        };
        // 按 Id 排序保证快照字节级确定性
        foreach (var id in _byId.Keys.Order())
        {
            var s = _byId[id];
            data.Structures.Add(new StructureData
            {
                Id = s.Id, Type = s.Type,
                X = s.Position.X, Y = s.Position.Y, Z = s.Position.Z,
            });
        }
        return data;
    }

    public static Simulation FromSnapshot(SaveData data)
    {
        var sim = new Simulation
        {
            TickCount = data.TickCount,
            _nextStructureId = data.NextStructureId,
        };
        foreach (var sd in data.Structures)
        {
            var s = new Structure
            {
                Id = sd.Id, Type = sd.Type,
                Position = new GridPos(sd.X, sd.Y, sd.Z),
            };
            sim._byPos[s.Position] = s;
            sim._byId[s.Id] = s;
        }
        return sim;
    }
```

注意：`TickCount` 的 setter 需从 `private set` 保持不变——`FromSnapshot` 在类内部可用对象初始化器赋值；`_nextStructureId` 为字段可直接赋值。`CreateSnapshot` 不在热路径（仅存档时调用），允许分配与排序。

- [ ] **Step 4: 跑测试确认通过**

Run: `dotnet test`
Expected: 全部 PASS（11 tests）

- [ ] **Step 5: Commit**

```powershell
git add SimCore SimCore.Tests
git commit -m "feat(sim): save snapshot round-trip with determinism tests"
```

---

### Task 6: Godot 项目接入与 SimDriver 固定步长桥

**Files:**
- Create: `game/project.godot`（Godot 编辑器生成）、`game/scenes/Main.tscn`、`game/scripts/Main.cs`、`game/scripts/SimDriver.cs`
- Modify: `game/game.csproj`（加 SimCore 引用）、`dev_game.sln`

**Interfaces:**
- Consumes: `Simulation`、`SimEvent`
- Produces:
  - `SimDriver : Node`：`Simulation Sim { get; }`、`event Action<SimEvent>? SimEventEmitted`、每帧累加 delta 并以 20tps 调 `Sim.Tick()` 后逐个派发事件
  - `Main : Node3D`：程序化灰盒地面；场景树 `Main/SimDriver`、`Main/UI/TickLabel`

- [ ] **Step 1: 创建 Godot 项目**

打开 Godot 4.4+ (.NET)，New Project → 路径选 `D:\self-learning\dev_game\game`，渲染器选 Forward+，创建后关闭编辑器。然后：

```powershell
dotnet sln add game/game.csproj
dotnet add game/game.csproj reference SimCore/SimCore.csproj
```

（`game.csproj` 在 Godot 首次构建 C# 时生成；若不存在，先在编辑器里随便建一个 C# 脚本触发生成。）

- [ ] **Step 2: 写 SimDriver**

`game/scripts/SimDriver.cs`:
```csharp
using Godot;
using SimCore;
using System;
using System.Collections.Generic;

public partial class SimDriver : Node
{
    public Simulation Sim { get; private set; } = new();

    public event Action<SimEvent>? SimEventEmitted;

    private const double TickInterval = 1.0 / Simulation.TicksPerSecond;
    private double _accumulator;
    private readonly List<SimEvent> _frameEvents = new();

    public override void _Process(double delta)
    {
        _accumulator += delta;
        while (_accumulator >= TickInterval)
        {
            _accumulator -= TickInterval;
            Sim.Tick();
            Sim.DrainEvents(_frameEvents);
            foreach (var e in _frameEvents)
                SimEventEmitted?.Invoke(e);
            _frameEvents.Clear();
        }
    }
}
```

- [ ] **Step 3: 写 Main（程序化灰盒地面 + tick 显示）**

`game/scripts/Main.cs`:
```csharp
using Godot;

public partial class Main : Node3D
{
    private SimDriver _driver = null!;
    private Label _tickLabel = null!;

    public override void _Ready()
    {
        _driver = GetNode<SimDriver>("SimDriver");
        _tickLabel = GetNode<Label>("UI/TickLabel");
        BuildGroundSlab();
        AddSun();
        // 临时观察相机；Task 7 玩家相机设 Current=true 后自动接管
        AddChild(new Camera3D
        {
            Position = new Vector3(0, 6, 12),
            RotationDegrees = new Vector3(-20, 0, 0),
            Current = true,
        });
    }

    public override void _Process(double delta)
    {
        _tickLabel.Text = $"tick: {_driver.Sim.TickCount}";
    }

    private void BuildGroundSlab()
    {
        var body = new StaticBody3D { Name = "Ground" };
        var shape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(100, 1, 100) },
        };
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(100, 1, 100) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.45f, 0.5f, 0.42f),
            },
        };
        body.AddChild(shape);
        body.AddChild(mesh);
        body.Position = new Vector3(0, -0.5f, 0); // 地表位于 y=0
        AddChild(body);
    }

    private void AddSun()
    {
        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50, -30, 0),
            ShadowEnabled = true,
        };
        AddChild(sun);
    }
}
```

- [ ] **Step 4: 组装 Main.tscn**

`game/scenes/Main.tscn`:
```
[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://scripts/Main.cs" id="1"]
[ext_resource type="Script" path="res://scripts/SimDriver.cs" id="2"]

[node name="Main" type="Node3D"]
script = ExtResource("1")

[node name="SimDriver" type="Node" parent="."]
script = ExtResource("2")

[node name="UI" type="CanvasLayer" parent="."]

[node name="TickLabel" type="Label" parent="UI"]
offset_left = 12.0
offset_top = 8.0
offset_right = 240.0
offset_bottom = 34.0
text = "tick: 0"
```

在 `game/project.godot` 的 `[application]` 节设置主场景（编辑器里 Project Settings → Run → Main Scene 选 `res://scenes/Main.tscn`，或手动加一行）：
```
run/main_scene="res://scenes/Main.tscn"
```

- [ ] **Step 5: 手动验证**

Run: 在 Godot 编辑器按 F5（或 `<godot.exe路径> --path game`）
Expected: 灰绿色地面 + 左上角 tick 数字以每秒约 20 的速度递增。同时确认 `dotnet build` 在仓库根仍然成功。

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(game): godot project with SimDriver fixed-timestep bridge"
```

---

### Task 7: 灰盒第三人称角色控制器

**Files:**
- Create: `game/scripts/PlayerController.cs`
- Modify: `game/scripts/Main.cs`（组装玩家）

**Interfaces:**
- Consumes: 无（独立于仿真）
- Produces: `PlayerController : CharacterBody3D`，场景树内名为 `Player`；其子节点 `Yaw/SpringArm3D/Camera3D` 是后续 BuildController 取相机的路径。移动：WASD 相对相机朝向，Space 跳跃，鼠标转视角，Esc 释放鼠标。

- [ ] **Step 1: 写控制器**

`game/scripts/PlayerController.cs`:
```csharp
using Godot;

public partial class PlayerController : CharacterBody3D
{
    [Export] public float Speed = 6.0f;
    [Export] public float JumpVelocity = 4.8f;
    [Export] public float MouseSensitivity = 0.003f;

    private Node3D _yaw = null!;
    private SpringArm3D _arm = null!;
    private float _pitch;

    public override void _Ready()
    {
        _yaw = GetNode<Node3D>("Yaw");
        _arm = GetNode<SpringArm3D>("Yaw/SpringArm3D");
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion
            && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw.RotateY(-motion.Relative.X * MouseSensitivity);
            _pitch = Mathf.Clamp(
                _pitch - motion.Relative.Y * MouseSensitivity, -1.2f, 0.5f);
            _arm.Rotation = new Vector3(_pitch, 0, 0);
        }
        if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var velocity = Velocity;
        if (!IsOnFloor())
            velocity.Y -= 9.8f * (float)delta;
        else if (Input.IsPhysicalKeyPressed(Key.Space))
            velocity.Y = JumpVelocity;

        // 灰盒阶段直接轮询物理键，正式 InputMap 归 M4 打磨
        var input = Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.W)) input.Y -= 1;
        if (Input.IsPhysicalKeyPressed(Key.S)) input.Y += 1;
        if (Input.IsPhysicalKeyPressed(Key.A)) input.X -= 1;
        if (Input.IsPhysicalKeyPressed(Key.D)) input.X += 1;

        var dir = (_yaw.GlobalBasis * new Vector3(input.X, 0, input.Y));
        dir.Y = 0;
        dir = dir.Normalized();

        velocity.X = dir.X * Speed;
        velocity.Z = dir.Z * Speed;
        Velocity = velocity;
        MoveAndSlide();
    }
}
```

- [ ] **Step 2: 在 Main.cs 里组装玩家**

在 `Main._Ready()` 末尾加 `AddChild(BuildPlayer());`，并追加方法：

```csharp
    private static PlayerController BuildPlayer()
    {
        var player = new PlayerController { Name = "Player" };
        player.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.8f },
            Position = new Vector3(0, 0.9f, 0),
        });
        player.AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.4f, Height = 1.8f },
            Position = new Vector3(0, 0.9f, 0),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.9f, 0.55f, 0.2f),
            },
        });
        var yaw = new Node3D { Name = "Yaw", Position = new Vector3(0, 1.5f, 0) };
        var arm = new SpringArm3D { Name = "SpringArm3D", SpringLength = 5.0f };
        arm.AddChild(new Camera3D { Name = "Camera3D", Current = true });
        yaw.AddChild(arm);
        player.AddChild(yaw);
        player.Position = new Vector3(0, 1.0f, 0);
        return player;
    }
```

- [ ] **Step 3: 手动验证**

Run: F5
Expected: 橙色胶囊可 WASD 跑动（方向随镜头）、Space 跳跃、鼠标环绕视角且 SpringArm 遇地面不穿插、Esc 切换鼠标捕获。角色不会从地面边缘外掉入虚空时卡死（掉下去属正常，重开即可）。

- [ ] **Step 4: Commit**

```powershell
git add game
git commit -m "feat(game): gray-box third-person character controller"
```

---

### Task 8: 网格放置交互闭环（幽灵预览 → 指令 → 事件 → 视图）

**Files:**
- Create: `game/scripts/BuildController.cs`
- Modify: `game/scripts/Main.cs`（组装 BuildController）

**Interfaces:**
- Consumes: `SimDriver.Sim`、`SimDriver.SimEventEmitted`、`Simulation.CanPlace`、`PlaceStructureCommand`/`RemoveStructureCommand`、玩家相机 `Player/Yaw/SpringArm3D/Camera3D`
- Produces: 完整的 M1 建造体验：屏幕中心射线指向地面/方块 → 幽灵方块吸附网格（可放=半透明蓝，不可放=半透明红）→ 左键放置、右键拆除 → 方块视图由仿真事件驱动生成/销毁（`Dictionary<int, MeshInstance3D>` 以 StructureId 为键）

- [ ] **Step 1: 写 BuildController**

`game/scripts/BuildController.cs`:
```csharp
using Godot;
using SimCore;
using System.Collections.Generic;

public partial class BuildController : Node3D
{
    private SimDriver _driver = null!;
    private Camera3D _camera = null!;
    private MeshInstance3D _ghost = null!;
    private StandardMaterial3D _ghostOk = null!;
    private StandardMaterial3D _ghostBad = null!;
    private readonly Dictionary<int, MeshInstance3D> _views = new();
    private GridPos? _aimedCell;
    private GridPos? _aimedExistingCell;

    private const float RayLength = 8.0f;

    public override void _Ready()
    {
        _driver = GetNode<SimDriver>("../SimDriver");
        _camera = GetNode<Camera3D>("../Player/Yaw/SpringArm3D/Camera3D");
        _driver.SimEventEmitted += OnSimEvent;

        _ghostOk = MakeGhostMaterial(new Color(0.3f, 0.7f, 1.0f, 0.4f));
        _ghostBad = MakeGhostMaterial(new Color(1.0f, 0.25f, 0.2f, 0.4f));
        _ghost = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = Vector3.One },
            MaterialOverride = _ghostOk,
            Visible = false,
        };
        AddChild(_ghost);
    }

    public override void _ExitTree() => _driver.SimEventEmitted -= OnSimEvent;

    private static StandardMaterial3D MakeGhostMaterial(Color color) => new()
    {
        AlbedoColor = color,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
    };

    public override void _PhysicsProcess(double delta)
    {
        UpdateAim();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } click)
            return;
        if (click.ButtonIndex == MouseButton.Left && _aimedCell is { } cell
            && _driver.Sim.CanPlace(cell))
            _driver.Sim.EnqueueCommand(new PlaceStructureCommand("base_block", cell));
        if (click.ButtonIndex == MouseButton.Right && _aimedExistingCell is { } target)
            _driver.Sim.EnqueueCommand(new RemoveStructureCommand(target));
    }

    private void UpdateAim()
    {
        var from = _camera.GlobalPosition;
        var to = from + -_camera.GlobalBasis.Z * RayLength;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);

        if (hit.Count == 0)
        {
            _ghost.Visible = false;
            _aimedCell = null;
            _aimedExistingCell = null;
            return;
        }

        var hitPos = (Vector3)hit["position"];
        var normal = (Vector3)hit["normal"];
        // 命中面向外偏移半格取放置格，向内偏移半格取被指向的已有格
        _aimedCell = ToCell(hitPos + normal * 0.5f);
        var inner = ToCell(hitPos - normal * 0.5f);
        _aimedExistingCell = _driver.Sim.CanPlace(inner) ? null : inner;

        var cell2 = _aimedCell.Value;
        _ghost.Visible = true;
        _ghost.GlobalPosition = CellCenter(cell2);
        _ghost.MaterialOverride = _driver.Sim.CanPlace(cell2) ? _ghostOk : _ghostBad;
    }

    private static GridPos ToCell(Vector3 world) => new(
        Mathf.FloorToInt(world.X),
        Mathf.FloorToInt(world.Y),
        Mathf.FloorToInt(world.Z));

    private static Vector3 CellCenter(GridPos cell) =>
        new(cell.X + 0.5f, cell.Y + 0.5f, cell.Z + 0.5f);

    private void OnSimEvent(SimEvent e)
    {
        switch (e)
        {
            case StructurePlaced placed:
                var view = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = Vector3.One },
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(0.15f, 0.15f, 0.18f),
                        EmissionEnabled = true,
                        Emission = new Color(0.2f, 0.6f, 1.0f),
                        EmissionEnergyMultiplier = 0.4f,
                    },
                };
                AddChild(view);
                view.GlobalPosition = CellCenter(placed.Position);

                var body = new StaticBody3D();
                body.AddChild(new CollisionShape3D
                {
                    Shape = new BoxShape3D { Size = Vector3.One },
                });
                view.AddChild(body);

                _views[placed.StructureId] = view;
                break;

            case StructureRemoved removed:
                if (_views.Remove(removed.StructureId, out var gone))
                    gone.QueueFree();
                break;
        }
    }
}
```

- [ ] **Step 2: 组装 + 准星**

`Main._Ready()` 末尾追加：
```csharp
        AddChild(new BuildController { Name = "BuildController" });
        var crosshair = new Label { Text = "+" };
        crosshair.SetAnchorsPreset(Control.LayoutPreset.Center);
        GetNode<CanvasLayer>("UI").AddChild(crosshair);
```
注意：`BuildController` 的 `_Ready` 依赖 `Player` 与 `SimDriver` 已在树上——`AddChild(BuildController)` 必须放在 `BuildPlayer()` 组装之后（保持在 `_Ready` 末尾即可）。

- [ ] **Step 3: 手动验证**

Run: F5
Expected:
1. 准星指向地面出现半透明蓝色幽灵方块，吸附整数网格
2. 左键放置：出现深色发光蓝边方块；对已占格幽灵变红且左键无效
3. 方块可堆叠（指向已有方块的顶面/侧面时幽灵出现在相邻格）
4. 右键指向已有方块将其拆除
5. 角色可以跳上放置的方块（有碰撞）
6. 左上角 tick 持续递增，放置/拆除不掉帧

- [ ] **Step 4: 回归确认**

Run: `dotnet test`
Expected: 全部 PASS（表现层改动不破坏 SimCore）

- [ ] **Step 5: Commit**

```powershell
git add game
git commit -m "feat(game): grid placement loop - ghost preview, commands, event-driven views"
```

---

## 月末完成定义

- `dotnet test` 全绿（≥11 个测试）
- 游戏可运行：角色在灰盒地面行走跳跃，放置/拆除发光方块，全程经由 SimCore 指令-事件流
- AGENTS.md / CREDITS.md / backlog.md 就位
- 每个任务一个 commit，主线始终可构建

## 明确不做（本月）

- Mixamo/正式角色模型（第 2 个月）
- 回路、能量、魔像、多方块拼装（M2/M3）
- InputMap 配置、手感打磨、任何 shader/素材引入（M4）
- 存档写盘 UI（本月只有内存内快照往返；写文件是 M5 的事）
