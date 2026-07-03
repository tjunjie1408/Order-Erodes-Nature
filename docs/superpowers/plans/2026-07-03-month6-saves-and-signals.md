# 第 6 个月：M5 前半——存档落盘与信号系统 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **开工前校准（必读）**：核对前五个月实际接口后再动工；步骤级代码按本文档接口契约与测试清单在校准时补足。

**Goal:** 存档从内存快照升级为完整的文件存读档（槽位/自动存档/版本迁移骨架）；信号系统上线（信标、命名频道、信号节点、传感符文/触发水晶），实现多魔像协作。

**Architecture:** 信号子系统（`SimCore/Signals/`）按 ISP 独立：维护一张"频道 → 本 tick 值"的世界信号表（纯数据，双缓冲——本 tick 写入下 tick 可读，杜绝同 tick 读写顺序依赖，保确定性）；回路 VM 经新增 OpCode 读写表；其他子系统不引用 Signals 类型。存档文件层放表现侧（`game/scripts/SaveManager.cs`），SimCore 只负责快照的产生与恢复（既有职责不变）。

**规格来源:** 设计文档 §5（信号系统/多魔像协作）、§4（存档）、§8（M5）

## Global Constraints

- 同前全部铁律。新增：
  - 信号表双缓冲：写入在 tick N，可读在 tick N+1；同频道多写者取**最大值**（确定性合流规则，简单且可解释——玩家文档口径"信号强度取最强"）
  - 存档文件带 `Version` 字段；`FromJson` 遇低版本走迁移函数链（本月只需 v1→v2 一个真实迁移作为骨架验证）

## 任务清单

### Task 1: 信号表与双缓冲（TDD）

**Files:** Create `SimCore/Signals/SignalBoard.cs`；Modify `Simulation`（tick 末尾 `SignalBoard.Flip()`）；Test `SignalBoardTests.cs`

接口契约：
```csharp
public sealed class SignalBoard
{
    public void Write(int channel, double value);   // 写入后缓冲
    public double Read(int channel);                // 读前缓冲，未写过=0
    public void Flip();                             // tick 边界交换
}
```
测试清单：`WriteIsInvisibleUntilFlip`、`MultipleWriters_MaxWins`、`UnwrittenChannel_ReadsZero`、`Board_SurvivesSaveRoundTrip`

Commit: `feat(signals): double-buffered world signal board`

### Task 2: 信号节点与 OpCode（TDD）

**Files:** Modify `NodeCatalog`（+3 节点：`event_on_signal`(收到信号时,内联参数channel+阈值)、`data_read_signal`(读信号:channel→Number)、`action_send_signal`(发信号:channel,value)）、`OpCode`（+ReadSignal/SendSignal）、编译器（多事件入口支持：`CompiledCircuit` 增加 `List<(int channel, double threshold, int entryPc)> SignalEntries`；VM 空闲时若信号越阈值则从对应入口启动）、`CircuitVm`
- Test：编译器多入口用例 + VM 信号触发用例（`IdleVm_StartsAtSignalEntry_WhenChannelCrossesThreshold`、`RunningVm_IgnoresSignalEntries`——运行中不被抢占，MVP 简化规则）

Commit: `feat(circuits): signal nodes with multi-entry programs`

### Task 3: 传感符文与触发水晶（世界内信号实体）（TDD）

**Files:** Create `SimCore/Signals/` 下两个固定结构模块：`module_sensor_rune`（探测半径 3 格内物品/魔像数量，写入配置频道）、`module_trigger_crystal`（读配置频道，越阈值时使所属结构启停）；表现层配置面板（对准按 E：频道号 SpinBox + 阈值）
- Test：`SensorRune_WritesGolemCount`、`TriggerCrystal_HaltsStructure_BelowThreshold`
- 手动验收：不开回路编辑器，仅摆放符文+水晶实现"仓库满则暂停采集结构"（规格：红石式直觉自动化）

Commit: `feat(signals): sensor runes and trigger crystals`

### Task 4: 多魔像协作验收场景

**Files:** Test `SimCore.Tests/Machines/CooperationTests.cs`
- **机器可读验收**（规格 MVP 定义的"信号协作"条目）：魔像 A 采集并在满载时 `send_signal(1, cargo)`；魔像 B 空闲挂 `event_on_signal(1, 阈值8)` 前来接驳搬运。断言 5000 tick 内储存碑吞吐 > 单魔像基线
- 手动试玩同场景确认体验成立（含 M4 的视觉/音效反馈）

Commit: `test(sim): two-golem signal cooperation acceptance`

### Task 5: 存档落盘（槽位/自动存档/版本迁移）

**Files:** Create `game/scripts/SaveManager.cs`（`user://saves/slot_N.json`，F5 快存/F9 快读、每 5 分钟自动存档、损坏文件容错提示）、`game/scenes/SaveMenu.tscn`（极简：三槽位+时间戳）；Modify `SimCore/Persistence`（Version=2：加入信号表；写 v1→v2 迁移函数 + 迁移测试 `V1Save_MigratesAndLoads`）
- 注意：读档 = 重建 `Simulation` 实例 + 表现层全量重建视图（BuildController/GolemView 提供 `RebuildFromSim()`——遍历当前仿真状态重建全部视图，这也是修复视图与仿真漂移的通用工具）
- Test：迁移用例 + `Autosave_DoesNotStallTick`（存档在主线程序列化耗时 < 50ms 的量级验证，超了就先记录 backlog 而非本月优化）

Commit: `feat(save): file persistence with slots, autosave, migration chain`

### Task 6: Tier3 解锁接线

**Files:** Modify `SimCore/Progression`（Tier3 = 信号系统全套，门槛 10 碎片；信号相关模块/节点在未解锁时拒绝）
- Test：`SignalNodes_LockedUntilTier3`
- Commit: `feat(progression): tier3 gates signal system`

## 月末完成定义

- 存档：三槽位存读、自动存档、v1 旧档可迁移加载、读档后产线继续运转且视图一致
- 信号：纯实体级（符文+水晶）与回路级（三节点）两条路都可用；双魔像协作验收测试常绿
- `dotnet test` 全绿

## 明确不做（本月）

- 蓝图库、核心上限、内容补足、教学（第 7 个月）
