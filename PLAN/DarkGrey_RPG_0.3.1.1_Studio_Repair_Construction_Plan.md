# DarkGrey_RPG 0.3.1.1 Studio 修缮施工 PLAN

> **版本定位**：0.3.1.x Studio 修缮线的第一轮集中修复版本  
> **目标版本**：DarkGrey_RPG 0.3.1.1  
> **施工基线**：`codex/0.3.1.0` @ `96c821d1a2b4038fecdc62bdbbbd8078291a8d0e`  
> **人工审计基线**：用户提交的《审计清单.docx》29 条 Studio 反馈  
> **源码审计补充**：此前确认的 A / B / C / D 四类问题，以及与这些问题同根的明显 Studio authoring 残留  
> **重要边界**：0.3.1.1 **不是 Studio 冻结版**。它只要求完整解决本轮已经确认的问题并避免明显回归。若人工复验后仍存在问题，继续进入 0.3.1.2 / 0.3.1.3。只有用户明确宣布“冻结 Studio”后，才停止 0.3.1.x Studio 修缮线并进入 0.3.2.0。

---

# 0. 总目标与版本边界

## 0.1 0.3.1.1 要解决什么

0.3.1.1 只围绕 **Studio 本身的可用性、编辑体验、authoring 模型一致性和明显旧设计残留**施工。

核心目标：

1. 修复《审计清单.docx》29 条反馈，不得遗漏。
2. 修复源码审计确认的 A / B / C / D 四类问题。
3. 优先修根因，而不是逐个截图打补丁。
4. 让 Story / Session / Task 的编辑体验重新回到统一 Graph Editor 语言。
5. 让 0.3.1.0 已经存在的 NPC / Group / Item 身份模型真正进入 Studio 的正常作者工作流。
6. 消除普通作者不应该看到的内部 ID、英文技术字段和旧 Actor 概念。
7. 让 0.3.1.1 成为一个可继续人工审计的明显更稳定版本，而不是自称“Studio 已完成”。

## 0.2 0.3.1.1 明确不做什么

本版本**不把范围扩展到 Minecraft 游戏内体验**。

禁止主动扩展：

- Minecraft 会话 GUI 美化；
- Minecraft Journal 美化；
- 指名器 / 复制器 / 收纳箱游戏内 UI 重做；
- CNPC 游戏内兼容专项修复；
- Story Package 安装体验重做；
- `/dgr reload` 游戏内交互重做；
- 完整 DGR Native NPC；
- 新增大量 Objective 类型；
- 新增大量 Action 类型；
- 新的脚本语言、变量语言、表达式语言；
- 第二套 Graph Editor；
- 全新的 Studio 框架重写；
- 为本轮修复另造通用 UI 框架、插件框架、Provider / Registry / Scenario / Harness 系统；
- 因“顺手”而大规模重构 Runtime。

**例外**：如果修正 Studio 的 canonical 数据契约必须同步修改少量 Runtime / loader / serializer 代码以保证现有数据不被破坏，可以做“维持契约一致性所需的最小修改”，但不得借此进入 Minecraft 游戏内功能施工。

## 0.3 设计优先级

发生冲突时，按以下顺序执行：

1. 本 0.3.1.1 PLAN；
2. 用户《审计清单.docx》的明确反馈；
3. 0.3.1.0 Construction Plan 的最终语义；
4. 0.3.0.0 Design Plan 中未被 0.3.1.0 替代的 Studio 原则；
5. 更早的 2.x / legacy 设计仅用于迁移兼容，不得重新成为新 authoring 模型。

---

# 1. 施工前源码定位：先认清根因再修改

Codex 在修改前必须阅读并定位下列热点文件，不得只根据截图猜测：

```text
studio/src/DarkGreyRPG.Studio/MainWindow.xaml

studio/src/DarkGreyRPG.Studio/Views/Graph/
  CanonicalStoryWorkspaceView.xaml
  CanonicalStoryWorkspaceView.xaml.cs
  CanonicalGraphEditorView.xaml
  CanonicalGraphEditorView.xaml.cs
  CanonicalGraphNodeControl.xaml

studio/src/DarkGreyRPG.Studio/Views/
  FlowPortControl.cs
  ActorCreationChoiceDialog.xaml

studio/src/DarkGreyRPG.Studio/ViewModels/Graph/
  CanonicalStoryWorkspaceViewModel.cs
  CanonicalNodeInspectorViewModel.cs
  GraphEditorHostViewModel.cs

studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/
  GraphNodeDefinition.cs
  GraphNodeDefinitionRegistry.cs
  GraphNodeAuthoringService.cs
  StoryStartSchema.cs
  CanonicalTaskObjectiveSchema.cs

studio/src/DarkGreyRPG.Studio.Core/Graphs/Editing/
  GraphEditSession.cs
  GraphEditValidation.cs

studio/src/DarkGreyRPG.Studio/Services/
  ActorWorkspaceDialogs.cs
```

以下根因已有较强源码证据，施工优先从这些点收敛。

## R1 — Graph 集合变化触发整图 Rebuild

当前 `CanonicalGraphEditorView` 的集合变化路径会直接调用 `RebuildGraph()`，而 `RebuildGraph()` 会清空并重建节点 Visual、端口索引和连线。

这与用户观察到的：

- 新节点像从上到下逐步刷新；
- 节点添加动画生硬；
- 某些图形修改时整块界面闪动；

高度一致。

**施工原则**：

> 先消灭无必要的整图重建，再谈动画。

禁止用 Fade / Storyboard 把全量 Rebuild “遮漂亮”。

## R2 — 拖线虚线是当前代码主动创建的

当前 `BeginWire()` 明确创建 dashed draft wire。

因此“连线又变成预览虚线”不是偶发现象，而是当前实现本身。

本版本必须按第 4 章重新实现真实连线拖动语义。

## R3 — Add Node 数据源把固定节点也列进来了

当前 authoring scope 主要只过滤 `CompatibilityOnly`，导致：

- Story【开始】；
- Session【起始】；
- Task【结算】；

这些固定 / required / unique 节点仍进入“添加节点”菜单，只是在 UI 上被置灰。

正确规则是：

> **不能由用户放置的节点，直接从添加菜单数据源消失，而不是显示成灰色。**

优先复用现有 `Required / Unique / NonDeletable` 元数据实现过滤；不要为此新造 Node Palette Framework。

## R4 — Session【选择】被 UI 人为禁止，但 Core 已能创建

Core authoring service 已有 Session【选择】的默认选项 / 动态端口初始化能力，但 View 的 authoring gate 又把 `Session + choice` 特殊排除。

因此用户看到【选择】灰掉属于 UI 与 Core 能力冲突，不是设计尚未讨论。

0.3.1.1 必须恢复正常创建【选择】。

## R5 — Task【结算】新增槽固定使用“新结果”

当前新增结果槽默认显示名固定为：

```text
新结果
```

导致第二次新增即触发重名。

必须改为确定性自动命名：

```text
结果 1
结果 2
结果 3
...
```

若某个标准名已存在，寻找第一个未占用编号。

