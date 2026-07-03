# 实现计划索引

规格来源：[../specs/2026-07-03-magic-automation-game-design.md](../specs/2026-07-03-magic-automation-game-design.md)

| 月 | 里程碑 | 计划 | 核心交付 |
|---|---|---|---|
| 1 | M0 + M1 前半 | [month1-skeleton-and-walking-slice](2026-07-03-month1-skeleton-and-walking-slice.md) | SimCore 骨架、存档往返、灰盒角色与网格建造 |
| 2 | M1 收尾 + M2 前半 | [month2-character-and-circuit-compiler](2026-07-03-month2-character-and-circuit-compiler.md) | 动画角色、回路图模型与编译器 |
| 3 | M2 收尾 | [month3-vm-golem-and-editor](2026-07-03-month3-vm-golem-and-editor.md) | 可挂起 VM、魔像、GraphEdit 编辑器、端到端采集 |
| 4 | M3 | [month4-assembly-energy-production](2026-07-03-month4-assembly-energy-production.md) | 多方块拼装、能量网络、轨道、法阵、解锁树 |
| 5 | M4 | [month5-presentation-upgrade](2026-07-03-month5-presentation-upgrade.md) | shader 套件、故障美学、音效、UI 主题 |
| 6 | M5 前半 | [month6-saves-and-signals](2026-07-03-month6-saves-and-signals.md) | 存档落盘、信号系统、多魔像协作 |
| 7 | M5 收尾 | [month7-blueprints-content-mvp](2026-07-03-month7-blueprints-content-mvp.md) | 蓝图库、内容补足、教学、MVP 验收 |

## 使用规则

1. **逐月执行**：只执行当前月的计划。执行方式见各计划头部（Codex / 子代理均可）。
2. **开工校准**：第 2 个月起，每月开工第一步是对照仓库实际代码核对该计划的 Interfaces 与文件路径。接口漂移 → 先修订计划再动工；**月度目标与验收标准不可降级**。步骤粒度越靠后越粗，校准时按计划内的接口契约与测试清单补足步骤级代码。
3. **范围纪律**：计划外的点子进 `docs/backlog.md`，不实现。
4. **月末**：跑该计划的"月末完成定义"清单，另加两项固定仪式：
   - **分配扫描**：用 Rider Heap Allocation Viewer（或 dotnet-counters 看 GC 频率）人工过一遍 `Simulation.Tick()` 调用树，抓 AI 留下的隐式分配（接口枚举器装箱、struct 转接口装箱、字符串插值）
   - **边界扫描**：`grep -r "using Godot" SimCore/` 为空；接口契约文件与上月相比无未审批 diff

   全过才进入下一月。
5. **喂料纪律（第 4 个月起强制）**：给 Codex 的任务只含当前子系统代码 + 相邻子系统接口契约，绝不含相邻子系统实现源码——防上下文污染导致的幻觉 API 与偷改接口。
