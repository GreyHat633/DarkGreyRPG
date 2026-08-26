# DarkGrey_RPG Studio 2.1 开发 Plan

> **项目根目录**：`E:\Java\MinecraftMod\DarkGrey_RPG`  
> **目标版本**：DarkGrey RPG Studio **2.1**  
> **技术栈**：C# + .NET 10 + WPF + XAML  
> **Minecraft Runtime**：Java + Forge 1.7.10  
> **NPC 后端**：CustomNPC+  
> **本轮定位**：把 2.0 从“Actor 管理器原型”升级为真正可用于 RPG 内容制作的“剧情驱动编辑器”。  
> **范围限制**：不开发 Native NPC、Boss、技能、Cutscene；不恢复 CNPC 式 `NPC → Dialogue → Quest` 嵌套模型。

---

## 1. 2.1 核心目标

2.0 当前主要问题：

1. 左侧导航使用 `A / D / Q / S` 黑字蓝底占位图标，辨识度低。
2. Bottom Dock（输出 / 问题 / 调试器 / Minecraft）固定占用大量高度，无法自由拖拽和折叠。
3. 只有 Actor 可以真正编辑，Dialogue / Quest / Story 仍然是 Placeholder。
4. 顶部“新建项目 / 打开项目”按钮长期占据黄金区域，但实际不是高频操作。
5. 项目级平铺 Actor / Dialogue / Quest / Story，随着资源数量增加会失控。
6. 缺少跨剧情复用资源的正式语义。
7. 缺少剧情内部流程图。
8. 缺少项目级宏观剧情图谱。

2.1 正式信息架构：

```text
Project
└─ 剧情
   ├─ 概览
   ├─ 角色
   ├─ 对话
   ├─ 任务
   └─ 流程
```

其中：

- **Project**：一整个服务器或整合包的 RPG 内容工程，例如 `DarkGrey`。
- **剧情**：作者主要工作的顶层内容单元，例如“王城迷案”“酒馆委托”“王国线”“帝国线”。
- **角色 / 对话 / 任务**：由剧情组织和使用，但底层仍是项目级唯一资源。
- **流程**：当前剧情唯一真实的逻辑编排图。
- **剧情图谱**：由所有剧情内部流程自动推导的宏观关系图，只用于浏览和跳转，不负责编辑逻辑。

---

## 2. Project 的正式定义

Project 不是“一条任务”。

Project 是整个 RPG 内容工程：

```text
DarkGrey
├─ 王城迷案
├─ 酒馆委托
├─ 王国线
├─ 帝国线
├─ 矿井失踪事件
└─ ...
```

正常使用时，一个服务器的所有 RPG 内容通常属于同一个 Project。

因此：

- 保留“新建项目 / 打开项目”能力；
- 移入 `文件` 菜单；
- 删除当前顶部两个常驻大按钮；
- 启动时优先自动恢复 `last_project`；
- 只有没有有效上次项目时，才显示 Welcome / Open Project 页面。

建议文件菜单：

```text
文件
├─ 新建项目
├─ 打开项目
├─ 最近项目
├─ 打开项目目录
├─ 关闭项目
└─ 退出
```

---

## 3. 剧情的正式定义

名称保持：

> **剧情**

不要改名。

剧情是一个相对独立、可进入、可结束、可跳转到其它剧情的 RPG 内容单元。

每个剧情内部固定：

```text
概览
角色
对话
任务
流程
```

例如：

```text
DarkGrey > 王城迷案

[概览] [角色] [对话] [任务] [流程]
```

所有 Tab 必须是真实页面，不允许 Decorative Tab。

---

## 4. 对话、任务、剧情的职责边界

### 对话

负责：

> **说什么。**

Dialogue 只描述：

- 说话者
- 文本
- 玩家选项
- 对话出口

Dialogue 不能直接：

- StartQuest
- CompleteQuest
- GiveReward
- EnterStory

### 任务

负责：

> **玩家要完成什么。**

Quest 只描述：

- 目标
- 进度
- 完成状态

Quest 不负责：

- 谁发布任务
- 哪段对话开始任务
- 进入哪个剧情

### 剧情流程

负责：

> **什么时候发生什么。**

只有 `剧情 > 流程` 可以把：

- 角色
- 对话
- 任务
- 条件
- 分支
- 其它剧情

编排成完整逻辑。

---

## 5. 剧情概览

每个剧情至少包含：

