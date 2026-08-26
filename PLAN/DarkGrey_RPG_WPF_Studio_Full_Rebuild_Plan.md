# DarkGrey_RPG Studio — WPF 全量重建 Plan

> **项目根目录**：`E:\Java\MinecraftMod\DarkGrey_RPG`  
> **目标**：废弃现有 Godot Studio UI 原型，从零构建新的 Windows 桌面版 DarkGrey RPG Studio  
> **新 Studio 技术栈**：C# + .NET 10 + WPF + XAML  
> **运行平台**：Windows 10 / Windows 11  
> **Minecraft Runtime**：继续使用现有 Java / Forge 1.7.10 代码  
> **当前业务范围**：Actor、Dialogue、Quest、Story 的 RPG 创作工具；本轮只完成 Studio 基础框架与 Actor 工作流  
> **重要限制**：新 Studio Phase 1 完成并验收前，禁止开发正式 Dialogue / Quest / Story 编辑器

---

# 0. 这不是“迁移 Godot UI”，而是重新做一个 Studio

现有 Godot 版 DarkGrey RPG Studio 定义为：

> **Prototype / 技术验证版**

它已经证明了一些有价值的东西：

- 外部 Studio 可以存在；
- RPG Project 可以独立于 Minecraft Runtime；
- Actor 可以作为独立资源存在；
- Actor JSON 可以被 Runtime 读取；
- Studio 与 Minecraft Runtime 可以通过共享项目文件工作；
- DarkGrey_RPG 的创作工具没有必要塞进 Minecraft 1.7.10 GUI。

但是旧版同时证明：

- Godot 并不是我们当前最合适的 Windows 桌面编辑器技术栈；
- 旧版 UI 结构不值得继续修补；
- 旧版“大面板 + Inspector + Tree”的视觉结构不应继续继承；
- 不能因为我们参考 Godot 的部分 UX 思想，就把 Studio 做成“另一个 Godot 编辑器”。

因此：

## 本轮原则

**保留数据和业务成果。**

**放弃旧 Studio 的 UI 实现。**

不要：

- 把 Godot `.tscn` 翻译成 XAML；
- 逐页面照搬旧布局；
- 模仿旧版颜色、边框、Inspector；
- 为“迁移成本”保留错误的 UI 架构。

允许从空白 WPF Window 开始。

---

# 1. 为什么选择 WPF

本项目新的 Studio 固定使用：

```text
C#
.NET 10
WPF
XAML
```

理由：

1. DarkGrey RPG Studio 是 Windows 桌面生产工具，而不是游戏界面。
2. WPF 本身具备成熟的：
   - XAML
   - Data Binding
   - Commands
   - Styles
   - Templates
   - Tree/List
   - Grid
   - Splitter
   - Menu
   - ContextMenu
   - Keyboard shortcuts
   - 响应式布局
3. WPF 很适合长期维护的编辑器类应用。
4. .NET 9+ WPF 已有 Fluent Theme；.NET 10 继续完善 Fluent 样式。
5. .NET 10 当前为 LTS，因此本项目使用 .NET 10，而不是旧 .NET Framework。
6. 未来 Dialogue、Quest、Story Graph 可以在同一桌面应用框架内继续扩展。
7. Minecraft Runtime 与 Studio 通过 JSON / 本地协议通信，不要求两边语言相同。

---

# 2. 不再把 Godot 当作 UI 目标

从现在开始，项目文档中区分：

## Visual Design Reference

视觉参考：

> Windows 11 / Fluent Design / CodexAgentSwitch

## Editor UX References

交互思想可以参考：

> Visual Studio / VS Code / Godot / Blender / Windows Explorer

但：

> **Godot 只是众多 UX 参考之一，不是 Studio 的视觉模板，也不是技术要求。**

不要要求：

- “做成 Godot Inspector”
- “做成 Godot Scene Tree”
- “做成 Godot Editor”
- “复制 Godot 的 Panel 结构”

真正的判断标准是：

> 这个交互是否适合 RPG 任务、对话、剧情制作？

---

# 3. Codex 的第一动作：冻结旧 Studio

在改任何文件之前：

## STEP 0.1 — Git 安全点

如果当前仓库已有 Git：

创建一个清晰的提交：

```text
chore: archive godot studio prototype before wpf rebuild
```

如工作区有未提交改动：

先检查并妥善提交当前有效状态。

不要丢失用户已有 Runtime 成果。

---

## STEP 0.2 — 归档 Godot Studio

将现有：

```text
studio/
```

移动到：

```text
legacy/studio-godot-prototype/
```

如果移动会破坏已有自动化脚本，可以暂时复制归档后删除旧入口。

最终必须做到：

