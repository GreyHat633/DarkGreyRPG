# DarkGrey RPG 0.3.2.0_B4 Construction PLAN

> 施工记录（2026-09-08）：B4 实现与代理实机验收已完成，状态 `AGENT_REAL_MACHINE_VERIFIED`。用户验收与架构冻结仍为 `NO`。最终产物、实机证据及版本边界见 [施工报告](0.3.2.0_B4_DEVELOPMENT_REPORT.md)。后续用户修订：新建故事固定显示 NameSpace 前缀，相关操作移至故事右键菜单；完整 DGR ID 的本地部分支持英文字母大小写并区分大小写。具体证据与交付版本见报告末尾。以下保留原始 PLAN 作为施工基线。

> **定位：0.3.2.0 最终 Architecture Closure**
>
> B4 是进入 `0.3.2.1` 前最后一次 Studio ↔ DGRS ↔ Minecraft Runtime 架构收口。
>
> 本阶段只做四个主任务：
>
> 1. Repeatable Story Run 生命周期重置
> 2. Story Start 资格 + 同 NPC 多 Story 交互仲裁
> 3. Legacy Story / Quest 生产 Runtime 退役，Canonical 成为唯一正式执行体系
> 4. Namespace 身份系统：全局 Namespace + Story 自定义 Namespace + 跨 Package 引用
>
> B4 通过用户人工验收后，才允许冻结 `0.3.2.0` 架构并进入 `0.3.2.1`。

---

# 0. Baseline

```text
repository:
GreyHat633/DarkGrey_RPG

baseline branch:
codex/0.3.2.0_B3

baseline HEAD:
805fed51913c637fe3a8c730f7310b2f5d114b2d

target branch:
codex/0.3.2.0_B4
```

不得回滚 B3 已完成的：

- DGRS direct-read；
- Package Content Fingerprint；
- Package Generation lifecycle；
- `UNCHANGED` reload 保留 Runtime；
- changed generation retire old Runtime；
- corrupt replacement 保留 last-known-good；
- offline package replacement reconciliation；
- Start `可重复` checkbox；
- dummy `darkgrey_rpg:copper_coin` 删除；
- Storage Box 成功聊天清理；
- Canonical Actor / Item identity；
- Nominator server authority；
- Story → Session → Task → Settlement 基础链路。

---

# 1. B4 Scope

```text
A. Story Run Lifecycle
B. Story Start / Actor Interaction Arbitration
C. Canonical-only Production Runtime
D. Namespace Identity
E. Full Regression / Real Acceptance
```

明确不做视觉重设计：

```text
Dialogue UI 美化
Choice UI 美化
Entity Nominator UI 重做
Item Nominator UI 重做
Task HUD 美化
Portrait / 立绘
Minecraft GUI 全局风格统一
```

B4 允许新增一个**最低功能**的 Story Chooser，因为它是同 NPC 多 Story 路由必需功能；不要做视觉 polish。

---

# 2. A — Repeatable Story Run Lifecycle

## 2.1 Human-audit bug

```text
Run #1
→ kill_slimes
→ Task 0/3 → 1/3 → 2/3 → 3/3
→ Task SETTLED
→ Story TERMINATED

Run #2
→ Story repeat 成功
→ 旧 SETTLED Task 被复用
→ 不需要再次击杀
→ 直接进入结算
```

根因：

```text
Task identity
=
(playerUuid, storyId, taskPlacementId)
```

Run #1 / Run #2 key 相同，而 `Task.start()` 会返回 existing instance。

---

# 3. A — Reset Boundary

正式定义：

```text
Story END
≠ Reset

Repeatable Story NEW RUN START
= Reset Boundary
```

## 3.1 ACTIVE

```text
Story ACTIVE
```

再次命中 Start：

```text
不 reset
不重建 Story
不重建 Task
不清 progress
不重新 route 当前 child
```

## 3.2 TERMINATED, before repeat

Story 结束后继续保留：

```text
terminal Story snapshot
settled Task result
Task completion
Story public Logic
```

这些可能被其他 Story / Cross-Story Logic / Journal / Debug 读取。

## 3.3 REPEATABLE_RESTART

只有：

```text
previous Story terminal
AND
repeat_policy = repeatable
AND
本次 Start 条件有效
AND
新 Story run 确实创建
```

