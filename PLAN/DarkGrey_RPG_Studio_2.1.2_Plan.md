# DarkGrey RPG Studio 2.1.2 开发计划

> **版本定位**：对 2.1.1 节点图优化进行完整性修复，重点补齐连接状态机、分支端口模型、参数可发现性、画布可靠性，以及 Project Story Graph 中同样存在的共享交互问题。
> **核心目标**：让 Story Flow 从“可以画节点”提升为“连接语义可靠、分支能力完整、鼠标交互稳定、旧项目可兼容”的正式节点编辑器。
> **基线版本**：用户当前本地运行的 DarkGrey RPG Studio 2.1.1。
> **远端注意事项**：GitHub `GreyHat633/DarkGrey_RPG` 当前远端可能尚未包含用户截图中的完整 2.1.1 实现；Codex 必须以本地现有 2.1.1 工作区为真实基线，禁止为了与旧远端一致而丢弃本地成果。
> **Runtime 边界**：本计划默认不新增 Java Runtime 节点类型，而是让 Studio 正确表达 Runtime 已有的控制流语义。只有发现 Studio 与 Runtime 契约存在真实缺陷时，才允许最小化修改 Runtime。
> **计划状态**：待实施。

---

## 1. 版本目标

2.1.2 不是一次纯视觉修补。它要解决 2.1.1 中仍然存在的四类根本问题：

1. **连线交互只完成了一半**
   - 只能从输出端拖线；
   - 输入端无法发起连接；
   - 已有连接无法从任意端重新接线；
   - 松开、取消、断线、撤销的语义不完整。

2. **鼠标手势仍然互相干扰**
   - 操作端口后中键平移可能卡死；
   - 拖线、节点拖动、框选、平移和右键菜单之间缺少统一状态机；
   - 鼠标捕获异常时不能稳定回到空闲状态。

3. **Studio 的端口模型仍未完整反映 Runtime**
   - `Branch`、`QuestState`、`HasItem`、`VariableCompare` 应有 `true / false` 两个输出；
   - `DialogueExitBranch` 应显示所有命名出口；
   - `Sequence` 应显示 `1 / 2 / 3...`；
   - 旧 `PlayDialogue` 命名 Result 连接必须保留；
   - 同一个普通 `next` 端口不能靠多条连线表达不确定分支。

4. **节点和画布的可发现性、可读性仍不足**
   - 参数折叠后完全不可见；
   - 端口悬停出现方形描边；
   - 次级菜单字体模糊；
   - 大型 Flow 仍可能受到固定画布、裁切、问题定位和草稿恢复不足的影响；
   - Project Story Graph 中仍存在类似的平移、缩放、画布边界、菜单渲染和图边表达问题。

版本完成后应达到以下用户体验：

- 从输入端或输出端都能创建新连接；
- 从已有连接的任意一端都能重新接线；
- 将已有连接拖到空白处可以断开；
- `Esc` 可以取消重接并恢复原连接；
- 中键平移始终可靠，不需要滚轮“解锁”；
- 端口 Hover 只有圆形放大/高亮，不出现方形边框；
- 节点折叠后仍明确显示“这里有参数”和参数摘要；
- 分支节点具有明确、具名、始终可见的多输出端口；
- 所有 Runtime 已支持且适合作者创建的节点都能从菜单中找到；
- Project Story Graph 保持逻辑只读，但共享画布交互与 Flow 一致，并能正确表示平行转场、自环、方向和循环。

---

## 2. 不可违反的设计原则

### 2.1 Flow 是唯一逻辑编辑源

Story Flow 仍是 Story 内逻辑的唯一 Source of Truth。

Project Story Graph 必须继续：

- 从 `EnterStory` 派生 Story 间的边；
- 只允许调整编辑器布局；
- 不允许创建、删除或重接逻辑边；
- 不允许通过图谱操作写入 Story Flow。

### 2.2 当前端口都是控制流端口，不做数据类型颜色编码

本系统当前没有着色器编辑器式的 Float、Vector、Color、Actor 等数据流端口。端口表达的是控制流：

- `next`
- `true / false`
- Dialogue Result 名称
- Sequence 序号

因此：

- 所有控制流端口使用统一基础颜色；
- 不使用“黄色只能接黄色、蓝色只能接蓝色”一类类型配色；
- 颜色仅用于交互状态：
  - 普通；
  - Hover；
  - 正在拖动；
  - 合法目标；
  - 非法目标；
  - 选中；
  - 错误/警告。

端口语义由位置和标签表达，而不是虚构不存在的数据类型限制。

### 2.3 节点之间保持自由组合，限制落在端口语义和图完整性上

2.1.2 不引入类似以下硬编码规则：

- `StartQuest` 后必须连接 `WaitQuestComplete`；
- `PlayDialogue` 前必须连接 `ActorInteract`；
- `GiveItem` 后只能连接 `End`。

允许作者自由组合节点，只要满足：

- 只能由输出端连接到输入端；
- 连接端点存在；
- 输出名称对当前节点有效；
- 同一个 `from + output` 最多一条连接；
- 终止节点不能有输出；
- 条件节点的必要分支完整；
- 整体 Flow 满足 Runtime 图验证。

### 2.4 一次手势只允许一个主状态

任何时刻只允许一个主交互状态：

```text
Idle
NodeDrag
BoxSelect
WireDrag
Pan
```

不允许多个布尔状态同时残留。

### 2.5 一次重接必须是一次撤销事务

“拆下旧端点并连接到新端点”属于一个原子编辑：

```text
旧连接 A → B
拖动 B 端到 C
新连接 A → C
```

一次 `Ctrl+Z` 必须完整恢复 `A → B`，不能拆成两次撤销。

### 2.6 参数折叠不能等于参数消失

任何带参数的节点在折叠状态下都必须显示：

- 可点击的折叠标题；
- 参数数量；
- 参数类型摘要或当前值摘要。

### 2.7 旧项目数据不能因 UI 重构被静默改写或丢失

旧节点类型、旧输出名称、旧连接、未知但 Runtime 支持的属性必须：

- 可加载；
- 可显示；
- 可移动；
- 可保存；
- 不得被自动改写成别的节点类型；
- 不得因为新 UI 没有对应端口而静默删除连接。

---

## 3. 统一的端口与连接契约

## 3.1 连接数据的唯一方向

无论用户从输入端还是输出端开始拖动，最终持久化的数据始终是：