```text
DarkGrey_RPG/
├─ legacy/
│  └─ studio-godot-prototype/
│
└─ studio/
   └─ <新的 WPF Studio>
```

旧 Godot Studio：

- 不再参与 Build；
- 不再继续开发；
- 不作为 UI 代码来源；
- 仅用于查阅旧行为或旧 JSON 格式。

---

# 4. 不允许重写 Minecraft Runtime

当前 Java / Forge Runtime 与 CNPC+ 相关代码默认保留。

本轮不得因为更换 Studio 技术栈而重写：

- Forge 工程；
- CNPC+ Actor Binding；
- `/dgrpg reload`；
- Actor ID persistence；
- RPG Project Loader；
- 已稳定的 Actor JSON schema。

Studio 是创作工具。

Runtime 是执行引擎。

二者必须继续解耦。

---

# 5. 新目录结构

建议：

```text
E:\Java\MinecraftMod\DarkGrey_RPG
│
├─ src/                         # 现有 Java / Forge Runtime
├─ schema/                      # Studio 与 Runtime 共享 JSON Schema
├─ docs/
├─ examples/
├─ run/
│
├─ legacy/
│  └─ studio-godot-prototype/
│
└─ studio/
   ├─ DarkGreyRPG.Studio.sln
   │
   ├─ src/
   │  ├─ DarkGreyRPG.Studio/
   │  ├─ DarkGreyRPG.Studio.Core/
   │  └─ DarkGreyRPG.Studio.Tests/
   │
   └─ README.md
```

---

# 6. WPF Solution 结构

## 6.1 `DarkGreyRPG.Studio`

负责：

- WPF
- XAML
- Window
- Views
- ViewModels
- Theme
- Controls
- Navigation
- Dialogs

不得直接承担磁盘 JSON 业务。

---

## 6.2 `DarkGreyRPG.Studio.Core`

纯 C# 核心库。

负责：

- Project Model
- Actor Document
- Validation
- Serialization
- Resource Repository
- Project Service
- Undo/Redo domain state（如采用自定义实现）
- Future Dialogue / Quest / Story Models

这个项目不得依赖 WPF。

目标：

未来即便再次换 UI，核心数据也不用重写。

---

## 6.3 `DarkGreyRPG.Studio.Tests`

负责：

- Actor ID validation
- Actor serialize / deserialize
- Create / duplicate / delete
- Atomic save
- Project loading
- Dirty state
- Resource collision
- Migration / schema validation（后续）

---

# 7. 技术规范

## Target Framework

```xml
<TargetFramework>net10.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
```

使用当前已安装的稳定 .NET 10 SDK。

不要使用：

- .NET Framework 4.x
- .NET 11 Preview
- WinForms 作为主 UI
- Electron
- WebView 作为整个 UI
- Godot
- Avalonia

---

# 8. WPF Fluent Theme

优先使用 WPF 自带 Fluent Theme 作为基础。

目标支持：

- System
- Light
- Dark

不要第一天手写一整套按钮模板。

先使用 WPF Fluent 基础样式，只有确实无法满足产品视觉时才增加少量 DarkGrey 自定义 Style。

---

## 8.1 主题原则

DarkGrey 自定义样式只负责：

- 页面间距；
- Navigation 选中状态；
- 品牌 Accent；
- Card / Surface 层级；
- Resource Row；
- Empty State；
- Toast；
- 编辑器专用组件。

不要重新设计所有：

- Button
- TextBox
- ComboBox
- Menu
- ScrollBar

除非有明确必要。

---

# 9. 新 Studio 的产品定位

新的 Studio 不是：

> “一个有四个标签的 JSON 编辑器。”

它是：

> **DarkGrey_RPG 的桌面 RPG Authoring Environment。**

从 Phase 1 开始，界面结构就必须为未来：

- Actor
- Dialogue
- Quest
- Story
- Problems
- Debugger
- Minecraft Live

预留正确位置。

但 Phase 1 不提前实现它们的业务。

---

# 10. 新 Studio 顶层布局

视觉基准优先参考用户现有 CodexAgentSwitch。

建议结构：

```text
┌─────────────────────────────────────────────────────────────────────┐
│ DarkGrey RPG Studio                            Project Name     ●    │
├─────┬─────────────────────┬─────────────────────────────────────────┤
│     │                     │                                         │
│ Nav │ Resource Browser    │ Workspace                               │
│     │                     │                                         │
│ 👤  │ [搜索...]           │                                         │
│ 💬  │                     │                                         │
│ 📋  │ 酒馆老板            │                                         │
│ ◇   │ 铁匠                │                                         │
│     │ 村长                │                                         │
│     │                     │                                         │
│ ⚙   │                     │                                         │
├─────┴─────────────────────┴─────────────────────────────────────────┤
│ 输出      问题      调试器      Minecraft                           │
└─────────────────────────────────────────────────────────────────────┘
```

