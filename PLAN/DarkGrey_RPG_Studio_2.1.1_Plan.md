# DarkGrey RPG Studio 2.1.1 开发计划

> **版本定位**：面向节点图编辑体验的全方位优化。
> **核心范围**：以 Story Flow 为重点，统一 Project Story Graph 中适用于只读图谱的画布交互，并修复剧情图谱现有的循环布局风险。
> **基线版本**：DarkGrey RPG Studio 2.1.0（GitHub `main` 当前实现）。
> **计划状态**：待实施。
> **说明**：本计划仅覆盖已经确认的 Flow 1—6 项问题，以及审计后确认适用于 Project Story Graph 的对应问题；未提供的第 7 项不在本版本范围内。

---

## 1. 版本目标

2.1.1 不增加新的 RPG 运行时能力，也不改变 Story、Actor、Dialogue、Quest 的核心产品边界。该版本的目标是把 2.1.0 中“功能已经存在，但节点图操作不符合常见编辑器习惯”的状态，修正为可长期使用的节点图工作流。

版本完成后应达到以下效果：

1. Story Flow 画布成为工作区主体，不再被常驻属性栏和重复报错文本挤压。
2. 节点参数直接集成在节点卡片内；复杂参数可折叠或通过轻量次级界面编辑。
3. Flow 支持真正的鼠标拖拽连线，而不是隐藏的“先点输出、再点输入”操作。
4. Flow 与 Project Story Graph 使用一致的基础画布习惯：
   - 左键选择和拖动节点；
   - 中键拖动画布；
   - 滚轮以鼠标所在位置为中心缩放；
   - 右键打开上下文菜单。
5. Flow 错误不再重复铺满页面，也不再阻止用户切换 Story 内页面。
6. Project Story Graph 保持逻辑只读，但同步改进画布空间、平移、缩放、诊断和右键菜单。
7. Project Story Graph 的自动布局可以安全处理循环，不会因循环关系导致打开图谱或自动布局卡死。
8. 旧 schema 1 Story 不再因为缺少 Story Membership 而把项目中真实存在的 Dialogue、Quest 误报为“不存在”。

---

## 2. 设计原则

### 2.1 画布优先

Flow 和 Project Story Graph 都属于空间型工作区。节点图本体必须占据绝大多数可用区域，辅助信息不得使用永久固定宽度的大侧栏。

### 2.2 共享交互保持一致

两种图的基础操作必须一致：

| 操作 | Story Flow | Project Story Graph |
|---|---|---|
| 左键拖节点 | 移动节点 | 移动 Story 布局节点 |
| 左键拖空白 | 框选 | 不框选；仅取消焦点或预留 |
| 中键拖动 | 平移 | 平移 |
| 滚轮 | 鼠标中心缩放 | 鼠标中心缩放 |
| 右键空白 | 添加节点等编辑菜单 | 自动布局、适应全部、重置视图 |
| 右键节点 | 编辑、复制、删除、断开 | 打开 Story/Flow、聚焦、复制 ID、查看诊断 |
| 拖拽端口 | 创建 Flow 连接 | 不适用，图谱逻辑只读 |

### 2.3 Story Graph 永远不是第二逻辑源

Project Story Graph 的边继续只从 Story Flow 中有效的 `EnterStory` 节点派生。

严禁在 Project Story Graph 中实现：

- 手工创建连接；
- 手工删除逻辑连接；
- 手工创建不存在的 Story 节点；
- 通过拖线直接修改 Story Flow。

### 2.4 验证错误不应囚禁用户

错误可以：

- 禁止把当前草稿保存为有效 Runtime Story；
- 在 Problems 中显示；
- 在节点或图谱上显示紧凑警告。

错误不可以：

- 阻止用户切换 Story 内的“概览 / 角色 / 对话 / 任务 / 流程”页面；
- 强制展开大型错误区域；
- 让用户无法回到其它页面修复相关资源。

