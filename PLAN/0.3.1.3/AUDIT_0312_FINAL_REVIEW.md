# 0.3.1.2 原始 19 条审计最终复核

最终复核于 2026-09-01 重新打开原始 DOCX，逐页读取 12 页、18 张内嵌截图；重读产物见 `evidence/audit/`。状态只使用 PLAN 允许的 `AGENT_VERIFIED` / `BLOCKED`，不代表用户接受。

## Audit #1

- 原文核心要求: Start 下拉必须真的改变类型、参数和数据。
- Root Cause: 类型选择未以 typed payload、持久化和 Undo/Redo 的完整事务验证。
- Implementation: 类型切换统一经 schema mutation，重建对应参数并保留稳定端口。
- Release EXE Steps: 打开 Start，展开类型下拉，切换“角色交互/进入区域”，保存并检查 JSON。
- Expected: 类型、字段和 JSON 同步变化。
- Actual: 实窗完成弹出与两类切换，最终角色参数落盘；但没有可信连续帧完整覆盖一次下拉选择前后。
- Evidence: `live/04-trigger-type-popup.png`, `live/11-fixed-actor-parameter-and-node-inspector.png`。
- Automated Support: Core/WPF final；Start typed payload、save、Undo/Redo tests。
- Status: **BLOCKED — NEED_USER_VERIFICATION**

## Audit #2

- 原文核心要求: 每个字段显示用途名，多条进入区域可区分。
- Root Cause: Inline/Inspector 直接排列输入框且条件身份弱。
- Implementation: 增加名称、类型、坐标、半径标签和条件卡标题。
- Release EXE Steps: 创建四条条件并同时查看节点与 Inspector。
- Expected: 不靠占位符也能识别每个字段及每条条件。
- Actual: 四条条件的名称、类型和参数用途清晰可见。
- Evidence: `live/02-start-four-conditions-light.png`, `live/03-start-inspector-scroll.png`。
- Automated Support: WPF final；Start card/label tests。
- Status: **AGENT_VERIFIED**

## Audit #3

- 原文核心要求: 统一为“启动条件”，参数卡显示名称且边界清晰。
- Root Cause: 多视图各自硬编码旧术语和“新触发”。
- Implementation: 统一术语、卡片边界与未占用序号命名。
- Release EXE Steps: 添加多条条件并检查菜单、节点、Inspector。
- Expected: 只出现“启动条件”，卡片有独立名称。
- Actual: 实窗显示“启动条件 1…4”与清晰卡片边界。
- Evidence: `live/02-start-four-conditions-light.png`, `live/03-start-inspector-scroll.png`。
- Automated Support: Core/WPF final；默认名称与术语 tests。
- Status: **AGENT_VERIFIED**

## Audit #4

- 原文核心要求: 输出端口真正靠节点右边缘。
- Root Cause: 动态节点内容和端口没有稳定右侧布局列。
- Implementation: 输出端口固定到右侧端口列。
- Release EXE Steps: 在实际 Story/Session 节点观察并连线。
- Expected: 输出锚点贴近节点右边缘且线从该处起始。
- Actual: 实际节点输出端口与右缘对齐，连线从右侧起始。
- Evidence: `live/17-session-task-aggregate-nodes.png`, `live/18-normal-reconnect-ctrl-bundle-result.png`。
- Automated Support: WPF final；node output column geometry tests。
- Status: **AGENT_VERIFIED**

## Audit #5

- 原文核心要求: Alt 光标为较小的黑白剪刀。
- Root Cause: 原实现为 32×32 多色像素剪刀。
- Implementation: 16×16 单色剪刀，热点 `(6,8)`。
- Release EXE Steps: 按住 Alt 移到画布并捕获真实系统 cursor。
- Expected: 小型黑白剪刀，热点稳定。
- Actual: 代码与 cursor handle tests 通过；当前录屏环境只捕获到系统箭头，不能冒充真实剪刀视觉证据。
- Evidence: 无可信实窗 cursor 图；错误捕获已从证据集中移除。
- Automated Support: WPF final；`ScissorsCursorFactoryTests`。
- Status: **BLOCKED — NEED_USER_VERIFICATION**

