# DarkGrey_RPG 0.3.0.0 开发进度报告

> 快照时间：2026-08-29 16:55:07 +08:00
> 仓库：`E:\Java\MinecraftMod\DarkGrey_RPG`
> 分支：`codex/0.3.0.0-flow-first-authoring`
> 基线提交：`9be86aa0edf5b3e849002d89e0fcd63810d332fe`（`Fix Studio dialogue and quest draft UX`）
> 计划：`E:\Java\MinecraftMod\DarkGrey_RPG\PLAN\DarkGrey_RPG_0.3.0.0_Design_Plan.md`

> **2026-08-29 验收范围更新：** 本报告原始快照中的“酒馆老板完整真实客户端纵向切片”不再是 0.3.0.0 Stage 5 Gate。现行 Gate 收敛为 Story Flow、Session、Task、Objective、Action、终止、持久化与清理的完整稳定 Runtime 执行闭环。现有 CustomNPC+ 酒馆老板链路只作为 Runtime Vertical Slice 测试宿主及兼容证据；CNPC 专属绑定、最终 NPC 体验、剧情包部署和最终任务 UI 延期至 0.3.1.0。

> **2026-08-29 17:01 发布授权更新：** 用户已明确授权将当前 0.3.0.0 成果提交并推送到 GitHub；授权范围包括分阶段 commit、开发分支 push 与 PR 提交，不自动包括 tag、merge 或 GitHub Release。

## 1. 执行摘要

0.3.0.0 的实施阶段为 Stage 0–7，共 8 个阶段。当前跟踪状态为：

- Stage 0–4：实现与阶段性验证已经完成；
- Stage 5（Story Flow 重构）：已按最新 Runtime Gate 完成；完整执行闭环 probe、临时内容清理、Java build、Core 与 WPF 回归均已通过；
- Stage 6（迁移与兼容）：已完成并通过阶段 Gate；跟踪的 2.1.3 项目已在隔离副本中完成真实窗口预览、明确确认、备份、10 文件写入、日志和刷新验证；
- Stage 7（发布验收）：本地 Gate 已完成；版本统一、全量测试、四套真窗口脚本、本地候选件、Dedicated Server 正常启停、客户端启动兼容冒烟和退出残留归因均有新鲜证据；GitHub/Release 尚未执行；
- 当前没有 commit、push、tag、merge 或 release。

旧范围下 Stage 5 卡在真实客户端验收的最后一段：任务已在客户端显示为 `ACTIVE 0/10`。最新范围取消了这条产品形态 Gate；现已用 CNPC 无关的自动化证据证明 Runtime 完整执行闭环，并清理旧实机验收留下的临时命令和配置。

## 2. 必须保持的产品与架构边界

- Story Flow 是唯一可编辑的 Runtime 编排来源。
- Project Story Graph 由 Story 引用关系派生，只读；不得重新变成第二套可编辑逻辑源。
- Session 管理会话内部顺序、台词、选择、条件和结果。
- Task 管理目标、逻辑关系、结算与进度。
- Story Flow 管理何时进入 Session/Task、条件分支、Action、跨 Story 和终止。
- 0.3.0.0 不引入隐式并发、多 Story 游标或同时启动多条有副作用的流程。
- 0.3.0.0 不扩展或打磨 CNPC 专属绑定、最终 NPC 体验、剧情包部署或最终任务 UI；这些属于 0.3.1.0。
- PLAN 是技术要求和验收依据，不是自动执行命令；任何 commit、push、发布仍需用户明确授权。

## 3. 已完成工作概览

### Stage 0：架构冻结

- 建立 0.3.0.0 分支和设计计划。
- 在 `docs/DECISIONS.md`、`docs/0.3.0.0_ARCHITECTURE.md`、`docs/0.3.0.0_BASELINE.md` 中记录术语、端口规则、非目标和基线。
- 冻结 Story / Session / Task 三层边界以及 Flow / Logic 两类接口语义。

### Stage 1：通用 Graph Core