### 2.5 不隐藏写入迁移

对于旧 schema 1 Story 的 Membership 缺失问题，打开项目时不应在用户无感知的情况下擅自改写 Story JSON。2.1.1 应先正确区分：

- 项目中不存在的资源：Error；
- 项目中存在，但未加入当前 Story Membership 的资源：Warning；
- 当前 Story 已拥有或引用的资源：正常。

可以提供“一键添加为引用”，但不自动写入。

---

## 3. 当前问题与 2.1.1 处理范围

## 3.1 Story Flow

### F-01：画布被固定属性栏和重复错误文本侵占

当前中间区域固定保留约 260 DIPs 的节点属性栏，底部又显示完整多行 `ValidationText`。错误数量越多，画布越小。

**2.1.1 修改：**

- 删除 Flow 内常驻右侧属性栏。
- Flow Canvas 占满中间剩余区域。
- 删除页面内完整多行错误文本。
- 底部只保留紧凑状态条：
  - 节点数；
  - 连接数；
  - 缩放比例；
  - 保存状态；
  - 错误/警告计数。
- 点击错误计数时：
  - 选择全局 Bottom Dock 的“问题”页；
  - 展开 Bottom Dock；
  - 聚焦当前 Story Flow 的问题。

### F-02：节点属性应集成在节点内部

当前节点只显示类型和 ID，参数全部放在右侧属性栏。

**2.1.1 修改：**

每个节点采用分层显示：

1. **标题区**
   - 节点类型；
   - 状态/警告徽标；
   - 可选折叠按钮。

2. **基础区**
   - 节点 ID 以次要文字显示；
   - 最常用参数直接显示在节点内：
     - `ActorInteract`：Actor 选择；
     - `PlayDialogue`：Dialogue 选择；
     - `StartQuest` / `WaitQuestComplete`：Quest 选择；
     - `EnterStory`：目标 Story 选择；
     - `DialogueExitBranch`：出口名称及对应端口。

3. **高级区**
   - 节点 ID 编辑；
   - 多参数节点的完整参数；
   - 旧 Runtime 节点的泛型属性编辑；
   - 默认折叠，可在节点内展开或打开轻量 Popover。

折叠状态在 2.1.1 中作为编辑器会话状态，不写入 Runtime Story JSON，避免引入新的数据 schema。

### F-03：真正的拖拽连线

当前实现只有“点击输出，再点击输入”，没有拖线视觉反馈。

**2.1.1 修改：**

- 左键按下输出端口后进入 Connecting 状态。
- 从真实输出端口中心绘制临时贝塞尔线。
- 临时线实时跟随鼠标。
- 合法输入端口 Hover 时高亮。
- 在合法输入端口松开鼠标时创建连接。
- 在空白处、自身节点或非法目标松开时取消。
- `Esc` 取消当前接线。
- 已存在同一 `from + output` 连接时，继续遵守“一条 output 只能连接一个目标”的规则，替换旧连接。
- 端口位置必须通过控件实际坐标计算，禁止继续使用固定 `Y + 82` 一类硬编码。
- 节点高度因参数展开变化后，已有连接仍准确贴合端口。

### F-04：鼠标中键平移

**2.1.1 修改：**

- 移除右键平移。
- 按住鼠标中键进入 Pan 状态。
- 移动期间使用平移光标。
- 松开中键立即结束。
- 平移逻辑不影响节点选择、框选和接线。

### F-05：右键上下文菜单

#### 空白区域右键

菜单至少包含：

- 添加节点
  - 触发
    - 剧情开始
    - 角色交互
  - 对话
    - 播放对话
    - Dialogue Exit 分支
  - 任务
    - 开始任务
    - 等待任务完成
  - 剧情
    - 进入剧情
    - 结束当前剧情
  - 结束
- 粘贴
- 全选
- 适应全部节点
- 重置视图

新节点生成位置必须对应右键发生时的 Graph 世界坐标，而不是固定位置。

