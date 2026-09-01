# DarkGrey_RPG 0.3.1.3 — Studio 第三轮修缮施工 PLAN

> **版本定位**：0.3.1.x Studio 修缮线的第三轮集中修复  
> **目标版本**：DarkGrey_RPG 0.3.1.3  
> **当前审计基线**：`codex/0.3.1.2B`，审计时 HEAD `c7ac508d317bf687c16f8d3a0c1e9968b2553fdf`  
> **建议施工分支**：`codex/0.3.1.3`  
> **第一优先输入**：用户原始文件 **`0.3.1.2审计.docx`**（19 条，含截图）  
> **第二输入**：本 PLAN 中补充的 6 条源码/结构风险  
>
> **重要**：0.3.1.3 仍然不是 Studio 冻结版。  
> 只有用户在实际使用后明确宣布“冻结 Studio”，0.3.1.x 才停止。  
> Codex 只能交付 **0.3.1.3 Release Candidate**，无权自行写“完成”“全部通过”“Studio 已冻结”。

---

# 0. 施工前硬 Gate：必须先读原始 `0.3.1.2审计.docx`

这是 0.3.1.3 最重要的流程约束。

## GATE 0A — 原文不可替代

Codex **在修改任何生产源码之前**，必须先打开并完整阅读：

```text
0.3.1.2审计.docx
```

必须阅读：

- 19 条原始文字；
- 每条对应截图；
- 截图中的鼠标位置、控件层级、边框、端口、菜单、主题、报错信息；
- 原文中的措辞和行为要求。

**禁止只读本 PLAN 的概括后开工。**

如果 Codex 的工作环境里没有 `0.3.1.2审计.docx`：

> 立即停止施工，状态写 `BLOCKED — ORIGINAL_AUDIT_MISSING`，要求用户把原始 DOCX 提供到工作区。

不得用：

- 0313 PLAN 摘要；
- Development Report；
- 旧截图；
- 自动化测试；
- 自己回忆；

代替原始 DOCX。

## GATE 0B — 开工前必须生成原文摄取记录

在改代码之前提交：

```text
PLAN/0.3.1.3/AUDIT_0312_INGEST.md
```

至少包含：

```text
原始文件名
文件 SHA-256
读取时间
19 条原始审计编号
每条：一句“原文要求核心”
每条：对应截图/页面
每条：初始状态
```

初始状态只能使用：

```text
REPRODUCED
SOURCE_CONFIRMED
NEEDS_LIVE_REPRO
```

不能一上来写：

```text
FIXED
PASS
NOT_APPLICABLE
```

如果某条无法从源码确认，也无法在 Release EXE 复现：

```text
NEEDS_LIVE_REPRO
```

不得猜根因。

## GATE 0C — 施工前再读 0.3.1.2B 当前源码

至少重新检查：

```text
studio/src/DarkGreyRPG.Studio/
  MainWindow.xaml
  ViewModels/ShellViewModel.cs
  ViewModels/ValidationIssuePresentation.cs

  Views/Graph/
    CanonicalStoryWorkspaceView.xaml
    CanonicalStoryWorkspaceView.xaml.cs
    CanonicalGraphEditorView.xaml
    CanonicalGraphEditorView.xaml.cs
    CanonicalGraphNodeControl.xaml
    CanonicalGraphNodeControl.xaml.cs
    CanonicalInlineNodeEditorControl.xaml
    CanonicalInlineNodeEditorControl.xaml.cs
    ScissorsCursorFactory.cs

  Views/FlowPortControl.cs

  ViewModels/Graph/
    CanonicalStoryWorkspaceViewModel.cs
    CanonicalNodeInspectorViewModel.cs
    GraphEditorHostViewModel.cs
    CanonicalGraphResourceEditorViewModel.cs

studio/src/DarkGreyRPG.Studio.Core/Graphs/
  Definitions/
    GraphNodeDefinitionRegistry.cs
    CanonicalTaskObjectiveSchema.cs
    StoryStartSchema.cs
  Editing/
    GraphEditSession.cs
  Resources/
    CanonicalStoryResourceLifecycleService.cs
    CanonicalStoryMembershipManifest.cs
```

如果实际 0.3.1.2B HEAD 已经晚于：

```text
c7ac508d317bf687c16f8d3a0c1e9968b2553fdf
```

必须在 `AUDIT_0312_INGEST.md` 记录真实基线 SHA，并重新检查差异。

---

# 1. 权威顺序

出现冲突时：

1. 用户 `0.3.1.2审计.docx` 原文和截图；
2. 本 0.3.1.3 PLAN；
3. 用户已经明确锁定的 DGR Studio / Graph 设计语义；
4. 0.3.1.2A / B 中仍然有效且没有被本轮反馈否定的行为；
5. 成熟行业节点编辑器的既有交互惯例；
6. 当前源码只代表“现在怎么实现”，不代表“产品应该怎么设计”。

特别是：

> **已经存在成熟行业交互的问题，优先采用成熟经验，不得为了当前代码方便自行发明一套奇怪操作。**

Graph Editor 重点参考成熟节点编辑器的连接、端口、拖拽、镜头与命中区习惯。

---

# 2. 0.3.1.3 范围

本版本处理：

- 用户新的 19 条 0.3.1.2 人工审计；
- 源码审计补充的 6 条潜在问题；
- 为真正解决这些问题必须做的最小同根修复。

仍然属于：

> **Studio 修缮。**

## 明确不做

- Minecraft 游戏内 GUI 整改；
- Minecraft Runtime 大改；
- CNPC 专项；
- 指名器/复制器/收纳箱游戏内重做；
- 新 Objective 大扩展；
- 新 Action 大扩展；
- 新脚本系统；
- 新变量系统；
- 第二套 Graph Editor；
- 通用 UI Framework 重写；
- Dev Harness / Scenario Platform；
- 因 0313 “顺手”大规模重构旧 Runtime。

若 0313 修改 canonical schema，为兼容当前 Runtime 所需的**最小 loader/parser 调整**允许做，但不能扩展为 0.3.2.0 Minecraft 工作。

---

# 3. 本版本状态模型

