# 第 5 个月：M4——表现升级（shader、故障美学、音效、UI 主题）实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **开工前校准（必读）**：本月工作对象是前四个月的实际产出，视觉基准以届时的真实场景为准。本计划锁定的是**交付物清单、调色板契约与验收标准**；shader 具体代码与数值由校准时补足。规格明示：Codex 擅长 shader 算法、不擅长调视觉参数——所有数值调优（Glow 强度、颜色曲线、扭曲幅度）由开发者亲自完成，计划步骤已按此分工拆分。

**Goal:** 把灰盒游戏升级到设计文档的目标画风：《秩序侵蚀自然》对比美学——自然大陆（免费素材）+ 冷发光几何巨构（自制）+ 故障美学 + 音效 + UI 主题。

**Architecture:** 视觉系统全部落在 `game/`，SimCore 零改动（唯一例外：若缺事件则补发事件）。建立三个可复用资产层：`game/shaders/`（shader 套件）、`game/theme/`（调色板常量与 UI Theme 资源）、`game/audio/`（事件→音效绑定表）。所有视觉绑定走"监听 SimEvent → 驱动 shader 参数/粒子/音效"的既有模式。

**规格来源:** 设计文档 §2（美术主题/氛围机制）、§6（素材策略/统一纪律）

## Global Constraints

- 同前全部铁律。本月新增（来自规格 §6 统一纪律，写死为资产层契约）：
  - **调色板锁死**：自然界暖色 ≤8 色；巨构黑白灰 + 冷光色唯一 `#4FC3F7`（冰蓝）+ 警示色唯一 `#FF1744`（猩红）。全部定义为 `game/theme/Palette.cs` 常量，任何材质/UI/粒子禁止硬编码其他颜色
  - 外来素材进场先"洗"：重着色进调色板、统一走基础材质
  - 每个素材入场即记 `CREDITS.md`，只收 CC0/明确可商用

## 任务清单

### Task 1: 调色板与主题基建

**Files:** Create `game/theme/Palette.cs`（全部颜色常量）、`game/theme/main_theme.tres`（Godot Theme：字体、按钮、面板、GraphEdit 配色——高对比网格、深底、冰蓝主色、猩红仅用于错误）
- 字体：思源黑体/Noto Sans SC（SIL OFL，中文支持）+ JetBrains Mono（回路编辑器数字/标签，SIL OFL），记入 CREDITS.md
- 全项目 UI 应用 theme；回路编辑器的连线颜色改为：Exec=白、Number=冰蓝、Bool=琥珀、Vector=青绿（琥珀/青绿从 8 色自然带选取并写进 Palette）
- 验收：全 UI 无默认灰色 Godot 皮肤残留；Commit `feat(theme): locked palette and ui theme`

### Task 2: 自然大陆场景升级

**Files:** Modify 地形构建；Create `game/assets/nature/`
- 用 Kenney Nature Kit / Quaternius 低多边形套件替换灰绿平板：起伏地形网格（Blender 或 Godot 内雕刻一块 200×200 手工地形，非程序生成）、树/岩石/草散布（用 Godot 的 MultiMeshInstance3D scatter）、魔力泉的自然形态（发光水池 + 上升粒子）
- 洗素材：全部模型重着色进 8 色自然带，统一 flat 材质
- WorldEnvironment 第一版：天空渐变、环境光、轻雾（数值开发者调）
- 验收：截图对比设计文档"温暖低多边形自然"的意图成立；Commit `feat(game): nature terrain with washed cc0 assets`

### Task 3: 巨构模块建模（开发者 Blender 周 + 落地）

**Files:** Create `game/assets/monolith/*.glb`（每模块一个：基座/采集棱镜/容量水晶/增幅符文/刻录槽/悬浮核心/导轨/轨道/法阵/储存碑/魔力泉汲取口/魔像本体）
- 建模纪律（规格 §6）：只用方/柱/锥 + 倒角，禁止有机曲面，每模块 ≤500 面，黑曜石/白混凝土两套基色 + 发光槽面（emission 材质槽留给 shader）
- 本任务主体是**开发者亲自的 Blender 工作**（第一周教程 + 逐个建模）；Codex 负责导入管线脚本（glb → 统一材质替换 → 碰撞生成）与 BuildController 视图替换
- 验收：所有灰盒方块被正式模块模型替换，悬浮模块带程序动画（自旋/浮动，第 3 个月 GolemView 模式推广）；Commit 按模块分批

