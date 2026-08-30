# DarkGrey_RPG 0.3.1.1 Root Cause Record

> 状态：Stage 5 源码与自动化收尾（2026-08-31）
>
> 基线：`codex/0.3.1.0` @ `96c821d1a2b4038fecdc62bdbbbd8078291a8d0e`
>
> 目标分支：`codex/0.3.1.1`
> 证据边界：本表的“已复现”包含对当前源码中确定性实现路径的复现；U1/U2/U3 必须保留真实 Studio 操作证据后才能关闭。自动测试不替代第 14 章人工验收。

| # | 问题 | 是否复现 | 根因 | 目标文件 | 最终修复 |
|---:|---|---|---|---|---|
| 1 | 新建故事主操作不居中 | 源码复现 | 空状态布局仍保留非必要的多区域结构 | `MainWindow.xaml` | 待 Stage 4 |
| 2 | 大标题及“故事 / 设置”应用侧栏冗余 | 源码复现 | Shell 同时保留 app rail、全屏 Settings 与大标题区 | `MainWindow.xaml`; `ShellViewModel.cs` | 待 Stage 4 |
| 3 | Flow 区域只能左右调、不能上下调 | 待实机复现 | U1：需先区分冗余头部挤压与真实纵向 splitter 缺失 | `MainWindow.xaml`; `CanonicalStoryWorkspaceView.xaml` | 待 Stage 4 |
| 4 | 新建资源导致已有资源闪动 | 源码复现 | 普通增删后 `ReloadCanonicalStoryWorkspace` 新建整个 Workspace VM | `ShellViewModel.cs`; `CanonicalStoryWorkspaceViewModel.cs` | 待 Stage 1 |
| 5 | 资源库展开动画卡顿 | 待实机复验 | 整体 workspace 重建是首要干扰；需在增量修复后复验 Expander | `CanonicalStoryWorkspaceView.xaml` | 待 Stage 1 |
| 6 | 节点右键没有编辑 / 删除 | 源码复现 | 右键命中 node 时主动拦截，只有空白画布菜单 | `CanonicalGraphEditorView.xaml.cs` | 待 Stage 2 |
| 7 | 资源拖入没有跟随鼠标的节点 ghost | 源码复现 | `DragOver` 只设置 effect，真实节点仅在 `Drop` 后创建 | `CanonicalStoryWorkspaceView.xaml.cs`; `CanonicalGraphEditorView.xaml.cs` | 待 Stage 2 |
| 8 | 拖线使用虚线预览而非真实实线 | 源码复现 | `BeginWire` 主动设置 `StrokeDashArray` | `CanonicalGraphEditorView.xaml.cs` | 待 Stage 2 |
| 9 | 缺少剪刀工具与 Left Alt 临时剪线 | 源码复现 | 当前 pointer state 没有 scissors mode / 左 Alt 生命周期 | `CanonicalGraphEditorView.xaml(.cs)` | 待 Stage 2 |
| 10 | 普通 UI 暴露内部 ID | 源码复现 | Node header / Inspector 主信息直接投影 NodeId / internal resource ID | `CanonicalGraphNodeControl.xaml`; `CanonicalStoryWorkspaceView.xaml`; `CanonicalStoryWorkspaceViewModel.cs` | 待 Stage 3/4 |
| 11 | 聚合节点双击与资源右键缺少统一编辑入口 | 部分复现 | 资源双击已有路径；node 右键被拦截，aggregate double-click 未接资源导航 | `CanonicalGraphEditorView.xaml.cs`; `CanonicalStoryWorkspaceView.xaml.cs` | 待 Stage 2 |
| 12 | 删除有连线节点被拒绝 | 源码复现 | UI 首次调用未确认删除，且缺少连线数量确认事务 | `CanonicalGraphEditorView.xaml.cs`; `GraphEditSession.cs` | 待 Stage 2 |
| 13 | 多处重复“返回 Story Flow” | 源码复现 | Inspector 仍保留大按钮，未统一到 breadcrumb | `CanonicalStoryWorkspaceView.xaml` | 待 Stage 4 |
| 14 | Actor ID 残留、参数全藏 Inspector、资源无法拖入参数 | 源码复现 | 旧 Actor authoring 入口与 inspector-only 参数路径仍在 | `ActorWorkspaceDialogs.cs`; `CanonicalNodeInspectorViewModel.cs`; `CanonicalStoryWorkspaceView.xaml` | 待 Stage 3 |
| 15 | 按 Alt 出现白框 | 待实机复现 | U2：需记录 focus / menu access key / capture 状态 | `CanonicalGraphEditorView.xaml.cs`; `MainWindow.xaml` | 待 Stage 2 |
| 16 | 创建角色不选择个体 / 集体 | 源码复现 | `RequestCreate` 仍返回旧 Actor identity 请求 | `ActorWorkspaceDialogs.cs`; `ActorCreationChoiceDialog.xaml` | 待 Stage 3 |
| 17 | 端口、字段和错误未充分中文化 | 源码复现 | display name 与 validation presentation 仍直接显示英文技术文本 | Graph definitions; `CanonicalStoryWorkspaceView.xaml`; presentation boundary | 待 Stage 4 |
| 18 | 固定节点在 palette 中置灰、Choice 被禁用 | 源码复现 | palette 仅过滤 compatibility；View 额外禁止 Session choice | `GraphNodeAuthoringService.cs`; `CanonicalGraphEditorView.xaml.cs` | 待 Stage 3 |
| 19 | Story / Session / Task 导航频繁要求保存 | 源码复现 | `CanLeaveCanonicalGraph` 对 dirty editor 明确返回 false | `ShellViewModel.cs`; `CanonicalStoryWorkspaceViewModel.cs` | 待 Stage 1 |
| 20 | 资源 selected state 不持久可见 | 源码复现 | 项目使用 Button 投影选择，缺少 SelectedTreeItem 的容器状态绑定 | `CanonicalStoryWorkspaceView.xaml` | 待 Stage 1 |
| 21 | Settlement 第二个结果重名 | 源码复现 | 新槽默认 display name 固定为“新结果” | `GraphEditSession.cs`; `CanonicalNodeInspectorViewModel.cs` | 待 Stage 3 |
| 22 | 新增节点像逐层刷新 | 源码复现 | Host collections 清空重投影，View 的 CollectionChanged 无条件 `RebuildGraph()` | `GraphEditorHostViewModel.cs`; `CanonicalGraphEditorView.xaml.cs` | 待 Stage 1 |
| 23 | Objective ComboBox 风格异常、类型边界不清 | 源码复现 | 目标选择控件未统一主题；正常类型仅需收口现有三种 | `CanonicalStoryWorkspaceView.xaml`; theme resources | 待 Stage 3/4 |
| 24 | Objective 仍要求 Minecraft raw ID | 源码复现 | schema / Inspector 正常 authoring 仍围绕 entity / item / actor_id 字段 | `CanonicalTaskObjectiveSchema.cs`; `CanonicalNodeInspectorViewModel.cs`; serializer / loader | 待 Stage 3 |
| 25 | 动态端口名称不同导致 anchor 不对齐 | 待实机复验（源码高置信） | 端口视觉布局由内容宽度影响 anchor | `FlowPortControl.cs`; `CanonicalGraphNodeControl.xaml` | 待 Stage 2 |
| 26 | 既定合法连接报错 | 待最小场景复现 | U3：不得改全局 cardinality；需先记录 issue code 与 aggregate projection | `GraphEditValidation.cs`; aggregate projection / reconnect path | 待 Stage 2 |
| 27 | 默认“进入区域”即使有其它 trigger 也删不掉 | 源码复现 | Start 删除规则把初始项当永久固定项，而非仅保证至少一条 | `StoryStartSchema.cs`; `GraphEditSession.cs`; Inspector | 待 Stage 3 |
| 28 | 新建“进入故事”立即报错 | 源码复现 | legacy `enter_story` 仍进入 Supported UI choices | `StoryStartSchema.cs`; Inspector | 待 Stage 3 |
| 29 | Inspector 控件缺少明确字段名 | 源码复现 | 多个控件依赖裸输入框 / tooltip / 值文本表达语义 | `CanonicalStoryWorkspaceView.xaml` | 待 Stage 4 |