每条问题必须有独立状态：

```text
NOT_STARTED
ROOT_CAUSE_CONFIRMED
IMPLEMENTED
AGENT_VERIFIED
USER_ACCEPTED
BLOCKED
```

解释：

### ROOT_CAUSE_CONFIRMED

必须能回答：

```text
为什么当前行为会发生？
修改哪一层最小且正确？
```

如果不知道：

```text
NEEDS_LIVE_REPRO / BLOCKED
```

不能写猜测。

### IMPLEMENTED

只代表代码已改。

### AGENT_VERIFIED

必须使用本轮 **Release EXE** 完成真实操作。

### USER_ACCEPTED

只有用户本人明确接受。

**自动测试不能把 UI 项从 IMPLEMENTED 直接升级成 AGENT_VERIFIED。**

---

# 4. Work Package A — Story Start / Inline Parameter 系统收口

覆盖原审计：

```text
#1 #2 #3 #8
```

并覆盖补充：

```text
#20 Inline Editor 广播刷新性能
#21 LostFocus 潜在未提交
#24 Start 默认命名
```

这是 0313 优先级最高的包之一。

---

## A1. Start 下拉框必须是真控件，不得“选了等于没选”

当前 0312 虽然有 `SelectedTriggerType` 等 TwoWay Binding，但用户真实点击后出现：

> 选项能弹出，点了却没有实际变化。

0313 不接受：

```text
setter 被调用了
```

作为通过证据。

必须验证完整链路：

```text
UI 选择
→ ViewModel
→ Host/Core mutation
→ Graph document
→ Inline UI
→ Inspector
→ Undo/Redo
```

### Start Trigger Type

每个正式类型都必须实际切换：

```text
角色交互
进入区域
逻辑条件
```

切换后：

- 当前类型文本立即改变；
- 字段集合立即改变；
- Graph JSON / draft state 改变；
- 对应 Logic port 按规则增减；
- Undo 可恢复；
- Redo 可重做。

任何失败不得静默回滚。

如果修改失败：

> 在当前字段附近显示简短中文原因。

技术 code 只去 Problems。

### 同根 ComboBox 回归

同样实际验证：

- Objective Type；
- Story Action Type；
- Session speaker；
- 角色/物品 selector。

---

## A2. 全部字段必须有“用途名”

禁止出现：

```text
[进入区域 ▼]
[进入区域 ▼]
```

但用户不知道一个是“条件类型”、一个是什么。

每条启动条件至少采用：

```text
┌────────────────────────────┐
│ 启动条件 1          [删除] │
├────────────────────────────┤
│ 名称         [启动条件 1] │
│ 类型         [进入区域 ▼] │
│ 维度         [0]          │
│ 坐标   X[] Y[] Z[]        │
│ 半径         [3]          │
└────────────────────────────┘
```

角色交互：

```text
名称
类型
目标角色
```

逻辑条件：

```text
名称
类型
逻辑条件说明/对应 ◆
```

不能让用户靠控件位置猜字段。

---

## A3. 统一术语：全部叫【启动条件】

0313 中用户可见统一：

```text
启动条件
```

删除：

```text
启动方式
开始触发
新触发
```

涉及：

- Inline Node；
- Inspector；
- Tooltip；
- Add Button；
- Delete Confirmation；
- Problems 中文提示；
- Automation Name 可同步中文，但内部 code 不必改。

---

## A4. 每条启动条件必须明确显示用户名称

`DisplayName` 不能只出现在 ●输出端口。

节点参数区和 Inspector 都必须显示：

```text
启动条件 1
启动条件 2
...
```

如果用户重命名：

```text
夜晚进入农场
和老板对话
```

参数卡片标题立即变为：

```text
夜晚进入农场
和老板对话
```

并与对应输出端口 DisplayName 同步。

stable port ID 不改变。

---

## A5. 每条启动条件必须有明显视觉边界

多条件时必须一眼区分。

最低：

- 独立 Card/Border；
- 8~12px 组间距；
- 条件标题；
- 类型显示；
- 明确的删除按钮；
- 选中/hover 不与相邻卡片混淆。

不要只靠一条很淡的灰线。

Dark / Light 都必须清楚。

---

## A6. 默认条件名自动唯一

新增时：

```text
启动条件 1
启动条件 2
启动条件 3
...
```

寻找第一个未占用标准名。

例：

已有：

```text
启动条件 1
夜间触发
启动条件 3
```

新增：

```text
启动条件 2
```

不要再使用：

```text
新触发
```

连续同名。

---

## A7. Inspector 必须滚动

当前右侧 Inspector 不能滚动，条件多时下面字段不可达。

最终结构：

```text
Inspector Header（可固定）
↓
ScrollViewer
  └─ Inspector Content
```

要求：

- 鼠标滚轮正常；
- touchpad 正常；
- scrollbar 自动出现；
- 1100×700 条件较多时仍能访问最后一个字段；
- 不抢 Graph Canvas 的缩放滚轮。

当鼠标位于 Inspector：

> 滚轮滚 Inspector，不缩 Graph。

当鼠标位于 Graph：

> 滚轮继续按 Graph 既定规则缩放。

---

## A8. 解决 Inline Editor 全图广播刷新

### 当前结构风险

当前每个 Inline Node 都可能拥有完整 `CanonicalNodeInspectorViewModel`，且每个都订阅全局：

```text
Host.GraphChanged
```

一次参数变化容易导致：

```text
所有节点 Inline VM
→ RefreshFromHost()
→ 重建 actor/item options
→ 重建动态列表
```

0313 必须消除这种“全图所有 Inline Editor 响应一个节点变化”的结构。

### 最小正确方向

允许选择以下最小实现：

#### 方案 A：Targeted Node Change

Host 发布：

```text
NodeChanged(nodeId, changeKind)
```

Inline VM 只处理自己的 NodeId。

#### 方案 B：节点 VM 自身 PropertyChanged

Inline Editor 直接观察自己的 `GraphEditorNodeViewModel`，资源目录变化使用独立的轻量 catalog event。

### 共同要求

- 改 Objective A 不刷新无关 Objective B；
- 改 Line 不重建所有 Actor options；
- 不 `RebuildGraph()`；
- 不重建所有 node controls。