### Task 4: 冷发光 shader 套件

**Files:** Create `game/shaders/`：`emission_pulse.gdshader`（能量脉冲，满足率驱动亮度——消费 M3 的 EnergyReport 绑定）、`fresnel_rim.gdshader`（边缘光）、`rune_flow.gdshader`（导轨/轨道的流动符文纹理，UV 滚动）、`hologram_ghost.gdshader`（幽灵预览升级：全息网格感）
- 分工：Codex 按参考（godotshaders.com，注明许可）写算法初稿 + 暴露 uniform；开发者调全部数值
- WorldEnvironment 第二版：Glow 开启（发光几何的质感来源）、体积雾、色调映射（开发者调）
- 验收：产线运转时导轨流光、机器随满足率明暗呼吸、巨构与自然的冷暖对比成立；Commit `feat(shaders): cold emission suite`

### Task 5: 故障美学（VmCrashed 的视觉化）

**Files:** Create `game/shaders/glitch_corruption.gdshader`（乱码噪点侵蚀表面——逐实体材质，安全）、`game/shaders/space_distortion.gdshader`（引力透镜扭曲——**规格实现红线：全局唯一后处理 Pass**，禁止逐实体挂屏幕空间材质（30 台同时崩溃 = 30 层全屏采样的 Overdraw 灾难）；崩溃坐标经 `uniform vec3 crash_positions[16]` + `uniform int crash_count` 传入，按距相机排序截断，一次全屏计算全部透镜）、`game/scripts/CrashVfx.cs`（监听 `VmCrashed`/`Reset` 事件：维护崩溃坐标数组喂给全局 Pass + 逐实体挂 glitch_corruption）
- 编辑器侧：崩溃时 CrashPc 对应节点持续猩红闪烁 + 错误说明（"回路陷入无限递归"）
- 验收：刻录死循环回路 → 魔像悬停原地、表面乱码、空间扭曲、编辑器定位崩溃节点；Reset 后恢复；Commit `feat(vfx): rational collapse glitch aesthetics`

### Task 6: 音效系统

**Files:** Create `game/audio/AudioDirector.cs`（事件→音效绑定表 + AudioBus 布局：Master/SFX/Ambient）、`game/assets/audio/`
- 采集：Sonniss GDC 包 + Freesound(CC0) + Kenney——机器嗡鸣（低频 loop，音量随满足率）、水晶泛音（采集完成）、机械键盘式 UI 音（节点吸附/连线/刻录——规格"笃定感"）、故障音（崩溃时的数字撕裂声）、环境底（风 + 远处低鸣）
- 绑定模式：`AudioDirector` 监听 `SimDriver.SimEventEmitted` 查表播放，3D 音效挂在对应视图位置
- 验收：戴耳机试玩十分钟，无一操作是哑的、无一音效刺耳重复；Commit `feat(audio): event-driven sfx and ambient beds`

### Task 7: 手感打磨与 InputMap 正式化

**Files:** Modify `project.godot`（正式 InputMap 替换物理键轮询）、`PlayerController`/`BuildController`（读 action）
- 打磨清单（逐项过，每项开发者验收）：建造格吸附的视觉毫不含糊、编辑器连线吸附半径手感、相机碰撞不穿墙、放置/拆除的 60fps 无感延迟确认、Esc 层级（编辑器→鼠标释放→暂停）
- 验收：设计文档 §2"交互反馈笃定、无 Q 弹动画、零拖泥带水"逐条对照；Commit `feat(game): input map and interaction polish`

## 月末完成定义（= M4 验收）

- 给一个没见过项目的人看 3 分钟试玩录像，能准确说出"温暖自然 vs 冷酷几何"的主题
- 产线运转有流光、呼吸、音效；崩溃有故障奇观；全程无灰盒残留
- `dotnet test` 全绿（本月不应有 SimCore 改动，除补发事件外）

## 明确不做（本月）

- 新玩法机制一律不加（信号/蓝图归 M5）；音乐采购（M5 末）；性能优化仍以测量为前提
