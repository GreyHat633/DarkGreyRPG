# DarkGrey RPG 0.3.2.0_B4 Construction PLAN

> **定位：0.3.2.0 Studio ↔ Minecraft 架构收口的最后一轮**
>
> B4 只修复一个人工审计确认的架构问题：
>
> **Repeatable Story 开启新一轮运行时，没有清理上一轮 Task / Session 等 run-scoped Runtime State，导致新一轮任务继承上一轮完成状态。**
>
> B4 通过用户人工验收后，0.3.2.0 的 Studio ↔ Minecraft 核心架构正式冻结，随后进入 `0.3.2.1`，集中处理 Minecraft 客户端 UI、表现和易用性。

---

# 0. Baseline

```text
B3 branch:
codex/0.3.2.0_B3

B3 HEAD:
805fed51913c637fe3a8c730f7310b2f5d114b2d

B4 branch:
codex/0.3.2.0_B4
```

B4 从 B3 继续，不回滚 B3 已完成的：

- DGRS direct-read；
- Package Content Fingerprint；
- Package Generation lifecycle；
- same-generation reload preserve；
- updated generation retire；
- corrupt replacement last-known-good；
- offline replacement reconciliation；
- Story Start `可重复` checkbox；
- dummy `darkgrey_rpg:copper_coin` 删除；
- Storage Box 成功聊天提示删除；
- Canonical Identity；
- `/dgr`；
- Story → Session → Task → Settlement 基础链路。

---

# 1. Human Audit Finding

真实 bug：

```text
Repeatable Story Run #1
→ Task “击杀3只史莱姆”
→ 0/3 → 1/3 → 2/3 → 3/3
→ Task SETTLED
→ Story TERMINATED
→ 正常获得奖励

Repeatable Story Run #2
→ Story 本身可以重新开始
→ 旧 Task completion 仍然存在
→ 新一轮到达同一个 Task placement 时复用旧 SETTLED Task
→ 玩家不需要再次击杀
→ Story 直接进入结算/奖励
```

这不是 UI 问题，而是：

```text
Story Run Lifecycle
```

问题。

---

# 2. Root Cause — 必须先确认

## 2.1 Story restart 已经正确

当前 `CanonicalStoryInstanceStore` 以：

```text
(playerUuid, storyId)
```

持有最多一个 Story instance。

terminal `REPEATABLE` Story 再次 start 时：

```text
旧 StoryInstance
→ 被新的 StoryInstance 替换
```

所以 B4 **不要重新实现 repeat policy**。

## 2.2 Task identity 仍然复用同一个 key

当前 `CanonicalTaskInstanceStore` key：

```text
(playerUuid, storyInstanceId, taskNodePlacementId)
```

但现有 coordinator 实际传入的 `storyInstanceId` 就是稳定的 `storyId`。

所以 Run #1 与 Run #2：

```text
player = same
story  = kill_slimes
placement = same
```

Task key 完全相同。

## 2.3 Task.start() 会返回旧实例

当前语义：

```text
if same Task key already exists:
    return existing
```

不会因为它已经：

```text
SETTLED
CANCELLED
ERROR
```

就自动新建。

这个行为**不能直接改掉**。

因为 Task 自己无法判断：

```text
这是新的 Story Run
```

还是：

```text
同一 Story Run 内再次到达同一个 placement
```

## 2.4 Story router 会消费旧 SETTLED Task

现有 routing：

```text
Story reaches Task placement
↓
tasks.start(...)
↓
returns old SETTLED Task
↓
Story sees SETTLED
↓
resumeTask(...)
↓
直接继续 Story Flow
```

这就是第二轮任务被直接跳过的直接根因。

---

# 3. Why B3 Missed It

B3 的 repeatable 实机验收主要走：

```text
进入 Story
→ 选择“拒绝”
→ Story TERMINATED
→ 再次交互
→ 新 Story run 创建成功
```

这证明了：

```text
StoryInstance repeat
```

但没有经历真实 Task settlement，所以没有证明：

```text
Story child Runtime reset
```

B4 必须把“完整 Task 完成后 repeat”变成 Release Gate。

---

# 4. B4 Architecture Decision

B4 不引入：

```text
动态 story_id
随机 StoryRunId
新的 DGRS schema
复杂 Run History
Graph migration
```

B4正式定义：

# Repeat Start = Reset Boundary

语义：

```text
Story TERMINATED
→ 保留上一轮结果

直到：
该 REPEATABLE Story 真正成功开始下一轮

此刻：
上一轮 run-scoped Runtime State reset
→ 新一轮从初始状态运行
```

