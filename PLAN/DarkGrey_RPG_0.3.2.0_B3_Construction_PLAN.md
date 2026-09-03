# DarkGrey RPG 0.3.2.0_B3 Construction PLAN

> **定位：B2 人工审计后的最后一轮 Runtime 生命周期纠偏 + 两项历史遗留清理 + 一个窄范围 Studio 可用性修正**
>
> **本阶段暂不处理 Minecraft Nominator UI 风格重做。**
>
> 开工前先阅读：
>
> `DGR_WORKFLOW_AND_CONCEPTS.md`
>
> 目的不是重新设计 DGR，而是把 Story Package 的“定义版本 / Runtime 生命周期”真正分开，并把 B2 人工审计中确认要提前处理的历史铜币、收纳箱提示一起收口。

---

# 0. Baseline

## 0.1 B2 baseline

```text
branch:
codex/0.3.2.0_B2

HEAD:
e44affa449de9f439ecd0099c2fb5bd011252976
```

推荐新分支：

```text
codex/0.3.2.0_B3
```

B3 从 B2 继续，不回滚 B2 已经验证通过的：

- DGRS direct-read；
- package snapshot / merge；
- Canonical Identity；
- `/dgr`；
- package delete / corrupt replacement 基础；
- Active Objective；
- Story → Session → Task 基础链路。

---

# 1. B3 Scope

B3 只做四件事：

```text
A. Story Package Generation / Runtime Lifecycle
B. Story Start “可重复” checkbox
C. 删除历史 dummy copperCoin
D. 删除收纳箱成功聊天提示
```

明确不做：

```text
Minecraft Entity Nominator UI 重做
Minecraft Item Nominator UI 重做
Minecraft GUI 原版风格统一
Task HUD 美化
其他 Presentation polish
```

这些留到后续专门讨论。

---

# 2. Root Cause: Story Identity 与 Story Generation 被混在了一起

当前真实问题不是：

```text
repeatable 按钮无效
```

而是：

```text
旧 Story Package
→ 产生旧 Runtime Instance
→ Package 被新内容替换
→ 旧 Runtime Instance 仍然继续被当作 authoritative
```

典型复现：

```text
Generation A
package_id = kill_slimes
story_id   = kill_slimes
repeat_policy = once

玩家拒绝任务
→ Story TERMINATED
→ terminal StoryInstance 保存 repeat_policy=ONCE

Studio 改成：
repeat_policy = repeatable

重新导出
→ 仍然 package_id = kill_slimes
→ 仍然 story_id   = kill_slimes

Minecraft 替换 .dgrs
→ /dgr reload

当前错误：
旧 ONCE StoryInstance 仍存在
→ 新 Story Definition 无法正常接管
```

因此 B3 必须正式区分：

```text
Identity
Generation
Runtime Instance
```

---

# 3. Identity Model — DO NOT Make story_id Dynamic

B3 **禁止把 `story_id` 改成每次导出随机变化的动态 ID**。

正确模型：

```text
package_id
= Story Package 的稳定安装身份

story_id
= Story 的稳定逻辑身份

content_fingerprint
= 当前 Package 内容属于哪一代定义

Runtime Instance
= 某一代 Definition 产生的玩家运行状态
```

例如：

```text
package_id = kill_slimes
story_id   = kill_slimes

Generation A:
fingerprint = aaa...

Generation B:
fingerprint = bbb...
```

A 与 B 仍然是：

> 同一个逻辑 Story Package 的不同内容代。

---

# 4. Package Install Identity

B3 使用：

```text
InstallIdentity = (package_id, story_id)
```

比较规则：

## 4.1 两者都相同

```text
same package_id
same story_id
```

说明：

```text
same logical package
```

再比较 fingerprint 判断内容是否变化。

## 4.2 package_id 相同、story_id 不同

这是：

```text
identity replacement
```

不是普通内容更新。

只有新 candidate 完整验证通过后：

```text
retire old package runtime
uninstall old logical package
install new logical package
```

日志必须明确报告 identity replacement。

## 4.3 source file 上 package_id 发生变化

同样视为：

```text
old package removed
+
new package installed
```

不能因为文件名相同就把两个 package 当成同一身份。

---

# 5. DGR Package Content Fingerprint v1

B3 新增 Runtime 自动计算：

```text
DGR Package Content Fingerprint v1
```

推荐类名：

```text
StoryPackageContentFingerprint
```

或同等窄职责名称。

**不需要修改 DGRS v1 schema。**

**不需要让 Studio 写 hash。**

Runtime 当前已经拥有 DGRS detached resource bytes，可以直接计算。

---

# 6. Fingerprint 的设计目标