才 reset 上一轮 run-scoped state。

---

# 4. A — Exact Reset Scope

粒度：

```text
exact playerUuid
+
exact storyId
```

清：

```text
previous Session instance
pending Session continuation
Task instances:
  ACTIVE
  SETTLED
  CANCELLED
  ERROR

Task objective progress
Task result port
Task Logic state
Task subscription-index entries
```

不清：

```text
Nominator binding
NpcIdentitySavedData
Item binding
Minecraft Entity
ItemStack / 背包
世界状态
其他玩家
其他 Story
Package Generation registry
```

新增：

```text
CanonicalTaskSavedData.discardByPlayerStory(UUID playerUuid, String storyId)
```

或职责完全等价的 targeted purge。

不得用全玩家的：

```text
discardByStoryIds(...)
```

---

# 5. A — Do Not Patch Task.start()

禁止：

```text
if existing Task is SETTLED:
    auto-create new Task
```

Task layer 不拥有 Story Run 生命周期。

---

# 6. A — Explicit Start Disposition

增加最小分类，例如：

```text
NEW
ALREADY_ACTIVE
ONCE_TERMINAL
REPEATABLE_RESTART
```

只有：

```text
REPEATABLE_RESTART
```

触发 previous-run reset。

---

# 7. B — Start Is Entry Only

用户已确认：

> 一个 Story 已经在运行后，绝对不能中途再次匹配自己的 Start。

正式规则：

```text
Story absent
→ Start eligible

Story ACTIVE
→ this Story's Start NOT eligible

Story TERMINATED + once
→ Start NOT eligible

Story TERMINATED + repeatable
→ Start eligible
```

最好在候选匹配阶段直接排除 ACTIVE Story，而不是先 start 再 no-op。

---

# 8. B — Active Start Regression

Story：

```text
Start: 与酒馆老板交互
↓
发布任务 Session
↓
Task: 击杀3只史莱姆
```

玩家当前：

```text
Story ACTIVE
Task 2/3
```

再次点击启动 NPC：

```text
不得重新触发 Start
不得重新打开发布任务 Session
不得重复 route Task
Task仍2/3
```

---

# 9. B — Actor Interaction Arbitration

不采用：

```text
NPC占用
专注模式
NPC lock
```

采用：

> 同一角色同时存在多个合法 Story 候选时，让玩家选择本次进行哪个 Story。

---

# 10. B — Actor Candidates

一次 Actor interaction 收集：

## A. Active continuation

```text
Story ACTIVE
当前 cursor 正等待这个 Actor
```

## B. New Story Start

```text
Story 尚未开始
Start Actor = 当前 Actor
其他 Start 条件满足
```

## C. Repeat Start

```text
Story TERMINATED
repeatable
Start Actor = 当前 Actor
其他 Start 条件满足
```

如果 Story ACTIVE，则它自己的 Start 绝不进入候选；但它当前 cursor 真正在等待该 Actor 时，可以以 Active continuation 身份进入候选。

---

# 11. B — Candidate Count

```text
0 candidate
→ DGR不消费交互

1 candidate
→ Server直接执行

2+ candidates
→ 弹 Story Chooser
→ 玩家选择
→ Server重新验证
→ 只执行所选 Story
```

未选择项保持原状态。

---

# 12. B — Minimal Story Chooser

B4 只做功能版，显示：

```text
Story Display Name
完整 Story_ID
状态：
  继续
  开始
  重新开始
```

优先使用普通 Minecraft 1.7.10 GUI 控件。

不做：

```text
自定义视觉体系
Dialogue风格
动画
Portrait
```

---

# 13. B — Server Authority

Client 不能任意提交 story_id 让 Server 盲执行。

正确：

```text
Server生成候选
→ Client选择
→ Server收到selection
→ 重新验证：
   player
   actor
   Story lifecycle
   candidate type
   current wait/trigger
→ 仍合法才消费
```

必须拒绝 stale selection。

---

# 14. B — No Destructive Ambiguity

多个 Story 同时等待一个 Actor：

```text
不再 Ambiguous → ERROR → cleanup
```

而是：

```text
2+ candidates
→ chooser
```

---

# 15. B — Interaction Cancellation

只有：

```text
DGR真正消费交互
OR
显示Story Chooser
```