允许 Workspace 共享一次排序后的：

```text
Actor Options
Item Options
```

而不是每个节点重复排序整个资源集合。

不要创建通用 Reactive Framework。

---

## A9. 不得因 `LostFocus` 丢最后输入

以下场景必须通过：

```text
点击 TextBox
输入新值
不点击其它地方
直接 Ctrl+S
关闭
重开
```

最新值必须保存。

至少验证：

- Start dimension；
- X/Y/Z；
- radius；
- Objective 数量；
- Line 台词；
- Action amount/message。

实现可选：

- 安全字段改为 PropertyChanged；
- 或 Save/Graph switch 前显式 commit 当前 editor binding；
- 或最小 staged-edit commit。

但不得通过“用户一般会点别处”规避。

---

## GATE A

必须真实 Release EXE 完成：

1. 新建 4 个启动条件；
2. 每个改名称；
3. 三种类型互相切换；
4. 参数区清楚区分四块；
5. Inspector 滚到最下面；
6. 直接编辑未失焦字段后 Ctrl+S；
7. Undo/Redo；
8. 重启检查。

任何“点下拉但没变化”：

> FAIL。

---

# 5. Work Package B — Graph Editor 连接、端口、拖放、镜头

覆盖：

```text
#4 #5 #6 #7 #9 #16
```

这是另一个最高优先级包。

---

## B1. 输入/输出端口锚点恢复稳定左右对齐

最终 invariant：

```text
所有输入 anchor：节点左侧固定 X
所有输出 anchor：节点右侧固定 X
```

文字长度不得改变 anchor X。

不能因为：

- 动态端口名；
- 参数区宽度；
- 条件名；
- 结果名；

发生漂移。

### 验收不看 XAML 意图

不能用：

```text
HorizontalAlignment="Right"
```

证明通过。

必须在真实运行时测量：

```text
Node bounds
Input anchor screen X
Output anchor screen X
```

同一节点所有 output anchor 的 X 差异：

```text
<= 1 device-independent pixel
```

不同节点在相同宽度下也必须贴各自右边。

---

## B2. 剪刀 Cursor 改成极简黑白/单色工具风格

当前彩色青蓝剪刀废弃。

要求：

- 参考 Windows/专业编辑器鼠标工具视觉；
- 黑/白或高对比单色；
- 小尺寸；
- 一眼能看出剪切；
- 不做彩色插画；
- Hotspot 位于实际切割位置；
- Dark / Light 都可辨识。

允许使用嵌入式 `.cur`。

不允许再写复杂彩色像素绘图代码只为了“有一个剪刀”。

### Gate

必须用实际 cursor 证据。

如果截图系统抓不到鼠标：

- 使用录屏/GIF；
- 或带真实 cursor overlay 的捕获工具。

无法看到 cursor：

> `NEED_USER_VERIFICATION`，不能 PASS。

---

## B3. Resource → Graph Ghost 与最终 Node 必须保持同一相对位置

不允许只证明“top-left 公式看起来一致”。

最终 invariant：

> **鼠标在 ghost 内的相对坐标，与 drop 后鼠标在真实节点内的相对坐标相同。**

即：

```text
ghostPointerOffset = pointer - ghostTopLeft
finalPointerOffset = dropPointer - nodeTopLeft

|ghostPointerOffset - finalPointerOffset| <= 2 DIP
```

至少：

- 50% zoom；
- 100%；
- 150%；
- pan 后；
- 无 pan；
- Session；
- Task。

### Ghost 视觉

Ghost 应尽量复用 aggregate node 的真实尺寸/结构语言。

不能一个 76px 高简化壳，最终节点高度完全不同，却仍宣称“位置一致”。

### Gate 证据

必须记录：

```text
cursor screen coordinate
ghost bounds
final node bounds
```

不能用“截图里看起来差不多”替代。

---

## B4. 端口视觉小，命中区适当扩大

用户明确要求：

> 文字不应成为连线范围，但也没必要真的只能点 9px 圆点。

最终：

```text
视觉 ● / ◆：约 9~11px
透明 hit target：建议 18~20px
```

规则：

- hitbox 以 anchor 中心为中心；
- 不覆盖端口 label；
- 不侵入相邻端口；
- 点击 label 不开始 wire；
- 点击 anchor 周围合理空白可以开始 wire。

禁止把整个 `FlowPortControl` row 重新变成 wire hit target。

---

# 6. Work Package C — 连线交互按成熟行业模型重写

这是 #9 的正式锁定规则。

**不得自行解释。**

---

## C1. Cardinality 不变

```text
● Flow：
  Output max 1
  Input 0..N

◆ Logic：
  Output 0..N
  Input max 1
```

---

## C2. 多容量端口：普通拖动 = 新增连接

多容量端口：

```text
● Flow Input
◆ Logic Output
```

无论当前已有：

```text
0
1
2
N
```

条连接：

> **普通拖动端口 = 新增一条 connection。**

已有连接全部原样留在屏幕上。

### 例：Logic Output

已有：

```text
        ┌──◆ A
X ◆─────┼──◆ B
        └──◆ C
```

普通拖 X：

```text
        ┌──◆ A
X ◆─────┼──◆ B
        ├──◆ C
        └── 鼠标（新连接）
```

---

## C3. 多容量端口：Ctrl + 拖动 = 移动全部已有连接

只有显式：

```text
Ctrl + Drag
```

才移动多容量端口的所有 incident wires。

### Flow Input

```text
A ●──┐
B ●──┼→ X ●Input
C ●──┘
```

Ctrl 拖 X：

- A/B/C 另一端固定；
- 三条 existing connection 的 X endpoint 同时跟鼠标；
- drop 到另一个 Flow Input → 全部重接；
- Esc → 全部恢复；
- 空白 drop → 全部断开。

### Logic Output

镜像行为。

### 重要

普通拖绝不能因为：

```text
incident.Count > 1
```

自动进入 bundle move。

必须实际检查 Ctrl modifier。

---

## C4. 单容量已占用端口：拖“被抓住的 endpoint”