---

# 5. Three Lifetime Phases

## 5.1 ACTIVE

Story 仍是：

```text
ACTIVE
```

再次触发 Start：

```text
不 reset
不清 Task progress
不创建第二个 run
```

例如：

```text
Task 2/3
再次点击老板
→ 仍是2/3
```

## 5.2 TERMINATED but not repeated yet

Story 已：

```text
TERMINATED
```

此时**不要清数据**。

保留：

```text
terminal Story snapshot
settled Task snapshot
Task result
Task completion state
Story public logic
```

原因：

这些结果可能被：

```text
Cross-Story Logic
后续剧情
Command / Journal / Debug
```

读取。

用户的典型例子：

```text
森林狩猎
→ 可选目标：拯救公主
→ 森林狩猎结束
→ “公主是否被救”仍可开启“王国密令”
```

所以：

> Story end 不是 reset boundary。

## 5.3 REPEATABLE_RESTART

只有：

```text
旧 Story terminal
AND
repeatable
AND
Start Trigger 成功
AND
确实创建新 Story run
```

此时才 reset 上一轮 run-scoped state。

---

# 6. Reset Scope

仅清：

```text
exact playerUuid
+
exact storyId
```

上一轮 Runtime。

## 6.1 Story

旧 terminal Story：

```text
由现有 repeat start 自然替换
```

不要把新 Story 再删掉。

## 6.2 Session

清：

```text
Session child
pending continuation
pending Story/Session handoff
```

优先复用当前：

```text
CanonicalSessionSavedData.cancelStoryChildren(playerUuid, storyId)
```

## 6.3 Task

永久删除该 exact player/story 的：

```text
ACTIVE Task
SETTLED Task
CANCELLED Task
ERROR Task
objective progress
result port
runtime logic state
subscription index entries
```

这才是本次 bug 的核心修复。

---

# 7. Must Preserve

Repeat Start 不得删除：

```text
Nominator entity binding
NpcIdentitySavedData
NPC_ID / Group_ID mapping
Item identity binding
玩家背包
Minecraft Entity
世界状态
Package Generation registry
其他 Story Runtime
其他玩家的同 Story Runtime
```

例如：

```text
tarven_boss 已被指名
```

Run #2：

```text
无需重新 Nominator
```

---

# 8. Per-player / Per-story Purge

B3 有：

```text
discardByStoryIds(...)
```

它用于 Package generation change，粒度是：

```text
整个 Story across all players
```

B4 repeat reset 不能用它。

新增：

```text
CanonicalTaskSavedData.discardByPlayerStory(UUID playerUuid, String storyId)
```

或等价 targeted API。

必须：

```text
只删 exact player/story
同步移除 Task subscription index
正确 markDirty
```

---

# 9. Do NOT Change Task.start()

禁止：

```text
if existing is SETTLED:
    replace with new Task
```

原因：

Task layer 不拥有 Story Run 生命周期。

Reset 必须由：

```text
Story repeat lifecycle
```

发起。

---

# 10. Explicit Start Disposition

B4 不要通过：

```text
activationTime 是否变化
```

来猜是否 repeat。

推荐增加一个极小的 Start disposition：

```text
NEW
ACTIVE_REENTRY
ONCE_TERMINAL_BLOCKED
REPEATABLE_RESTART
```

或同等命名。

它只负责回答：

> 本次 Start 到底是什么生命周期事件？

不要扩张成新状态机。

---

# 11. Correct Reset Timing

正确顺序：

```text
Start Trigger arrives
↓
验证当前 Story / Trigger
↓
Story start 成功
↓
明确 disposition
↓
如果 REPEATABLE_RESTART:
    reset exact player/story previous children
↓
然后才 route 新 Story
↓
启动新的 Session / Task
```

关键：

```text
新 Story run 已确认
但新的 Task 尚未 tasks.start()
```

旧 Task 必须在这个窗口删除。

禁止：

```text
收到交互
→ 先删旧数据
→ 后发现 Start trigger 不成立
```

---

# 12. Coordinator Boundary

B4 优先在：

```text
CanonicalStoryForgeManager
```

协调 reset。

因为这里同时拥有：

```text
Story service
Session manager
Task manager
```

不要让：

```text
CanonicalStoryRuntime
```

直接依赖 Forge SavedData。

保持 pure runtime 与 persistence 分离。

---

# 13. AggregateGateway

当前 trusted routing seam 已有：

```text
startSession
startTask
executeAction
cleanup
```