## 源码审计补充 A / B / C / D

| 项 | 是否复现 | 根因 | 目标文件 | 最终修复 |
|---|---|---|---|---|
| A Session【起始】旧 ◆输出 | 源码复现 | 新 authoring definition 仍生成 `logic_out` | `GraphNodeDefinitionRegistry.cs`; migration | 待 Stage 3 |
| B 【物品】右键 fallback 成【角色】 | 源码复现 | folder-kind fallback 未显式覆盖 Items | `CanonicalStoryWorkspaceView.xaml.cs` | 待 Stage 1 |
| C 固定节点菜单源错误 | 源码复现 | authoring scope 未排除 Required / Unique / NonDeletable | `GraphNodeAuthoringService.cs` | 待 Stage 3 |
| D 旧 Actor UI 与新 Identity 并存 | 源码复现 | Schema 3 资源存在，但 Shell 创建入口仍使用旧 Actor 请求 | `ActorWorkspaceDialogs.cs`; actor view models / dialogs | 待 Stage 3 |

## Stage 0 验证状态

- [x] 基线 commit 精确为 `96c821d1a2b4038fecdc62bdbbbd8078291a8d0e`。
- [x] 已建立 `codex/0.3.1.1` 分支，并保留既有 dirty / untracked 文件。
- [x] Core baseline tests：`344/344` PASS，TRX：`TestResults/Core/Core-0.3.1.0-baseline.trx`。
- [x] WPF baseline tests：`328/328` PASS，TRX：`TestResults/Wpf/Wpf-0.3.1.0-baseline.trx`。
- [ ] 人工审计复现项目：待真实 Studio 操作后保存。
- [ ] U1 / U2 / U3：待真实 Studio 最小场景记录。