才 cancel 原 Minecraft interaction。

如果：

```text
0 candidate
```

不得因为实体有 DGR Actor ID 就直接吞掉原版/其他 Mod 交互。

---

# 16. B — Region Ambiguity Safety

B4 的 chooser 只用于 Actor Interaction。

对于多个 Story 同时等待相同 Region：

```text
至少禁止 Ambiguous → ERROR / destructive cleanup
```

未定义最终 UX 前必须采用非破坏性处理，不要在 B4 新造 Region UI。

---

# 17. C — Canonical Only

用户确认：

> Legacy Story / Quest 旧式残余应迁移到新 Canonical 体系，不再长期双运行。

B4 目标：

```text
Canonical Story
Canonical Session
Canonical Task

= 唯一 production gameplay runtime
```

---

# 18. C — Legacy Production Audit

开工前把 Legacy 分三类：

### A. Old production execution

```text
old Story runtime
old Quest runtime
old gameplay event consumer
old Story/Quest state machine
```

目标：

```text
删除 / 完全退出 production path
```

### B. Compatibility reader / migration

如果仍需要打开旧开发数据：

```text
可保留只读迁移入口
```

但：

```text
不得订阅游戏事件
不得拥有 production runtime state
不得作为 /dgr fallback
```

### C. Temporary presentation adapter

如果某些旧 Journal UI 暂时仍显示 Canonical Task 数据：

```text
可以保留 presentation adapter
```

但：

```text
data source = Canonical
execution = Canonical
```

不得继续运行旧 Quest 状态机。

---

# 19. C — Single Event Path

B4 后这些 production event：

```text
Entity interact
Region/position
Kill
Pickup/item
Task objective
Session action
```

每一种只能存在一条权威 Canonical 执行路径。

禁止：

```text
legacy + canonical 双投递
```

---

# 20. C — /dgr Canonical-only

至少：

```text
/dgr story list
/dgr story info
/dgr story start
/dgr story state
/dgr story reset

/dgr session ...
/dgr task ...
```

统一操作 Canonical。

`/dgr story reset` 必须调用 Canonical exact player/story cleanup。

---

# 21. C — DGRS Compatibility

B4 不要求为了删除 Legacy execution 强行打碎 DGRS v1。

旧 parser / manifest 字段若仍用于迁移，可保留。

但：

```text
新 Studio 正常 authoring/export = Canonical
Minecraft production gameplay = Canonical only
```

---

# 22. D — Namespace Product Contract

完整 DGR ID：

```text
<namespace>:<local_id>
```

至少适用于：

```text
Story_ID
NPC_ID
Actor Group_ID
Item_ID
Item Group_ID
Session_ID
Task_ID
```

以及其他正式 author-facing DGR Resource ID。

---

# 23. D — No Hidden UUID

禁止：

```text
UI都是 boss
底层UUID区分
```

用户看到的完整 ID 本身就是可识别身份。

Studio / Nominator / DGRS / Debug / Log 都使用 full ID。

---

# 24. D — First-use Onboarding

第一次使用 Studio 且尚无 Namespace：

```text
必须先设置 NameSpace
+
创建第一个 Project
```

文案只说明：

> 建议使用自己的 Minecraft 游戏内 ID 作为 NameSpace，方便其他用户识别，并降低不同作者创建重复 ID 引发冲突的风险。

要求：

```text
不得出现任何具体用户名示例
不得出现开发者真实ID示例
不联网
不做正版验证
```

---

# 25. D — Global Namespace Storage

Global Namespace 存在 Studio settings。

当前已有：

```text
StudioSettings
SettingsService
```

优先扩展现有 settings persistence。

不新增账号系统 / 网络身份系统。

---

# 26. D — File Menu

入口：

```text
文件
→ NameSpace 设置...
```

不新增顶级“作者”标签页。

窗口只做核心功能：

```text
更改当前 NameSpace
```

---

# 27. D — Global vs Custom Story Namespace

默认：

```text
Story NamespaceMode = Global
```

全局 Namespace 修改时：

```text
所有 Global Story
+
这些 Story owned resources
```

一起迁移。

---

# 28. D — Story-level Namespace

故事图谱中：

```text
右键 Story
→ 更改 NameSpace...
```

设置后：

```text
NamespaceMode = Custom
CustomNamespace = ...
```

