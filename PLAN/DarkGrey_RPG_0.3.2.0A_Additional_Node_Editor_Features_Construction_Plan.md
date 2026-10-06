# DarkGrey_RPG 0.3.2.0_A 追加功能 Construction PLAN

> **文档性质：当前已在开发中的 `0.3.2.0_A` 的增量追加计划（Delta Plan）。**
>
> 本文不替代、不重排、不回滚既有 `0.3.2.0_A` 主 PLAN，也不要求从旧版本重新开工。执行者必须先读取当前实际 working tree、现行 `0.3.2.0_A` PLAN、未提交修改与已有验收记录，再把本文作为追加工作并入当前开发。
>
> 本次只锁定两项 Studio 图编辑器追加功能：
>
> **A7：框选 / 多选 / 批量移动**  
> **A8：Shift + 单节点拖动，将节点插入现有连线**
>
> 不在本文中顺带扩展 Task 生命周期、事件体系、Minecraft Runtime、通用变量系统或新的通用交互框架。

---

## 0. 执行前置：先与当前 0.3.2.0_A 对齐

`0.3.2.0_A` 已经开发一段时间，本 PLAN 不能按“从 `0.3.1.5` 开始实现”的方式执行。

Codex 开工前必须：

1. 读取当前实际 `0.3.2.0_A` 分支 / working tree。
2. 读取现有 `0.3.2.0_A` 主 PLAN 与已有追加 PLAN。
3. 检查未提交修改；禁止 reset / checkout 覆盖当前开发工作。
4. 定位当前版本中的 canonical 图编辑器 View、pointer/gesture transient state、Graph Host/EditSession、connection validation、atomic replace/reconnect、layout persistence、scissors、Ctrl 多连线 reconnect、node deletion 与 selection。
5. 对 A7/A8 分别标记：`NOT_STARTED / ROOT_CAUSE_CONFIRMED / IMPLEMENTED / AGENT_VERIFIED / USER_ACCEPTED / BLOCKED`。
6. 如果当前版本已经实现其中一部分，只补缺口，不重写已工作的能力。

### 已知旧基线：仅供找入口

`codex/0.3.1.5` 中相关入口曾包括：

- `studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalGraphEditorView.xaml.cs`
- `studio/src/DarkGreyRPG.Studio/ViewModels/Graph/GraphEditorHostViewModel.cs`
- `studio/src/DarkGreyRPG.Studio/Views/FlowPortControl.cs`
- `studio/src/DarkGreyRPG.Studio/Views/Graph/GraphPointerState.cs`
- `studio/src/DarkGreyRPG.Studio.Wpf.Tests/CanonicalGraphEditorViewTests.cs`
- `studio/src/DarkGreyRPG.Studio.Wpf.Tests/GraphEditorHostViewModelTests.cs`
- `studio/src/DarkGreyRPG.Studio.Wpf.Tests/FlowPortControlTests.cs`
- `studio/src/DarkGreyRPG.Studio.Wpf.Tests/ScissorsCursorFactoryTests.cs`

旧基线已经有 Alt 临时剪刀、Ctrl+端口拖动整束连接、单节点布局拖动以及 Host/EditSession 级 connection replace 能力。若 `0.3.2.0_A` 已经改名或移动这些入口，跟随当前代码，不得为了匹配旧结构重新造一套。

---

# A7 — 框选 / 多选 / 批量移动

## A7.1 产品目标

加入成熟节点编辑器常见的批量布局操作：

- 空白区域左拖框选多个节点；
- Ctrl+节点点击增量选择 / 取消单个节点；
- Ctrl+空白左拖增量框选；
- 拖动任意一个已选节点时整体移动当前 Selection；
- 保持所有节点相对位置；
- 支持批量删除可删除节点；
- Required / NonDeletable / 固定节点继续受到保护。

A7 只属于 Studio authoring UX，不改变 Graph JSON 语义、Flow/Logic 规则、Runtime 或 Minecraft。节点位置继续走现有 Studio layout persistence。

## A7.2 选择语义

### 单击节点

普通单击一个未选节点：

```text
清除旧 Node Selection
→ 只选中当前节点
```

