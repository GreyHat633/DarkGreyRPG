# DarkGrey_RPG 0.3.1.4 — Studio 恶性缺陷修复与历史回归封锁施工 PLAN

> **版本定位**：`0.3.1.x` Studio 修缮线的第四轮集中稳定化版本；本轮重点不是继续堆功能，而是消除 0.3.1.3 人工审计暴露出的恶性缺陷，并把 0.3.1.0 → 0.3.1.3 已经形成的产品契约转化为不可回归的 Release Gate。
>
> **目标版本**：DarkGrey_RPG `0.3.1.4`
>
> **施工基线分支**：`codex/0.3.1.3`
>
> **本轮源码审计时确认的 0.3.1.3 commit**：`b855abae465edb05b35655b8658c73a402ae912f` — `fix(studio): finalize 0.3.1.3 acceptance repairs`
>
> **硬规则**：Codex 开工时仍必须执行 `git rev-parse HEAD`。若 `codex/0.3.1.3` 已出现比上述 SHA 更新的提交，必须以开工时真实 HEAD 为施工基线，在 `BASELINE.md` 中记录，禁止静默退回旧 commit。
>
> **第一优先输入**：用户原始文件 `0.3.1.3审计.docx`，共 **8 条**人工问题，含截图。
>
> **历史回归输入**：0.3.1.0 正式产品语义、0.3.1.1 的 29 条 Studio 修缮、0.3.1.2A 返工 Gate、0.3.1.2B 新增交互、0.3.1.3 的 19 条审计 + 6 条源码风险，以及之后已经锁定的交互修正规则。
>
> **重要**：0.3.1.4 仍然 **不是 Studio 冻结版**。Codex 最终只能交付：
>
> ```text
> DarkGrey_RPG 0.3.1.4 Release Candidate
> ```
>
> 只有用户本人可以授予 `USER_ACCEPTED`；只有用户本人明确说“冻结 Studio”后，才可停止 `0.3.1.x` 修缮并进入 `0.3.2.0`。

---

# 0. 0.3.1.4 的核心目标

0.3.1.4 不是：

> “把 0.3.1.3 审计的 8 个 Bug 打上补丁。”

0.3.1.4 必须是：

> **修复 0.3.1.3 新暴露的 8 条问题，解决其中已经反复出现的状态边界、交互状态机与持久化根因，同时对 0.3.1.0 → 0.3.1.3 的仍有效产品契约做一次完整历史回归封锁。**

最终原则：

```text
新问题修复 PASS
AND
历史合同无回归
AND
真实 Release EXE 无恶性稳定性问题
= 0.3.1.4 Release Candidate
```

任何以下情况均为 **NO-GO**：

- 8 条新问题看似修好，但旧问题复发；
- Objective 不再闪退，但节点布局仍丢失；
- 节点布局持久化了，但污染 Runtime canonical JSON / Story Package；
- 单线重接修了，但普通多容量拖动再次抓起全部线；
- ComboBox 能切换了，但每个字符依然触发完整 canonical mutation；
- 为了修 #8 专门硬编码“第一个选项 / 第二个选项”，新增第三个选项又错位；
- 自动化全绿，但真实 Release EXE 仍无法证明动态交互；
- Codex 把 `AGENT_VERIFIED` 写成 `USER_ACCEPTED`。

---

# 1. 输入、权威顺序与“历史要求不等于永远原样保留”

## 1.1 权威顺序

发生冲突时按以下顺序解释：

1. 用户本轮原始 `0.3.1.3审计.docx` 的明确反馈；
2. 本 `0.3.1.4 Construction PLAN`；
3. 2026-09-01 Handoff 中已经明确锁定的产品契约；
4. 0.3.1.3 PLAN 中仍有效且未被新反馈替代的要求；
5. 0.3.1.2A / 0.3.1.2B / 0.3.1.1 / 0.3.1.0 中仍有效且未被后续设计替代的要求；
6. Unity / Godot / Blender / Unreal 等成熟行业交互先例；
7. 当前源码仅代表“现在怎么实现”，不自动代表“应该怎么设计”。

## 1.2 历史回归 Gate 必须识别“已被后续需求替代”的旧要求

0.3.1.4 **不是机械地把所有旧要求全部恢复**。

必须明确记录已被后续决定替代的旧行为。例如：

### 节点删除确认

0.3.1.1 曾要求节点删除弹确认。

0.3.1.3 最新人工审计明确认为该行为过于打断创作。

因此 0.3.1.4 正式规则改为：

```text
普通可删除 Graph placement/node：立即删除
Ctrl+Z：恢复
```

资源本体删除仍可保留确认，因为它是真正 destructive lifecycle 操作。

### 端口 hit target

0.3.1.2B 曾强调“只有圆点/菱形本体可开始拖线”。

后续人工使用证明纯 9px hit target 太小。

最终规则为：

```text
visual anchor ≈ 9–11 px
interaction hitbox ≈ 18–20 px
label text != wire target
```

因此历史回归不是把 hitbox 再缩回 9px。

### 条件节点名称

旧文档中的【条件】后续已统一作者可见名称为：

```text
【条件判断】
```

内部 stable type 仍可保持 `condition`。

### Session Start

旧版 Session【起始】曾有 legacy Logic Output；当前正常 authoring 已明确只保留 Flow。

### 图层术语

旧文档中的“局部图”描述不再作为正式讨论术语，统一：

```text
图谱 → 流程图 → 会话图 / 任务图
```

Codex 必须在 `HISTORICAL_REGRESSION_BASELINE.md` 中维护一节：

```text
Superseded Historical Contracts
```

禁止旧测试把已经被用户明确修改的行为重新锁死。

---

# 2. 施工前硬 Gate

## GATE 0A — 必须完整读取原始 `0.3.1.3审计.docx`

在修改任何生产源码之前，Codex 必须打开原始 DOCX，并逐页阅读：

- 8 条原始文字；
- 每条截图；
- 截图中鼠标位置、节点位置、端口发光、连线形态、Inspector 排版、Choice 选项与端口的相对位置；
- 用户用词中的“完全改不了”“闪退”“重启重置”等强行为描述。

禁止只读本 PLAN 的摘要后开工。

若工作区缺少原始 DOCX：

```text
BLOCKED — ORIGINAL_AUDIT_0313_MISSING
```

不得用 Handoff 摘要代替。

## GATE 0B — 建立审计摄取记录

施工前创建：

```text
PLAN/0.3.1.4/AUDIT_0313_INGEST.md
```

至少记录：

```text
原始文件名
SHA-256
读取时间
8 条审计编号
每条原文行为核心
对应截图 / 页码
严重级别
初始复现状态
初始源码状态
```

初始状态只能使用：

```text
REPRODUCED
SOURCE_CONFIRMED
SOURCE_RISK_IDENTIFIED
NEEDS_LIVE_REPRO
BLOCKED
```

禁止直接写 PASS。

## GATE 0C — 锁定真实 Git 基线

创建：

```text
PLAN/0.3.1.4/BASELINE.md
```

必须包含：

```text
git branch --show-current
git rev-parse HEAD
git status --short
.NET SDK / Windows 环境
Release build 命令
Core tests 结果
WPF tests 结果
```

要求：

- 基线必须是 `codex/0.3.1.3` 的真实开工 HEAD；
- 新建 `codex/0.3.1.4`；
- 不从 0.3.1.2B 或旧 Development Report 反推当前代码。

## GATE 0D — 修复前必须做一次真实 Release 复现

尽可能使用 0.3.1.3 Release EXE 复现 8 条问题。