- 建立 Canonical Graph 资源、节点、端口、连接、动态端口和作用域规则。
- 实现 Flow / Logic 基数与类型校验、稳定 `port_id`、图编辑会话和持久化。
- Studio Core、WPF 和 Java 项目加载侧均加入相应实现与测试。

### Stage 2：资源树工作台

- Story Flow 常驻工作区、资源树、Inspector、面包屑和资源创建/引用工作流已接入。
- Session / Task 可作为 Story 聚合资源进入局部图，并返回 Story Flow。
- Project Story Graph 保持派生只读。

### Stage 3：Session Graph

- Canonical Session 的节点定义、编辑、存储、实例、SavedData、网络编解码、服务端路由和客户端模型已实现。
- 新增 `GuiCanonicalSessionScreen`，可在真实 Minecraft 客户端显示并推进 canonical Session。
- Story 聚合 Session 的结果路由和单结果推进已有探针覆盖。

### Stage 4：Task Logic Graph

- Canonical Task 的目标、逻辑、结算、实例、事件驱动进度和持久化已实现。
- Task Journal 已接入 canonical Task 投影；客户端截图证明 `stage5_slime_task` 能显示为 `ACTIVE 0/10`。
- 相关 Stage 4 文档位于 `docs/0.3.0.0_STAGE4_TASK_LOGIC_GRAPH.md`。

### Stage 5：Story Flow 重构（主体已完成，按新范围复核 Gate）

已完成的核心内容：

- Start 触发配置与索引：`CanonicalStoryStartConfiguration`、`CanonicalStoryTriggerIndex`；
- 角色交互触发与区域进入检测：`StoryEventAdapter`、`CanonicalStoryRegionEntryTracker`；
- 触发索引缓存与 Forge 生命周期接入：`CanonicalStoryForgeManager`、`DarkGreyRpg`；
- Story 单实例、最低重复策略、Session/Task 聚合交接、失败清理和终止路径；
- 条件、Action、`EnterStory` 与派生 StoryGraph 适配；
- Action schema 的 Studio fixture、Core 测试、WPF Inspector 测试与 Java 配置探针；
- 注册真实物品 `darkgrey_rpg:copper_coin`，供酒馆老板纵向切片奖励 10 枚铜币；
- 在 `run/server/darkgrey_rpg_project` 准备被忽略的 Stage 5 实机验收工程。

## 4. 已有验证证据

### 4.1 Studio 测试

Stage 6 收口后重新执行的全量结果：

- Studio Core：`313/313` 通过；
- Studio WPF：`325/325` 通过。

说明：以上包含 Stage 5 Runtime 边界和 Stage 6 迁移/UI 的当前完整回归。

### 4.2 Java / Runtime 探针

最后一次已完成的 Stage 5 关键结果：

```text
CANONICAL_STORY_RUNTIME_SINGLE_CURSOR=PASS
CANONICAL_STORY_EVENT_WAITS=PASS
CANONICAL_STORY_RUNTIME_AGGREGATE_HANDOFF=PASS
CANONICAL_STORY_RUNTIME_STRICT_VALIDATION=PASS
CANONICAL_STORY_ACTION_CONFIGURATION=PASS
CANONICAL_STORY_ACTION_STRICT_REJECTION=PASS
CANONICAL_STORY_INSTANCE_IDENTITY=PASS
CANONICAL_STORY_INSTANCE_REPEAT_POLICY=PASS
CANONICAL_STORY_INSTANCE_NBT_RESTORE=PASS
CANONICAL_STORY_SESSION_CHECKPOINT=PASS
CANONICAL_STORY_START_TRIGGER_SCHEMA=PASS
CANONICAL_STORY_SESSION_SERVICE=PASS
CANONICAL_STORY_ATOMIC_RESTART=PASS
CANONICAL_STORY_ERROR_CHILD_CLEANUP=PASS
CANONICAL_STORY_FORGE_AGGREGATE_ROUTE=PASS
CANONICAL_STORY_FORGE_TRANSFER_ROUTE=PASS
CANONICAL_STORY_FORGE_FAILURE_CLEANUP=PASS
CANONICAL_STORY_BARTENDER_VERTICAL_SLICE=PASS
CANONICAL_STORY_BARTENDER_PERSISTENCE_RELOAD=PASS
CANONICAL_STORY_ACTIVE_CHILD_CLEANUP=PASS
CANONICAL_STORY_RUNTIME_EXECUTION_LOOP=PASS
```