单容量端口：

```text
● Flow Output
◆ Logic Input
```

已有连接时，用户从该端口拖动：

> **抓起原 connection 的这个 endpoint 本身。**

另一 endpoint 固定。

### Flow Output 例

原：

```text
A ●────────────● B
^
鼠标按这里
```

拖动后：

```text
      A 原位置

鼠标 ●
      ╲
       ╲────────● B
```

必须做到：

- B 端保持原位置；
- A 端跟随鼠标；
- 原来的 connection visual 仍存在并变形；
- 第一帧线长不会突然归零；
- 不删除原线；
- 不创建一根替代白线/preview 线。

### Logic Input

同理，拖 input endpoint：

- input endpoint 跟鼠标；
- output 另一端固定。

---

## C5. Reconnect 事务

拖已有 connection：

### Valid same-direction endpoint

提交 reconnect。

### Esc

原连接恢复原 geometry。

### Invalid target

不改变正式数据。

### Blank drop

明确断开当前被拖连接/连接组。

---

## C6. 新建 wire 的视觉

尚未提交的新连接可以有 transient geometry，但必须使用正式 wire renderer 的：

- Flow 颜色；
- Logic 颜色；
- 正式粗细；
- 正式曲线。

禁止：

- 白色统一 preview；
- 虚线 preview；
- 第二套线条视觉语言。

---

## C7. 单根 existing wire 不得被“删除再新建”模拟

Gate 必须证明：

- 开始拖动前 connection visual instance = X；
- 拖动中仍是 X；
- Cancel 后仍是 X；
- 只有 commit 后数据 endpoint 改变。

视觉/数据两层都要验证。

---

## GATE C — 必做矩阵

### Flow Output（单）
- 空 → 新建；
- 已占用 → 拖 grabbed output endpoint；
- Esc；
- blank；
- valid reconnect。

### Logic Input（单）
同上。

### Flow Input（多）
- 0 条普通 drag → 新增；
- 1 条普通 drag → 新增第二条；
- 3 条普通 drag → 新增第四条；
- 3 条 Ctrl drag → 三条整体移动。

### Logic Output（多）
同上。

任何“普通拖多端口把所有线抓起来”：

> FAIL。

任何“拖单容量端口第一帧原线消失/归零”：

> FAIL。

---

# 7. Work Package D — Task Objective authoring 与性能

覆盖：

```text
#15
```

并与 A8 性能根因共同验收。

---

## D1. Objective 用户名称统一

作者 UI 改为：

```text
实体击杀
物品收集
角色交互
```

不再显示：

```text
击杀实体
收集物品
交互角色
```

内部 persisted type 保持：

```text
kill_entity
collect_item
interact_actor
```

不因为中文改名做数据迁移。

---

## D2. 三种 Objective 不再共享错误模板

### 实体击杀

字段：

```text
目标类型：实体击杀
目标对象：角色 / 角色组
数量：N
```

### 物品收集

字段：

```text
目标类型：物品收集
目标对象：物品 / 物品组
数量：N
```

### 角色交互

字段：

```text
目标类型：角色交互
目标角色：角色 / 角色组（按现有正式身份契约）
```

**不显示数量。**

角色交互完成语义：

> 一次满足该目标的正式交互事件完成该 Objective。

如果用户需要多个独立角色/阶段：

> 建多个【目标】节点，用显式逻辑组合。

不再用：

```text
角色交互 ×10
```

表达。

---

## D3. Objective Schema 修正

当前 `required` 是 CommonProperties。

0313 改为：

```text
Common:
  objective_type
  description

kill_entity:
  + entity
  + required

collect_item:
  + item
  + metadata
  + required

interact_actor:
  + actor_id
  不含 required
```

### 旧数据兼容

旧 `interact_actor.required`：

#### required == 1

允许安全兼容并在下一次正式保存时规范化去除。

#### required > 1

不得静默改成 1。

在 Problems 显示中文迁移提示：

```text
旧版“角色交互”目标包含数量 N。
0.3.1.3 中角色交互为单次目标，请确认后改为单次目标或拆分为多个目标节点。
```

不要自动生成 N 个节点。

如当前 Java loader 需要 `required`，允许最小兼容修改：

```text
interact_actor 缺省 required => 1
```

但不开始 Minecraft 功能施工。

---

## D4. Inline Objective 必须真正可编辑

节点本体中：

- 类型 ComboBox；
- 目标 ComboBox；
- 数量（仅 kill/collect）；

都必须立即生效。

不允许：

```text
看起来是 ComboBox
实际改变不了
```

Inspector 与 Inline 必须反映同一状态。

---

## D5. Target switch 不得刷新全图

从：

```text
测试对象
→ 测试对象组
```

只更新：

- 当前 Objective；
- 当前对应 Inspector；
- 必要验证状态。

不得：

- 所有节点 RefreshFromHost；
- 所有 Inline VM rebuild options；
- RebuildGraph；
- 全部 connections 重画。

---

## D6. 性能 Gate

建立实际可操作 fixture：

```text
100 个角色/物品资源
100 个 Graph 节点
其中 >= 30 Objective
```

Release EXE：

连续切换同一 Objective 的目标 20 次。

要求：

- 无肉眼可见几秒冻结；
- 单次操作在 Agent acceptance machine 上不得出现 >500ms 的 UI stall；
- 报告 median / max；
- 同时用自动化断言：无关 node inline editor 不执行 refresh。

性能测量可使用最小 Stopwatch/事件计数测试。

**不要为此新建 Performance Harness。**

---

# 8. Work Package E — 资源 Inspector、生命周期、排序、重命名完整性

覆盖：

```text
#10 #14 #17
```

并覆盖补充：

```text
#22 rename round-trip 完整性
```

---

## E1. 角色 / 物品 Inspector 信息完整

### Individual Actor

类型：

```text
角色
```

字段至少：

```text
显示名称
NPC_ID
标签
拥有/引用状态（若现有 UI 已有，可保留）
```

### Collective Actor

类型：

```text
角色组
```

字段：

```text
显示名称
Group_ID
标签
```

### Individual Item

类型：

```text
物品
```

字段：