尤其 #7：

> **必须先捕获真实 crash / unhandled exception stack，再宣布 crash root cause。**

建立：

```text
PLAN/0.3.1.4/CRASH_0313_07.md
```

至少记录：

```text
复现项目
操作顺序
目标切换序列
Exception Type
Message
完整 Stack Trace
InnerException（若有）
Dispatcher / UI Thread 信息
最后一个成功操作
最后一个 GraphRevision
```

如果环境无法触发用户的闪退：

```text
NEEDS_LIVE_REPRO / NEED_USER_VERIFICATION
```

可以修源码中已发现的结构风险，但不得把“猜测的 reentrancy”写成已经确认的闪退根因。

## GATE 0E — 建立历史回归基线

施工前创建：

```text
PLAN/0.3.1.4/HISTORICAL_REGRESSION_BASELINE.md
```

至少摄取：

- 0.3.1.0 正式产品/Graph/资源语义；
- 0.3.1.1 的 29 条人工修缮目标；
- 0.3.1.2A 返工和真实 Release Gate；
- 0.3.1.2B 的 8 项新增交互；
- 0.3.1.3 的 19 条审计 + 6 条源码风险；
- 之后锁定的 wire / hitbox / draft / layout / terminology 规则；
- 被后续需求明确替代的旧合同。

目的不是复制几百页文档，而是形成：

```text
Contract ID
来源版本
当前有效行为
自动化可证明部分
Release EXE 必验部分
是否被后续要求替代
```

## GATE 0F — 未完成以上记录前不得开始生产代码施工

这不是为了文档形式主义，而是防止 0.3.1.1 / 0.3.1.2 曾发生的：

> “修了眼前截图，却把前一轮刚修好的东西重新弄坏。”

---

# 3. 当前 0.3.1.3 源码审计结论

本节用于指导施工入口，不替代 Codex 在本地对真实 HEAD 的再次确认。

## #1 — Inline 属性编辑 / 类型 ComboBox / 文本卡顿

### 已确认的结构问题

当前 UI 中仍存在：

```text
TextBox UpdateSourceTrigger=PropertyChanged
→ setter
→ Host canonical mutation
→ validation / projection / state publish
```

例如 Start 启动条件名称、Session 台词、部分 Action 文本仍有逐字符写入路径。

Objective Inspector 中：

```text
SelectedObjectiveType
→ ChangeObjectiveType
→ 可能再次 SetNodeProperty
→ RefreshFromHost
```

目标 Actor / Item 切换同样在 setter 内直接 `SetNodeProperty` 后 `RefreshFromHost()`。

这已经证明：

> 当前实现仍然没有真正建立完整的 `UI Draft ≠ Canonical Value` 边界。

### 尚需真实复现确认

“下拉点其它类型完全不改变”的**具体失败点**必须通过真实 UI 事件链确认：

```text
SelectionChanged
→ setter
→ mutation result
→ NodesChanged
→ RefreshFromHost
→ SelectedItem projection
```

不要只因为有 TwoWay Binding 就判定 ComboBox 正常。

---

## #2 — 节点删除确认

源码当前仍显式调用：

```text
MessageBox.Show(... "确认删除节点" ...)
```

且 Delete 键和右键删除都走同一路径。

这条是 **SOURCE_CONFIRMED**。

---

## #3 — Valid Target Glow 滞留

当前 `UpdateWire()` 每次 mouse move：

- 更新 `IsConnecting`；
- 对当前命中的 target 设置 `IsValidTarget = valid`；
- 但没有先可靠清除“上一帧 target”的 `IsValidTarget`。

全量清理主要发生在 gesture cancel/end。

因此 stale glow 与源码行为一致。

这条是 **SOURCE_CONFIRMED**。

---

## #4 — 单容量 occupied reconnect 仍错误 / 轻点出现左伸线段

当前代码已经尝试在 occupied port 上复用原正式 `Path` visual。

但 single reconnect 的 `UpdateWire()` 仍然走：

```text
start = 被抓端口原 anchor
wire = WireGeometry(start, mouse)
```

而正确语义应该是：

```text
fixed = 原 connection 的另一端
moving = mouse
wire = WireGeometry(fixed, moving)
```

所以实现只在“复用旧 Path”层面像 reconnect，geometry 语义仍然是新线从被抓端口往外拉。

这是 #4 的核心源码根因之一。

“轻点一下就出现额外左段”还必须结合真实鼠标 down / move threshold 复现。

---

## #5 — Actor / Item Inspector 信息层级

当前 XAML 明确使用连续 TextBlock：

```text
显示名称
<value>
<ID label>
<value>
标签
<value>
拥有/引用状态
<value>
```

label/value 缺乏视觉层级，“拥有/引用状态”也直接暴露给普通作者。

这条是 **SOURCE_CONFIRMED**。

---

## #6 — Node Layout 重启丢失

当前：

```text
GraphEditorHostViewModel._layout
```

明确是 host-only dictionary。

节点移动只更新 `_layout`，不改变 canonical graph JSON。

但 `CanonicalGraphResourceEditorViewModel` 创建 Host 时：

```text
new GraphEditorHostViewModel(Document.Graph, Document.Scope)
```

没有加载持久化 layout；`CreatePersistenceSnapshot()` 也只返回 canonical envelope。

因此关闭 Studio 后无法恢复作者排版不是偶然，而是当前持久化边界中**根本没有这一层数据**。

这条是 **P0 / SOURCE_CONFIRMED**。

---

## #7 — Objective Target 切换闪退

当前源码已经存在明显高风险链：

```text
ComboBox setter
→ SetNodeProperty / ChangeObjectiveType
→ Graph mutation
→ Host NodesChanged / state notifications
→ Inspector RefreshFromHost
→ 重建 selection options / SelectedItem projection
→ setter 返回后又显式 RefreshFromHost
```

这与旧版“切目标很卡”和新版“切一下可能闪退”高度相关。

但：

> **没有 exception stack 前，不把某个具体 reentrancy/null/collection mutation 猜测写成 ROOT_CAUSE_CONFIRMED。**

施工要求是先捕获 stack，再收敛修复。

---

## #8 — Session【选择】节点 option / output 排版错位

正式模型中每个 Choice option 有稳定身份，并对应一个具名 Flow Output。

当前通用节点视觉把：

```text
all inputs → 左侧 Grid
all outputs → 右侧独立 Grid
```

分开堆叠；Inspector 的 ChoiceOptions 又是另一套独立列表。

当前 Node Control 没有“Choice option row ↔ 对应 output port”的布局概念。

这与用户看到不同 option 与出口上下错位**结构上相容**，但仍需通过原始截图 + Release EXE 确定：

- 是纯 visual row 错位；
- 还是 option order / port order 本身映射错；
- 或者二者都有。

初始状态：

```text
SOURCE_RISK_IDENTIFIED + NEEDS_LIVE_REPRO
```

---

# 4. 本版本状态模型

每一条施工项必须使用：

```text
NOT_STARTED
ROOT_CAUSE_CONFIRMED
IMPLEMENTED
AGENT_VERIFIED
USER_ACCEPTED
BLOCKED
```

补充：

### ROOT_CAUSE_CONFIRMED

必须能明确回答：

```text
为什么发生？
是哪一层状态/数据/视觉关系错误？
为什么这个改法比局部 workaround 更正确？
```

### IMPLEMENTED

代码已写，但不能说明真实 UI 已工作。

### AGENT_VERIFIED

必须用本轮 **Release EXE** 真正操作并有证据。