这些结果证明纯逻辑、服务端协调、活动 Session/Task 持久化重载、Objective 进度、Task 结算、Action、终止和 active child cleanup 路径。它们不等同于真实 Minecraft 客户端验收，但已满足更新后的 0.3.0.0 Runtime Gate。

### 4.3 真实 Minecraft 验收已证明部分

- CustomNPC+ 实际角色交互已进入 canonical Story actor hook。
- 首轮触发发现旧持久化 orphan Session，日志报告同玩家/Story 已有 Session；Story 失败路径执行 `markStoryError + cancelByStory`，随后状态自行恢复。
- 新触发成功打开酒馆老板 offer Session。
- 按 Enter 完成 offer Session 后，Story 成功启动 Task。
- 截图 `E:\Java\MinecraftMod\DarkGrey_RPG\run\client\screenshots\2026-08-29_14.03.24.png` 显示 Task Journal 中任务为 `ACTIVE 0/10`。
- 截图 `E:\Java\MinecraftMod\DarkGrey_RPG\run\client\screenshots\2026-08-29_14.09.13.png` 显示 y=200 验收平台上的真实史莱姆。
- 截图 `E:\Java\MinecraftMod\DarkGrey_RPG\run\client\screenshots\2026-08-29_14.03.10.png` 记录 offer Session 关闭后的世界画面。

### 4.4 尚未取得、但不再阻塞 0.3.0.0 Stage 5 的真实客户端证据

- 史莱姆击杀进度从 0/10 增长至 10/10；
- Task 完成后 Story 打开 thanks Session；
- thanks Session 完成后玩家获得 10 枚 `darkgrey_rpg:copper_coin`；
- Story 执行 terminate 并清理活动 Session、Task 和 Story 状态；
- once/repeatable 策略在真实服务端状态下符合配置；
- 旧范围下的酒馆老板真实客户端纵向切片完整通过。

这些缺口必须继续如实记录，但只用于区分兼容证据与最终产品形态，不再驱动 0.3.0.0 的 CNPC/NPC/UI 开发。

### 4.5 Stage 7 新鲜版本、候选件与 Forge 兼容证据

- Studio 与 Runtime 的单一对外版本已统一为 `0.3.0.0`；Studio 窗口/About、程序集版本、Gradle artifact 名称和 `mcmod.info` 一致。
- Studio 单文件候选件：`dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`，`141161174` bytes，SHA-256 `1840CBED0C56E30C040F03265801B1C8223A46AA8E0F8386A583E09DCD2A1BFE`，FileVersion/ProductVersion 均为 `0.3.0.0`。
- Runtime 候选件：`build\libs\darkgrey_rpg-0.3.0.0.jar`，`543897` bytes，SHA-256 `3ED13E84E78C8D32AE1F97BDCCDC2579BF693EC3E01D2F0CEF4FCCCDEC834C52`。
- 新鲜 Dedicated Server 烟测已到达 `Done (0.893s)!`，随后执行正常 `stop`；世界保存、Live Bridge 停止、Gradle `BUILD SUCCESSFUL`，25565/32145 端口与服务端进程均已清理。`run\server\logs\latest.log` SHA-256 为 `FB5341AEB40994F0A0C532FACDE0649E457B268DB16610CA7FB971EA623C1F40`。
- 新鲜 Minecraft 客户端启动烟测从 `darkgrey_rpg-0.3.0.0-dev.jar` 识别 `DarkGrey RPG:0.3.0.0`，成功加载包括 CustomNPC+ 在内的 13 个模组，初始化 Runtime 并出现 `Minecraft 1.7.10` 窗口；证据日志为 `run\client\logs\fml-client-latest.log`。
- 客户端没有进入旧酒馆老板产品化链路。第一次启动中发送标准窗口关闭消息后窗口消失，但 Java 进程超过 60 秒未自行结束，最终只终止本轮精确 PID `18668`；该次 `runClient` 因精确终止而以非零结束。
- Worker 使用新的 PID `7916` 再次复现并在关闭后两次执行 jstack：主残留为 Minecraft 非守护 `Client thread`，栈位于 `Minecraft.runGameLoop -> org.lwjgl.opengl.Display.sync`；次级残留为 CustomNPC+ `noppes.npcs.client.VersionChecker` 和 Paulscode SoundSystem `CommandThread`/`StreamThread`。线程转储中没有 DarkGrey RPG 线程，源码复核确认 Live Bridge 的 accept/client 线程均显式 `setDaemon(true)`。因此该退出行为记录为 ForgeGradle/Minecraft/LWJGL 开发运行路径及依赖限制，不列为 DarkGrey RPG 0.3.0.0 产品 P0/P1；复现进程已精确终止并复核无残留。