```text
显示名称
Item_ID
标签
```

### Collective Item

类型：

```text
物品组
```

字段：

```text
显示名称
Group_ID
标签
```

删除：

```text
个体物品
集体物品
```

这样的产品用语。

内部类名不必改。

---

## E2. 标签必须可见

既然创建资源允许 tags，普通 Inspector 至少显示 tags。

可以：

- chips；
- 文本列表；
- 只读/可编辑沿用现有资源能力。

不允许完全隐藏。

本版本不额外新增复杂 Tag Manager。

---

## E3. 删除/解除 Session 或 Task resource 必须清理 Story aggregate placement

当当前 Story 中：

- 删除 owned Session；
- 删除 owned Task；
- 解除 referenced Session；
- 解除 referenced Task；

该资源从当前 Story membership 消失时：

> 当前 Story Flow 中所有 `resource_id` 指向它的 aggregate node 必须同时删除。

同时删除：

- aggregate node；
- incident connections。

### 多 placement

同一个 Session/Task 如果在 Story Flow 放置多次：

> 全部 placement 删除。

---

## E4. 删除 aggregate node 绝不能删除 resource

反方向规则：

```text
删除 Story Flow 中【会话】节点
≠ 删除 Session 资源

删除 Story Flow 中【任务】节点
≠ 删除 Task 资源
```

只删除：

- placement；
- incident wires。

资源库项目和资源文件保留。

这是硬不对称规则。

---

## E5. Resource lifecycle 必须原子/可回滚

资源删除 + membership + Story Graph aggregate cleanup 涉及多状态。

不能：

```text
resource 已删
Story graph 清理失败
→ 留半残状态
```

必须：

- 先建立 mutation plan；
- 验证所有目标 placement；
- 一次事务/补偿式事务提交；
- 任一步失败 → 恢复原状态；
- 明确错误写 Problems。

不要因为这个要求发明通用数据库事务框架。

复用现有 AtomicFileWriter / lifecycle compensation 思路。

---

## E6. 资源排序拖动必须显示落点

当前 B8 只有 Drop，没有 drag insertion preview。

0313 使用成熟列表排序模式：

```text
────────────  ← insertion indicator
目标资源
```

建议：

- pointer 在 row 上半部 → before；
- pointer 在 row 下半部 → after；
- 2px accent insertion line；
- drag leave → indicator 清除；
- invalid cross-folder → 禁止符/无 indicator。

不要只高亮一个 row，因为无法表达 before/after。

---

## E7. Rename 必须保存所有非 DisplayName 数据

四类资源：

- 角色；
- 角色组；
- 物品；
- 物品组；
- Session；
- Task；

重命名 DisplayName 后：

```text
除 DisplayName 外，
序列化语义字段必须完全一致。
```

特别防止：

```text
new Resource { id, name, tags }
```

遗漏未来/现有其它字段。

自动化加入 round-trip deep comparison。

---

# 9. Work Package F — Palette / Authoring Model 清理

覆盖：

```text
#11 #12
```

并覆盖补充：

```text
#25 普通角色交互/进入区域节点合法性审计
```

---

## F1. Story Flow 添加节点菜单移除【聚合】

普通 blank-canvas palette 不再显示：

```text
聚合
  会话
  任务
```

Session / Task aggregate 的作者入口：

> **从左侧资源库把真实 Session / Task 拖入 Story Flow。**

Graph definition 仍保留用于：

- rendering；
- loading；
- validation；
- existing placement。

只是不允许：

```text
blank canvas → 添加节点 → 空会话/任务
```

---

## F2. 不要硬编码一个菜单补丁

建议为 GraphNodeDefinition 增加最小明确属性，例如：

```text
CanvasPlaceable
```

或等价 metadata。

含义：

```text
是否允许从 blank-canvas palette 直接创建
```

这样可统一表达：

- required/unique fixed node → false；
- compatibility-only → false；
- aggregate Session/Task → false；
- ordinary action/logic node → true。

不要新建 Palette Framework。

---

## F3. 【条件判断】顺序调整

Story / Session Logic category 中：

推荐明确顺序：

```text
逻辑输入
与
或
非
逻辑输出
条件判断
```

至少满足用户要求：

> 【条件判断】在【逻辑输出】下面。

不要改 internal node type：

```text
condition
```

---

## F4. 审计普通 Story Flow【角色交互】【进入区域】节点

当前 Registry 中存在普通：

```text
角色交互
进入区域
```

并归类：

```text
触发
```

但当前已确认的正式 0310 authoring 模型中，这两个概念明确用于：

```text
Story【开始】的启动条件
```

并没有明确授权它们作为普通 Story Flow mid-flow 节点。

### 施工前必须查正式 PLAN

Codex 必须查：

- 0.3.0.0 Design Plan；
- 0.3.1.0 Construction Plan；
- 0312 之前最终 Canonical 说明。

如果找不到明确授权：

> 0313 将普通 `interact_actor` / `enter_region` 标成 compatibility-only，移出新 authoring palette。

旧项目：

- 继续读取；
- 不静默删除；
- Problems 可提示旧节点。

如果 Codex 找到明确正式设计依据要求它们存在：

> 在报告中引用具体文件和章节，先标 `DESIGN_CONFLICT`，不得自行决定保留。

---

# 10. Work Package G — Light Theme、项目首页、普通作者诊断

覆盖：

```text
#13 #18 #19
```

并覆盖补充：

```text
#23 技术术语泄漏
```

---

## G1. Light Theme 做完整对比度 pass

不是修一个 Border。

当前 Graph/Inline 仍有很多硬编码深色：

```text
#25292F
#F7FAFC
#59616D
...
```

0313：

- 优先改为现有 DynamicResource；
- 若 Graph 确实需要专属颜色，增加有限的 Graph theme tokens；
- Dark / Light 各有明确值。

不要建立 Theme Engine。

### 检查范围

- Main shell；
- Project Home；
- resource library；
- graph background；
- node body/header；
- selected node；
- ports；
- wires；
- inline parameter cards；
- Inspector；
- ComboBox popup；
- context menu；
- Problems；
- resource selected/hover；
- splitter/border。