Fingerprint 必须满足：

```text
同一 package 的 authoritative runtime content 不变
→ fingerprint 稳定

任意 authoritative content 发生变化
→ fingerprint 改变
```

包括：

- Story Graph；
- Start `repeat_policy`；
- Start triggers；
- Story nodes / ports / connections；
- Session；
- Task；
- Actor；
- Actor Group；
- Item；
- Item Group；
- Dialogue；
- Quest；
- membership；
- Story public Logic graph；
- project contract metadata（有限范围）。

不要专门手写：

```text
节点数量
节点类型
参数数量
连接数量
```

这种业务字段 hash。

原因：

> 以后新增字段时非常容易漏算。

B3 直接 hash authoritative resource bytes。

---

# 7. Fingerprint Input Set

Fingerprint 只覆盖会影响 Runtime Definition 的确定性输入。

## 7.1 Synthetic contract header

加入：

```text
format
format_version
schema_version
story_schema_version
```

不加入：

```text
producer_version
package_version
```

原因：

- exporter 版本变化不应该仅因为工具版本变化就强制 retire Runtime；
- package_version 目前不是 Runtime semantic source，不让它单独触发生命周期 reset。

Identity 也不放进 fingerprint：

```text
package_id
story_id
```

因为它们已经单独作为 InstallIdentity 比较。

## 7.2 Resource records

加入：

```text
project.json
```

以及 manifest `required_resources` 声明的全部 authoritative entries：

```text
story
actors
items
item_groups
dialogues
quests
canonical_stories
canonical_memberships
sessions
tasks
story_logic_graph（如果存在）
```

---

# 8. Fingerprint Record Role

不能只 hash 文件路径。

每个 entry 还带一个固定 role：

```text
project
story
actor
item
item_group
dialogue
quest
canonical_story
canonical_membership
session
task
story_logic_graph
```

这样即使未来 manifest 中同一资源路径被错误放到不同 role，fingerprint 也会改变。

---

# 9. Fingerprint v1 Exact Algorithm

算法：

```text
SHA-256
```

输出：

```text
64-char lowercase hex
```

输入必须 deterministic。

## 9.1 Header

首先写入固定 ASCII：

```text
DGR-PACKAGE-CONTENT-FINGERPRINT-V1\0
```

然后依次写入 contract fields：

```text
format
format_version
schema_version
story_schema_version
```

每个字段使用 length-prefixed UTF-8，禁止模糊字符串拼接。

## 9.2 Entry sorting

所有 Resource Record 按：

```text
role ASC
then
path ASC
```

稳定排序。

路径使用 manifest 已验证的：

```text
forward-slash path
```

不要依赖 ZIP entry physical order。

## 9.3 Record encoding

每个 record：

```text
uint32_be role_byte_length
role_utf8_bytes
uint32_be path_byte_length
path_utf8_bytes
uint64_be content_byte_length
raw_content_bytes
```

然后持续 update 同一个 SHA-256 digest。

不要：

```text
role + ":" + path + ":" + content
```

这种可能有边界歧义的普通字符串拼接。

---

# 10. Why Raw Bytes Instead of JSON Semantic Canonicalization

B3 v1 不做递归 JSON canonical serializer。

使用：

```text
validated raw UTF-8 resource bytes
```

原因：

1. 当前 frozen Studio exporter 输出格式稳定；
2. 不会漏掉未知未来 semantic field；
3. 实现简单；
4. 不引入第二套 JSON semantic normalization；
5. fingerprint false-negative 风险最低。

代价：

```text
纯格式/空格变化
```

也可能得到新 fingerprint。

B3 接受这个代价。

如果未来 exporter 出现大量 formatting-only churn，再单独设计：

```text
Fingerprint v2
```

不要现在过度工程化。

---

# 11. LoadedStoryPackage Changes

`LoadedStoryPackage` 增加：

```text
contentFingerprint
```

至少暴露：

```text
getContentFingerprint()
```

构造后 immutable。

对于 DGRS：

```text
fingerprint
```

直接基于 detached archive content 计算。

对于仍兼容的 unpacked legacy package：

```text
同样根据已验证 required resource bytes
```

计算。

不要用：

```text
directory timestamp
file modified time
ZIP timestamp
file size
```

替代内容 fingerprint。

---

# 12. Package Generation Classification

每次 reload 必须得到一个明确 delta：

```text
UNCHANGED
UPDATED
REPLACED
ADDED
REMOVED
REJECTED_CANDIDATE
```

## 12.1 UNCHANGED

```text
old InstallIdentity == new InstallIdentity
AND
old fingerprint == new fingerprint
```

行为：

