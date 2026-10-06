# DarkGrey_RPG 0.3.2.0_B — Studio ↔ Minecraft Integration Construction PLAN

**Project:** DarkGrey_RPG / DGR  
**Target Version:** `0.3.2.0_B`  
**Target Branch:** `codex/0.3.2.0_B`  
**Construction Baseline:** user-accepted `codex/0.3.2.0_A`  
**PLAN-time verified A HEAD:** `782a78d28ae49efd731ff61e8cb116bebf870dd1`  
**Primary Human Audit:** `0.3.2.0_A审计(1).docx`

---

# 0. Executive Contract

`0.3.2.0_A` 的目标是证明：

```text
Studio 可以 Author
→ 可以保存 Canonical Graph
→ 可以导出有效 DGRS v1
```

`0.3.2.0_B` 的目标是证明另一半：

```text
Frozen Studio
→ 导出 .dgrs
→ Minecraft 直接读取 .dgrs
→ 完整得到 Story / Actor / Item / Session / Task
→ Nominator 能直接选择这些资源
→ Story / Session / Task 在真实游戏中完整运行
```

因此 B 是 **Integration / Runtime Bridge** 版本，不是新的 Studio 设计版本，也不是游戏表现层美化版本。

最高 Release Gate：

```text
Frozen Studio 0.3.2.0
        ↓
导出真实 .dgrs
        ↓
放入 Minecraft Story Package 目录
        ↓
服务器直接读取 archive，不整体解压
        ↓
完整注册 Story / Actor / Item / ItemGroup / Session / Task
        ↓
实体/物品 Nominator 正确引用已加载包
        ↓
正常玩法事件启动 Story
        ↓
Session → Task → Objective → Settlement
        ↓
Story Flow 正常继续
```

任何一段断裂：

```text
0.3.2.0_B = NO-GO
```

---

# 1. Studio Freeze — HARD BOUNDARY

## 1.1 开工前置

只有用户明确验收 `0.3.2.0_A` 后，才正式开始 B。

施工报告记录：

```text
0.3.2.0_A USER_ACCEPTED
Studio Authoring / Editing / UI FROZEN
Frozen A HEAD = <actual accepted SHA>
```

如果用户最终验收的 A HEAD 与本 PLAN 记录的 `782a78d...` 不同，以实际验收 HEAD 为准。

## 1.2 B 中冻结的 Studio 区域

禁止为了 Minecraft 问题重新打开以下设计：

- Studio Workspace；
- Story / Session / Task 编辑器布局；
- Graph Editor；
- Flow / Logic 端口规则；
- reconnect / marquee / multi-select / Shift splice；
- Node authoring semantics；
- Inspector / Inline Editor；
- Draft / Commit；
- Story Start authoring；
- Session authoring；
- Task Objective / 前置条件 authoring；
- Theme / Studio UI framework。

原则：

> **B 修连接，不重新设计 Studio。**

## 1.3 唯一允许的 Studio 例外

如果真实 A 导出的 `.dgrs` 证明 DGRS writer / manifest / package closure 本身存在 interoperability blocker，才允许最小修改 Studio Packaging。

必须满足：

1. 先证明不是 Runtime reader 自己的问题；
2. 不修改 Graph / Inspector / Workspace / Authoring；
3. 优先让 Java reader 适配已冻结 DGRS v1；
4. 真需修改时，只动 Packaging / DGRS docs / package tests；
5. DEVELOPMENT REPORT 单列 `STUDIO_FREEZE_EXCEPTION`。

默认目标：

```text
B 中 Studio authoring/UI source = 0 changes
```

---

# 2. 人工审计 → 版本归类

| 审计项 | 版本处理 |
|---|---|
| 1. 铜币名称/金粒贴图问题 | **0.3.2.1** |
| 2. 收纳箱左下角冗余提示 | **0.3.2.1** |
| 3. `/dgr` 与 `/dgrpg` 混用 | **B 必修** |
| 4. 实体指名应浏览故事包和角色 | **B 必修** |
| 5. 物品指名 UI 复杂且没有明确被指名 ItemStack | **B 必修** |
| 6. DGRS 资源加载/统计不完整 | **B P0** |
| 7. 正确 ID 仍提示“故事未加载”；提示英文 | **B P0/P1** |
| 8. Task 当前 Objective 游戏内显示 | **B 做功能闭环，视觉 polish 留 0.3.2.1** |
| 9. DGRS Runtime 整体解压、目录污染与生命周期 | **B P0，目标为直接读取** |

`0.3.2.1` 明确保留：铜币/金粒、收纳箱提示、Task UI 图标/动画/HUD 美化、其他 Presentation polish。

---

# 3. 当前代码 Root-Cause Map

施工开始时必须重新核验当前 HEAD；PLAN-time 对 A HEAD 的代码审查已确认以下事实。

## RC-1 — `/dgrpg` 仍然是主命令

