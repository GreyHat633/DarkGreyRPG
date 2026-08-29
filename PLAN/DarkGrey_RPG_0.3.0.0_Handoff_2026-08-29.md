# DarkGrey_RPG 0.3.0.0 下一对话 Handoff

> 目的：让新的 Codex 对话在不依赖旧上下文的情况下，安全接续 0.3.0.0 开发。
> 仓库：`E:\Java\MinecraftMod\DarkGrey_RPG`
> 当前分支：`codex/0.3.0.0-flow-first-authoring`
> 当前阶段：Stage 5、Stage 6 与 Stage 7 本地 Gate 已完成；2026-08-29 17:01 +08:00 用户已授权 commit、开发分支 push 与 PR 提交，tag、merge 和 GitHub Release 未授权
> 详细进度报告：`E:\Java\MinecraftMod\DarkGrey_RPG\PLAN\DarkGrey_RPG_0.3.0.0_Development_Report_2026-08-29.md`

> **2026-08-29 验收范围更新（优先于本文其余旧现场说明）：** Stage 5 不再以现有 CustomNPC+ 酒馆老板链路作为最终游戏内产品形态验收，而只把它视为 Runtime Vertical Slice 测试宿主。0.3.0.0 的核心 Gate 是 Story Flow、Session、Task、Objective、Action、终止、持久化与清理形成完整稳定闭环。CNPC 专属绑定、最终 NPC 体验、剧情包部署和最终任务 UI 延期至 0.3.1.0；已有 CNPC 实机结果仅保留为兼容/测试证据。

## 1. 给下一对话的首条任务说明

请先完整阅读：

1. `E:\Java\MinecraftMod\DarkGrey_RPG\PLAN\DarkGrey_RPG_0.3.0.0_Design_Plan.md`
2. `E:\Java\MinecraftMod\DarkGrey_RPG\PLAN\DarkGrey_RPG_0.3.0.0_Development_Report_2026-08-29.md`
3. 本 Handoff。

随后只读核对当前 Git 状态、Forge 进程和最新日志；当前应从 135 条脏树状态的逐项归属分拣、分阶段提交、开发分支推送和远端验证继续。不要重复 Stage 6/7 本地验收，也不要继续旧酒馆老板产品化实机链路。当前授权不包括 tag、merge、GitHub Release 或清理用户文件。

## 2. 不可破坏的约束

- 保留所有现有修改、删除和未跟踪文件；不要 `git reset --hard`、`git clean`、整树 checkout 或批量恢复。
- `examples/phase4_project` 的 6 个删除是已有本地状态，不要恢复或提交。
- `.codex/config.toml`、`AGENTS.md` 等可能属于用户/工具配置，不要当成产品改动处理。
- 不要把纯探针、Dedicated Server 启动或日志推断写成真实客户端验收；同时也不要再把真实 CNPC 客户端全链路当作 0.3.0.0 Gate。
- Story Flow 是唯一可编辑 Runtime 编排源；Project Story Graph 必须派生只读。
- Session 管理会话内部图；Task 管理目标与结算；不要把两者内部时序重新塞回 Story Flow。
- 当前不支持并发、多游标或 Task 内部 Session 入口/返回；不要扩展 Stage 5 范围。
- 当前工作在本 Handoff 原始快照中尚未提交；17:01 已取得 commit、开发分支 push 与 PR 提交授权，tag、merge 和 GitHub Release 未授权。

## 3. 首次接手时先执行的只读核对

在 PowerShell 中：

```powershell
Set-Location -LiteralPath 'E:\Java\MinecraftMod\DarkGrey_RPG'
git status --short --branch
git rev-parse HEAD
Get-CimInstance Win32_Process |
    Where-Object { $_.Name -eq 'java.exe' } |
    Select-Object ProcessId, ParentProcessId, ExecutablePath, CommandLine
Get-Content -LiteralPath 'run\server\logs\latest.log' -Tail 120
Get-Content -LiteralPath 'run\client\logs\latest.log' -Tail 120
```

本 Handoff 生成时的现场：