默认不强制常驻右侧 Inspector。

这是对旧设计的重要修正。

---

# 11. 不再强制“三栏 + Inspector”

旧版：

```text
资源树
Workspace
Inspector
```

并不是所有 RPG 工作流都适合。

新的原则：

> **Workspace 是核心。属性应该在最适合当前资源的地方编辑。**

Phase 1 Actor：

可以直接在 Workspace 内形成完整编辑页。

未来：

- Dialogue Editor 有自己的属性区域；
- Quest Editor 有自己的 Objective Inspector；
- Story Graph 可以按需出现右侧 Properties Panel。

因此：

**不要为了“像 Godot”而永久占用 300px Inspector。**

---

# 12. Navigation Rail

最左侧为应用级导航。

项目：

- 角色
- 对话
- 任务
- 剧情
- 分隔
- 设置

要求：

- 图标 + Tooltip；
- 当前项明显选中；
- 固定窄宽；
- 不受 Resource Browser 内容影响；
- Hover 不改变尺寸；
- 支持键盘焦点；
- 必须是真实 Button / Navigation Item。

---

## 12.1 行为

角色：

```text
Resource Browser → Actor Resources
Workspace → Actor Home / Actor Editor
```

对话：

```text
Resource Browser → Dialogue Resources
Workspace → Phase 2 Empty State
```

任务：

```text
Resource Browser → Quest Resources
Workspace → Phase 3 Empty State
```

剧情：

```text
Resource Browser → Story Resources
Workspace → Phase 4 Empty State
```

即使未实现，也必须真实切换。

---

# 13. Resource Browser

作用：

> 当前资源类型的专用浏览器。

Actor 页面：

```text
角色                         ＋
──────────────────────────────
🔍 搜索角色
──────────────────────────────
酒馆老板
铁匠
村长
```

要求：

- Filter Search
- New
- Select
- Double-click Open
- Right-click Context Menu
- Duplicate
- Delete
- Keyboard navigation
- Empty state

---

# 14. Workspace：Actor 首页

如果没有选中 Actor：

不要显示空白。

显示：

```text
角色

角色用于给 Minecraft 中的 NPC 建立 RPG 身份。

你可以创建一个角色，然后在 Minecraft 中将它绑定到
CustomNPC+ NPC。

[新建角色]
```

---

# 15. Workspace：Actor 编辑页

选中 `酒馆老板` 后：

```text
酒馆老板                                      已保存
tavern_owner

────────────────────────────────────────────────────────

基本信息

资源 ID
[tavern_owner                                  ]

显示名称
[酒馆老板                                     ]

标签
[城镇] [商人] [+ 添加]

备注
┌─────────────────────────────────────────────┐
│ 酒馆中的普通委托 NPC。                      │
└─────────────────────────────────────────────┘


Minecraft

绑定状态
未绑定

Minecraft Live 尚未连接。

────────────────────────────────────────────────────────

引用

当前没有其它 DarkGrey RPG 资源引用此角色。
```

Phase 1 允许将 `Minecraft` 部分做成只读状态。

不要制作“假的绑定按钮”。

---

# 16. Actor 不是 CNPC 属性编辑器

新 Studio 中 Actor 只保存 RPG 身份。

Phase 1 Actor 字段：

```text
id
display_name
notes
tags
schema_version
```

不要加入：

- 模型
- 皮肤
- HP
- AI
- 攻击
- 移速
- 动画

这些继续由 CustomNPC+ 自己管理。

---

# 17. Actor Document 模型

必须先完成可靠的数据模型，再接 UI。

```text
ActorResource on disk
        ↓
Load
        ↓
ActorDocument
        ↓
Editable Draft
        ↓
Validation
        ↓
Save
        ↓
Atomic Write
        ↓
ActorResource on disk
```

`ActorDocument` 至少包含：

```text
Id
DisplayName
Notes
Tags
SourcePath
IsNew
IsDirty
ValidationErrors
```

---

# 18. MVVM

WPF Studio 默认采用 MVVM。

不要把全部事件写进：

```text
MainWindow.xaml.cs
```

核心原则：

```text
View
↓ Binding / Command
ViewModel
↓
Service
↓
Core Model / Repository
```

允许少量纯 UI 行为使用 code-behind，例如：

- Window drag
- Focus
- Animation
- View-only state

但业务行为必须通过 ViewModel / Service。

---

# 19. ViewModel 建议

初期至少：

```text
ShellViewModel
NavigationViewModel
ResourceBrowserViewModel
ActorWorkspaceViewModel
ActorEditorViewModel
BottomPanelViewModel
ProblemsViewModel
SettingsViewModel
```