```text
story_id
display_name
description
tags
entry_presentation
```

### 删除“剧情重要级别”

2.1 不存在 `importance_level`。

作者分类使用 Tags。

玩家进入剧情时看到什么，由明确配置决定。

### 进入展示

概览中提供：

```text
进入展示
────────────────

展示方式：
○ 无
○ 标题
○ 章节标题

上方文字：
[命运已发生变动]

主标题：
[王城迷案]

持续时间：
[4.0] 秒
```

如果 Runtime 暂时还没支持标题播放：

- 配置仍真实保存；
- UI 明确提示“当前 Runtime 尚未实现该展示”；
- 不允许假装已经可用。

---

## 6. 剧情驱动，但资源底层仍然项目级唯一

视觉上：

```text
王城迷案
├─ 角色
├─ 对话
├─ 任务
└─ 流程
```

底层不要真的把资源复制锁死在剧情目录。

正式模型：

```text
Project Resource Registry
├─ Actors
├─ Dialogues
├─ Quests
└─ Stories
```

每个 Actor / Dialogue / Quest：

- 拥有项目级唯一 ID；
- 拥有 `home_story_id`；
- 可以被其它剧情引用。

---

## 7. Home Story

每个：

- Actor
- Dialogue
- Quest

增加：

```text
home_story_id
```

例如：

```text
Actor:
detective

Home Story:
royal_city_mystery
```

在“王城迷案 > 角色”中显示为：

```text
本剧情
侦探
```

如果王国线引用：

```text
王国线 > 角色

引用
侦探    来自：王城迷案
```

---

## 8. 资源操作语义：创建、引用、导入

这是 2.1 必须严格实现的规则。

### 页面顶部只保留两个顶级动作

在：

- 角色
- 对话
- 任务

顶部统一：

```text
[+ 创建] [引用]
```

**“导入”不是与“引用”同级按钮。**

---

## 9. 引用

点击：

```text
引用
```

打开 Resource Picker。

用户从项目中选择一个已有资源。

结果：

```text
王城迷案 ─┐
           ├─→ Actor: detective
王国线 ───┘
```

引用的语义：

- 同一个 Resource ID；
- 同一份持久数据；
- 任意地方修改后，所有引用处同步更新。

> **引用 = Link**

---

## 10. 创建 → 导入

点击：

```text
+ 创建
```

打开次级 UI：

```text
创建角色

○ 空白创建
○ 导入已有角色

[取消] [继续]
```

Dialogue / Quest 同理。

“导入”的语义：

> 以旧资源作为模板，创建一个全新的独立资源。

例如：

```text
原：
imperial_guard

导入后：
capital_elite_guard
```

必须：

- 新 Resource ID；
- 新文件；
- `home_story_id = 当前剧情`；
- 后续修改互不影响。

> **创建 → 导入 = Copy as New**

---

## 11. 引用与导入的 UI 区分

引用资源在列表中显示：

```text
侦探
detective
↗ 来自：王城迷案
```

导入后生成的新资源则归入：

```text
本剧情
```

导入界面必须明确说明：

```text
将创建一份新的独立资源。
后续修改不会影响原资源。
```

---

## 12. 剧情内资源页统一结构

例如：

```text
王国线 > 角色

本剧情
────────────────
国王
王国将军

引用
────────────────
侦探          来自：王城迷案
宰相          来自：王城迷案

[+ 创建] [引用]
```

Dialogue / Quest 使用相同结构。

---

## 13. 删除与引用完整性

如果资源仍被其它剧情引用，禁止直接删除。

例如：

```text
无法删除“侦探”

该角色仍被以下剧情引用：

• 王国线
• 帝国线

[查看引用]
[取消]
```

2.1 默认不提供“一键强制从所有剧情删除”。

必须先解除引用，再删除。

---

## 14. ProjectResourceRegistry

Core 中新增正式：

```text
ProjectResourceRegistry
```

至少管理：

```text
ActorRegistry
DialogueRegistry
QuestRegistry
StoryRegistry
```

至少提供：

```text
GetById
Exists
GetReferences
GetHomeStory
Create
ImportAsNew
AddReference
RemoveReference
CanDelete
```

UI 不直接遍历文件系统猜关系。

---

## 15. Dialogue Editor：2.1 必须可编辑

2.1 不允许 Dialogue 继续只是 Placeholder。

最低数据：

```text
dialogue_id
display_name
home_story_id
notes
nodes
```