可增加一个窄职责：

```text
resetForRepeat(storyId)
```

production 行为：

```text
sessions.cancelByStory(player, storyId)
tasks.discardByPlayerStory(player, storyId)
```

probe 同样走此 contract。

不要创建新的大型 lifecycle framework。

---

# 14. All Start Surfaces

不能只修老板交互。

所有能产生 Story start 的入口必须共享规则：

```text
startByActor
startByRegion
startByLogic
startByEntry
Enter Story / transferred target
Cross-Story Logic start
```

任一入口出现：

```text
REPEATABLE_RESTART
```

都必须 reset previous run children。

---

# 15. Story Termination Semantics Remain

当前 termination cleanup 可以继续：

```text
Session active child → cancel/remove
Task active child → cancel
```

但**不能**改成：

```text
Story一结束
→ discard all Task snapshots
```

settled Task result 必须保留到下一轮 repeat start。

---

# 16. Post-Run Result Window

正式定义：

```text
Run #1 TERMINATED
↓
结果可查询
↓
等待下一次 repeat
```

这个期间：

```text
Story terminal state retained
Task settled state retained
public logic retained
```

窗口结束于：

```text
REPEATABLE_RESTART
```

---

# 17. Cross-Story Regression

自动化至少做：

```text
Story A = repeatable forest_hunt
Run1:
rescue_princess completed
Story A TERMINATED
```

在未 repeat 前：

```text
result still queryable
```

Story B：

```text
kingdom_secret
```

可以依据 retained Story A result/public logic 启动或消费。

然后 Story A Run2 开始：

```text
Story A old run state reset
rescue_princess initial again
```

但：

```text
Story B 已存在 Runtime
不得被删除
```

---

# 18. Real Minecraft Release Gate — kill_slimes

必须使用用户现有真实：

```text
kill_slimes
```

作为主要证据。

## Run #1

```text
可重复 = true
↓
与 tarven_boss 交互
↓
接受任务
↓
Task ACTIVE 0/3
↓
真实击杀
0→1→2→3
↓
Task SETTLED
↓
交任务 / reward
↓
Story TERMINATED
```

## Terminal retention

在第二轮开始前，先证明：

```text
旧 Task SETTLED 仍可查询
旧 completion/result 仍存在
```

避免错误修成“Story end 就清”。

## Run #2 start

再次与：

```text
同一个 tarven_boss
```

交互。

要求：

```text
无需重新指名
REPEATABLE_RESTART
old Task state purged
new Story run starts
```

## Run #2 Task

再次接受任务。

必须：

```text
Task ACTIVE
progress = 0/3
```

然后真实：

```text
0→1→2→3
```

完成第二轮。

## Run #3 sanity

再开始第三轮。

至少证明：

```text
Task = 0/3
```

用于证明不是一次性修补。

---

# 19. Negative Gates

## Active re-entry

```text
Story ACTIVE
Task 2/3
再次触发 Start
```

结果：

```text
仍2/3
```

## ONCE

```text
once Story TERMINATED
再次触发 Start
```

结果：

```text
不 restart
不 reset result
```

## Player isolation

```text
Player A Story S = terminal
Player B Story S = active 2/3
```

A repeat：

```text
A fresh
B still 2/3
```

## Story isolation

同一玩家：

```text
Story A repeat
Story B active
```

A reset：

```text
B 不动
```

---

# 20. Restart Gates

## Terminal before repeat

```text
Run1 TERMINATED
Task SETTLED
server restart
```

重连：

```text
terminal result still retained
```

只有随后真正 repeat：

```text
才 reset
new Task 0/3
```

## Active new run

Run2：

```text
Task 1/3
server restart
```

必须：

```text
仍1/3
```

restart 不是 repeat reset。

---

# 21. Package Generation Remains Orthogonal

B3：

```text
Package UPDATED / REPLACED / REMOVED
→ generation lifecycle
→ affected Story runtime retirement
```

B4：

```text
REPEATABLE_RESTART
→ exact player/story run reset
```

两者是不同维度。

不要把 repeat reset 塞进 Package Generation。

继续保持：

```text
unchanged /dgr reload
→ active Task progress preserve

corrupt replacement
→ last-known-good + progress preserve
```

---

# 22. No Studio Changes

B3 已经完成：

```text
可重复 checkbox
```

B4：

```text
studio/**
```

重新冻结。

最终：

```text
git diff 805fed51913c637fe3a8c730f7310b2f5d114b2d...<B4_HEAD> -- studio/
```

预期：

```text
empty
```