以后全局修改：

```text
不改变该 Story 自身 Namespace
不改变该 Story owned resources Namespace
```

---

# 29. D — Return to Global

右键增加：

```text
恢复跟随全局 NameSpace
```

行为：

```text
Custom → Global
```

并将 Story + owned resources 迁移到当前全局 Namespace。

---

# 30. D — Ownership Boundary

直接利用 Canonical Membership：

```text
ownedResources
referencedResources
```

Story Namespace change 修改：

```text
Story itself
owned Actors
owned Actor Groups
owned Items
owned Item Groups
owned Sessions
owned Tasks
其他 owned resources
```

不修改：

```text
referenced Actors
referenced Items
referenced Groups
referenced Sessions
referenced Tasks
其他 Story owned resources
```

---

# 31. D — References Follow Renames

Custom Story 不受全局 Namespace 修改，意思是：

```text
自己的 ID / owned resources 不改
```

但如果它引用的 Global-owned resource 被改名：

```text
old full ID → new full ID
```

Custom Story 的 reference 必须自动更新。

---

# 32. D — Typed Rename Map

禁止全文字符串替换。

必须构建：

```text
Resource Rename Map
old full resource ID
→
new full resource ID
```

然后更新：

```text
resource identity
Story graph refs
Membership
Story Logic refs
Session refs
Task refs
其他 typed refs
```

---

# 33. D — Atomic Migration

无论：

```text
global Namespace change
Story custom Namespace change
return-to-global
```

都必须：

```text
compute rename map
↓
detect all collisions
↓
compute all typed reference rewrites
↓
validate final project
↓
all PASS
↓
single commit
```

任意失败：

```text
project remains unchanged
```

推荐整个迁移为一个 Undo 单元。

---

# 34. D — Collision Rules

```text
A:boss
B:boss
```

合法。

```text
A:boss
A:boss
```

如果不是明确同一个 shared definition：

```text
collision
```

不得 silent overwrite。

---

# 35. D — Namespace Validation

建立唯一 canonical Namespace validator。

要求：

```text
non-empty
stable
':' reserved separator
跨平台安全
Studio / Exporter / Java Runtime 使用同一规则
不得 silent normalization 造成 UI 与 Runtime identity 不一致
```

开工先审计已有 ID validator，再统一。

---

# 36. D — Windows Filename Constraint

逻辑 full ID 可以是：

```text
namespace:local_id
```

但 Windows 文件名不能包含 `:`。

因此禁止：

```text
filename == full ID + ".json"
```

推荐物理布局：

```text
resources/actors/<namespace>/<local_id>.json
resources/items/<namespace>/<local_id>.json

resources/canonical/stories/<namespace>/<local_id>.json
resources/canonical/sessions/<namespace>/<local_id>.json
resources/canonical/tasks/<namespace>/<local_id>.json
resources/canonical/memberships/<namespace>/<local_id>.json
```

逻辑 ID 与物理路径必须解耦。

---

# 37. D — Stop Deriving ID from Filename

Namespaced identity 后：

```text
authoritative ID
=
parsed resource content / typed manifest identity
```

不再：

```text
basename(path) == resource ID
```

Manifest path 只用于寻找文件。

---

# 38. D — DGRS Filename

逻辑：

```text
story_id
package_id
```

可以 namespaced。

但 `.dgrs` physical filename 必须 filesystem-safe。

Runtime identity 读取：

```text
manifest.package_id
manifest.story_id
```

而不是依赖文件名。

---

# 39. D — Existing B3 Project Migration

当前旧 Project 是 bare IDs。

打开 unnamespaced Project：

```text
检测到旧式 ID
```

如果 Studio 尚无 Namespace：

```text
先要求设置 Namespace
```

然后：

```text
owned bare IDs
→ namespaced IDs
→ rewrite all typed refs
```

必须 atomic。

不得要求用户重新制作 Story。

不得 silent 改盘；做一次明确确认即可。

---

# 40. D — Cross-author References

创建自己的资源：

```text
默认使用 Story effective Namespace
```

引用其他人的资源：

```text
允许输入/选择 custom Namespace
```

创建和引用必须是不同语义：

```text
owned resource
≠
referenced resource
```

---

# 41. D — External Reference Across DGRS

Package B 可以：