### USER_ACCEPTED

只有用户本人赋予。

### BLOCKED

不能真实复现 / 环境无法采集动态证据 / crash stack 不可得 / 仍有失败。

自动化测试不能把视觉/交互项目直接推到 `AGENT_VERIFIED`。

---

# 5. Work Package A — Editor Draft / Commit 与 Objective 稳定性

**覆盖审计：#1、#7**

**优先级：P0 + P1，本版本第一施工包。**

本包的目标不是给 TextBox 加一个随意 debounce，而是建立最小、明确的：

```text
UI Draft
≠
Canonical Commit
```

## A1. 字段按“Draft 类型”分类

### 文本字段

例如：

- 启动条件名称；
- 台词文本；
- Choice prompt；
- 结束名 / 逻辑输出名；
- Action message；
- 其它 author-facing 可编辑字符串。

规则：

```text
每个字符 → 只改 UI draft
Enter / LostFocus / Explicit Apply / Ctrl+S flush → 一次 canonical commit
```

编辑途中允许：

- 空字符串；
- 尚未完整输入的内容；
- 临时不满足 canonical validation 的文本。

如果最终 commit 时非法：

- Canonical 保持最近一次合法值；
- Draft 留在输入框，不被强行回填一个字符；
- 字段旁显示简短中文错误；
- 技术 code 去 Problems；
- 用户继续编辑即可。

### 数值字段

例如 quantity / radius / XYZ 等：

```text
""
"-"
"1."
```

在输入过程中可以作为 draft 存在。

只在 commit point 解析并写 canonical。

### ComboBox / Picker

ComboBox 不需要等到 LostFocus 才 commit。

正确规则：

```text
一次明确 selection change
→ 一次原子 canonical mutation
```

但 selection commit 期间：

- 不能被同一 mutation 触发的 refresh 反向重入 setter；
- 不能先改 type、再用多个独立 SetNodeProperty 拼出模板；
- 不能在一次选择中生成多条 Undo history。

## A2. 一个作者动作 = 一个正式 mutation transaction

例如 Objective：

```text
实体击杀 → 物品收集
```

应该由 Core/Host 提供一个完整事务，负责：

- 修改 objective_type；
- 初始化该类型需要的默认/空 target；
- 删除不属于新类型的旧字段；
- 保留 description 等共享字段；
- 正确增减 `required`；
- 一次 validate；
- 一次 NodesChanged / PortsChanged（如有必要）；
- 一次 Undo entry。

禁止 UI setter 自己串：

```text
ChangeObjectiveType
SetNodeProperty A
SetNodeProperty B
RefreshFromHost
```

## A3. Objective target switch 同样原子化

对：

```text
角色 → 角色组
物品 → 物品组
```

要求：

- 一次用户选择只写一次正式 target identity；
- 不重建无关资源列表；
- 不全图刷新；
- 不重新创建所有 Inline Inspector VM；
- 不让 SelectedItem 在刷新期间瞬时变 null 再触发 setter；
- 不改变用户当前 viewport / node selection / keyboard focus。

## A4. 最小 reentrancy / projection guard

允许实现一个**局部、明确的** editor refresh guard，例如：

```text
ApplyingCanonicalChange
ProjectingCanonicalChange
```

或等价最小机制。

目的只有：

> “UI 正在把刚提交的 canonical state 投影回来时，不再次把投影当作用户输入。”

禁止扩张成通用 UI State Machine Framework。

## A5. Targeted refresh

Graph mutation 后只更新：

- 当前 changed node；
- 该 node 的 inline editor；
- 当前 Inspector；
- 必要的 validation state；
- port projection（仅当 ports 真变化）。

不得因为：

```text
改一个 Objective target
```

就：

- `Host.Refresh()` 全图；
- 重建全部节点 editor；
- 重建所有 actor/item options；
- 重画所有 connections。

## A6. Ctrl+S 必须 flush 当前 active draft

典型场景：

1. 用户正在 TextBox 输入最后一个字；
2. 焦点仍在 TextBox；
3. 立即按 Ctrl+S。

结果必须：

- active editor draft 先 commit/validate；
- 合法值进入 snapshot；
- 再保存；
- 不能保存倒数第二个值。

Graph switch / Project close 若需要处理 draft，同样先走一致的 editor completion boundary，不通过“把所有字段强制 PropertyChanged”解决。

## A7. ComboBox 真正行为 Gate

必须真实验证：

### Story Start

```text
角色交互
→ 进入区域
→ 逻辑条件
→ 角色交互
```

每次：

- 当前类型文字真实改变；
- 参数字段集合真实改变；
- 对应 Logic port 正确；
- Undo/Redo 正确；
- save/reopen 正确。

### Objective

```text
实体击杀
→ 物品收集
→ 角色交互
→ 实体击杀
```

### Action

逐项切换当前支持的 action type。

### Speaker / resource selector

选择不同 Actor / Item 后状态真实变化。

“能弹下拉菜单”不能算 PASS。

## A8. 性能 Gate

复用现有测试项目/fixture，不新造 Performance Platform。

建议场景：

```text
100 个角色/物品资源
100 个 Graph 节点
>= 30 个 Objective
```

人工 Release EXE：

- 连续键入 30+ 个中文字符；
- 连续切换同一 Objective target 100 次；
- 连续切换 Objective type 100 次；
- 连续切换 Start type 50 次。

硬要求：

```text
0 crash
0 unhandled exception
0 多秒冻结
最终选择正确
```

性能记录至少写：

```text
median
p95
max observed stall
```

建议 Gate：

- 普通一次选择 / commit 的 p95 不高于约 500 ms；
- 任意普通作者操作出现 >1 s UI freeze → NO-GO；
- 更重要的是不能再出现“每敲一个字都完整 canonical mutation”的线性卡顿。

## A9. 自动化最低要求

至少测试：

- 文本 draft 改 20 次字符，GraphRevision 不随字符次数增加；
- 一次 commit 最多产生一次正式 mutation / Undo step；
- draft 可暂时为空；
- invalid draft 不覆盖最后合法 canonical 值；
- Ctrl+S flush active editor；
- Objective type change 是单 transaction；
- Objective target change 是单 transaction；
- UI projection 不重入 setter；
- 100 次 target switch 无异常；
- unrelated node VM identity 不变化。

---

# 6. Work Package B — P0 Node Layout 持久化

**覆盖审计：#6**

**优先级：P0。**

## B1. 数据边界锁定

必须保持：

```text
Canonical RPG Data
≠
Studio Node Layout
≠
Viewport State
```

### Canonical RPG Data

- 节点语义；
- ports；
- connections；
- resource IDs；
- Objective/Action 等 Runtime 数据。

进入 Story Package / Runtime。

### Studio Node Layout

- Node X；
- Node Y。

是作者工作成果，必须跨 Studio restart 保存。

### Viewport State

- Pan X；
- Pan Y；
- Zoom。

仍按已经锁定的“每张图独立 camera”行为；0314 不要求把 viewport 强行持久到永久，除非当前实现已经这样做。

## B2. 优先采用 Studio-only sidecar

施工前先检查项目目前是否已有 Studio metadata / sidecar 约定。

优先复用现有项目级 metadata 目录。

如果没有，允许增加一个**单一、最小** Studio layout sidecar，例如：

```text
studio_layout.json
```

命名可按仓库现有约定调整，但必须满足：

- 项目级一个文件即可；
- 不建立数据库；
- 不建立通用 Persistence Framework；
- 不进入 Story Package；
- Java Runtime 不读取；
- schema 有简单版本字段；
- 原子写入（临时文件 + replace 或现有安全写机制）。

