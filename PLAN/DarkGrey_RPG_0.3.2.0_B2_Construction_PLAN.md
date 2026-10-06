# DarkGrey RPG 0.3.2.0_B2 Construction PLAN

> **阶段定位：B1 中断后的纠偏继续施工**
>
> 本 PLAN 不重新解释 DGR 的完整产品模型。施工前必须先阅读同目录/仓库中的长期认知文档：
>
> `DGR_WORKFLOW_AND_CONCEPTS.md`
>
> 本 PLAN 只回答：**基于当前 B-pre-correction WIP，哪些保留、哪些修、哪些重新验证，以及怎样完成 B2。**

---

# 0. 基线与施工入口

## 0.1 Frozen Studio baseline

```text
branch:
codex/0.3.2.0_A

HEAD:
782a78d28ae49efd731ff61e8cb116bebf870dd1
```

A 仍是 Studio frozen baseline。

B2 不重新开放 Studio authoring/UI。

---

## 0.2 当前 WIP checkpoint

```text
branch:
codex/0.3.2.0_B-pre-correction

HEAD:
faa34b81ea527069fe00cf00731857d48036d291
```

该 commit 是 A baseline 的直接后继，仅包含一轮 B WIP 集成施工。

**不要 reset 回 A。**

B1 中已经存在多项方向正确的实现，B2 应从该 checkpoint 继续纠偏。

推荐新分支：

```text
codex/0.3.2.0_B2
```

---

# 1. B2 的核心目标

B2 不是重新设计 DGR。

B2 的任务是：

```text
正确理解 DGR 工作流
        ↓
审查并保留 B1 正确实现
        ↓
纠正错误测试模型
        ↓
补齐少量真实 Integration 缺口
        ↓
重新做可证明 Canonical Identity 的 Minecraft Vertical Slice
        ↓
恢复完整 Story → Session → Task → Settlement 链路
```

B2 最重要的纠偏原则：

> **不得为了让 Runtime 测试更容易通过，而反向修改已经完成的作者定义。**

Studio 定义什么，Runtime 就消费什么。

---

# 2. 施工前理解确认

正式改代码前，Codex 必须：

1. 阅读 `DGR_WORKFLOW_AND_CONCEPTS.md`；
2. 阅读 `PLAN/0.3.2.0_B_WIP_INTERRUPTION_REPORT.md`；
3. 检查当前真实 `project_test` / frozen-A 导出的 DGRS；
4. 检查当前 B-pre-correction 的 Nominator、Identity Resolver、Forge Event Normalizer、Canonical Task Runtime；
5. **在施工会话里先用自己的话做一次简短复述。**

不要求再创建第三份 md。

复述控制在约 500 字以内，至少说明本次真实链路：

```text
Studio 已有 Resource
→ DGRS
→ Runtime load
→ Nominator bind Minecraft Host
→ DGR Identity Resolution
→ real Gameplay Event
→ Canonical Runtime
```

并明确说出当前 fixture 中：

```text
“史莱姆”
= Collective Actor
= Group_ID: slimes
```

以及：

```text
Task “消灭3只史莱姆”
target = slimes
```

Codex 必须明确理解：

```text
未指名的原版史莱姆
≠ 自动拥有 slimes

被指名到 slimes 的任意 Minecraft Entity
= DGR 视角下属于该 Group
```

如果复述中把以下任意概念混淆：

```text
Group_ID = minecraft mob type
NPC_ID = Group_ID
Item_ID = Minecraft registry ID
Nominator = Resource Editor
Story = StoryInstance
Resource = Placement
```

则停止施工，先纠正理解。

---

# 3. 施工边界

## 3.1 B2 默认禁止修改 Studio

默认禁止：

```text
studio/**
```

包括：

- Graph authoring；
- Actor / Actor Group authoring；
- Item / Item Group authoring；
- Story Flow；
- Session Graph；
- Task Graph；
- Inspector；
- Workspace；
- Studio UI；
- frozen schema。

如果 Java Runtime 无法消费 frozen-A 导出的合法 DGRS：

```text
先定位 Runtime / Reader / Integration
```

不能先修改 Studio。