```text
Definition no-op
Runtime instances PRESERVE
```

管理员单纯执行 `/dgr reload` 不能清玩家任务。

## 12.2 UPDATED

```text
same InstallIdentity
different fingerprint
```

行为：

```text
new generation
```

必须 retire 旧 generation Runtime。

## 12.3 REPLACED

source mapping 对应的 InstallIdentity 改变：

```text
old (package A, story A)
new (package B, story B)
```

行为：

```text
retire old
install new
```

## 12.4 ADDED

全新 package：

```text
install
```

不需要清其他 package Runtime。

## 12.5 REMOVED

旧 package 不再存在：

```text
uninstall
retire its Runtime
```

## 12.6 REJECTED_CANDIDATE

新文件存在但 candidate validation 失败：

```text
DO NOT retire old
DO NOT replace old
DO NOT mutate generation registry
DO NOT clear old Runtime
```

继续保留：

```text
last-known-good
```

---

# 13. Validate First, Retire Later

这是 B3 最重要的事务顺序。

错误：

```text
看到新文件
→ 先清旧 Runtime
→ 再解析
→ 发现新包坏了
```

禁止。

正确：

```text
scan candidate
↓
read
↓
validate archive
↓
validate manifest
↓
build snapshot
↓
merge entire candidate set
↓
compute fingerprint / generation delta
↓
所有步骤 PASS
↓
commit replacement
↓
retire changed/removed generation Runtime
```

原则：

> **Candidate 失败绝不能伤害 last-known-good generation。**

---

# 14. Runtime Generation Ownership

B3 需要建立最小但明确的 package-generation ownership。

不要做 generic migration framework。

需要知道：

```text
某个持久化 Runtime Instance
来自哪个 logical package generation
```

推荐：

```text
PackageGenerationKey {
    packageId
    storyId
    contentFingerprint
}
```

---

# 15. Persisted Generation Registry

仅靠内存比较不够。

场景：

```text
server stop
↓
管理员在服务器关闭期间替换 .dgrs
↓
server start
```

旧内存 package map 不存在。

因此 B3 必须持久化一个**非常小的 world-level generation registry**：

```text
package_id
story_id
content_fingerprint
```

推荐类似：

```text
StoryPackageGenerationSavedData
```

只负责：

```text
上一轮被世界接受的 Package Generation 身份
```

不要扩展成 Package Manager。

---

# 16. Startup Generation Reconciliation

Server world / MapStorage 可用后，在正常 DGR Gameplay Event 开始前：

```text
load current validated packages
↓
load persisted generation registry
↓
diff
↓
UNCHANGED → preserve runtime
UPDATED  → retire old runtime
REPLACED → retire old runtime
REMOVED  → retire old runtime
ADDED    → no old runtime
↓
publish authoritative current generation registry
```

注意：

> 不要试图在 Forge preInit 里访问尚未存在的 world MapStorage。

如果当前 package scanning 保留在 preInit，可以继续 scan/load；但 generation reconciliation 必须放到 world/server state 已可用的阶段。

---

# 17. Online `/dgr reload` Generation Reconciliation

服务器运行时：

```text
/dgr reload
```

也必须走同一套 Generation Delta。

不要写两套 startup lifecycle / reload lifecycle。

核心 diff / retire 逻辑应共用。

---

# 18. Runtime Retirement Semantics

当：

```text
UPDATED
REPLACED
REMOVED
```

发生时，旧 generation 产生的**运行状态**必须 retire。

清除：

```text
Canonical StoryInstance
Canonical SessionInstance
Canonical TaskInstance
Pending Session/Story continuations
Story cursor
Task objective runtime progress
old aggregate wait state
```

必须覆盖：

```text
在线玩家
离线玩家
```

即：

> 不能只循环当前 online players。

要从真正的 WorldSavedData / persistent stores 按 Story ownership 清理。

---

# 19. What Retirement Must NOT Delete

不要删除：

```text
Nominator entity bindings
NpcIdentitySavedData
Item identity bindings
玩家背包
玩家世界状态
Minecraft entities
作者定义的 NPC_ID / Group_ID 映射
```

这些属于 World Binding / World State，不是 Story Runtime Instance。

例如：

```text
Villager
→ NPC_ID = tarven_boss
```

更新 `kill_slimes` package 后，只要新 package 仍定义 `tarven_boss`，绑定立即继续有效，不要求玩家重新指名。

---

# 20. Missing Identity After Replacement

如果新 generation 不再定义某个之前绑定的：

```text
NPC_ID
Group_ID
Item_ID
```

不要自动删除 world binding。

语义：

```text
unresolved / dormant
```

它不应触发缺失 Story。

