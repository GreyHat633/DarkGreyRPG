# 0.3.1.2 原始审计摄取记录

## 输入与基线

- 原始文件：`PLAN/0.3.1.2审计.docx`
- SHA-256：`D399ECA704127358192A4B68699E1A14C7D68FF7EAF5873A0AA4DDEF9418105F`
- 完整读取时间：`2026-09-01T06:40:31+08:00`
- 文档页数：12 页
- 内嵌截图：18 张
- 施工基线分支：`codex/0.3.1.2B`
- 施工基线 HEAD：`c7ac508d317bf687c16f8d3a0c1e9968b2553fdf`
- PLAN 审计基线与实际 HEAD：一致
- 视觉读取：已用 Microsoft Word 2019 导出 PDF，并逐页检查 12 页原文、截图、红色标注、鼠标位置、控件层级、边框、端口、菜单、主题与报错信息。

## 原始 19 条审计

| # | 原文要求核心 | 对应截图/页面 | 初始状态 | 当前基线依据 |
|---:|---|---|---|---|
| 1 | Start 节点中的类型下拉必须真的改变类型、参数和数据，不能只弹出选项。 | 第 1 页 | NEEDS_LIVE_REPRO | 源码存在 TwoWay 选择链，但用户截图否定了“有 setter 即可”的结论；必须用 Release EXE 复现完整 UI→VM→Core→JSON→Undo/Redo 链。 |
| 2 | 每个字段必须显示用途名；两个“进入区域”不能让用户猜哪个是名称、哪个是类型，多条条件必须可区分。 | 第 1–2 页 | SOURCE_CONFIRMED | Inline/Inspector 当前直接排列名称框和类型框，缺少稳定的“名称/类型/坐标/半径”等字段标签。 |
| 3 | “启动方式”“开始触发”等术语统一为“启动条件”，参数卡显示用户名称且边界清晰。 | 第 2–4 页 | SOURCE_CONFIRMED | Workspace、Inline、Tooltip、按钮和确认文本仍混用“启动方式”“开始触发”“新触发”。 |
| 4 | 输出端口必须真正靠节点右边缘对齐。 | 第 4 页 | NEEDS_LIVE_REPRO | XAML 声明了右对齐，但用户截图显示最终布局不满足；需要 Release 实窗像素/命中测量，不能只凭 XAML 判定。 |
| 5 | Alt 剪刀光标应为简洁、较小的黑白剪刀，不要当前过大且花哨的样式。 | 第 5 页 | SOURCE_CONFIRMED | `ScissorsCursorFactory` 当前绘制 32×32、多色青色像素剪刀。 |
| 6 | 资源拖入画布时 ghost 与落点必须保持鼠标相对锚点，截图证据必须包含鼠标位置。 | 第 5–6 页 | SOURCE_CONFIRMED | 当前 ghost 固定 232×76、锚点固定 (116,38)，真实节点最小高度 92 且内容动态，二者结构和锚点不一致。 |
| 7 | 标签不是连线命中区；真实端口命中范围应适度扩大。 | 第 6 页 | SOURCE_CONFIRMED | `FlowPortControl` 外层虽为 20×20，但 `IsAnchorHitTarget` 只接受 9/11px 可见形状，标签已排除但有效端口范围过小。 |
| 8 | Inspector 必须可滚动，多条启动条件时最下方参数仍可访问。 | 第 6 页 | SOURCE_CONFIRMED | 右侧 Inspector 当前是不可滚动的 `StackPanel`。 |
| 9 | 普通拖多容量端口只新增一条线；Ctrl+拖才整体移动；单容量已占用端口应拖动原线的当前端点，另一端固定且原线不断显。 | 第 6–7 页 | SOURCE_CONFIRMED | `BeginWire` 当前只要 incident 数量大于 1 就进入 bundle，未检查 Ctrl；单线重连的瞬态几何未固定另一端。 |
| 10 | 角色/物品 Inspector 补齐名称、NPC_ID/Item_ID/Group_ID、标签；产品类型名用“角色/角色组/物品/物品组”。 | 第 7–8 页 | SOURCE_CONFIRMED | 当前 Inspector 只呈现类型、标题、保存状态与错误；`InspectorId` 未绑定，Actor 未投影 tags，类型仍有“个体物品/集体物品”。 |
| 11 | 添加节点菜单移除“聚合”分类；Session/Task 只能从资源库拖入。 | 第 8 页 | SOURCE_CONFIRMED | Registry 的 Session/Task aggregate 定义仍进入 authoring palette 分组，只在命令层被禁用。 |
| 12 | “条件判断”移到“逻辑输出”下面。 | 第 8–9 页 | SOURCE_CONFIRMED | Registry 当前排序把 `condition` 放在 and/or/not/logic_output 之前。 |
| 13 | Light Theme 不能白成一片，节点、边框、端口、ghost、Inspector 等必须具有清晰层次。 | 第 9 页 | SOURCE_CONFIRMED | Graph、Node、Inline、ghost 与剪刀仍存在多处硬编码深色/白色值，未完整使用动态主题资源。 |
| 14 | 删除/解除 Session 或 Task 资源时清理 Story Flow 中全部对应 placement；反向删除 placement 不删除资源。 | 第 9–10 页 | SOURCE_CONFIRMED | 当前资源 lifecycle 只更新资源文件和 membership，不把 Story graph aggregate cleanup 纳入同一补偿事务。 |
| 15 | Inline 参数必须真可编辑且切换目标不能卡顿；目标名改为“实体击杀/物品收集/角色交互”，角色交互不要求数量。 | 第 10–11 页 | SOURCE_CONFIRMED | Objective schema 对 `interact_actor` 仍要求 `required`；每个节点 Inspector 订阅全图 `GraphChanged` 并全量重建资源选项，类型名称也仍是旧词。 |
| 16 | Story、Session、Task 各自保存独立镜头位置，来回切换恢复各自视口。 | 第 11 页 | SOURCE_CONFIRMED | Graph View 当前只持有一个 view 级 `_viewportController`，host 切换时没有按 graph 保存/恢复状态。 |
| 17 | 资源排序拖动必须显示明确的 before/after 落点预览。 | 第 11 页 | SOURCE_CONFIRMED | 当前 DragOver 只设置 Move，Drop 直接移动到目标索引，没有插入线和上下半区语义。 |
| 18 | Project Home 改为三栏心智模型：左侧故事列表、中间 Story Graph、右侧 Inspector。 | 第 11–12 页 | SOURCE_CONFIRMED | 当前首页在左侧列出故事，中央显示概览；Story Graph 是单独页面，没有同屏右侧 Inspector。 |
| 19 | 普通错误区只显示可操作的中文；code/英文/技术详情只放 Problems。 | 第 12 页 | SOURCE_CONFIRMED | `ValidationIssuePresentation.Format` 当前把 `[code]` 与 `技术详情` 拼进普通 Inspector 错误文本。 |