当前 `CommandDarkGreyRpg`：

```java
getCommandName() -> "dgrpg"
getCommandAliases() -> ["dgr"]
```

B 必须反转为：

```text
/dgr = 唯一公开 root command
```

## RC-2 — Minecraft DGRS 仍是“整体解压后伪装成 ProjectDirectory”

当前 `StoryPackageLoader`：

```text
xxx.dgrs
→ ZipFile
→ extractArchive()
→ .dgrs-runtime/<package>-<nanoTime>/
→ ProjectRepository(directory)
```

`LoadedStoryPackage` 仍持有 `File directory`；`StoryPackageSnapshotMerger` 还会再次从该 directory 读取 shared-resource raw bytes。

所以 A 的 DGRS Writer 已经是真正的单文件容器，但 Minecraft Reader 还不是。

B 的 P0：

> **正常 DGRS 运行路径不再整体解压，也不再创建 `.dgrs-runtime`。**

## RC-3 — Nominator Story catalog 仍偏 legacy

当前：

```java
NominatorCatalog.from(ProjectSnapshot)
```

构建 Story 列表只遍历：

```java
snapshot.getStories()
```

DGRS v1 主 Story 却位于：

```java
snapshot.getCanonicalStories()
```

因此 canonical-only DGRS 可以出现“Actor 看得见，但 Story 目录为空/错误”。

## RC-4 — “正确 ID 仍提示故事未加载”已有明确原因

当前 `NominatorService.bindEntity(...)` 检查：

```java
project.getStory(storyId)
```

它只查 legacy `StoryDefinition`，不查：

```java
project.getCanonicalStory(storyId)
```

所以 canonical DGRS Story 明明已经安装，仍可能返回：

```text
Selected story is not loaded.
```

B 必须从 Story resolution 层修复，不能靠 `storyId = null` 绕开。

## RC-5 — 当前加载摘要无法完整表达 DGRS canonical 内容

`ProjectRepository.ReloadResult` 主要统计 legacy：

```text
actors / dialogues / quests / stories
```

并不完整报告：

```text
items / item_groups / canonical_stories / sessions / tasks
```

所以审计中“只显示两个角色”至少包含一个确定的状态呈现缺口；B 必须同时证明实际资源和显示数字都正确。

## RC-6 — Item Nominator 没有真实目标槽

当前 `GuiNominatorInventory` 仍是 `GuiScreen`，用：

```text
上一个槽位 / 下一个槽位 / selectedSlot
```

选择背包物品，不符合“打开玩家背包 + 放置被指名物品的槽位”的需求。

## RC-7 — Task Runtime 已有正确基础，不应另造阶段系统

当前已经有：

```text
INACTIVE / ACTIVE / COMPLETED
prerequisite sticky activation
CanonicalTaskJournalProjector
```

因此 Task UI 只需投影 Runtime 状态，不应新增 `Stage / Sequential / Parallel` 字段。

---

# 4. Priority

| Priority | Work Package |
|---|---|
| **P0** | WP-1 DGRS Direct Runtime Reader |
| **P0** | WP-2 完整 Package Snapshot / Reload Lifecycle |
| **P0** | WP-3 Canonical Story Loaded-State / package-aware catalog |
| **P1** | WP-4 `/dgr` 统一 + 玩家提示中文化 |
| **P1** | WP-5 实体指名：Package → Actor |
| **P1** | WP-6 物品指名：Inventory Target Slot → Item / Group |
| **P1** | WP-7 最小 Canonical Task Active-Objective UI |
| **Release Gate** | WP-8 Frozen Studio → Minecraft Vertical Slice |

P0 未闭合前，禁止先花大量时间 polish GUI。

---

# 5. WP-1 — DGRS Direct Runtime Reader

## 5.1 最终运行路径

```text
story.dgrs
    ↓
java.util.zip.ZipFile
    ↓
索引并验证所有 entry path
    ↓
直接读取 manifest.json
    ↓
直接读取 manifest 声明的资源 entries
    ↓
parse / validate
    ↓
immutable LoadedStoryPackage
```

禁止正常路径继续：

```text
extractArchive()
→ ProjectDirectory
→ filesystem rescan
```

## 5.2 产品目录

正常运行后 Story Package 目录仍应是：

```text
story_a.dgrs
story_b.dgrs
```

不应出现：

```text
.dgrs-runtime/
story_a/
story_b/
```

## 5.3 最小内部 seam

不要做 Generic VFS。

允许增加严格限定在 Story Package loading 的小型结构，例如：

```text
DgrsArchiveReader
DgrsPackageContent
```

其能力只需：

```text
manifest
entry names
readBytes(path)
readUtf8(path)
```

或在成功 load 后保存 immutable `Map<String, byte[]>`。

核心契约：

> **DGRS 版 LoadedStoryPackage 不依赖解压 directory 才能 merge / resolve。**

