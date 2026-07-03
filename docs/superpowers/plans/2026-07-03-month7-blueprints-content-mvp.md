# 第 7 个月：M5 收尾——蓝图库、内容补足与 MVP 验收 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **开工前校准（必读）**：核对前六个月实际接口；本月以"MVP 完成定义"为唯一北极星，任何与验收无关的新点子一律进 backlog。

**Goal:** 符文蓝图库（写一次部署一支舰队）、核心上限、内容补足（≥10 模块/≥15 节点/3 层解锁全通）、无文本新手引导，最终对照规格逐条完成 MVP 验收。

**Architecture:** 蓝图库落在 SimCore（`SimCore/Blueprints/`）：蓝图 = 命名的 `CircuitGraphData` + 编译缓存（BlueprintId → CompiledCircuit，编译一次多机共享——规格 §5"指令数组静态绑定到蓝图 ID"在此兑现）。魔像存档从"内嵌 GraphJson"迁移为"引用 BlueprintId"（匿名刻录自动生成隐式蓝图，兼容旧档走版本迁移 v2→v3）。

**规格来源:** 设计文档 §5（蓝图库/核心上限）、§8（MVP 完成定义）、§9（测试策略）

## Global Constraints

- 同前全部铁律。新增（规格"蓝图更新是惰性且安全的"）：编译缓存以 BlueprintId 为键；**直接对单机刻录 = 立即 Reset（编辑者明确意图）；蓝图修改 = 引用者仅标记 `PendingUpdate`**，在 PC 回到程序入口（天然循环边界）时静默切换新指令流，崩溃态/手动 Reset 时也切换——禁止对舰队瞬间集体 Reset（半空中的运输魔像会僵死、载货的会拿着货重跑初始逻辑）

## 任务清单

### Task 1: 蓝图库（TDD）

**Files:** Create `SimCore/Blueprints/BlueprintLibrary.cs`；Modify 刻录指令族（`SaveBlueprintCommand(name, graph)`、`InscribeBlueprintCommand(golemId, blueprintId)`）、`SimCore/Persistence`（v3：蓝图表 + 魔像引用 BlueprintId；v2→v3 迁移）；Test `BlueprintTests.cs`

测试清单：
- `SaveBlueprint_CompilesOnce_SharedAcrossGolems`（两魔像刻同一蓝图，`CompiledCircuit` 引用相等）
- `EditBlueprint_MarksSubscribersPending_WithoutImmediateReset`（正在执行动作的引用者不被打断）
- `PendingSubscriber_SwapsAtLoopBoundary`（PC 回到入口时切换新指令流，货物与位置保留）
- `CrashedSubscriber_SwapsOnReset`
- `PendingUpdate_SurvivesSaveRoundTrip`（半切换状态入档往返）
- `DeleteBlueprintInUse_IsRejected`（错误码 `blueprint_in_use`）
- `V2Save_MigratesGolemGraphsToImplicitBlueprints`

UI：编辑器加"保存为蓝图/从蓝图加载"下拉 + 蓝图管理面板（重命名/删除/引用计数显示）。

Commit: `feat(blueprints): named circuit library with shared compilation`

### Task 2: 核心上限（TDD）

**Files:** Modify `SimCore/Progression`（活跃魔像上限：初始 2，Tier2 → 4，Tier4 → 8；超限的 SpawnGolem 拒绝，错误码 `core_limit`）；Tier4 = 蓝图库 + 核心上限 + 高级模块，门槛 20 碎片
- Test：`GolemSpawn_RejectedAtCoreLimit`、`Tier4_RaisesLimit`
- Commit: `feat(progression): core limit and tier4`

### Task 3: 内容补足（数据表工作）

**Files:** Modify `NodeCatalog`/`ModuleCatalog`/`RecipeCatalog`——补至规格量：
- 节点补至目标量：已有 16（月2 的 10 + 月3 的 2 个相对寻址传感 + 月6 的 4；测试专用节点不计），≥15 已达标——本月按体验缺口补 `data_arith`（加减乘）、`data_counter`（计数器，VM 局部状态）、`action_toggle_structure`（启停目标结构）、`sensor_detect_items`（探测范围物品数），共 20 个
- 模块补至 ≥10：已有 9 → 补 `module_beacon`（信标，可配置广播）、高级模块 `module_overclock_core`（Tier4：速度 ×2、能耗 ×3——给 Tier4 一个值得攒 20 碎片的理由）
- 每个新节点/模块：目录定义 + 编译器/VM 支持（如需新 OpCode）+ 单测 + 灰盒→正式模型（复用 M4 管线）+ 音效绑定
- 配方微调：保证三层解锁的碎片曲线可在 3-5 小时通完（试玩标定）

Commit 按节点/模块分批。

### Task 4: 无文本新手引导

**Files:** Create `game/scripts/TutorialDirector.cs`（状态机监听 SimEvent 推进阶段）+ 引导视觉（发光指引线、目标结构轮廓高亮、编辑器首次打开时预置半成品回路——缺一条连线，玩家补上即完成第一课）
- 阶段：①走到初始魔像旁 → ②打开编辑器 → ③补全预置回路并刻录 → ④魔像开始工作 → ⑤指引第一个魔力泉 → 结束（此后靠解锁树牵引）
- 规格约束：无叙事无文本——引导只用视觉语言（光线、高亮、图标），UI 提示仅限操作键位图标
- 验收：找一位没看过项目的朋友试玩，不提示的情况下 10 分钟内完成第一条采集回路

Commit: `feat(game): wordless tutorial director`

### Task 5: MVP 验收（对照规格逐条）

规格 §8 MVP 完成定义逐条验收，每条一个证据（测试或试玩录像）：

| 验收条 | 证据形式 |
|---|---|
| 给魔像编写采集程序 | 教学流程试玩录像 |
| 拼装出第一条自动产线 | `FullChain_RawToTruthShard` 常绿 + 试玩 |
| 用信号让两台魔像协作 | `CooperationTests` 常绿 + 试玩 |
| 解锁至少一层科技 | `ProgressionTests` + 试玩 |
| 存档退出再读档继续 | 迁移链测试 + 试玩（含关进程重开） |
| 新玩家无引导可完成上述 | 外部试玩者实测（≥2 人） |

附加工程验收：
- 性能冒烟：脚本化铺设 500 结构 + 8 魔像 + 满负荷产线，tick 耗时 < 10ms、帧率 ≥ 60（不达标 → 按规格预案上 MultiMesh/数据布局优化，先测量后动手）
- `dotnet test` 全绿；`grep -r "using Godot" SimCore/` 为空；CREDITS.md 与实际素材一一对应

Commit: `chore: mvp acceptance evidence and performance smoke`

### Task 6: 发布一个可分发的 Demo 构建

**Files:** Godot 导出预设（Windows），打包 zip 发给试玩者
- 图标/窗口标题/版本号 0.1.0；导出模板安装与一键导出脚本
- Commit: `chore: v0.1.0 demo export preset`

## 月末完成定义（= MVP 达成）

规格 §8 六条全过 + 性能冒烟达标 + 可分发 zip。达成后开香槟，然后回到设计文档 §2 的 backlog（植被枯萎、真理核心矩阵、月相魔力……）规划下一阶段。

## 明确不做（本月）

- Steam 页面/宣传（MVP 验证后再议）、多语言、任何 backlog 项