```text
source node.output → target node.input
```

输入端反向发起拖动只是一种操作方式，不改变逻辑方向。

## 3.2 连接基数

### 输出端

- 一个具体输出端最多连接一条线；
- `node + output` 是唯一键；
- 再连接同一输出时属于重接/替换，不允许同时扇出到多个目标。

### 输入端

- 一个节点输入端允许接收多条来源线；
- 多条入线表示“多个路径都可以到达此节点”；
- 不表示并行同步；
- 不表示必须等待所有前置路径完成。

### 特殊节点

- `StoryStart`：无输入，通常有一个 `next` 输出；
- `End`、`EndStory`、`EnterStory`：有输入，无输出；
- 其它普通节点：一个输入，按节点类型提供输出。

## 3.3 标准输出端口表

Studio Core 中必须建立统一的节点描述注册表，至少覆盖以下端口契约：

| Canonical Type | 输入 | 输出 |
|---|---:|---|
| `StoryStart` | 无 | `next` |
| `ActorInteract` | 有 | `next` |
| `EnterRegion` | 有 | `next` |
| `PlayDialogue` | 有 | 默认 `next`；兼容旧命名 Result |
| `DialogueExitBranch` | 有 | 动态命名出口 |
| `StartQuest` | 有 | `next` |
| `WaitQuestComplete` / `QuestCompleted` | 有 | `next` |
| `CompleteQuest` | 有 | `next` |
| `Branch` | 有 | `true`, `false` |
| `QuestState` | 有 | `true`, `false` |
| `HasItem` | 有 | `true`, `false` |
| `VariableCompare` | 有 | `true`, `false` |
| `Sequence` | 有 | `1`, `2`, `3`... |
| `GiveItem` | 有 | `next` |
| `GiveXp` | 有 | `next` |
| `SendMessage` | 有 | `next` |
| `SetVariable` | 有 | `next` |
| `EnterStory` | 有 | 无 |
| `EndStory` | 有 | 无 |
| `End` | 有 | 无 |

### `PlayDialogue` 兼容规则

新建 Flow 推荐：

```text
PlayDialogue.next
→ DialogueExitBranch
→ accept / refuse / ...
```

但旧项目可能存在：

```text
PlayDialogue.accept → A
PlayDialogue.refuse → B
```

2.1.2 必须：

- 保留现有连接所使用的命名 Result 端口；
- 在节点上标记为兼容端口；
- 允许检查、重接和删除；
- 保存时不得静默转换或丢失。

## 3.4 动态端口

### DialogueExitBranch

端口来自配置的命名出口，例如：

```text
accept
refuse
threaten
```

要求：

- 每个出口独立显示标签和圆形输出端；
- 参数折叠时出口端口仍始终可见；
- 改名或删除出口时，必须处理已有连接。

### Sequence

端口来自步骤数量或现有连接：

```text
1
2
3
```

要求：

- 节点内提供“添加步骤”“删除最后一步”或等价操作；
- 删除仍有连接的步骤时必须确认；
- 现有数字输出必须按数值排序；
- 旧项目中出现的数字端口必须保留。

## 3.5 分支设计的正确方式

普通 `next` 不允许一对多，因为 Runtime 无法确定执行语义。

作者需要通过多输出节点表达分支。

### 对话选择分支

```text
PlayDialogue: npc_offer
        │ next
        ▼
DialogueExitBranch
   ├─ accept → GiveItem: coin ×10 → EnterStory: happy_ending
   └─ refuse → GiveItem: apple ×1  → EnterStory: bad_ending
```

### 条件分支

```text
QuestState
├─ true  → PlayDialogue: completion
└─ false → SendMessage: 任务尚未完成
```

### 变量结局分支

```text
VariableCompare
├─ true  → EnterStory: happy_ending
└─ false → EnterStory: bad_ending
```

UI 中：

- 唯一 `next` 可以只显示圆点；
- `true / false` 必须显示文字；
- Dialogue Result 必须显示文字；
- Sequence 序号必须显示。

---

## 4. Story Flow 修复范围

## F-01：输入端与输出端均可发起新连接

### 新连接

从未连接端口开始：

```text
按下端口
→ 绘制实线临时连接
→ 自由端跟随鼠标
→ Hover 合法异侧端口
→ 松开创建连接
```

必须支持：

- 输出端 → 输入端；
- 输入端 → 输出端。

禁止：

- 输入端 → 输入端；
- 输出端 → 输出端；
- 节点自连；
- 终止节点作为来源；
- `StoryStart` 作为目标输入。

临时线使用与正式线一致的：

- 实线；
- 颜色体系；
- 粗细；
- 贝塞尔样式。

不得使用虚线作为拖线主体。

## F-02：已有连接支持双端重接

### 从输出端重接

输出端最多只有一条连接，因此：

```text
拖动已连接输出端
→ 原目标端暂时脱离
→ 输出端保持固定
→ 松到新输入端后替换目标
```

### 从输入端重接

输入端可能有多条入线。

#### 只有一条入线

```text
拖动输入端
→ 原来源端暂时脱离
→ 输入端保持固定
→ 松到新输出端后替换来源
```

#### 多条入线

禁止静默选择“最后一条”或“第一条”。

推荐实现：

1. Hover 输入端时显示临时的“入线扇出句柄”；
2. 每条入线在输入端附近出现一个独立小圆形端点句柄；
3. 拖动具体句柄时重接对应连线；
4. 拖动中央输入端本身时发起一条新连接。

若实现扇出句柄成本过高，可退化为来源选择小列表，但必须满足：

- 用户明确知道正在重接哪一条；
- 不得无提示改动其它入线。

## F-03：重接、断线、取消语义

### 新连接

- 松开到合法端口：创建；
- 松开到空白：取消；
- `Esc`：取消；
- 窗口失焦：取消；
- 打开右键菜单：取消。

### 已有连接重接

- 松开到合法端口：重接；
- 松开到空白：断开并删除原连接；
- `Esc`：恢复原连接；
- 窗口失焦：恢复原连接；
- 打开右键菜单：恢复原连接。

### Undo/Redo

- 新建连接：一步 Undo；
- 删除连接：一步 Undo；
- 重接连接：一步 Undo；
- 动态出口改名并迁移连接：一步 Undo。

## F-04：连接手势状态机重构

禁止继续以多个互相独立的布尔值控制手势。

建议：

