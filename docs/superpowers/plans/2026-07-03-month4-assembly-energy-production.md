# 第 4 个月：M3——多方块拼装、能量网络与生产体系 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **开工前校准（必读）**：本计划的接口按第 1-3 个月计划的产出推定。开工第一步核对 `Simulation`/`Structure`/魔像实际形态，尤其"结构"需要从单方块升级为多方块——若第 3 个月实际实现与推定不符，先修订本计划。步骤级代码在校准时按本文档的接口契约与测试清单补足。

**Goal:** 结构从单方块升级为多方块模块组装（连通性+能力聚合）；能量网络（魔力泉/导轨/按比例降速）；浮运轨道物品传输；转化法阵配方加工；解锁树雏形。

**Architecture:** 三个新子系统全部落在 SimCore、彼此按 ISP 隔离：**建造拓扑**（连通分量管理，产出"结构=模块集合+能力表"纯数据）、**能量网络**（独立图，产出"每网络满足率"表）、**物品/配方**（消费能力表与满足率）。任何子系统不引用另一子系统的类型，交互只经 `Simulation` 聚合的纯数据表（规格 §7 ISP 铁律）。

**规格来源:** 设计文档 §5（多方块拼装、能量系统、机器体系、物品与配方、进度）、§4（性能设计：拓扑只在建造/拆除 tick 重算）

## Global Constraints

- 同前全部铁律。本月新增：
  - 连通性/能量拓扑重算只发生在建造/拆除指令的那个 tick，用局部 BFS，绝不进每 tick 热路径
  - 单结构模块数上限 **64**（规格"几十格量级"的具体化；超限放置被拒绝，错误码 `structure_size_limit`）

## 子系统接口契约（校准时锁死签名，先按此实现）

```csharp
// —— 建造拓扑子系统（SimCore/Assembly/）——
// 模块类型目录（数据表）：module_base(基座)、module_harvest_prism(采集棱镜)、
//   module_capacity_crystal(容量水晶)、module_amplifier(增幅符文)、
//   module_inscription_slot(回路刻录槽)、module_hover_core(悬浮移动核心)
public sealed class StructureInfo          // 拓扑子系统的唯一输出（纯数据）
{
    public int StructureId;
    public List<GridPos> Cells;
    public Capabilities Caps;              // 聚合能力表
}
public struct Capabilities                 // 全平凡值
{
    public bool CanHarvest, CanMove, HasInscriptionSlot;
    public int CargoCapacity;              // 基础10 + 每容量水晶 +20
    public float SpeedMultiplier;          // 1.0 + 每增幅符文 +0.25
    public int EnergyDemandPerTick;        // 模块数 × 1
}
// 放置相邻模块 → 并入结构并重算 Caps；拆除 → 局部 BFS 判断是否分裂，
// 分裂产生新 StructureId，各自重算 Caps；魔像=含 hover_core 的结构，其 VM/货物挂在 StructureId 上

// —— 能量网络子系统（SimCore/Energy/）——
// 实体：魔力泉(源, 产出 20/tick)、能量导轨(传导)、结构经导轨邻接入网
public sealed class EnergyReport           // 能量子系统每 tick 的唯一输出（复用实例，零分配）
{
    // structureId → 满足率 [0,1]；不在表中 = 未接网 = 0
    public Dictionary<int, float> Satisfaction;
}
// 满足率 = min(1, 网络总供给 / 网络总需求)；影响：动作速度与配方进度 × 满足率
// （规格：降速不停机）。魔像未接网时按 0.2 倍速运转（"残余魔力"，避免开局死锁——
// 初始魔像必须在无能量网络时也能工作，这是开局体验的保底规则）

// —— 物品/配方子系统（SimCore/Items/）——
// 物品：glimstone(辉石,原料)、aether_dust(以太尘,原料)、
//       glim_ingot(辉锭)、aether_lens(以太透镜)、logic_matrix(逻辑矩阵)、
//       truth_shard(真理碎片)
// 配方（转化法阵，均需满足率>0，耗时按满足率缩放）：
//   glimstone×2 → glim_ingot (100 tick)
//   aether_dust×3 → aether_lens (150 tick)
//   glim_ingot×1 + aether_lens×1 → logic_matrix (200 tick)
//   logic_matrix×2 → truth_shard (400 tick)

// —— 解锁树（SimCore/Progression/）——
// UnlockState: 持有 truth_shard 数 + 已解锁层级集合；
// 层级门槛：Tier1(固定结构+浮运轨道)=2碎片, Tier2(转化法阵+运算节点+增幅)=5, 
// Tier3/4 归 M5。未解锁的模块/节点：放置/刻录指令被拒绝，错误码 "locked"
```

---

### Task 1: 建造拓扑——模块合并与分裂（TDD）

**Files:** Create `SimCore/Assembly/`（ModuleCatalog/StructureInfo/AssemblyTopology）；Modify `Simulation`（放置指令改走模块目录）；Test `AssemblyTopologyTests.cs`