如果当前已经多选 A/B/C，而用户从其中一个已选节点 C 直接开始拖动：

```text
保留 A/B/C
→ 整体移动
```

不能在 MouseDown 一开始就把 Selection 压缩为 C；需要通过 drag threshold 区分“点击重新选择”和“从现有 Selection 开始拖动”。

### Ctrl + 节点点击

```text
未选中 → 加入 Selection
已选中 → 从 Selection 移除
```

不得影响现有：

```text
Ctrl + Port Drag → 多容量端口整束 reconnect
```

端口 gesture 优先于节点选择。

### 空白点击

```text
单击空白 → 清空 Node Selection 与 Connection Selection
```

若从空白开始拖动并超过系统 drag threshold，则进入 marquee。

## A7.3 Marquee

普通空白左拖：

```text
Blank MouseDown
→ 超过 drag threshold
→ 显示 marquee rectangle
→ 与 marquee 矩形相交的节点成为 Selection
```

V1 使用 **矩形相交（intersects）**，不要求节点完全包含在框内。

Ctrl+空白左拖：

```text
保留 drag 开始前的 Selection
+
把本次 marquee 命中的节点加入 Selection
```

本版本不做复杂 XOR 框选反选。

### 视觉

marquee 只需要半透明填充 + 清楚边框，并且 Dark/Light Theme 都可辨认。禁止为了框选新建 selection theme engine、粒子、动画或大面积发光。

## A7.4 多选视觉与 Inspector

所有 selected node 使用同一套明确但不过度显眼的 selected 视觉，不需要 Primary/Secondary Selection 体系。

如果当前 Inspector 只支持单节点：

```text
多选时 → 可显示“已选择 N 个节点”或清空单节点 Inspector
```

本次不实现通用批量 Inspector。

Selection 是当前 graph editor session 的 transient UI state。禁止新增 Generic Selection Service、跨 Studio Selection Registry、消息总线或通用 UI framework。

## A7.5 批量移动

开始拖动 Selection 中任意节点时：

```text
记录每个 Selected Node 的 drag origin
→ PointerMove 计算统一 ΔX / ΔY
→ 每个节点 NewPosition = Origin + Δ
```

要求：

- 所有节点保持相对位置；
- incident wires 实时重绘；
- 不改变任何 connection；
- 经过端口、连线、其他节点时不触发 reconnect 或 topology 修改；
- 最终位置走当前 layout persistence；
- Studio 保存、关闭、重启后位置仍保持。

不得把 X/Y 为此塞入 Runtime semantic JSON，也不得另造第二套 layout 文件。

## A7.6 Escape / 取消

Marquee 期间：

```text
Esc
→ marquee 消失
→ 恢复 drag 开始前 Selection
```

批量移动优先复用当前 pointer cancellation 习惯。若当前实现可低成本保存 origins，则 Esc 恢复本次拖动前位置；若 `0.3.2.0_A` 当前单节点拖动取消并非这种语义，则保持现有一致性，不得为了本功能新建全图快照/Undo 系统。

## A7.7 批量删除

Delete 在多选时：

1. 对每个节点读取当前 canonical deletion policy。
2. 可删除节点一起删除。
3. Required / NonDeletable / 固定节点保留。
4. incident wires 按既有 graph removal semantics 清理。
5. Selection 同时含固定与普通节点时，不因一个固定节点让整个操作失败。
6. 不弹 N 个确认框。
7. 当前已有的“资源引用删除保护”若存在，继续沿用，不得绕过。

---

# A8 — Shift + 单节点拖动插入现有连线

## A8.1 最高优先级交互契约

> **普通节点拖动永远只负责布局。只有拖动单个节点期间按住 Shift，才启用插线候选。松开 Shift 立即退出候选；MouseUp 时 Shift 仍按住且候选合法，才允许修改拓扑。**

A8 不是默认自动插线。

## A8.2 Modifier 分工

| 输入 | 行为 |
|---|---|
| 普通空白左拖 | 框选 |
| Ctrl + 空白左拖 | 增量框选 |
| Ctrl + 节点点击 | 增量 / toggle Selection |
| 拖动选中节点 | 移动 Selection |
| **Shift + 单节点拖动** | **尝试插入现有连线** |
| Alt | 剪刀 |
| Ctrl + 多容量端口拖动 | 整束 reconnect |