不要创建几十个没有意义的小 ViewModel。

---

# 20. Service 建议

```text
ProjectService
ActorRepository
ValidationService
SettingsService
NotificationService
FileSystemService
```

未来再加入：

```text
MinecraftBridgeService
DialogueRepository
QuestRepository
StoryRepository
```

本轮不要提前实现未来 Service。

---

# 21. Actor ID 规范化

合法 ID：

```regex
[a-z0-9_-]+
```

用户输入：

```text
Teacher NPC
```

UI 应优先建议或自动转换：

```text
teacher_npc
```

规则：

- Trim
- 转小写
- 空格 → `_`
- 连续 `_` 合并
- 删除不支持字符或明确提示
- 防止空 ID
- 防止重复 ID

---

# 22. ID 重命名不是普通文本修改

如果一个已保存 Actor：

```text
tavern_owner
```

改成：

```text
innkeeper
```

这意味着资源 ID 和文件路径变化。

必须作为显式 Rename 操作处理。

Phase 1 可选择：

## 简化策略

已保存 Actor 的 ID 默认只读。

提供：

```text
[重命名资源]
```

执行 Rename。

这是更安全的方案，优先采用。

不要让用户编辑 TextBox 后莫名导致文件名/引用错乱。

---

# 23. Dirty State

任何字段变化：

```text
IsDirty = true
```

UI 显示：

```text
酒馆老板  ●
```

或者：

```text
未保存
```

不要依靠 Save Button 文字后面加 `*` 这种不清晰方式。

---

# 24. 保存语义

快捷键：

```text
Ctrl+S
```

保存当前资源。

保存流程：

1. Validate。
2. 序列化到临时文件。
3. Flush / close。
4. Atomic replace/rename。
5. 成功后更新 `SourcePath`。
6. `IsDirty = false`。
7. Resource Browser 更新。
8. Toast：
   `已保存“酒馆老板”`

---

# 25. 保存失败

必须：

- 不覆盖最后一个有效文件；
- 保持 Dirty；
- 字段就地显示 Validation；
- Problems 面板记录；
- Output 记录技术细节；
- Toast 用自然语言提示。

禁止：

```text
Actor save failed. See Problems.
```

作为唯一反馈。

---

# 26. 新建 Actor

点击：

```text
+ 新建角色
```

行为：

1. 生成 Draft。
2. 默认 ID：
   `new_actor`
3. 已存在则：
   `new_actor_2`
4. 加入 Resource Browser 的“未保存”分组或显示特殊状态。
5. 自动打开 Workspace。
6. 聚焦显示名称或 ID。
7. Ctrl+S 正式创建文件。

---

# 27. Duplicate

支持：

- Ctrl+D
- Context Menu → 复制

例如：

```text
tavern_owner
```

产生：

```text
tavern_owner_copy
```

重复：

```text
tavern_owner_copy_2
```

复制：

- display_name
- tags
- notes

新副本必须拥有新 ID。

---

# 28. Delete

Delete：

- Delete key
- Context Menu
- Command Bar

必须弹出 Windows 风格确认 Dialog。

显示：

```text
删除角色“酒馆老板”？

资源：
tavern_owner

删除后将从当前项目移除。

[取消] [删除]
```

默认安全按钮为取消。

---

# 29. Search

Resource Browser 搜索：

即时过滤：

- Display Name
- ID
- Tags

大小写不敏感。

搜索为空时恢复全部。

---

# 30. Undo / Redo

至少覆盖编辑字段：

- Display Name
- Notes
- Tags

资源 Rename 可暂时不接 Undo。

快捷键：

```text
Ctrl+Z
Ctrl+Y
```

Undo/Redo 后：

- View 更新；
- Dirty 更新；
- Validation 更新。

---

# 31. 顶部标题栏

优先保留标准 Windows Window 行为。

不要为了“看起来酷”第一阶段重写整个标题栏。

要求：

- DarkGrey RPG Studio 图标；
- 标准最小化；
- 最大化；
- 关闭；
- DPI 表现正常。

不要出现 Godot 图标。

---

# 32. 应用图标

创建临时 DarkGrey RPG Studio 应用图标。

要求：

- 简洁；
- 可辨识；
- 不使用 Godot 图标；
- 不复制其它品牌图标；
- 可以先使用 `DG` / 节点连接 / 对话框的简单矢量概念。

如果当前阶段没有正式美术：

允许使用程序生成的临时 `.ico`。

但标题栏和 exe 必须不再显示 Godot。

---

# 33. Top Command / Menu

不要过度堆菜单。

第一版：

```text
文件
编辑
项目
视图
帮助
```

