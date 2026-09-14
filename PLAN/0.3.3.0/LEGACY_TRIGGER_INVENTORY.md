# 用户澄清更新（2026-09-12）

只删除旧独立触发器节点，现用开始配置和启动路径保持不变。此前记录的
“激活契约缺口/等待用户确认”判断已撤销；下文保留为 P0 原始审计历史，不代表当前阻塞。
P1 已完成，当前结果见 [P1.md](P1.md) 和已修正的施工计划。以下所有 P1 等待状态均为作废的历史判断。

# 0.3.3.0 P0 Legacy Trigger inventory

审计日期：2026-09-12。基线：`82c6443238e41ce820f076e09d65d3cb26bb2236`。
扫描脚本：`scripts/inventory-0330-legacy.ps1`。

## 结论及 P1 开工条件

当前 Trigger 是仍被生产入口使用的架构，不能按死代码直接删除。

1. `CanonicalStoryServerService.actorCandidates()` 逐个读取 Start Trigger 的
   `interact_actor / actor_id` 来生成玩家可启动的 Story 候选；
   `executeActorCandidate()` 使用对应 Trigger port 和 repeat policy 启动。
2. `CanonicalStoryEventAdapter.onEntityInteract()` 是生产 NPC 交互入口，
   通过 Forge manager 路由到上述候选选择。它还与 Task 交互目标联动；不能整段删除。
3. `CanonicalStoryForgeManager` 的跨 Story Logic 传播调用
   `CanonicalStoryServerService.startByLogic()`；后者只读取 Logic Start Trigger。
4. 管理命令 `startByEntry()` 仍经 `selectEnterStory()` 选择旧触发入口。
5. `onPlayerTick()` → `matchingRegionTriggers()` → `CanonicalStoryRegionEntryTracker`
   → `handleRegionPosition()` 是活动区域触发路径。

因此，计划中“Story 被激活后从纯开始节点执行”还缺少“激活来源”的替代契约。
已向用户提出问题，未代替用户冻结下列行为：

- NPC 交互启动是否保留；如保留，Actor 与 Story 的启动关系放在哪里；
- 跨 Story Logic 自动启动删除还是用什么新契约承接；
- 原 `repeat_policy` 所表达的 Story 再次启动规则如何承接。

该问题属于 **P1 激活契约缺口**，与计划已有 P2 的 DESIGN-GATE-A/B/C 不同。
在答复前保留当前运行行为；不实现兼容层，也不把删除后的入口缺失当作完成。

## 证据覆盖

`evidence/legacy-source-inventory.csv` 共 140 个候选文件，每项含域、行号、匹配符号、SHA-256：

| 域 | 文件数 |
| --- | ---: |
| Runtime | 36 |
| Runtime probe | 13 |
| Studio Core | 17 |
| Studio UI | 28 |
| Studio test | 28 |
| Schema | 2 |
| Example | 1 |
| Documentation/tool | 10 |
| Historical archive | 5 |

这是关键词候选清单，不是 140 个待删文件。Task 同名 Objective、历史文档和归档
必须分别审查。扫描范围为当前存在的 Git 源码、测试、schema、examples、docs、scripts、
legacy 和 README；已在工作区删除的历史文件记录于 `initial-worktree.txt`，不恢复。
忽略目录下历年 `.tooling` 构建/验收副本不是当前作者数据，不批量迁移或删除。

`evidence/legacy-project-inventory.csv` 只读检查以下当前项目，共 19 行：

- `E:\Java\MinecraftMod\RPGProject\TestProject`；
- `E:\Java\MinecraftMod\RPGProject\TestProject_2`；
- 本仓库 `run/client/darkgrey_rpg_project`（无 canonical graphs）；
- 本仓库 `run/server/darkgrey_rpg_project`。

其中当前 Story 仍含 `repeat_policy,triggers`。清单保存文件哈希，未修改作者项目和存档。
`evidence/legacy-package-inventory.csv` 直接读取活动/验收包及本轮隔离副本中的 JSON，
记录 8 个匹配条目和 archive SHA-256；不解包到运行目录，不替换游戏包。

## 精确施工边界

| 子域 | 源码锚点 | P1 处理要求 |
| --- | --- | --- |
| Start contract | `Graphs/Definitions/StoryStartSchema.cs`, `CanonicalStoryStartConfiguration.java` | 用纯 Start 契约替换 Trigger schema；禁止永久 fallback |
| Registry/shape | `GraphNodeDefinitionRegistry.cs`, `GraphNodeShapeValidator.cs`, `CanonicalGraphResourceLoader.java` | 删除 Story event types / enter_story，并证明 strict load 拒绝旧 payload |
| 创建/编辑/删除 | `GraphNodeFactory`, `GraphNodeAuthoringService`, `GraphEditSession`, `CanonicalProjectStoryGraphService`, `CanonicalStoryLifecycleService` | 清除旧 trigger-port 创建和维护逻辑，保留基础 aggregate/boundary |
| Inspector | `CanonicalNodeInspectorViewModel.cs`, `CanonicalNodeParameterSummary.cs`, `CanonicalStoryWorkspaceViewModel.cs` | 删除旧触发编辑模型和摘要；引用处由 CSV 行号定位 |
| 事件索引 | `CanonicalStoryTriggerIndex`, `CanonicalStoryRegionEntryTracker`, `CanonicalStoryEventAdapter` | 删除旧事件启动链；保留 Task 交互及必要的生产交互分发 |
| Runtime | `CanonicalStoryRuntime`, `CanonicalStoryServerService`, `CanonicalStoryForgeManager` | 替换激活入口；删除事件 wait / enter_story 路径；明确 repeat policy 归属 |
| 持久化 | `CanonicalStorySnapshot`, `CanonicalStoryInstance*`, `CanonicalStoryInstanceNbtCodec` | 清点 trigger_port_id、wait payload、repeat policy 的所有调用者；仅一次性开发迁移 |
| 包/项目校验 | `CanonicalProjectContentLoader`, `ProjectRepository`, `StoryPackageExporter` | 同步更新依赖收集和负面校验，防止旧 payload 仍被接受 |
| Identity 引用 | `ResourceRenameMap`, `NamespaceProjectValidator` | 混有 Trigger 引用遍历；若必须移除旧分支，需精确审查范围例外，不可改 identity 语义 |
| Objective | `CanonicalTaskObjectiveSchema`, `CanonicalTaskRuntime`, `CanonicalTaskJournalProjector` | **保留 Task interact_actor**；P2 才扩展其他目标 |
| 非本阶段 | Session narration、历史 Story/Quest 兼容作者工具 | narration 属于 P5；其余先判别实际调用关系，禁止按单词批量删 |

P1 回归必须覆盖 actor chooser、Story commands、跨 Story Logic、Task 交互推进、
Story instance reload、包加载以及纯 Start 连线。仅隐藏节点菜单不足以通过。