以后同 ID Resource 返回时可以重新 resolve。

---

# 21. Active Runtime on Package Update

B3 暂时不做 Graph migration。

如果 active Story 的 package content fingerprint 发生变化：

```text
active Story
→ retire
```

连同其 Session / Task child 一起结束运行生命周期。

不要尝试 old node id → new node id 自动迁移。

B3 contract：

> **Definition replacement can retire Runtime; it does not hot-migrate Runtime.**

---

# 22. Terminal Runtime on Package Update

Terminal instance 同样属于旧 generation。

因此：

```text
once terminal instance
Generation A
```

当 Package 更新为 Generation B 时必须被 retire。

之后下一次启动条件发生：

```text
使用 Generation B 当前 authored repeat policy
```

不允许旧 `ONCE` 继续阻塞。

这正是本次人工审计问题的核心修复。

---

# 23. Same Generation Repeatable Semantics

注意：

```text
Package fingerprint 不变
```

时，repeatable 仍然要正常工作。

同一个 loaded generation：

```text
repeatable Story
run #1 TERMINATED
↓
再次满足 Start Trigger
↓
new StoryInstance
run #2
```

不需要 reload。

反之：

```text
once Story
run #1 TERMINATED
↓
再次满足 Start Trigger
↓
不得新建 run #2
```

---

# 24. Repeatable Checkbox — Narrow Studio Thaw

B3 同时把用户容易忘记的 Studio 可用性修正一起完成。

这是 B3 唯一授权的窄范围 Studio thaw。

只改：

```text
Story Start repeatability authoring surface
```

不得借此重开其他 Studio 功能。

---

# 25. New Authoring Label

删除用户界面中的：

```text
重复策略
```

正常 authoring 语义统一为：

```text
可重复
```

表现：

```text
☐ 可重复
```

---

# 26. Storage Contract Remains Unchanged

不要把 DGRS property 改成 bool。

底层继续：

```json
"repeat_policy": "once"
```

或：

```json
"repeat_policy": "repeatable"
```

Mapping：

```text
unchecked → once
checked   → repeatable
```

这样 DGRS schema 不变、Runtime parser 不变、旧项目可继续打开、不需要 migration。

---

# 27. Start Node Body

参考 Task `【目标】` 节点已有“前置条件” checkbox 的交互方式。

在 `【开始】` 节点内部的“参数”区域加入：

```text
☐ 可重复
```

必须在节点本体即可看见。

不要要求作者只能去 Inspector 才知道当前 Story 是否可重复。

---

# 28. Inspector

Inspector 中原来的：

```text
重复策略 [可重复 ▼]
```

改成：

```text
可重复 [checkbox]
```

Node body 与 Inspector：

```text
同一 property
双向同步
```

不能形成两份状态。

---

# 29. Repeatable Editing Requirements

必须支持：

```text
click checkbox
save
close/reopen
undo
redo
export
re-import/open
```

状态一致。

至少验证：

```text
unchecked → once
checked   → repeatable
```

---

# 30. Repeatable + Fingerprint Integration Test

## A

当前 fixture：

```text
repeatable = false
```

导出 A，记录：

```text
package_id
story_id
fingerprint_A
```

## B

仅修改：

```text
☐ 可重复
→
☑ 可重复
```

其他 Story 内容不动。

导出 B。

要求：

```text
package_id A == package_id B
story_id A   == story_id B
fingerprint_A != fingerprint_B
```

---

# 31. Real Lifecycle Acceptance: once → repeatable

顺序：

```text
Install A
↓
绑定 tarven_boss
↓
触发 Story
↓
选择拒绝
↓
Story TERMINATED
```

确认：

```text
再次交互
→ ONCE 不重启
```

然后：

```text
替换为 B
↓
/dgr reload
```

必须发生：

```text
UPDATED generation detected
↓
old A Story/Session/Task runtime retired
↓
B authoritative
```

再与同一个已指名 `tarven_boss` 交互：

```text
新的 Story run 启动
```

并且：

```text
无需重新 Nominator
```

---

# 32. Same B Repeatable Acceptance

B 本身是 repeatable。

完成/拒绝一次 B run 后，再次交互 `tarven_boss`：

```text
必须再次启动新的 StoryInstance
```

证明 repeatable 不是只因为 reload 清了状态才碰巧能重启。

---

# 33. Unchanged Reload Must Preserve Runtime

另一个强制反例：

运行一个 active Task：

```text
progress = 2/3
```

不改变 DGRS 文件。

执行：

```text
/dgr reload
```

要求：

```text
same fingerprint
→ UNCHANGED
→ StoryInstance preserve
→ TaskInstance preserve
→ progress still 2/3
```