建议 key：

```text
GraphResourceKind
GraphResourceId
NodeId
```

例如概念结构：

```json
{
  "schema": 1,
  "graphs": {
    "story:story_id": {
      "node_a": { "x": 120.0, "y": 80.0 }
    },
    "session:session_id": {},
    "task:task_id": {}
  }
}
```

这只是结构示意；若现有项目 metadata 有更合适格式，优先复用，不需要机械照搬。

## B3. Save / Dirty 语义

节点位置不是“无关 UI 临时状态”。

它是 authoring work。

因此：

- node drag 完成后标记 `LayoutDirty`；
- Studio 总 dirty 状态应能反映“语义未保存或 layout 未保存”；
- Ctrl+S / Save All 保存 canonical snapshot + layout snapshot；
- 关闭项目 / Studio 时，只有 layout dirty 也不能静默丢失；
- 不要求每个 mouse move 写盘；
- 不要 node drag 时每像素写一次 sidecar。

推荐：

```text
mouse move → host memory layout
mouse up → layout dirty
explicit save → disk
```

## B4. Open / Load

打开每个 graph editor 时：

1. 根据 `ResourceKind + ResourceId` 读 layout；
2. 过滤非有限坐标；
3. 只将存在的 node IDs 传入 `GraphEditorHostViewModel`；
4. 新节点没有 layout 时使用现有 fallback placement；
5. 旧 sidecar 中已删除节点的 stale entry 可以在下一次 save 时清理。

## B5. 不得破坏 stable identity

- 重命名 DisplayName → Node layout 不丢；
- Session/Task resource display name rename → layout 不丢；
- node stable ID 不变时 rename/reorder 不丢；
- node 真删除 → 对应 layout entry 删除；
- Undo 删除若恢复同 stable node ID，应恢复合理位置（优先保留本会话内旧 layout）。

## B6. Release EXE Gate

必须至少创建：

- 1 个 Story Flow；
- 2 个 Session；
- 2 个 Task。

每张图排成明显不同位置。

然后：

```text
Save All
关闭 Studio 进程
重新启动
打开同项目
逐张图进入
```

要求：

- 每个节点恢复到保存位置；
- 允许渲染小数/布局计算误差 ≤ 1 DIP；
- 连线对应正确；
- viewport 不串图；
- rename 后重启仍保持；
- Light / Dark 不影响布局数据。

任何“只是在同一次进程里切走再回来保持”不算通过。

## B7. 自动化

至少：

- sidecar roundtrip；
- Story/Session/Task key 不串；
- finite validation；
- deleted node cleanup；
- resource rename stable key；
- canonical serialization before/after移动节点完全相同；
- Story Package 不包含 layout；
- restart fixture 重新创建 editor 后能加载布局。

---

# 7. Work Package C — Wire Gesture 状态重构（最小显式状态）

**覆盖审计：#3、#4**

**优先级：P1 + P2。**

这是已经跨多个版本反复失败的交互，0314 不允许继续靠零散 bool 打补丁。

## C1. 明确四种 gesture

在现有 Graph Editor 内明确区分：

```text
NewConnection
ReconnectSingleEndpoint
AddOnMultiPort
ReconnectMultiBundle
```

不必建立新 Graph Engine。

可以是 enum / 小型 context record。

核心目标：

> 一旦手势开始，代码明确知道用户是在“新增”还是“移动已有 endpoint”。

## C2. Port press 先进入 Pending，超过 drag threshold 才真正拉线

为解决“轻点一下输出端口就出现莫名左伸线段”：

MouseDown 不应立即制造完整 wire geometry。

推荐：

```text
PortPressed
→ mouse displacement >= SystemParameters.MinimumHorizontalDragDistance / MinimumVerticalDragDistance
→ Begin actual wire gesture
```

如果用户只是轻点并松开：

- 不出现 transient wire；
- 不改变 connection visual；
- 不修改 graph；
- 不产生 Undo；
- 不留下 glow。

## C3. 单容量 occupied port：抓起用户实际按下的 endpoint

单容量：

```text
● Flow Output
◆ Logic Input
```

已有 connection 时普通 drag：

```text
A ●────────────● B
^ grab A
```

拖动中必须表现为：

```text
A old position

mouse ●
       ╲
        ╲────────● B
```

硬要求：

- B 固定；
- A endpoint 跟鼠标；
- 使用原 connection 的正式 Path visual；
- 原线不先消失；
- 不创建第二条 fake preview；
- MouseDown 时线不 collapse；
- geometry 永远由 `fixed opposite endpoint → pointer` 计算。

拖 input endpoint 时反过来。

## C4. 单容量 reconnect drop 规则

### Valid same-direction target

移动 output endpoint：

```text
→ drop 到另一个同类型 output
```

移动 input endpoint：

```text
→ drop 到另一个同类型 input
```

一次原子 reconnect。

### Escape

恢复原 connection / 原 visual / 原 endpoint。

### Invalid target

formal graph 不变。

### Blank drop

明确 disconnect 原 connection。

## C5. 多容量规则必须保持，不得为了修单线再次破坏

多容量：

```text
● Flow Input
◆ Logic Output
```

### Ordinary drag

永远：

```text
add one new connection
```

即使当前已有 1、2、N 条。

### Ctrl + drag

才是：

```text
move all existing incident connections
```

这一条不得回退成早期“端口有多条线时普通拖动就抓起全部”。

## C6. Valid target glow 必须和 drop hit-test 一致

每一次 mouse move：

1. 清除上一帧所有 `IsValidTarget`；
2. 用统一 18–20px effective hitbox 找当前 candidate；
3. 计算当前 candidate 是否 valid；
4. 仅当前真正 valid candidate 发光；
5. pointer 一离开 effective area，同一 mouse move 立即取消 glow。

必须复用**同一套 hit-test**用于：

```text
hover/glow
mouse-up drop validation
```

禁止一套区域负责亮、一套区域负责 drop。

## C7. Port hit contract 回归

必须保持：

```text
visual ●/◆ ≈ 9–11px
hitbox ≈ 18–20px
label text 不开始 wire
hitbox 不覆盖邻近端口
```

## C8. 全 cardinality 回归矩阵

### Flow

合法：

```text
A Out → X In
B Out → X In
```

非法：

```text
A Out → X In
A Out → Y In
```

### Logic

合法：

```text
A Out → X In
      → Y In
```

非法：

```text
A Out ─┐
B Out ─┴→ X In
```

## C9. Release 动态证据

单线 reconnect 必须用视频/GIF/连续帧证明：

```text
Frame 1: 原正式 wire
Frame 2: mouse down（没有 collapse / extra segment）
Frame 3: endpoint 离开原 port，另一端固定
Frame 4: valid target hover
Frame 5: commit / cancel
```

额外：

- 对同一端口轻点 20 次，不出现左伸 wire；
- glow 从有效端口移出后立即消失；
- ordinary multi drag 新增一条；
- Ctrl multi drag 整束移动。

静态 XAML / 单测不能代替该 Gate。

---

# 8. Work Package D — Actor / Item Inspector 信息设计

**覆盖审计：#5**

**优先级：P2。**

## D1. 资源 Inspector 层级

不要继续：

```text
显示名称
测试角色
NPC_ID
xxx
标签
...
```

label 和 value 视觉同权重。

建议采用明确 field block：

```text
显示名称：
测试角色

NPC_ID：
test_actor

标签：
merchant, quest
```