最低 Node：

```text
Line
Choice
End
```

### UI 目标

不要 CNPC 式“一句一个弹窗”。

示例：

```text
对话：最终质询

[侦探]
证据已经足够了。

[玩家选择]
├─ 交出证物
│   Exit: hand_over
└─ 隐瞒证物
    Exit: conceal

[+ 台词]
[+ 选择]
```

必须支持：

- 新建 Line
- 新建 Choice
- 删除
- 排序
- 编辑 Speaker
- 编辑文本
- 编辑 Choice Text
- 编辑 Exit Name
- Save
- Dirty
- Undo / Redo

Dialogue 只返回命名出口，例如：

```text
hand_over
conceal
refuse
```

流程决定后续逻辑。

---

## 16. Quest Editor：2.1 必须可编辑

2.1 不允许 Quest 继续只是 Placeholder。

最低数据：

```text
quest_id
display_name
description
home_story_id
objectives
```

2.1 最低 Objective：

```text
KillEntity
CollectItem
InteractActor
```

示例：

```text
任务：搜集案件证物

描述：
调查案件并取得关键证物。

目标
────────────────
1. 与侦探交谈
2. 收集“染血的徽章” ×1

[+ 添加目标]
```

必须具备：

- CRUD
- Dirty
- Validation
- Save
- Undo / Redo

---

## 17. Flow：2.1 核心编辑器

每个剧情内部只有一套：

> **流程**

它是这个剧情唯一真实的逻辑 Source of Truth。

最低节点类型：

### Entry / Trigger

```text
剧情开始
角色交互
```

### Dialogue

```text
播放对话
```

### Quest

```text
开始任务
等待任务完成
```

### Branch

```text
按 Dialogue Exit 分支
```

### Story

```text
进入剧情
结束当前剧情
```

### End

```text
结束
```

---

## 18. Flow Graph UI

目标是成熟节点编辑器式操作，但不要复制 Godot 外观。

必须支持：

- 平移
- 缩放
- 节点拖拽
- 端口连接
- 框选
- Delete
- Ctrl+C / Ctrl+V
- Ctrl+Z / Ctrl+Y
- 节点视觉位置持久化
- Resource Picker
- 连接校验

示例：

```text
[剧情开始]
      ↓
[开始任务：搜集案件证物]
      ↓
[等待任务完成]
      ↓
[角色交互：侦探]
      ↓
[播放对话：最终质询]
      ↓
[按 Dialogue Exit 分支]
     ↙                  ↘
 hand_over            conceal
    ↓                    ↓
[进入剧情]            [进入剧情]
 王国线                帝国线
```

---

## 19. 跨剧情连接

跨剧情关系唯一通过当前剧情 Flow 中的：

```text
进入剧情：王国线
进入剧情：帝国线
```

产生。

不要建立第二套“项目级可编辑剧情流程”。

---

## 20. 剧情图谱

Project 首页提供：

```text
[剧情列表] [剧情图谱]
```

剧情图谱自动扫描所有剧情 Flow 中的：

```text
EnterStory
```

推导：

```text
王城迷案 → 王国线
王城迷案 → 帝国线
```

显示：

```text
              王国线
             ↗
王城迷案
             ↘
              帝国线
```

---

## 21. 剧情图谱的编辑边界

剧情图谱：

> **逻辑只读，布局可编辑。**

禁止：

- 手工从剧情 A 拉线到剧情 B；
- 删除逻辑边；
- 添加出口；
- 修改进入条件；
- 通过剧情图谱产生 Flow 逻辑。

允许：

- 拖动剧情节点；
- 平移；
- 缩放；
- 搜索；
- 标签过滤；
- 自动布局；
- 保存节点视觉位置。

视觉位置属于 Editor Metadata，不影响 Runtime。

---

## 22. 剧情图谱点击跳转

点击 / 双击：

```text
王国线
```

必须直接打开：

```text
DarkGrey > 王国线 > 流程
```

不经过额外详情页。

这是剧情图谱最重要的交互。

---

## 23. 不做剧情图谱编辑快捷方式

明确不实现：

```text
右键 → 添加剧情出口
右键 → 连接剧情
```

等快捷写逻辑功能。

剧情图谱仅：

> 浏览 / 理解 / 导航

---

## 24. 剧情图谱诊断

可以自动检测：

### Missing Story Reference

Flow 指向不存在剧情。

### Isolated Story

没有其它剧情进入，且自身也不是明确独立入口。