---

# 23. No Minecraft UI Work

B4 不处理：

```text
Dialogue UI
Choice UI
Entity Nominator UI
Item Nominator UI
Minecraft GUI style
Portrait
Task HUD visual polish
```

这些正式留到：

```text
0.3.2.1
```

---

# 24. Recommended Order

## Phase 0
- 从 B3 创建 B4；
- full build baseline。

## Phase 1
先写一个能稳定复现当前 bug 的 probe：

```text
Run1 Task SETTLED
→ repeat
→ second Task currently SETTLED
```

未修复代码必须先 FAIL。

## Phase 2
新增 exact Task purge：

```text
discardByPlayerStory
```

## Phase 3
增加最小 Story Start disposition。

## Phase 4
把 repeat reset 放到：

```text
successful new repeat run
→ before child routing
```

## Phase 5
覆盖全部 Start surface。

## Phase 6
跑：
- cross-story；
- active re-entry；
- once；
- player isolation；
- story isolation；
- restart。

## Phase 7
真实 Minecraft `kill_slimes` Run1 / Run2 / Run3。

## Phase 8
完整回归 B3 fingerprint / generation / B2 identity / existing tests。

---

# 25. Required Automated Outputs

至少：

```text
STORY_REPEAT_FULL_TASK_RUN1_SETTLES=PASS
STORY_REPEAT_TERMINAL_RESULT_RETAINED=PASS
STORY_REPEAT_NEW_RUN_RESETS_TASK=PASS
STORY_REPEAT_NEW_RUN_TASK_STARTS_ZERO=PASS
STORY_REPEAT_SECOND_RUN_COMPLETES_NORMALLY=PASS
STORY_REPEAT_THIRD_RUN_STARTS_ZERO=PASS

STORY_ACTIVE_REENTRY_PRESERVES_PROGRESS=PASS
STORY_ONCE_REENTRY_PRESERVES_TERMINAL_RESULT=PASS

STORY_REPEAT_PLAYER_ISOLATION=PASS
STORY_REPEAT_OTHER_STORY_ISOLATION=PASS

STORY_REPEAT_TERMINAL_RESTART_RETENTION=PASS
STORY_ACTIVE_RUN_RESTART_PRESERVES_PROGRESS=PASS

STORY_REPEAT_CROSS_STORY_RESULT_WINDOW=PASS
```

---

# 26. Existing B3 Gates Must Remain Green

至少：

```text
PACKAGE_GENERATION_UNCHANGED_PRESERVES_RUNTIME
PACKAGE_GENERATION_UPDATED_RETIRES_RUNTIME
PACKAGE_CORRUPT_REPLACEMENT_PRESERVES_OLD
PACKAGE_OFFLINE_REPLACEMENT_RECONCILES_ON_START

FINGERPRINT_SAME_CONTENT_STABLE
FINGERPRINT_REPEAT_POLICY_CHANGE_DETECTED
```

以及全部已有 Java / Studio tests。

---

# 27. NO-GO

以下任一成立，B4 不允许 Agent Verified：

1. Story TERMINATED 时立即删除所有 settled Task；
2. repeat 开始前旧结果已经消失；
3. Run2 仍从 3/3 / SETTLED 开始；
4. `Task.start()` 被改成 SETTLED 自动重建；
5. active re-entry 导致 progress reset；
6. ONCE re-entry 导致 terminal result 被清；
7. Player A repeat 清 Player B；
8. Story A repeat 清 Story B；
9. Nominator / Item bindings 被 reset；
10. server restart 被当成 repeat；
11. unchanged reload 清 progress；
12. B3 Package Generation lifecycle 被破坏；
13. 新增动态 Story ID / random Run ID；
14. 引入 run-history / graph-migration framework；
15. 修改 `studio/**`；
16. 顺手修改 Minecraft UI；
17. Codex 标记 `USER_ACCEPTED`。

---

# 28. Development Report

产出：

```text
PLAN/0.3.2.0_B4_DEVELOPMENT_REPORT.md
```

必须包含：

## Root Cause
明确写：

```text
Story repeat creates new StoryInstance
Task key remains same player/story/placement
Task start reuses prior SETTLED instance
Story router immediately consumes settled result
```

## Reset Boundary

```text
Story end → retain
Repeat start → reset
```

## Exact Scope
列清：

```text
removed
preserved
```

## Real Evidence

必须是真实：

```text
Run1 0→1→2→3
terminal result retained
Run2 starts 0
Run2 0→1→2→3
Run3 starts 0
```