## Audit #6

- 原文核心要求: ghost 与落点保持鼠标相对锚点，证据包含鼠标。
- Root Cause: ghost 与锚点均为固定尺寸/中心点。
- Implementation: 保存真实抓取偏移并以节点视觉尺寸绘制 ghost。
- Release EXE Steps: 从资源树非中心位置抓取 Session，拖到画布后释放。
- Expected: ghost 不跳到中心，落点保持同一相对锚点。
- Actual: 拖动帧同时显示鼠标和 ghost；释放后的 Session placement 对应同一锚点。
- Evidence: `live/16-session-ghost-during-real-drag.png`, `live/17-session-task-aggregate-nodes.png`。
- Automated Support: WPF final；ghost anchor/drop coordinate tests。
- Status: **AGENT_VERIFIED**

## Audit #7

- 原文核心要求: 标签不是命中区，端口命中范围适度扩大。
- Root Cause: 20 DIP 槽存在但命中仅接受 9/11 DIP 图形。
- Implementation: 20×20 DIP 槽命中，可见形状 9–11 DIP，标签排除。
- Release EXE Steps: 分别点击端口中心、边缘 +3/+4 DIP、标签和空白。
- Expected: 前三者按矩阵命中，标签/空白不命中。
- Actual: 实窗连线成功；本轮没有可信坐标序列覆盖完整命中矩阵。
- Evidence: `live/18-normal-reconnect-ctrl-bundle-result.png`（仅证明实际连线）。
- Automated Support: WPF final；`FlowPortControlTests` 完整 20 DIP/标签矩阵。
- Status: **BLOCKED — NEED_USER_VERIFICATION**

## Audit #8

- 原文核心要求: Inspector 可滚动，末尾条件仍可访问。
- Root Cause: 右栏根容器不可滚动。
- Implementation: Inspector 内容进入独立 ScrollViewer。
- Release EXE Steps: 创建四条条件并滚动到最下方。
- Expected: 下方参数可访问且 Graph 不被误缩放。
- Actual: 实窗滚动到末尾条件，参数完整可见。
- Evidence: `live/03-start-inspector-scroll.png`。
- Automated Support: WPF final；Inspector scroll layout tests。
- Status: **AGENT_VERIFIED**

## Audit #9

- 原文核心要求: 普通拖只新增一线，Ctrl 才整体移动；单线重连保持另一端。
- Root Cause: 起拖逻辑只看 incident 数，未看 Ctrl，且没有正式 reconnect 状态。
- Implementation: 拆分 new/reconnect/bundle 三种 wire gesture。
- Release EXE Steps: 实际创建多线、从占用端重连、Ctrl 拖 bundle。
- Expected: 新增/重连/bundle 互不混淆，取消不损坏原线。
- Actual: 实窗得到四线结果并完成重连及 Ctrl bundle；但没有连续帧/视频覆盖 C7 的 Flow 与 Logic 全矩阵及取消路径。
- Evidence: `live/17-session-task-aggregate-nodes.png`, `live/18-normal-reconnect-ctrl-bundle-result.png`。
- Automated Support: Core/WPF final；wire gesture/reconnect/bundle/cancel tests。
- Status: **BLOCKED — NEED_USER_VERIFICATION**

## Audit #10

- 原文核心要求: 角色/物品 Inspector 补齐名称、ID、组 ID、标签并统一类型名。
- Root Cause: Inspector 只投影通用资源摘要。
- Implementation: 四类资源投影名称、canonical ID、Group ID、tags、ownership；统一中文类型名。
- Release EXE Steps: 逐一选择角色、角色组、物品、物品组。
- Expected: 四类字段与术语完整。
- Actual: 实窗验证角色与 Task 资源 Inspector；物品/两个组类型未逐一形成实窗证据。
- Evidence: `live/08-actors-expanded-before-drag.png`, `live/11-fixed-actor-parameter-and-node-inspector.png`。
- Automated Support: Core/WPF final；四类资源 Inspector tests。
- Status: **BLOCKED — NEED_USER_VERIFICATION**

