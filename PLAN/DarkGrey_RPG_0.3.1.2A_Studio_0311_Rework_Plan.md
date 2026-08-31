# DarkGrey_RPG 0.3.1.2A — Studio 0.3.1.1 返工修复施工 PLAN

> **版本定位**：0.3.1.1 验收失败后的返工版本  
> **目标**：只补齐 0.3.1.1 本来就已经承诺、但实际没有正确完成或没有真实验收的 Studio 内容  
> **基线分支**：`codex/0.3.1.1`  
> **基线 HEAD（审计时）**：`33c1168b2d80d9225146c5339b80274624f84b40`  
> **建议施工分支**：`codex/0.3.1.2A`  
> **输入证据**：
> - `0.3.1.1审计.docx`
> - `DarkGrey_RPG_0.3.1.1_Studio_Repair_Construction_Plan.md`
> - GitHub 当前 0.3.1.1 源码
>
> **特别声明**：0.3.1.2A 不是新功能版本，也不是“顺便优化”版本。它是 **0.3.1.1 未完成项的返工**。  
> Codex 不得把“已经写过相关代码”“单元测试通过”“自动化验证通过”当成完成。  
> 本版本最终只能先提交 **Release Candidate**；在用户真实操作确认前，Codex **无权自行宣布 0.3.1.2A 完成**。

---

# 0. 这次为什么要单独做 A 版

0.3.1.1 的主要失败不是“完全没写代码”，而是：

1. 把需求做成了“看起来像”；
2. 用自动化测试验证了代码路径，却没有验证真实 Studio 交互；
3. 明知人工 S1-S19 全部 `PENDING`，仍然写“已完成本轮 Studio 修缮范围”；
4. 部分实现直接违背了原 PLAN 的明确语义。

典型例子：

- “剪刀鼠标”实现成 `Cursors.Cross`；
- “真实连线拖动”实现成白色 `_draftWires`；
- “节点上编辑参数”实现成只读 `ParameterSummary TextBlock`；
- “固定节点不应出现在添加菜单”仍然通过 `CanAuthorNodeType=false` 置灰，而不是从菜单源排除；
- Objective UI 套了 DGR 资源选择器，但 Core 仍默认写 `minecraft:slime / minecraft:stone`；
- “未保存不应阻塞普通编辑”仍有 `CanMutateCanonicalStoryResources()` 一刀切；
- 真实 Studio 人工验收没有做，却自行宣布完成。

0.3.1.2A 的目标就是把这些 **0311 应当做到、但实际上没有做到的东西**补完。

---

# 1. 绝对版本边界

## 1.1 本版本必须修

只修以下类型：

- 0.3.1.1 PLAN 已明确要求的行为；
- 0.3.1.1 人工审计已经指出、0311 本应解决的旧问题；
- 为了把这些旧问题真正修好而必须处理的同根缺陷；
- 0.3.1.1 自动化“假通过”所暴露出的验收缺陷。

## 1.2 本版本不修

以下新增需求放到 **0.3.1.2B**：

- 项目级 breadcrumb：`<项目名> > <故事名>`；
- 资源右键“重命名”；
- 区域参数重新排版为“维度单独 / XYZ 同排”；
- 【条件】更名为【条件判断】；
- 资源库改为全新一行式紧凑排版；
- 端口只能点击圆点/菱形本体开始拖线；
- 【结算】动态端口修改后实时刷新节点的新增专项修复；
- 资源库拖动排序并持久化顺序。

这些不是 A 的范围，禁止在 A 版顺手做。

## 1.3 禁止扩张

不得新增：

- 新 Graph Editor；
- Node UI DSL；
- 通用 Inspector Framework；
- 通用 Drag Framework；
- 通用 Theme Engine；
- 全局 AutoSave Framework；
- Dev Harness / Scenario / Fixture 平台；
- 新 Objective 类型；
- 新 Action 类型；
- 新 Runtime 系统；
- Minecraft 游戏内功能。

优先修改现有代码，局部修正即可。

---