只有证明 frozen exporter/schema 本身存在真实兼容缺陷时，才停止并报告，等待用户决定。

---

## 3.2 `project_test` 是只读验收项目

当前用户测试项目已经具有完整任务线。

B2 施工期间不得为了验收方便：

- 新建 Actor；
- 新建 Actor Group；
- 新建 Item；
- 新建 Item Group；
- 新建 NPC_ID；
- 修改 Group_ID；
- 修改 Item_ID；
- 修改 Story；
- 修改 Session；
- 修改 Task；
- 修改 Objective target；
- 修改 Story membership；
- 重编 Story Flow。

尤其禁止再次创建：

```text
NPC_ID = slime
```

因为当前需求已经由：

```text
Group_ID = slimes
```

表达。

如果现有 fixture 与 PLAN 不一致，先报告，不自行修作者数据。

---

# 4. B1 WIP 总体判断

A → B-pre-correction 当前 diff 约涉及：

```text
50 个施工文件
```

但不应把它们视为同一风险级别。

B2 按以下四类处理：

```text
KEEP
FIX
REVIEW
REVERIFY
```

---

# 5. KEEP — 默认保留的 B1 实现

以下工作方向与 DGR 预期工作流一致，不因 B1 的测试误解而回退。

---

## 5.1 DGRS Direct Read

保留当前：

```text
.dgrs
→ ZipFile
→ validated archive entries
→ byte[]
→ ProjectSnapshot
```

不得回退：

```text
.dgrs
→ 整包解压到临时目录
→ 再当 project directory 读取
```

当前核心文件：

```text
DgrsArchiveReader
StoryPackageSnapshotReader
LoadedStoryPackage
StoryPackageLoader
StoryPackageSnapshotMerger
```

目标：

- 正常读取不创建 `.dgrs-runtime`；
- `ZipFile` load 完成后关闭；
- Runtime snapshot 不依赖 archive 保持打开；
- merger 不依赖物理解压目录。

---

## 5.2 Detached bytes loader

保留给现有严格 parser 增加 `byte[]` / memory entry 入口的方向。

不要另写第二套宽松 parser。

优先复用：

```text
ProjectRepository
CanonicalGraphResourceLoader
CanonicalProjectContentLoader
CanonicalStoryMembershipLoader
CanonicalStoryLogicGraphLoader
StoryLoader
```

现有语义验证规则应尽量共用。

---

## 5.3 Package-aware Nominator Catalog

保留：

```text
loaded Story Package
→ Story
→ package resource closure
→ Actor / Actor Group
→ Item / Item Group
```

Nominator 正常 UI 不应要求用户记忆完整 ID。

用户应能够从已加载 package 中选择资源。

---

## 5.4 Canonical Story loaded-state

保留：

```text
ProjectSnapshot.containsStory(id)
```

其含义必须是：

```text
legacy Story exists
OR
canonical Story exists
```

不要再把：

```text
Story definition 已加载
```

和：

```text
某个玩家存在 StoryInstance
```

混为一谈。

---

## 5.5 `/dgr`

保留：

```text
primary command = /dgr
```

不恢复 `/dgrpg` public alias。

玩家/管理员正常输出继续中文化。

---

## 5.6 Item Nominator Container

保留当前目标：

```text
GuiContainer
+ server Container
+ 玩家背包
+ 1 个明确目标 ItemStack 槽
```

绑定必须基于服务器端目标槽真实 ItemStack。

不要回退到“选择玩家第 N 个槽”的旧 UI。

---

## 5.7 Active Objective projection

保留：

```text
Task Runtime 决定 INACTIVE / ACTIVE / COMPLETED
UI 只显示 ACTIVE
```

不要让 UI 重新推导 Task Graph 拓扑。

---

# 6. REVERIFY — 核心 Identity Runtime 先验证，不先改

以下核心模块在 B1 identity 事故中没有被修改或没有证据表明其语义已坏：

```text
EntityDgrIdentityResolver
CanonicalTaskForgeEventAdapter
CanonicalTaskForgeEventNormalizer
CanonicalTaskRuntime
```

因此 B2 的默认策略是：