### 4.6 Stage 7 真窗口与最终本地审计

- `studio/qa/2.1.1-ui-acceptance.ps1`：退出码 0，Flow 内联 UI、平移、缩放、拖线、上下文菜单、图密度、最小窗口和源 JSON 不变均通过。
- `studio/qa/2.1.2-connection-ui-acceptance.ps1`：退出码 0，连接/重连/撤销、动态端口、项目图、删除级联、DPI 与源 JSON 不变均通过。
- `studio/qa/2.1.3-resource-creation-ui-acceptance.ps1`：先准确暴露旧标题断言；将唯一产品标题期望从 `2.1.3` 更新为 `0.3.0.0` 后重跑退出码 0，Release 窗口、Dialogue/Quest 创建、复制/引用、重启恢复和干净退出均通过。
- `studio/qa/0.3.0.0-migration-ui-acceptance.ps1`：六项 PASS，继续证明真实窗口确认、10 个 canonical 文件、legacy 字节保持、完整备份和成功日志。
- 最终 `git diff --check` 没有空白错误，产品源码中未发现 TODO/FIXME/HACK/NotImplemented 残留；当前无 Minecraft/Studio 进程，25565/32145 均未监听。
- 工作树仍有 135 条状态：35 个 tracked modified、6 个此前保留的 tracked deleted、94 个 untracked。该审计证明本地实现和验证结果，不代表这些条目已完成提交归属分拣。

## 5. 实机验收中发现的环境问题

- `spawn-monsters=false` 导致召唤的史莱姆立即消失；被忽略的 `run/server/server.properties` 已临时改为 `spawn-monsters=true`。
- 最初 y=100 的平台与山体地形相交，验收位置移动到 y=200。
- 为方便攻击，`run/client/options.txt` 临时设为：

```text
key_key.attack:34
key_key.use:33
```

- 键盘自动化没有先确认聊天框状态，导致攻击字符和命令被当作聊天发送：

```text
<Developer> gggggtc/dgrpg task progress
```

- 因此目前没有有效的 1/10 以上实机任务进度证据。

## 6. 报告生成时的运行现场

- Stage 7 新启动的 Dedicated Server 已于 16:40:33 正常保存并停服；当前没有 Dedicated Server Java 进程，25565/32145 均未监听。
- Stage 7 新启动的 Minecraft 客户端窗口已关闭，遗留的精确 PID `18668` 已在确认命令行归属后终止；当前没有本轮 Minecraft 客户端 Java 进程。
- 玩家断开前最后日志为错误输入的聊天消息，当前服务端中的 Task/Story 持久化状态没有在本报告生成时重新验证。

## 7. 工作树状态与保全要求

当前工作树非常脏，所有内容均未提交：

- `git status --porcelain` 当前共 130 条顶层状态记录；
- 6 个 `examples/phase4_project` 已跟踪删除仍按原样保留；
- 大量 modified 与 untracked 内容同时存在，未进行 reset、clean、批量恢复或提交。