#### 节点右键

菜单至少包含：

- 展开/折叠参数；
- 复制；
- 创建副本；
- 断开所有连接；
- 删除节点。

右键未选中的节点时，应先把该节点设为当前选择，再打开菜单。

#### 连线右键

菜单至少包含：

- 删除连接。

保留键盘 `Delete`、`Ctrl+C`、`Ctrl+V`、`Ctrl+Z`、`Ctrl+Y`。

### F-06：鼠标位置中心缩放

滚轮缩放采用以下不变量：

> 缩放前鼠标指向的 Graph 世界坐标，在缩放后仍位于鼠标下方。

计算规则：

```text
graph_point = (cursor_in_viewport - old_pan) / old_zoom
new_pan = cursor_in_viewport - graph_point * new_zoom
```

- 滚轮缩放中心：鼠标所在位置。
- 工具栏 `+ / -` 缩放中心：CanvasViewport 可视中心。
- 缩放范围维持 25%—250%。
- 提供“100%”和“适应全部节点”入口。

### F-07：端口视觉重做

普通端口不再使用带边框的 Button，也不再显示 `next ●`。

**样式规则：**

- Input：节点左边缘的圆点。
- Output：节点右边缘的圆点。
- 视觉直径约 8—10 DIPs。
- 实际命中区域约 18—22 DIPs。
- Hover、Connecting、Valid Target 有明确但克制的高亮。
- 普通 `next` 只显示圆点，不显示文字。
- `next` 语义保留在 ToolTip、AutomationProperties 和内部数据中。
- `DialogueExitBranch` 的 Named Exit 必须显示名称，例如：

```text
hand_over    ●
conceal      ●
```

### F-08：错误不再阻止 Story 内页面切换

当前从 Flow 切换到其它 Story 页面时会强制 `TrySaveCurrentFlow()`；无效 Flow 因无法保存而阻止切换。

**2.1.1 修改：**

- 同一个 Story 内切换五个页面时，不强制保存 Flow。
- `CurrentFlow` 保留在内存中，未保存草稿不会因页面切换丢失。
- 离开当前 Story、打开另一个 Story、关闭项目或退出程序时，才执行未保存确认：
  - 保存；
  - 放弃；
  - 取消。
- 选择保存但 Flow 仍有错误时：
  - 不离开；
  - 展开 Problems；
  - 聚焦错误。
- 选择放弃时：
  - 从磁盘重新加载 Flow；
  - 允许离开。

### F-09：旧 Story 资源误报修复

当前 Flow Editor 只拿当前 Story 的 `owned_resources + referenced_resources` 作为有效候选。旧 schema 1 Story 的 Membership 为空，因此项目中真实存在的 Dialogue、Quest 也会被误报为“不存在”。

**2.1.1 修改：**

Flow Editor 同时接收两组数据：

1. 项目级全部 Actor / Dialogue / Quest / Story ID；
2. 当前 Story 已拥有或引用的资源 ID。

验证规则：

- ID 不在项目级 Registry：Error，资源不存在。
- ID 在项目级 Registry，但不在当前 Story Membership：Warning，资源存在但未组织进当前 Story。
- ID 在当前 Story Membership：正常。

节点资源选择器：

- 优先列出当前 Story 资源；
- 可展开“项目中的其他资源”；
- 对未加入 Membership 的资源提供“添加为引用”操作；
- 不在打开时自动改写 Story JSON。

---

## 3.2 Project Story Graph

### G-01：移除常驻 280px 诊断侧栏

当前 Project Graph 永久保留约 280 DIPs 的“图谱诊断”区域。

**2.1.1 修改：**

- 删除常驻诊断侧栏。
- Project Graph Canvas 占满剩余工作区。
- 图谱左下角或状态栏显示紧凑诊断计数：
  - Error 数；
  - Warning 数。
- 点击计数时打开全局 Problems。
- Story 节点使用警告徽标和边框表达与自身相关的诊断。