Legacy unpacked package 若仍需兼容，可以保留 Directory source，但不要把 `.dgrs` 再 materialize 成 Directory source。

## 5.4 Parser reuse

禁止复制一份新的 ProjectRepository serialization stack。

优先：

- 给已有 loader/parser 增加 `String / Reader / byte[]` overload；
- 把现有纯 JSON parse/validate 逻辑提成 package-private helper；
- 或增加一个只面向 Story Package 的 `StoryPackageSnapshotReader`。

不得降低 validator，不得建立 provider registry / virtual filesystem framework。

## 5.5 LoadedStoryPackage / Merger

DGRS load 成功后至少保留：

```text
StoryPackageManifest
ProjectSnapshot
CanonicalStoryLogicGraph
source archive identity
manifest-declared raw resource bytes
```

`StoryPackageSnapshotMerger` 的 shared-resource byte equality 应改为读取 package 内保存的 declared bytes，而不是 `Files.readAllBytes(value.getDirectory()...)`。

这样 `ZipFile` 可在 load transaction 结束时关闭。

## 5.6 ZipFile 生命周期

成功 load 后：

```text
所有 Runtime 状态已 detached
→ ZipFile close
```

这样 Windows 下 `.dgrs` 可替换、可删除，不会因为 Runtime 长期持有 file handle 而失败。

## 5.7 DGRS v1 parity

Java reader 与 Frozen Studio DGRS v1 严格对齐：

- `format = dgrs`
- `format_version = 1`
- `producer = DarkGreyRPGStudio`
- required `manifest.json`
- required `project.json`
- manifest-declared closure
- UTF-8
- `/` separators
- no absolute/drive path
- no empty / `.` / `..` segment
- duplicate normalized path rejected
- unsupported version fail-closed

不能只看 `.dgrs` 后缀就信任 archive。

---

# 6. WP-2 — 完整 Package Snapshot 与 Reload Lifecycle

## 6.1 “加载成功”的定义

必须完整通过：

```text
Manifest
Actors
Items
Item Groups
Canonical Story
Membership
Sessions
Tasks
Story Logic Graph
Cross-resource identity
Merged ProjectSnapshot publication
```

## 6.2 精确资源数

以当前人工审计包为代表，若包声明：

```text
Actors: 2
Items: 1
Sessions: 2
Tasks: 1
Canonical Stories: 1
```

则 authoritative merged snapshot 必须真实满足对应 count，而不是只改 status 文案。

## 6.3 完整状态摘要

`/dgr status` / `/dgr reload` 最少输出：

```text
已加载 1 个故事包。
故事：1
角色：2
物品：1
物品组：0
会话：2
任务：1
```

多包时给总数即可，不做 debug dump。

## 6.4 Package Set 原子发布

```text
scan sources
→ build candidates
→ validate each
→ merge candidate set
→ build authoritative ProjectSnapshot
→ atomic installSnapshot
→ publish catalog revision
```

不能一边 load 一边逐个改正式 Repository。

## 6.5 Corrupt replacement

已成功加载的 `story.dgrs` 被损坏版本覆盖：

```text
/dgr reload
→ 新 candidate FAIL
→ 保留上一份 accepted package
→ 中文报告更新失败
```

延续现有 fail-safe 行为。

## 6.6 Package 删除

如果 `story.dgrs` 真正被删除：

```text
下一次 reload
→ package registry 移除
→ merged snapshot 移除其资源
→ Nominator catalog 不再列出
```

删除不是“损坏 update”，不能保留旧 package。

## 6.7 删除最后一个包

当前代码在 package map 为空时可能不会重新 install merged snapshot。B 必须保证：

```text
最后一个 .dgrs 删除
→ Runtime 不再保留旧 Story/Actor/Task
```

应恢复重新验证后的 base project snapshot，或明确的 empty/unloaded snapshot；具体取决于当前配置语义，但绝不能继续使用不存在的 Story Package 数据。

## 6.8 Resolver / manager refresh

确认以下 manager 都解析同一个 authoritative `ProjectRepository` snapshot：

- CanonicalStoryForgeManager
- CanonicalSessionForgeManager
- CanonicalTaskForgeManager
- Nominator
- legacy Dialogue/Quest compatibility

如果存在 cache，`installSnapshot` 后必须明确失效/refresh。

---

# 7. WP-3 — Canonical Story Loaded-State + Package-aware Catalog

## 7.1 Story loaded 的统一定义

增加/复用明确查询：

```text
containsStory(storyId)
```

语义：

```text
legacy Story exists
OR
canonical Story exists
```

DGRS v1 canonical Story：

```text
getCanonicalStory(id) != null
→ loaded
```

## 7.2 分清状态

至少分开：

```text
Package discovered
Package validated
Package installed
Story present in authoritative snapshot
Story currently running for player
```

Nominator 所需的是第四项，不是“玩家是否已经启动 StoryInstance”。