### 目标

浅色模式不能：

```text
一片白
边界消失
文字/框线看不清
```

---

## G2. Project Home 改为与 Story Workspace 相同的三栏心智模型

项目首页最终：

```text
┌──────────────┬───────────────────────┬──────────────┐
│ 故事列表      │ 故事图谱               │ Inspector    │
│ 搜索 / 新建   │ Project Story Graph   │ 当前故事详情  │
│ Story A      │                       │              │
│ Story B      │                       │              │
└──────────────┴───────────────────────┴──────────────┘
```

### 左侧

- 故事列表；
- 搜索；
- 新建故事；
- compact selection。

不再需要：

```text
故事列表 / 故事图谱
```

两个互斥页面按钮。

### 中央

项目 Story Graph 常驻。

### 右侧 Inspector

显示当前 selected Story 的已有详细信息。

**只搬运现有 ProjectHome 已有数据，不在0313发明大量新 Story metadata。**

### 交互

- 单击 Story → Inspector 更新；
- Story Graph selection 可同步 Story selection；
- 双击 Story / 图谱 Story node → 进入该 Story；
- breadcrumb 项目名 → 返回这里。

---

## G3. 普通 Inspector 只显示简短中文错误

当前做法：

```text
中文
[graph.xxx]
技术详情：英文...
```

常驻 Inspector 不再这样。

改为：

```text
任务目标缺少目标对象，请选择一个角色或角色组。
```

或：

```text
此逻辑输入已经有一个来源。
```

最多再加：

```text
请在“问题”面板查看详情。
```

不显示：

- stable code；
- technical raw message；
- internal field path。

---

## G4. 技术详情统一进入【问题】

底部 Problems 保留开发/诊断信息。

至少可查看：

```text
中文问题
严重性
资源
字段
稳定错误代码
技术详情
```

可以：

- row 展开；
- details pane；
- 双击查看；

按现有最小结构实现。

普通用户只有主动打开 Problems 才看到这些技术信息。

---

## G5. 去除普通 UI 的 Canonical / NodeType 等术语

作者可见状态栏、Toast、Inspector Kind：

删除/替换：

```text
Canonical Story
Canonical 角色
node type = objective
node type = start
```

改为：

```text
故事
角色
目标
开始
...
```

`InspectorKindText` 对 node 必须映射 user-facing display name，而不是返回内部 `NodeType`。

内部：

- code；
- class；
- JSON；
- AutomationId；

可以继续英文/技术名。

---

# 11. Work Package H — 每张 Graph 独立 Viewport State

覆盖：

```text
#16
```

---

## H1. 每个 Graph 独立保存 transient camera

最少保存：

```text
Pan X
Pan Y
Zoom
```

作用范围：

- Story Flow；
- 每一个 Session；
- 每一个 Task。

例：

```text
Story = (10,10), 100%
Task A = (20,20), 125%
Session A = (50,50), 80%
```

切换回来必须恢复各自状态。

---

## H2. Key 必须绑定 graph resource，不绑定一个共用 view

推荐 key：

```text
GraphResourceKind + ResourceId
```

或直接由对应：

```text
CanonicalGraphResourceEditorViewModel
```

持有 transient `ViewportState`。

不要使用：

```text
CanonicalGraphEditorView 全局唯一 viewport
```

作为所有图共享摄像机。

---

## H3. 不扩成新的持久化 schema

用户当前要求是：

> 层级切换回来保留离开时的镜头。

0313 先做：

> **本次 Studio 会话内保留。**

不要求关闭 Studio 后还记住 camera。

避免引入新的 layout persistence schema。

---

## H4. Toolbar 作用域

当前图执行：

- 100%；
- Fit；
- Reset；

只改变当前 Graph 的 viewport state。

切走后不影响其它 Graph。

---

# 12. 补充问题 20~25 正式定义

为防遗漏，6 条补充问题单独编号：

## 20 — Inline VM 广播刷新

见 A8 / D5 / D6。

## 21 — LostFocus 未提交风险

见 A9。

## 22 — Rename round-trip 完整性

见 E7。

## 23 — 普通 UI 技术术语泄漏

见 G5。

## 24 — Start 默认条件名重复/不清晰

见 A6。

## 25 — 普通 Story Flow 触发节点是否属于正式 authoring

见 F4。

---

# 13. 19 条原始审计 Traceability

最终不能只按 Work Package 验收。

**必须重新打开原始 `0.3.1.2审计.docx`，逐条对照。**

| 原审计 | 0313 处理位置 |
|---:|---|
| 1 下拉选择看似可用但实际上不改变 | A1 / D4 |
| 2 字段用途名缺失、多启动方式混乱 | A2 / A5 |
| 3 启动方式/开始触发术语混乱、条件名不显示 | A3 / A4 / A6 |
| 4 输出端口再次没有右对齐 | B1 |
| 5 剪刀 cursor 难看 | B2 |
| 6 resource ghost 与 drop 最终位置不一致 | B3 |
| 7 端口命中范围过小 | B4 |
| 8 Inspector 无滚动 | A7 |
| 9 多线/单线连线手势错误 | C1-C7 |
| 10 角色/物品 Inspector 过简、类型命名不一致 | E1 / E2 |
| 11 palette 的“聚合”无意义 | F1 / F2 |
| 12 条件判断 palette 顺序奇怪 | F3 |
| 13 Light Theme 边界不可读 | G1 |
| 14 删除资源不清理 aggregate node | E3-E5 |
| 15 Objective 假编辑/卡顿/模板语义错误/命名 | A8 / D1-D6 |
| 16 不同 Graph 共用 camera | H1-H4 |
| 17 resource reorder 无落点预览 | E6 |
| 18 Project Home 改成三栏 | G2 |
| 19 常驻错误显示技术 code 太多 | G3-G4 |

---

# 14. 每条原审计都必须有独立验收记录

最终生成：

```text
PLAN/0.3.1.3/AUDIT_0312_FINAL_REVIEW.md
```

格式必须是 19 行独立记录，不能只写：

```text
WP A PASS
WP B PASS
```

每条至少：