### G-02：统一中键平移

与 Flow 一致：

- 中键拖动平移；
- 右键不再平移；
- 左键继续用于节点布局拖动。

### G-03：统一鼠标中心缩放

与 Flow 共用同一缩放算法和辅助类：

- 滚轮以鼠标位置为中心；
- 工具栏按钮以视口中心为中心；
- 支持 100%、适应全部、重置视图；
- 缩放范围 25%—250%。

### G-04：只读右键菜单

#### Story 节点右键

- 打开 Story；
- 打开 Flow；
- 聚焦此节点；
- 复制 Story ID；
- 查看该 Story 诊断。

严禁提供：

- 删除 Story；
- 创建连接；
- 删除逻辑连接；
- 创建图谱节点。

#### 空白处右键

- 自动布局；
- 适应全部节点；
- 重置视图。

### G-05：提高节点密度

当前每个 Story 节点内都有完整“打开 Flow”按钮，固定高度较大。

**2.1.1 修改：**

- 移除完整宽度的“打开 Flow”按钮。
- 双击节点继续打开 Flow。
- 右键菜单保留“打开 Story / 打开 Flow”。
- 可以在标题区保留一个紧凑图标按钮，但不得显著增加节点高度。
- 节点内容集中为：
  - Display Name；
  - Story ID；
  - Home Story 标识；
  - 诊断徽标。

### G-06：连线增加方向表达

Project Graph 没有可编辑端口，也不应增加看起来可拖拽的端口。

**2.1.1 修改：**

- 派生边继续直接连接 Story 节点边缘。
- 在线条目标端增加清晰、克制的箭头。
- 保持只读视觉，不显示可交互端口圆点。
- ToolTip 显示 `Source Story → Target Story` 及来源 `EnterStory` 节点 ID。

### G-07：循环诊断和安全自动布局

当前自动布局在可达循环中可能不断提高层级并重复入队，导致打开图谱或自动布局卡死。

**2.1.1 修改：**

采用 Strongly Connected Components（SCC）处理：

1. 从有效 `EnterStory` 边构建有向图。
2. 使用 Tarjan 或 Kosaraju 算法计算 SCC。
3. 以下情况产生 `project_graph.story.cycle` Warning：
   - SCC 包含两个或更多 Story；
   - 单 Story 存在自环。
4. 将每个 SCC 折叠成一个组件，形成无环 DAG。
5. 在组件 DAG 上计算分层布局。
6. 同一循环组件内的 Story 以小型纵向或环形簇排列。
7. 所有生成坐标必须是有限值。
8. 循环是 Warning，不阻止打开、保存或 Runtime 使用。

现有诊断语义调整为：

| 诊断 | 严重性 |
|---|---|
| EnterStory 目标缺失 | Error |
| Story 孤立 | Warning |
| Story 循环 | Warning |
| 图谱布局保存失败 | Warning |

### G-08：图谱诊断进入统一 Problems

Project Graph 当前维护独立 `Diagnostics`，没有成为全局问题系统的一部分。

**2.1.1 修改：**

扩展 `ProblemsViewModel` 支持按 Source 更新，而不是每次完全覆盖：

```text
ReplaceForSource(source, problems)
RemoveSource(source)
ClearAll()
```

建议 Source：

```text
project-graph
project-graph/<story_id>
story/<story_id>/flow
```

这样：

- Flow 问题；
- Project Graph 问题；
- 当前资源问题；

可以进入同一个 Problems 面板，并保留来源信息。

---

## 4. 共享画布基础设施

为避免 Flow 与 Project Graph 再次出现不同实现、相同 Bug，2.1.1 应抽出共享画布交互层。

建议新增：

```text
studio/src/DarkGreyRPG.Studio/Views/Graph/
├─ GraphViewportController.cs
├─ GraphCoordinateTransform.cs
└─ GraphContextMenuHelpers.cs
```

### GraphViewportController 负责