```text
reference OtherNamespace:boss
```

但不拥有这个 Actor definition。

B4 必须允许该 Story 导出。

### Per-package validation

验证：

```text
own resource definitions
Story graph syntax
membership syntax
reference ID format
```

对 `referencedResources` 允许合法 unresolved external reference。

### Merged server package set

所有 accepted package 合并后：

```text
resolve every external reference
```

缺失：

```text
merged set invalid
```

不得 silent bind。

---

# 42. D — Last-known-good

若新 package 引入无法解析 external reference：

```text
merged validation FAIL
```

继续 B3：

```text
last-known-good snapshot remains
old Runtime not retired
```

---

# 43. D — Shared Resource Semantics

多个 Package 都引用 same full ID：

```text
正常共享
```

多个 Package 都携带 same full ID definition：

```text
byte-identical
→ shared

different
→ conflict
→ reject merge
```

不 silent overwrite。

---

# 44. D — Minecraft Uses Full IDs

至少审计并迁移：

```text
ProjectSnapshot
Actor resolver
NpcIdentitySavedData
ItemIdentitySavedData
Nominator
Story triggers
Story waits
Task objective identity
Membership
Story Logic
Session refs
Task refs
Package merge
/dgr commands
logs/debug
```

全部使用 namespaced full ID。

---

# 45. D — Nominator

Nominator 显示和保存 full NPC_ID / Group_ID / Item_ID。

不同 Namespace 相同 local_id 必须是不同候选，不能只用 local_id 判断已占用。

---

# 46. D — Namespace Change + B3 Generation

Namespace 修改重新导出后，资源 identity / Story identity / DGRS content 会变化。

不新增 Minecraft 世界迁移系统。

沿用 B3 Package Generation / Fingerprint，按实际 delta：

```text
UPDATED
REPLACED
REMOVED + ADDED
```

正确 retire old Runtime 并让新定义 authoritative。

---

# 47. Construction Order

## Phase 0
- checkout B3 HEAD；
- create B4；
- baseline Java + Studio tests。

## Phase 1
- repeat failing probe；
- targeted per-player/story purge；
- Start disposition；
- Run2 从0开始。

## Phase 2
- ACTIVE Story Start 在 eligibility 阶段排除；
- Task2/3 点击启动NPC仍2/3。

## Phase 3
- Actor candidate collector；
- 0/1/N；
- server revalidation；
- minimal Story Chooser。

## Phase 4
- Legacy production dependency map；
- 退役双执行；
- `/dgr` Canonical-only。

## Phase 5
- Namespace core model；
- global/custom mode；
- rename plan；
- atomic migration。

## Phase 6
- logical full ID / physical path 解耦；
- 删除 basename-derived ID 假设。

## Phase 7
- old Project migration；
- real `kill_slimes` migration。

## Phase 8
- Studio first-use；
- File → NameSpace 设置；
- Story context-menu custom / return-global；
- custom namespace reference。

## Phase 9
- external DGRS reference；
- merged-set validation。

## Phase 10
- Minecraft full-ID audit。

## Phase 11
- full real-machine vertical acceptance。

---

# 48. Required Automated Gates — Repeat

```text
STORY_REPEAT_RUN1_TASK_SETTLES=PASS
STORY_REPEAT_TERMINAL_RESULT_RETAINED=PASS
STORY_REPEAT_RUN2_RESETS_OLD_TASK=PASS
STORY_REPEAT_RUN2_STARTS_ZERO=PASS
STORY_REPEAT_RUN2_SETTLES_NORMALLY=PASS
STORY_REPEAT_RUN3_STARTS_ZERO=PASS

STORY_ACTIVE_START_NOT_ELIGIBLE=PASS
STORY_ACTIVE_START_DOES_NOT_REROUTE_TASK=PASS
STORY_ACTIVE_START_DOES_NOT_RESTART_SESSION=PASS

STORY_ONCE_TERMINAL_START_NOT_ELIGIBLE=PASS
STORY_REPEAT_PLAYER_ISOLATION=PASS
STORY_REPEAT_OTHER_STORY_ISOLATION=PASS
```

---

# 49. Required Automated Gates — Actor Arbitration