## 7.3 Package-aware NominatorCatalog

新的 catalog 最少表达：

```text
PackageChoice
- packageId
- storyId
- story display name
- actor IDs in closure
- item IDs in closure
- item-group IDs in closure
```

数据源：

```text
StoryPackageLoader validated packages
+ merged ProjectSnapshot
+ manifest / canonical membership closure
```

不能继续只靠 `snapshot.getStories()`。

## 7.4 Shared resource

如果 Actor / Item 被多个包引用：

```text
选择 Package A
→ 显示 Package A 声明的 closure
```

不要只靠 `Actor.homeStoryId` 猜资源所属。

## 7.5 Revision

保留 catalog revision。

GUI 打开后如果 `/dgr reload`：

```text
旧 revision 提交
→ server 拒绝
→ 用户重新打开/刷新
```

---

# 8. WP-4 — `/dgr` 统一 + 中文玩家提示

## 8.1 Root command

最终：

```java
getCommandName() -> "dgr"
getCommandAliases() -> empty
```

`/dgrpg` 不再作为公开别名。

命令不是 persistence schema，不需要长期背旧 root。

## 8.2 Subcommand

保留已有英文 subcommand：

```text
/dgr status
/dgr reload
/dgr actor
/dgr story
/dgr session
/dgr task
/dgr debug
```

本版不发明中文 command grammar。

## 8.3 中文化范围

玩家/管理员正常可见：

- usage
- status
- reload
- package error summary
- Nominator success/error
- stale catalog
- permission
- conflict
- Story unavailable
- Task UI

必须中文。

技术 error code / server log / stack trace 可以英文。

不要为了 B 新建完整 i18n framework；优先用现有 `ChatMessages` 或小型集中 message helper。

---

# 9. WP-5 — 实体指名：Package → Actor

## 9.1 正常工作流

```text
使用实体指名工具
→ 点击实体
→ 选择已加载故事包
→ 浏览包中的角色 / 角色组
→ 选择
→ 指名
```

手输完整 Actor ID 不再是正常流程。

## 9.2 最小 UI

```text
实体指名
目标：<实体名>

故事包
[ kill_slimes / 清理酒馆周边 ]

角色
[ 酒馆老板   NPC_ID: tavern_boss ]
[ 史莱姆     Group_ID: slimes ]

[ 指名 ] [ 解除指名 ] [ 关闭 ]
```

可保留轻量搜索：显示名 / ID。

## 9.3 Individual / Collective

保持既有语义：

- Individual → NPC_ID；
- Collective → Group_ID。

不建立第二套 binding model。

## 9.4 Server authoritative

客户端只提交：

```text
package/story identity
selected actor identity
target entity UUID
catalog revision
```

服务器重新验证：

- package 仍 installed；
- Story loaded；
- Actor 在 package closure；
- Actor type；
- permission；
- unique NPC conflict / transfer policy。

## 9.5 unknown_story regression

必须有独立测试：

```text
canonical-only DGRS Story
+ valid Actor
+ valid storyId
→ accepted
```

---

# 10. WP-6 — 物品指名：真实 Inventory + Target Slot

## 10.1 核心产品目标

不是“把旧 UI 画漂亮”。

而是明确：

> **我现在要给哪一个 ItemStack 指名。**

因此移除“上一个槽位 / 下一个槽位”作为主流程。

## 10.2 使用真实 Container

推荐：

```text
GuiContainer
+ server Container
```

界面：

```text
物品指名

故事包
[ ... ]

资源
[ 铁剑      Item_ID: iron_sword ]
[ 战利品组  Group_ID: loot ]

被指名物品
[ Target Slot ]

玩家背包
[ inventory ]
[ hotbar ]

[ 指名 ] [ 关闭 ]
```

## 10.3 Target Slot 生命周期

目标槽必须是真正 server-authoritative 的 container slot，不是客户端假图。

要求：

- 不消费 ItemStack；
- 不复制 ItemStack；
- GUI close 归还；
- bind 成功归还；
- bind 失败归还；
- disconnect/container close 不吞物；
- inventory 满时用标准安全回退（必要时在玩家位置掉落）。

## 10.4 Item / Item Group

正常 UI 支持：

```text
Item_ID → existing exact Item binding
Group_ID → existing EXACT group member binding
```

当前 fuzzy-group backend 可保留兼容，但不再暴露为主 UI 的复杂组合。

## 10.5 Package closure

```text
选择 Package/Story
→ 只显示其 manifest/membership 内 Item / ItemGroup
```

## 10.6 Server validation

bind 时重新检查：

- target slot non-empty；
- package/story loaded；
- selected resource in closure；
- catalog revision current；
- Item_ID / Group_ID exists；
- stack capture valid；
- conflict policy。

---

# 11. WP-7 — Canonical Task 最小 Active-Objective UI

## 11.1 B 为什么仍需要