## 文件

- 新建项目
- 打开项目
- 保存
- 保存全部
- 打开项目目录
- 退出

## 编辑

- 撤销
- 重做
- 复制资源
- 删除资源
- 搜索

## 项目

- 重新加载
- 验证项目
- 项目设置

## 视图

- 浅色
- 深色
- 跟随系统
- 显示/隐藏资源浏览器
- 显示/隐藏底部面板

## 帮助

- 关于 DarkGrey RPG Studio

任何尚未实现菜单项：

**Disabled。**

---

# 34. Bottom Panel

保留：

```text
输出
问题
调试器
Minecraft
```

但是真正做成可切换底部工作区。

WPF 使用：

- Grid
- RowDefinition
- GridSplitter
- TabControl 或自定义轻量 Tab

---

## 34.1 Output

显示技术日志和操作日志。

不要用整屏纯红纯绿。

建议：

- 信息：正常灰色
- 成功：Accent / subtle green
- Warning：amber
- Error：red

---

## 34.2 Problems

结构化列表：

| 严重性 | 资源 | 问题 |
|---|---|---|
| Error | actor/tavern_owner | ID 已存在 |
| Warning | actor/guard | 显示名称为空 |

双击：

打开对应资源。

如能定位字段则聚焦字段。

---

## 34.3 Debugger

Phase 1：

Empty State：

```text
调试器将在后续阶段显示正在运行的任务和剧情状态。
```

---

## 34.4 Minecraft

Phase 1：

```text
Minecraft 未连接

当前版本仍通过项目文件和 /dgrpg reload 与 Runtime 配合。
Live Bridge 将在后续阶段加入。
```

---

# 35. Toast / Notifications

新增统一通知系统。

例如：

```text
✓ 已保存“酒馆老板”
```

```text
已复制为“酒馆老板 副本”
```

```text
无法保存：请修复 1 个错误
```

Toast：

- 不阻塞操作；
- 自动消失；
- Error 可以保留更久；
- 不用 MessageBox 处理每一个小事件。

---

# 36. Responsive / Adaptive Layout

WPF 的 Layout 系统负责尺寸适配。

禁止用大量：

```text
Canvas.Left
Canvas.Top
固定 Width
固定 Height
```

拼主界面。

主布局使用：

- Grid
- Auto
- *
- MinWidth
- MaxWidth
- GridSplitter
- ScrollViewer
- WrapPanel / ItemsControl

---

# 37. 最低尺寸

建议：

```text
MinWidth = 1100
MinHeight = 700
```

如果经过实际测试可支持更小，可以下调。

不要为了支持极小窗口把 UI 挤到不可使用。

---

# 38. 必测尺寸

至少人工/自动截图验证：

- 1920×1080
- 1600×900
- 1440×900
- 1366×768
- 1280×720（如低于 MinSize，则验证窗口约束行为）

以及：

- 125% Windows DPI
- 150% Windows DPI（如测试环境允许）

---

# 39. Resource Browser 自适应

建议宽度：

```text
默认 260
Min 200
Max 400
```

使用 GridSplitter 调整。

用户改变后：

保存宽度设置。

---

# 40. Bottom Panel 自适应

默认：

```text
约 200~240px 高
```

可拖动。

允许：

```text
Collapse
Expand
```

保存用户上一次高度。

---

# 41. Workspace 不允许大块无意义留白

没有资源时用 Empty State。

有资源时展示真正编辑内容。

未来：

- Dialogue → Dialogue Editor
- Quest → Quest Editor
- Story → Graph

不要预先造空白大画布。

---

# 42. Light / Dark / System

WPF ThemeMode：

- Light
- Dark
- System

设置保存到用户配置目录。

例如：

```text
%AppData%\DarkGreyRPG\Studio\settings.json
```

不要写入 RPG Project。

Project 是内容。

Studio Settings 是用户偏好。

---

# 43. 用户设置

Phase 1 至少保存：

```text
theme
window_width
window_height
window_maximized
resource_browser_width
bottom_panel_height
last_project
```

如果恢复窗口尺寸会跑出当前屏幕：

回退到安全默认尺寸。

---

# 44. Project 与 User Settings 分离

项目目录：

```text
project.json
actors/
dialogues/
quests/
stories/
resources/
```

用户设置：

```text
%AppData%\DarkGreyRPG\Studio\
```

绝对不要把：

- 窗口大小
- 主题
- Dock 宽度

写入 RPG 项目。

---

# 45. JSON 兼容策略

新 Studio 首先读取现有 Actor JSON。

不要随意修改 schema。

如果旧 Studio 产生的数据格式确实有问题：

1. 记录问题。
2. 新 Studio 支持读取旧格式。
3. 保存时升级到新 schema。
4. 必要时增加 `schema_version` migration。