```text
Audit #:
原文核心要求:
Root Cause:
Implementation:
Release EXE Steps:
Expected:
Actual:
Evidence:
Automated Support:
Status:
```

状态：

```text
AGENT_VERIFIED
BLOCKED
```

不能在用户确认前写：

```text
USER_ACCEPTED
```

---

# 15. 6 条补充问题也必须独立验收

生成：

```text
PLAN/0.3.1.3/SOURCE_AUDIT_FINAL_REVIEW.md
```

逐条 20~25。

不得因为“用户没有截图”就跳过。

---

# 16. 真实 UI Gate 规则

## 16.1 必须用 Release EXE

不得使用：

- WPF test host；
- ViewModel unit test；
- XAML 静态查看；
- Debug mock window；

替代真实 Studio。

## 16.2 截图不能证明动态行为时，不得硬凑

以下必须视频/GIF/连续帧或可验证坐标数据：

- wire endpoint drag；
- Ctrl multi-wire；
- scissors cursor；
- ghost pointer offset；
- resource insertion indicator；
- viewport restore；
- ComboBox selection；
- performance stall。

如果环境无法可信记录：

```text
BLOCKED / NEED_USER_VERIFICATION
```

不能 PASS。

## 16.3 不能用“代码存在”作为视觉 PASS

禁止：

```text
HorizontalAlignment=Right → 输出端口 PASS
ScrollViewer exists → 滚动 PASS
Ctrl check exists → 多线交互 PASS
Cursor factory exists → 剪刀 PASS
```

必须实际操作。

---

# 17. Automated Test 最低矩阵

自动化只提供 SUPPORT，不替代 UI Gate。

---

## 17.1 Start

- add 自动名 `启动条件 N`；
- rename stable port ID；
- type switch 三种；
- duplicate standard name allocator；
- condition add/remove port；
- no silent mutation failure；
- one trigger min；
- multi trigger remove。

## 17.2 Inline refresh

建立 >=30 inline node VM：

修改一个 node。

断言：

- 只有目标 VM refresh；
- 无关 VM 不 rebuild resource options；
- no `RebuildGraph()`。

## 17.3 Pending edit

针对：

- numeric；
- text；

模拟输入后直接 Save commit。

## 17.4 Port alignment

STA 测试 actual layout 后测 anchor X。

## 17.5 Port hitbox

- anchor center；
- visual 边缘；
- 扩大透明 hitbox；
- label；

四类点击。

## 17.6 Wire

必须覆盖 C7 全矩阵。

尤其：

```text
multi ordinary drag != bundle
multi Ctrl drag == bundle
single occupied drag moves grabbed endpoint
```

## 17.7 Objective

Schema：

- kill required；
- collect required；
- interact no required；
- legacy required=1；
- legacy required>1 warning。

## 17.8 Resource deletion

- delete Session → all placements removed；
- remove Session reference → placements removed；
- delete Task → all placements removed；
- delete aggregate → resource remains；
- lifecycle failure → rollback。

## 17.9 Rename

四类资源 deep equality except DisplayName。

## 17.10 Viewport

Story / Task / Session：

```text
A state
B state
C state
```

来回切换保持。

## 17.11 Project Home

- selection→Inspector；
- graph selection→Story；
- double click→Story workspace；
- project breadcrumb→Home。

## 17.12 Validation presentation

Inline:

```text
无 code
无 technical English
```

Problems：

```text
code/detail 保留
```

---

# 18. 人工验收场景

---

## Case 1 — Start 4 条启动条件

创建：

```text
启动条件 1 = 进入区域
启动条件 2 = 角色交互
启动条件 3 = 逻辑条件
启动条件 4 = 进入区域
```

检查：

- 名称；
- 类型；
- 字段 label；
- card 分隔；
- scroll；
- rename；
- type switch；
- output port sync。

---

## Case 2 — 下拉不是假按钮

连续修改：

```text
Start 类型
Objective 类型
Objective target
Action 类型
Speaker
```

每次都观察：

- selected value；
- dependent fields；
- graph/document；
- Undo/Redo。

---

## Case 3 — 输出锚点

创建不同长短端口名：

```text
是
普通完成
一个非常非常长的结果名称
```

runtime measurement + screenshot。

---

## Case 4 — Port hitbox

对每类 ●/◆：

- 点中心；
- 点视觉外 3~4px；
- 点 label；
- 点相邻空白。

---

## Case 5 — Wire industry interaction matrix

完整执行 C7。

必须录制。

---

## Case 6 — Ghost drop

Session/Task：

- 50/100/150%；
- pan/no-pan；

保存 cursor coordinate 和 node bounds。

---

## Case 7 — Objective performance

100 resources / 100 nodes fixture。

20 次 target switch。

记录 median/max。

---

## Case 8 — Objective semantics

创建：

```text
实体击杀：史莱姆组 ×10
物品收集：铜币组 ×20
角色交互：酒馆老板
```

角色交互 UI 没有 quantity。

---

## Case 9 — Resource Inspector

四类资源：

```text
角色
角色组
物品
物品组
```

检查 ID + Tags。

---

## Case 10 — Resource delete vs placement delete

### Resource → Graph

删除 Session resource：

- aggregate 全消失；
- wires 全消失。

### Graph → Resource

只删 aggregate：

- Session resource 仍在。

Task 同样。

---

## Case 11 — Resource reorder preview

5 个资源。

拖：

- before；
- after；
- top；
- bottom；
- cancel；
- cross-folder invalid。

必须看到 insertion marker。

---

## Case 12 — Graph Viewport isolation

设置：

```text
Story = 10,10 @100%
Task = 20,20 @125%
Session = 50,50 @80%
```

来回切换三轮。

每个恢复自身状态。

---

## Case 13 — Light Theme

至少：

```text
1100×700 @100%
1100×700 @125%
1700×980 @100%
1700×980 @150%
```

检查：

- Project Home；
- Story；
- Session；
- Task；
- Inline；
- Inspector；
- dropdown；
- Problems。

---

## Case 14 — Project Home 三栏

- 左 Story list；
- 中 Story Graph；
- 右 Story Inspector；
- selection sync；
- double click；
- breadcrumb return。