# 2. 本版本的状态机：禁止“假完成”

所有施工项必须使用以下状态，禁止只写“完成/未完成”。

```text
NOT_STARTED
IMPLEMENTED
AGENT_VERIFIED
USER_ACCEPTED
BLOCKED
```

定义：

### NOT_STARTED
尚未施工。

### IMPLEMENTED
代码已经写入，但还没有真实 Studio 操作证据。

> IMPLEMENTED **不等于完成**。

### AGENT_VERIFIED
Codex 已经使用 **Release 构建的真实 Studio 窗口**完成该项人工操作，并留下明确证据。

自动测试不能把状态从 IMPLEMENTED 升为 AGENT_VERIFIED。

### USER_ACCEPTED
用户实际使用 Release Candidate 后明确确认该项通过。

### BLOCKED
Codex 无法完成真实操作验证、无法复现、环境受限或仍有失败。

> BLOCKED 不是 PASS，不允许为了推进版本而自行忽略。

---

# 3. 0.3.1.2A 总 Gate 规则

这是本 PLAN 最重要的部分。

---

## GATE A0 — 基线锁定

开工前必须：

1. 记录基线 commit；
2. 新建 `codex/0.3.1.2A`；
3. 完整 Build；
4. 跑现有 Core / WPF tests；
5. 保存 0.3.1.1 的失败截图/复现场景；
6. 不得改写 0.3.1.1 的历史报告来伪造它已通过。

Gate 输出：

```text
PLAN/0.3.1.2A/BASELINE.md
```

---

## GATE A1 — 自动化只能证明“代码路径”

自动化测试可以证明：

- 方法返回值；
- 数据模型；
- command CanExecute；
- schema；
- cardinality；
- persisted IDs；
- collection event；
- undo transaction。

自动化测试**不能单独证明**：

- 按钮是否歪；
- 字体颜色是否可读；
- 鼠标是不是剪刀；
- 拖线看起来是不是“真实线”；
- Expander 动画是否顺滑；
- ghost 与落点是否一致；
- 白框是否消失；
- ComboBox 是否空白；
- Inspector 是否能正常操作；
- 用户是否能完成实际交互。

凡是视觉/交互项，只有自动测试：

> 最高只能写 `IMPLEMENTED`。

---

## GATE A2 — UI/交互必须使用真实 Release EXE 验收

必须使用本轮 Release/self-contained Studio EXE。

禁止用：

- ViewModel 单测；
- STA 测试；
- XAML 静态检查；
- Debug 单元宿主；
- 代码推理；

替代真实窗口操作。

每个 UI Gate 必须记录：

```text
测试步骤
实际结果
证据文件
状态
```

证据目录：

```text
PLAN/0.3.1.2A/evidence/
```

静态视觉可用 PNG。

动态交互优先用短 GIF / MP4 / 连续截图。

如果 Codex 环境无法提供可信动态证据：

> 标记 `BLOCKED / NEED_USER_VERIFICATION`，版本保持 NO-GO。

禁止写“测试环境受限，但代码看起来正确，所以 PASS”。

---

## GATE A3 — Codex 无权自行给版本 FINAL PASS

Codex最终只能输出：

```text
0.3.1.2A Release Candidate
```

不能输出：

```text
0.3.1.2A 完成
Studio 修复完成
全部验收通过
```

只有用户明确回复类似：

```text
0.3.1.2A 通过
```

之后才允许把总状态改成 `USER_ACCEPTED`。

---

## GATE A4 — 0.3.1.2B 不得提前施工

默认规则：

> **在用户明确接受 0.3.1.2A 前，不得开始 0.3.1.2B。**

若用户明确授权并行，则以用户新指令为准。

---

# 4. Work Package A1 — 新建故事空状态布局返工

对应 0.3.1.1 旧问题：新建故事按钮排版。

## 现象

0.3.1.1 实机中，“新建故事”主按钮仍然视觉偏斜，未达到“单主操作居中”的要求。

## 施工要求