## Stage 5 最终实现状态

> 本节是上方 Stage 0 “待 Stage”栏位的最终结果补充；人工 Studio 证据边界保持不变。

| 范围 | 最终状态 | 证据摘要 |
|---|---|---|
| #1-#2、#13 Shell 收敛 | 已实现 | 新建 Story 主操作保持居中；移除 app rail、Settings page、大标题和重复返回按钮；窗口标题为 0.3.1.1 |
| #4-#5、#19-#20、#22 Workspace 稳定性 | 已实现并自动化验证 | 增量 snapshot / stable item identity；图增量视觉更新；导航保留 editor；SelectedTreeItem 视觉绑定 |
| #6-#9、#11-#12 Graph 操作 | 已实现并自动化验证 | 编辑/删除菜单、aggregate 导航、ghost、实线 wire、reconnect、事务化多线、剪刀 / Left Alt |
| #10、#14、#16-#18、#21、#23-#24、#27-#29 Authoring / 中文 | 已实现并自动化验证 | 隐藏内部 ID；typed actor；参数摘要与资源 drop；固定节点过滤；Choice；结果 N；Objective / Give Item 资源选择；中文 Validation |
| #25 端口 anchor | 已实现并自动化验证 | 固定 11×11 anchor slot；label 方向不改变连接点；长短名称测试覆盖 |
| #3 / U1 | 待真实 Studio 复验 | Bottom Dock 已具备折叠与纵向 GridSplitter；仍需在目标分辨率 / DPI 判断是否还有局部纵向空间问题 |
| #15 / U2 | 待真实 Studio 复验 | Left Alt 生命周期、无焦点装饰端口和剪刀状态已有自动化覆盖；未取得用户现场白框的真实 UI 复现证据 |
| #26 / U3 | 待用户场景复验 | Flow / Logic cardinality、aggregate projection、single / incident reconnect 已有最小规则测试；未取得原截图对应的具体 ValidationIssue.Code，未改全局 cardinality |
| 审计 A / B / C / D | 已实现并自动化验证 | Session Start 新模型与旧警告；Items 右键分类；fixed palette 过滤；旧 Actor authoring 入口收口 |

## 最终自动化证据

- Core：`345/345` PASS，0 fail，0 skip；`TestResults/Core/Core-0.3.1.1-final.trx`。
- WPF：`350/350` PASS，0 fail，0 skip；`TestResults/Wpf/Wpf-0.3.1.1-final.trx`。
- Release build：PASS，0 error；Studio EXE FileVersion / ProductVersion 均为 `0.3.1.1`。
- `git diff --check`：PASS（只有 LF/CRLF 提示）。
- 第 14 章 S1-S19：仍待真实 Studio 人工验收，不以自动化结果替代。