```csharp
enum GraphPointerMode
{
    Idle,
    NodeDrag,
    BoxSelect,
    WireDrag,
    Pan
}
```

并增加：

```csharp
sealed class WireDragSession
{
    ConnectionEndpoint FixedEndpoint;
    ConnectionEndpoint MovingEndpoint;
    StoryFlowConnectionEditorItem? OriginalConnection;
    WireDragKind Kind; // New / ReconnectSource / ReconnectTarget
}
```

统一清理入口：

```csharp
CancelPointerGesture(bool restoreOriginalConnection)
```

必须在以下时机调用：

- 左键松开；
- 中键松开；
- `Esc`；
- `LostMouseCapture`；
- Window `Deactivated`；
- View `Unloaded`；
- DataContext 改变；
- 节点视觉重建；
- 右键菜单打开；
- 切换 Story；
- 关闭项目。

## F-05：修复端口操作后中键平移卡死

具体要求：

- 不再保留“点击一次端口即武装 PendingConnection”的模式；
- 端口必须使用按下—拖动—松开的完整手势；
- 中键按下拥有最高优先级；
- 如果存在尚未完成的新连接拖动，中键按下时取消该拖动并进入 Pan；
- 如果存在已有连接重接，进入 Pan 前恢复原连接；
- 中键松开后必须稳定回到 `Idle`；
- 不允许依赖滚轮缩放或 RebuildGraph 才恢复平移；
- 端口 Hover、选中状态和鼠标捕获均不得阻止中键事件进入 Canvas。

## F-06：端口视觉重做

端口不能直接使用带默认 Chrome 的普通 Button。

推荐结构：

```text
20×20 透明 Hit Area
└─ 8×8 Ellipse
```

### 视觉状态

| 状态 | 视觉 |
|---|---|
| Normal | 8px 圆点 |
| Hover | 10—11px 圆点 |
| WireDrag Source | 11px、高亮 |
| Valid Target | 11px、圆形光环 |
| Invalid Target | 8px、低亮度或禁止光标 |
| Connected | 实心或中心标识 |
| Selected Connection Endpoint | 11px、选中描边 |

必须去掉：

- 方形 Hover 背景；
- 方形 Border；
- 默认 Focus Rectangle；
- 默认 Button Pressed 效果；
- 节点布局因端口放大而跳动。

实现要求：

```text
Focusable = false
FocusVisualStyle = null
Background = Transparent
BorderThickness = 0
```

端口放大只作用于圆形视觉，Hit Area 和节点尺寸保持不变。

## F-07：正式连线与临时连线统一渲染

新增统一方法：

```csharp
CreateConnectionGeometry(start, end)
```

正式连接和拖动连接均使用它。

端点必须来自真实端口控件中心：

- 禁止 `node.Y + 82` 一类硬编码；
- 使用 `TranslatePoint()` 或等价坐标转换；
- 节点展开、折叠、DPI 改变后连接仍对齐；
- 连接路径应位于节点后方，但端口位于连接上方；
- 连接 Hit Test 应有比视觉线更宽的透明命中路径。

## F-08：参数折叠改为可发现的 Expander

每个带参数的节点始终显示参数标题。

### 折叠状态

示例：

```text
▸ 参数（2）· kill_10_slimes / NOT_STARTED
```

或：

```text
▸ 参数（2）：任务、状态
```

### 展开状态

```text
▾ 参数（2）
  任务 [ kill_10_slimes ▼ ]
  状态 [ NOT_STARTED     ▼ ]
```

要求：

- 整个标题行可点击；
- 有清楚的 `▸ / ▾`；
- 显示参数数量；
- 显示摘要；
- 参数区域动画不得影响字体清晰度；
- 折叠状态不隐藏分支输出端口；
- 右键“展开参数/收起参数”只是快捷操作，不是唯一入口。

### 参数层级

#### 核心参数，默认可见

- `ActorInteract`：Actor；
- `PlayDialogue`：Dialogue；
- `StartQuest` / `QuestState` 等：Quest；
- `EnterStory`：Target Story。

#### 次级参数，可折叠

- 状态；
- 数量；
- metadata；
- 比较运算符；
- 区域坐标；
- radius；
- 变量值。

#### 高级兼容参数

- 旧节点属性；
- Runtime 支持但 Studio 没有专用编辑器的字段；
- 调试或兼容信息。

## F-09：分支输出始终可见

以下端口属于流程结构，不能与参数一起折叠：

```text
true / false
accept / refuse / ...
1 / 2 / 3...
```

节点折叠后仍必须一眼看出：

- 这是分支节点；
- 有几条分支；
- 每条分支叫什么。

## F-10：动态出口改名和删除保护

以 `DialogueExitBranch` 为例：

原出口：

```text
accept
refuse
```

用户改成：

```text
agree
refuse
```

若 `accept` 已有连接，必须弹出明确选择：

```text
“accept”仍有连接
├─ 将连接迁移到“agree”
├─ 删除旧出口及连接
└─ 取消修改
```

删除出口也必须处理已有连接。

要求：

- 迁移连接保持目标节点不变；
- 迁移属于一次 Undo；
- 不能留下不可见的旧 output；
- 不能静默删除连接；
- 出口名称必须经过 ID/输出名称验证；
- 禁止重复出口名。

## F-11：建立统一 StoryNodeDefinitionRegistry

建议在 Studio Core 中新增：

```text
Stories/Definitions/
├─ StoryNodeDefinition.cs
├─ StoryPortDefinition.cs
├─ StoryPropertyDefinition.cs
└─ StoryNodeDefinitionRegistry.cs
```

每个定义描述：

```text
Canonical Type
Persisted Type
显示名称
分类
是否允许输入
输出策略
核心参数
折叠参数
默认属性
终止性
兼容别名
```

输出策略至少支持：

```text
SingleNext
Boolean
DialogueExits
Sequence
Terminal
LegacyPreserved
```

以下模块必须共用同一注册表：

- 节点添加菜单；
- 节点标题；
- 参数控件；
- 输入/输出端口；
- 连接校验；
- 默认属性；
- 节点摘要；
- Accessibility；
- 单元测试。

不得继续在多个 ViewModel 和 View 中各写一套不同的 `switch`。

## F-12：节点添加菜单完整化

右键菜单和顶部快捷按钮的职责分开：

### 顶部快捷按钮

只保留最常用节点，避免工具栏过长。

### 右键“添加节点”

至少包含：