否则 fingerprint lifecycle 变成了“reload 就清任务”，同样错误。

---

# 34. Corrupt Replacement Must Preserve Runtime

运行 Generation B，Task 2/3。

将 source 替换成 corrupt DGRS：

```text
/dgr reload
```

要求：

```text
candidate rejected
B remains authoritative
Runtime remains
Task still 2/3
```

绝不能：

```text
fingerprint unknown
→ retire old
```

---

# 35. Delete Package

删除：

```text
kill_slimes.dgrs
```

reload：

```text
REMOVED
→ package uninstalled
→ old runtime retired
→ Nominator catalog no longer exposes package
→ authoritative resources disappear
```

World bindings 保留 dormant。

---

# 36. Reinstall

重新安装 valid Generation B：

```text
ADDED
```

要求：

```text
existing tarven_boss binding resolves again
Story can start
```

---

# 37. Offline Replacement / Restart Gate

步骤：

```text
server running Generation A
→ create terminal/active runtime
→ clean stop
```

服务器关闭期间：

```text
replace A.dgrs with B.dgrs
```

再启动。

要求：

```text
persisted generation registry sees:
old fingerprint A
current fingerprint B

→ retire A runtime before gameplay resumes
→ B authoritative
```

不得 restore A cursor against B resource。

---

# 38. B3 Runtime Cleanup API

不要散落很多：

```text
if fingerprint changed then ...
```

建议建立一个很窄的 lifecycle service，例如：

```text
StoryPackageGenerationLifecycle
```

职责仅：

```text
diff accepted generations
compute affected story IDs
retire runtime state for affected story IDs
update generation SavedData
```

禁止扩张成 generic Package Manager / migration framework / VFS / plugin lifecycle framework。

---

# 39. Runtime Retirement by Story Ownership

需要审计所有 canonical persistence owner。

至少：

```text
CanonicalSessionSavedData
Canonical Story store
Canonical Session store
pending continuations
Canonical Task persistent store / SavedData
```

添加明确：

```text
discardByStoryIds(Set<String>)
```

或同等能力。

必须一次处理 all player UUIDs，不是只清当前玩家。

---

# 40. Do Not Reuse `bindAvailable()` as the Whole Solution

B2 的：

```text
bindAvailable()
```

主要解决 Resource 已不存在的问题。

B3 新问题是：

```text
Resource ID 仍存在
但 generation 已变
```

不能仅靠：

```text
resolver.resolve(id) != null
```

判断旧 instance 合法。

Generation mismatch 必须成为明确 retire 条件。

---

# 41. Existing Story Resource Fingerprint

当前 Canonical Story Runtime 已有自己的：

```text
resourceFingerprint
```

B3 不删除它。

它继续负责：

```text
Story resource snapshot integrity
```

Package Content Fingerprint 负责更高一层：

```text
整个 Story Package generation
```

二者关系：

```text
resourceFingerprint = 单 Story Resource integrity
package contentFingerprint = Package generation identity
```

不要互相替代。

---

# 42. Why Package-Level Fingerprint Is Necessary

仅 Story Resource fingerprint 不够。

例如：

```text
Story Graph 没改
Task resource 改了
```

Story resource fingerprint 可能不变。

但正在执行旧 Task 的 Runtime 已经不应继续解释新 Task Definition。

Package fingerprint 会变化：

```text
→ generation update
→ entire package runtime retire
```

---

# 43. Historical Cleanup 1 — Remove Dummy Copper Coin

B3 删除模组历史测试物品：

```text
darkgrey_rpg:copper_coin
```

当前实现是：

```text
ModItems.copperCoin
texture = minecraft:gold_nugget
```

这是历史遗留 dummy item。

---

# 44. Copper Coin Removal Scope

删除：

```text
ModItems.copperCoin field
new Item() registration
GameRegistry.registerItem(..., "copper_coin")
相关 lang entry
相关 creative-tab/reference
相关 test/probe
任何仅为 dummy item 服务的资源
```

执行 repository-wide reference search。

最终：

```text
ModItems.copperCoin references = 0
```

---

# 45. DO NOT Delete Authored `Item_ID=copper_coin`

删除的是：

```text
Minecraft mod registry item:
darkgrey_rpg:copper_coin
```

不是：

```text
Studio authored Item Resource:
Item_ID = copper_coin
```

用户现有 Story Package 中 `Item_ID=copper_coin` 继续合法。

Nominator 仍然可以：

```text
minecraft:stick
→ Item_ID=copper_coin
```

---

# 46. Missing Mapping Compatibility

删除注册 Item 后，旧测试世界可能保存过：