> **先证明它们是否正确，再决定是否需要修改。**

禁止一开始就“修 Identity”。

---

# 7. Canonical Identity 自动化验证

B2 必须新增/补齐针对“名字或外观碰巧相同”的正反例。

重点不是写大量模拟框架，而是锁定最容易被误解的边界。

---

## 7.1 Entity Group negative control

前提：

```text
Task target = slimes
```

输入：

```text
Minecraft identity = minecraft:slime
DGR actor identities = []
```

结果：

```text
不得产生等价于 slimes 的 DGR 匹配
Task progress 不变
```

---

## 7.2 Entity Group positive control

输入：

```text
Minecraft Host type = minecraft:cow
DGR actor identities = [slimes]
```

Canonical event 链路必须包含：

```text
slimes
```

Task：

```text
target = slimes
```

应推进。

---

## 7.3 Multi-host group proof

至少证明：

```text
cow → slimes
pig → slimes
zombie → slimes
```

都可以推进同一 Group Objective。

这证明：

```text
Group_ID
```

不是某种 Minecraft class alias。

---

## 7.4 Same-type unbound negative

再次验证：

```text
unbound minecraft:slime
```

不会推进：

```text
target = slimes
```

此反例必须一直保留。

---

## 7.5 Individual Actor

对 fixture 中已有的 Individual Actor：

```text
NPC_ID = <existing id>
```

验证一个 Host Entity 经 Nominator 绑定后：

```text
interact_actor(existing NPC_ID)
```

通过真实 identity resolution 匹配。

不要新建测试 NPC_ID。

---

## 7.6 Item negative / positive

选一个与 DGR Item identity 完全无关的 Host，例如：

```text
minecraft:stick
```

Negative：

```text
普通 stick
→ 不应自动匹配 Item_ID=copper_coin
```

Positive：

```text
stick
→ Item Nominator
→ Item_ID=copper_coin
→ Runtime 可解析为 copper_coin
```

这里的 `copper_coin` 只是使用当前已有 Resource。

**不要使用模组历史 dummy `darkgrey_rpg:copper_coin` 作为正式验收 Host。**

也不要把 gold nugget 作为唯一正式证据，避免继续产生“贴图/名字巧合”。

---

# 8. Nominator B2 纠偏

B1 Nominator 的总体方向保留，但 B2 必须验证它是否真正遵循工作流。

---

## 8.1 Entity Nominator 正常用户路径

目标路径：

```text
使用 Nominator 选择世界 Entity
        ↓
打开当前已加载 Story Package
        ↓
选择 Actor / Actor Group
        ↓
服务器校验 package closure
        ↓
保存 binding
```

对 Collective Actor：

```text
individualId = null
groupIds = [Group_ID]
```

对 Individual Actor：

```text
individualId = NPC_ID
groupIds = [...]
```

不得把 Collective Actor 转成新的 NPC_ID。

---

## 8.2 UI 应明确 Resource 类型

B2 允许一个非常小的 wording 修正，以避免继续混淆：

Collective：

```text
史莱姆
Group_ID: slimes
```

Individual：

```text
酒馆老板
NPC_ID: tarven_boss
```

不要只显示：

```text
slimes / collective
```

也不要把两类都叫“角色 ID”。

这不是 0.3.2.1 的 Presentation polish，而是防止语义误读的最低限度标签。

---

## 8.3 Type-scoped Group 不参与正式 B2 验收

当前 backend 存在 entity-type group compatibility 能力。

B2 不在本阶段重新设计它。

但正式 identity Vertical Slice 禁止依赖：

```text
Minecraft entity type
→ whole-type Group_ID
```

因为这会污染“未指名原版史莱姆”的 negative control。

正式测试前必须保证测试世界没有遗留 type-group mapping。

是否长期保留该兼容能力，不在 B2 中扩张讨论。

---

# 9. Item Nominator B2 验证

正常 UI：

```text
Package
→ Item / Item Group
→ 把真实 ItemStack 放进目标槽
→ Bind
```

必须验证：

- Package closure 校验；
- catalog revision；
- Item identity revision；
- server authoritative target stack；
- 绑定成功；
- 绑定失败安全；
- close 安全；
- disconnect/异常情况下无复制；
- 目标物品不会静默丢失。