不要先猜 XAML 已经 `HorizontalAlignment=Center` 就认为正确。

必须在 Release EXE 中实际测量/观察：

- 空状态容器实际宽度；
- 按钮实际中心；
- 文本区域实际中心；
- 是否受右侧隐藏区域、父级 Grid 列、Margin、DockPanel 影响。

最终要求：

```text
空项目主内容区域视觉中心
      │
      ▼
[ 空状态标题 ]
[ 空状态说明 ]
[ 新建故事 ]
```

按钮中心应与空状态内容组中心一致。

## Gate A1

至少：

- 1100×700；
- 1700×980；
- 100% DPI；
- 125% DPI。

四种实际窗口截图。

如果任意截图肉眼明显偏斜：

> FAIL。

---

# 5. Work Package A2 — Alt 白框必须真正消失

对应旧 U2 / 0311 审计白框。

## 现象

按 Alt 后仍会出现异常白色矩形/焦点框。

## 要求

禁止继续写：

> “自动化覆盖了 Left Alt 生命周期，所以视为已实现。”

必须在真实窗口：

1. Graph 获得焦点；
2. 按 Left Alt；
3. 松开；
4. 连续重复 10 次；
5. 在选中节点、空白画布、连接线附近分别测试。

记录：

- focus element；
- menu access key 状态；
- mouse capture；
- 白框是否出现。

## Gate A2

若无法复现用户问题：

```text
BLOCKED — NEED_USER_VERIFICATION
```

不能写 PASS。

若能复现，修复后必须保留修复前/修复后证据。

---

# 6. Work Package A3 — 资源库深色主题可读性

对应 0311 审计：资源文字变纯黑。

## 已知高可信根因

当前 Canonical 资源项 Button 使用自己的局部 `Button.Style`，但没有可靠继承主题样式；资源标题 TextBlock 也没有明确绑定主题 Foreground。

## 施工要求

- 资源标题使用 `TextFillColorPrimaryBrush` 或等价主题前景；
- 次要身份文本使用 `TextFillColorSecondaryBrush`；
- selected / hover 状态仍保持足够对比度；
- Dark / Light 均可读；
- 不在 XAML 中写死黑色；
- 不写一套只服务 Canonical resource 的独立主题系统。

## Gate A3

真实 Release EXE：

- Dark；
- Light；
- selected；
- hover；
- disabled/missing（若有）。

全部截图。

任何正常资源标题与背景对比不足：

> FAIL。

---

# 7. Work Package A4 — 资源右键必须命中资源，不得被 Folder 截获

对应旧右键菜单问题。

## 已知根因

父级 `Expander` 与资源 Button 都在 `PreviewMouseRightButtonDown` 上处理事件。

Preview 是 tunneling，父级可能先处理并 `Handled=true`，导致用户右键“新任务”时出现的是“任务文件夹”菜单。

## 修复原则

### Folder Menu

只有右键 **Folder Header** 本身时打开。

### Resource Menu

右键资源项时必须由资源项自己处理。

A 版至少要求：

```text
编辑
删除 / 解除引用
```

“重命名”属于 0.3.1.2B，不在本版本添加。

## Gate A4

四个文件夹各建至少 1 个资源：

- 角色；
- 物品；
- 会话；
- 任务。

逐个右键。

必须看到资源菜单，而不是 folder 菜单。

---

# 8. Work Package A5 — 节点参数必须是真正可编辑，不是只读摘要

这是 0.3.1.1 最严重的语义偷换之一。

## 当前错误实现

当前节点：

```text
Expander Header="参数"
└─ TextBlock ParameterSummary
```

只是只读字符串。

这不符合：

> “不用点节点、不用去右侧 Inspector，也能直接在节点上编辑高频参数”。

## 施工要求

节点内部必须明确分三类交互区域：

```text
[Header Drag Zone]
[Port Zone]
[Parameter Interactive Zone]
```

### Header Drag Zone

负责拖动节点。

### Port Zone

负责连线。

### Parameter Interactive Zone

控件必须正常获得：