B 不是 Presentation 版本，但没有最小 Task UI，就无法正常验收：

```text
Studio Task
→ DGRS
→ Minecraft TaskInstance
→ Objective activation
```

所以 B 只做 **Functional Task Tracker**。

## 11.2 唯一显示规则

```text
Task UI
=
ObjectiveStatus == ACTIVE
```

绝不显示所有 Objective，绝不由 UI 分析 graph 推断 sequential / parallel。

## 11.3 Sequential

```text
杀10史莱姆
→ 老板交互
```

初始：

```text
当前目标
○ 杀死10个史莱姆 0 / 10
```

完成后同一次 authoritative refresh：

```text
当前目标
○ 与酒馆老板交互
```

旧目标消失。

## 11.4 Parallel

并联 Active objectives 同时显示；每个完成后该行消失；AND 成立后下游 Objective 出现。

## 11.5 复用现有 Journal projection

优先复用：

```text
CanonicalTaskJournalProjector
CanonicalTaskJournalEntry
CanonicalTaskJournalObjectiveRow
```

如果 projector 输出所有 rows，UI/network 层只筛 `ACTIVE`，或增加一个最小 active projection。

不新增：

```text
Stage
SequentialMode
ParallelMode
TaskPhase
```

## 11.6 Display

B 最少显示：

- Task 名；
- 当前目标；
- Objective description；
- kill/collect progress。

`interact_actor` 可以只显示文本，不强制显示 `0/1`。

## 11.7 0.3.2.1 再做

- 图标；
- 动画；
- HUD 大改；
- 完成特效；
- 历史记录；
- pin/track；
- Journal 美术重构。

---

# 12. WP-8 — Frozen Studio → Minecraft Vertical Slice

这是 B 最终权威 Gate。

## 12.1 Fixture

优先使用用户真实 A 项目重新导出的 `.dgrs`。

最低覆盖：

```text
Actors 2
Items 1
Sessions 2
Tasks 1
Canonical Stories 1
```

以实际包为准，最终 report 记录 exact SHA-256 与 manifest counts。

## 12.2 Install

只做：

```text
复制 story.dgrs
→ Story Package install directory
```

不手工解压。

## 12.3 Startup

真实 Minecraft 1.7.10 / Forge 启动后：

```text
DGRS discovered
DGRS validated
DGRS installed
```

并确认：

```text
无 .dgrs-runtime
无 same-name extracted folder
```

## 12.4 `/dgr status`

真实 server 中必须准确显示包/Story/Actor/Item/Session/Task 数量，并全部中文。

## 12.5 Entity Nominator

真实操作：

```text
点击实体
→ 选 package
→ 选 Actor
→ 指名
```

不以手输 ID 作为唯一验收路径。

完成后：

- binding persistence 正常；
- 不再报“故事未加载”；
- Actor resolver 可获得正确 DGR identity。

## 12.6 Item Nominator

真实：

```text
打开 UI
→ 玩家背包可见
→ 放一个 ItemStack 到 target slot
→ 选 package
→ 选 Item / ItemGroup
→ 指名
```

验证 stack 不丢、不复制，collect matching 可用。

## 12.7 Story Start

必须通过正常 gameplay event 启动 Story，例如与已指名 Actor 交互。

`/dgr debug story start` 只能辅助定位，不能作为最终唯一证明。

## 12.8 Session

Story 进入 Session aggregate 后：

```text
Session GUI 正常
Dialogue / Choice / End 正常
Flow 返回 Story
```

## 12.9 Task

Story 进入 Task：

```text
TaskInstance ACTIVE
→ 当前 Active Objectives 显示
```

真实完成 kill / collect / interact 中 exact fixture 覆盖的类型。

## 12.10 Prerequisite

必须证明：

```text
Objective B prerequisite=False
→ 不显示
→ 不计数
```

A 完成后：

```text
completion Logic=True
→ B ACTIVE
→ UI 替换为 B
```

另用 fixture 覆盖 parallel。

## 12.11 Settlement

```text
first true settlement slot
→ Task aggregate named Flow output
→ Story Flow 继续
```

如果 exact package 有第二 Session，必须真实进入。

## 12.12 Restart

在有 Story/Task 状态时重启 server：

- Entity/Item binding 保持；
- Story/Task snapshot 保持；
- Active/Completed 不倒退；
- DGRS 仍直接读取；
- 无 runtime extraction cache。

---

# 13. Package Remove / Replace Gate

## 13.1 Remove

删除 `story.dgrs` 后 `/dgr reload`：

- registry 移除 package；
- snapshot 移除资源；
- Nominator 不再显示；
- 不留 extraction/cache；
- persisted binding 可保留为 unresolved/dormant reference，但不得继续启动不存在的 Story。

不要无授权破坏世界中的用户 binding persistence。

## 13.2 Reinstall

