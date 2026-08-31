# DarkGrey_RPG 0.3.1.2B — Studio 新增交互修缮施工 PLAN

> **版本定位**：在 0.3.1.2A 用户验收通过之后，处理新一轮人工使用中提出的新增 Studio 需求  
> **前置条件**：0.3.1.2A 必须已经由用户明确 `USER_ACCEPTED`  
> **建议施工分支**：`codex/0.3.1.2B`  
> **基线**：用户接受的 0.3.1.2A commit SHA，而不是旧 0.3.1.1 HEAD
>
> **重要**：B 版不能吸收 A 版未修好的旧问题。  
> 若 B 开工后发现 A 的旧问题仍存在，应立即停止 B，把问题退回 A 修复。

---

# 0. B 版范围

B 版只处理用户在 0.3.1.1 后续审计中新提出、此前 0311 PLAN 没有明确锁死的 Studio 行为：

1. 项目级 breadcrumb；
2. 资源右键【重命名】；
3. 区域参数布局重排；
4. 【条件】更名为【条件判断】；
5. 资源库紧凑一行式排版；
6. 端口只有圆点/菱形本体才是拖线 hit target；
7. 动态端口在节点上实时刷新；
8. 资源库拖动排序并持久化。

B 版不是新的大功能版本，不得扩展到 Minecraft。

---

# 1. B 版同样使用严格 Gate

与 A 版相同：

```text
NOT_STARTED
IMPLEMENTED
AGENT_VERIFIED
USER_ACCEPTED
BLOCKED
```

规则：

- 自动测试只能把功能推进到 IMPLEMENTED；
- 真实 UI/交互必须 Release EXE 操作；
- Codex 最终只能提交 `0.3.1.2B Release Candidate`；
- 只有用户明确接受后才能标 `USER_ACCEPTED`；
- 不允许再出现“人工验收 PENDING，但版本已完成”。

证据目录：

```text
PLAN/0.3.1.2B/evidence/
```

人工记录：

```text
PLAN/0.3.1.2B/MANUAL_ACCEPTANCE.md
```

---

# 2. Work Package B1 — 项目级 Breadcrumb

## 用户目标

进入某个 Story 后，当前顶部：

```text
<故事名>
```

改为：

```text
<项目名> > <故事名>
```

进入 Session：

```text
<项目名> > <故事名> > <会话名>
```

进入 Task：

```text
<项目名> > <故事名> > <任务名>
```

## 行为

### 点击项目名

返回：

```text
Project Home
```

即能看到：

- 全部故事；
- 故事图谱入口；
- 项目级内容。

### 点击故事名

无论当前 Session / Task：

返回 Story Flow。

### 点击当前项

无操作或保持当前视图。

## 实现约束

不要建立通用 Navigation Framework。

推荐最小实现：

- Shell 在 Configure Canonical Workspace 时注入：
  - ProjectDisplayName
  - ReturnToProject callback/command
- Canonical Workspace 继续管理 Story / Session / Task breadcrumb。

## Dirty 行为

点击 breadcrumb：

- 不得因为当前 Graph dirty 而阻塞；
- 保留内存编辑状态；
- 返回后仍可继续。

## Gate B1

真实操作：

```text
Project
→ Story
→ Session
→ Project
→ 再开同 Story
```

breadcrumb 全程正确。

---

# 3. Work Package B2 — 资源右键新增【重命名】

## 菜单

对：

- 角色；
- 物品；
- 会话；
- 任务；

右键资源至少：

```text
编辑
重命名
删除 / 解除引用
```

新建/引用可以继续保留，但必须与资源自身操作视觉分组。

## “重命名”的定义

本版本的普通【重命名】只改：

```text
Display Name
```

不改稳定身份。

### 角色
不改：

- NPC_ID
- Group_ID

### 物品
不改：

- Item_ID
- Group_ID

### Session / Task
不改：

- resource stable ID
- file identity
- aggregate resource_id
- dynamic port IDs

这样重命名不会断引用。

## Gate B2

每种资源：

1. 右键；
2. 重命名；
3. 资源库立即更新；
4. aggregate node / Inspector 需要显示名的地方同步；
5. 保存；
6. 重启；
7. ID 不变。

---

# 4. Work Package B3 — 区域触发参数布局

## 用户要求

当前错误：

```text
维度 | X | Y
Z    | 半径
```

改为明确分组：

```text
维度
[________]

坐标
X [____]  Y [____]  Z [____]

半径
[________]
```

或在空间足够时：

```text
维度 [____]

X [____]  Y [____]  Z [____]

半径 [____]
```

核心要求：

> X / Y / Z 必须同排。

不要把“维度”和 X/Y 混成一组。

## 节点内参数区

如果 Start 节点内也直接编辑区域参数，节点内同样遵守：

```text
XYZ 同组
```

## Gate B3

- 右侧 Inspector；
- 节点内参数区；

都截图。