不要让已有项目直接失效。

---

# 46. Atomic Save

所有 Project Resource 保存采用：

```text
file.tmp
→ serialize
→ validate
→ flush
→ replace original
```

保存中断不能损坏原文件。

---

# 47. Project Service

负责：

```text
OpenProject
CloseProject
ReloadProject
ValidateProject
SaveAll
```

不负责 UI。

---

# 48. Actor Repository

负责：

```text
ListActors
LoadActor
CreateActor
SaveActor
DuplicateActor
RenameActor
DeleteActor
```

所有 Actor 文件操作从这里进入。

ViewModel 不直接：

```csharp
File.WriteAllText(...)
```

---

# 49. 错误处理

区分：

## Validation Error

用户输入问题。

显示在字段和 Problems。

## IO Error

文件被占用 / 权限 / 路径问题。

Toast + Problems + Output。

## Programmer Error

Unexpected Exception。

写日志。

显示：

```text
DarkGrey RPG Studio 遇到意外错误。
```

不要直接崩溃或把 StackTrace 塞给普通 UI。

---

# 50. 日志

日志保存：

```text
%AppData%\DarkGreyRPG\Studio\logs\
```

至少记录：

- App start
- Project open
- Resource save
- Resource delete
- Validation failure
- IO exception
- Unhandled exception

---

# 51. 不要过早引入大型第三方 UI 框架

第一版优先：

```text
WPF Built-in Fluent Theme
+ 少量自定义资源
```

不要一开始加入：

- MaterialDesignInXaml
- MahApps
- HandyControl
- 大型商业组件库

除非确实证明原生 WPF 无法满足某个必要需求。

目标是减少依赖和主题冲突。

---

# 52. 第三方 NuGet 依赖原则

允许必要的小型库。

例如 MVVM 辅助库可以使用稳定版本。

但是：

- 必须记录用途；
- 必须锁版本；
- 不要为一个按钮引入一个框架；
- Core 尽量只依赖 .NET BCL。

---

# 53. Phase 1 新 Studio 功能清单

本轮必须真正完成：

## Project

- [ ] New Project
- [ ] Open Project
- [ ] Reload Project
- [ ] Validate Project
- [ ] Save All
- [ ] Last Project

## Navigation

- [ ] Actor
- [ ] Dialogue Placeholder
- [ ] Quest Placeholder
- [ ] Story Placeholder
- [ ] Settings

## Actor

- [ ] List
- [ ] Search
- [ ] New
- [ ] Open
- [ ] Edit
- [ ] Validate
- [ ] Save
- [ ] Duplicate
- [ ] Rename
- [ ] Delete
- [ ] Dirty
- [ ] Undo
- [ ] Redo

## App

- [ ] Light
- [ ] Dark
- [ ] System
- [ ] Bottom Panel
- [ ] Problems
- [ ] Output
- [ ] Toast
- [ ] Settings persistence

---

# 54. “按钮必须有用”规则

Codex 每创建一个：

- Button
- MenuItem
- Navigation item
- Hyperlink
- Tab
- ContextMenu item

必须满足之一：

```text
A. Command 已绑定且可执行
B. 明确 Disabled
C. 打开明确 Placeholder / Empty State
```

否则不允许提交。

---

# 55. 第一阶段不做“漂亮 Demo”

拒绝：

- 假数据充满列表；
- 点按钮只打印日志；
- 假 Loading；
- 假 Minecraft Connected；
- 无后端的 Save；
- 无功能的 ContextMenu；
- 只有视觉状态的 Toggle。

Phase 1 的 UI 必须围绕真实数据运行。

---

# 56. 执行顺序

Codex 必须按照以下顺序。

---

## STEP 1 — 审计与冻结

1. 检查 Git。
2. 记录当前状态。
3. 归档 Godot Studio。
4. 不修改 Runtime。
5. 输出：
   `docs/WPF_STUDIO_REBUILD_AUDIT.md`

内容：

- 可保留的数据格式；
- 可保留的 Runtime API；
- Godot 版 Actor 保存 bug；
- 新旧 Studio 边界。

---

## STEP 2 — 创建 WPF Solution

创建：

```text
studio/DarkGreyRPG.Studio.sln
```

三个项目：

```text
DarkGreyRPG.Studio
DarkGreyRPG.Studio.Core
DarkGreyRPG.Studio.Tests
```

完成：

```text
dotnet restore
dotnet build
dotnet test
```

全部通过。

---

## STEP 3 — Core Actor Model

先不做漂亮 UI。

实现：

- ActorResource
- ActorDocument
- ActorValidator
- ActorRepository
- ProjectService

编写测试：