同 ID 有效 package 放回并 reload，旧 unresolved binding 如 ID 仍匹配可重新解析。

## 13.3 Corrupt Update

有效 package 被损坏版本覆盖：

```text
reload
→ candidate FAIL
→ prior accepted package 继续服务
```

明确区别于“文件被删除”。

---

# 14. Mod Version Alignment

当前 Java Mod build metadata 仍可能停在 `0.3.1.0`。

B 应统一到：

```text
Studio Version: 0.3.2.0 (frozen)
DGRS producer_version: 0.3.2.0
Minecraft Mod Version: 0.3.2.0
Development phase: 0.3.2.0_B RC
```

A/B 是施工阶段标签，不必写进 DGRS `producer_version`。

---

# 15. Automated Verification

自动测试不是 Minecraft 实机验收的替代品。

## DGRS Direct Reader

至少：

- valid frozen-A DGRS direct read；
- no extraction directory；
- manifest parity；
- traversal / duplicate path reject；
- corrupt ZIP reject；
- unsupported version reject；
- missing required entry reject；
- canonical identity/kind reject；
- shared resource conflict reject。

## Counts

fixture exact count：2 Actor / 1 Item / 2 Session / 1 Task / 1 canonical Story。

## Reload lifecycle

覆盖：

```text
valid load
valid replacement
corrupt replacement retains prior
delete removes package
delete last package clears stale snapshot
reinstall
```

## Story resolution

覆盖：

```text
legacy only
canonical only
both
missing
```

## Nominator catalog

覆盖 package/story filtering、shared resource、items/groups、stale revision、canonical-only story。

## Entity bind

canonical-only DGRS Story + valid Actor + storyId → accepted。

## Item Container

覆盖：

- empty slot cannot bind；
- move stack in；
- Item_ID bind；
- Group_ID exact bind；
- close/success/failure return stack；
- stale revision reject；
- no dup/loss。

## Command

```text
primary == dgr
no public dgrpg alias
usage starts /dgr
```

## Task projection

```text
INACTIVE omitted
ACTIVE shown
COMPLETED omitted
sequential transition
parallel rows
stable ordering
```

---

# 16. Real Minecraft Acceptance

以下内容不能仅靠 pure Java probe 宣布 PASS：

- package GUI/catalog 是否真的出现；
- ItemStack 是否真的可拖入 target slot；
- stack 是否无丢失；
- `/dgr` 是否真实注册；
- normal event 是否启动 Story；
- Session GUI；
- Task current objective transition；
- server restart；
- package delete/reload；
- filesystem 是否无 extraction cache。

最终必须使用：

```text
Minecraft 1.7.10 + Forge
DarkGrey RPG 0.3.2.0_B RC
Frozen Studio A-produced DGRS
```

---

# 17. Studio Freeze Integrity Gate

最终执行：

```text
git diff <FROZEN_A_HEAD>...<B_HEAD> -- studio/
```

理想：

```text
0 authoring/UI source changes
```

若出现 Packaging freeze exception，逐文件解释。

以下被冻结路径若被 B 非必要修改即 NO-GO：

```text
CanonicalGraphEditorView
GraphEditorHostViewModel
CanonicalNodeInspectorViewModel
GraphEditSession authoring semantics
GraphNodeDefinitionRegistry authoring semantics
Story Workspace UI
```

---

# 18. Explicit Non-Goals

B 不建立：

- Generic VFS；
- DB；
- runtime asset database；
- provider/plugin API；
- hot-reload daemon；
- file watcher framework；
- remote package repository；
- DGRS encryption/signature；
- patch/update system；
- 新 Story schema；
- 新 Task stage model；
- 通用 HUD framework；
- 新 Studio editor；
- 新 Item matching language。

Reload 仍可由：

```text
server startup
/dgr reload
```

驱动。

---

# 19. Recommended Work Order

## Phase 0 — Freeze Baseline

- 用户验收 A；
- 记录 frozen A SHA；
- 建/切 `codex/0.3.2.0_B`；
- 记录 dirty state；
- 保存用户 exact DGRS fixture。

## Phase 1 — Reproduce Audit

不改代码先复现：

- DGRS extraction；
- count/status mismatch；
- canonical Actor + correct ID → unknown_story；
- entity manual-ID-only；
- item slot cycling；
- `/dgrpg` primary。

## Phase 2 — Direct DGRS Reader

先打掉 extraction dependency。

## Phase 3 — Snapshot / Lifecycle

完成 full closure、counts、atomic package set、remove/reinstall/corrupt。

## Phase 4 — Canonical Story Resolution

先修 server model 和 package-aware catalog，再碰 GUI。

## Phase 5 — `/dgr`

统一 root + normal surface 中文化。

## Phase 6 — Entity Nominator

Package → Actor browser。

## Phase 7 — Item Nominator

GuiContainer + target slot + Package Item/Group。

## Phase 8 — Functional Task Tracker