```text
ACTOR_NO_CANDIDATE_DOES_NOT_CONSUME=PASS
ACTOR_ONE_CONTINUATION_DIRECT=PASS
ACTOR_ONE_NEW_START_DIRECT=PASS
ACTOR_ONE_REPEAT_START_DIRECT=PASS
ACTOR_ACTIVE_STORY_OWN_START_EXCLUDED=PASS
ACTOR_MULTIPLE_CANDIDATES_CHOOSER=PASS
ACTOR_SELECTION_ONLY_ADVANCES_SELECTED_STORY=PASS
ACTOR_UNSELECTED_STORY_UNCHANGED=PASS
ACTOR_STALE_SELECTION_REJECTED=PASS
ACTOR_MULTIPLE_WAITS_NOT_ERROR=PASS
```

---

# 50. Required Automated Gates — Canonical-only

```text
PRODUCTION_ACTOR_EVENT_CANONICAL_ONLY=PASS
PRODUCTION_REGION_EVENT_CANONICAL_ONLY=PASS
PRODUCTION_TASK_EVENT_CANONICAL_ONLY=PASS
LEGACY_STORY_RUNTIME_NOT_REGISTERED=PASS
LEGACY_QUEST_RUNTIME_NOT_REGISTERED=PASS
DGR_STORY_STATE_CANONICAL=PASS
DGR_STORY_RESET_CANONICAL=PASS
```

---

# 51. Required Automated Gates — Namespace

```text
NAMESPACE_GLOBAL_PERSISTENCE=PASS
NAMESPACE_FIRST_USE_REQUIRED=PASS
NAMESPACED_ID_ROUNDTRIP=PASS
NAMESPACE_SAME_LOCAL_DIFFERENT_NAMESPACE_ALLOWED=PASS

NAMESPACE_GLOBAL_RENAME_GLOBAL_STORIES=PASS
NAMESPACE_GLOBAL_RENAME_SKIPS_CUSTOM_STORY=PASS
NAMESPACE_CUSTOM_STORY_RENAME=PASS
NAMESPACE_CUSTOM_TO_GLOBAL=PASS

NAMESPACE_RENAME_OWNED_RESOURCES=PASS
NAMESPACE_RENAME_DOES_NOT_RENAME_EXTERNAL_OWNERS=PASS
NAMESPACE_RENAME_UPDATES_REFERENCES=PASS
NAMESPACE_COLLISION_ATOMIC_ABORT=PASS
NAMESPACE_MIGRATION_UNDO_SINGLE_STEP=PASS
```

---

# 52. Required Gates — Storage / DGRS

```text
NAMESPACE_COLON_NOT_USED_AS_WINDOWS_FILENAME=PASS
NAMESPACED_RESOURCE_PATH_ROUNDTRIP=PASS
RESOURCE_ID_NOT_DERIVED_FROM_BASENAME=PASS

DGRS_NAMESPACED_STORY_EXPORT=PASS
DGRS_NAMESPACED_ACTOR_EXPORT=PASS
DGRS_NAMESPACED_ITEM_EXPORT=PASS

DGRS_EXTERNAL_REFERENCE_PACKAGE_LOAD=PASS
DGRS_EXTERNAL_REFERENCE_MERGED_RESOLVE=PASS
DGRS_EXTERNAL_REFERENCE_MISSING_REJECTS_MERGE=PASS

DGRS_SAME_LOCAL_DIFFERENT_NAMESPACE_COEXIST=PASS
DGRS_SAME_FULL_ID_DIFFERENT_DEFINITION_CONFLICT=PASS
```

---

# 53. B3 Regression Gates

```text
FINGERPRINT_SAME_CONTENT_STABLE
FINGERPRINT_REPEAT_POLICY_CHANGE_DETECTED
PACKAGE_GENERATION_UNCHANGED_PRESERVES_RUNTIME
PACKAGE_GENERATION_UPDATED_RETIRES_RUNTIME
PACKAGE_GENERATION_CORRUPT_PRESERVES_OLD
PACKAGE_OFFLINE_REPLACEMENT_RECONCILES
```

---

# 54. Real Studio Acceptance

## First use

```text
clean settings
→ launch
→ prompt NameSpace
→ 文案建议使用Minecraft游戏内ID
→ 不出现具体用户名示例
→ create first Project
```

重开后 Namespace persists。

## Global / Custom

准备：

```text
Story A = Global
Story B = Global
Story C = Custom
```