`port_id` 继续保持稳定 opaque ID，绝不由显示名生成。

## R6 — Objective 仍直接使用 Minecraft 原始目标字符串

当前 Task Objective 的 kill / collect 正常 authoring 仍围绕：

```text
minecraft:slime
minecraft:stone
```

以及 `entity / item / metadata / actor_id` 这类旧目标字段。

这与 0.3.1.0 已建立的：

- 个体角色 NPC ID；
- 集体角色 Group ID；
- 个体 Item ID；
- 集体 Item Group ID；

不一致。

0.3.1.1 必须把**正常 Studio authoring**切换到 DGR 角色 / 物品资源。

旧 raw 字段只允许作为兼容入口存在，不再作为普通作者首选 UI。

## R7 — 旧 Actor authoring UI 没有真正退场

Core 已经存在：

```text
IndividualActorResource
CollectiveActorResource
npc_id
group_id
```

但当前创建流程仍大量沿用：

```text
空白创建 / 导入已有角色
ActorIdentityDialog
Actor ID
```

说明 Schema 3 已经存在，但 Studio 作者入口仍主要是旧 Actor 工作流。

0.3.1.1 必须完成：

> **旧 Actor authoring → 角色（个体 / 集体）authoring**

的 UI 收口。

内部 C# 类名暂时叫 Actor 可以接受；普通用户界面不能再把旧 Actor ID 当主要产品概念。

## R8 — Session【起始】仍保留旧 ◆ Logic Out

当前节点定义中 Session【起始】仍有：

```text
● Flow Out
◆ Logic Out
```

但 0.3.1.0 已经有独立【逻辑输入】。

最终 authoring 语义应为：

```text
【起始】
  只负责 ● 会话流程起点

外部 ◆
  统一通过【逻辑输入】进入
```

0.3.1.1 必须让**新创建 / 正常编辑的 Session**不再产生【起始】◆输出。

旧项目若包含该端口，不得静默破坏；按第 9 章做兼容迁移。

## R9 — Story Start 的 legacy EnterStory 与新 UI 混在一起

最终规则中：

```text
正式启动类型：角色交互 / 进入区域 / 逻辑条件
legacy：enter_story
```

因此 `enter_story` 只能被旧数据 loader / migration 接受，不能继续成为新创作选项。

旧数据中的 EnterStory 只能：

- 兼容读取；
- 迁移；
- 或明确显示“旧数据待迁移”；

不能让用户新建。

## R10 — 内部 ID 被主动放进普通 UI

当前普通界面会直接显示 NodeId、资源 Id、InspectorId、旧 Actor ID、`actor_id` 等。

本版本必须明确区分：

### 普通作者可见、具有产品语义的身份

可以显示：

- NPC ID；
- Group ID；
- Item ID；
- Item Group ID；
- 用户命名的 Story / Session / Task 名称。

### 普通作者默认不可见的内部标识

隐藏：

- Graph Node ID；
- `aggregate_<guid>`；
- `port_id`；
- 内部资源 GUID；
- `actor_id` / `speaker_actor_id` 这样的代码字段名。

如诊断需要，可以在 Problems / 技术详情里显示，但不能占据普通编辑界面。

## R11 — 资源右键分类遗漏【物品】

当前右键分类对 Session / Task / Missing 有分支，其他 fallback 到 Actors；【物品】可能被错误当成【角色】。

本版本必须修复并新增回归测试。

## R12 — Graph 导航存在显式保存门

当前 Workspace 有 `CanLeaveGraph` 一类切图保存 gate，这与用户从 Story Flow 切 Session / Task 时频繁被要求保存的体验一致。

0.3.1.1 的目标不是“把弹窗文案写好”，而是：

> **普通图之间导航不再以保存为前置条件。**

详见 Work Package B。

## U1 — 根因未确定：Flow 区域上下尺寸不够灵活

审计反馈表现为：

> “左右可以移动，但是上下不能移动 Flow 窗口。”

先根据用户截图与当前布局复现。

- 若删除冗余顶部区域后问题自然消失，不再额外制造 splitter；
- 若仍然存在实际上下布局调节需求，再加入最小的纵向 `GridSplitter`。

禁止在未复现前凭感觉增加多层可拖区域。

## U2 — 根因未确定：按 Alt 后出现白色框

不要先假定是 Focus、AccessKey 或 Selection Adorner。

必须：

1. 实机复现；
2. 记录 Keyboard Focus / Mouse Capture / 相关 Visual 状态；
3. 确认根因；
4. 再与 Left Alt 剪刀快捷键一起修正。

## U3 — 根因未确定：既定合法 Flow 连接却报错

当前全局基数规则必须保持：

```text
● Flow：输出最多 1，输入可多
◆ Logic：输出可多，输入最多 1
```

而现有 CandidateEdgeValidator 的主要 cardinality 也已经按这个方向实现。

所以禁止为了修用户截图直接修改全局 cardinality。

必须先复现并记录：

```text
ValidationIssue.Code
from_node / from_port
to_node / to_port
interface_kind
aggregate port projection
```

再定位是否属于：

- aggregate 动态端口投影；
- 旧脏数据；
- reconnect transaction；
- port identity；
- scope；
- 其它局部问题。

---

# 2. Work Package A — Workspace Shell 收敛

解决审计 #1、#2、#3、#13 的主要问题，同时释放更多真正用于创作的屏幕空间。

## A1. 新建故事页面

当前只有一个主要“新建故事”操作时：

- 主操作居中；
- 不保留假想的第二列空位；
- 空项目说明与主操作形成清晰层级。

不要为了未来可能增加按钮而长期保留明显空洞布局。

## A2. 删除冗余应用级“故事 / 设置”左侧导航

当前 app-level navigation rail 中的：

```text
故事
设置
```

对 Story-first 工作流没有足够价值。

0.3.1.1：

- 删除这层冗余导航；
- 打开项目后直接进入项目 / Story 工作区；
- Theme 设置继续放在顶部【视图】菜单；
- 不再为简单主题设置维护一个全屏 Settings page。

注意：**不要删除 Story 内真正的角色 / 物品 / 会话 / 任务资源库。**

## A3. 删除占空间的大标题区

普通工作区不再长期占据大面积显示：

```text
DarkGrey RPG Studio
当前项目
```

窗口标题改为：

```text
<Project Display Name> — DarkGrey RPG Studio 0.3.1.1
```

无项目时：

```text
DarkGrey RPG Studio 0.3.1.1
```

项目必要状态可用小型 breadcrumb / status 表达，不要再用巨大 banner。

## A4. 纵向可调空间

按 U1 先复现。

最终原则：

- Story workspace 不应被不必要的固定头部 / 底部布局锁死；
- 真正需要调节的上下相邻区域提供合理 `GridSplitter`；
- Bottom Dock 继续可折叠、可拖高度；
- 不制造多层嵌套 splitter。

## A5. Breadcrumb 成为局部图返回的唯一主导航

进入 Session / Task 局部图后：