- Minecraft 客户端已经断开并退出；旧的客户端 exec/session 标识不能再用。
- 2026-08-29 16:28:50 的只读核对中，旧 Forge Server PID `37576` 已不存在；只看到 Gradle daemon/worker PID `27056`、`22276`，不得把它们描述成运行中的 Dedicated Server。
- PID 会漂移，必须以接手时的只读检查为准；若 Stage 7 需要服务端验收，应新鲜启动并保留独立证据。
- 最后一次确认的客户端进度是 Task `ACTIVE 0/10`；玩家断开后的持久化状态尚未重新核实。
- 16:39:30 的新鲜 Dedicated Server 已到达 ready，并于 16:40:33 正常保存、停止 Live Bridge 和退出 Gradle；25565/32145 及服务端进程均已复核清理。
- 16:41–16:44 的新鲜客户端已确认 `darkgrey_rpg 0.3.0.0` 与包括 CustomNPC+ 在内的 13 个模组成功加载并出现 `Minecraft 1.7.10` 窗口；标准窗口关闭后窗口消失但 Java 进程超过 60 秒未自行结束，最终精确终止 PID `18668`，当前无客户端残留进程。
- Worker 使用 PID `7916` 再次复现，并在关闭后两次 jstack：主残留是 Minecraft/LWJGL `Client thread`，次级为 CustomNPC+ VersionChecker 与 Paulscode 声音线程；没有 DarkGrey RPG 线程，Live Bridge 线程均为 daemon。该行为已归类为开发运行路径/依赖限制而非 0.3.0.0 产品 P0/P1，复现进程已精确清理。

## 4. Stage 5 当前准确位置

正式实现已覆盖：

- Start：进入 Story、角色交互、区域进入配置和匹配；
- Story 单实例与最低重复策略；
- Session/Task 聚合节点的交接；
- 条件、Action、EnterStory、terminate 与失败清理；
- StoryGraph 派生适配；
- 真实铜币物品 `darkgrey_rpg:copper_coin`；
- 酒馆老板纯 Runtime 纵向切片探针。

真实客户端已覆盖：

```text
CustomNPC+ actor interaction
  -> canonical Story start hook
  -> offer Session opened
  -> offer Session completed
  -> slime Task active at 0/10
```

真实客户端尚未覆盖、且按最新范围不再要求在 0.3.0.0 继续完成：

```text
kill 10 slimes
  -> Task settlement
  -> thanks Session
  -> grant 10 copper coins
  -> terminate Story
  -> clean Story/Session/Task state
```

## 5. 已完成的接手路径（不要重复执行）

以下步骤 A–E 与后续 Stage 6 包均已在本 Handoff 更新前完成，保留它们是为了说明证据来源，不是要求下一对话重新执行。下一对话应从 Stage 7 新鲜测试矩阵、版本统一和兼容冒烟开始。

### 步骤 A：重设并锁定 Stage 5 Gate

1. 在 PLAN、Handoff、Development Report 中统一记录最新验收范围。
2. 将 `stage5_bartender` 明确标注为 Runtime 测试夹具/兼容宿主，不作为最终剧情包或 NPC 产品形态。
3. 不再启动 Minecraft 客户端继续击杀史莱姆、打磨 CNPC 绑定或收集最终任务 UI 证据。

### 步骤 B：完成纯 Runtime 闭环证据

通过自动化 probe/test 明确覆盖：

1. Story Start 与单实例；
2. Session 打开、完成与结果路由；
3. Task 激活、Objective 事件进度与唯一结算；
4. 活动状态持久化、重载和恢复；
5. Action 执行；
6. terminate；
7. Story、Session、Task 活动状态清理和失败清理。

证据不得依赖 CNPC、最终 NPC 体验、剧情包部署或最终任务 UI。

### 步骤 C：保留既有兼容证据

- 保留已有 CNPC 交互、offer Session、Task `ACTIVE 0/10` 截图与日志，准确描述为兼容/测试证据。
- 不得声称 10 枚铜币已在客户端发放，也不得声称旧 CNPC 真实客户端全链路已完成。
- 上述未完成项不再阻塞 Stage 5 Runtime Gate。

### 步骤 D：清理临时验收内容

最新范围已经取消旧完整实机 Gate，因此在确认内容仅为本轮验收临时产物后进行：

1. 在 `src/main/java/darkgrey/rpg/command/CommandDarkGreyRpg.java` 精确移除：
   - `/dgrpg actor face <yaw> <pitch>` 分支；
   - `faceActorTestTarget(...)`；
   - 仅由它使用的 `EntityList`、`Vec3` import；
   - usage 中的 `face`。
2. 不要回退该文件中的 canonical Session/Task 命令，它们是正式实现。
3. 将 `run/server/server.properties` 的 `spawn-monsters` 恢复为原值 `false`。
4. 将 `run/client/options.txt` 的临时 G/F 映射恢复为原有/用户期望键位；修改前先核对，不要猜测覆盖用户设置。
5. 删除或归档 `run/tools/SetPlayerRotation.java`、`SetPlayerRotation.class` 和 `.before-stage5-rotation` 备份前，先确认它们确实仅为本轮验收产物。
6. 测试世界中的 NPC、史莱姆和平台不必为了代码验证立即删除；如需清理，先解析并确认准确世界路径，不做递归广泛删除。

### 步骤 E：完成 Stage 5 回归并进入 Stage 6

Java 环境：