不能再只用“拒绝任务”证明 repeat。

## Isolation
- player isolation；
- story isolation。

## Restart
- terminal retain across restart；
- repeat then reset；
- active run progress across restart。

## Regression
- B3 generation / fingerprint；
- B2 Identity；
- full build。

## Studio Freeze

```text
studio/** diff = 0
```

---

# 29. Definition of Done

B4 完成意味着：

1. Story termination 保留上一轮结果；
2. Repeatable Story 新一轮开始成为明确 reset boundary；
3. 上一轮 settled Task 不再污染下一轮；
4. 新一轮 Task 从 `0/required` 开始；
5. Session / continuation stale child 同步清理；
6. reset 只针对 exact player/story；
7. active re-entry 不 reset；
8. ONCE terminal 不 reset；
9. restart 不 reset active run；
10. terminal result 跨 restart 保留；
11. repeat 后才 reset；
12. Package Generation lifecycle 不受影响；
13. Nominator/world bindings 不受影响；
14. `studio/**` 不改；
15. Minecraft UI 不改；
16. full regression PASS；
17. real `kill_slimes` Run1/Run2/Run3 PASS；
18. 等待用户人工验收。

---

# 30. Exit Gate to 0.3.2.1

B4 是：

```text
0.3.2.0 Studio ↔ Minecraft architecture closure
```

只有用户明确验收后：

```text
0.3.2.0 architecture = frozen
```

然后进入：

```text
0.3.2.1
```

0.3.2.1 重点：

```text
Minecraft Dialogue UI
Choice UI
Entity Nominator UI
Item Nominator UI
Minecraft-native tool GUI style
Task display / client presentation
其他 gameplay-facing UX / visual polish
```

---

# 31. Status Rule

Codex 最多：

```text
0.3.2.0_B4
IMPLEMENTED
AGENT_VERIFIED
NEED_USER_VERIFICATION
```

只有用户可以：

```text
USER_ACCEPTED
```

---

# 32. Short Instruction for Codex

> 从 `codex/0.3.2.0_B3 @ 805fed51913c637fe3a8c730f7310b2f5d114b2d` 创建最后的 B4。B4 只修 Repeatable Story 的 per-run Runtime reset，不做任何 Minecraft UI，也不改 Studio。先复现真实 bug：第一轮 `kill_slimes` Task 0→3 完成并让 Story TERMINATED，第二轮 repeat 时旧 SETTLED Task 因 `(player, storyId, placement)` key 相同被 `Task.start()` 复用，Story router看到 SETTLED 后直接继续，因此第二轮无需击杀。不要在 Story termination 时清数据：terminal Story/Task result 必须保留，供 Cross-Story Logic/后续查询使用；真正 reset boundary 是“REPEATABLE Story 成功创建下一轮运行”的瞬间。不要修改 `Task.start()` 为 SETTLED 自动重建，也不要引入动态 Story ID/随机 Run ID。建立一个最小、明确的 Story start disposition（NEW / ACTIVE_REENTRY / ONCE_TERMINAL_BLOCKED / REPEATABLE_RESTART 或等价）；只有 `REPEATABLE_RESTART` 成功后、且在新 Session/Task routing 之前，清理 exact player+story 的上一轮 Session child、pending continuation、全部 TaskInstance（包括 SETTLED/CANCELLED/ERROR）、Task progress/result/logic 和 subscription index；新的 StoryInstance由现有 repeat start 自然替换旧 terminal Story。新增 `CanonicalTaskSavedData.discardByPlayerStory(UUID, storyId)` 或等价 targeted purge，不能使用全玩家 `discardByStoryIds`。不得删除 Nominator/NPC/Item bindings、世界实体、玩家物品、其他 Story、其他玩家。所有 Start surface（Actor/Region/Logic/Entry/Transfer/Cross-Story）必须共享同一 reset 规则。真实验收必须用现有 `kill_slimes`：Run1 完整击杀3只→结算→Story结束；在 repeat 前先证明 terminal Task result 仍可查询；Run2 开始后必须 0/3，再真实0→1→2→3；Run3 至少证明重新从0/3开始。另验证 active re-entry 不重置2/3、ONCE不重置终态、Player/Story隔离、terminal状态跨restart保留且repeat后才清、active run跨restart保留、unchanged `/dgr reload`仍保留progress、B3 fingerprint/generation lifecycle全部回归通过。B4 `studio/**` diff必须为0。完成后只能报告 AGENT_VERIFIED，等待用户人工验收；用户通过后才正式进入0.3.2.1。