实现上允许：

- label 使用 secondary / smaller style；
- value 使用 primary style；
- ID 用 monospace 不是必须，避免过度技术感；
- 每个 field 有稳定间距。

## D2. 作者术语必须保持当前正式模型

Actor：

```text
角色
NPC_ID
```

Actor Group：

```text
角色组
Group_ID
```

Item：

```text
物品
Item_ID
```

Item Group：

```text
物品组
Group_ID
```

不要回退到：

```text
Actor ID
角色 ID
个体物品
集体物品
```

## D3. “拥有/引用状态”从普通属性中移除

正常作者不需要理解内部 membership provenance 才能编辑资源。

0314 最小方案：

> 从普通 Actor / Item Inspector 的主要“资源属性”区删除“拥有/引用状态”。

如果现有功能确实需要用户知道资源来自哪里，可放到：

```text
次要“资源信息”区
```

并使用真正作者可理解的文案，例如：

```text
当前故事：自有资源
当前故事：引用资源
```

但没有明确用途时优先不显示。

## D4. 不得回归的 Inspector 旧要求

- Inspector 仍可纵向滚动；
- Light / Dark 都可读；
- IDs / tags 仍能看到；
- 普通 UI 不泄漏 internal resource GUID；
- selected resource 与 Inspector 一致；
- compact resource rows 不受影响。

---

# 9. Work Package E — Session【选择】节点 option ↔ Flow Output 一一对齐

**覆盖审计：#8**

**初始优先级：P1（存在作者误接线风险）。**

如果最终确认仅为纯装饰间距问题，可降为 P2；若 output 映射顺序本身错误，则保持 P1。

## E1. 先确认语义映射，不要先修 CSS/XAML

正式 Choice model：

```text
prompt
options[]
```

每个 option：

```text
option_id
stable flow_port_id / stable output identity
DisplayText
Order
```

每个选项必须清楚对应**一个** Flow Output。

施工前记录：

```text
option_id
option order
display_text
port_id
port order
rendered row index
```

确认用户截图中的错位到底发生在哪一层。

## E2. 最终 UI invariant

对于 N 个选项：

```text
选项 1  ───────────── ● 对应出口 1
选项 2  ───────────── ● 对应出口 2
选项 3  ───────────── ● 对应出口 3
...
```

具体文字可按现有产品语言设计，但必须：

- 一行/一组明确对应；
- 不能 option 2 的 label 与 option 1 的 port 垂直错位；
- port anchor 仍统一贴节点右边缘；
- 长文本只影响内部 label 排版，不移动 anchor X；
- option rename 不断线；
- option reorder 不断线；
- stable `option_id` / `port_id` 不由 DisplayText 生成。

## E3. 不要硬编码“第一个/第二个”

禁止：

```text
if index == 0 margin = ...
if index == 1 margin = ...
```

正确做法应基于：

- 同一 ordered option projection；
- stable mapping；
- 统一 row template；
- 或为 Choice node 提供一个明确的专用 row projection。

允许 Choice 节点有专用 template，因为它有独特的“选项 ↔ 动态输出”语义；但不要因此重做 Generic Node UI Framework。

## E4. Inline / Inspector 与 Port 关系

当前 Inspector 已能编辑 ChoiceOptions。

0314 应保证：

```text
Inspector option order
=
Node option visual order
=
Flow output order
```

一次 rename/reorder：

- 只刷新当前 Choice node；
- 不 RebuildGraph；
- 不丢 node position；
- 不丢 selection；
- connections 仍绑定 stable port ID。

## E5. Gate

真实创建：

```text
1 个 option
2 个 options
5 个 options
10 个 options
```

测试：

- add；
- rename；
- reorder；
- delete；
- long text；
- 每个 output 接到不同 node；
- save / reopen；
- Light / Dark；
- 100% / 125% / 150% DPI。

要求所有 option 与 output 一一对应，不交叉、不错位。

---

# 10. Work Package F — Graph Node 删除 UX

**覆盖审计：#2**

**优先级：P2。**

本包明确替代 0.3.1.1 旧“每次节点删除都确认”的行为。

## F1. 普通可删除 Graph node / placement

右键【删除】或 Delete：

```text
立即执行
```

不弹 MessageBox。

## F2. Undo 是主要安全机制

一次 node deletion transaction 必须包含：

- node；
- incident connections。

`Ctrl+Z` 一次恢复全部。

## F3. 固定节点仍不可删除

例如：

- Story【开始】；
- Session【起始】；
- Task【结算】；
- 其它 `Required / NonDeletable`。

不因为去掉 confirmation 就突破 Core protection。

## F4. Aggregate placement lifecycle 不变

删除 Story Flow 中：

```text
Session placement
Task placement
```

只删除 placement + incident wires。

**资源本体保留。**

资源库删除 Session / Task 本体时：

- 仍按 destructive resource lifecycle 处理；
- 删除/解除 Story membership 时清理所有 placements；
- 资源级操作可以继续有确认。

## F5. 不增加“永不再询问”Setting

用户给了两种可接受方向：

- 删掉确认；
- 或“不再询问”。

0314 选择更成熟且更简单的：

> **节点删除直接执行 + Undo。**

不要为了一个确认框再新增全局 Settings infrastructure。

---

# 11. Work Package G — 历史回归封锁

这是 0.3.1.4 与以往修补版最不同的部分。

**任何 still-valid 历史契约回归 = NO-GO。**

## G1. Workspace / Navigation 回归

必须保持：

- Project level 三栏：Story list / 图谱 / selected Story Inspector；
- Story level 三栏：resource library / 流程图或会话图/任务图 / Inspector；
- breadcrumb：`<Project> > <Story> > <Session/Task>`；
- 点击 Project 返回 Project workspace；
- 点击 Story 返回 Story Flow；
- Dirty 不阻塞 Story / Session / Task 普通导航；
- Dirty 不阻塞创建角色/物品/会话/任务；
- Inspector 可滚动；
- selected resource 有稳定可见状态；
- 资源右键命中资源本体，不被 folder 截获。

## G2. Resource Library 回归

必须保持：

```text
角色
物品
会话
任务
```

资源行仍紧凑：

```text
酒馆老板    NPC_ID: tavern_boss
史莱姆      Group_ID: slimes
铜币        Item_ID: copper_coin
剑类        Group_ID: swords
```

Session / Task 单行。

资源排序：

- drag reorder；
- 有清晰 insertion/drop-position preview；
- 保存重启保持；
- 不自动重新按名称排序。

## G3. Resource lifecycle / stable identity 回归

- rename 只改 DisplayName；
- NPC_ID / Item_ID / Group_ID / stable resource ID 不变；
- dynamic port ID 不因 rename 变化；
- delete resource → placements + incident wires 清理；
- delete placement → resource 保留；
- referenced / owned 语义不因 reorder 变化。

## G4. Palette 回归

正常 blank-canvas add menu **不出现**：

- Story【开始】；
- Session【起始】；
- Task【结算】；
- Task【激活】；
- compatibility-only nodes；
- “聚合 → 会话/任务”。

Session/Task aggregate placement 继续由资源库 drag 创建。

【条件判断】保持正确分类与顺序。

## G5. Story Start 回归

- author-facing 统一“启动条件”；
- 1..N 条；
- 默认名唯一：启动条件 1/2/3...；
- DisplayName 同时作为 Flow Output label；
- rename 不改变 stable port ID；
- 类型：当前正式支持项；
- legacy EnterStory 不作为新 authoring；
- 多启动条件 OR；
- 有第二条后第一条可删除；
- 只剩最后一条时不得删除；
- region 参数 XYZ 同组；
- 启动条件卡片分组清晰。