特别注意：

- `examples/phase4_project` 的 6 个删除是此前保留的本地状态，不得擅自恢复、删除或纳入提交。
- `.codex/config.toml`、`AGENTS.md` 可能是工具/用户侧配置变化，必须视为非 Stage 5 所有，未经逐项确认不得纳入发布。
- `.dotnet/`、`.tmp/`、PLAN、docs、新 Java/C# 源码和测试中含大量未跟踪项；不得使用 `git clean`、`git reset --hard` 或整树 checkout。
- 原快照生成时没有 Git 操作授权；17:01 后已取得 commit、开发分支 push 与 PR 提交授权，tag、merge 和 GitHub Release 仍未授权。

## 8. 临时验收内容清理状态

按最新范围，不再继续旧 Stage 5 实机验收。以下代码和运行配置已经精确清理或恢复：

1. `src/main/java/darkgrey/rpg/command/CommandDarkGreyRpg.java`
   - 临时 `/dgrpg actor face <yaw> <pitch>`；
   - 临时 `EntityList`、`Vec3` import；
   - 临时 `faceActorTestTarget(...)`；
   - actor usage 中的 `face` 分支。
   - 注意：同文件中的 canonical Session/Task 命令是正式 Stage 3/4/5 实现，不得整文件回退。
2. `run/client/options.txt`
   - 临时 G/F 键位映射。
3. `run/server/server.properties`
   - `spawn-monsters=true` 是验收临时配置；原值为 `false`。
4. `run/tools/SetPlayerRotation.java` 与 `SetPlayerRotation.class` 已删除。

仍保留且不得凭推断清理：

1. `run/server/phase1-runtime-world/playerdata/a01a21f5-c117-3c1f-b5d0-e055b927f733.dat.before-stage5-rotation`；
2. 测试世界中的 NPC、史莱姆和高空平台；任何清理前都必须先确认准确世界路径和可恢复性。

## 9. 剩余阶段

### Stage 5 收尾

- 已完成：CNPC 无关的自动化 probe/test 覆盖 Story Flow、Session、Task、Objective、Action、终止、持久化与清理；
- 已完成：保留已有 CNPC 交互、offer Session 与 Task `ACTIVE 0/10` 作为兼容/测试证据，不继续旧实机链路；
- 已完成：移除临时 `actor face`、旋转工具与运行配置；
- 已完成：Stage 5 清理后 Java build/probes 通过；Stage 6 收口后的当前全量为 Studio Core `313/313`、WPF `325/325`。

### Stage 6：迁移与兼容