- Zoom 范围；
- Cursor-centered Zoom；
- Viewport-centered Zoom；
- PanBy；
- ScreenToGraph；
- GraphToScreen；
- ResetView；
- FitToBounds；
- 鼠标捕获状态清理。

Flow 和 Project Graph 仍可保持各自 ViewModel，但不再分别手写缩放和平移公式。

---

## 5. Flow 节点控件重构

当前 `CreateNodeVisual()` 在 code-behind 中动态拼装整个节点。加入内嵌属性、动态高度、折叠、高亮端口和真实端口坐标后，该方法会过于复杂。

建议新增：

```text
studio/src/DarkGreyRPG.Studio/Views/
├─ StoryFlowNodeControl.xaml
├─ StoryFlowNodeControl.xaml.cs
├─ FlowPortControl.cs
└─ StoryFlowNodeEditorTemplates.xaml
```

### StoryFlowNodeControl 职责

- 节点标题与选中状态；
- 内嵌基础属性；
- 高级参数折叠；
- Input/Output 端口；
- 端口真实坐标；
- 节点右键菜单；
- Accessibility Name；
- 节点警告徽标。

### StoryFlowEditorView 职责

- Canvas；
- 节点摆放；
- 多选和框选；
- 临时连线；
- 永久连线；
- 平移、缩放；
- 空白和连线右键菜单。

### StoryFlowEditorViewModel 新增能力

建议新增方法：

```csharp
AddNodeAt(string canonicalType, double x, double y)
DisconnectNode(string nodeId)
DuplicateSelection()
GetNodeIssues(string nodeId)
```

保留现有：

```csharp
Connect(...)
MoveSelection(...)
DeleteSelectionCommand
CopyCommand
PasteCommand
UndoCommand
RedoCommand
```

---

## 6. 文件影响范围

预计主要修改：

### Flow

- `studio/src/DarkGreyRPG.Studio/Views/StoryFlowEditorView.xaml`
- `studio/src/DarkGreyRPG.Studio/Views/StoryFlowEditorView.xaml.cs`
- `studio/src/DarkGreyRPG.Studio/ViewModels/StoryFlowEditorViewModel.cs`

### Project Story Graph

- `studio/src/DarkGreyRPG.Studio/Views/ProjectGraphView.xaml`
- `studio/src/DarkGreyRPG.Studio/Views/ProjectGraphView.xaml.cs`
- `studio/src/DarkGreyRPG.Studio/ViewModels/ProjectHomeViewModel.cs`

### Shell、导航和 Problems

- `studio/src/DarkGreyRPG.Studio/ViewModels/ShellViewModel.cs`
- `studio/src/DarkGreyRPG.Studio/ViewModels/StoryWorkspaceViewModels.cs`
- `studio/src/DarkGreyRPG.Studio/ViewModels/ProblemsViewModel.cs`
- 相关 Dialog Service 接口和实现

### 新增共享控件/基础设施

- `Views/Graph/GraphViewportController.cs`
- `Views/StoryFlowNodeControl.xaml`
- `Views/StoryFlowNodeControl.xaml.cs`
- `Views/FlowPortControl.cs`

具体文件名可在实施时微调，但职责边界不得重新集中回单一大型 code-behind。

---

## 7. 实施阶段

## 阶段 A：回归基线和交互基础设施

1. 新建 2.1.1 开发分支。
2. 记录当前 Core、WPF、Runtime 测试基线。
3. 新增共享 GraphViewportController。
4. 为两种图补充：
   - 中键平移；
   - 鼠标中心缩放；
   - 视口中心缩放；
   - Fit All；
   - Reset View。
5. 在不改变节点 UI 的前提下先完成共享行为测试。

**Gate A：**

- Flow 和 Graph 中键平移工作；
- 右键不再触发平移；
- 鼠标中心缩放误差在允许范围内；
- 旧测试无回归。

## 阶段 B：Flow 画布和节点 UI 重构