正式验收 Host 使用：

```text
minecraft:stick
```

或其他无关 vanilla Item。

---

# 10. FIX — B2 必须修的 Integration 缺口

---

## 10.1 Last Package Removal stale snapshot

需要重点审查：

```text
最后一个 .dgrs 被删除
→ package registry 变空
→ authoritative ProjectSnapshot
```

正确行为：

如果 base project 合法：

```text
restore base project snapshot
```

如果 base project 不存在/非法：

```text
install explicit empty/unloaded snapshot
```

同时 report reload failure。

**绝对不能继续保留已经删除 package 的旧 Story / Actor / Item / Session / Task。**

添加回归：

```text
load package
→ resources present

delete last .dgrs
→ reload

resources absent
```

---

## 10.2 Corrupt replacement vs deletion

两个情况语义必须不同。

### 文件仍存在但被替换成 corrupt DGRS

```text
new candidate rejected
previous accepted package remains active
```

### 文件真正被删除

```text
package uninstalled
previous accepted package must disappear
```

不要把“坏更新回滚”和“用户卸载 package”做成同一种行为。

---

## 10.3 DGRS archive budget

当前 direct reader 已有单 entry size bound。

B2 补充最小安全边界：

```text
MAX_ENTRY_COUNT
MAX_TOTAL_UNCOMPRESSED_BYTES
```

目的只是在 direct-read 内存模型下避免恶意/异常 archive 把总内存撑爆。

不要扩张成通用 VFS / sandbox framework。

---

## 10.4 历史 `.dgrs-runtime` residue

新 Runtime 不应再创建 `.dgrs-runtime`。

B2 可以安全清理已知旧缓存目录，但边界必须非常窄：

```text
仅 configured Story Package install directory
仅确切名称 .dgrs-runtime
```

禁止删除其他未知目录。

如果无法保证安全，则只在 status/log 中提示，不做激进删除。

---

# 11. REVIEW — B1 两项 Integration contract 修改

这两项不是 Identity incident 的产物，不应机械 revert。

---

## 11.1 Session Choice empty prompt

B1 允许：

```text
Choice prompt = ""
```

B2 必须直接对照 frozen Studio schema 和真实 frozen-A DGRS。

如果 Studio 本来就允许空 prompt：

```text
Java frame
network codec
session runtime
```

也应允许。

确认后：

```text
KEEP
```

如果 Studio 实际 contract 不支持，才回退。

不要以“真实运行越过报错”作为唯一理由。

---

## 11.2 Canonical Story port order

B1 将 Story port order uniqueness 从：

```text
整个 node 全局唯一
```

改为类似：

```text
同 direction 下唯一
```

B2 必须对照 frozen Studio Graph port semantics。

确认：

```text
Input order 0
Output order 0
```

是否是正常合法数据。

如果合法：

```text
KEEP
```

否则：

```text
REVERT / minimal fix
```

必须以 cross-contract 为依据，不以“测试刚好能跑”为依据。

---

# 12. DGRS Resource Closure / Status

B2 必须继续完成原 B audit 中“只看见两个 Actor”的问题。

Runtime status/reload 需要准确报告至少：

```text
Story
Actor
Item
Item Group
Session
Task
```

实际 count 来自 authoritative merged snapshot。

不能通过 manifest 文本直接猜 count。

真实 frozen-A package load 后必须与 package 内容一致。

---

# 13. `/dgr` B2 规则

继续使用：

```text
/dgr
```

子命令可保持英文，例如：

```text
/dgr status
/dgr reload
/dgr story
/dgr session
/dgr task
/dgr debug
```

普通玩家/管理员提示尽量中文。

`debug` 可以保留施工诊断，但不能代替真实验收。

---

# 14. Fresh Minecraft World

B1 的测试世界已经被污染。

已知曾发生：

- 原版史莱姆被绑定 `slimes`；
- Task 进度到 2/3；
- `tarven_boss` binding；
- `copper_coin` item binding；
- 多次召唤/击杀；
- SavedData 操作；
- 可能存在 type-group / stale binding。