- mouse click；
- keyboard focus；
- text input；
- ComboBox open；
- Expander toggle。

点击参数控件不能开始拖节点。

## 最低需要真正可编辑的节点

### 【开始】

节点内至少能编辑/操作：

- 启动方式类型；
- 对应主要目标（角色/逻辑）；
- 区域基础参数；
- 启动方式增删的高频入口。

不要求把所有高级字段都塞进去。

### Task【目标】

节点内：

- 目标类型；
- 目标对象；
- 数量。

### 【动作】

节点内：

- 动作类型；
- 目标物品（GiveItem）；
- 数量 / 主要值。

### Session【台词】

节点内：

- 说话角色；
- 台词文本或可编辑文本入口。

## Expander

参数区域：

- 可以收起；
- 可以展开；
- 收起后真实减少节点高度；
- 再展开恢复控件；
- 不能只是箭头变化。

## Gate A5

真实窗口逐项：

1. 不选中节点；
2. 直接点击节点内部 TextBox；
3. 修改值；
4. 改 ComboBox；
5. 收起；
6. 展开；
7. 移动节点；
8. 保存、重开；
9. 数据保持。

如果任何操作仍必须先去 Inspector：

> FAIL。

---

# 9. Work Package A6 — Inspector 主题和 ComboBox 空白问题

对应旧 #23 / 当前审计 #7。

## 要求

Inspector 中：

- TextBox；
- ComboBox；
- ItemsControl；
- +/- 按钮；
- selected item；
- dropdown popup；

必须使用当前 Studio 主题。

特别是 Objective Type：

- 下拉打开后必须看得到：
  - 击杀实体
  - 收集物品
  - 交互角色
- 不能只显示白色空白大框；
- Dark / Light 都正常。

## Gate A6

至少真实测试：

- Start；
- Objective；
- Action；
- Line；
- Choice；
- Settlement。

每类打开 Inspector。

任何下拉弹出为空白：

> FAIL。

---

# 10. Work Package A7 — 固定节点必须从“添加节点”菜单源消失

## 当前错误

`ForAuthoringScope()` 仍只过滤 `CompatibilityOnly`。

然后 View 用 `CanAuthorNodeType()` 把固定节点变成 disabled。

这不符合原要求：

> 不能由用户放置的节点根本不应出现在菜单。

## 正确规则

新 authoring palette 不出现：

### Story Flow
- 【开始】

### Session
- 【起始】

### Task
- 【结算】

同时：

- compatibility-only 节点不出现；
- Task【激活】不出现；
- 【选择】正常出现并可创建。

## 实现建议

优先修：

```text
GraphNodeDefinitionRegistry.ForAuthoringScope()
```

或等价的 canonical authoring source。

不得只在 XAML 按名字 `Collapsed`。

## Gate A7

真实右键添加节点菜单截图：

- Story；
- Session；
- Task。

固定节点不存在，而不是灰色。

---

# 11. Work Package A8 — 剪刀模式必须真的显示“剪刀鼠标”

## 当前错误

代码使用：

```text
Cursors.Cross
```

这不是剪刀。

## 要求

加入真正的剪刀 cursor：

- 推荐一个轻量 `.cur` 资源；
- 或等价的自定义 Cursor。

要求：

### 点击剪刀按钮
鼠标变剪刀。

### 按 Left Alt
临时变剪刀。

### 松开 Left Alt
恢复之前 cursor。

### 离开 Graph
不污染其它窗口/菜单 cursor。

## Gate A8

证据必须直接看到剪刀 cursor。

若截图系统无法捕获 cursor：

> 使用短视频/GIF；否则标记 NEED_USER_VERIFICATION。

不能用“代码加载了 scissors.cur”替代视觉验收。

---

# 12. Work Package A9 — 连线拖动必须脱离“预览线”思维

## 当前错误实现

0.3.1.1 把：

```text
虚线 draft wire
```

改成了：

```text
白色实线 draft wire
```

但仍然是独立 `_draftWire / _draftWires`。