```text
添加节点
├─ 触发
│  ├─ 剧情开始
│  ├─ 角色交互
│  └─ 进入区域
├─ 条件
│  ├─ 条件分支
│  ├─ 任务状态
│  ├─ 是否持有物品
│  └─ 变量比较
├─ 对话
│  ├─ 播放对话
│  └─ Dialogue Exit 分支
├─ 任务
│  ├─ 开始任务
│  ├─ 等待任务完成
│  └─ 完成任务
├─ 动作 / 奖励
│  ├─ 给予物品
│  ├─ 给予经验
│  ├─ 发送消息
│  └─ 设置变量
├─ 流程控制
│  └─ 顺序执行
├─ 剧情
│  ├─ 进入剧情
│  └─ 结束当前剧情
└─ 结束
```

要求：

- 使用本地化中文显示名称；
- 内部类型只出现在 Tooltip 或高级信息中；
- 支持键盘导航；
- 后续节点增加时只修改注册表，不手写多套菜单。

可选增强：

- 菜单顶部搜索框；
- 最近使用节点；
- 快捷键。

搜索不是 2.1.2 Gate 的硬要求，完整节点覆盖是硬要求。

## F-13：修复次级菜单字体模糊

Context Menu 必须脱离 GraphCanvas 的缩放和位移 Transform。

结构必须类似：

```text
Window UI Layer（固定 100%）
├─ Toolbar
├─ ContextMenu / Popup
└─ GraphViewport
   └─ GraphCanvas（受 Zoom / Pan 影响）
```

WPF 设置：

```text
UseLayoutRounding = true
SnapsToDevicePixels = true
TextOptions.TextFormattingMode = Display
TextOptions.TextRenderingMode = ClearType
```

同时检查：

- Per-Monitor V2 DPI Awareness；
- 子菜单 Popup 的 DPI 上下文；
- ContextMenu 不继承 78%、87% 等 Graph Zoom；
- 菜单坐标落在整数设备像素；
- 不对菜单使用 ScaleTransform 动画；
- 不用自绘位图文本。

必须在以下缩放环境实测：

- Windows 100%；
- Windows 125%；
- Windows 150%；
- Graph Zoom 25%、78%、100%、150%、250%。

## F-14：连接和节点的右键菜单语义

### 节点右键

- 展开/收起参数；
- 复制；
- 创建副本；
- 断开所有连接；
- 删除节点；
- 聚焦节点；
- 查看相关问题。

多选状态下：

- 右键已选节点：作用于全部选中节点；
- 菜单文字显示数量，例如“删除 3 个节点”；
- 右键未选节点：先只选中该节点。

### 连线右键

- 删除连接；
- 聚焦来源节点；
- 聚焦目标节点；
- 查看连接信息。

### 空白右键

- 添加节点；
- 粘贴；
- 全选；
- 适应全部节点；
- 实际大小；
- 重置视图。

打开任何右键菜单前必须结束或取消当前手势。

## F-15：大型 Flow 的无限画布和边界计算

禁止继续依赖固定 `4000 × 2600` 作为真正世界边界。

可选实现：

1. 使用大范围虚拟世界坐标；
2. 根据节点 Bounds 动态扩张 Canvas；
3. 将 Pan/Zoom 与节点世界坐标分离，不依赖固定 Canvas 尺寸。

必须支持：

- 负坐标节点；
- 节点超出当前视口；
- 大型 Flow；
- 展开参数后高度变化；
- Fit All；
- Reset View；
- 100% 实际大小。

`Fit All` 必须使用每个节点的：

```text
Left
Top
ActualWidth
ActualHeight
```

并增加安全边距，不能只使用 `X/Y`。

## F-16：工具栏缩放信息去歧义

当前界面可能同时出现：

```text
100%
适应
重置
− 78% +
```

改为明确的命令和状态：

```text
[实际大小] [适应全部] [重置视图] [−] 78% [+]
```

避免两个百分比同时出现。

## F-17：Problems 直接定位节点和字段

点击 Problems 中的 Flow 问题后：

```text
打开对应 Story
→ 切换到 Flow
→ 平移/缩放聚焦节点
→ 选中节点
→ 展开相关参数组
→ 高亮出错字段
```

问题 Source 至少包含：

```text
story/<story_id>/flow/<node_id>
```

Field 指向：

```text
actor_id
dialogue_id
quest_id
target_story_id
exit_names
...
```

错误计数点击后只打开全局 Problems，不再在 Flow 内重复显示长文本。

## F-18：无效 Flow 草稿恢复

无效 Flow 不能保存为 Runtime Story，但不能因为崩溃丢失大量编辑。

增加 Studio-only Recovery：

```text
resources/editor/recovery/
```

或 `%AppData%/DarkGreyRPG/Studio/recovery/`。

要求：

- 不被 Java Runtime 读取；
- 定时或关键操作后写入编辑器草稿快照；
- 正常保存后清理对应 Recovery；
- 启动时检测未恢复草稿；
- 用户可选择恢复、忽略或删除；
- Recovery 不改变正式 Story JSON。

## F-19：中文显示和内部类型分离

正式 UI 中：

```text
QuestState      → 任务状态
Branch          → 条件分支
SendMessage     → 发送消息
GiveXp          → 给予经验
Actor           → 角色
Dialogue        → 对话
```

内部类型、持久化类型和节点 ID继续保留，但放在：

- 次要文字；
- Tooltip；
- 高级参数；
- Debug 信息。

---

## 5. Project Story Graph 同步修复范围

Project Story Graph 仍然是只读图。只同步适用的问题。

## G-01：共享鼠标状态机

Graph 使用：

```text
Idle
NodeDrag
Pan
```

要求：

- 中键平移；
- 左键拖动布局节点；
- 双击打开 Flow；
- 右键菜单不与拖动冲突；
- `LostMouseCapture`、窗口失焦、View 卸载时恢复 `Idle`；
- 不允许节点拖动后中键失效。

## G-02：共享无限画布、鼠标中心缩放和 Fit All

与 Flow 共用：

- Graph 坐标转换；
- Cursor-centered Zoom；
- Viewport-centered Zoom；
- Pan；
- Fit All；
- Actual Size；
- Reset View；
- 动态 Bounds；
- 最后视口状态。

Graph 操作不得修改 Story JSON。

## G-03：只读 Context Menu 的 DPI 和清晰度

Graph 菜单同样放在 Window UI Layer。

### Story 节点右键