---

## Case 15 — Validation

制造：

- Logic input multiple sources；
- Objective target missing；
- Start invalid；
- dynamic port duplicate。

Inspector：

> 只有中文可操作说明。

Problems：

> 能看到完整 code/field/technical details。

---

## Case 16 — Ctrl+S without focus loss

每类 TextBox：

输入后立刻 Ctrl+S，不点别处。

重启后最新值仍在。

---

# 19. Light/Dark 与 DPI Gate

0313 不能再次只说：

```text
Dark 常用，所以 Light 大概可以。
```

Release evidence 至少：

| Theme | Resolution | DPI |
|---|---|---|
| Dark | 1100×700 | 100% |
| Dark | 1700×980 | 125% |
| Light | 1100×700 | 100% |
| Light | 1100×700 | 125% |
| Light | 1700×980 | 150% |

如果 acceptance machine 无法精确得到 125/150：

- 记录实际 `GetDpiForWindow`；
- 使用最接近可配置 DPI；
- 不伪写。

---

# 20. Performance Gate

0313 特别禁止“功能正确但每次编辑卡几秒”。

要求：

- 参数改变不 full graph rebuild；
- target change 不 refresh all inline editor；
- resource selector list 不在每个 node 每次 mutation 重排；
- dynamic port 只刷新目标 node；
- Graph pan/zoom 不受参数 refresh 影响。

### NO-GO

任何普通单次：

```text
切 Objective target
改下拉
改数量
```

在 acceptance fixture 中明显冻结 >1 秒：

> NO-GO。

报告中必须写具体性能结果，不得写：

```text
感觉流畅
```

---

# 21. 防止 Codex 再次“假完成”

以下句式禁止出现在 Development Report 作为完成依据：

```text
“代码已经存在，因此通过”
“测试覆盖了该 setter，因此下拉通过”
“XAML 有 Right，因此对齐通过”
“截图没有看到问题，因此通过”
“自动化通过，因此用户体验通过”
“无法捕获鼠标，但逻辑正确，因此通过”
```

允许：

```text
IMPLEMENTED
AGENT_VERIFIED
BLOCKED
NEED_USER_VERIFICATION
```

---

# 22. 0.3.1.3 Exit Gate

Codex只有同时满足以下条件，才能提交 RC：

- [ ] 施工前已读取原始 `0.3.1.2审计.docx`
- [ ] 已提交 `AUDIT_0312_INGEST.md`
- [ ] 原19条全部有独立 root cause / phenomenon record
- [ ] 补充6条全部有独立处理
- [ ] 19条全部至少 IMPLEMENTED
- [ ] 6条全部至少 IMPLEMENTED 或有用户确认的设计冲突
- [ ] 所有可实机验证项已 AGENT_VERIFIED
- [ ] 所有动态交互有可信证据
- [ ] Core tests PASS
- [ ] WPF tests PASS
- [ ] Release build PASS
- [ ] Objective 性能 Gate PASS
- [ ] Light Theme Gate PASS
- [ ] resource delete transaction Gate PASS
- [ ] viewport isolation Gate PASS
- [ ] wire C7 完整矩阵 PASS
- [ ] `AUDIT_0312_FINAL_REVIEW.md` 已逐条重新对原 DOCX
- [ ] `SOURCE_AUDIT_FINAL_REVIEW.md` 已完成
- [ ] 所有 FAIL / BLOCKED 明确列出
- [ ] GitHub 已 push

只要原始 19 条中任意一条仍为：

```text
FAIL
BLOCKED
NEEDS_LIVE_REPRO
```

最终状态必须：

```text
0.3.1.3 RC — NO-GO / NEED_USER_VERIFICATION
```

不能写“完成”。

---

# 23. 最终再次逐条阅读原始 DOCX 的硬 Gate

Codex完成代码后，**必须再次打开 `0.3.1.2审计.docx` 原文**。

不是看 `AUDIT_0312_INGEST.md`。

逐条：

```text
1 → 原文 + 截图 → Release EXE
2 → 原文 + 截图 → Release EXE
...
19 → 原文 + 截图 → Release EXE
```

每条必须回答：

```text
原用户到底在抱怨什么？
现在的 Release EXE 是否直接解决这个抱怨？
证据是否真的能证明？
```

如果只是“代码层面相关”：

> 不算过关。

---

# 24. 最终交付文件

至少：

```text
PLAN/0.3.1.3/
  AUDIT_0312_INGEST.md
  ROOT_CAUSE_RECORD.md
  DEVELOPMENT_REPORT.md
  MANUAL_ACCEPTANCE.md
  AUDIT_0312_FINAL_REVIEW.md
  SOURCE_AUDIT_FINAL_REVIEW.md
  EVIDENCE_INDEX.md
  evidence/
```

Development Report 必须明确：

```text
Baseline SHA
Final SHA
Release EXE
Core tests
WPF tests
19条状态
6条状态
BLOCKED
NEED_USER_VERIFICATION
```

---

# 25. GitHub 最终要求

完成施工后：

1. 所有源码、测试、PLAN、Evidence 提交 Git；
2. push：

```text
codex/0.3.1.3
```

3. 提供最终 commit SHA；
4. 不自动 merge `main`；
5. 不自动 tag；
6. 不自动创建 GitHub Release；
7. GitHub 中必须能直接读取：
   - Development Report；
   - 19条 Final Review；
   - 6条 Final Review；
   - Evidence index。

Codex最终回复只能说：

```text
0.3.1.3 Release Candidate 已推送，等待用户验收。
```

不得说：

```text
0.3.1.3 已完成
Studio 已完成
Studio 已冻结
```

---

# 26. 一句话 DoD

> **0.3.1.3 的任务不是再给 Studio 增加一层“看起来能用”的控件，而是以用户 `0.3.1.2审计.docx` 原文为逐条验收基准，把 Start 参数、连线端点、多线手势、Objective 语义与性能、资源生命周期、独立镜头、排序预览、Light Theme、Project Home 和错误呈现真正做成可操作的 Studio 行为；原19条必须逐条重新打开原文复验，任何一条没有真实 Release 证据都不得宣称完成。**