用户看到的仍是“预览线”。

## 最终交互语义

### 新建连接

技术上可以有 uncommitted transaction，但视觉必须使用 **正式 Wire Renderer 同一套样式**：

- Flow 使用正式 Flow 颜色/粗细；
- Logic 使用正式 Logic 颜色/粗细；
- 不允许白色通用预览线；
- 不允许虚线；
- 不出现和正式 wire 明显不同的 preview look。

### 重接已有单线

必须直接拖动当前已存在的 connection visual：

```text
A ───── B
```

拖 A：

- B 端保持固定；
- 原来的那根线实时跟随；
- 不叠加第二条 draft line。

取消：

- 恢复原 geometry。

成功：

- commit endpoint。

### 多线端点

Flow Input / Logic Output 多 incident wire 时：

- 直接移动当前那些真实 wire visuals；
- 不新画一束白色 draft wires。

## Gate A9

真实窗口录屏/连续截图必须证明：

- 拖之前只有 1 条；
- 拖动中仍是那 1 条，不出现第二条 preview；
- cancel 后恢复；
- reconnect 后只有最终连接。

多线同理。

仅检查 `StrokeDashArray == null`：

> 不算通过。

---

# 13. Work Package A10 — Dirty State 不得阻塞普通 Story authoring

这是 0311 旧“未保存就不让动”的真正收口。

## 当前明确问题

当前仍有：

```text
CanMutateCanonicalStoryResources()
```

只要 Story 有任意 dirty editor，就禁止：

- 创建资源；
- 引用资源；
- 删除资源。

这与 Studio 的正常工作方式冲突。

## 本版本必须确立的 invariant

> **Dirty 只表示“尚未写盘”，不是“编辑器锁定”。**

在同一当前项目/Story 中，即使 Graph dirty，也必须允许：

- 创建角色；
- 创建物品；
- 创建会话；
- 创建任务；
- 引用资源；
- 在 Story / Session / Task 之间导航；
- 继续编辑其它资源；
- 普通资源删除（若删除本身与 dirty 数据有真实引用冲突，则按引用规则处理，而不是因为“dirty”一刀切）。

真正需要 dirty boundary 的情况：

- 关闭 Studio；
- 关闭/切换项目；
- 明确丢弃当前未保存状态；
- 导出要求磁盘一致快照；
- migration / destructive whole-project operation。

## 实现约束

不要做全局 AutoSave Framework。

优先：

- 保持内存 editor；
- 去掉无关 `HasDirtyEditors` gate；
- 需要引用检查时直接检查引用；
- Save 仍由用户控制。

## Gate A10

按顺序：

1. 打开 Story；
2. 修改 Story Flow，不保存；
3. 创建角色；
4. 创建物品；
5. 创建 Session；
6. 创建 Task；
7. 切 Session；
8. 再切 Task；
9. 返回 Story；
10. 原 dirty 编辑仍存在。

全程不能出现：

```text
请先保存，再创建...
请逐个保存后再...
```

若出现：

> FAIL。

---

# 14. Work Package A11 — 资源库展开/收起动画不能“先瞬移再动画”

## 现象

用户观察：

### 收起
下面 folder 不随上方同步上移，而是等动画结束后突然跳上去。

### 展开
下面 folder 先瞬移到最终位置，再看上方慢慢展开。

## 要求

先定位真正动画来源。

如果当前 Expander/容器并没有真正布局动画，而是某种 content transition：

- 宁可取消动画，做干净的即时折叠；
- 也不要保留“看起来卡顿”的伪动画。

可接受两种结果：

### 方案 1
完全即时、无动画，但没有跳变错觉。

### 方案 2
真正 layout-aware 展开，下面 folder 与高度变化同步移动。

本版本不要求花哨动画。

## Gate A11

动态问题必须真实操作。

连续收起/展开 10 次。

无法用静态截图证明。

没有可信动态证据：

> NEED_USER_VERIFICATION / NO-GO。

---

# 15. Work Package A12 — Resource Ghost 与最终落点必须一致

