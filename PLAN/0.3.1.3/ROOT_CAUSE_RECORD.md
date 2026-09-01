# 0.3.1.3 Root Cause Record

基线：`c7ac508d317bf687c16f8d3a0c1e9968b2553fdf`

| # | 根因 | 最小正确修改层 | 施工结果 |
|---:|---|---|---|
| 1 | Start 类型选择链只证明 setter 存在，类型切换后的 typed payload、端口与持久化没有形成一个可验证事务。 | Start schema + node Inspector + host mutation | 类型选择统一走 schema mutation，保存、Undo/Redo 与 JSON 同步。 |
| 2 | Inline/Inspector 直接排列输入框，缺少字段语义标签与条件身份。 | Inline node editor | 增加名称、类型、坐标、半径等用途名，并显示条件名称。 |
| 3 | 多个视图和命令各自使用“启动方式/开始触发/新触发”。 | 术语资源与 Start 命名器 | 全部统一为“启动条件”，名称分配第一个未占用序号。 |
| 4 | 端口虽声明右对齐，但节点动态内容与端口布局不共享稳定右侧列。 | Node control layout | 输出端口固定在节点右侧端口列。 |
| 5 | cursor factory 生成 32×32 多色像素图，视觉尺寸和热点均不符合审计。 | Cursor factory | 改为 16×16 黑白剪刀，热点 `(6,8)`。 |
| 6 | ghost 使用固定 232×76 和固定中心锚点，与真实节点尺寸/抓取点无关。 | Graph drag/drop presentation | ghost 复用节点视觉尺寸并保存鼠标相对锚点。 |
| 7 | 20 DIP 外层槽存在，但命中判断只接受 9/11 DIP 可见图形。 | FlowPortControl hit testing | 可见图形保持 9–11 DIP，整个 20×20 槽作为端口命中区，标签排除。 |
| 8 | Inspector 根容器为不可滚动 StackPanel。 | Workspace Inspector XAML | Inspector 内容置于 ScrollViewer。 |
| 9 | wire 起拖只按 incident 数判断 bundle，未区分 Ctrl；单线重连没有保留另一端固定。 | Wire interaction state machine | 普通拖只新增一线；Ctrl 才 bundle；占用单容量端口进入正式重连。 |
| 10 | 资源 Inspector 只投影通用摘要，未暴露 canonical ID、标签与所有权。 | Resource editor VM + Inspector views | 补齐角色/角色组/物品/物品组名称、ID、Group ID、标签和所有权。 |
| 11 | aggregate 定义仍注册到 authoring palette，仅命令层拒绝创建。 | Definition registry/palette filtering | Session/Task aggregate 从 palette 移除，只能由资源拖入产生。 |
| 12 | `condition` 的注册顺序早于逻辑运算与 `logic_output`。 | Definition registry ordering | 条件判断排到逻辑输出之后。 |
| 13 | Graph、Node、ghost、Inspector 多处硬编码颜色。 | Dynamic theme resources | 使用动态主题画刷，Light/Dark 均保留边界层次。 |
| 14 | resource lifecycle 只改资源与 membership，Story graph cleanup 不在补偿事务；活跃 host 还可能保留旧快照。 | Lifecycle transaction + workspace snapshot reconciliation | 删除/解除同事务清理全部 placement/wire，失败回滚；成功后活跃 Story 采用持久化快照，避免复活。 |
| 15 | Objective schema 误要求角色交互数量；每个 Inspector 订阅全图并重建选项。 | Objective schema + targeted refresh + Runtime projector | 三种语义统一，角色交互无数量；只刷新目标节点，Java Runtime 同步投影。 |
| 16 | View 只有一个 `_viewportController`，host 切换无 graph 状态。 | `GraphViewportState` per host | Story/Session/Task 独立保存 zoom/pan。 |
| 17 | DragOver 只有 Move 效果，Drop 直接用目标索引。 | Resource tree reorder adorner/state | 上下半区计算 before/after 并显示插入线。 |
| 18 | Project Home 只显示故事列表和概览，Graph/Inspector 在别的路由。 | Project Home composition | 改为故事列表、Story Graph、Inspector 三栏。 |
| 19 | 普通 Inspector 和 Problems 共用含 code/detail 的 formatter。 | Validation presentation split | 普通区仅可操作中文；Problems 保留稳定 code 与技术详情。 |
| 20 | 每个 node Inspector 监听任意 GraphChanged 并全量重建资源集合。 | Targeted node change signatures | 只通知受影响节点；资源选项按相关资源版本刷新。 |
| 21 | TextBox 只在 LostFocus 写回，直接保存/切换/关闭可能丢尾字符。 | Binding update + save preflight | 关键字段即时写源，Ctrl+S 先提交焦点内编辑。 |
| 22 | rename 通过构造新对象逐字段复制，新增字段容易遗漏。 | Immutable copy/serializer round trip | 保留除 DisplayName 外的全部序列化字段，并以深比较回归。 |
| 23 | 同 #19，展示层未区分用户信息与诊断信息。 | Validation presentation split | compact Inspector 与 detailed Problems 使用不同投影。 |
| 24 | 默认条件名固定为“新触发”。 | Start naming allocator | 分配第一个未占用的“启动条件 N”。 |
| 25 | `interact_actor`/`enter_region` 同时被当作普通 palette 节点和 Start 条件，且旧设计对角色拖空白存在冲突。 | Registry + drop contract | 采用用户明确边界：二者是 Start 条件类型；角色资源可拖入现有节点属性参数，但绝不作为节点创建。旧文件只读兼容并告警。 |