1. 移除 260px 属性侧栏。
2. 移除完整 ValidationText 区域。
3. 引入 StoryFlowNodeControl。
4. 将基础资源参数移入节点。
5. 实现高级参数折叠。
6. 改造动态节点高度和真实端口坐标。
7. 改造端口样式。

**Gate B：**

- 1100×700 最小窗口中 Flow Canvas 仍是工作区主体；
- 所有主要节点可在节点内编辑资源参数；
- 普通 `next` 不再显示文字；
- Named Exit 名称清晰可见；
- 展开/折叠不会让连线错位。

## 阶段 C：Flow 拖拽连线

1. 实现 Connection Draft 状态。
2. 实现临时贝塞尔线。
3. 实现合法输入端口命中和高亮。
4. 实现松开创建、空白取消、Esc 取消。
5. 实现连接替换和 Undo/Redo。
6. 保留连接选择、Delete 和右键删除。

**Gate C：**

- 用户可以按住输出端口拖到输入端口完成连接；
- 连接过程中有实时视觉反馈；
- 取消操作不产生脏连接；
- 动态节点高度下连接锚点准确；
- 保存重启后连接继续存在。

## 阶段 D：Flow 右键菜单、错误和导航

1. 实现空白、节点、连线 Context Menu。
2. 实现按鼠标 Graph 坐标添加节点。
3. 实现紧凑错误计数。
4. 点击错误计数打开全局 Problems。
5. 修改 Flow 路由离开策略：
   - Story 内页面切换不保存；
   - 离开 Story/项目/程序时确认。
6. 扩展 ProblemsViewModel 的 Source 分组。
7. 增加对应 Accessibility Name。

**Gate D：**

- 无效且未保存的 Flow 可以切换到角色、对话、任务和概览；
- 切回 Flow 后草稿仍在；
- 关闭 Story 时能保存、放弃或取消；
- 页面内不再显示大段重复红字；
- 右键菜单中的删除、复制和断开可用。

## 阶段 E：旧 Story Membership 兼容

1. Flow Editor 接收项目级资源 ID 与 Story Membership ID。
2. 将“资源不存在”和“未加入当前 Story”拆成 Error / Warning。
3. 资源选择器分组显示。
4. 提供“添加为引用”入口。
5. 使用 Phase 4 示例工程做回归。

**Gate E：**

- 项目中存在的 Dialogue/Quest 不再误报为不存在；
- 真正缺失的 Actor/Dialogue/Quest 仍是 Error；
- 未加入 Membership 时显示 Warning；
- 不产生未经用户确认的 Story JSON 改写。

## 阶段 F：Project Story Graph 优化

1. 移除 280px 诊断栏。
2. 使用紧凑诊断徽标和 Problems。
3. 同步中键平移、鼠标中心缩放、Fit All。
4. 实现只读 Context Menu。
5. 压缩 Story 节点高度，移除完整“打开 Flow”按钮。
6. 添加方向箭头。
7. 实现 SCC 循环诊断。
8. 重写自动布局，使循环安全。

**Gate F：**

- Project Graph 不再有常驻诊断侧栏；
- 任何循环图都能在有限时间内打开和自动布局；
- 循环只产生 Warning；
- 图谱操作不修改任何 Story JSON；
- 图谱仍不能创建或删除逻辑边；
- 双击和右键都能进入目标 Flow。

## 阶段 G：完整 QA 和发布

1. 全部 Core / WPF 测试。
2. 2.1 Acceptance Project 回归。
3. Phase 4 legacy Project 回归。
4. 实际 WPF 自动化：
   - 中键 Pan；
   - Cursor Zoom；
   - Drag Connect；
   - Context Menu；
   - Inline Properties；
   - Invalid Flow Route Switch；
   - Graph Cycle；
   - Problems；
   - Restart Persistence。
5. 1700×980 与最小 1100×700 界面检查。
6. 更新版本号为 2.1.1。
7. 打包单文件 EXE。
8. 记录 SHA-256 和截图。