- 顶部 breadcrumb 可返回 Story Flow；
- 删除重复的【返回 Story Flow】大按钮；
- 不在多个 Inspector / 页面反复出现同义返回按钮。

---

# 3. Work Package B — Workspace 状态、资源库与刷新稳定性

优先解决 #4、#5、#19、#20、#22。它们会干扰后续所有 UI 判断，因此必须先修。

## B1. 资源创建不得整棵资源库闪烁

现象：

> 新建角色、物品、会话、任务时，已有资源全部闪动。

Codex 必须先确认 Shell / Workspace 当前是否通过“重新加载整个 CanonicalStoryWorkspaceViewModel”完成普通刷新。

最终要求：

- 新建一个资源只增量加入对应 folder；
- 删除只移除目标项；
- 引用只更新目标项 / membership；
- 其它 folder 和已有资源 visual 不重建；
- 展开状态、选择状态、滚动位置不丢。

Full reload 只允许用于：

- 项目重新打开；
- schema migration；
- recovery；
- 用户显式 refresh；

而不是普通资源增删。

## B2. 资源库使用稳定集合

正常 workspace 生命周期内资源 item identity 必须稳定。

实现优先使用现有 WPF `ObservableCollection` 或等价的最小增量方案。

禁止新建 Resource State Framework。

## B3. 资源库展开 / 收起顺畅

先完成 B1/B2，再检查 Expander。

要求：

- 打开 folder 不触发整个 workspace reload；
- 不对大量子项使用重型布局动画；
- 如保留动画，必须短、轻，不造成输入延迟；
- 100+ 资源时仍能顺畅展开和滚动。

本版本不追求华丽动画。

## B4. Graph CollectionChanged 不再默认整图 Rebuild

修改现有 `CanonicalGraphEditorView`，不要新建 Graph Editor v2。

### Add Node

只创建该 node visual、索引该节点 ports，并更新受影响部分。

### Remove Node

只删除该 node visual、ports 与 incident connection visuals。

### Add / Remove Connection

只新增 / 移除对应 wire visual。

### Node Position

继续局部移动 + 重画 incident wires。

### Reset / Host replacement

只有这里允许 full `RebuildGraph()`。

要求保留：

- viewport zoom；
- pan；
- node selection；
- connection selection；
- keyboard focus；
- Inspector selection。

## B5. 节点添加不得表现为“逐层刷出来”

修完 B4 后再验收。

一个新节点应当在一次明确的 visual commit 中出现，而不是让用户肉眼看到：

```text
节点容器刷新
→ 端口刷新
→ 连线刷新
→ 选择刷新
```

## B6. 图导航不再弹“先保存”

从：

```text
Story Flow
→ Session
→ Task
→ Story Flow
```

普通导航不得弹保存确认。

正确语义：

- 当前编辑状态继续留在内存；
- `IsDirty` 继续存在；
- `Ctrl+S` 保存当前；
- `Ctrl+Shift+S` 保存全部；
- 关闭项目 / 退出 Studio 时才按正常未保存策略询问；
- 切换图不是关闭文档。

优先方案：

> 保留 Story / Session / Task 对应 editor / host 的内存实例，不因切换而从磁盘重载。

若现有代码用极小改动实现“静默保存后切换”更安全，也只有满足以下条件才可采用：

- 不弹窗；
- 不因暂时无效编辑强制丢数据；
- 用户返回原图时状态完全一致；
- Undo / Redo 语义不被破坏。

禁止为此开发全局 AutoSave Framework。

## B7. 资源选中状态必须持久可见

要求：

- `SelectedTreeItem` 与资源项视觉直接绑定；
- 选中资源有明确但不过度刺眼的背景 / 边框；
- 资源滚出视窗再回来仍显示选中；
- 右侧 Inspector 与左侧高亮始终一致；
- 删除按钮始终基于当前明确选中项。

## B8. 修复【物品】右键误分类

为物品资源明确映射到：

```text
CanonicalStoryFolderKind.Items
label = 物品
```

右键物品不得再出现“新建角色”等错误操作。

---

# 4. Work Package C — Graph Editor 核心交互修复

解决 #6、#7、#8、#9、#12、#15、#25、#26，并覆盖与 #11 相关的 aggregate node 操作。

**只修改现有 Canonical Graph Editor。不得建立第二套编辑器。**

## C1. 节点右键菜单

右键节点至少提供：

```text
编辑
删除
```

### 聚合【会话】/【任务】

【编辑】直接进入对应局部 Graph。

### 普通可配置节点

【编辑】：

- 选中节点；
- 聚焦节点参数区域或 Inspector；
- 不弹一个全新的通用编辑器窗口。

### 固定节点

如【开始】【起始】【结算】：

- 可以编辑属性；
- 不显示不可执行的【删除】。

不得出现 Decorative Menu Item。

## C2. 资源拖入 Flow 时显示节点 ghost

当前资源 Drop 后才突然生成节点。

改为：

1. 用户从资源库开始拖 Session / Task；
2. 鼠标进入 Graph viewport 后显示一个**未提交的节点 ghost**；
3. ghost 使用目标聚合节点的真实视觉或轻量等价预览；
4. ghost 随鼠标实时移动；
5. 有效 drop 时才真正写入 Graph；
6. Esc / 拖出 viewport / cancel 时 ghost 消失，不修改文档。

禁止在 `DragOver` 阶段提前创建真实节点再回滚。

优先复用现有 `CanonicalGraphNodeControl` 的视觉语言，不建立 Drag Preview Framework。

## C3. 真实连线拖动：全局规则

**禁止继续使用虚线 preview wire。**

拖动中的线必须：

- 使用与正常连线同类的实线视觉；
- 表现为正在移动的真实 wire；
- 有效目标端口可以高亮。

Cardinality 保持：

| 接口 | 端点 | 允许连接数 |
|---|---|---:|
| ● Flow | 输出 | 最多 1 |
| ● Flow | 输入 | 0..N |
| ◆ Logic | 输出 | 0..N |
| ◆ Logic | 输入 | 最多 1 |

## C4. 从没有连线的端口开始

若端口当前没有 incident connection：

- 从输入端拖也可以开始新连接；
- 从输出端拖也可以开始新连接；
- 另一端必须落到相反方向、同接口类型端口；
- Graph 文档内部最终仍保存标准 `output -> input` connection。

拖动线为实线，不是虚线。

## C5. 拖动已有单连接端点

示例：

```text
A 输出 ● ───── ● 输入 B
```

拖 A 的输出端：

```text
B 输入保持固定
真实连线另一端跟随鼠标
```

空白处松手：

```text
断开原连接
```

拖 B 输入端同理。

如果拖到新的**同方向端口**，则将该端点重接到新端口。

即：

- 移动 output endpoint → drop 到另一个 output；
- 移动 input endpoint → drop 到另一个 input。

这与“从空端口新建 connection 时 drop 到相反方向端口”是两种 gesture，必须区分。

## C6. 拖动多连接端点

只有两种端点合法拥有多条 incident wire：

```text
● Flow Input
◆ Logic Output
```

如果用户直接拖动这种已连接端点：

> **所有 incident wires 一起被拖动。**