测试清单（先全写成失败测试再实现）：
- `PlaceAdjacentModules_MergeIntoOneStructure`
- `Capabilities_AggregateFromModules`（基座+2容量水晶 → CargoCapacity=50）
- `RemoveBridgeModule_SplitsIntoTwoStructures_EachWithOwnCaps`
- `RemoveLeafModule_KeepsSingleStructure`
- `SplitUsesLocalBfs_DoesNotTouchDistantStructures`（拆 A 结构不改变 B 结构的 StructureId——锁增量性）
- `StructureExceeding64Modules_RejectsPlacement`
- `GolemIdentity_SurvivesModuleAddition`（给魔像加容量水晶，VM 不复位？——**否**：规格"模块变更一律复位 VM"，断言 Reset 发生且货物保留）

Commit: `feat(assembly): multi-block structures with merge/split topology`

### Task 2: 能量网络（TDD）

**Files:** Create `SimCore/Energy/`；Modify `Simulation`（魔力泉作为地图生成时布置的世界实体 + `PlaceConduitCommand`）；Test `EnergyNetworkTests.cs`

测试清单：
- `IsolatedStructure_HasZeroSatisfaction_GolemRunsAtFloorSpeed`
- `ConnectedToSpring_FullSupply_SatisfactionOne`
- `DemandExceedsSupply_ProportionalThrottle`（供20 需40 → 0.5）
- `TwoSeparateNetworks_IndependentSatisfaction`
- `RemovingConduit_SplitsNetwork_RecalculatedOnce`（重算只发生在拆除 tick）
- `HarvestDuration_ScalesInverselyWithSatisfaction`（0.5 满足率 → 40 tick 完成采集）

Commit: `feat(energy): mana networks with proportional throttling`

### Task 3: 浮运轨道物品传输（TDD）

**Files:** Create `SimCore/Items/`（RailSegment/ItemInTransit）；Test `RailTransportTests.cs`

设计：轨道为相邻格链；物品以"轨道段+段内进度 float"表示，每 tick 进度 += 速度×满足率；到达终点若邻接储存碑/法阵入口则移交，否则堵塞（后方物品排队不重叠）。测试清单：
- `ItemTraversesRail_AtSatisfactionScaledSpeed`
- `ItemsQueue_WithoutOverlap_WhenOutputBlocked`
- `RailEndAtStorage_DepositsItem`
- `BrokenRail_ItemsHalt_NoLoss`（拆中段，物品停在原地不消失）

Commit: `feat(items): hover-rail transport with blocking queues`

### Task 4: 转化法阵与配方（TDD）

**Files:** Create `SimCore/Items/RecipeCatalog.cs`、法阵状态并入 Assembly 的能力（`module_transmute_core` 模块赋予 CanTransmute）；Test `TransmutationTests.cs`

测试清单：
- `RecipeCompletes_AfterScaledDuration_ConsumingInputs`
- `MissingIngredient_Idles_WithoutConsuming`
- `OutputBlocked_HoldsFinishedItem_UntilSpace`
- `FullChain_RawToTruthShard`（集成：两条采集线+轨道+两级法阵 → 5000 tick 内产出 truth_shard——**M3 的机器可读验收**）

Commit: `feat(items): transmutation recipes and full production chain test`

### Task 5: 解锁树雏形（TDD）

**Files:** Create `SimCore/Progression/UnlockState.cs`；Modify `Simulation`（放置/刻录校验解锁；`SpendShardsCommand`）；Test `ProgressionTests.cs`

测试清单：`LockedModule_PlacementRejected`、`SpendShards_UnlocksTier_EmitsEvent`、`UnlockState_SurvivesSaveRoundTrip`

Commit: `feat(progression): shard-gated unlock tiers`

### Task 6: 表现层——模块建造、导轨、法阵、轨道物品（灰盒）

**Files:** Modify `BuildController`（建造栏扩展为模块列表 UI——底部热键栏 1-9；模块/导轨/轨道的灰盒视图；轨道上物品用 MultiMesh 或简单 MeshInstance 插值）；能量满足率的视觉初版：结构 emission 亮度 = 满足率（shader 参数，M4 再美化）

手动验收（M3 玩法验收）：铺一条"采集棱镜结构 → 浮运轨道 → 转化法阵 → 轨道 → 储存碑"的产线，接魔力泉供能，观察满载降速；断导轨看物品停驻、拆桥模块看结构分裂；攒 2 碎片解锁 Tier1。

Commit: `feat(game): gray-box production line building and views`

### Task 7: 存档扩展 + 回归

多方块结构/能量网/轨道物品/法阵进度/解锁状态全部入档往返（追加 SaveRoundTripTests 用例：`FullFactory_SurvivesRoundTrip_AndKeepsProducing`）。`dotnet test` 全绿 + 试玩回归。

Commit: `feat(sim): full factory state in save round-trip`

## 月末完成定义（= M3 验收）

- 从原料到真理碎片的全自动产线可搭建、可存档、断电降速、拆除分裂全部正确
- 三个新子系统互不引用（代码评审确认 + AGENTS.md 结构地图更新）
- 集成测试 `FullChain_RawToTruthShard` 与 `FullFactory_SurvivesRoundTrip` 常绿

## 明确不做（本月）

- 信号/蓝图库（M5）、一切美化（M4）、性能优化（除非试玩明显掉帧——先测量再动手）