## Audit #11

- 原文核心要求: palette 移除“聚合”，Session/Task 只能从资源库拖入。
- Root Cause: aggregate 定义仍进入 palette，仅命令层拒绝。
- Implementation: 从 palette 过滤 aggregate；资源拖放成为唯一 placement 创建路径。
- Release EXE Steps: 打开添加节点菜单，再从资源树拖入 Session/Task。
- Expected: 菜单无“聚合”，拖放可创建 placement。
- Actual: 实窗通过资源拖放创建 Session/Task placement，添加菜单无聚合分类。
- Evidence: `live/16-session-ghost-during-real-drag.png`, `live/17-session-task-aggregate-nodes.png`。
- Automated Support: Core/WPF final；palette filtering/drop-only tests。
- Status: **AGENT_VERIFIED**

## Audit #12

- 原文核心要求: 条件判断位于逻辑输出之后。
- Root Cause: registry 排序值错误。
- Implementation: 调整 palette ordering。
- Release EXE Steps: 打开节点菜单并检查逻辑分类顺序。
- Expected: 逻辑输出在前，条件判断在后。
- Actual: Registry 和自动化顺序正确；没有保留可信实窗菜单截图。
- Evidence: 无独立实窗证据。
- Automated Support: Core/WPF final；registry ordering tests。
- Status: **BLOCKED — NEED_USER_VERIFICATION**

## Audit #13

- 原文核心要求: Light Theme 具有节点、边框、端口、ghost、Inspector 层次。
- Root Cause: 多处硬编码深色/白色画刷。
- Implementation: 改用动态主题资源并补齐边界对比。
- Release EXE Steps: Light/Dark 下以两种窗口尺寸打开 Project Home 与 Graph。
- Expected: 两主题均无白成一片或边界丢失。
- Actual: DPI 119 下 Light/Dark、1364×868 与 1700×980 实窗层次清晰；机器无法精确切换 100%/150%，已按 PLAN 记录实际 DPI。
- Evidence: `live/01-project-home-light.png`, `live/10-restarted-fixed-build-project-home-dark.png`, `live/30-light-project-home-1100x700-logical-dpi119.png` 至 `live/33-dark-project-home-1100x700-logical-dpi119.png`。
- Automated Support: WPF final；dynamic resource/theme tests。
- Status: **AGENT_VERIFIED**

## Audit #14

- 原文核心要求: 删除/解除 Session/Task 清理所有 placement；反向删除 placement 不删资源。
- Root Cause: Story graph cleanup 不在生命周期补偿事务，活跃 host 还会保留陈旧 snapshot。
- Implementation: 资源、membership、placement/wire 同事务；成功后 workspace 采用持久化 snapshot，失败回滚。
- Release EXE Steps: 删除含线 Session，随后编辑 Task 并保存；再只删 Task placement。
- Expected: Session 文件/placement/线消失且不复活；Task 文件保留。
- Actual: 首轮发现复活缺陷并修复；复测中后续保存未复活 Session，删除 Task placement 后 Task 资源仍在。
- Evidence: `live/19-session-resource-delete-confirmation.png`, `live/22-post-delete-edit-no-resurrection.png`, `live/23-placement-delete-task-resource-preserved.png`。
- Automated Support: Core/WPF final；transaction rollback 与 no-resurrection regression。
- Status: **AGENT_VERIFIED**

## Audit #15