- Valid ID
- Invalid ID
- Duplicate ID
- Serialize
- Deserialize
- Atomic Save
- Duplicate
- Delete
- Rename

---

## STEP 4 — 最小真实 Actor Window

先做一个非常朴素的 WPF 页面。

必须跑通：

```text
Open Project
→ List Actor
→ Select
→ Edit
→ Save
→ Restart App
→ Reload
```

只有数据闭环通过，才开始视觉设计。

---

## STEP 5 — Fluent Theme

启用：

- Light
- Dark
- System

实现 Settings persistence。

验证切换无重启即可生效。

---

## STEP 6 — App Shell

实现：

- Navigation Rail
- Resource Browser
- Workspace
- Bottom Panel
- Top Menu

不要照搬旧 Godot 页面。

---

## STEP 7 — Actor Workspace

将 STEP 4 的最小功能迁移到正式 Actor UI。

完成：

- New
- Save
- Duplicate
- Rename
- Delete
- Search
- Inline validation
- Dirty state

---

## STEP 8 — Empty State / Placeholder

实现：

- Dialogue
- Quest
- Story

真正导航，但不实现业务。

---

## STEP 9 — Problems / Output / Toast

建立统一反馈系统。

删除 UI 中所有：

```text
Actor save failed. See Problems.
```

式模糊错误。

---

## STEP 10 — Menus / Commands / Shortcuts

实现：

- Ctrl+S
- Ctrl+Shift+S
- Ctrl+Z
- Ctrl+Y
- Ctrl+D
- Delete
- Ctrl+F

所有菜单项检查 Command。

---

## STEP 11 — Responsive / DPI Pass

测试不同窗口与 DPI。

修复：

- clipping
- text cutoff
- overlap
- tiny controls
- giant blank region
- unreadable dark mode

---

## STEP 12 — Runtime Compatibility Regression

使用 Studio 保存 Actor。

Minecraft Runtime：

```text
/dgrpg reload
```

必须正常读入。

已有 CNPC Actor Binding：

不能受影响。

---

## STEP 13 — Packaging

建立可重复打包流程。

目标输出：

```text
dist/
└─ DarkGreyRPGStudio/
   └─ DarkGreyRPGStudio.exe
```

使用 Windows x64 自包含或明确记录 Runtime 依赖方案。

不要要求最终用户安装 Godot。

---

# 57. 验收场景 A：从零开始

1. 启动新的 WPF Studio。
2. 界面不得出现 Godot Logo。
3. 新建项目。
4. 创建 Actor。
5. ID：
   `teacher`
6. Display Name：
   `老师`
7. Notes：
   `学校中的任务 NPC`
8. Tags：
   `school`, `quest`
9. Ctrl+S。
10. 关闭 Studio。
11. 重新打开。
12. Actor 完整存在。

---

# 58. 验收场景 B：非法 ID

输入：

```text
Teacher NPC
```

必须：

- 明确提示；
- 提供 `teacher_npc` 建议或自动规范化；
- 不产生模糊 save failed；
- 不损坏文件。

---

# 59. 验收场景 C：重命名

已有：

```text
teacher
```

点击：

```text
重命名资源
```

改：

```text
school_teacher
```

必须：

- 检查冲突；
- 更新文件名；
- 更新 document；
- Browser 更新；
- 旧文件不残留。

如果未来存在引用更新需求：

Phase 1 可明确提示“当前无其它已实现资源引用 Actor”。

---

# 60. 验收场景 D：复制 / 删除

复制：

```text
teacher
→ teacher_copy
```

删除副本。

重启 Studio。

磁盘和 Browser 一致。

---

# 61. 验收场景 E：Navigation

依次点击：

- 角色
- 对话
- 任务
- 剧情
- 设置

所有项：

- 有选中状态；
- Workspace 真实变化；
- 无“点了没反应”。

---

# 62. 验收场景 F：主题

测试：

- Light
- Dark
- System

切换：

- 不需要重启；
- 所有文本可读；
- Input / Menu / List / ScrollBar 正常；
- 下次启动保持设置。

---

# 63. 验收场景 G：窗口尺寸

测试：

```text
1920×1080
1600×900
1440×900
1366×768
```

要求：

- 不裁字；
- 不重叠；
- Browser 可调宽；
- Bottom Panel 可调高；
- Workspace 始终可操作。

---

# 64. 验收场景 H：Hover / Focus

遍历：

- Navigation
- Resource rows
- Buttons
- Menu
- Tab
- TextBox
- ContextMenu

要求：

- Hover 不改变尺寸；
- 文字不截断；
- Focus 清晰；
- 键盘可操作。

---

# 65. 验收场景 I：Minecraft Runtime

Studio：