修改 File → NameSpace：

```text
A changed
B changed
C own namespace unchanged
```

若 C 引用了 A-owned resource，则 C reference 自动更新。

## Story custom

右键 Story B → 更改 NameSpace：

```text
B + B owned resources changed
B referenced resources unchanged
```

再改全局，B remains custom。

右键 → 恢复跟随全局，B 迁到 current global namespace。

---

# 55. Real Old-project Migration

打开真实 B3 `kill_slimes` bare-ID 工程。

迁移后：

```text
Story
Actor
Group
Item
Session
Task
Membership
all typed references
```

全部正常。

要求：

```text
不重做节点
graph connection保留
export succeeds
```

---

# 56. Real Minecraft — Repeat

```text
Run1:
0→1→2→3
→交任务
→Story结束

结束后:
old result仍可查

Run2:
→Task 0/3
→0→1→2→3
→完成

Run3:
→Task 0/3
```

---

# 57. Real Minecraft — Active Start

```text
Story ACTIVE
Task 2/3
```

再点启动 NPC：

```text
不回Session
不重Start
不reroute Task
仍2/3
```

---

# 58. Real Minecraft — Multi Story One NPC

两个合法 Story 同时引用同一 Actor。

点击 Actor：

```text
Story Chooser
```

选 A：

```text
A advances
B unchanged
```

再次交互仍可选择 B。

---

# 59. Real Minecraft — Namespace Coexist

两个独立 DGRS：

```text
different Namespace
same local actor id
```

同时安装：

```text
both load
no conflict
Nominator displays two distinct full IDs
```

---

# 60. Real Minecraft — External Reference

Consumer Package 不拥有 Actor X，只 reference：

```text
OtherNamespace:X
```

Provider + Consumer 同时安装：

```text
merge succeeds
consumer Story recognizes same Actor
```

删除 provider：

```text
merged validation fails safely
no silent wrong binding
last-known-good preserved where applicable
```

---

# 61. NO-GO

任一成立，B4 不允许 Agent Verified：

1. Story TERMINATED 就永久删除 settled Task；
2. Run2 仍继承 Run1 Task完成状态；
3. ACTIVE Story仍可匹配自己的Start；
4. ACTIVE Story点击Start NPC重新route Session/Task；
5. 多Story同NPC仍 Ambiguous→ERROR；
6. chooser由client任意story_id直接驱动，无server revalidation；
7. 0 candidate仍无条件取消原NPC交互；
8. production gameplay仍双投递Legacy+Canonical；
9. `/dgr story state/reset`仍操作Legacy；
10. 新Studio继续创建bare ID；
11. 使用隐藏UUID代替用户可见full ID；
12. 不同Namespace相同local ID仍冲突；
13. Namespace修改使用全文字符串替换；
14. Custom Story被全局修改自身owned资源；
15. 被改名resource的真实引用断裂；
16. Story级改Namespace误改external referenced resource；
17. Namespace migration半成功；
18. full ID直接作为Windows含colon文件名；
19. Runtime继续从basename推导full ID；
20. external namespaced reference无法跨DGRS resolve；
21. external reference缺失时silent ignore；
22. B3 fingerprint/generation回归失败；
23. B4顺手重做Dialogue/Nominator视觉；
24. Codex标记`USER_ACCEPTED`。

---

# 62. Development Report

产出：

```text
PLAN/0.3.2.0_B4_DEVELOPMENT_REPORT.md
```

必须记录：

- B3 baseline / B4 final HEAD；
- Repeat Run1 / terminal retain / Run2 0 / Run2完成 / Run3 0；
- ACTIVE Start excluded；
- Actor candidates + chooser + stale selection；
- Legacy production paths removed / migration-only remnants；
- Namespace validator / full-ID format / path mapping；
- Global / Custom / return-global migration；
- owned / referenced rewrite evidence；
- old Project migration；
- DGRS external reference provider / consumer；
- B3 generation/fingerprint regression；
- real Minecraft acceptance。

最终状态只能：

```text
IMPLEMENTED
AGENT_VERIFIED
NEED_USER_VERIFICATION
USER_ACCEPTED=NO
```

---

# 63. Definition of Done

B4 完成意味着：