- 已完成第一包：纯内存、只读、确定性的 Dialogue → Session 与 Quest → Task preview；
- 已完成第一包：Jump 兼容/旁路、命名 End、ALL / ANY / SEQUENCE、稳定合成 ID 与不支持结构 fail-closed；
- 已完成第一包复核：直接读取跟踪的 2.1 验收 Dialogue/Quest，验证文本、资源 ID、命名出口和旧物品 metadata 语义保留；
- 已完成第一包新鲜回归：Studio Core `299/299`、WPF `318/318`；
- 已完成第二包：Story Flow 安全 pattern preview；Session/Task 聚合按资源类型解析，Dialogue 出口必须完整一致，终止-only Story 使用确定性合成 Start；不安全结构稳定 fail-closed；
- 已完成第二包跟踪样本复核与补全：`kingdom_route`、`empire_route`、`uncategorized` 可安全预览；`royal_mystery` 的中流 `ActorInteract` 迁移为 `interact_actor` 事件等待节点，未折叠进 StoryStart；`EnterRegion` 同样迁移为精确数值球体等待，未知字段或冲突别名继续 fail-closed；
- 已完成第三包：project-level 只读预览聚合、源字节 SHA-256、严格 membership 映射与复数 canonical 目标清单；构造和预览零写入，canonical 非空、解析/引用/ID/文件名问题均 fail-closed；
- 已完成第三包新鲜回归：Studio Core `305/305`、WPF `318/318`；跟踪的 2.1 验收项目预览前后字节不变；
- 已完成第四包：显式事务 Apply；应用前与备份后双重 freshness 校验，完整 `.migration-backups`、严格原子写入、失败回滚、迁移日志、重复执行拒绝和路径 containment 均已覆盖；
- 已完成第四包新鲜回归：7 个事务场景与完整 Studio Core `312/312`、WPF `318/318` 通过；后端未接入 `OpenProject` 自动执行；
- 已完成第五包：project-level preview/confirm UI；迁移不会在 `OpenProject` 时自动运行，只有可应用预览且用户明确确认后才调用事务 Apply；脏 canonical 编辑器/Actor 缓存会阻止执行；
- 已完成 Legacy compatibility 收口：同 ID legacy/canonical 项目发现结果在迁移后同时保留，旧 Dialogue/Quest/Story 文件不被覆盖或删除；
- 已完成完整跟踪项目验证：真实 WPF 窗口显示源文件 9、资源 6、拟写入 10、问题 0；确认后写入 10 个严格 canonical JSON，legacy 字节前后不变，完整备份逐文件 SHA-256 一致，`migration.log` 记录成功；
- 已完成 Stage 6 新鲜回归：Studio Core `313/313`、WPF `325/325`，Release Studio build `0 warnings / 0 errors`；真实窗口 QA 标记 `MIGRATION_UI_REAL_WINDOW=PASS`、`MIGRATION_UI_TRACKED_PROJECT_APPLY=PASS`、`MIGRATION_UI_LEGACY_BYTES_PRESERVED=PASS`、`MIGRATION_UI_CANONICAL_FILES_10=PASS`、`MIGRATION_UI_COMPLETE_BACKUP=PASS`、`MIGRATION_UI_SUCCESS_LOG=PASS`；
- Stage 6 Gate 已满足；真实窗口证据位于 `.tooling/stage6/migration-ui-live/migration-preview.png`，验收只修改隔离副本，不修改跟踪的 2.1.3 fixture。

### Stage 7：发布验收

- 已完成：Core `313/313`、WPF `325/325`、Runtime build/probes；
- 已完成：2.1.1、2.1.2、2.1.3 与 0.3.0.0 迁移四套真实窗口 UI Automation；
- 已完成：Forge Dedicated Server ready、保存与正常停服；
- 已完成：Minecraft 客户端验证 `darkgrey_rpg 0.3.0.0` 与 13 模组可加载并出现窗口；关闭残留经双 jstack 归因为 Forge/Minecraft/LWJGL 与依赖线程，确认不是 DarkGrey RPG 产品线程；
- 不把 CNPC 专属绑定、最终 NPC 体验、剧情包部署或最终任务 UI 拉回 0.3.0.0 Gate；
- 已完成：Studio 与 Runtime 对外版本统一为 0.3.0.0，本地 EXE/JAR 候选件及 SHA-256 已生成；
- 已完成：客户端退出问题分级、最终本地源码审计与发布证据文档收口；
- 进行中：135 条脏树状态的提交归属分拣、分阶段 commit、开发分支 push、PR 提交与远端可读性验证；
- 未授权：tag、merge 与 GitHub Release。

## 10. 当前结论

0.3.0.0 没有偏离 PLAN 的主架构：Story Flow 负责编排、Session/Task 负责内部图、Project Story Graph 保持派生只读。Stage 5 已按 CNPC 无关的 Runtime 完整执行闭环完成，Stage 6 已通过迁移与兼容 Gate，Stage 7 本地发布验收也已完成，当前产品范围无已知 P0/P1。真实 CNPC 链路仍只完成到 Task `ACTIVE 0/10`，该结果仅保留为兼容证据；本轮没有继续该产品化链路，也没有声称 10/10、客户端铜币或最终 NPC/任务 UI 体验。GitHub 源码提交现已获授权并进入分拣/推送阶段；tag、merge 与 GitHub Release 仍未授权。