- 打开 Story；
- 打开 Flow；
- 聚焦此节点；
- 复制 Story ID；
- 查看相关诊断。

### 空白右键

- 自动布局；
- 适应全部 Story；
- 实际大小；
- 重置视图。

### 派生边右键/单击

- 查看来源 Story；
- 查看目标 Story；
- 查看来源 `EnterStory`；
- 打开来源 Flow 并定位该节点。

禁止：

- 删除逻辑边；
- 重接逻辑边；
- 创建 Story 节点；
- 直接修改 Flow。

## G-04：派生边可 Hover 和检查

旧实现中若边 `IsHitTestVisible = false`，Tooltip 和检查交互无法可靠生效。

2.1.2 应采用两层路径：

```text
宽透明 Hit Path
+
窄可见 Stroke Path
```

边信息至少显示：

```text
来源 Story
目标 Story
EnterStory 节点 ID
进入该 EnterStory 的分支 output（若可推导）
```

## G-05：方向箭头

Project Graph 没有可编辑端口，必须通过箭头表达方向。

要求：

- 箭头位于目标端；
- 缩放后清晰；
- 不被节点遮挡；
- 平行边和自环仍有方向；
- 不产生“看起来可拖动”的端口圆点。

## G-06：平行转场边

同一 Source Story 到同一 Target Story 可能有多个 `EnterStory`。

例如：

```text
accept       → kingdom
secret_route → kingdom
```

不能让两条曲线完全重叠而看起来只有一条。

推荐宏观聚合：

```text
王城迷案 ──×2──▶ 王国线
```

Hover/检查时列出：

```text
enter_kingdom_from_accept
enter_kingdom_from_secret
```

若不聚合，则使用稳定、对称的偏移曲线。

## G-07：自环

`Story A → Story A` 必须绘制明确自环：

- 自环位于节点上方或右上；
- 有箭头；
- 可 Hover；
- 产生 Cycle Warning；
- 不允许普通左右贝塞尔退化成不可见线。

## G-08：边显示分支原因

若来源 Flow 中：

```text
DialogueExitBranch.accept → EnterStory: kingdom
DialogueExitBranch.refuse → EnterStory: empire
```

Graph 边至少在 Tooltip 中显示：

```text
accept
refuse
```

可选在边上显示轻量标签。

宏观图谱不应只告诉用户“两个 Story 有关系”，还应尽量说明“通过哪个分支进入”。

## G-09：循环安全自动布局

使用 SCC：

1. 构建 Story 有向图；
2. Tarjan/Kosaraju 计算强连通分量；
3. 多节点 SCC 或自环产生 Cycle Warning；
4. 折叠 SCC 得到 DAG；
5. 在 DAG 上分层；
6. SCC 内部使用稳定的小环形或纵向布局；
7. 布局必须在有限时间内终止；
8. 所有坐标必须是有限值。

循环是 Warning，不是 Error。

## G-10：Graph Problems 定位

点击以下诊断：

- Missing Target；
- Isolated Story；
- Cycle；
- Layout Persistence Failure；

应：

```text
聚焦相关 Story 节点
```

Missing Target 还应提供：

```text
打开来源 Story Flow
→ 定位对应 EnterStory 节点
```

## G-11：Graph 多选与右键范围

若保留或新增多选布局：

- 右键已选 Story 时不取消多选；
- 菜单文字显示作用数量；
- “适应所选节点”“自动排列所选节点”不修改逻辑；
- 不提供删除 Story 或连线命令。

---

## 6. 共享基础设施与代码结构

## 6.1 GraphViewportController

建议：

```text
studio/src/DarkGreyRPG.Studio/Views/Graph/
├─ GraphViewportController.cs
├─ GraphCoordinateTransform.cs
├─ GraphPointerState.cs
├─ GraphBounds.cs
└─ GraphContextMenuHost.cs
```

负责：

- Screen ↔ Graph 坐标；
- Pan；
- Zoom；
- Cursor-centered Zoom；
- Fit Bounds；
- Actual Size；
- Reset；
- Pointer State；
- Mouse Capture 清理；
- Viewport 持久化。

Flow 和 Story Graph 不再各自手写一套相似逻辑。

## 6.2 StoryFlowNodeControl

建议将动态拼装节点拆为：

```text
Views/StoryFlow/
├─ StoryFlowNodeControl.xaml
├─ StoryFlowNodeControl.xaml.cs
├─ FlowPortControl.xaml
├─ FlowPortControl.xaml.cs
├─ FlowConnectionLayer.cs
└─ FlowConnectionHitLayer.cs
```

### StoryFlowNodeControl

负责：

- 标题；
- 中文类型名；
- ID；
- 核心参数；
- Expander；
- 参数摘要；
- 分支端口标签；
- 警告徽标；
- 节点 Context Menu；
- Accessibility。

### StoryFlowEditorView

负责：

- Canvas；
- 节点布局；
- 连接层；
- 临时连接；
- 框选；
- 平移；
- 缩放；
- 空白和连线菜单；
- Gesture 状态。

## 6.3 StoryNodeDefinitionRegistry

应位于 Core 或不依赖 WPF 的层，确保可测试。

不得把 Runtime 端口语义只写在 code-behind 中。

## 6.4 Connection Edit API

建议 ViewModel 新增原子接口：

```csharp
BeginConnectionEdit(...)
CommitNewConnection(...)
ReconnectConnection(...)
DisconnectConnection(...)
CancelConnectionEdit(...)
RenameDynamicOutput(...)
RemoveDynamicOutput(...)
```

或者等价事务对象。

UI 不直接修改 `Connections` 集合多个步骤后再补状态。

## 6.5 Viewport 与折叠状态的编辑器持久化

可以保存到：

```text
resources/editor/
├─ story-flow-view-state.json
└─ story-graph-layout.json
```

仅包含：

- Story ID；
- Zoom；
- Pan；
- 节点参数折叠状态；
- 可选选择状态。

不得包含 Runtime 逻辑，不得被 Java Runtime 消费。

---

## 7. 预计文件影响范围

### Studio Core

- `studio/src/DarkGreyRPG.Studio.Core/Stories/StoryValidator.cs`
- `studio/src/DarkGreyRPG.Studio.Core/Stories/StoryResource.cs`
- 新增 `Stories/Definitions/*`
- 可能新增 Editor-only view state repository

### Flow ViewModel