强约束：

- **Alt 不得复用给插线。**
- **Ctrl 不得复用给插线。**
- Shift 是 A8 唯一 modifier。

## A8.3 Shift 必须实时进入 / 退出

用户可以先普通拖动 C：

```text
拖 C
→ 没有插线反馈
→ C 经过 A→B
→ 仍没有插线反馈
```

拖动过程中按下 Shift：

```text
Shift Down
→ 立即查找 candidate wire
→ 合法 A→B 高亮
→ 显示 A→C→B ghost preview
```

继续移动离开：

```text
旧 candidate 立即取消
→ 新合法 candidate 才能亮
```

松开 Shift：

```text
candidate 立即清除
ghost preview 立即消失
A→B 恢复正常
当前 drag 继续作为普通布局拖动
```

即使随后就在原线附近 MouseUp，也不得提交 splice。

## A8.4 仅单节点 Drag 允许 Splice

如果实际移动集合：

```text
SelectedNodes.Count > 1
```

则即使 Shift 按下：

```text
不进入 splice
不高亮 candidate
不显示 ghost
MouseUp 不改变 connection
```

V1 不做多节点自动入口/出口推断、不做整组插入、不做宏节点、不根据位置猜链顺序。

## A8.5 Candidate Wire 命中

只有同时满足：

```text
PointerMode == NodeDrag
&& 实际拖动节点数量 == 1
&& Shift == Down
```

才启用 splice candidate hit-test。

普通 drag 完全不做 splice candidate，也不显示“经过连线”的候选视觉。

候选 wire 命中优先复用当前 connection transparent hit path / geometry distance，不再建立另一套彼此不一致的连线命中系统。

## A8.6 V1 端口解析：必须“不猜”

设原连接：

```text
A.out → B.in
```

若 InterfaceKind = Flow，拖入 C 后只有在能够 **唯一确定**：

```text
C.flowInput
C.flowOutput
```

时，才允许候选：

```text
A.out → C.flowInput
C.flowOutput → B.in
```

Logic 同理。

候选解析：

1. 找 C 上方向正确、InterfaceKind 正确的 Inputs。
2. 找同 kind 的 Outputs。
3. 对所有 `(input, output)` pair 做完整 splice preflight。
4. **只有恰好一个 pair 能安全完成整个 replacement 时 candidate 才合法。**

结果：

```text
0 个合法 pair → invalid
>1 个合法 pair → ambiguous → invalid
```

禁止猜第一个端口。

因此像【选择】这类多个 Flow Output 的节点，如果存在多个合法 pair 且无法唯一决定：

```text
不显示有效候选
不提交
不弹 Toast
```

以后若确实需要“用户选择插入端口”，另行设计；A8 V1 不做 popup。

## A8.7 Cardinality 与已有连接保护

继续严格遵守：

```text
Flow Output → max 1 target
Flow Input  → 0..N sources

Logic Output → 0..N targets
Logic Input  → max 1 source
```

A8 不得为了制造合法 splice 而偷偷断开 C 的 unrelated wire。

例如 C.flowOutput 已经被占用：

```text
C.out → X
```

而新 splice 会违反 Flow Output cardinality，则：

```text
candidate invalid
→ 无有效高亮
→ 无提交
→ C→X 保留
```

A8 只允许替换当前候选的原 connection，不能静默修改 dragged node 的其他 incident connections。

## A8.8 Preflight 必须针对“最终 replacement”

不能只看端口颜色，也不能在 live graph 上“先断原线，再试新线”。

原状态：

```text
A → B
```

最终候选：

```text
A → C
C → B
```

Preflight 必须把 `A→B` 视为将被替换，验证整个最终状态：

```text
remove [A→B]
add [A→C, C→B]
```

优先复用当前 authoritative Graph validation / EditSession / ReplaceConnections seam。

禁止：

- preview 阶段修改正式 Graph 再撤回；
- 第一条新线成功后第二条失败；
- 先删原线导致失败后丢线；
- 为 A8 建另一套 cardinality validator。

## A8.9 Preview 视觉

合法 candidate 出现时至少：