### Flow Input

多个 source output 保持固定，所有线的 input endpoint 一起跟鼠标移动。

- drop 到新 Flow Input → 全部重接到新 input；
- drop 空白 → 全部断开。

### Logic Output

多个 target input 保持固定，所有线的 output endpoint 一起跟鼠标移动。

- drop 到新 Logic Output → 全部重接到新 output；
- drop 空白 → 全部断开。

增加一条新连接时，仍从另一侧的空 / 单连接端开始：

- 新 Flow Output → 已有 Flow Input；
- 新 Logic Input → 已有 Logic Output。

因此不需要为“新增一条线”再发明 modifier。

## C7. 多线重接必须事务化

多线重接时：

1. 先验证整个目标是否合法；
2. 合法 → 一次 commit；
3. 非法 → 所有原 wire 保持原状；
4. drop 空白 → 明确 disconnect 对应 wire(s)。

不得出现“前两条已经移走，第三条失败”的半提交状态。

## C8. 剪刀工具

在 Graph toolbar 加入【剪刀】。

### 点击工具

- 进入剪线模式；
- 鼠标显示剪刀 cursor；
- 点击任意 wire → 断开该 wire；
- 节点仍正常显示，但不会误触 node drag。

### Left Alt 临时快捷键

- 按住 **左 Alt** → 临时进入剪线模式；
- 松开 → 恢复此前工具状态；
- 只监听 Left Alt，不把 Right Alt / AltGr 当成剪刀。

### Tooltip

鼠标悬停剪刀按钮后显示：

```text
剪断连线
Alt 键
```

不要为一个快捷键新造 Shortcut Overlay System。

## C9. 修复 Alt 白框

与 C8 同阶段解决 U2。

验收：

- 按 Left Alt 不出现异常白矩形；
- 不抢走 Graph selection；
- 不意外激活顶部 Menu；
- 松开后工具状态恢复。

如果根因是 WPF access key / focus 行为，应局部处理 Graph 区的键盘事件，不要全局禁用 Alt。

## C10. 删除节点

任何可删除节点执行【删除】前都弹确认。

### 无连接

```text
确定删除节点“XXX”吗？
```

### 有连接

```text
节点“XXX”当前连接了 N 条连线。
删除节点会同时删除这些连线。
确定继续吗？
```

确认后：

- 删除节点；
- 删除全部 incident connections；
- 更新 aggregate / Problems / Inspector；
- Undo 可以恢复整次操作。

Core 已有删除节点并处理引用/连线的能力时，优先把 UI confirmation 正确接上，不重写删除核心。

## C11. 端口锚点固定

最终视觉：

```text
输入端口：统一贴左边缘
输出端口：统一贴右边缘
```

端口 anchor 的 X 坐标不能因为：

- “是”；
- “普通完成”；
- “一个非常长的结果名称”；

长度不同而变化。

文字放在 anchor 内侧：

- 输入 label 向右延伸；
- 输出 label 向左延伸；
- anchor 位置固定；
- 动态端口改名只影响文字，不影响线接点。

## C12. #26 连接错误专项

先建立四条最小规则测试。

### Flow 合法

```text
A Flow Out ─┐
            ├→ X Flow In
B Flow Out ─┘
```

### Flow 非法

```text
A Flow Out → X Flow In
          └→ Y Flow In
```

### Logic 合法

```text
A Logic Out → X Logic In
            → Y Logic In
```

### Logic 非法

```text
A Logic Out ─┐
             ├→ X Logic In
B Logic Out ─┘
```

然后复现用户截图中的 aggregate Task 场景。

只有拿到具体 `ValidationIssue.Code` 后才修局部根因。

**禁止把 Flow Input 改成单来源，也禁止把 Logic Output 改成单去向。**

---

# 5. Work Package D — 导航与资源编辑入口

解决 #11、#13，并与 C1 / B6 合并验收。

## D1. 双击聚合节点进入局部图

Story Flow：

```text
双击【会话】 → 对应 Session Graph
双击【任务】 → 对应 Task Graph
```

要求：

- 使用节点绑定的真实 `resource_id`；
- 资源缺失时不崩溃，显示中文问题；
- 不弹“先保存”。

## D2. 资源库右键【编辑】

对 Session / Task：

```text
右键资源 → 编辑
```

进入对应局部图。

对角色 / 物品：

```text
右键资源 → 编辑
```

选中并聚焦对应资源 Inspector / editor。

## D3. 双击资源保持一致语义

- 双击 Session / Task 资源 → 局部图；
- 双击角色 / 物品 → 正常资源编辑；
- 不制造第二套“打开方式”。

---

# 6. Work Package E — 角色 / 物品 authoring 模型收口

解决 #10、#14、#16、#24，以及源码审计 D。

## E1. 普通 UI 不再出现“Actor ID”

用户可见统一使用：

```text
角色
NPC ID
Group ID
```

内部类名 `ActorResource` 暂时保留无妨。

以下普通作者文案应消失：

```text
Actor ID
actor_id
speaker_actor_id
```

如诊断需要显示原始字段名，只能放进技术详情。

## E2. 创建角色第一步必须选择角色类型

创建角色流程：

```text
创建角色
├─ 个体
└─ 集体
```

### 个体

字段：

```text
NPC ID
显示名称
标签
```

创建：

```text
IndividualActorResource
type = individual
npc_id = <用户输入>
```

### 集体

字段：

```text
Group ID
显示名称
标签
```

创建：

```text
CollectiveActorResource
type = collective
group_id = <用户输入>
```

不得再让新用户先理解旧“Actor ID”。

旧“空白创建 / 导入已有角色”若仍需保留：

- 可以放到下一层；
- 或改成“从已有角色复制”；
- 不能取代个体 / 集体身份选择。

## E3. 角色资源库显示有意义的身份

资源项主要显示：

```text
显示名称
```

若需要小字：

个体：

```text
NPC ID: tavern_boss
```

集体：

```text
Group ID: slimes
```

不得显示 Graph GUID / internal resource GUID。

## E4. 物品资源遵循同样原则

个体物品：

```text
Item ID
显示名称
```

集体物品：

```text
Group ID
显示名称
```

UI 不暴露内部资源 GUID。

---

# 7. Work Package F — 节点参数、Inspector 与资源拖入参数

解决 #14、#23、#24、#29，并修复“Inspector 是唯一编辑入口”造成的拖拽冲突。

核心原则：

> **高频、决定节点语义的参数应该在节点本体上可见 / 可操作；Inspector 是完整详情，不是唯一入口。**

不要因此开发通用可视化表单 DSL。

对当前明确节点做最小显式模板即可。

## F1. 节点内增加可折叠【参数】区域

至少覆盖本轮最需要的节点。

### 【开始】

节点本体直接显示启动方式摘要，例如：

```text
开始
────────────────
角色交互  酒馆老板
进入区域  主世界 (0,64,0) r=5
+ 启动方式
```

可折叠，但不能把所有核心语义藏在 Inspector。

每条启动方式的 ● 输出仍保持独立。

### Task【目标】

至少显示：