- `studio/src/DarkGreyRPG.Studio/ViewModels/StoryFlowEditorViewModel.cs`
- `studio/src/DarkGreyRPG.Studio/ViewModels/ShellViewModel.cs`
- `studio/src/DarkGreyRPG.Studio/ViewModels/ProblemsViewModel.cs`

### Flow Views

- `studio/src/DarkGreyRPG.Studio/Views/StoryFlowEditorView.xaml`
- `studio/src/DarkGreyRPG.Studio/Views/StoryFlowEditorView.xaml.cs`
- 新增 StoryFlowNodeControl、FlowPortControl、Connection Layer

### Project Graph

- `studio/src/DarkGreyRPG.Studio/Views/ProjectGraphView.xaml`
- `studio/src/DarkGreyRPG.Studio/Views/ProjectGraphView.xaml.cs`
- `studio/src/DarkGreyRPG.Studio/ViewModels/ProjectHomeViewModel.cs`
- `ProjectGraphLayoutStore` 相关文件

### Shell / DPI / App

- `MainWindow.xaml`
- `App.xaml` / manifest / DPI 配置
- 相关主题资源

### Tests

- `DarkGreyRPG.Studio.Tests`
- `DarkGreyRPG.Studio.Wpf.Tests`
- 真实 WPF 自动化脚本
- 2.1 Acceptance 工程

具体文件名可调整，但职责边界不得再次集中回单个超大 code-behind。

---

## 8. 实施阶段与 Gate

## 阶段 0：安全基线与 Git 状态确认

1. 检查本地：
   - `git status`
   - `git log`
   - `git remote -v`
   - 当前 2.1.1 代码是否已提交；
   - 当前 2.1.1 是否已推送远端。
2. 不得执行会丢失本地 2.1.1 的：
   - `git reset --hard origin/main`
   - 强制 checkout 覆盖；
   - 删除未提交文件。
3. 若 2.1.1 尚未提交，先建立安全 checkpoint commit。
4. 从真实本地 2.1.1 基线创建 2.1.2 分支。

**Gate 0：**

- 本地 2.1.1 可重新构建；
- 所有现有测试基线已记录；
- 工作区不存在未备份的成果；
- 2.1.2 分支包含用户截图对应的 2.1.1 实现。

## 阶段 A：统一节点定义与端口契约

1. 建立 `StoryNodeDefinitionRegistry`。
2. 覆盖全部 Runtime 已支持节点。
3. 建立动态输出策略。
4. 保留旧连接实际使用的兼容 output。
5. 使用注册表生成添加节点菜单和节点显示名称。

**Gate A：**

- `Branch`、`QuestState`、`HasItem`、`VariableCompare` 均显示 `true / false`；
- `DialogueExitBranch` 显示命名出口；
- `Sequence` 显示数字出口；
- 终止节点无输出；
- 旧 `PlayDialogue.accept/refuse` 端口可见且连接不丢失；
- 所有 Runtime 节点都能从右键菜单找到或明确标为只读兼容节点。

## 阶段 B：共享 Pointer State 与 Viewport

1. 引入单一 Pointer State。
2. 统一 Mouse Capture 清理。
3. 修复中键平移。
4. 修复鼠标中心缩放。
5. 动态画布 Bounds。
6. Fit All / Actual Size / Reset。
7. Window UI Layer Context Menu。

**Gate B：**

- 点击、拖动或取消端口后，中键立即可用；
- 不需要滚轮解锁；
- View 失焦后没有残留手势；
- Flow 与 Graph 均可中键平移；
- 菜单不随 Graph Zoom 缩放；
- 负坐标和大型图不被固定 Canvas 裁切。

## 阶段 C：双向新建连接

1. 从输出端发起；
2. 从输入端发起；
3. 实线临时连接；
4. 合法目标高亮；
5. 空白取消；
6. `Esc` 取消；
7. 自动滚动画布边缘可选，不是 Gate 硬要求。

**Gate C：**

- 输入和输出端都可以创建连接；
- 输入→输入、输出→输出被拒绝；
- 临时线为正式实线视觉；
- 松开空白不产生脏数据；
- 新连接一次 Undo。

## 阶段 D：双端重接与多入线处理

1. 输出端重接目标；
2. 单入线输入端重接来源；
3. 多入线输入端扇出句柄或明确来源选择；
4. 空白松开断线；
5. `Esc` 恢复；
6. 重接事务化；
7. 连接 Hit Path。

**Gate D：**

- 现有连接任意一端可重接；
- 多入线时不会误改其它连接；
- 空白松开能断线；
- `Esc` 恢复原线；
- 一次 `Ctrl+Z` 恢复整个重接前状态。

## 阶段 E：端口视觉与参数可发现性

1. FlowPortControl；
2. 删除方形 Hover；
3. 圆点放大和高亮；
4. Expander 标题；
5. 参数摘要；
6. 分支端口始终可见；
7. 中文节点名。

**Gate E：**

- Hover 端口无方框；
- 圆点视觉放大但布局不跳；
- 所有带参数节点折叠时仍显示参数标题；
- 用户不打开右键菜单也能发现参数；
- `true/false`、命名出口、Sequence 序号在折叠时仍可见。

## 阶段 F：动态出口与完整节点菜单

1. Dialogue Exit 改名迁移；
2. 删除出口保护；
3. Sequence 步骤增删；
4. 完整分类菜单；
5. DPI/子菜单清晰度修复；
6. 核心参数和高级参数分层。

**Gate F：**

- 改名出口不会丢连接；
- 删除已连接出口必须确认；
- 菜单 100%/125%/150% DPI 下清晰；
- Graph Zoom 不影响菜单文字；
- 所有作者常用 Runtime 节点可以创建。

## 阶段 G：Problems 定位与 Recovery

1. Problems Source 精确到节点；
2. 点击问题聚焦；
3. 展开相关参数；
4. Recovery 草稿；
5. 正常保存清理 Recovery；
6. 崩溃恢复流程。

**Gate G：**

- 点击问题能直接定位节点；
- 无效草稿崩溃后可恢复；
- Recovery 不进入 Runtime 项目加载；
- 正式 Story JSON 仍只在有效保存时更新。

## 阶段 H：Project Story Graph 同步优化

1. Pointer State；
2. 无限画布；
3. DPI Context Menu；
4. 边 Hit Test；
5. 箭头；
6. 平行边聚合；
7. 自环；
8. 分支原因；
9. SCC 布局；
10. Problems 定位。