1. 原候选 wire 明确进入 candidate highlight；
2. 显示两段 ghost/preview：
   - `A → C.input`
   - `C.output → B`
3. Flow preview 使用 Flow 视觉语言；
4. Logic preview 使用 Logic 视觉语言。

正式 Graph 在 preview 阶段必须保持不变。

原 wire 可以变暗/高亮，但在提交前不能从 formal data 消失。

Shift KeyUp 要在同一交互周期内立即清除 candidate 与 ghost。

## A8.10 MouseUp 原子提交

MouseUp 必须重新确认：

```text
Shift 仍按下
&& 只有一个 dragged node
&& candidate 仍存在
&& candidate preflight 仍合法
```

正式 mutation 应等价于：

```text
ReplaceConnections(
  remove: [A→B],
  add: [
    A→C.input,
    C.output→B
  ]
)
```

必须保证：

成功：

```text
A→B 不存在
A→C 存在
C→B 存在
```

失败：

```text
A→B 完整保留
A→C 不存在
C→B 不存在
```

绝不能产生半完成状态。

若当前 EditSession 已把 replace 视为一个 Undo step，直接复用；若当前会生成多个历史步骤，只允许在既有 transaction seam 内做最小修补，不新增全局 Undo framework。

## A8.11 Invalid / Cancel 行为

以下全部只按“普通布局拖动结束”处理，不弹错误 Toast：

- Shift 未按；
- Shift 中途松开；
- 没命中 wire；
- kind 不兼容；
- 节点没有 pass-through pair；
- candidate pair > 1；
- cardinality 不允许；
- MouseUp 前 candidate 失效；
- multi-node drag；
- validation 失败。

技术 issue 如需保留，进入 Problems/debug seam，不污染直接操控界面。

---

# A7 + A8 输入优先级

对象命中优先级：

```text
Port hit
→ Port/Wire gesture

Connection hit + Scissors active
→ Cut

Node parameter control
→ inline/Inspector interaction

Node draggable surface
→ Node Selection / Node Drag

Blank canvas
→ Clear / Marquee / Pan
```

Modifier 在确定对象上下文后解释：

```text
Alt → Scissors
Ctrl + Port Drag → Bundle Reconnect
Ctrl + Node/Canvas → Selection additive/toggle
Shift + Single Node Drag → Splice
```

不要先解释 modifier 再判断鼠标对象，否则容易出现 Ctrl 点端口被 selection 抢走、Alt 剪刀失效、Shift 多选误改 topology。

---

# Pointer State：只做最小扩展

A7 可以在现有 transient pointer state 上增加概念上的：

```text
MarqueeSelection
```

A8 不必另造一个大 PointerMode。推荐：

```text
NodeDrag
+
Shift state
+
_spliceCandidate
```

可能需要的 transient data：

A7：

```text
_selectionBeforeMarquee
_marqueeStart
_marqueeRect
_dragNodeOrigins
```

A8：

```text
_spliceCandidate
_splicePreviewVisuals
```

禁止新增：

- InteractionModeManager
- Gesture Router Framework
- Global Selection Service
- Generic Graph Transaction Framework
- 插件 API
- 后台 worker
- DB

---

# 历史 Wire / Scissors 回归边界

## Alt 剪刀

必须继续满足：

```text
Left Alt Down → 临时剪刀
Left Alt Up   → 恢复按 Alt 前的剪刀状态
```

若按钮常驻剪刀本来已开启，Alt Down/Up 不能把常驻剪刀关掉。

## 多容量端口 Ctrl Drag

多容量：

- Flow Input
- Logic Output

继续：

```text
普通 Drag → 新增一根 connection
Ctrl + Drag → 移动该端口全部既有 connections
```

A7 的 Ctrl selection 不得抢走 Port gesture。

## 单容量已占用端口 reconnect

- Flow Output
- Logic Input

必须继续：

- 抓起实际被拖 endpoint；
- opposite endpoint 固定；
- 原线在 drag 中保持正确视觉；
- valid target 原子 reconnect；
- Escape 恢复；
- blank drop disconnect。

A8 不允许改变这套语义。

---

# Work Packages

## WP-A — 0.3.2.0_A Current-State Alignment