因此 B2 正式 identity proof 必须使用：

```text
全新干净 Minecraft world
```

不要基于 B1 r3 world 继续证明 Identity。

旧世界只保留 forensic evidence。

---

# 15. Real Minecraft — Entity Identity Gate

严格执行下面的正反例顺序。

---

## 15.1 Fixture inspection

只读确认 frozen-A DGRS / source fixture 中：

```text
Collective Actor:
史莱姆
Group_ID = slimes

Task Objective:
消灭3只史莱姆
target = slimes
```

记录 hash / resource IDs。

不修改。

---

## 15.2 Clean server load

Story Package directory 只安装本次 frozen-A exact DGRS。

启动真实 Minecraft 1.7.10 / Forge。

确认：

```text
package load success
resource counts correct
no new .dgrs-runtime
```

---

## 15.3 Negative #1

生成一只：

```text
原版史莱姆
```

不要使用 Nominator。

击杀。

预期：

```text
Task remains 0/3
```

如果变成 1/3：

```text
IDENTITY GATE FAIL
```

先定位：

```text
binding
type-group
resolver
normalizer
runtime match
```

不要改 Studio。

---

## 15.4 Positive #1

生成：

```text
原版牛
```

用实体 Nominator：

```text
Package
→ 史莱姆
→ Group_ID: slimes
→ 指名
```

真实击杀。

预期：

```text
1/3
```

---

## 15.5 Positive #2

生成：

```text
原版猪
```

绑定：

```text
Group_ID: slimes
```

击杀。

预期：

```text
2/3
```

---

## 15.6 Negative #2

再生成：

```text
未指名原版史莱姆
```

击杀。

预期：

```text
仍为 2/3
```

---

## 15.7 Positive #3

生成：

```text
原版僵尸
```

绑定 `slimes`。

击杀。

预期：

```text
3/3
```

Objective 完成。

该 sequence 是 B2 必须保存的最终 Identity evidence。

---

# 16. Real Minecraft — Individual Actor Gate

使用现有 fixture 中已有的一个 Individual Actor。

不得新建。

流程：

```text
现实 Minecraft Entity
→ Nominator
→ existing NPC_ID
→ 真实交互
→ Story/Session/interact_actor 正确消费
```

如果使用 CustomNPC+：

```text
CustomNPC+ 只是 Host
DGR Actor identity 仍来自现有 Resource
```

不要把 CustomNPC 名称当 NPC_ID。

---

# 17. Real Minecraft — Item Identity Gate

使用现有：

```text
Item_ID = copper_coin
```

但正式 Host 使用：

```text
minecraft:stick
```

或其他完全无关 vanilla item。

---

## 17.1 Negative

未绑定 stick：

```text
real pickup / collect
```

不得因为任何名称巧合推进：

```text
Item_ID=copper_coin
```

---

## 17.2 Positive

打开 Item Nominator：

```text
把 stick 放入目标槽
→ 选择 package
→ 选择 Item_ID: copper_coin
→ Bind
```

关闭/返回物品。

再通过真实 gameplay collect/pickup。

预期：

```text
Runtime 识别 DGR identity copper_coin
```

---

## 17.3 禁止的正式验收 Host

不要用：

```text
darkgrey_rpg:copper_coin
```

证明：

```text
Item_ID=copper_coin
```

也不要用 gold nugget 作为唯一正式证据。

那个历史 dummy item 的删除仍属于：

```text
0.3.2.1
```

B2 不把 audit #1 偷渡回来。

---

# 18. Task Active Objective UI

B2 继续只做 functional UI。

数据源必须是：

```text
Canonical Task Journal / Runtime projection
```

只显示：

```text
ObjectiveStatus == ACTIVE
```

Sequential：

```text
A ACTIVE
B INACTIVE

A completed
→ A disappear
→ B becomes ACTIVE
→ B display
```

Parallel：

```text
A ACTIVE
B ACTIVE
→ both display
```

不要在 GUI 中根据连接关系推导。

动画、icon、HUD polish 延期 `0.3.2.1`。

---

# 19. Full Story Vertical Slice