```powershell
$env:JAVA_HOME='E:\Java\jdk1.8.0_471'
$env:GRADLE_USER_HOME='E:\Java\gradle'
.\gradlew.bat build canonicalStoryRuntimeProbe canonicalStoryActionConfigurationProbe canonicalStoryInstanceProbe canonicalStoryServerServiceProbe canonicalStoryForgeCoordinatorProbe --offline --no-daemon --console=plain
```

.NET/WPF 环境：

```powershell
$env:WINDIR=$env:SystemRoot
$env:DOTNET_CLI_HOME='E:\Java\MinecraftMod\DarkGrey_RPG\.dotnet'
$env:NUGET_PACKAGES='E:\Java\MinecraftMod\DarkGrey_RPG\.nuget\packages'
& 'E:\Java\dotnet-sdk-10\dotnet.exe' test 'studio\src\DarkGreyRPG.Studio.Tests\DarkGreyRPG.Studio.Tests.csproj' -c Release --no-restore --nologo
& 'E:\Java\dotnet-sdk-10\dotnet.exe' test 'studio\src\DarkGreyRPG.Studio.Wpf.Tests\DarkGreyRPG.Studio.Wpf.Tests.csproj' -c Release --no-restore --nologo
```

2026-08-29 Stage 6 收口后的新鲜结果：Core `313/313`、WPF `325/325`；Release Studio build `0 warnings / 0 errors`；Java `build` 与 Story Runtime / Action / Instance / Server Service / Forge Coordinator probes 全部通过。

## 6. 关键文件定位

### 计划与文档

- `PLAN/DarkGrey_RPG_0.3.0.0_Design_Plan.md`
- `docs/0.3.0.0_ARCHITECTURE.md`
- `docs/0.3.0.0_BASELINE.md`
- `docs/0.3.0.0_PHASE1_GRAPH_CORE.md`
- `docs/0.3.0.0_STAGE4_TASK_LOGIC_GRAPH.md`
- `docs/DECISIONS.md`

### Java Runtime

- `src/main/java/darkgrey/rpg/DarkGreyRpg.java`
- `src/main/java/darkgrey/rpg/story/runtime/StoryEventAdapter.java`
- `src/main/java/darkgrey/rpg/story/canonical/`
- `src/main/java/darkgrey/rpg/session/`
- `src/main/java/darkgrey/rpg/task/`
- `src/main/java/darkgrey/rpg/graph/`
- `src/main/java/darkgrey/rpg/client/gui/GuiCanonicalSessionScreen.java`
- `src/main/java/darkgrey/rpg/client/gui/GuiQuestJournal.java`
- `src/main/java/darkgrey/rpg/command/CommandDarkGreyRpg.java`
- `src/main/java/darkgrey/rpg/content/ModItems.java`

### Studio

- `studio/src/DarkGreyRPG.Studio.Core/Graphs/`
- `studio/src/DarkGreyRPG.Studio/ViewModels/Graph/`
- `studio/src/DarkGreyRPG.Studio/Views/Graph/`
- `studio/src/DarkGreyRPG.Studio/ViewModels/ShellViewModel.cs`
- `studio/src/DarkGreyRPG.Studio/ViewModels/ProjectHomeViewModel.cs`

### Stage 5 验收工程与证据

- `run/server/darkgrey_rpg_project/resources/canonical/stories/stage5_bartender.json`
- `run/server/darkgrey_rpg_project/resources/canonical/sessions/stage5_bartender_offer.json`
- `run/server/darkgrey_rpg_project/resources/canonical/sessions/stage5_bartender_thanks.json`
- `run/server/darkgrey_rpg_project/resources/canonical/tasks/stage5_slime_task.json`
- `run/client/screenshots/2026-08-29_14.03.24.png`：Task ACTIVE 0/10。
- `run/client/screenshots/2026-08-29_14.09.13.png`：y=200 平台上的史莱姆。
- `run/client/logs/latest.log`
- `run/server/logs/latest.log`

## 7. 已知探针成功标记

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

这些标记必须由明确覆盖完整 Runtime 执行闭环的断言支撑，不能只靠名称宣布通过。它们不再需要替代或等待旧酒馆老板真实客户端全链路验收。

## 8. Stage 5 完成定义

只有以下条件全部满足，才能把 Stage 5 标为完成并开始 Stage 6：

- 同玩家同 Story 不产生重复实例；
- Session/Task 聚合端口与资源结构动态同步；
- terminate 清理 Story、Session、Task 活动状态；
- Project Story Graph 继续保持派生只读；
- Runtime Vertical Slice 自动化证据覆盖 Story Flow、Session、Task、Objective、Action、终止、持久化与清理；
- 该闭环不依赖 CNPC 专属绑定、最终 NPC 体验、剧情包部署或最终任务 UI；
- 既有 CNPC 实机结果已作为兼容/测试证据保留，不作为阻塞 Gate；
- 临时验收代码和配置已经清理；
- Java build/probes、Core tests、WPF tests 在清理后重新通过；
- 工作树经过只读审计，未混入用户/工具侧无关文件。