## G6. Session 回归

- 独立会话图；
- 固定【起始】只保留 Flow；
- 【台词】/【选择】/【旁白】/【条件判断】/逻辑节点可正常 author；
- Choice 动态端口实时刷新；
- 进入/退出对应 aggregate ports 稳定；
- Session resource drag into Story Flow 正常。

## G7. Task 回归

Task 必须继续：

```text
纯 ◆ Logic 图
```

不得重新出现：

```text
● Flow
【激活】
【起始】
【动作】
【会话】
```

【结算】：

- 固定唯一；
- N named Logic inputs；
- 结果 1/2/3 自动唯一；
- first true wins；
- rename/reorder stable port ID。

## G8. Objective 回归

正式作者可见类型：

```text
实体击杀
物品收集
角色交互
```

语义：

### 实体击杀

- Actor / Actor Group；
- quantity required。

### 物品收集

- Item / Item Group；
- quantity required。

### 角色交互

- Actor / Actor Group（按正式身份契约）；
- **无 quantity**。

不得重新要求用户输入 `minecraft:slime` / `minecraft:stone` 作为正常 authoring。

## G9. Actor / Item identity 回归

Actor：

```text
角色 → NPC_ID
角色组 → Group_ID
```

Item：

```text
物品 → Item_ID
物品组 → Group_ID
```

Give Item：

```text
只接受 Individual Item_ID
```

Group_ID 不可作为 Give Item target。

## G10. Graph visual / interaction 回归

必须保持：

- ● Flow 圆形；
- ◆ Logic 菱形；
- input anchor 左；
- output anchor 右；
- label 长度不改变 anchor X；
- 18–20px hit target；
- label 不开始 wire；
- resource ghost 与 drop pointer-relative position 一致；
- wire 使用正式 Flow / Logic visual language；
- scissors cursor 当前正式版本仍可用；
- Left Alt 临时 scissors；
- per-graph viewport 不串。

## G11. Theme 回归

Light / Dark 都必须可用：

- node border；
- panel boundary；
- selected resource；
- ports；
- wires；
- TextBox/ComboBox；
- popup；
- Problems；
- Inspector label/value。

任何“Dark 正常所以 Light 先不管” → FAIL。

## G12. Error presentation 回归

普通 authoring surface：

```text
短中文 + 可操作建议
```

不得常驻显示：

```text
graph.xxx.xxx
raw exception
internal node type
raw field path
```

这些进入 Problems technical details。

## G13. Save / restart 回归

保存并彻底重启后必须保持：

- canonical node / connection；
- dynamic ports；
- stable IDs；
- resource identities；
- resource order；
- **0314 新增 node layout**；
- Start conditions；
- Choice options；
- Task settlement order。

## G14. 一个完整 Studio Authoring Smoke

从空项目完成：

1. 创建 Story `史莱姆清理委托`；
2. 创建角色组 `史莱姆 / Group_ID: slimes`；
3. 创建角色 `酒馆老板 / NPC_ID: tavern_boss`；
4. 创建个体 Item `铜币 / Item_ID: copper_coin`；
5. 创建 Session；
6. Session 中放【台词】+【选择】；
7. Choice 至少 3 个选项并分别接线；
8. 创建 Task；
9. Objective：`实体击杀 / slimes / 10`；
10. 接到【结算】；
11. 从资源库把 Session / Task 拖到 Story Flow；
12. Story Start → Session → Task；
13. 如使用奖励动作，Give Item 只能选 `copper_coin`；
14. 修改多个节点位置；
15. Save All；
16. 关闭 Studio；
17. 重开；
18. 验证全部资源、连线、option mapping、node layout、resource order。

0314 不要求进入 Minecraft 做完整 0.3.2 级别游戏内验收；但 Studio 必须能完整编制并保存这个 canonical 示例。

---

# 12. Anti-Overarchitecture 施工约束

本轮禁止新增：

- Graph Editor v2；
- Generic Inspector Framework；
- Generic Form DSL；
- 全局 AutoSave Framework；
- 新数据库；
- Layout Database；
- 通用 Event Bus；
- UI Plugin System；
- Dev Harness / Scenario Platform；
- 大型性能测试平台；
- 新 Objective 类型；
- 新 Action 类型；
- Minecraft 0.3.2 功能；
- Runtime 大重构。

允许的最小结构修正：

- 明确的 editor draft model；
- 明确的 4 类 wire gesture state；
- 单一 Studio-only layout sidecar；
- Choice 专用 row projection；
- 局部 reentrancy guard；
- 受影响 node targeted refresh。

这些是为了消灭已经反复出现的根因，不属于“平台化”。

---

# 13. 建议施工顺序

## Stage 0 — Audit / Baseline / Repro

完成：

- GATE 0A–0F；
- 读原始 0313 审计；
- 锁 HEAD；
- baseline build/tests；
- 8 条 pre-fix repro；
- #7 crash stack；
- historical regression baseline。

**未完成 Stage 0 不改生产代码。**

## Stage 1 — P0 Objective Crash + Draft/Commit

完成 Work Package A。

原因：

> 当前编辑状态链会影响所有后续 UI 验收；如果它仍在 reentrant crash，先修视觉没有意义。

Gate：

- target 100 次 0 crash；
- type 100 次 0 crash；
- TextBox 可临时空；
- typing 不逐字符 canonical mutate；
- Ctrl+S flush；
- dead ComboBox 真正生效。

## Stage 2 — P0 Node Layout Persistence

完成 Work Package B。

Gate：

- Story/2 Session/2 Task 排版；
- save；
- process restart；
- 全部位置恢复。

## Stage 3 — Wire Gesture

完成 Work Package C。

Gate：

- light click 20 次无 phantom；
- single endpoint 真 reconnect；
- glow 与 drop 一致；
- multi ordinary/Ctrl 两种行为都正确；
- cardinality 全矩阵。

## Stage 4 — Choice / Inspector / Delete UX

完成 Work Package D/E/F。

Gate：

- Choice 1/2/5/10 options 对齐；
- Actor/Item Inspector 层级正确；
- node delete 无弹窗 + Undo；
- resource delete 生命周期不变。

## Stage 5 — Historical Regression Pass

按 Work Package G 全量巡检。

不是只跑自动化。

所有曾经要求 Release EXE 的动态项继续 Release EXE。

## Stage 6 — Final Audit Re-open

Codex 必须再次打开原始：

```text
0.3.1.3审计.docx
```

逐条 1→8 检查。

然后再次打开历史 regression baseline，逐条检查 still-valid contracts。

禁止只按 Work Package 自己总结“都修了”。

---

# 14. 0.3.1.4 自动化测试最低要求

自动化负责证明可自动证明的部分，不承担动态 UX 最终验收。

## 14.1 Draft / Commit

- draft typing 不增 GraphRevision；
- commit 一次增一次（若实际有变更）；
- temporary empty 合法存在于 draft；
- invalid commit 不破坏 canonical；
- Ctrl+S flush；
- projection change 不重入 mutation；
- one ComboBox selection = one transaction。

## 14.2 Objective Stability

- type cycle 100 次；
- actor target cycle 100 次；
- item target cycle 100 次；
- no exception；
- options final selection correct；
- unrelated node VM identity retained；
- no full graph rebuild counter/event。

## 14.3 Layout