### Possible Story Cycle

存在环路。

注意：

- Cycle 只做 Warning；
- 不要把所有循环判定为错误。

---

## 25. UI：左侧导航图标

删除当前：

```text
A
D
Q
S
```

黑字 + 蓝底占位图标。

替换为高辨识度矢量图标。

因为 2.1 改为剧情驱动，主导航建议简化为：

```text
剧情
设置
```

项目级“角色 / 对话 / 任务”不再作为长期主入口。

角色、对话、任务只在进入某个剧情后出现。

Icon 要求：

- WPF Geometry / Path；
- 或合法可用的 Fluent 风格 icon；
- 单色；
- Normal 深灰；
- Selected 使用 Accent；
- Hover 使用浅背景；
- 不再用字母替代 icon。

---

## 26. Bottom Dock 重构

当前固定：

```text
输出 / 问题 / 调试器 / Minecraft
```

必须改成：

> **可拖拽、可折叠、可记忆高度的 Bottom Dock**

WPF 结构：

```text
Main Workspace
GridSplitter
Bottom Dock
```

必须支持：

- 向上拖动增高；
- 向下拖动缩小；
- 最小只剩 Tab Header；
- 点击当前活动 Tab 再次折叠；
- 双击 Tab Bar 折叠 / 恢复；
- 记忆上次高度；
- Problems 有错误时显示 Badge，但不强制展开。

首次启动默认：

- Collapsed；
- 或较小高度（约 120px）。

不要默认占 1/3 屏幕。

---

## 27. 顶部项目区重构

删除当前常驻大按钮：

```text
[新建项目]
[打开项目]
```

项目管理移动到 `文件` 菜单。

顶部只保留必要信息：

```text
DarkGrey RPG Studio
DarkGrey
```

以及可选：

```text
Minecraft ● Offline
```

项目路径放到 Tooltip / Project Menu，不必永久占据大面积顶部空间。

---

## 28. Project Home

启动并自动恢复 DarkGrey Project 后，直接进入：

```text
剧情
```

空项目显示：

```text
还没有剧情

剧情是 DarkGrey RPG 中的主要创作单元。

[创建第一个剧情]
```

不要默认打开 Actor 页面。

---

## 29. Theme 入口

当前右上角常驻：

```text
主题 Light
```

2.1 建议移动到：

```text
视图 → 主题
```

或 Settings。

允许保留一个小型快捷主题按钮，但不能占用主工作区。

---

## 30. 响应式布局

继续使用 WPF：

- Grid
- Auto
- *
- MinWidth
- GridSplitter
- ScrollViewer

禁止主布局依赖固定像素坐标。

至少测试：

```text
1920×1080
1600×900
1440×900
1366×768
```

以及 Windows 125% DPI。

---

## 31. Resource Picker

“引用”统一使用 Resource Picker。

例如：

```text
引用角色

搜索：
[侦探________]

来源剧情
────────────────
王城迷案
  侦探

酒馆委托
  酒馆老板
```

支持：

- Search
- Filter by Story
- Home Story
- Resource ID
- 已引用标记

不要只显示文件路径。

---

## 32. 创建 → 导入 Picker

导入页面复用 Picker，但明确：

```text
从已有角色导入

将创建一份新的独立角色。
后续修改不会影响原角色。
```

必须避免用户把“导入”理解成“引用”。

---

## 33. 文件与 Registry

虽然 UI 上剧情驱动，物理文件优先仍采用：

```text
project.json
stories/
actors/
dialogues/
quests/
```

不要为了 UI 层级强行变成：

```text
stories/王城迷案/actors/...
```

除非审计后证明必要。

Project Registry 是事实来源。

---

## 34. Story Membership Schema

建议语义：

```json
{
  "schema_version": 2,
  "id": "royal_city_mystery",
  "display_name": "王城迷案",
  "description": "",
  "tags": [],
  "owned_resources": {
    "actors": ["detective"],
    "dialogues": ["final_interrogation"],
    "quests": ["collect_evidence"]
  },
  "referenced_resources": {
    "actors": [],
    "dialogues": [],
    "quests": []
  },
  "flow_ref": "royal_city_mystery"
}
```

实际字段命名可以调整，但语义必须保持。

---

## 35. Resource Schema

Actor 示例：

```json
{
  "schema_version": 2,
  "id": "detective",
  "display_name": "侦探",
  "home_story_id": "royal_city_mystery"
}
```