1. Repeatable 每轮 Task 真正独立；
2. Story结束结果保留到下一轮开始；
3. ACTIVE Story Start完全失去资格；
4. 同NPC多Story由玩家选择；
5. Canonical是唯一production runtime；
6. Legacy不再双重消费游戏事件；
7. 首次使用Studio必须设置全局NameSpace；
8. File菜单可修改全局NameSpace；
9. Story可右键设置Custom NameSpace；
10. Custom Story不被全局改自身owned资源；
11. 可恢复跟随全局；
12. Namespace迁移自动处理owned resources；
13. 所有真实references随目标identity更新；
14. 不误改external referenced resources；
15. full namespaced ID是用户可见唯一identity；
16. physical path与logical full ID正确解耦；
17. 不同Namespace相同local ID可共存；
18. external namespaced reference可跨DGRS resolve；
19. B3 Package Generation/Fingerprint继续正确；
20. full automated + real vertical slices PASS；
21. 等待用户人工验收。

---

# 64. Exit Gate to 0.3.2.1

只有用户明确：

```text
0.3.2.0_B4 USER_ACCEPTED
```

才：

```text
freeze Studio↔DGRS↔Runtime architecture
```

进入：

```text
0.3.2.1
```

下一阶段重点：

```text
Dialogue bottom-box UI
Speaker name / text
future portrait slot
Choice centered floating list
Entity Nominator two-level menu
Item Nominator vanilla-style container
Task HUD / gameplay presentation
other Minecraft client UX
```

---

# 65. Short Instruction for Codex

> 从 `codex/0.3.2.0_B3 @ 805fed51913c637fe3a8c730f7310b2f5d114b2d` 创建 B4。B4 是 `0.3.2.0` 最终 Architecture Closure，必须同时完成：① Repeatable Story Run reset：Story终止后保留上一轮结果，只有 repeatable Story 真正开始下一轮时，才按 exact player+story 清上一轮 Session/Continuation 和全部 TaskInstance/Objective progress/result/index；不得在 Task.start() 内猜新一轮。② Start/Actor routing：Story ACTIVE 后自己的全部 Start Trigger 在候选阶段排除；Actor interaction 收集 Active continuation / new Start / repeat Start 候选，0项不消费、1项直达、2+项弹最小 Story Chooser；Server必须重验 selection；多个Story同NPC不再 Ambiguous→ERROR。③ Legacy production execution 退役：真实 Minecraft event 只进入 Canonical Story/Session/Task；旧 StoryRuntime/QuestRuntime/event bus 不得双执行；`/dgr story state/reset/start`统一 Canonical；仅允许必要的旧格式只读迁移代码或临时 presentation adapter 留存。④ Namespace：首次使用 Studio 必须设置全局 NameSpace并创建第一个Project，引导只说明建议使用Minecraft游戏内ID，不出现任何具体用户名示例；入口 `文件 → NameSpace 设置`，修改后所有 Global Story + owned resources 改前缀；Story 图谱右键可 `更改 NameSpace` 进入 Custom，Custom Story不受后续全局修改自身owned资源影响，并提供 `恢复跟随全局 NameSpace`。逻辑 full ID 为 `<namespace>:<local_id>`，不使用隐藏UUID。Namespace迁移必须基于 Canonical Membership 的 owned/referenced 边界构建 typed rename map，自动更新所有真实引用，禁止全文字符串替换并必须 atomic。Windows 文件名不能含冒号，因此 logical full ID 与 physical path 必须解耦，并删除“从basename推导resource ID”的新格式假设。现有B3无Namespace项目必须可一次迁移，不得要求重做Story。跨作者引用允许在 reference 中使用自定义Namespace；per-package validation允许合法 unresolved external referencedResources，最终 installed package set merge时必须全部解析，缺失provider则安全拒绝并保留last-known-good。所有 Minecraft Nominator/SavedData/Story/Task/Session/commands 使用full namespaced ID。重新导出后沿用B3 Package Generation/Fingerprint处理新旧定义。B4只做Story Chooser最低功能UI，其余Dialogue/Nominator/Task视觉全部留给0.3.2.1。最终 full Java + Studio tests + real `kill_slimes` Run1/Run2/Run3 + active-start + multi-Story-one-NPC + Namespace coexist/external-reference垂直验收通过后，只能标记 AGENT_VERIFIED，等待用户人工验收。