```text
目标类型
目标对象
数量
```

例如：

```text
击杀
史莱姆
10
```

### 【动作】

至少显示：

```text
动作类型
核心目标
数量 / 主要参数
```

### Session【台词】

至少能看到：

```text
说话角色
台词摘要
```

不要求在 0311 把全部复杂字段塞进节点。

## F2. Inspector 变成明确属性表单

Inspector 中每一个控件必须有作者能理解的中文 Label。

例如 Start：

```text
重复策略
启动方式名称
启动方式类型
角色
维度
X
Y
Z
半径
逻辑条件
```

禁止只摆裸 TextBox / ComboBox 让用户靠 Tooltip 猜。

## F3. 资源可以拖入节点参数

至少支持：

### 角色资源

拖到：

- 【开始】角色交互目标；
- 【台词】说话角色；
- Task 角色相关 Objective 目标。

### 物品资源

拖到：

- Task【收集物品】目标；
- 【动作：给予物品】目标。

Drop 时：

- 检查资源类型；
- 合法 → 写入 DGR resource identity；
- 不合法 → 不修改，并给轻量中文提示。

**开始拖资源时不得因为 click-selection 把当前节点 Inspector / 参数上下文切走。**

## F4. 【给予物品】必须选择 DGR 个体 Item ID

0.3.1.0 最终规则：

```text
给予物品
→ 只接受个体 Item ID
→ 不接受 Group ID
```

若当前仍允许自由输入 raw registry / item string，改成：

- Item resource picker；
- 或物品资源拖入。

集体 Item Group 在该字段中应明确不可选。

---

# 8. Work Package G — Task Objective authoring 收口

解决 #23、#24。

## G1. 0.3.1.1 不新增 Objective 类型

当前正式最低目标类型维持：

```text
击杀
收集
交互
```

用户反馈“其它目标类型呢？”反映当前 UI 有明显缺失感，但本版本**不要擅自扩展几十种 Objective**。

旧 ReachLocation / 进入区域是否正式回归，留到后续专门设计。

0311 先把现有三种做正确、做可用。

## G2. Objective 类型选择框统一 Studio 主题

修复当前白色 / Windows 默认样式的 ComboBox。

要求：

- Dark / Light 与 Studio Fluent 视觉一致；
- hover / selected / drop-down 背景正确；
- 不出现白底刺眼菜单；
- DPI 125% / 150% 不错位。

不要单独造 Objective 专用主题框架。

## G3. 击杀目标改为 DGR【角色】资源

正常 authoring 不再要求：

```text
minecraft:slime
```

而是：

```text
角色：史莱姆
```

角色资源可为：

- 个体 NPC ID；
- 集体 Group ID；

具体何者允许参与该 Objective，应遵循现有 DGR identity/runtime contract。

若当前 Runtime 对某种 identity 暂未支持：

- Studio 明确过滤 / 提示；
- 不得退回 raw Minecraft registry string 作为正常 authoring 入口。

## G4. 收集目标改为 DGR【物品】资源

正常 authoring：

```text
物品：铜币
```

可以引用：

- 个体 Item ID；
- 集体 Item Group ID。

匹配细节由【物品】资源自身精确 / 模糊定义负责。

Objective UI 不再要求作者填写：

- `minecraft:stone`；
- 原始 metadata；
- NBT 字符串。

旧字段仅做兼容。

## G5. 交互目标统一使用 DGR 角色资源

普通 UI 不再显示 `actor_id`。

显示：

```text
交互角色
角色：酒馆老板
```

## G6. Canonical 数据契约处理原则

不要为了 UI 好看创建一个只有 Studio 理解、Runtime 完全不懂的新格式。

Codex 必须先追踪：

```text
Studio Objective
→ serializer
→ Story Package
→ Java canonical loader
→ Task runtime matcher
```

优先复用 0.3.1.0 已有的 DGR identity/resource contract。

若现有 persisted schema 只能表达 raw Minecraft ID，则允许做**最小 canonical schema 修正**，但必须满足：

1. 新 authoring 保存 DGR identity/reference；
2. 旧 `entity / item / actor_id` 数据仍可读；
3. 旧项目不静默损坏；
4. Java loader 至少能解析新格式，不因 0311 Studio 保存而崩；
5. 更深入的 Minecraft 行为验收留给 0.3.2.0。

---

# 9. Work Package H — 固定节点、Start、Session、Settlement 清理

解决 #18、#21、#27、#28，以及源码审计 A / C。

## H1. 固定节点不出现在【添加节点】

新 authoring 菜单中隐藏：

### Story Flow

```text
【开始】
```

### Session

```text
【起始】
```

### Task

```text
【结算】
```

它们由资源创建流程自动存在，不是用户放置节点。

优先根据现有 `Required / Unique / NonDeletable` 元数据过滤。

## H2. 【选择】恢复正常可添加

移除 UI 对 Session `choice` 的特殊禁止。

验证：

- 菜单中【选择】正常可点击；
- 创建后默认至少一个合法选项；
- Flow / Logic 动态端口使用 stable ID；
- 连续添加多个【选择】合法。

## H3. Session【起始】移除旧 ◆输出

新建 / 正常 authoring：

```text
【起始】
  ● 流程输出
```

不再出现：

```text
◆ Logic Out
```

外部逻辑统一通过：

```text
【逻辑输入】◆
```

进入 Session。

### 旧数据兼容

若已有 0.3.1.0 项目包含 `start.logic_out`：

- 不允许简单删除然后断线；
- loader 可继续兼容；
- Studio migration 尽量转成明确【逻辑输入】；
- 无法自动判定语义时生成迁移问题，不静默猜测。

## H4. 【结算】结果槽自动命名

连续按 `+`：

```text
结果 1
结果 2
结果 3
...
```

寻找第一个未占用标准名。

例如已有：

```text
结果 1
完美完成
结果 3
```

新增应得到：

```text
结果 2
```

## H5. Start 至少保留一条启动方式，但默认条目不是永久不可删

规则：

- 只有 1 条启动方式时，【删除】禁用或提示“至少保留一条启动方式”；
- 有 2 条及以上时，任何一条都可以删除，包括最开始自动生成的“进入区域”；
- 删除有连线的启动方式前确认；
- 确认后删除对应 port 和 connections；
- 其它启动方式 stable ID 不变化。

## H6. “进入故事”从新 Start 类型选择中彻底消失

正常下拉只允许当前正式支持的启动方式，例如：

```text
角色交互
进入区域
逻辑条件
```

`enter_story`：

- 不得出现在新建选项；
- 旧项目出现时显示兼容 / 迁移状态；
- 不能再出现“UI 允许选择 → Core 立即报 unsupported”的矛盾体验。

---

# 10. Work Package I — 中文化与诊断展示

解决 #17、#29，并清理普通 authoring 页面的代码术语。

## I1. Port DisplayName 中文化

用户可见：

```text
Flow In        → 流程输入
Flow Out       → 流程输出
Logic In       → 逻辑输入
Logic Out      → 逻辑输出
True           → 是
False          → 否
Logic Complete → 完成
```