只有 Identity Gate 通过之后，才恢复完整 Integration slice。

建议使用用户现有完整任务线，不再重新编一个“方便测试”的故事。

目标链路：

```text
Frozen Studio project
→ export exact .dgrs
→ Minecraft direct load
→ package resources visible
→ Actor / Group Nominator
→ Item Nominator
→ normal gameplay starts Story
→ Session
→ Task placement
→ Objective ACTIVE
→ real event progression
→ prerequisite transition
→ Task settlement
→ Story resumes from result Flow
→ later Session / Story Flow continues
```

如果用户现有 Story 有第二 Session，就继续到第二 Session。

---

# 20. Restart / Persistence

完成主 vertical slice 后验证：

```text
server stop
→ restart
```

至少检查：

- StoryInstance；
- Session / Task instance；
- Objective progress；
- Nominator entity binding；
- Item identity definition/binding；
- package authoritative snapshot。

不得因为 restart 重新用 debug command 手工补状态。

---

# 21. Package Lifecycle Gate

真实测试：

---

## 21.1 Valid package

```text
install
→ load
→ resources present
```

---

## 21.2 Valid replacement

```text
replace with another valid same-package source
→ validate complete candidate
→ atomic publish
```

不能出现半更新。

---

## 21.3 Corrupt replacement

```text
replace source file with corrupt package
→ new candidate rejected
→ last-known-good remains active
```

---

## 21.4 Delete

```text
delete source .dgrs
→ reload
→ package removed
→ Nominator catalog removed
→ merged resources removed
```

---

## 21.5 Delete last package

重点：

```text
delete final .dgrs
→ no stale package snapshot
```

---

## 21.6 Reinstall

```text
reinstall valid package
→ resources resolve again
```

已有 world bindings 可以作为 unresolved/dormant reference 保留，但 missing package 不得继续触发 Story。

不要自动破坏性删除用户 world authoring state。

---

# 22. Build / Automated Regression

B1 interruption 时完整 build 曾被 Spotless 挡住。

B2 最终必须恢复正常完整命令。

要求：

```text
Spotless PASS
compile PASS
existing probes PASS
new identity guard probes PASS
DGRS archive probes PASS
Nominator container probes PASS
Story/Session/Task probes PASS
```

不能：

```text
排除格式任务
→ 然后声称 full build PASS
```

---

# 23. Debug Command 证据边界

允许：

```text
/dgr debug
```

用于：

- 读取状态；
- 辅助定位；
- isolate manager/service。

但最终不能用：

```text
/dgr debug task emit_kill slimes
```

代替：

```text
真实玩家击杀被 Nominator 指名的 Entity
```

也不能仅用：

```text
/dgr debug story start
```

证明 normal gameplay Story start。

---

# 24. 不在 B2 做的事情

明确延期：

```text
删除 DGR dummy copperCoin
→ 0.3.2.1

删除 storage-box 左下角提示
→ 0.3.2.1

Task UI presentation polish
→ 0.3.2.1

图标 / 动画 / HUD beautification
→ 0.3.2.1
```

B2 不扩张为：

- Studio redesign；
- generic VFS；
- generic resource provider framework；
- i18n framework；
- new quest authoring model；
- new identity subsystem；
- 自动创建 Resource；
- 自动修改用户 Story。

---

# 25. 重点代码热点

B2 施工主要可能涉及：

```text
src/main/java/darkgrey/rpg/project/packages/**
src/main/java/darkgrey/rpg/project/ProjectRepository.java
src/main/java/darkgrey/rpg/project/ProjectSnapshot.java

src/main/java/darkgrey/rpg/nominator/**
src/main/java/darkgrey/rpg/network/message/nominator/**
src/main/java/darkgrey/rpg/client/gui/GuiNominatorEntity.java
src/main/java/darkgrey/rpg/client/gui/GuiNominatorInventory.java

src/main/java/darkgrey/rpg/quest/runtime/CanonicalTaskLegacyJournalAdapter.java
src/main/java/darkgrey/rpg/client/gui/GuiQuestJournal.java

src/main/java/darkgrey/rpg/session/runtime/CanonicalSessionRuntime.java
src/main/java/darkgrey/rpg/story/canonical/runtime/CanonicalStoryRuntime.java
```