Dialogue / Quest 同理。

---

## 36. 2.0 → 2.1 Migration

2.0 已存在 Actor，但没有 `home_story_id`。

2.1 打开旧项目时不得丢资源。

推荐：

自动创建：

```text
未分类
```

剧情。

把旧 Actor 迁移到：

```text
未分类 > 角色
```

如果未来已有 Dialogue / Quest，也采用同样策略。

迁移必须：

- 先备份；
- 可重复；
- 有日志；
- 测试；
- 失败时不破坏原项目。

---

## 37. Runtime 兼容

新增 `home_story_id` 等 Editor / Resource 字段后：

- `/dgrpg reload` 仍必须读取 Actor；
- CNPC Actor Binding 不受影响；
- Actor ID persistence 不受影响。

不要因为 2.1 UI/Schema 重构重写稳定的 CNPC Runtime。

---

## 38. Core 架构新增

建议新增：

```text
StoryResource
StoryRepository
ProjectResourceRegistry
ResourceReferenceService
ResourceImportService
StoryGraphAnalyzer
FlowGraph
FlowValidator
DialogueResource
QuestResource
```

---

## 39. ViewModel 新增

建议：

```text
ProjectHomeViewModel
StoryListViewModel
StoryWorkspaceViewModel
StoryOverviewViewModel
StoryActorsViewModel
StoryDialoguesViewModel
StoryQuestsViewModel
StoryFlowViewModel
StoryGraphViewModel
ResourcePickerViewModel
CreateResourceViewModel
```

不要把所有逻辑塞进 `ShellViewModel` 或 `MainWindow.xaml.cs`。

---

## 40. StoryGraphAnalyzer

确定性实现：

```text
for each Story:
    scan Flow nodes
    for each EnterStoryNode:
        add edge(source_story, target_story)
```

输出：

```text
StoryGraphSnapshot
├─ Nodes
├─ Edges
└─ Diagnostics
```

剧情图谱 UI 只消费 Snapshot。

---

# 41. 2.1 开发 Milestone

## M1 — UI Shell 修复

完成：

- 新高辨识度 icon；
- 删除 A/D/Q/S；
- Bottom Dock GridSplitter；
- Bottom Dock collapse / restore；
- 记忆 Dock 高度；
- 删除顶部 New/Open 大按钮；
- Project 自动恢复；
- Project 操作进入 File Menu。

Gate：

- UI 主框架在 1366×768 及以上可用；
- Bottom Dock 不再挡视线；
- 所有可点击控件有真实行为。

---

## M2 — Story 数据模型与迁移

完成：

- StoryResource；
- HomeStory；
- Owned / Referenced membership；
- ProjectResourceRegistry；
- 2.0 → 2.1 migration；
- “未分类”剧情迁移；
- 单元测试。

Gate：

- 旧 Actor 项目可安全打开；
- 重启后资源归属稳定；
- Runtime reload 不回归。

---

## M3 — 剧情驱动导航

完成：

```text
Project Home
├─ 剧情列表
└─ 剧情图谱（先可空）
```

进入剧情后：

```text
概览
角色
对话
任务
流程
```

全部真实切换。

Gate：

- 不存在“点了没反应”的 Tab；
- Project Home 不再默认展示全局 Actor 列表。

---

## M4 — 创建 / 引用 / 导入

先以 Actor 完整实现：

```text
+ 创建
  ├─ 空白创建
  └─ 导入已有角色

引用
```

完成：

- Reference
- ImportAsNew
- RemoveReference
- ViewReferences
- Delete protection

然后复用到 Dialogue / Quest。

Gate：

- 引用修改可同步；
- 导入后互不影响；
- 有引用的资源不能误删。

---

## M5 — Dialogue + Quest Editor

Dialogue：

- Line
- Choice
- End
- Named Exit

Quest：

- KillEntity
- CollectItem
- InteractActor

全部支持：

- Create
- Edit
- Save
- Dirty
- Validation
- Undo/Redo

Gate：

- Dialogue / Quest 不再是 Placeholder；
- Dialogue 不包含 Quest Action；
- Quest 不包含 Issuer NPC。

实施状态（2026-08-24）：**已通过**。