创建：

```text
tavern_owner
```

保存。

Minecraft：

```text
/dgrpg reload
```

Runtime 能找到：

```text
tavern_owner
```

已有 CNPC Actor 绑定功能正常。

---

# 66. 本轮成功标准

必须同时满足：

## 技术

- [ ] Godot Studio 已归档
- [ ] WPF net10.0-windows
- [ ] Solution build
- [ ] Tests pass
- [ ] Core 不依赖 WPF
- [ ] Runtime 没被重写

## UI

- [ ] Fluent 风格
- [ ] Light / Dark / System
- [ ] Windows 标准交互
- [ ] 无 Godot Logo
- [ ] 无旧版紫蓝游戏 GUI
- [ ] Responsive
- [ ] Hover 无裁字

## Actor

- [ ] CRUD 全部真实可用
- [ ] Search
- [ ] Rename
- [ ] Duplicate
- [ ] Delete confirm
- [ ] Validation
- [ ] Dirty
- [ ] Save
- [ ] Atomic write
- [ ] Restart persistence
- [ ] Undo/Redo

## Navigation

- [ ] Actor 页面
- [ ] Dialogue Placeholder
- [ ] Quest Placeholder
- [ ] Story Placeholder
- [ ] Settings
- [ ] 没有 Decorative Buttons

## Feedback

- [ ] Toast
- [ ] Problems
- [ ] Output
- [ ] Inline validation
- [ ] 无模糊 save failed

## Compatibility

- [ ] 现有 Actor JSON 可读
- [ ] Runtime reload 正常
- [ ] CNPC Actor Binding 正常
- [ ] Actor ID persistence 正常

---

# 67. 本轮禁止的伪完成

以下任意一种存在，都不能宣称完成：

- 只是把 Godot 版 UI 用 WPF 重画了一遍；
- MainWindow.xaml.cs 超大，承担所有业务；
- Button 看起来能点但没有 Command；
- Actor 保存只在内存成功；
- 没测试重启；
- 没有单元测试；
- 主题只是换背景色；
- Dark Mode 部分控件不可读；
- Resource Browser 只是静态 List；
- Dialogue / Quest / Story 点了没反应；
- 固定 1440×900 才正常；
- Runtime 兼容性未测试；
- 为了赶进度直接修改 Actor schema 而不做兼容。

---

# 68. Codex 工作规则

1. 当前只做这个 WPF Studio Rebuild。
2. 不进入 Dialogue Phase 2。
3. 每完成一个 STEP：
   - build；
   - test；
   - 手动验证；
   - 更新文档。
4. 当前 STEP 有失败时：
   - 先修；
   - 不继续堆功能。
5. 不凭空声称 UI 已测试。
6. 对 WPF API 不确定时：
   - 查官方文档；
   - 实际 build。
7. 避免大规模无关重构 Runtime。
8. 所有新第三方依赖写入：
   `docs/STUDIO_DEPENDENCIES.md`
9. 架构决策写入：
   `docs/DECISIONS.md`
10. WPF Studio Phase 1 全部验收后停止，等待用户试用。

---

# 69. Codex 第一条执行指令

```text
工作目录：
E:\Java\MinecraftMod\DarkGrey_RPG

现有 Godot Studio 是已废弃的 Prototype。
不要继续修补它，也不要照搬它的 UI。

首先执行：
STEP 1 — 审计与冻结。

保留现有 Java/Forge Runtime、Actor JSON 兼容和 CNPC Actor Binding。

新建：
C# + .NET 10 + WPF Studio。

视觉目标：
Windows 10/11 Fluent + CodexAgentSwitch 风格。

不要把 Godot 当作视觉模板。

编辑器 UX 只在确实合适时参考 Godot / Blender / VS Code / Visual Studio。

新 Studio 当前只完成：
项目管理 + Actor 完整 CRUD + 可扩展 App Shell。

Dialogue / Quest / Story 只做真实可切换 Placeholder。

禁止进入 Phase 2，直到新 WPF Studio Phase 1 全部验收通过。
```

---

# 70. 最终原则

DarkGrey RPG Studio 不属于 Godot。

也不应该“长得像 Godot”。

它首先应该是一款：

> **自然、现代、稳定、长期可用的 Windows RPG 内容生产工具。**

技术：

> **C# + .NET 10 + WPF**

视觉：

> **Windows 11 Fluent / CodexAgentSwitch**

交互：

> **为 RPG Authoring 服务，并择优吸收成熟编辑器的操作逻辑**

底层：

> **继续与 Minecraft 1.7.10 DarkGrey_RPG Runtime 通过稳定数据格式解耦**

本轮宁可彻底重建，也不要继续在错误的 UI 基础上积累技术债。