---

## 8. 测试计划

## 8.1 ViewModel / Core 测试

### Flow

- `AddNodeAt` 使用正确 Graph 坐标。
- 节点资源字段修改写入 `StoryDocument`。
- 节点展开状态不进入 Runtime JSON。
- `Connect` 替换同一 output 的旧连接。
- Connection 删除可 Undo。
- 节点删除同时删除相关连接。
- ID 重命名继续更新连接。
- Story 内路由切换不触发 Flow 保存。
- 离开 Story 时 Save / Discard / Cancel 行为正确。
- 项目级存在、Story 未引用的资源产生 Warning。
- 项目级不存在的资源产生 Error。

### Project Graph

- Missing Target 产生 Error。
- Isolated Story 产生 Warning。
- 两节点循环产生 Cycle Warning。
- 自环产生 Cycle Warning。
- 循环自动布局终止。
- 所有布局坐标为有限值。
- 删除 `EnterStory` 后派生边消失。
- Graph Layout 仍只写 `resources/editor/story-graph-layout.json`。

### Problems

- 可以按 Source 更新问题。
- 更新 Flow 问题不会删除 Project Graph 问题。
- Error / Warning 计数准确。
- 清理某 Source 不影响其它 Source。

## 8.2 WPF 交互测试

- 中键拖动改变 PanX/PanY。
- 右键不改变 PanX/PanY。
- 滚轮前后鼠标下的 Graph 坐标不变。
- 输出端口按下后显示临时线。
- 移到合法输入端口时目标高亮。
- 松开后创建连接。
- 空白松开和 Esc 不创建连接。
- 右键空白按点击位置添加节点。
- 右键节点删除节点。
- 右键连线删除连接。
- 节点内 ComboBox 修改资源。
- 高级参数展开后端口锚点仍正确。
- Flow 错误指示点击后展开 Problems。
- Invalid Flow 可以切换到其它 Story 页面。
- Graph 节点双击进入 Flow。
- Graph 右键菜单无任何逻辑编辑命令。
- Graph 箭头方向正确。

## 8.3 回归测试

必须继续通过：

- 现有 M2、M4、M5、M6、M7 Core/WPF 测试；
- Story Flow 8 节点、7 连接验收；
- Copy/Paste/Delete/Undo/Redo；
- Project Graph 布局持久化；
- Story JSON 哈希在 Graph 操作前后不变；
- Runtime Java build 和既有 probe，确认 Studio UI 改造未改变数据契约。

---

## 9. 发布内容

2.1.1 发布应包含：

- `DarkGreyRPGStudio.exe` 2.1.1；
- 更新后的 `README_WPF.md`；
- `docs/2.1.1_FLOW_GRAPH_OPTIMIZATION.md`；
- 更新后的 `docs/TESTING.md`；
- 更新后的 `docs/DECISIONS.md`；
- Flow 和 Project Graph 操作说明；
- 新的 UI 自动化脚本；
- QA 截图；
- EXE SHA-256。

版本号需要同步修改：

- `DarkGreyRPG.Studio.csproj`
  - `Version`
  - `AssemblyVersion`
  - `FileVersion`
- `studio/package-studio.ps1`
  - Publish Version
  - AssemblyVersion
  - FileVersion
  - InformationalVersion
- Studio 窗口标题或 About 信息中的版本显示。

Java Mod 版本保持 `0.5.0`，除非实施过程中确实修改了 Runtime；本计划默认不修改 Java Runtime 版本。

---

## 10. 非目标

2.1.1 明确不包含：

- 在 Project Story Graph 中编辑逻辑连接；
- 新增 Runtime Story Node 类型；
- 重构 Dialogue Editor 为节点图；
- 重构 Quest Editor 为节点图；
- WPF 接入 Live Bridge；
- Debugger、Pick、Locate、Play Test 的 WPF 迁移；
- StoryInstance 持久化；
- Entry Presentation Runtime；
- 全项目 schema 2 统一工作；
- 更换第三方 WPF 节点图框架；
- 全局 Shell 视觉重做。