```text
darkgrey_rpg:copper_coin
```

B3 必须至少验证：

```text
加载旧测试 world 不因 missing item mapping 崩溃
```

如果 Forge 1.7.10 报 missing mapping 需要处理：

```text
添加最小 ignore/cleanup compatibility
```

不要为了旧 dummy item 继续保留注册。

不要默认把它 remap 成 `minecraft:gold_nugget`。

---

# 47. Historical Cleanup 2 — Storage Box Chat Noise

用户要求删除的是：

```text
收纳箱成功操作时左下角聊天提示
```

不是所有 Tool feedback。

---

# 48. Remove Storage Success Messages

当前 Storage Box 成功路径类似：

```text
已保存创造模式实体模板；原实体保留。
实体已收纳；唯一 NPC ID 继续被占用。
已生成创造模式副本；模板仍保留。
实体已恢复；收纳箱已清空。
```

B3 删除这些成功聊天消息。

---

# 49. Keep Storage Error Messages

保留真正错误：

```text
收纳箱已占用
无法放出
数据损坏
实体工具操作失败
```

即：

```text
success → silent
failure → visible
```

---

# 50. Storage Box Visual State Remains Authoritative

不改已有：

```text
open icon
closed icon
occupied state
item damage/state
```

玩家通过收纳箱自身动画/外观判断空或已收纳。

---

# 51. Storage Box Tooltip

本次审计针对左下角聊天提示。

B3 默认不删除 hover tooltip：

```text
空置
已收纳: ...
生存搬运...
创造模板...
```

因为 tooltip 不会持续污染聊天区。

---

# 52. Minecraft Nominator UI Explicitly Deferred

B3 不碰：

```text
GuiNominatorEntity
GuiNominatorInventory
```

的整体 UI 风格/布局重做。

即使人工审计已指出它们不够好用，本轮：

```text
DEFER
```

到专门 UX 版本。

---

# 53. Studio Freeze Boundary in B3

B3 唯一允许改的 Studio area：

```text
Story Start repeat_policy authoring projection
```

除此之外：

```text
studio/**
```

继续 frozen。

最终报告必须列所有 Studio diff，并证明都只属于“可重复” checkbox。

---

# 54. Recommended Implementation Order

## Phase 0 — Baseline

```text
checkout B2
create B3
record HEAD
full build baseline
```

## Phase 1 — Fingerprint Pure Layer

实现：

```text
StoryPackageContentFingerprint
```

纯函数测试。

此阶段不改 lifecycle。

## Phase 2 — Loaded Package Generation Metadata

给 `LoadedStoryPackage` 加 fingerprint。

内部 comparison 使用完整 64 hex。

## Phase 3 — Generation Delta

实现：

```text
UNCHANGED
UPDATED
REPLACED
ADDED
REMOVED
REJECTED
```

## Phase 4 — Runtime Retirement

先做 Story / Session / Task / Continuation 全玩家持久化级清理。

## Phase 5 — Persisted Generation Registry

增加最小 world SavedData。

验证 write / restart / read / diff。

## Phase 6 — Wire Startup + `/dgr reload`

统一走同一个 Generation Reconciliation。

## Phase 7 — Studio “可重复”

只改 Start node checkbox、Inspector checkbox、property mapping、undo/redo/save/export。

## Phase 8 — Historical Cleanups

删除 dummy copperCoin 与 Storage Box 成功聊天。

## Phase 9 — Automated Regression

跑 full build、现有 probes、新 generation/fingerprint probes、Studio tests。

## Phase 10 — Real Minecraft Lifecycle Vertical Slice

严格按 A/B generation 场景实测。

---

# 55. Automated Fingerprint Tests

至少：

```text
FINGERPRINT_SAME_CONTENT_STABLE
FINGERPRINT_RESOURCE_CHANGE_DETECTED
FINGERPRINT_ROLE_CHANGE_DETECTED
FINGERPRINT_ENTRY_ORDER_INDEPENDENT
FINGERPRINT_REPEAT_POLICY_CHANGE_DETECTED
```

---

# 56. Automated Lifecycle Tests

至少：

```text
PACKAGE_GENERATION_UNCHANGED_PRESERVES_RUNTIME
PACKAGE_GENERATION_UPDATED_RETIRES_RUNTIME
PACKAGE_GENERATION_REPLACED_RETIRES_OLD
PACKAGE_GENERATION_REMOVED_RETIRES_RUNTIME
PACKAGE_CORRUPT_REPLACEMENT_PRESERVES_OLD
PACKAGE_OFFLINE_REPLACEMENT_RECONCILES_ON_START
```

---

# 57. Repeat Policy Runtime Tests