- sidecar roundtrip；
- resource kinds isolated；
- node stable key；
- rename persists；
- deleted stale cleanup；
- canonical JSON unchanged by positions；
- Story Package unaffected。

## 14.4 Wire

- new connection；
- single reconnect output endpoint；
- single reconnect input endpoint；
- cancel restores；
- invalid target no formal mutation；
- blank drop disconnect；
- ordinary Flow Input adds one；
- Ctrl Flow Input bundle move；
- ordinary Logic Output adds one；
- Ctrl Logic Output bundle move；
- four cardinality cases。

## 14.5 Node Delete

- immediate deletion no confirmation seam required；
- protected nodes fail closed；
- incident connections removed；
- one Undo restores node + wires；
- placement delete does not delete Session/Task resource。

## 14.6 Choice

- 1/2/5/10 option projection；
- option order == output order；
- stable mapping by IDs, not display text；
- rename/reorder connections survive；
- targeted node port refresh only。

## 14.7 Regression

继续保留并修正旧 tests：

- palette filtering；
- Start names/ports；
- Session start no legacy logic out；
- Task pure logic；
- Settlement stable slots；
- DGR Actor/Item identity；
- resource delete placements；
- reorder persistence；
- theme resource bindings；
- error presentation mapping；
- per-graph viewport isolation。

如果某旧 test 锁的是被用户后续明确推翻的旧行为（例如 node delete confirmation），必须：

1. 标记为 superseded；
2. 修改/删除该旧 assertion；
3. 在 historical baseline 记录原因；
4. 不能为了让旧 test 绿而违背最新用户要求。

---

# 15. 真实 Release EXE Manual Acceptance Matrix

必须写入：

```text
PLAN/0.3.1.4/MANUAL_ACCEPTANCE.md
```

每条：

```text
Case ID
Build SHA
EXE path
Steps
Expected
Actual
Evidence
Status
```

## S1 — Text Draft

- Start 名称从已有文字 Ctrl+A 删除到空；
- 保持空 2 秒；
- 再输入新名称；
- 期间 UI 不强塞旧字符；
- 不多次重 GraphRevision；
- Enter commit。

## S2 — ComboBox

真实点选 Start / Objective / Action 每个类型。

不能用 programmatic setter 替代。

## S3 — Objective target 100 次稳定性

```text
角色
↔ 角色组
```

或合法资源序列持续切换。

0 crash。

## S4 — Node Layout Restart

排图 → Save → 关闭进程 → 重开。

## S5 — Node Delete

Delete 无确认；Ctrl+Z 恢复。

## S6 — Glow

进入 valid hitbox → glow；移动到明确外部 → 同一移动后取消。

## S7 — Light Click Phantom

输出 port 轻点 20 次，无左伸 wire。

## S8 — Single Flow Output Reconnect

occupied Flow Output 抓起原 endpoint。

## S9 — Single Logic Input Reconnect

occupied Logic Input 抓起原 endpoint。

## S10 — Multi Flow Input Ordinary

已有 3 条线，普通 drag 仍只新增一条。

## S11 — Multi Flow Input Ctrl

Ctrl+drag 3 条一起移动。

## S12 — Multi Logic Output Ordinary/Ctrl

同样两套手势。

## S13 — Inspector

Actor / Actor Group / Item / Item Group 四种资源截图。

检查 label/value、ID、tags、无“拥有/引用状态”主字段。

## S14 — Choice 1/2/5/10

每个 option 与 output 明确一一对齐。

## S15 — Choice connection persistence

5 个 option 各连不同 target；rename/reorder/save/restart 后映射不串。

## S16 — Light / Dark

Graph + Inspector + Choice + Problems。

## S17 — Resource lifecycle

- 删 placement → resource 在；
- 删 resource → placement 全消失。

## S18 — Resource reorder

有 insertion preview，save/restart 保持。

## S19 — Per-graph viewport

Story / Session A / Task A 设置不同 camera，切换恢复自身 camera。

## S20 — Canonical Authoring Smoke

完整跑第 G14 的“击杀10个史莱姆”Studio 编制流程。

---

# 16. 性能与稳定性 NO-GO

以下任一出现，0.3.1.4 不得提交为可验收 RC：

```text
任何已知操作导致 Studio process crash
普通 ComboBox selection 可导致 unhandled exception
普通文本输入持续 >1s freeze
Objective target 切换出现多秒卡死
保存后 node layout 丢失
single reconnect 仍先断线再拉新线
glow 与真实 drop region 不一致
Choice option 与 output 映射仍错位
```

Crash 不能写成：

```text
偶发，后续再看
```

P0 稳定性问题必须清零。

---

# 17. Source Modification Guidance（当前热点）

Codex 开工应重点检查但不限于：

```text
studio/src/DarkGreyRPG.Studio/
  ViewModels/Graph/
    CanonicalGraphResourceEditorViewModel.cs
    CanonicalNodeInspectorViewModel.cs
    GraphEditorHostViewModel.cs

  Views/Graph/
    CanonicalGraphEditorView.xaml.cs
    CanonicalGraphNodeControl.xaml
    CanonicalGraphNodeControl.xaml.cs
    CanonicalInlineNodeEditorControl.xaml
    CanonicalStoryWorkspaceView.xaml

studio/src/DarkGreyRPG.Studio.Core/
  Graphs/Editing/
    GraphEditSession.cs
    GraphEditorCommandBridge.cs (若实际存在/对应文件)
  Graphs/Definitions/
    SessionChoiceSchema.cs
    CanonicalTaskObjectiveSchema.cs
    StoryStartSchema.cs
```

以及：

- workspace / project persistence repository；
- project metadata 保存位置；
- WPF tests；
- Core tests。

## 17.1 不要把 UI transaction 逻辑全塞进 XAML code-behind

类型切换、target switch 的原子语义应该在 Core/Host 可测试边界完成。

## 17.2 不要把 layout 写回 GraphNode properties

除非本地源码审计证明项目已经有正式 editor metadata canonical extension，且 Runtime 明确忽略；否则优先 Studio sidecar。

## 17.3 不要用 `Host.Refresh()` 解决每一个状态同步问题

优先 targeted projection / event。

## 17.4 不要继续增加 wire booleans

将 gesture 意图统一进一个明确 context。

---

# 18. 8 条最新审计 Traceability

| 0313 审计 | 严重度 | 0314 处理位置 | 最终 Gate |
|---:|---|---|---|
| #1 类型下拉无效 / 打字卡 / 不能临时清空 | P1 | A1-A9 | S1/S2 + Draft automated |
| #2 删除节点确认烦 | P2 | F1-F5 | S5 |
| #3 valid target glow 滞留 | P2 | C6-C7 | S6 |
| #4 左伸 phantom + 单容量 reconnect 错 | P1 | C1-C9 | S7-S12 |
| #5 Actor/Item Inspector 信息层级差 | P2 | D1-D4 | S13 |
| #6 节点排版重启丢失 | **P0** | B1-B7 | S4 |
| #7 Objective target 切换闪退 | **P0** | A2-A9 + Crash Gate | S3 |
| #8 Session【选择】option/output 排版错位 | P1 初始 | E1-E5 | S14/S15 |

最终 Review 不得只引用此表，必须重新打开原 DOCX。

---

# 19. Historical Regression Traceability（最低主合同）

最终 `HISTORICAL_REGRESSION_FINAL_REVIEW.md` 至少逐项确认：