1100×700 / 125% DPI 下仍不挤坏。

---

# 5. Work Package B4 — 【条件】正式更名【条件判断】

## 规则

用户可见名称：

```text
【条件判断】
```

替代：

```text
【条件】
```

范围：

- Story Flow；
- Session。

Task 内没有此 flow node，不新增。

## 重要

只改 author-facing display name。

内部 stable node type 继续：

```text
condition
```

不得做数据迁移。

保存旧项目后不改变 type。

## Gate B4

- Palette；
- 节点标题；
- Problems 中文描述；
- 搜索/菜单；

都显示【条件判断】。

---

# 6. Work Package B5 — 资源库紧凑一行式排版

## 当前问题

资源项占用两行，Session / Task 甚至为了一个空 IdentityText 预留第二行。

## 最终排版

### Individual 角色

```text
酒馆老板        NPC_ID: tavern_boss
```

### Collective 角色

```text
史莱姆          Group_ID: slimes
```

### Individual 物品

```text
铜币            Item_ID: copper_coin
```

### Collective 物品

```text
剑              Group_ID: swords
```

### Session

```text
接受委托
```

只占一行。

### Task

```text
清理史莱姆
```

只占一行。

## 术语锁定

角色个体身份必须写：

```text
NPC_ID
```

不要再写：

```text
角色 ID
NPC ID
Actor ID
```

本版本遵循用户指定的专有词 `NPC_ID`。

同理推荐：

```text
Group_ID
Item_ID
```

## 行高

目标：

- 可读；
- 紧凑；
- 不用每个资源 50~60px 高。

具体 px 可以根据 DPI 自适应，不锁死单值。

## Gate B5

每个 folder 至少 10 个资源。

必须：

- 一屏能显示明显更多项目；
- Session / Task 没有空白第二行；
- 角色/物品 name + ID 同行。

---

# 7. Work Package B6 — 只有端口本体可以开始拖线

## 当前根因

`FlowPortControl` 整个是 Button：

```text
[圆点 + “流程输入”文字]
```

Graph Editor 找祖先 `FlowPortControl`，因此点文字也等于点端口。

## 最终规则

只允许：

```text
●
◆
```

本体区域开始 wire drag。

点击：

```text
流程输入
流程输出
逻辑输入
结果名称
```

文字：

- 不开始 wire；
- 可用于普通 hover / tooltip；
- 将来可以文本选择或其它交互，但本版本无需实现。

## 推荐实现

把：

```text
FlowPortControl = Button(container)
```

拆成：

```text
Port Row
├─ Anchor hit element
└─ Label non-wire element
```

wire gesture 只绑定 Anchor。

仍然保持：

- anchor 固定位置；
- label 不影响 anchor。

不要再用 ancestor-of-label 判定 wire start。

## Gate B6

真实测试：

- 精确点圆点 → 能拖线；
- 精确点菱形 → 能拖线；
- 点“流程输入”文字 20 次 → 一次都不能开始拖线；
- 点长结果名文字 → 不拖线。

---

# 8. Work Package B7 — 动态端口必须实时刷新节点

## 当前问题

在 Inspector 修改 Task【结算】的动态结果槽：

- Inspector 数据变化；
- Graph 数据变化；
- 但当前节点 visual 不立即重建 ports；
- 需要离开 Task 再回来才能看到。

## 已知结构问题

`CanonicalGraphNodeControl.RebuildPorts()` 主要在 Node 对象替换时执行。

而 HostGraphChanged 当前只：

```text
IndexPorts()
RedrawConnections()
```

如果 NodeControl 内部 ports 没重新构建，IndexPorts 当然索引不到新的 visual。

## 最终要求

以下操作必须**当前帧/当前交互后立即可见**：

### Settlement
- Add result；
- Remove result；
- Rename result；
- Reorder result。

### Choice
- Add option；
- Remove option；
- Rename option；
- Reorder option。

### Start
- Add trigger；
- Remove trigger；
- Rename trigger；
- Reorder trigger；
- 条件 Logic port 增减。

### Logic AND / OR
若支持动态输入，同样实时。

## 实现原则

不要 `RebuildGraph()`。

只重建受影响的：

```text
CanonicalGraphNodeControl ports
```

然后：

```text
IndexPorts(affected node)
RedrawIncidentConnections(affected node)
```

保留：

- node instance；
- node position；
- selection；
- viewport；
- focus。

## Gate B7

在同一 Task 画面：

1. 选中【结算】；
2. + 结果；
3. 不离开 Task；
4. 新 port 立即出现；
5. rename；
6. 节点文字立即更新；
7. reorder；
8. port 顺序立即更新；
9. delete；
10. port 立即消失。

任何一步需要“退出再回来”：

> FAIL。

---

# 9. Work Package B8 — 资源库拖动排序并持久化

## 用户目标

资源库不是固定按名称排序。

用户可以：

```text
拖动资源
```