至少：

```text
STORY_ONCE_TERMINAL_DOES_NOT_RESTART_SAME_GENERATION
STORY_REPEATABLE_TERMINAL_RESTARTS_SAME_GENERATION
STORY_ONCE_TO_REPEATABLE_NEW_GENERATION_RETIRES_OLD_ONCE_INSTANCE
```

---

# 58. Runtime Preservation Test

必须覆盖：

```text
Task ACTIVE
progress 2/3
same package reload
```

Expected：

```text
2/3
```

这是 B3 release gate。

---

# 59. Real Minecraft Test Fixture

继续使用用户真实：

```text
kill_slimes
```

不要再创建另一个方便测试的 Story。

需要两份导出：

```text
A: 可重复 = false
B: 可重复 = true
```

除这个 property 外尽量不改其他 authoring 内容。

---

# 60. Real Test Sequence

## Step 1

安装 A：

```text
bind tarven_boss
interact
choose 拒绝
```

Story terminal。

再交互：

```text
不得 restart
```

## Step 2

替换 B：

```text
/dgr reload
```

status/log 必须报告：

```text
kill_slimes UPDATED
old fingerprint <short>
new fingerprint <short>
runtime retired
```

## Step 3

不重新指名 tarven_boss。

再次交互：

```text
Story starts
```

## Step 4

终止 B run。

再次交互：

```text
repeatable starts another run
```

## Step 5

启动 Task 到 2/3，不改 package，执行 `/dgr reload`：

```text
UNCHANGED
progress still 2/3
```

## Step 6

corrupt replacement：

```text
candidate rejected
old valid B remains
2/3 remains
```

## Step 7

delete package：

```text
REMOVED
runtime retired
resources removed
bindings dormant
```

## Step 8

reinstall B：

```text
ADDED
old world binding resolves
```

---

# 61. Studio “可重复” Acceptance

必须人工看到：

Start node body：

```text
☐ 可重复
```

Inspector：

```text
可重复 ☐
```

勾选任一处，另一处同步。

保存重开保持。

导出后：

```text
repeat_policy=repeatable
```

取消勾选：

```text
repeat_policy=once
```

---

# 62. CopperCoin Acceptance

Minecraft creative tab / item registry：

```text
不再出现历史 DGR 铜币 dummy item
```

repository：

```text
ModItems.copperCoin references = 0
```

但 package：

```text
Item_ID=copper_coin
```

仍可被 Runtime/Nominator 识别。

---

# 63. Storage Box Acceptance

正常：

```text
capture
release
creative template spawn
```

成功时：

```text
聊天区不出现收纳箱状态播报
```

箱子 open/closed visual state 仍正确。

失败：

```text
occupied
corrupt
invalid release
```

仍出现明确错误。

---

# 64. Build Gate

最终必须：

```text
Spotless / formatting PASS
compile PASS
tests PASS
all JavaExec probes PASS
Studio relevant tests PASS
git diff --check PASS
```

不得绕过格式任务。

---

# 65. NO-GO

任一成立，B3 不允许 Agent Verified：

1. `story_id` 被改成每次导出随机/动态值；
2. 相同 package 内容仅 `/dgr reload` 就清玩家 Runtime；
3. fingerprint 依赖 ZIP entry order / timestamp；
4. fingerprint 只算 Story Graph，不算 Session/Task/membership；
5. 新 corrupt candidate 导致旧 Runtime 被清；
6. same fingerprint reload 丢 Task 进度；
7. updated fingerprint 仍继续使用旧 terminal ONCE instance；
8. offline replacement 后恢复旧 generation cursor；
9. Package update 删除 Nominator world bindings；
10. 为了解决 lifecycle 引入复杂 Graph migration；
11. `Item_ID=copper_coin` 被误删；
12. `darkgrey_rpg:copper_coin` dummy item 仍注册；
13. 收纳箱成功操作仍刷左下角聊天；
14. 收纳箱真正错误提示被一起删掉；
15. B3 顺手重做 Minecraft Nominator UI；
16. Studio diff 超出 Start “可重复” authoring；
17. Codex 标记 `USER_ACCEPTED`。

---

# 66. Development Report

产出：

```text
PLAN/0.3.2.0_B3_DEVELOPMENT_REPORT.md
```

必须包含：

## A. Baseline

```text
B2 HEAD
B3 HEAD
```

## B. Fingerprint Spec

明确记录最终 v1：

```text
identity tuple
input roles
sorting
binary framing
SHA-256
```

不能只写“计算了 hash”。

## C. Generation Delta Evidence

列出 UNCHANGED / UPDATED / REPLACED / REMOVED / REJECTED 各自证据。