产出：

- 当前实际实现入口；
- A7/A8 当前完成度；
- 与进行中修改冲突的文件；
- 最小修改范围。

未完成这一步，不得开始大规模改代码。

## WP-B — Minimal Selection Extension

实现：

- transient SelectedNodes；
- single select；
- Ctrl toggle；
- clear；
- selection visuals；
- 与现有 Inspector 的单选/多选边界。

不做批量 Inspector。

## WP-C — Marquee

实现：

- blank drag threshold；
- marquee visual；
- intersects hit test；
- Ctrl additive；
- Esc cancellation；
- Dark/Light 可见。

## WP-D — Multi-node Move + Layout

实现：

- drag origins；
- one delta for N nodes；
- realtime incident-wire redraw；
- existing layout persistence；
- no topology mutation。

## WP-E — Multi-delete

实现：

- delete selected deletable nodes；
- Required / NonDeletable 保护；
- 复用已有 node removal；
- 不制造 N 个弹窗。

## WP-F — Shift Splice Candidate

实现：

- NodeDrag 中实时 Shift；
- single-node only；
- candidate wire hit；
- unique pass-through pair；
- final-state preflight；
- candidate highlight；
- ghost preview；
- Shift release immediate clear。

## WP-G — Atomic Splice Commit

实现：

- one replace transaction；
- remove 1 / add 2；
- no partial graph state；
- preserve unrelated incident wires；
- integrate current Undo/Redo seam。

## WP-H — Historical Regression Gate

至少重跑：

- Flow/Logic cardinality；
- new wire；
- occupied single endpoint reconnect；
- multi-capacity Ctrl reconnect；
- blank drop disconnect；
- Escape；
- valid-target glow；
- visible anchor center；
- node layout persistence；
- per-graph viewport；
- delete protection；
- Alt scissors；
- inline ComboBox/TextBox interaction；
- Dark/Light；
- node/connection selection。

不要把回归 gate 扩建为新的 UI 自动化平台。

---

# Automated Verification

自动测试只证明程序契约，不等于视觉验收。

## A7

### Selection

```text
Click A       → {A}
Ctrl+Click B  → {A,B}
Ctrl+Click A  → {B}
Blank Click   → {}
```

### Marquee

- marquee 命中 A/B → A/B selected；
- 未命中 C → C not selected；
- Ctrl marquee 保留已有 Selection；
- Esc 恢复 marquee 开始前 Selection。

### Multi-move

初始：

```text
A=(100,100)
B=(300,160)
```

delta：

```text
(+40,+25)
```

结果：

```text
A=(140,125)
B=(340,185)
```

相对位置不变。

### Topology untouched

multi-drag 前后 `Graph.Connections` 值必须相等。

### Delete protection

Selection 同时包含 normal + Required node：

```text
Delete
→ normal 删除
→ Required 保留
```

## A8

### Ordinary drag safety

```text
A→B
C 拖过 A→B
Shift=false
→ MouseUp
→ Connections 仍为 A→B
```

### Live Shift enable

NodeDrag 开始时 Shift=false，拖动中 Shift=true，合法 candidate 出现。

### Live Shift disable

candidate 存在时 Shift KeyUp：

- candidate cleared；
- preview cleared；
- Graph 不变。

### Successful splice

```text
Before: A.out→B.in

After:
A.out→C.in
C.out→B.in
```

旧 A→B 不存在。

### Atomic failure

让 replacement 中任意一条因 cardinality/validation 失败：

```text
最终仍为 A→B
```

不得产生半条新线。

### Ambiguous node

C 有多个合法同 kind pass-through pair：

```text
no valid candidate
no preview
no mutation
```

### Existing incident wire protection

C 的候选单容量端口已占用：

```text
no candidate
existing wire preserved
```

### Multi-selection + Shift

多节点一起拖 + Shift：

```text
no splice candidate
no topology change
```

### Modifier regression

- Alt scissors 正常；
- Ctrl+multi-capacity Port Drag 正常；
- Ctrl selection 不抢 Port；
- Shift 不影响 wire-drag 本身。

---

# Release EXE 动态验收

以下不能因为 unit/WPF tests 通过就宣布 PASS。