**Gate H：**

- Graph 仍不可编辑逻辑；
- Graph 操作前后 Story JSON 哈希不变；
- 平行边不再完全重叠；
- 自环可见；
- 箭头方向正确；
- 循环布局不会卡死；
- 点击 Missing Target 可定位来源 EnterStory。

## 阶段 I：完整回归与发布

1. Core tests；
2. WPF ViewModel tests；
3. 真实 WPF 自动化；
4. Runtime Gradle build 和 probes；
5. Acceptance Project；
6. Phase 4 legacy Project；
7. 1100×700 与 1700×980；
8. 100%/125%/150% DPI；
9. 打包单文件 EXE；
10. 文档、截图和哈希。

**Gate I：**

- 所有既有测试通过；
- 所有 2.1.2 新测试通过；
- 无已知 P0/P1；
- 版本号和发布文件一致；
- Git 工作区干净。

---

## 9. 自动化测试矩阵

## 9.1 Node Definition Tests

- 每个 Canonical Type 有唯一描述；
- 所有 Runtime Type 有映射；
- Boolean 节点输出正好为 `true / false`；
- Terminal 节点无输出；
- Sequence 数字输出排序；
- Dialogue Exit 去重；
- 旧 output 与标准 output 合并时不丢失；
- 中文显示名完整；
- 添加菜单分类完整。

## 9.2 Connection ViewModel Tests

- 输出端新建连接；
- 输入端反向新建连接；
- 同 output 替换旧目标；
- 输出端重接；
- 输入端单入线重接；
- 输入端多入线不自动选择；
- 空白松开删除已有连接；
- `Esc` 恢复已有连接；
- 重接单步 Undo/Redo；
- 删除节点清理相关连接；
- 动态出口改名迁移连接；
- 删除动态出口保护；
- 旧 PlayDialogue Named Result 保存不变。

## 9.3 Pointer State Tests

- `Idle → WireDrag → Idle`；
- `WireDrag → Esc → Idle`；
- `WireDrag → LostCapture → Idle`；
- `NodeDrag → Pan` 不允许重叠；
- 端口操作后中键进入 Pan；
- 中键松开回 Idle；
- 右键菜单打开前清理手势；
- View 卸载清理；
- DataContext 切换清理。

## 9.4 Viewport Tests

- Cursor-centered Zoom 坐标误差；
- Viewport-centered Zoom；
- Fit All 包含展开节点 ActualHeight；
- 负坐标节点可 Fit；
- Reset View；
- Zoom Clamp；
- Flow/Graph 视口状态独立；
- 恢复上次视口。

## 9.5 Problems / Recovery Tests

- 问题 Source 包含 Story/Node；
- 点击问题请求定位；
- 错误字段映射；
- Recovery 写入；
- 正常保存清理；
- 启动发现 Recovery；
- 忽略和删除；
- Runtime ProjectRepository 不读取 Recovery。

## 9.6 Project Graph Tests

- 平行边聚合或偏移；
- 自环；
- 箭头目标；
- 边 Hit Test 信息；
- 分支来源解析；
- Missing Target；
- Isolated Story；
- Cycle SCC；
- SCC 自动布局终止；
- Graph 操作不修改 Story JSON；
- 多选布局不修改逻辑。

---

## 10. 真实 WPF 验收场景

## 场景 1：输入端发起新连接

1. 新建两个普通节点；
2. 从目标节点输入端按住左键；
3. 拖到来源节点输出端；
4. 松开；
5. 确认形成 `source.next → target`；
6. 保存、重启、连接仍在。

## 场景 2：输出端重接

1. 已有 `A → B`；
2. 从 A 输出端拖到 C 输入端；
3. 确认变为 `A → C`；
4. `Ctrl+Z`；
5. 恢复 `A → B`。

## 场景 3：输入端重接

1. 已有 `A → B`；
2. 从 B 输入端拖到 C 输出端；
3. 确认变为 `C → B`；
4. 拖到空白处；
5. 连接断开。

## 场景 4：多入线输入端

1. `A → C`；
2. `B → C`；
3. Hover C 输入端；
4. 能明确选择 A 或 B 的端点；
5. 重接 A 的线；
6. B 的线完全不受影响。

## 场景 5：中键卡死回归

1. Hover/点击/拖动输出端；
2. 取消；
3. 立即按中键平移；
4. 连续重复十次；
5. 不使用滚轮；
6. Pan 始终可用。

## 场景 6：分支剧情

创建：

```text
PlayDialogue
→ DialogueExitBranch
   ├─ accept → GiveItem → EnterStory(happy)
   └─ refuse → GiveItem → EnterStory(bad)
```

确认：

- accept/refuse 标签清楚；
- 每个端口独立连接；
- 同一端口不能连两条；
- Runtime JSON 输出正确；
- Project Graph 派生两条目标 Story 边；
- Graph Tooltip 显示 accept/refuse。

## 场景 7：条件节点

创建 `QuestState`：

```text
true  → completion
false → active_message
```

折叠参数后：

- 仍显示“参数”标题；
- 仍显示 true/false；
- 用户能看出这是分支节点。

## 场景 8：DPI 菜单

分别在 Windows 100%、125%、150%，Graph Zoom 78%：

- 打开空白右键菜单；
- 打开“添加节点”二级菜单；
- 所有文字清晰；
- 菜单尺寸正确；
- 不跟随 Graph 缩放。

## 场景 9：草稿恢复

1. 创建无效 Flow；
2. 不保存；
3. 模拟异常退出；
4. 重启；
5. Studio 提示恢复；
6. 恢复后节点、位置、连接和参数存在；
7. Runtime 正式 JSON 未被污染。

## 场景 10：Project Graph

构造：

- 两条 A→B 转场；
- B→B 自环；
- A→C；
- C→A 循环。

确认：

- 平行边可识别；
- 自环可见；
- 箭头正确；
- Cycle Warning；
- 自动布局不冻结；
- 双击 Story 打开 Flow；
- Graph 操作不修改 Story JSON。

---

## 11. 性能与稳定性目标

最低验收规模：

### Story Flow

- 200 节点；
- 400 连接；
- 包含动态分支和展开参数；
- 打开、平移、缩放和节点拖动无明显冻结；
- Rebuild 不应在每次小属性变化时重建整个图；
- 连接移动优先局部更新。

### Project Story Graph