## D. Persistence Evidence

证明 online update / offline update / restart 都正确。

## E. Repeatable

记录：

```text
A once fingerprint
B repeatable fingerprint
same IDs
different fingerprint
```

## F. Runtime State

明确记录：

```text
which Runtime states retire
which World bindings preserve
```

## G. Historical Cleanup

记录：

```text
copperCoin removed
storage success chat removed
```

## H. Studio Diff

列出所有 `studio/**` modified files，并解释每个都只服务“可重复” checkbox。

---

# 67. Definition of Done

B3 完成意味着：

1. `package_id / story_id` 保持稳定逻辑身份；
2. Package Content Fingerprint v1 自动识别内容 generation；
3. same generation reload 保留 Runtime；
4. valid updated generation retire 旧 Runtime；
5. corrupt replacement 保留 old generation；
6. removed package retire Runtime；
7. offline replacement 在 restart 时正确 reconcile；
8. Nominator / Item bindings 不因 package update 被破坏；
9. once → repeatable hot replacement 正常；
10. repeatable 同 generation 可多次运行；
11. Start 节点和 Inspector 都使用 `可重复` checkbox；
12. schema 仍为 `once / repeatable`，不迁移 DGRS；
13. dummy `darkgrey_rpg:copper_coin` 删除；
14. authored `Item_ID=copper_coin` 不受影响；
15. 收纳箱正常成功操作不再刷聊天；
16. 收纳箱错误反馈保留；
17. Minecraft Nominator UI 未在 B3 扩张；
18. full build / probes PASS；
19. 等待用户人工验收。

---

# 68. Status Rule

Codex 最多可以：

```text
0.3.2.0_B3
IMPLEMENTED
AGENT_VERIFIED
NEED_USER_VERIFICATION
```

不能：

```text
USER_ACCEPTED
```

---

# 69. Short Instruction for Codex

> 从 `codex/0.3.2.0_B2 @ e44affa449de9f439ecd0099c2fb5bd011252976` 创建 B3。先阅读 `DGR_WORKFLOW_AND_CONCEPTS.md`。B3 不做 Minecraft Nominator UI redesign，只处理 Story Package generation lifecycle、Start“可重复”checkbox、历史 dummy copperCoin 删除、收纳箱成功聊天清理。不要把 `story_id` 动态化；`package_id + story_id` 是稳定 InstallIdentity。新增 Runtime-computed `DGR Package Content Fingerprint v1`：SHA-256，基于固定 contract header + manifest 声明的 authoritative resource records；records 以 role/path 稳定排序并使用 length-prefixed binary framing，覆盖 project/story/actor/item/item_group/dialogue/quest/canonical_story/canonical_membership/session/task/story_logic_graph，排除 ZIP 时间戳、entry physical order、producer_version/package_version。same identity + same fingerprint = UNCHANGED，必须保留所有 Story/Session/Task runtime 和 Task progress；same identity + different fingerprint = UPDATED，candidate 完整验证/merge 成功后 retire 旧 generation 的 Story/Session/Task/continuation，再让新 definition authoritative；corrupt candidate 必须保留 last-known-good generation 和 Runtime；delete = REMOVED 并 retire Runtime。增加极小的 world-level persisted generation registry，使服务器关闭期间替换 DGRS 后，restart 能比较旧 fingerprint 与当前 fingerprint 并在 gameplay 恢复前清理旧 generation runtime。不要清 Nominator entity/item bindings；缺失 ID 只 dormant。不要做 Graph migration。B3 唯一 Studio thaw 是 Story Start repeatability authoring：节点参数和 Inspector 都改成 `☐ 可重复`，unchecked↔`once`、checked↔`repeatable`，底层 schema 不变，必须支持同步、undo/redo、save/reopen/export。真实验收用同一个 `kill_slimes`：A=once，B=repeatable，package_id/story_id 保持一致而 fingerprint 必须变化；A terminal 后不重启，换 B reload 后无需重新指名 tarven_boss 即可新开 Story，且 B terminal 后同 generation 再次交互也能新开。另必须证明 active Task 2/3 时 unchanged reload 仍为 2/3，corrupt replacement 仍保留 2/3。删除 `ModItems.copperCoin`/`darkgrey_rpg:copper_coin` dummy registry 及相关引用，但绝不能删除 Studio `Item_ID=copper_coin`；验证旧 world missing mapping 不崩。删除 Storage Box capture/release 的成功聊天提示，只保留错误/损坏提示；保留箱子自身 open/closed visual state 和 tooltip。最终 full build + all probes，通过后只报告 AGENT_VERIFIED，等待用户验收。