## 当前明确根因

ghost：

```text
X = mouse.X - Width/2
Y = mouse.Y - 38
```

正式 drop：

```text
node.X = mouseGraph.X
node.Y = mouseGraph.Y
```

二者 anchor 不一致。

## 规则

统一一个 drag anchor：

例如：

```text
dragAnchor = 鼠标在 ghost 节点内部的相对位置
```

Drop 时：

```text
finalTopLeft = mouseGraph - dragAnchorGraphOffset
```

要求：

- ghost 中鼠标在哪；
- 放下后鼠标相对节点的位置就在哪；
- zoom 50% / 100% / 150% 仍一致；
- pan 后仍一致。

## Gate A12

至少：

- 50% zoom；
- 100%；
- 150%；
- 有 pan；
- 无 pan。

每次记录 ghost 与 drop 前后。

明显跳位：

> FAIL。

---

# 16. Work Package A13 — Objective 必须真正切到 DGR 身份资源

## 当前错误

即使 Inspector 已有角色/物品 ComboBox，Core 新 Objective 仍默认：

```text
kill_entity -> minecraft:slime
collect_item -> minecraft:stone
```

这导致新节点一出生就是“未解析 DGR 资源”。

## 要求

### 新建击杀 Objective

不能写 `minecraft:slime` 作为正常默认值。

目标应为：

- 未选择；
- 或从当前 Story 的 DGR 角色资源选择一个合法默认项。

合法角色目标：

- Individual NPC_ID；
- Collective Group_ID；

具体 runtime compatibility 保持 0.3.1.0 既定身份契约。

### 新建收集 Objective

目标为：

- DGR Item_ID；
- 或 Item Group_ID。

不得默认 `minecraft:stone`。

### 交互 Objective

使用 DGR 角色资源。

## 兼容旧数据

旧 `minecraft:*`：

- 可以 load；
- UI 显示“旧目标未解析 / 需要迁移”；
- 不得作为新 authoring 默认值。

## Gate A13

从空项目：

1. 新建角色 `NPC_ID=tavern_boss`；
2. 新建角色组 `Group_ID=slimes`；
3. 新建 Item；
4. 新建 Item Group；
5. 创建 3 个 Objective；
6. 全程普通 UI 不输入 `minecraft:*`；
7. 保存 JSON 后检查目标字段不再是默认 `minecraft:slime/stone`。

---

# 17. Work Package A14 — Start 默认“进入区域”在有替代 trigger 后必须能删除

## 已知 Core

Core `RemoveStoryStartTrigger()` 已经只检查：

```text
Count <= 1
```

所以“第一条永久不可删”不是 Core 规则。

问题高度可能在 UI command state / refresh。

## 要求

- 只有 1 条时删除禁用；
- 新增第 2 条后：
  - 第 1 条删除立刻可用；
  - 第 2 条删除也可用；
- 删除任意一条后：
  - 剩 1 条；
  - 最后那条删除再次禁用。

### 重点

`RemoveCommand.CanExecute` 依赖 `StoryStartTriggers.Count`。

每次 Add/Remove/Refresh 后必须显式更新所有 trigger command state。

## Gate A14

真实操作：

```text
初始：进入区域
+ 角色交互
删除：进入区域
```

必须成功。

然后：

```text
只剩角色交互
删除按钮不可用
```

---

# 18. Work Package A15 — 0311 已声称修好的项目全部重新验收

因为 0.3.1.1 的“自动化已验证”已经被证明不可靠，所以以下 0311 既有项目不能直接继承 PASS。

必须在 A 版重新做 regression acceptance：

- 节点右键【编辑】【删除】；
- 聚合 Session / Task 双击进入局部图；
- 删除节点确认 + incident wires 一起删除；
- 资源 selected state；
- 【物品】右键不再 fallback 成角色；
- 角色创建 individual / collective；
- Session【起始】新建时无旧 ◆ Logic Out；
- 【选择】可正常创建；
- Settlement 连续 `结果 1/2/3...`；
- EnterStory 不在新 Start 选项；
- GiveItem 只能 Individual Item_ID；
- port anchor 不因文字长度漂移；
- 基数规则：
  - ●输出 1 / 输入多；
  - ◆输出多 / 输入 1；