## PLAN 补充的 6 项同根风险

| # | 风险核心 | 初始结论 |
|---:|---|---|
| 20 | Inline Editor 不应让每个节点在任意 GraphChanged 后全量刷新。 | SOURCE_CONFIRMED — 每个 `CanonicalNodeInspectorViewModel` 都订阅全图事件并重建多个集合。 |
| 21 | 关键输入不能只靠 LostFocus 提交，切换节点/保存/关闭前不得丢最后一次编辑。 | SOURCE_CONFIRMED — 多个 TextBox 仍使用 LostFocus 更新。 |
| 22 | DisplayName rename 必须保留其余全部序列化字段，并做 round-trip deep comparison。 | SOURCE_CONFIRMED — Item rename 当前用新对象逐字段重建，存在遗漏现有/未来字段的结构风险；尚无全类型深比较 Gate。 |
| 23 | 普通 Inspector 错误与 Problems 技术详情必须分层。 | SOURCE_CONFIRMED — 当前两处共用同一个含 code/detail 的 formatter。 |
| 24 | 新增 Start 条件名必须分配第一个未占用的“启动条件 N”。 | SOURCE_CONFIRMED — 当前默认值是“新触发”。 |
| 25 | 审计普通 `interact_actor` / `enter_region` 的正式 authoring 依据。 | SOURCE_CONFIRMED — 当前二者都是普通 Story Flow palette 节点；正式设计只明确把两者作为 Start 启动条件，但 0.3.0.0 Design Plan §10.6 又允许 Actor 拖入空白处创建已配置角色交互节点，形成需在 F4 处理的 `DESIGN_CONFLICT`。 |

## Gate 0 结论

- GATE 0A：完成。
- GATE 0B：本记录已生成；尚未提交 Git，等待与后续 Gate 证据一起按 PLAN 提交。
- GATE 0C：完成；实际 HEAD 与 PLAN 审计基线一致。
- 尚未把任何条目标记为 `FIXED`、`PASS` 或 `USER_ACCEPTED`。
- F4 的 `interact_actor` 正式设计冲突已记录，施工到该决策点前不得擅自移除其新建能力。