- Dialogue 已支持 Line / Choice / End / Named Exit，以及创建、编辑、保存、Dirty、Validation、Undo/Redo；旧 Jump 节点只做兼容保留。
- Quest 已支持 KillEntity / CollectItem / InteractActor，以及创建、编辑、保存、Dirty、Validation、Undo/Redo；旧 ReachLocation 目标只做兼容保留。
- Dialogue / Quest 复用 M4 的 Create / ImportAsNew / Reference / RemoveReference / ViewReferences / Delete protection 语义。
- Studio schema v2 增加 `display_name` 与 `home_story_id`；Java Runtime 同时兼容 schema v1/v2，且仍拒绝 Dialogue Quest Action 与 Quest Issuer NPC 等越界字段。
- Release 测试：Core 52/52、WPF 69/69；独立 Java Runtime probe 全部通过。
- 真实 WPF 验收：完成编辑、保存、磁盘校验、重启恢复、UI Automation 可访问性检查，并保存 Dialogue / Quest / restart 截图证据于 `.tooling/m5-ui-qa/screenshots/`。

---

## M6 — Story Flow Editor

实现节点：

```text
StoryStart
ActorInteract
PlayDialogue
StartQuest
WaitQuestComplete
DialogueExitBranch
EnterStory
EndStory
End
```

支持：

- Graph edit
- Pan/Zoom
- Connection
- Copy/Paste
- Undo/Redo
- Save layout
- Validation

Gate：

能够完整搭建“王城迷案”样例流程。

实施状态（2026-08-26）：**已通过**。

- Story Flow 已替换占位界面，成为可自由拖动节点、自由排列和端口连线的 WPF 节点画布。
- 已实现 StoryStart / ActorInteract / PlayDialogue / StartQuest / WaitQuestComplete / DialogueExitBranch / EnterStory / EndStory / End。
- 已实现框选、多选拖动、连接选择删除、复制/粘贴、撤销/重做、右键平移、滚轮/按钮缩放、布局持久化、资源选择与跨资源校验。
- 节点改名会同步更新连接；旧 Runtime 节点类型以原始类型无损保留。
- “王城迷案”最终验收工程已搭建 8 节点、7 连接，`hand_over` / `conceal` 分别进入“王国线”与“帝国线”。
- 测试：Core 56/56、WPF 73/73，Build 0 warning / 0 error。
- 真实 WPF 验收：节点拖拽、端口连线、框选、复制粘贴、Undo/Redo、Delete、保存、磁盘校验、重启恢复与 UI Automation 可访问性全部通过；证据位于 `.tooling/2.1-acceptance/screenshots/`。

---

## M7 — 剧情图谱

完成：

- 自动分析 Flow；
- 逻辑只读；
- 布局可编辑；
- Pan / Zoom；
- Search / Filter；
- Auto Layout；
- 点击/双击剧情 → 直接进入对应剧情 Flow；
- Missing target diagnostic；
- Isolated Story warning。

Gate：

Flow 删除 EnterStory 后，剧情图谱对应边自动消失。

实施状态（2026-08-26）：**已通过**。

- 剧情图谱已升级为自由布局节点画布，边只从 Story Flow 的 EnterStory 自动派生，界面不提供逻辑连线修改入口。
- 图谱节点支持拖动、右键平移、滚轮/按钮缩放、搜索、连接/孤立/警告筛选与分层自动布局。
- 节点按钮与双击可直接进入对应 Story Flow。
- Missing target 与 Isolated Story 诊断已进入侧栏和节点警告样式。
- 图谱位置只写入 `resources/editor/story-graph-layout.json`；真实 WPF 验收对全部 Story JSON 做前后 SHA-256 对比，确认逻辑文件零变化。
- Gate 集成测试通过：保存删除 EnterStory 后的 Flow，再从仓储重载剧情图谱，对应边消失。
- 测试：Core 56/56、WPF 77/77；真实 WPF 的布局保存/重启、搜索筛选、诊断、自动布局与直达 Flow 全部通过，截图位于 `.tooling/2.1-acceptance/screenshots/04-...` 至 `07-...`。

---

## M8 — Runtime Vertical Slice + 回归

至少实现数据层垂直闭环：

```text
王城迷案
→ 最终选择
→ 王国线 / 帝国线
```

如果完整 Minecraft Runtime 尚有差距：

- 明确列出 gap；
- 不得声称已完成；
- 继续补齐 2.1 验收所需部分。

回归：

- Actor Binding
- Actor persistence
- `/dgrpg reload`

实施状态（2026-08-26）：**已通过**。