- 中文端口；
- 普通 UI 不展示 Graph Node GUID；
- 保存重启后 stable IDs/lines 不漂移。

任何一个失败：

> A 版 NO-GO。

---

# 19. 0.3.1.2A 自动化测试最低要求

自动化不是最终 Gate，但仍必须做。

至少新增：

### Palette
- fixed node 不在 `AuthoringDefinitions`；
- 不是只 `CanAuthor=false`。

### Dirty invariant
- dirty Story editor 时 CreateActor/CreateItem/CreateSession/CreateTask command 仍可执行；
- graph navigation 仍可执行。

### Objective
- 新 Objective 不默认 `minecraft:*`；
- DGR actor/item identity 可保存。

### Trigger
- 2 triggers 时第一个 RemoveCommand CanExecute 立即变 true；
- 删除后剩 1 条时 false。

### Ghost math
- preview anchor 与 final placement 计算使用同一 offset。

### Wire
- reconnect existing wire 不创建第二个 committed visual；
- cancel 保持原 connection；
- multi incident transaction 原子。

---

# 20. 0.3.1.2A Agent Manual Acceptance

Codex 必须真实运行 Release EXE，逐条填写：

```text
PLAN/0.3.1.2A/MANUAL_ACCEPTANCE.md
```

格式：

```text
Gate ID:
Build SHA:
EXE:
Steps:
Expected:
Actual:
Evidence:
Status: AGENT_VERIFIED / BLOCKED
```

严禁“根据代码推断 Actual”。

---

# 21. 0.3.1.2A Exit Gate

Codex想提交 RC，必须同时满足：

- [ ] Build PASS
- [ ] Core tests PASS
- [ ] WPF tests PASS
- [ ] A1-A15 全部至少 IMPLEMENTED
- [ ] 所有可由 Codex真实操作的 UI 项达到 AGENT_VERIFIED
- [ ] 无任何已知 FAIL 被写成 PASS
- [ ] 无任何 `PENDING` 被写成“完成”
- [ ] 无任何“自动化替代人工 UI”声明
- [ ] Release EXE 已生成
- [ ] 证据目录已提交
- [ ] 未施工 B 版内容
- [ ] GitHub 已 push

如果存在 Codex 无法验证的动态 UI 项：

最终状态必须写：

```text
0.3.1.2A RC — NEED_USER_VERIFICATION
```

不能写“完成”。

---

# 22. 用户最终 Gate

用户拿到 RC 后人工验收。

只有用户明确说：

```text
0.3.1.2A 通过
```

才允许：

1. 把 A 标为 `USER_ACCEPTED`；
2. 固定 A 的 commit SHA；
3. 以该 SHA 作为 0.3.1.2B 基线；
4. 开始 B 版施工。

如果用户继续发现 A 范围失败：

> 继续修 A，不得把失败偷偷挪到 B。

---

# 23. GitHub 交付要求

Codex 必须：

1. commit 到 `codex/0.3.1.2A`；
2. push GitHub；
3. 提供最终 RC commit SHA；
4. 提供：
   - Development Report
   - Manual Acceptance
   - Evidence
   - 自动测试结果
5. 不自动 merge `main`；
6. 不自动 tag；
7. 不自动创建 Release，除非用户明确要求；
8. 最终回复必须明确写：
   - `RC` 还是 `USER_ACCEPTED`
   - 还有哪些 `BLOCKED / NEED_USER_VERIFICATION`
   - GitHub push 状态。

---

# 24. 一句话验收

> **0.3.1.2A 不是“再写一轮相关代码”，而是把 0.3.1.1 已承诺却没有真正兑现的 Studio 行为逐项做实；凡是需要真实窗口才能判断的需求，没有真实窗口证据就不允许自称完成。**