- 原文核心要求: Objective 真可编辑、切换不卡；命名正确；角色交互无数量。
- Root Cause: schema 误要求 `required`，所有 Inspector 订阅全图重建。
- Implementation: 三种语义和中文名收口；角色交互移除数量；targeted refresh 与 Runtime projector 同步。
- Release EXE Steps: 切换物品收集/角色交互并把角色拖入属性参数。
- Expected: 字段即时变化、节点数不变、角色交互无数量、单次无 >1 秒 UI 冻结。
- Actual: 实窗完成物品收集与角色交互切换；角色拖入 Objective 后节点数仍为 2、`actor_id=actor_alpha`；脚本测量含等待为 519ms/1167ms，未覆盖实体击杀 ×10 和完整 100-resource 实窗 fixture。
- Evidence: `live/27-objective-collect-item-switch.png`, `live/28-objective-actor-parameter-switch.png`, `live/29-actor-dropped-into-objective-parameter-node-count-unchanged.png`。
- Automated Support: Core/WPF final；100 nodes/20 inspectors targeted refresh；Java probes PASS。
- Status: **BLOCKED — NEED_USER_VERIFICATION**

## Audit #16

- 原文核心要求: Story/Session/Task 各自保存镜头。
- Root Cause: 单一 view controller 被所有 graph 共用。
- Implementation: 每个 graph host 持有独立 `GraphViewportState`。
- Release EXE Steps: Story 调到 125%并平移，Task 调到 90%并平移，来回切换两次。
- Expected: 各自恢复原 zoom/pan。
- Actual: Story 恢复 125%（节点 x=1112,width=331），Task 恢复 90%（x=697,width=257）。
- Evidence: `live/25-story-viewport-restored-125-percent-pan.png`, `live/26-task-viewport-restored-90-percent-pan.png`。
- Automated Support: WPF final；per-host viewport tests。
- Status: **AGENT_VERIFIED**

## Audit #17

- 原文核心要求: resource reorder 显示明确 before/after 预览。
- Root Cause: DragOver 无插入位置模型或视觉。
- Implementation: 上下半区决定 before/after 并显示插入线。
- Release EXE Steps: 把 actor_beta 拖到 actor_alpha 上方。
- Expected: 释放前显示上方插入线，释放后顺序改变。
- Actual: 实窗显示 before 插入线，落盘顺序为 beta、alpha；未完成 5 资源、after、取消、跨文件夹全矩阵。
- Evidence: `live/24-actor-reorder-before-preview.png`。
- Automated Support: WPF final；before/after/cancel/cross-folder tests。
- Status: **BLOCKED — NEED_USER_VERIFICATION**

## Audit #18

- 原文核心要求: Project Home 为左故事、中 Graph、右 Inspector 三栏。
- Root Cause: 首页只含列表和概览，Graph/Inspector 在独立路由。
- Implementation: 重组为三栏同屏工作区。
- Release EXE Steps: 打开项目首页并选故事/节点。
- Expected: 三栏同时存在且在窗口尺寸变化下可用。
- Actual: Light/Dark 与两种尺寸下三栏稳定显示。
- Evidence: `live/01-project-home-light.png`, `live/10-restarted-fixed-build-project-home-dark.png`, `live/30-light-project-home-1100x700-logical-dpi119.png` 至 `live/33-dark-project-home-1100x700-logical-dpi119.png`。
- Automated Support: WPF final；Project Home composition tests。
- Status: **AGENT_VERIFIED**

## Audit #19

- 原文核心要求: 普通错误区只显示可操作中文，技术详情只在 Problems。
- Root Cause: 两处共用同一 formatter。
- Implementation: 拆分 compact user projection 与 detailed diagnostic projection。
- Release EXE Steps: 输入非法半径并分别查看近场和 Problems。
- Expected: 近场无 code/英文详情，Problems 有稳定 code 和技术详情。
- Actual: 实窗近场仅中文；Problems 显示稳定诊断 code 与详情。
- Evidence: `live/13-invalid-radius-near-field-visible.png`, `live/14-problems-detailed-stable-code.png`。
- Automated Support: Core/WPF final；validation presentation tests。
- Status: **AGENT_VERIFIED**

## 汇总

- AGENT_VERIFIED：11（#2 #3 #4 #6 #8 #11 #13 #14 #16 #18 #19）
- BLOCKED / NEED_USER_VERIFICATION：8（#1 #5 #7 #9 #10 #12 #15 #17）
- USER_ACCEPTED：0
- RC 结论：**0.3.1.3 RC — NO-GO / NEED_USER_VERIFICATION**