核心 identity 文件：

```text
EntityDgrIdentityResolver
CanonicalTaskForgeEventNormalizer
CanonicalTaskRuntime
```

只有 fresh guard test 证明 bug 后才修改。

---

# 26. Acceptance Matrix

| Gate | Requirement |
|---|---|
| Understanding | Codex 能正确复述 Studio→DGRS→Nominator→Runtime |
| Fixture | `project_test` 未被修改 |
| Studio Freeze | `studio/**` 无 B2 修改 |
| DGRS | direct archive read，无正常解压 |
| DGRS | 不创建新的 `.dgrs-runtime` |
| DGRS | archive 总量/entry count 有边界 |
| DGRS | corrupt replacement 保留 last-known-good |
| DGRS | delete package 真正卸载 |
| DGRS | delete last package 无 stale snapshot |
| Catalog | Story/Actor/Item/Session/Task count 正确 |
| Entity UI | Individual/Group 概念明确 |
| Entity Negative | 未指名原版史莱姆不推进 `slimes` |
| Entity Positive | cow→slimes 推进 |
| Entity Positive | pig→slimes 推进 |
| Entity Negative | 第二只未指名原版史莱姆仍不推进 |
| Entity Positive | zombie→slimes 推进 |
| Individual Actor | existing NPC_ID 真实交互可解析 |
| Item Negative | 未绑定 stick 不匹配 `copper_coin` |
| Item Positive | stick→Item_ID=copper_coin 可匹配 |
| Item Safety | target slot 无丢失/复制 |
| Task UI | 只显示 ACTIVE Objective |
| Story | normal gameplay start |
| Session | canonical Session 正常执行 |
| Task | normal Task activation |
| Settlement | result Flow 返回 Story |
| Restart | runtime state 可恢复 |
| `/dgr` | public root 正确 |
| Language | 普通输出中文 |
| Build | Spotless + full regression PASS |

---

# 27. NO-GO

以下任一成立，B2 不允许声明 Agent Verified：

1. 未阅读 `DGR_WORKFLOW_AND_CONCEPTS.md` 就继续施工。
2. 把 `slimes` 当作 Minecraft slime type。
3. 再创建 `NPC_ID=slime` 迎合测试。
4. 修改用户现有 Task / Story 来让 Runtime PASS。
5. 未指名原版史莱姆推进 `target=slimes`。
6. 只用原版史莱姆证明 Group identity。
7. 用 `darkgrey_rpg:copper_coin` 作为 Item_ID=copper_coin 的正式 Host 证据。
8. 用 debug event 代替真实 Gameplay Event。
9. 修改 frozen `studio/**`。
10. DGRS 恢复成整包解压读取。
11. 删除最后 package 后旧资源仍 authoritative。
12. full build 需要跳过格式任务才能 PASS。
13. 使用 B1 被污染世界作为 fresh Identity evidence。
14. 没有 root-cause evidence 就重写 core Identity Runtime。
15. Codex 写 `USER_ACCEPTED`。

---

# 28. Development Report

B2 收尾产出：

```text
PLAN/0.3.2.0_B2_DEVELOPMENT_REPORT.md
```

报告保持事实化，不再写长篇产品教程。

必须包含：

## A. Baseline

```text
A frozen HEAD
B-pre-correction HEAD
B2 HEAD
```

## B. Understanding

不超过一小节，说明：

```text
DGR authoring identity
Minecraft Host
Nominator
Runtime
```

的关系。

## C. KEEP / FIX / REVIEW

列出：

```text
哪些 B1 保留
哪些 B2 修复
Choice prompt 决策
Story port order 决策
```

## D. Fixture integrity

记录至少：

```text
slimes Actor Group resource
Task resource
Story membership
```

施工前后 hash。

预期相同。

## E. Identity evidence

分开记录：

```text
Negative controls
Positive controls
```

必须明确 Host 原生类型和绑定 DGR identity。

## F. DGRS evidence

包括：

```text
direct-read
no extraction
corrupt replacement
delete
delete-last
reinstall
```