以上条件已在 2026-08-29 的新范围回归中满足，Stage 5 可视为完成。未完成的旧 CNPC 客户端 10/10、铜币背包截图与最终 NPC/任务 UI 体验仍按事实保留，但不阻塞 Stage 6。

## 9. Stage 5 之后

- Stage 6 第一包已完成：Dialogue → Session 与 Quest → Task 的只读 Core preview；跟踪的 2.1 验收资源已证明文本、ID、命名出口和 metadata 语义保留，Core `299/299`、WPF `318/318`。
- Stage 6 第二包已完成并补全：Story Flow 只读 pattern preview；结构明确的 Story、Session/Task 聚合和终止-only Story 可确定性转换，不安全节点稳定 fail-closed。`royal_mystery` 的中流 `ActorInteract` 迁移为持久化 `interact_actor` 等待节点，`EnterRegion` 迁移为精确球体 `enter_region` 等待节点；二者都不是 StoryStart。
- Stage 6 第三包已完成：project-level 只读预览、源文件 SHA-256、严格 membership 映射与 canonical 写入清单；预览零写入，Core `305/305`、WPF `318/318`。
- Stage 6 第四包已完成：显式事务 Apply、双重 freshness 校验、`.migration-backups`、严格原子写入、失败回滚与迁移日志；后端未接入 `OpenProject` 自动执行。Core `312/312`、WPF `318/318`。
- Stage 6 第五包已完成：project-level preview/confirm UI；只有可应用预览且用户明确确认后才能调用事务 Apply，取消与无效预览保持零写入，成功后刷新 Project Home。
- Stage 6 真实窗口验收已完成：在隔离的 2.1.3 项目副本中显示源文件 9、资源 6、拟写入 10、问题 0；确认后生成 10 个 canonical 文件，legacy 字节不变，备份逐文件哈希一致，成功日志完整。截图：`.tooling/stage6/migration-ui-live/migration-preview.png`。
- Stage 6 当前全量结果：Core `313/313`、WPF `325/325`、Release Studio build `0 warnings / 0 errors`；阶段 Gate 已满足。
- Stage 7：完整测试矩阵、真窗口 UI Automation、Dedicated Server、Minecraft 客户端兼容冒烟、0.3.0.0 版本统一、打包和发布验收。最终 CNPC/NPC/任务 UI 产品体验留到 0.3.1.0。
- Stage 7 本地 Gate：Core `313/313`、WPF `325/325`、Runtime build/probes、2.1.1/2.1.2/2.1.3/迁移四套真实窗口脚本、版本统一、本地 EXE/JAR、Dedicated Server 正常启停、客户端启动兼容和退出残留归因均已完成；当前产品范围无已知 P0/P1。
- 最终本地审计：`git diff --check` 无错误，未发现 TODO/FIXME/HACK/NotImplemented 残留，无 Minecraft/Studio 进程或 25565/32145 监听；工作树仍有 135 条状态（35 modified、6 个既有 deleted、94 untracked），发布前必须逐项分拣归属。
- 本地候选件：`dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`，SHA-256 `1840CBED0C56E30C040F03265801B1C8223A46AA8E0F8386A583E09DCD2A1BFE`；`build\libs\darkgrey_rpg-0.3.0.0.jar`，SHA-256 `3ED13E84E78C8D32AE1F97BDCCDC2579BF693EC3E01D2F0CEF4FCCCDEC834C52`。
- commit、push、tag、release 需要用户明确授权，不能从“继续开发”自动推导。

## 10. 向用户汇报时的证据措辞

- 可以说：纯 Runtime 酒馆老板纵向探针通过。
- 可以说：真实客户端已证明 offer Session 能启动 Task，Journal 显示 ACTIVE 0/10。
- 可以说：既有 CNPC 链路是兼容/测试证据，不再是 0.3.0.0 Stage 5 产品形态 Gate。
- 可以说：Stage 6 跟踪项目在隔离副本上完成真实窗口确认迁移，10 个 canonical 文件、legacy 字节保留、完整备份和日志均已验证。
- 不能说：真实纵向切片已经通过。
- 不能说：10 枚铜币已在客户端发放。
- 不能说：terminate/cleanup 已完成真实客户端验收。
- 不能说：0.3.0.0 已完成最终 CNPC/NPC/任务 UI 产品体验。
- 不能说：本轮新增的 Actor/Region 事件等待已经取得新的 Minecraft 客户端投递证据。
- 不能说：0.3.0.0 已发布或可发布。
- 可以说：客户端启动并加载 0.3.0.0 与 13 个模组通过；关闭残留已由双 jstack 证明不来自 DarkGrey RPG 产品线程。
- 不能说：ForgeGradle 开发客户端是正常自行退出的；它仍依赖精确终止残留 JVM。