**只改 display name，不改稳定 port ID。**

## I2. 普通节点 / 资源 UI 中文化

清理普通作者界面的：

```text
Actor
Actor ID
Story
Flow In
Logic Out
actor_id
speaker_actor_id
resource_id
```

内部类名、JSON 字段名、Validation code 可以继续英文。

## I3. 错误至少中文可读

不要重写所有 Core error code。

推荐在 WPF presentation 边界根据 `ValidationIssue.Code` 映射成中文作者提示。

示例：

```text
一个逻辑输入只能有一个来源。
[graph.connection.logic.input.multiple_sources]
```

这样：

- 用户看到中文；
- 开发者仍可用 code 定位。

本版本不要求建立完整国际化框架。

最低覆盖：

- Graph connection；
- node authoring；
- dynamic port；
- Story Start；
- Objective；
- resource；
- save / migration；

当前 Studio 正常 authoring 可能触发的错误必须至少有中文说明。

未知错误可以：

```text
操作失败（错误代码：xxx）
技术详情：<原始英文>
```

但普通提示不能只有英文。

---

# 11. Work Package J — Inspector 与视觉一致性

收尾 #1、#17、#23、#29 的视觉部分。

## J1. Inspector 统一表单层级

推荐：

```text
分组标题
字段名称
控件
短说明 / 错误
```

不要：

```text
裸 TextBox
裸 ComboBox
靠 Tooltip 才知道是什么
```

## J2. 控件主题统一

重点检查：

- ComboBox；
- TextBox；
- ScrollViewer；
- Expander；
- ContextMenu；
- selected resource；
- disabled state；
- validation state。

Dark / Light 都必须可读。

## J3. 不追求花哨动画

0311 视觉优先级：

```text
不闪烁
> 不错位
> 选中状态清楚
> 可操作
> 风格一致
> 动画好看
```

---

# 12. 建议施工顺序

Codex 按下列顺序施工，避免不同修复相互覆盖。

## Stage 0 — 冻结 0.3.1.0 基线并建立 0311 分支

动作：

1. 确认基线 commit：
   `96c821d1a2b4038fecdc62bdbbbd8078291a8d0e`
2. 新建：
   `codex/0.3.1.1`
3. 完整跑当前 Studio tests。
4. 保存一份人工审计复现项目。
5. 记录 29 条问题的可复现 / 不可复现状态。

输出一份简短记录：

```text
PLAN/DarkGrey_RPG_0.3.1.1_Root_Cause_Record.md
```

只需记录：

```text
问题
是否复现
根因
目标文件
最终修复
```

**禁止把它扩展成新测试平台。**

Gate：

- 基线能构建；
- 当前已有失败与 0311 新修改可以区分。

## Stage 1 — Workspace 稳定性

优先完成：

- B1-B8；
- 资源增量更新；
- Graph 增量 visual；
- 去导航保存 Gate；
- 资源 selected state。

原因：

> 如果整界面仍反复 Rebuild，后面的拖线、动画、选择与 Inspector 验收都不可靠。

Gate：

- 新建四种资源时已有资源不闪；
- 新增节点不整图重建；
- Story / Session / Task 连续切换无保存弹窗；
- 返回后未保存编辑仍在；
- 资源选中态稳定。

## Stage 2 — Graph 核心交互

完成：

- C1-C12；
- node context menu；
- drag ghost；
- real-wire drag；
- multi-wire endpoint drag；
- scissors；
- Left Alt；
- node delete confirm；
- port anchors；
- #26 root-cause 修复。

Gate：

通过第 14 章 Graph 相关人工矩阵。

## Stage 3 — Authoring 模型收口

完成：

- E1-E4；
- F1-F4；
- G1-G6；
- H1-H6。

重点：

- 角色 individual / collective 真正进入创建 UI；
- raw Actor ID 消失；
- Objective 接 DGR 角色 / 物品；
- GiveItem 接个体 Item；
- Session 起始旧 Logic Out 不再新建；
- fixed nodes 从 palette 消失；
- Choice 可用；
- Settlement 连续添加；
- Start 删除规则；
- EnterStory legacy 隔离。

Gate：

从空 Story 创建一个完整 authoring demo，全程不输入任何 `minecraft:*` 或 Graph GUID。

## Stage 4 — Shell / Inspector / 中文视觉收尾

完成：

- A1-A5；
- I1-I3；
- J1-J3。

Gate：

- 主窗口空间明显简化；
- 无冗余“故事 / 设置”app rail；
- 无大标题浪费空间；
- Inspector 每个字段有名称；
- 普通作者页面不再大量出现英文/内部字段；
- Dark / Light 均正常。

## Stage 5 — 全量回归

完成：

- 单元测试；
- WPF STA tests；
- 保存 / 重启；
- migration；
- UI 人工验收；
- Release build。

不要求进入 Minecraft 做完整游戏内功能验收。

如果本轮 schema compatibility 修改触及 Java：

- 跑 Java build；
- 跑受影响 parser/runtime probe；
- 只证明“不因 Studio 0311 输出而破坏当前 Runtime”；
- 不借机开始 Minecraft 侧 UI / 体验整改。

---

# 13. 必须新增 / 修正的自动化测试

## 13.1 Resource Library

至少覆盖：

- 新增角色不重建其它 folder item identity；
- 新增物品不重建其它 folder；
- Session / Task 增删保持无关项与选中项稳定；
- Items 右键分类为 Items；
- selected state 与 Inspector 一致。

## 13.2 Graph Incremental Update

至少覆盖：

- AddNode 不触发 full visual reset；
- AddConnection 不重建全部 node controls；
- RemoveConnection 保留 node visual；
- viewport / selection 保持。

可以通过稳定 visual instance / event counter seam 验证。

不要把 screenshot pixel diff 作为唯一证据。

## 13.3 Node Palette

Story：

- 不出现【开始】；
- 不出现 compatibility【进入故事】。

Session：

- 不出现【起始】；
- 【选择】可创建。

Task：

- 不出现【结算】；
- 不出现【激活】；
- 【目标】可创建。

## 13.4 Settlement

连续按 `+`：

```text
结果 1
结果 2
结果 3
```

重命名、删除、再新增仍不冲突。

stable port ID 不随 display name 改变。

## 13.5 Start

- 一条 trigger 时删除被拒绝并中文提示；
- 两条时第一条可删；
- 删除有 connection 的 trigger 需要确认；
- `enter_story` 不在 Supported UI choices；
- legacy enter_story data 可加载，不可新建。

## 13.6 Session Start

新建 Session：

```text
start
→ 只有 Flow Out
```

外部 Logic 使用【逻辑输入】。

旧 `start.logic_out` fixture 不静默损坏。

## 13.7 Graph Cardinality

必须覆盖 C12 四种合法 / 非法组合。

## 13.8 Node Deletion

- 删除无连线节点 → confirm → 删除；
- 删除有 1 条线节点 → confirm 显示 1 → 节点 + 线删除；
- 删除多线节点 → 数量正确；
- Cancel → 什么都不改；
- Undo → 节点和线一起恢复。