这些工作应放入后续版本，不得借 2.1.1 无限制扩张范围。

---

## 11. 风险与控制

### 风险 1：动态节点控件使 code-behind 进一步膨胀

**控制：** 将节点和端口拆成独立控件，Canvas View 只处理空间交互。

### 风险 2：拖拽接线破坏现有 Click-Click 测试

**控制：** ViewModel 的 `Connect()` 契约保持不变；只替换 View 层输入方式。必要时保留 Click-Click 作为键盘或辅助功能备用路径，但不作为主交互。

### 风险 3：导航允许保留无效草稿后，关闭程序时丢失数据

**控制：** 把未保存确认从“每次切页”移动到“离开 Story / 项目 / 程序”，并补齐 Flow 专用 Save / Discard / Cancel。

### 风险 4：Problems 分 Source 后影响旧资源问题刷新

**控制：** 保留兼容方法，逐步迁移到 `ReplaceForSource`；增加跨 Source 单元测试。

### 风险 5：循环布局改造影响无环图的原有顺序

**控制：** 无环图继续使用确定性分层排序；SCC 只对循环组件产生额外簇布局；测试固定节点顺序和有限坐标，不强依赖每个像素。

### 风险 6：旧 Story Membership 修复引入隐式语义变化

**控制：** 只修正验证和资源选择；自动添加引用必须由用户显式执行。

---

## 12. Definition of Done

2.1.1 只有在以下条件全部满足时才算完成：

- [ ] Flow 不再有常驻节点属性侧栏。
- [ ] Flow 不再在画布下方显示完整多行错误文本。
- [ ] 节点常用参数已集成在节点内。
- [ ] 复杂参数可以折叠或打开次级编辑界面。
- [ ] 中键平移在 Flow 和 Project Graph 中均可用。
- [ ] 右键不再触发平移。
- [ ] Flow 支持按住输出端口拖线并在输入端口松开完成连接。
- [ ] 接线过程中有临时线和目标高亮。
- [ ] 普通 Input/Output 使用圆点端口。
- [ ] 普通 `next` 不显示文字。
- [ ] Named Exit 保留名称。
- [ ] Flow 空白、节点、连线右键菜单可用。
- [ ] 滚轮缩放在两种图中均以鼠标位置为中心。
- [ ] 无效 Flow 不再阻止 Story 内页面切换。
- [ ] 离开 Story 时有完整 Save / Discard / Cancel。
- [ ] 项目中存在但未加入 Membership 的资源不再误报为不存在。
- [ ] Project Graph 不再有常驻诊断侧栏。
- [ ] Project Graph 使用紧凑诊断和全局 Problems。
- [ ] Project Graph 边具有明确方向箭头。
- [ ] Project Graph 仍无法手工创建或删除逻辑边。
- [ ] Project Graph 循环不会造成打开或自动布局卡死。
- [ ] 循环显示 Warning。
- [ ] Graph 操作不修改 Story JSON。
- [ ] 原有 Core、WPF、Runtime 回归全部通过。
- [ ] 实际 Release WPF 交互验收通过。
- [ ] 单文件 EXE 版本为 2.1.1，并记录 SHA-256。

---

## 13. 最终版本定义

**DarkGrey RPG Studio 2.1.1** 是一次专门面向节点图的可用性修复版本：

- Story Flow 从“已有节点画布功能”升级为“符合常见节点编辑器操作习惯的可用编辑器”；
- Project Story Graph 保持只读，但获得一致的 Pan、Zoom、空间利用、诊断和上下文交互；
- 两者共享画布基础设施，避免同类交互 Bug 重复出现；
- 不改变 RPG 逻辑 Source of Truth，不把 Project Story Graph 变成第二套剧情编辑器。