- Java Runtime 已识别 StoryStart / DialogueExitBranch / EnterStory / EndStory，并保留旧节点拼写与旧 Dialogue Result 直连的兼容路径。
- PlayDialogue 会记录命名出口，DialogueExitBranch 按出口名选择端口，EnterStory 会结束源 Story、启动目标 Story 并在同一事件推进入口；转场深度有界。
- 新增 `/dgrpg story start <id>` 及补全；StoryLoader 会校验命名出口、资源引用、EnterStory 目标和 terminal 节点。
- `studio21VerticalSliceProbe` 已通过 `hand_over → kingdom_route` 与 `conceal → empire_route` 两条分支，并验证 StoryInstance 快照恢复。
- `studio21RegressionProbe` 的 20 项旧 Project / Dialogue / Quest / Story / Live probe 全部通过。
- `studio21ActorBindingProbe` 证明 Actor ID 写入 CustomNPC+ persistent stored data，包装器重建后仍可读取，并可正确解绑与通知客户端。
- 真实 Forge 1.7.10 dedicated server 已加载 2 Actor、1 Dialogue、1 Quest、4 Story 的最终验收工程；`/dgrpg reload`、Story list/info、干净停服全部通过。
- `gradlew build`（含 Spotless、编译、JAR）通过。M8 采用本节要求的“至少数据层垂直闭环”；本轮未把无记录的 Minecraft 客户端视觉点击冒充实机证据，边界详见 `docs/TESTING.md`。

---

# 42. 2.1 最终验收 Project

Codex 创建：

```text
DarkGrey 2.1 Acceptance
```

---

## Story A：王城迷案

### Actor

```text
侦探
```

### Dialogue

```text
最终质询
```

选项：

```text
交出证物 → hand_over
隐瞒证物 → conceal
```

### Quest

```text
搜集证物
```

### Flow

```text
Start
↓
Start Quest
↓
Wait Quest Complete
↓
Interact Detective
↓
Play Dialogue
↓
Dialogue Exit Branch
├ hand_over → Enter Story: 王国线
└ conceal   → Enter Story: 帝国线
```

---

## Story B：王国线

角色页：

```text
引用
→ 侦探
```

验收：

- 引用的是同一个 Resource；
- 在王国线修改侦探显示名称；
- 王城迷案同步看到同一变化。

---

## Story C：帝国线

角色页：

```text
创建
→ 导入已有角色
→ 侦探
```

创建：

```text
帝国线侦探
```

验收：

- 新 Resource ID；
- 新文件；
- 修改帝国线侦探不影响原侦探。

---

# 43. 剧情图谱验收

自动显示：

```text
              王国线
             ↗
王城迷案
             ↘
              帝国线
```

必须：

- 不允许手工连线；
- 不允许删除逻辑边；
- 节点可拖动；
- 重启后保留布局；
- 双击王国线直接进入 `王国线 > 流程`；
- 从王城迷案 Flow 删除 `EnterStory: 帝国线` 后，对应边自动消失。

---

# 44. UI 验收

### Icon

- [x] 无 A/D/Q/S 字母占位图标
- [x] 使用辨识度高的矢量 icon
- [x] Hover / Selected 清晰
- [x] 不裁字、不跳动

### Bottom Dock

- [x] 可自由拉伸
- [x] 可折叠
- [x] 可恢复
- [x] 记忆高度
- [x] 默认不遮挡主工作区

### Project

- [x] 顶部无大号 New/Open 按钮
- [x] File Menu 可新建/打开
- [x] 自动恢复上次项目

### Story

- [x] Project 首页主入口是剧情
- [x] 剧情列表可搜索
- [x] Story 内五个页面都真实可用
- [x] 剧情图谱可用

### Resource

- [x] Create
- [x] Create → Import
- [x] Reference
- [x] Home Story
- [x] Reference integrity
- [x] Delete protection

### Dialogue

- [x] 可编辑
- [x] Choice 支持 Named Exit
- [x] 不含 Quest Action

### Quest

- [x] 可编辑
- [x] Objective 可保存

### Flow

- [x] 可以连接 Actor / Dialogue / Quest / Story
- [x] Flow 是逻辑唯一 Source of Truth

---

# 45. 2.1 明确不做

- Native NPC
- Boss
- Combat Skill Editor
- Cutscene Timeline
- Camera Editor
- 项目级可编辑剧情 Flow
- 在剧情图谱中直接拉逻辑边
- 剧情图谱编辑快捷方式
- CNPC Quest GUI 兼容
- CNPC Dialogue GUI 兼容
- 多人协作
- 云同步
- Git UI
- 大型 Localization Editor