## 13.9 Role / Item Authoring

角色创建：

```text
个体 → NPC ID
集体 → Group ID
```

物品：

```text
个体 → Item ID
集体 → Group ID
```

不出现旧 Actor ID 作为主要作者字段。

## 13.10 Objective Resource Target

- kill 可以从合法角色资源设置；
- collect 可以从合法物品资源设置；
- interact 可以设置角色资源；
- resource drag 生效；
- 不合法类型 drop 不污染数据；
- 普通 UI 不要求输入 `minecraft:slime` / `minecraft:stone`。

## 13.11 Give Item

只允许 Individual Item。

Item Group：

- picker 不允许；
- drop 被拒绝；
- 提示中文。

---

# 14. 0.3.1.1 人工 Studio 验收矩阵

Codex 自动测试通过后，必须自己操作 Studio 完成一轮；**无需 Minecraft。**

## Case S1 — 新建项目 / 新建故事

1. 打开 Studio。
2. 新建空项目。
3. 新建故事。
4. 单一“新建故事”操作视觉居中。
5. 打开项目后无冗余 app-level“故事 / 设置”侧栏。
6. Window Title 显示项目名。

## Case S2 — 四类资源库

创建：

- 个体角色；
- 集体角色；
- 个体物品；
- 集体物品；
- Session；
- Task。

观察：

- 已有资源不闪；
- folder 不整体刷新；
- 选中态保留；
- Inspector 与选中资源一致；
- 右键菜单类型正确。

## Case S3 — 角色创建

个体：

```text
NPC ID = tavern_boss
显示名称 = 酒馆老板
```

集体：

```text
Group ID = slimes
显示名称 = 史莱姆
```

全程不出现旧 Actor ID authoring。

## Case S4 — 资源拖入 Story Flow

拖 Session：

- 一开始拖就出现聚合节点 ghost；
- ghost 跟鼠标；
- 松手后节点正式创建。

Task 同样。

Cancel drag 不产生脏节点。

## Case S5 — 局部图导航

- 双击聚合 Session → Session Graph；
- breadcrumb → Story Flow；
- 双击聚合 Task → Task Graph；
- 右键资源 → 编辑；
- 不出现“请先保存”。

在 Session 中改一段文字不保存，切 Story，再切回，内容仍在。

## Case S6 — Palette

Session 添加菜单：

- 没有【起始】；
- 有可用【选择】。

Task：

- 没有【结算】；
- 没有【激活】；
- 有【目标】。

Story：

- 没有可放置【开始】；
- 没有 EnterStory。

## Case S7 — Wire 新连接

分别从：

- 空 Flow Output；
- 空 Flow Input；
- 空 Logic Output；
- 空 Logic Input；

开始一次连接。

全部使用实线。

## Case S8 — Wire 重接

单线：

- 拖 source endpoint；
- 拖 target endpoint；
- blank drop 断线；
- valid same-direction port drop 重接。

## Case S9 — 多线端点

Flow Input 接入 3 条线：

- 拖 Input；
- 3 条真实线一起移动；
- blank drop → 3 条全部断开。

Logic Output 发出 3 条线：

- 拖 Output；
- 3 条一起移动；
- blank drop → 全部断开。

## Case S10 — Scissors

- 点剪刀 → cursor 变化；
- 点线 → 断线；
- hover 显示 Alt；
- Left Alt hold → 临时剪刀；
- 松开恢复；
- 无异常白框。

## Case S11 — 删除节点

分别测试：

- 无线；
- 单线；
- 多线。

每次都先确认。

有线时提示连线数量。

## Case S12 — 端口对齐

建立动态名称：

```text
是
普通完成
这是一个比较长的结果名称
```

所有 output anchor X 坐标一致。

所有 input anchor X 坐标一致。

## Case S13 — Flow / Logic 基数

按 C12 四种结构实际接线。

不能出现“既定合法结构报错”。

## Case S14 — Settlement

连续添加 5 个结果槽，无重名错误。

重命名中间项后继续新增仍正常。

## Case S15 — Start

1. 初始“进入区域”；
2. 新增“角色交互”；
3. 删除最初“进入区域”；
4. 成功；
5. 删除到只剩最后一条时拒绝；
6. 下拉中没有“进入故事”。

## Case S16 — Objective

创建：

```text
击杀：史莱姆 Group ×10
收集：铜币 Item ID ×10
交互：酒馆老板 NPC ID
```

优先使用：

- picker；
- 资源拖入。

普通 UI 中不得要求输入 Minecraft registry name。

## Case S17 — Give Item

动作：

```text
给予物品
铜币
10
```

只能选择个体 Item ID。

## Case S18 — 中文

从 Story → Session → Task → Problems 全部巡查：

- 无大面积 Flow In / Logic Out；
- 无普通 `actor_id`；
- 错误至少有中文说明；
- 技术 code 可以附在后面。

## Case S19 — 保存 / 重启

1. 完成一组 Story / Session / Task 编辑。
2. 保存全部。
3. 关闭 Studio。
4. 重新打开。
5. Graph layout、端口、结果槽、资源身份、Start trigger 均保持。
6. 不出现 ID 漂移 / 接线丢失。

---

# 15. 用户 29 条人工反馈 Traceability

本表只用于最终防遗漏，不代表施工顺序。

| 原反馈 | 本 PLAN 处理位置 |
|---|---|
| 1 新建故事按钮不居中 | A1 / J |
| 2 大标题、故事/设置侧栏冗余 | A2 / A3 |
| 3 Flow 只能左右、不能上下调 | A4 / U1 |
| 4 新建资源已有资源闪动 | B1 / B2 |
| 5 资源库展开动画卡 | B3 |
| 6 节点右键无菜单 | C1 |
| 7 资源拖入没有节点跟随 | C2 |
| 8 连线变虚线预览 | R2 / C3-C7 |
| 9 剪刀 + Left Alt | C8 / C9 |
| 10 到处显示内部编号 | R10 / E3 / I |
| 11 双击聚合节点、右键编辑 | D1 / D2 |
| 12 删除节点因连线报错 | C10 |
| 13 到处 Return Story Flow | A5 / D |
| 14 Actor_id、参数全藏 Inspector、无法拖角色 | E1 / F |
| 15 Alt 白框 | U2 / C9 |
| 16 角色没个体/集体创建 | R7 / E2 |
| 17 未汉化、英文报错 | I |
| 18 固定节点出现在添加菜单、Choice 变灰 | R3 / R4 / H1 / H2 |
| 19 切图频繁要求保存 | R12 / B6 |
| 20 资源选中无持久框 | B7 |
| 21 结算第二个结果就重名 | R5 / H4 |
| 22 节点添加像逐步刷新 | R1 / B4 / B5 |
| 23 Objective ComboBox 风格、目标类型疑问 | G1 / G2 / J |
| 24 Objective 仍用 minecraft:* | R6 / F3 / G |
| 25 输出端口因名字长度错位 | C11 |
| 26 标准连接报错 | U3 / C12 |
| 27 初始进入区域永远删不掉 | H5 |
| 28 EnterStory trigger 一选就报错 | R9 / H6 |
| 29 Inspector 字段没有名称 | F2 / J1 |