Active Objectives only。

## Phase 9 — Full Java Regression

运行全部 existing probes/tests/format/compile + B probes。

## Phase 10 — Real Vertical Slice

完整执行 Sections 12–13。

## Phase 11 — Freeze Integrity

确认 Studio authoring/UI diff 为零。

---

# 20. Expected Code Hotspots

基于 A HEAD，仅供 reconnaissance：

```text
src/main/java/darkgrey/rpg/project/packages/
  StoryPackageLoader.java
  StoryPackageManifest.java
  LoadedStoryPackage.java
  StoryPackageSnapshotMerger.java

src/main/java/darkgrey/rpg/project/
  ProjectRepository.java
  ProjectSnapshot.java

src/main/java/darkgrey/rpg/nominator/
  NominatorCatalog.java
  NominatorStorySearch.java
  NominatorService.java

src/main/java/darkgrey/rpg/client/gui/
  GuiNominatorEntity.java
  GuiNominatorInventory.java
  GuiQuestJournal.java

src/main/java/darkgrey/rpg/network/
  NominatorNetwork.java
  nominator packets

src/main/java/darkgrey/rpg/command/
  CommandDarkGreyRpg.java

src/main/java/darkgrey/rpg/task/journal/
  CanonicalTaskJournalProjector.java
  CanonicalTaskJournalEntry.java
  CanonicalTaskJournalObjectiveRow.java

src/main/java/darkgrey/rpg/task/forge/
src/main/java/darkgrey/rpg/task/instance/
src/main/java/darkgrey/rpg/DarkGreyRpg.java

gradle.properties
build.gradle.kts
```

不要机械修改全部文件；先确认 dependency chain。

---

# 21. Acceptance Matrix

| ID | Gate | Expected |
|---|---|---|
| P1 | A frozen baseline | exact SHA |
| P2 | Studio authoring/UI untouched | diff proof |
| D1 | DGRS direct-read | no extraction |
| D2 | `.dgrs-runtime` absent | filesystem proof |
| D3 | DGRS v1 strict | PASS |
| D4 | Actor count correct | exact fixture |
| D5 | Item count correct | exact fixture |
| D6 | Session count correct | exact fixture |
| D7 | Task count correct | exact fixture |
| D8 | canonical Story loaded | exact fixture |
| D9 | corrupt update retains prior | PASS |
| D10 | deleted package removed | PASS |
| D11 | delete-last clears stale snapshot | PASS |
| C1 | primary command `/dgr` | real server |
| C2 | `/dgrpg` not public | real server |
| C3 | status/reload Chinese | real output |
| N1 | entity GUI shows package | real GUI |
| N2 | entity GUI shows package Actors | real GUI |
| N3 | manual ID not required | real GUI |
| N4 | canonical story bind succeeds | real server |
| N5 | unknown_story regression fixed | PASS |
| I1 | item GUI shows player inventory | real GUI |
| I2 | item target slot exists | real GUI |
| I3 | Item_ID binding works | real server |
| I4 | Group_ID exact binding works | real server |
| I5 | stack never lost/duplicated | real server |
| T1 | INACTIVE hidden | real UI |
| T2 | ACTIVE shown | real UI |
| T3 | COMPLETED row disappears | real UI |
| T4 | prerequisite activates next row | real UI |
| T5 | parallel rows simultaneous | real UI/fixture |
| V1 | normal Actor event starts Story | full slice |
| V2 | Session executes | full slice |
| V3 | Task executes | full slice |
| V4 | Settlement returns to Story Flow | full slice |
| V5 | restart persistence | full slice |
| V6 | uninstall/reinstall lifecycle | full slice |

---

# 22. NO-GO Conditions

任意一条成立，B 不能发布 RC：

1. DGRS 正常 load 仍整体解压。
2. Package 目录仍生成 `.dgrs-runtime` / 同名解压目录。
3. status 显示成功但 snapshot 缺 Item/Session/Task。
4. canonical Story 仍被 Nominator 判定未加载。
5. 实体指名仍必须记 Actor ID。
6. Item Nominator 没有明确 target ItemStack。
7. Item container 有丢物/复制风险。
8. `/dgrpg` 仍是主命令或公开 alias。
9. 正常玩家提示仍大量英文。
10. package 删除后旧 Story 仍在 Runtime。
11. corrupt update 清空上一份有效 package。
12. Task UI 显示全部 Objectives。
13. UI 自己推断 sequential/parallel。
14. INACTIVE Objective 预计数。
15. B 修改冻结的 Studio Graph/Inspector/Workspace。
16. 为 direct DGRS 新造大型 VFS/framework。
17. 只用 `/dgr debug` 证明 Vertical Slice。
18. 无真实 Minecraft 证据却报告 Integration PASS。
19. Codex 自行标记 `USER_ACCEPTED`。
20. 把铜币/收纳箱/大量 Presentation polish 偷偷吸回 B。