---

# 46. 禁止的错误实现

禁止：

1. 把剧情当真实磁盘文件夹，导致跨剧情复用只能复制。
2. `引用` 实际创建副本。
3. `导入` 实际仍链接原资源。
4. Dialogue 中写 StartQuest。
5. Quest 中写 Issuer NPC。
6. Actor 中写 Dialogue。
7. Story Graph 和 Story Flow 各保存一套逻辑边。
8. Story Graph 可直接创建剧情跳转。
9. Bottom Dock 继续固定高度。
10. 用字母替代正式 icon。
11. 创建可点击但无行为的按钮。
12. 只做 UI 不做真实 Save。
13. Schema 改了但 Runtime 不兼容。
14. 为了 2.1 重写稳定 CNPC Runtime。

---

# 47. 构建与测试规则

每个 Milestone：

1. 审计当前实现。
2. 明确最小改动路径。
3. 实现。
4. `dotnet build`
5. `dotnet test`
6. 手动 UI 验证。
7. 更新 docs。
8. Gate 不通过，不进入下一 Milestone。

Java Runtime 有改动时：

```text
gradlew build
```

必须通过。

---

# 48. 文档要求

2.1 至少新增/更新：

```text
docs/2.1_ARCHITECTURE.md
docs/2.1_RESOURCE_MODEL.md
docs/2.1_STORY_FLOW.md
docs/2.1_STORY_GRAPH.md
docs/2.1_UI_UX.md
docs/2.1_MIGRATION.md
docs/TESTING.md
docs/DECISIONS.md
```

---

# 49. 2.1 完成后的标准用户工作流

```text
启动 DarkGrey RPG Studio
↓
自动打开 DarkGrey Project
↓
剧情列表
↓
+ 新建剧情：王城迷案
↓
进入王城迷案
↓
角色
    + 创建侦探
↓
对话
    + 创建最终质询
↓
任务
    + 创建搜集证物
↓
流程
    把侦探、对话、任务连接
    最后进入王国线或帝国线
↓
回 Project
↓
剧情图谱自动显示：
王城迷案 → 王国线
         → 帝国线
↓
双击王国线
↓
直接进入王国线 > 流程
```

在王国线：

```text
角色
→ 引用
→ 侦探
```

得到同一资源。

在帝国线：

```text
角色
→ 创建
→ 导入已有角色
→ 侦探
```

得到独立副本。

---

# 50. 2.1 最终产品原则

**Project 是整个 RPG 工程。**

**剧情是作者主要工作的顶层内容单元。**

**角色 / 对话 / 任务由剧情组织，但底层仍是项目级唯一资源。**

**引用 = 同一资源。**

**创建 → 导入 = 以旧资源为模板创建独立新资源。**

**流程 = 当前剧情唯一真实逻辑。**

**剧情之间通过 Flow 的 Enter Story 节点连接。**

**剧情图谱 = 自动生成的逻辑只读视图；只允许调整视觉布局和导航。**

**剧情图谱点击剧情节点必须直接进入对应剧情的 Flow。**

**剧情没有“重要级别”。**

**进入大标题由明确的“进入展示”配置控制。**

**UI 必须符合 Windows Fluent 风格、响应式、所有按钮真实可用。**

**不要重新制造 CustomNPCs 的编辑逻辑。**

---

# 51. Codex 第一条执行指令

```text
工作目录：
E:\Java\MinecraftMod\DarkGrey_RPG

目标版本：
DarkGrey RPG Studio 2.1

完整阅读本 Plan 后，严格按 M1 → M8 推进。

2.1 不是单纯 UI Patch。

必须同时完成：
1. 当前 WPF Shell 的 UI 可用性修复；
2. Project → 剧情 的剧情驱动信息架构；
3. 剧情内 Actor / Dialogue / Quest / Flow；
4. Create / Create→Import / Reference 的正式语义；
5. Story Flow 作为唯一逻辑 Source of Truth；
6. 自动生成、逻辑只读的剧情图谱；
7. 剧情图谱点击直接进入对应剧情 Flow；
8. 2.0 → 2.1 Schema Migration；
9. Minecraft Runtime Compatibility Regression。

不要进入 Native NPC / Boss / Cutscene 等范围。

每个 Milestone Gate 通过后才允许继续。
```