---

# 16. 源码审计补充 A / B / C / D Traceability

## A — Session【起始】旧 ◆输出复发

→ `R8 / H3`

必须修。

## B — 【物品】资源右键 fallback 成【角色】

→ `R11 / B8`

必须修。

## C — 固定节点菜单源错误

→ `R3 / H1`

不要只在 XAML 按节点名隐藏，要从 authoring source 修正。

## D — 旧 Actor UI 与新 Identity 模型并存

→ `R7 / E`

必须完成 UI 收口。

---

# 17. 与已知问题同根、必须一并处理的项目

这些不是扩张范围，而是如果不处理，会让已知问题继续复发。

## 17.1 【动作：给予物品】不得继续自由填写 raw item string

原因与 #24 同根。

→ `F4`

## 17.2 普通 Graph Node Header 不显示 NodeId

原因与 #10 同根。

## 17.3 Inspector 顶部不再把内部 NodeId / resource internal ID 当主要信息

有意义的 NPC ID / Group ID / Item ID 可以显示。

Graph 技术 ID 默认隐藏。

## 17.4 Start 高频参数不能只藏在 Inspector

原因与 #14 / #29 同根。

---

# 18. 防止 Codex 无限扩张的施工约束

这是 0.3.1.1 的硬规则。

## 18.1 优先修现有实现，不重写整个 Studio

禁止因为：

```text
CanonicalGraphEditorView.xaml.cs 太长
CanonicalStoryWorkspaceViewModel 太大
```

就主动重做整个 MVVM / WPF 架构。

允许局部拆方法 / 小类，但必须直接服务于本轮问题。

## 18.2 禁止“顺手平台化”

本轮不得新建：

- Generic Inspector Framework；
- Node Parameter DSL；
- UI Plugin System；
- Dev Harness；
- Scenario Registry；
- Drag Framework；
- Graph Rendering Engine v2；
- 全局 AutoSave Service；
- 全局 Localization Framework。

需要什么就做**最小直接实现**。

## 18.3 不新增未批准产品能力

尤其禁止：

- 因 #23 自动新增十几种 Task Objective；
- 因 Inspector 修缮加入通用变量系统；
- 因剪刀加入整套 toolbar framework；
- 因拖拽 ghost 重写整个 drag-and-drop 层。

## 18.4 不改变已经锁定的 Graph 基数规则

必须保持：

```text
●：输出单去向，输入多来源
◆：输出多去向，输入单来源
```

## 18.5 不重新引入已经删除的概念

不得重新引入：

- Task【激活】；
- Task ●流程；
- 【并发】；
- 【返回】；
- 额外【入口】；
- 新 authoring【进入故事】；
- 隐式 AND / OR；
- 旧 Actor property 模型。

## 18.6 不把本轮完成写成“Studio 已冻结”

Development Report 最终只能写：

> **0.3.1.1 已完成本轮 Studio 修缮范围。**

不能写：

> Studio 已最终完成 / 已冻结 / 不再需要修复。

冻结只能由用户后续明确宣布。

---

# 19. Definition of Done

0.3.1.1 只有同时满足以下条件才可提交“完成候选”。

1. 人工审计 29 条全部有明确处理结果。
2. A / B / C / D 四个源码审计补充全部修复。
3. 不再因为普通集合变化整图 Rebuild。
4. 资源创建不再造成整棵资源库明显闪动。
5. Story / Session / Task 导航不再频繁弹保存确认。
6. 资源 selected state 清楚稳定。
7. 节点右键至少有真实【编辑】【删除】能力。
8. Session / Task aggregate 双击可进入局部图。
9. 资源 drag 有跟随鼠标的 node ghost。
10. wire drag 使用实线真实连线语义。
11. 多连接 Flow Input / Logic Output 可整体拖动对应 incident wires。
12. 剪刀和 Left Alt 工作，Alt 白框消失。
13. 删除节点确认后节点与连线一并删除。
14. 动态端口 anchor 与文字长度无关。
15. #26 的实际根因被找到并修复，既定 cardinality 不改变。
16. 创建角色明确选择个体 / 集体。
17. 普通 UI 不再以 Actor ID / actor_id 为作者概念。
18. Graph Node ID / aggregate GUID 等内部 ID 默认隐藏。
19. fixed required nodes 不再出现在 Add Node palette。
20. Session【选择】可正常创建。
21. Session【起始】新 authoring 不再有旧 ◆ Logic Out。
22. Settlement 连续新增结果不重名。
23. Start 默认 trigger 在存在其它 trigger 时可删除。
24. EnterStory 不再出现在新的 Start trigger 选项。
25. Objective UI 风格统一。
26. Objective 正常 authoring 使用 DGR 角色 / 物品资源，而不是要求 `minecraft:*`。
27. GiveItem 使用 DGR 个体 Item ID。
28. Start / Objective 等高频参数可在节点本体看到，并支持必要资源 drop。
29. Inspector 所有主要控件有明确中文字段名。
30. 普通作者可遇到的 Validation 至少有中文解释。
31. Dark / Light、1100×700、1700×980、100% / 125% / 150% DPI 基本可用。
32. 保存、关闭、重开后 Graph / 端口 / 资源身份不漂移。
33. Studio unit / WPF tests 全通过。
34. 若触及 canonical serializer / Java parser，相关 Java build / probe 通过。
35. 没有为了本轮修复引入大规模新架构。

**DoD 不包含“Studio 冻结”。**

---

# 20. 最终交付物

Codex 完成本轮后必须提供：

1. `0.3.1.1` 完整源码；
2. `PLAN/DarkGrey_RPG_0.3.1.1_Development_Report.md`；
3. `PLAN/DarkGrey_RPG_0.3.1.1_Root_Cause_Record.md` 最终状态；
4. 自动测试结果；
5. 第 14 章人工 Studio acceptance 结果；
6. 未解决 / 无法复现问题清单；
7. Release build / Studio 可运行产物路径；
8. 最终 commit SHA。

---

# 21. GitHub 最终要求

施工完成后：

1. 将源码、文档、测试修改完整提交到 Git；
2. 推送到 GitHub；
3. 推荐使用分支：

```text
codex/0.3.1.1
```

4. 不得只把本地测试结果口头报告给用户而不上传源码；
5. GitHub 最终状态必须足够让下一次审计直接读取；
6. 未经用户明确授权，不必自动合并 `main`、创建 tag 或正式 Release；
7. 最终回复必须给出：
   - 分支名；
   - commit SHA；
   - 测试结果摘要；
   - GitHub 已推送确认；
   - 仍待用户人工确认的问题。

---

# 22. 一句话验收标准

> **0.3.1.1 的目标不是宣布 Studio 完成，而是把当前已经明确暴露的 Studio 交互、刷新、节点编辑、旧新 authoring 模型冲突和中文可用性问题系统性修正，让用户拿到一个明显更稳定、更一致、能够继续进行下一轮人工审计的 Studio。**