改变同一 folder 内的上下顺序。

## 范围

只允许：

- 角色 folder 内排序；
- 物品 folder 内排序；
- 会话 folder 内排序；
- 任务 folder 内排序。

本版本不做跨 folder 拖动改变资源类型。

## 持久化

Codex开工前先检查现有 Story Membership 数据：

- owned/referenced arrays 是否已经有稳定顺序；
- loader 是否保留 array order；
- workspace 是否只是额外 `OrderBy(DisplayName)` 抹掉顺序。

### 优先方案

若 membership 数组本身有顺序：

> 直接把数组顺序作为 author order。

删除 workspace 的强制 alphabetical sort。

拖动后更新 membership array。

### 禁止

不要先发明：

```text
sort_order
layout.json
ResourceOrderingService
```

如果现有 membership 无法无损保存顺序，先报告，再做最小 schema 方案。

## Owned / Referenced

排序只表示当前 Story 资源库中的展示顺序。

不改变：

- ownership；
- reference；
- resource home story；
- resource ID。

## Gate B8

每个 folder 创建 5 个资源。

拖成明显逆序。

然后：

1. 保存；
2. 关闭 Studio；
3. 重开；
4. 顺序完全保持。

再：

- 删除中间一个；
- 新建一个；
- 顺序不被重新 alphabetically reset。

---

# 10. B 版自动化测试

至少：

## Breadcrumb
- project crumb injected；
- project click routes ProjectHome；
- dirty graph 不阻塞。

## Rename
- display name change；
- stable IDs 全不变；
- aggregate mapping 不断。

## Condition
- type 仍 `condition`；
- display name 变【条件判断】。

## Resource Row
- Session/Task IdentityText 不产生第二行数据；
- actor/item identity exact label。

## Port Hit Target
- label click 不触发 BeginWire；
- anchor click 触发。

## Dynamic Ports
- changed node raises targeted port rebuild event；
- no full graph reset；
- selected node instance保持。

## Ordering
- reorder round-trip persistence；
- reload order same；
- ownership unchanged。

---

# 11. B 版人工 Gate

Codex真实 Release EXE 至少完成：

### B-S1 Breadcrumb
Project → Story → Session → Project。

### B-S2 Rename
四类资源各一次。

### B-S3 Region Layout
Inspector + node inline。

### B-S4 条件判断
Story / Session palette 与节点。

### B-S5 Compact Resource
每 folder 10 items。

### B-S6 Port Hit
anchor vs label。

### B-S7 Dynamic Port
Settlement / Choice / Start。

### B-S8 Ordering
drag + save + restart。

证据必须写入：

```text
PLAN/0.3.1.2B/MANUAL_ACCEPTANCE.md
PLAN/0.3.1.2B/evidence/
```

---

# 12. B 版 Exit Gate

Codex只能提交：

```text
0.3.1.2B Release Candidate
```

必须：

- [ ] 基线是用户接受的 A commit
- [ ] Build PASS
- [ ] Core tests PASS
- [ ] WPF tests PASS
- [ ] B1-B8 IMPLEMENTED
- [ ] 可验证 UI 项 AGENT_VERIFIED
- [ ] 无已知 FAIL 写成 PASS
- [ ] 无 `PENDING` 被当“完成”
- [ ] Release EXE 生成
- [ ] evidence 提交
- [ ] GitHub push

Codex不得写：

```text
0.3.1.2B 已完成
Studio 已冻结
Studio 最终可用
```

只有用户验收后才能 `USER_ACCEPTED`。

---

# 13. 用户验收与后续

用户验收 B 后：

### 通过
继续 0.3.1.x 下一轮 Studio 审计，或由用户决定是否已经接近冻结。

### 不通过
继续修 0.3.1.2B，或开 0.3.1.3。

**仍然不自动冻结 Studio。**

只有用户明确宣布：

```text
冻结 Studio
```

之后才进入 0.3.2.0 Minecraft 对接 / 游戏内修缮阶段。

---

# 14. GitHub 要求

Codex必须：

1. 创建 `codex/0.3.1.2B`；
2. 基于 USER_ACCEPTED 的 A commit；
3. 完整 commit；
4. push GitHub；
5. 提供 RC SHA；
6. 上传 Development Report / Manual Acceptance / Evidence；
7. 不自动 merge main；
8. 不自动 tag/release；
9. 最终明确说明：
   - AGENT_VERIFIED 项；
   - BLOCKED 项；
   - NEED_USER_VERIFICATION 项。

---

# 15. 一句话验收

> **0.3.1.2B 只新增这一轮人工反馈中此前没有锁死的 Studio 交互：项目级 breadcrumb、资源重命名与排序、紧凑资源行、XYZ 布局、【条件判断】命名、端口精确 hit target 和动态端口实时刷新；同样必须通过真实 Release Studio 的交互 Gate，禁止自动测试代替人工验收。**