Codex 最终必须构建真实 Release Candidate EXE，并提供连续帧或视频证据。

## A7 动态证据

1. 空白左拖出现 marquee；
2. 多节点进入 selected visual；
3. Ctrl 可追加 / toggle；
4. 从任意 selected node 开始 drag，整个 selection 同步移动；
5. incident wires 跟随；
6. MouseUp 后位置保持；
7. 保存、关闭 Studio、重启后 layout 仍保持；
8. Required/fixed node 不会被批量删除。

## A8 动态证据

### Sequence 1 — 普通移动安全

```text
C 拖过 A→B
Shift 未按
→ wire 无 candidate glow
→ MouseUp
→ A→B 保持
```

### Sequence 2 — 动态进入

```text
拖 C
→ 按 Shift
→ A→B candidate highlight
→ ghost A→C→B
```

### Sequence 3 — 动态退出

```text
仍在 drag
→ 松 Shift
→ highlight 立即消失
→ ghost 立即消失
→ MouseUp
→ Graph 仍为 A→B
```

### Sequence 4 — 提交

```text
拖 C
→ Shift held
→ valid candidate
→ MouseUp while Shift still held
→ A→B 被原子替换为 A→C + C→B
```

### Sequence 5 — Invalid / Ambiguous

使用无法唯一决定 pass-through pair 的节点：

- 不出现误导性 valid glow；
- 不修改 graph。

### Sequence 6 — Modifier 回归

- Alt 剪刀工作；
- Ctrl+multi-port bundle reconnect 工作；
- Ctrl Selection 不抢端口；
- multi-node drag + Shift 不 splice。

---

# 明确不做

本追加 PLAN 不得顺手实现：

- 可配置快捷键系统；
- 快捷键设置页；
- 通用 Gesture Engine；
- 通用 Selection Service；
- 多节点自动布线；
- 自动排序；
- 吸附网格；
- group/frame/comment box；
- minimap；
- reroute point；
- topology optimizer；
- 多节点整组 splice；
- ambiguous port popup；
- Flow/Logic cardinality 改造；
- Runtime schema 改造；
- Minecraft Runtime 改造；
- canonical graph editor 重写。

若 A7/A8 暴露阻塞性 root cause：

1. 记录 root cause；
2. 做最小必要修复；
3. 证明没有扩大产品范围。

---

# Definition of Done

## A7

必须同时满足：

- marquee 可用；
- Ctrl additive/toggle 可用；
- multi-selection 视觉明确；
- multi-drag 保持相对位置；
- layout 正确持久；
- layout drag 永不修改 topology；
- batch delete 遵守 node protection；
- existing wire/port gestures 不回归。

## A8

必须同时满足：

- ordinary drag 绝不 splice；
- Shift 是唯一 splice modifier；
- Shift 可在 drag 中实时进入/退出；
- Shift release 立即撤 candidate/preview；
- only single-node drag；
- candidate pass-through pair 必须唯一；
- invalid/ambiguous 不猜；
- 无 Toast spam；
- preview 阶段 formal Graph 不变；
- commit 是 atomic remove-one/add-two；
- failure 无 partial mutation；
- unrelated incident wires preserved；
- Alt scissors 完整保留；
- Ctrl bundle reconnect 完整保留。

## 最终交付

Codex 提供：

- 当前 `0.3.2.0_A` 对齐结论；
- 修改文件清单；
- A7/A8 implementation summary；
- automated test results；
- historical regression results；
- Release EXE / package；
- 动态 UX 证据；
- 尚需用户亲自确认的条目。

Codex 最终最多可写：

```text
AGENT_VERIFIED
Release Candidate ready for user verification
```

不得自行写：

```text
USER_ACCEPTED
Studio 已冻结
0.3.2.0_A 已通过用户验收
```

只有用户本人可以授予这些状态。

---

# 最终产品契约

### A7

> **空白拖动可以框选；Ctrl 用于增量选择；拖动任意已选节点会整体移动当前 Selection，布局操作永不修改图拓扑。**

### A8

> **普通节点拖动永远只移动；仅在单节点拖动期间按住 Shift，才允许把节点原子插入一根唯一且合法的现有连线；Alt 始终保留给剪刀。**