---

# 23. DEVELOPMENT REPORT Requirements

最终报告至少包含：

## Baseline

```text
Frozen A HEAD
B start HEAD
B final HEAD
dirty state
Java mod version
exact DGRS SHA-256
```

## Studio Freeze Integrity

```text
studio/ diff summary
authoring/UI changed paths = 0
```

## DGRS Root Cause

明确说明 A 版本 Minecraft 为什么解压 DGRS、B 如何移除 directory dependency。

## Resource Count Root Cause

分别列：Story / Actor / Item / ItemGroup / Session / Task 的 parsed / merged 数量与旧 summary 问题。

## Nominator unknown_story Root Cause

必须明确写到：

```text
legacy getStory
vs
canonical getCanonicalStory
```

## Filesystem proof

列出 install dir：

```text
before startup
after startup
after reload
after uninstall
```

证明无 extraction residue。

## Real Vertical Slice

逐步记录：

```text
package install
/dgr status
entity bind
item bind
Story start
Session
Task
Objective transition
Settlement
Story continue
restart
uninstall/reinstall
```

## Deferred 0.3.2.1

明确保留：

- copper coin/gold-nugget cleanup；
- storage-box bottom-left hint；
- Task UI icon/animation/polish；
- game presentation work。

---

# 24. Definition of Done

Codex 只有在以下全部成立时，才可以报告：

```text
0.3.2.0_B Release Candidate
IMPLEMENTED
AGENT_VERIFIED
```

必须：

- Studio Authoring / Editing / UI freeze 未被破坏；
- Frozen A 导出的真实 DGRS 被 Minecraft 直接读取；
- 不产生 extraction directory；
- manifest/resource closure 完整；
- Story / Actor / Item / Session / Task 数量正确；
- `/dgr` 成为唯一公开 root；
- 正常 player-facing message 中文；
- canonical Story loaded-state 正确；
- Entity Nominator 可 Package → Actor 选择；
- Item Nominator 有真实 inventory + target slot；
- Item / ItemGroup 可正常 binding；
- Task UI 只显示 Active Objectives；
- prerequisite sequential/parallel 行为真实可观察；
- normal gameplay event 能启动 Story；
- Session → Task → Settlement → Story Flow 全链路完成；
- restart persistence 正常；
- package deletion/reinstall lifecycle 正常；
- full Java regression PASS；
- real Minecraft vertical slice PASS；
- 0.3.2.1 deferred 项未混入。

Codex 不得自行报告：

```text
USER_ACCEPTED
0.3.2.0_B 正式完成
0.3.2.1 authorized
```

最终仍由用户验收。

---

# 25. Short Codex Instruction

> Implement `0.3.2.0_B` from the user-accepted frozen `0.3.2.0_A` HEAD. Treat Studio Authoring, Graph Editing, Inspector, Workspace and established Studio UI as frozen. B is the Studio↔Minecraft integration release. First replace the current DGRS extraction-to-`.dgrs-runtime` path with a real direct ZIP-entry DGRS v1 reader that builds an immutable validated `LoadedStoryPackage`/`ProjectSnapshot` without materializing a project directory. Remove merger dependence on `LoadedStoryPackage.getDirectory()` by retaining manifest-declared raw bytes or an equally small package-entry seam; do not build a generic VFS. Make package reload atomic, retain the previous accepted package on a corrupt replacement, remove a package when its `.dgrs` source is deleted, and ensure deleting the last package cannot leave stale runtime resources. Verify the exact package closure including canonical Story, Actor, Item/ItemGroup, Session and Task and expose accurate Chinese status counts. Change the public root command from `/dgrpg` to `/dgr` only. Fix Nominator canonical Story resolution and make the server catalog package-aware so entity nomination can browse loaded Story Packages and their Actors/Actor Groups instead of requiring memorized IDs. Replace the current item slot-cycling screen with a real server-authoritative inventory container containing the player's inventory and one explicit target ItemStack slot; allow concise Package→Item/ItemGroup selection, preserve stack integrity, and keep fuzzy-group matching out of the normal UI. Player-visible normal command/nominator/package errors must be Chinese. Add only a functional canonical Task tracker in B: display only `ACTIVE` Objectives from existing TaskInstance state; do not add task stages or infer graph topology. The release gate is a real frozen-Studio→DGRS→Minecraft vertical slice: install a real A-produced package, direct-load it without extraction, show exact resource counts, bind Actor and Item through normal GUIs, start Story through a real gameplay event, run Session→Task→Objective transition→Settlement→Story continuation, restart, and test package uninstall/reinstall. Keep copper-coin/gold-nugget cleanup, storage-box hint cleanup, Task UI icons/animation and other presentation work deferred to `0.3.2.1`. Codex may deliver `IMPLEMENTED / AGENT_VERIFIED / Release Candidate`; only the user may declare `USER_ACCEPTED`.