## G. Build

完整命令与结果。

## H. Studio freeze

执行：

```text
git diff 782a78d28ae49efd731ff61e8cb116bebf870dd1...<B2_HEAD> -- studio/
```

预期：

```text
empty
```

---

# 29. 状态规则

可使用：

```text
NOT_STARTED
ROOT_CAUSE_CONFIRMED
IMPLEMENTED
AGENT_VERIFIED
BLOCKED
NEED_USER_VERIFICATION
USER_ACCEPTED
```

Codex 可以到：

```text
0.3.2.0_B2
IMPLEMENTED
AGENT_VERIFIED
NEED_USER_VERIFICATION
```

`USER_ACCEPTED` 只能由用户明确给出。

---

# 30. Definition of Done

B2 完成需要同时满足：

1. Codex 已按 `DGR_WORKFLOW_AND_CONCEPTS.md` 正确理解实际产品工作流；
2. 不再通过创建新 Authoring Resource 迎合 Runtime 测试；
3. B1 正确的 DGRS/Nominator/Task UI 集成工作被保留；
4. B1 identity 验收证据被作废并用 fresh clean-world 正反例重做；
5. `Group_ID=slimes` 被正确证明为 DGR authored group identity；
6. 未指名原版史莱姆不会碰巧推进；
7. 不相关 Host Entity 绑定 `slimes` 后能够推进；
8. Item identity 使用不相关 Host 做正反例；
9. DGRS 保持 direct-read；
10. package lifecycle 不留下 stale authoritative state；
11. Session Choice / Story port order 有明确 cross-contract 结论；
12. Active Objective UI 可用；
13. Story → Session → Task → Settlement → Story continue 真实链路通过；
14. restart / package remove / reinstall 通过；
15. full build / probes 通过；
16. `studio/**` 保持 frozen；
17. 最终交给用户人工验收。

---

# 31. 给 Codex 的短指令

> 从 `codex/0.3.2.0_B-pre-correction` 的 WIP checkpoint 继续创建 `0.3.2.0_B2`，不要 reset 回 A。施工前先完整阅读 `DGR_WORKFLOW_AND_CONCEPTS.md` 和 B WIP interruption report，并在会话中用不超过约 500 字复述本次真实工作流与现有 fixture；确认“史莱姆是 Collective Actor / Group_ID=slimes，Task target=slimes 匹配 DGR identity，而不是 Minecraft EntitySlime”。不要修改 `project_test` 来迎合 Runtime，也不要再创建 `NPC_ID=slime`。保留 B1 已经正确完成的 DGRS direct-read、detached package snapshot、package-aware Nominator、`/dgr`、Item target container、canonical Story loaded-state 与 Active Objective projection。先为当前核心 identity path 增加正反例验证；在没有 fresh test 证明错误前，不要重写 `EntityDgrIdentityResolver`、`CanonicalTaskForgeEventNormalizer` 或 `CanonicalTaskRuntime`。正式 Minecraft Identity Gate 使用全新世界：未指名原版史莱姆必须不推进；分别把牛、猪、僵尸通过 Nominator 指名到已有 `Group_ID=slimes` 后击杀，才推进 0→1→2→3，中间再杀一只未指名原版史莱姆必须仍保持 2/3。Item Identity 使用 `minecraft:stick` 等无关 Host，先证明未绑定不匹配，再用 Item Nominator 指名为已有 `Item_ID=copper_coin`；不要用 DGR dummy `darkgrey_rpg:copper_coin` 或金粒作为正式 identity proof。继续修 B2 的真实 integration 缺口：最后 package 删除后的 stale snapshot、DGRS archive 总预算、历史 `.dgrs-runtime` residue；单独对照 frozen Studio contract 审查 B1 的空 Choice prompt 与 Story port order 修改。Identity Gate 通过后再恢复现有 Story 的真实 Story→Session→Task→Settlement→Story continue vertical slice，并完成 restart、package corrupt replacement、delete、delete-last、reinstall。不得修改 frozen `studio/**`，不得用 `/dgr debug` 代替最终 Gameplay evidence，Codex 不得标记 `USER_ACCEPTED`。