- 200 Stories；
- 400 EnterStory 派生边；
- 包含平行边、自环和多个 SCC；
- 自动布局在有限时间内完成；
- 不出现无限入队；
- 搜索和过滤不阻塞 UI。

建议引入简单性能日志或基准测试，但不把具体毫秒值硬编码为跨机器唯一标准。

---

## 12. 文档与版本更新

新增或更新：

- `docs/2.1.2_FLOW_CONNECTION_MODEL.md`
- `docs/2.1.2_NODE_PORT_CONTRACT.md`
- `docs/2.1.2_STORY_GRAPH.md`
- `docs/TESTING.md`
- `docs/DECISIONS.md`
- `studio/README_WPF.md`
- 将本计划保存到 `docs/plans/DarkGrey_RPG_Studio_2.1.2_Plan.md`

文档必须解释：

- 输入端/输出端双向发起；
- 重接和断线；
- 多入线的含义；
- 同 output 只能一条连接；
- 分支节点；
- Dialogue Exit；
- Sequence；
- 参数折叠；
- 中键 Pan；
- 鼠标中心缩放；
- Graph 只读边界。

版本号同步：

- `DarkGreyRPG.Studio.csproj`
  - `Version = 2.1.2`
  - `AssemblyVersion = 2.1.2.0`
  - `FileVersion = 2.1.2.0`
- `studio/package-studio.ps1`
- 窗口标题；
- About；
- 发布文档；
- EXE 文件属性。

Java Mod 版本默认保持 `0.5.0`。

---

## 13. 发布交付物

必须包含：

- `DarkGreyRPGStudio.exe` 2.1.2；
- EXE SHA-256；
- 完整源代码；
- Core / WPF / Runtime 测试结果；
- 2.1.2 自动化脚本；
- Flow 双端接线截图或短录屏；
- Branch / Dialogue Exit / Sequence 截图；
- 参数折叠前后截图；
- 100%/125%/150% DPI 菜单截图；
- Project Graph 平行边、自环、循环截图；
- 已知限制清单；
- 从 2.1.1 升级说明。

---

## 14. 明确非目标

2.1.2 不包含：

- 在 Project Story Graph 中编辑逻辑连接；
- 新增数据类型端口和颜色类型系统；
- Dialogue Editor 全面改为节点图；
- Quest Editor 全面改为节点图；
- WPF Live Bridge 迁移；
- StoryInstance Runtime 持久化；
- Entry Presentation Runtime；
- 新经济系统或通用 `GiveCurrency`；
- 多线程并行剧情执行；
- 通用 AND Join / Barrier 节点；
- 更换第三方节点图框架；
- 全局 Shell 重设计。

若确实需要并行同步或通用货币系统，应单独规划后续版本。

---

## 15. Definition of Done

2.1.2 只有在以下条件全部满足后才能宣布完成：

### 连接系统

- [ ] 输入端和输出端都能发起新连接；
- [ ] 已有连接任意一端都能重接；
- [ ] 多入线不会误改；
- [ ] 空白松开可断开已有连接；
- [ ] `Esc` 恢复原连接；
- [ ] 重接单步 Undo；
- [ ] 端口操作后中键不再卡死；
- [ ] 临时线使用正式实线视觉。

### 端口语义

- [ ] Boolean 节点显示 true/false；
- [ ] Dialogue Exit 显示命名出口；
- [ ] Sequence 显示数字出口；
- [ ] Terminal 无输出；
- [ ] 旧 PlayDialogue Named Result 不丢失；
- [ ] 同 output 仍只允许一条连接；
- [ ] 没有不必要的类型颜色编码。

### 节点 UI

- [ ] Hover 无方形描边；
- [ ] 折叠时仍显示参数标题和摘要；
- [ ] 分支端口不随参数折叠；
- [ ] 节点类型中文统一；
- [ ] 完整节点添加菜单可用；
- [ ] 二级菜单在多种 DPI 下清晰。

### 画布

- [ ] 单一 Pointer State；
- [ ] Lost Capture 可恢复；
- [ ] 中键 Pan；
- [ ] 鼠标中心缩放；
- [ ] 无限/动态画布；
- [ ] Fit All 使用 Actual Bounds；
- [ ] 工具栏缩放信息无歧义。

### Problems 与恢复

- [ ] 问题可定位节点和字段；
- [ ] 无效草稿可恢复；
- [ ] Recovery 不污染 Runtime JSON。

### Project Story Graph

- [ ] 只读边界未破坏；
- [ ] 平行边可识别；
- [ ] 自环可见；
- [ ] 方向箭头正确；
- [ ] 分支原因可检查；
- [ ] SCC 循环布局安全；
- [ ] Graph 操作不改 Story JSON。

### 构建与发布

- [ ] Core tests 全通过；
- [ ] WPF tests 全通过；
- [ ] Runtime build/probes 全通过；
- [ ] 实际 WPF QA 全通过；
- [ ] EXE 版本为 2.1.2；
- [ ] 文档和截图齐全；
- [ ] 工作区干净，无未提交最终修改。

---

## 16. 最后强制要求：Codex 必须将最终成品上传到 GitHub

完成全部开发、测试和打包后，Codex **不得只把成果留在本地工作区**。必须执行以下最终交付：

1. 将完整 2.1.2 源代码、测试、文档和必要 QA 证据提交到：
   - `https://github.com/GreyHat633/DarkGrey_RPG`
2. 如果远端尚未包含本地 2.1.1：
   - 先保留并提交真实本地 2.1.1 基线；
   - 再在其上提交 2.1.2；
   - 禁止从旧 `main` 重置并丢弃本地 2.1.1。
3. 使用可明确识别的远端分支，例如：
   - `studio/2.1.2-flow-graph-reliability`
4. 推送全部最终 commits，并打开面向 `main` 的 Pull Request。
5. PR 描述必须包含：
   - 版本范围；
   - 主要改动；
   - 测试结果；
   - Runtime 是否修改；
   - EXE SHA-256；
   - QA 截图路径；
   - 已知限制。
6. 最终交付消息必须明确给出：
   - GitHub 仓库；
   - 远端分支名；
   - Pull Request 编号或链接；
   - 最终 commit SHA；
   - 测试通过数量；
   - 发布产物路径和哈希。
7. 推送后再次确认 GitHub 上可以读取最终源码，不能只报告“已提交本地”。
8. 这样后续 ChatGPT 才能直接读取远端 2.1.2 成品、继续审计和开发。