| Contract | 当前正式要求 |
|---|---|
| Terminology | 故事；图谱→流程图→会话图/任务图 |
| Flow cardinality | Out max1 / In multi |
| Logic cardinality | Out multi / In max1 |
| Multi drag | ordinary add / Ctrl bundle move |
| Single occupied reconnect | 抓真实 existing endpoint |
| Hit target | 9–11px visual / 18–20px hitbox / label non-target |
| Dirty | unsaved != locked |
| Draft | UI draft != canonical value |
| Project hierarchy | Project 3-column / Story 3-column |
| Aggregate creation | resource drag placement；palette 不造空 aggregate |
| Resource deletion | resource→placements delete；placement→resource keep |
| Palette | fixed/unique/compat/aggregate hidden |
| Session Start | Flow only |
| Task | pure Logic, no 激活 |
| Settlement | unique, named Logic slots, first true |
| Objective interact | no quantity |
| Actor | 角色/NPC_ID；角色组/Group_ID |
| Item | 物品/Item_ID；物品组/Group_ID |
| Give Item | Individual Item_ID only |
| Resource order | drag preview + persistence |
| Viewport | per graph isolation |
| Node Layout | **0314：cross-restart persistence** |
| Light/Dark | both usable |
| Errors | normal UI Chinese; technical details in Problems |
| Acceptance | Codex RC only; user grants USER_ACCEPTED |

此表只是最低集合；0311/0312/0313 已明确但未列在此处的 still-valid 条目仍要在正式 regression baseline 中保留。

---

# 20. Final Audit Gate

施工结束后，Codex 必须执行以下顺序，不能反过来：

## Gate F1 — Build / Automated

- Release build PASS；
- Core tests PASS；
- WPF tests PASS；
- 若触及 canonical serializer / Java parser，相关 Java build / tests PASS；
- 若只新增 Studio-only layout，不得为了“顺手”修改 Runtime。

## Gate F2 — 0313 Original Audit Re-open

重新打开：

```text
0.3.1.3审计.docx
```

逐条对照 1–8。

建立：

```text
PLAN/0.3.1.4/AUDIT_0313_FINAL_REVIEW.md
```

每条必须记录：

```text
Original Issue
Root Cause
Implementation
Release Steps
Evidence
Status
```

## Gate F3 — Historical Regression Re-open

逐项核对：

```text
PLAN/0.3.1.4/HISTORICAL_REGRESSION_BASELINE.md
```

输出：

```text
PLAN/0.3.1.4/HISTORICAL_REGRESSION_FINAL_REVIEW.md
```

任何 still-valid 历史项 FAIL：

> 0314 NO-GO。

## Gate F4 — Dynamic Evidence Rule

以下必须视频/GIF/连续帧，单截图不够：

- single endpoint reconnect；
- Ctrl multi bundle；
- ordinary multi add；
- port glow clearing；
- light-click phantom absence；
- Choice reorder/port mapping；
- Objective 100x stability（可视频 + log）；
- viewport restoration。

Node layout restart 可用：

- 关闭前截图；
- 进程关闭证据；
- 重开后截图；
- 坐标记录。

## Gate F5 — 不可证明就 BLOCKED

如果 Codex 环境无法捕获鼠标/视频或无法稳定复现用户 crash：

```text
BLOCKED / NEED_USER_VERIFICATION
```

不能因为单测绿就改成 PASS。

---

# 21. Definition of Done / Release Candidate Gate

0.3.1.4 只有同时满足以下条件，Codex 才可以提交 **Release Candidate**：

- [ ] 原始 `0.3.1.3审计.docx` 已完整读取；
- [ ] 8 条审计有独立 root cause / status；
- [ ] #6 Node Layout 跨进程重启持久化；
- [ ] #7 Objective target switch 0 crash；
- [ ] #1 建立真实 Draft/Commit 边界；
- [ ] 类型 ComboBox 真实可改；
- [ ] 文本编辑途中可以暂时为空；
- [ ] Ctrl+S 不丢最后一次 draft；
- [ ] single occupied reconnect 使用原正式 wire；
- [ ] 轻点 port 无 phantom segment；
- [ ] valid glow 与 drop hitbox 完全一致；
- [ ] multi ordinary / Ctrl 手势保持锁定语义；
- [ ] Actor/Item Inspector 信息层级修正；
- [ ] 普通资源 Inspector 不再把“拥有/引用状态”作为主要属性；
- [ ] Choice option ↔ Flow Output 一一对齐；
- [ ] Choice rename/reorder stable ID 不断线；
- [ ] graph node delete 无重复确认且 Undo 正确；
- [ ] resource deletion / placement deletion 不对称生命周期保持；
- [ ] 0.3.1.0→0.3.1.3 still-valid 历史合同无回归；
- [ ] Light / Dark 均通过；
- [ ] Release build PASS；
- [ ] Core/WPF tests PASS；
- [ ] MANUAL_ACCEPTANCE 已填写；
- [ ] dynamic UI evidence 齐全或明确 BLOCKED；
- [ ] 无已知 P0/P1 FAIL 被写成 PASS；
- [ ] GitHub 已 push `codex/0.3.1.4`；
- [ ] 提供最终 RC commit SHA。

以下语句 Codex **禁止**写：

```text
0.3.1.4 已最终完成
Studio 已冻结
Studio 全部通过
不再需要用户验收
```

允许：

```text
DarkGrey_RPG 0.3.1.4 Release Candidate
AGENT_VERIFIED: ...
BLOCKED / NEED_USER_VERIFICATION: ...
等待用户人工验收
```

---

# 22. 最终交付目录

建议提交：

```text
PLAN/0.3.1.4/
├─ BASELINE.md
├─ AUDIT_0313_INGEST.md
├─ CRASH_0313_07.md
├─ ROOT_CAUSE_RECORD.md
├─ HISTORICAL_REGRESSION_BASELINE.md
├─ MANUAL_ACCEPTANCE.md
├─ AUDIT_0313_FINAL_REVIEW.md
├─ HISTORICAL_REGRESSION_FINAL_REVIEW.md
├─ EVIDENCE_INDEX.md
└─ evidence/
```

并提供：

```text
PLAN/DarkGrey_RPG_0.3.1.4_Development_Report.md
```

Development Report 必须区分：

```text
ROOT_CAUSE_CONFIRMED
IMPLEMENTED
AGENT_VERIFIED
BLOCKED
NEED_USER_VERIFICATION
```

不得把“测试存在”写成“真实交互已验证”。

---

# 23. GitHub 施工与提交要求

1. 基于开工时真实 `codex/0.3.1.3` HEAD；
2. 创建：

```text
codex/0.3.1.4
```

3. 小步 commit，但不要为了 Work Package 建大量无意义分支；
4. 最终 push GitHub；
5. 提供 RC SHA；
6. 不自动 merge `main`；
7. 不自动 tag / GitHub Release，除非用户明确要求；
8. 不修改历史版本报告来制造“以前其实已通过”的记录；
9. 若 branch 基线在施工中变化，不静默 rebase 到未知状态，先记录；
10. 最终源码和证据必须足够让下一轮 ChatGPT / Codex 直接审计。

---

# 24. 一句话施工标准

> **DarkGrey_RPG 0.3.1.4 的任务不是再做一轮表面修补，而是把 0.3.1.3 暴露出的 P0 数据丢失与闪退、系统级 Draft/Commit 错误、反复失败的 single-wire reconnect、Choice 映射错位和 Inspector 信息设计一次收口；同时把 0.3.1.0→0.3.1.3 已经形成的 still-valid 产品行为作为完整 Regression Gate，任何旧问题复发都视为 0.3.1.4 Release Blocker。**
