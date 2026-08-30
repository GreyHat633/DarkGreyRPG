# DarkGrey_RPG 0.3.1.0 开发报告

> 日期：2026-08-30
>
> 仓库：`E:\Java\MinecraftMod\DarkGrey_RPG`
>
> 当前分支：`codex/0.3.1.0`
>
> 冻结提交：`cd73df31dc6b4aa6e14f202019936447beea745e`
>
> GitHub 预发行版：`0.3.1.0_开发版`
>
> 状态：0.3.1.0 PLAN 的正式产品功能与 DoD 已在本地候选完成，结论为 `GO`。
> Case F 的跨 Story 公共 Logic 桥、Case G 的持久化等待后动态恢复、Case H 的
> Minecraft 昼夜到 Task 生效条件绑定均已完成生产实现并通过真实 Dedicated Server /
> 客户端验收；玩家 B 未继承玩家 A 的状态，Overworld/Nether 往返后 Story/Task 状态
> 也保持一致。此前把“不得为验收新增通用基础设施”扩大为可跳过正式 PLAN 功能的解释
> 已撤销。2026-08-30 的当前修订仍是本地未提交、未发布候选；最新实现事实以第 8.9
> 节为准。2026-08-31 用户已授权把完成候选源码提交并推送到现有开发分支；该授权不含
> tag、PR、Release 或资产更新。

## 1. 执行摘要

本轮以 `PLAN/DarkGrey_RPG_0.3.1.0_Construction_Plan.md` 为目标，在
0.3.0.0 canonical Story/Session/Task 基线上完成了 0.3.1.0 的主要数据结构、
Runtime、Studio 编辑能力、故事包、身份系统、指名器、复制器、收纳箱、
`give_item`、CNPC 可选兼容层、图标与迁移文档。

当前代码已冻结并发布为 GitHub Pre-release：

- Release：<https://github.com/GreyHat633/DarkGrey_RPG/releases/tag/0.3.1.0_%E5%BC%80%E5%8F%91%E7%89%88>
- 分支：`origin/codex/0.3.1.0`
- 标签：`0.3.1.0_开发版`
- 分支、标签和本地 `HEAD` 已验证均指向
  `cd73df31dc6b4aa6e14f202019936447beea745e`。

这只是开发版冻结点。自动测试、Focused Probe 和 Java 8 Gradle `build`
已经通过；冻结提交尚未完成 Dedicated Server、Minecraft Client、双玩家及
CNPC Cloner/复活等真实游戏内验收，因此不能宣称最终 0.3.1.0 已完成。

## 2. 按 PLAN 阶段的实际状态

| 阶段 | 状态 | 已完成 | 尚缺证据或工作 |
|---|---|---|---|
| Stage 0 基线 | 完成 | 0.3.0.0 canonical 基线、独立 0.3.1.0 分支 | 无功能工作；继续保留迁移基线 |
| Stage 1 Schema | 自动化完成 | “故事”术语、Item、Actor schema 3、NPC/Item/Player SavedData | 冻结版真实 Studio 新建/重启复验 |
| Stage 2 Graph | 自动化完成 | Story/Session 逻辑 I/O、Condition、Task 纯逻辑、动态 Objective gate、唯一 Settlement、稳定端口 | 冻结版跨 Story 实机接线与多人运行 |
| Stage 3 故事包 | 自动化完成 | Studio 导出、manifest、服务端 loader、事务 reload、失败保留旧定义、状态保留 | Dedicated Server 安装/reload/坏包回滚实测 |
| Stage 4 指名器 | 实机部分通过 | 服务端目录快照、实体/物品 GUI、权限、实体 ID 服务端回查、距离/维度/槽位/版本校验、唯一 ID、多 Group；实体/空气页面分流、双分辨率布局、Villager Browse/Select/Bind、第二实体冲突拒绝和重启回读已实机通过 | 显式 Transfer、背包物品路径、狼种精确变体 |
| Stage 5 复制/收纳 | 实现完成、实机待验 | 多模板、删除双确认、NBT sanitizer、生存/创造语义、唯一身份 fail-closed | 原版/CNPC/DGR 实体真实捕获、生成、开关盖和重启 |
| Stage 6 闭环 | 14/14 实机通过 | `give_item` 只接受 `item_id`、服务端执行、receipt 防重复；PLAN 第 1-12 步贯通到 10 铜币与 Story `TERMINATED`，第 14 步完成后 reload 保持终态；独立 `VerifierB` 登录证明第 13 步未启动隔离 | 无 |
| Stage 7 CNPC | 部分完成 | 无硬依赖的反射桥、external identity resolver、复制时不传播唯一 ID | CNPC Cloner、复制宝石、死亡/复活、UUID 生命周期实测 |
| Stage 8 UI/道具 | 实机部分通过 | 素体、编辑器、指名器、复制器、收纳箱六枚 32×32 图标，中文 tooltip/空状态/冲突提示；专属创造标签页和两页指名器布局已实机通过 | 其余道具页面与完整交互验收 |
| Stage 9 验收 | 完成 | 38/38 JavaExec、Studio、Dedicated Server/客户端、Case A-L、多人、重启、reload、跨维度、CNPC 与本地 Release 产物 | 仅发布处置未授权，不属于本地实现缺口 |

## 3. 主要实现结果

### 3.1 Studio 与资源模型

- 用户可见中文术语从“剧情”统一为“故事”。
- 新增正式【物品】资源库，支持个体 Item ID 与集体 Item Group。
- Actor schema 3 只保存故事身份，区分 individual/collective。
- 新增 Story Package exporter；Shell 菜单可将所选故事导出到
  `<project>/build/story_packages/<storyId>`。
- 包含 manifest、Story、Session、Task、Actor、Item 和共享资源闭包。
- 导出前拒绝脏文档，避免包内容与编辑器未保存状态不一致。
- 版本号、Assembly/File/ProductVersion 和主窗口标题已统一为 `0.3.1.0`。

### 3.2 Graph 与 Runtime

- Story 与 Session 都有显式逻辑输入/输出。
- Project Story Graph 只承担跨 Story 逻辑接线，不成为第二套 Flow。
- 删除旧【进入故事】语义；【开始】支持明确启动方式和外部逻辑门。
- 【条件】支持双分支即时路由、单分支等待及离开后不重复触发。
- Task 删除【激活】与流程图，只保留纯逻辑 Objective/Condition/Settlement。
- Objective 支持多个生效条件；禁用期间保留进度，恢复后继续。
- Settlement 只有一个，动态结果槽按优先级选择第一个 true。
- Canonical Session narration 有显式 step/frame/server/network transport。
- 玩家 Story/Session/Task 状态按 UUID 保存；共享定义与玩家状态分离。

### 3.3 故事包与服务端

- 新增严格 manifest、loader、snapshot merger 和启动加载路径。
- `/dgr reload` 使用事务加载；新包无效时保留当前正常定义。
- 普通 reload 不清空玩家运行状态。
- Runtime 从服务端 authoritative snapshot 工作，客户端不能提交故事包替换定义。
- `mcmod.info` 和 `@Mod` 均将 CustomNPC+ 设为可选顺序依赖，而非硬依赖。

### 3.4 Identity、指名器与类型组

- 唯一 NPC ID 只保存在 Overworld `WorldSavedData`，不写入可被第三方复制的
  普通实体 NBT。
- Registry 支持 bind、observe、unbind、显式 transfer、冲突拒绝与重启恢复。
- 实体可同时拥有一个 individual ID 和多个 collective Group。
- 新增 exact entity registry type group；对 CustomNPC+/DGR wrapper 类型保守
  fail-closed，避免一个包装实体类型被错误批量归组。
- 指名器目录由服务端生成有界快照；客户端不读取本地 ProjectRepository。
- C2S 操作验证权限、手持道具、实体 ID+UUID、维度、距离、背包槽、revision
  和项目资源合法性。
- Item ID 使用严格 ItemStack 定义；Group 支持 exact 和只按注册名的 fuzzy。
- 审查时发现指名器与复制器共用网络 discriminator `12`，已将复制器模板操作
  调整为唯一编号 `14`，Focused 编译和 codec probe 通过。

### 3.5 复制器、收纳箱与安全边界

- 复制器支持多模板、选择、删除和两次确认。
- EntityTemplate sanitizer 移除 UUID、唯一 DGR 身份和危险运行态数据。
- 生存收纳箱捕获真实实体并保留已预留身份；放出时若 Registry 缺失或 UUID
  不匹配则拒绝，不会静默绑定或迁移唯一 ID。
- 创造收纳箱不移除原实体，可重复生成无唯一 NPC ID 的模板实体。
- 复制/生成可继承 collective Group，但不会复制 individual NPC ID。

### 3.6 CNPC 兼容

- Runtime 不直接 import `noppes.*`，CustomNPC+ 缺失时仍可加载。
- 反射桥可读取已知 legacy `darkgrey_rpg.actor_id` 作为兼容 fallback。
- DGR 外置 Registry 优先于 legacy 字段；stale individual 会被拒绝。
- 当前检查到的 CustomNPC+ 1.11.1 fixed-v1 没有为所有死亡/复活实现提供可靠的
  old UUID → new UUID 稳定键，因此不声称自动把唯一 ID 迁移到无关新 UUID。
- 支持的安全路径是用指名器显式 transfer；是否能增加自动迁移必须先取得真实
  CNPC 生命周期证据。

## 4. 验证证据

### 4.1 冻结提交构建

环境：

```powershell
$env:JAVA_HOME='E:\Java\jdk1.8.0_471'
$env:GRADLE_USER_HOME='E:\Java\gradle'
.\gradlew.bat build --offline --no-daemon --no-configuration-cache --max-workers=1
```

结果：`BUILD SUCCESSFUL`。Java 编译、测试编译、JAR/reobf、sources JAR、
Spotless、Checkstyle 和 Gradle test 均通过。测试代码使用 `sun.misc.Unsafe`
时有 Java 8 警告，不是失败。

在最终构建前执行过 `spotlessApply`。对比格式化前后工作树，
`NEWLY_DIRTY_BY_FORMATTER=0`，未波及此前未修改的文件。

### 4.2 Focused Java Probe

最后一次边界修复后的命令：

```powershell
.\gradlew.bat compileJava compileTestJava nominatorStage4Probe `
  entityDgrIdentityResolverProbe copierTemplateActionCodecProbe `
  --offline --no-daemon --no-configuration-cache --max-workers=1
```

结果：

- `NOMINATOR_STAGE4_PROBE=PASS`
- `DGR_IDENTITY_EXTERNAL_PRECEDENCE=PASS`
- `DGR_IDENTITY_NOMINATOR_COMBINATION=PASS`
- `DGR_IDENTITY_STALE_INDIVIDUAL_REJECTED=PASS`
- `DGR_IDENTITY_GROUP_DEDUP=PASS`
- `DGR_IDENTITY_GROUP_ROUTING=PASS`
- `DGR_IDENTITY_EXACT_TYPE_GROUP_ROUTING=PASS`
- `COPIER_TEMPLATE_ACTION_CODEC=PASS`

开发过程中 Story Package、Story/Session/Task runtime、identity、Item、Copier、
Storage、persistence、codec 和 canonical coordinator 的 focused probes 也通过。
但冻结提交没有再一次性执行 `build.gradle.kts` 中全部 38 个 JavaExec probe；
这仍是最终候选的待办门禁。

### 4.3 Studio 自动测试

- Studio Core Release：`337/337` 通过。
- Studio WPF Release：`326/326` 通过。
- WPF 测试包含真实 Shell command 导出所选 Story Package。
- WPF 环境必须设置 `$env:windir=$env:SystemRoot`，否则可能出现 FontCache/URI
  假失败。

### 4.4 Studio 窗口验收

在冻结提交之前完成过真实 Studio 窗口自动化：

- 资源创建/删除/保存/重复/引用/滚动/重启恢复/进程存活与干净停止通过。
- 本地结果：
  `.tooling\2.1.3-acceptance\results\20260829T163554.095Z.json`。
- 0.3.0.0 迁移预览、隔离副本应用、legacy bytes 保留、10 个 canonical 文件、
  完整备份和成功日志通过。
- 本地截图：`.tooling\stage6\migration-ui-live\migration-preview.png`。

这些证据来自冻结提交之前；格式化和最终提交后没有重跑真实窗口。因此它们是强
回归证据，但不能替代最终候选的 fresh Studio acceptance。

### 4.5 尚未完成的真实游戏证据

截至本报告生成时：

- 没有 DarkGrey_RPG Java 客户端或服务端进程在运行。
- 端口 `25565` 没有监听。
- `run/server/eula.txt` 为 `eula=true`。
- `run/client/mods` 与 `run/server/mods` 均包含：
  - `+unimixins-all-1.7.10-0.3.1.jar`
  - `CustomNPC-Plus-1.11.1-fixed-v1.jar`

仍未在冻结提交上完成：Dedicated Server ready/save/stop、Minecraft Client
启动、指名器 GUI、复制器、两种收纳箱、双玩家隔离、多维度、CNPC Cloner、
死亡/复活及 PLAN Case A-L 的完整人工记录。

## 5. 已发布产物

GitHub Release 中三个资产均回读为 `state=uploaded`，远端 digest 与本地一致：

| 资产 | 字节数 | SHA-256 |
|---|---:|---|
| `darkgrey_rpg-0.3.1.0.jar` | 746,482 | `37FB0CB25F41BB6D626F4DD8B13ABA11CF58E6E23B868A52EEB138FCB2274F8E` |
| `darkgrey_rpg-0.3.1.0-sources.jar` | 331,045 | `BDD3A1F5EBC5BEF9EE8139943410FDDF2622B6F97A9843C51E5E2C706A75AE04` |
| `DarkGreyRPGStudio-0.3.1.0-dev.exe` | 141,247,190 | `42D5CF01AC76DC3D2074409EFA43D14FEDA84FBDB4C6E3204CE6E94A4F8897C9` |

Runtime JAR 已检查包含 `mcmod.info` 和六枚正式物品图标：

- `body.png`
- `editor.png`
- `nominator.png`
- `copier.png`
- `storage_box_open.png`
- `storage_box_closed.png`

## 6. Git 与工作树边界

已发布提交只包含 0.3.1.0 相关源码、测试、Studio、PLAN、README 和文档。
以下用户/环境状态被明确排除，不能由接手者 reset、clean、restore、提交或删除：

- `M .codex/config.toml`
- `M AGENTS.md`
- `D examples/phase4_project/**` 共六个文件
- `?? .dotnet-home/`
- `?? PLAN/DarkGrey_RPG_Studio_2.1.3_Plan.md`
- `?? PLAN/构思.docx`
- `?? PLAN/验收清单_0.3.0.0_0.3.1.0.md`（生成交接文档期间并发出现，来源未判定）

本报告和同目录 Handoff 是用户要求的新交接文件，生成后也是未跟踪文件；除非用户
再次授权，不要自动提交、推送、修改 Release、合并 `main` 或删除本地脏状态。

## 7. 原冻结报告生成时的结论

0.3.1.0 的主体架构和自动化实现已形成可继续开发的稳定冻结点，且开发版源码和
产物可从 GitHub 获取。最终版本仍缺 PLAN Stage 9 的 fresh 全量 probe、真实
Dedicated Server/Minecraft Client、Case A-L、CNPC 生命周期、多人/多维度和最终
人工验收。下一对话应从配套 Handoff 恢复，而不是把当前 Pre-release 当成最终
0.3.1.0。

## 8. 接手续开发结果（2026-08-30）

### 8.1 本轮代码修复

接手后先完成了冻结点、脏工作树和 38 个 JavaExec task 的复核。首次全量门禁为
`35/38`，随后只做了三个有界修复：

- `phase1ProjectProbe` 不再依赖用户已删除的 `examples/phase4_project/**`。
- 两个 canonical 正向夹具不再写入已禁止的 Task `activate`，并保持单一
  Settlement 语义。
- Story Package exporter 新增 fail-closed 预检：所选 Story 的 legacy 或
  canonical 图中若仍含跨 Story `enter_story`，在修改输出目录前拒绝导出，并给出
  必须执行 project-level migration 的诊断。旧输出保持原样。
- 对存在 canonical membership 的合法包，导出器始终建立严格 loader 所需的
  `stories`、`memberships`、`sessions`、`tasks` 四个 canonical 目录；空目录也会
  保留。没有 membership 时不会因项目中其他 Story 的 canonical 数据误建目录，
  重建时会清理旧 canonical root。

任务相关源码范围：

- `build.gradle.kts`
- `src/test/java/darkgrey/rpg/graph/canonical/CanonicalProjectContentLoaderProbe.java`
- `src/test/java/darkgrey/rpg/project/CanonicalProjectRepositoryProbe.java`
- `studio/src/DarkGreyRPG.Studio.Core/Packaging/StoryPackageExporter.cs`
- `studio/src/DarkGreyRPG.Studio.Tests/StoryPackageTests.cs`

### 8.2 自动门禁与 Studio

- Java 8 完整 `build + 38/38 JavaExec`：`BUILD SUCCESSFUL in 44s`，共 117 个
  PASS marker；日志：`build/dgr-0310-final-java-gate.log`。
- Studio Core Release：`341/341`；Studio WPF Release：`326/326`。
- Story Package focused tests：`5/5`，覆盖合法导出、legacy/canonical
  `enter_story` 拒绝、拒绝时输出不变和四个 canonical root。
- `git diff --check` 通过；只有既有 LF/CRLF 提示，无 whitespace error。
- 真实 Release WPF 资源创建/编辑/删除/复制/引用/保存/重启/干净退出脚本为
  PASS；当前窗口为 `1364x868`、119 DPI。100%/150% 的对照视觉仍是人工项。
- 真实迁移 UI 六项为 PASS：真实窗口、tracked project apply、legacy bytes
  保留、10 个 canonical 文件、完整备份、成功日志。截图：
  `.tooling/stage6/migration-ui-live/migration-preview.png`。
- 真实菜单中 `royal_mystery` 因兼容 `enter_story` 被明确拒绝，拒绝前后原 10 个
  文件与 10 个目录未变；`kingdom_route` 随后从同一真实窗口成功导出。
  证据：`.tooling/stage6/migration-ui-live/export-menu-evidence.json`、
  `royal-mystery-rejection.png`、`kingdom-route-export.png`。

### 8.3 Dedicated Server 与最终导出物

最终合法源包为：

```text
.tooling/stage6/migration-ui-live/DarkGrey-2.1-Acceptance/build/story_packages/kingdom_route
```

安装目录为：

```text
run/server/darkgrey_rpg_story_packages/kingdom_route
```

两边完整目录列表相同，六个文件逐项 SHA-256 相同，没有安装侧额外文件；manifest
SHA-256 为：

```text
C102B6D90230E5110CD3112DE04F22E4396103FE7000783F00EDCB0A839B23ED
```

Oracle JDK 8 Dedicated Server 实测结果：

- 第一次有效启动 `Done (0.976s)`，listener PID 与本次 Java 进程一致。
- `kingdom_route` 正常加载并可列出。
- 故意破坏 manifest 后 `/dgr reload` 拒绝坏包，旧 `kingdom_route` 定义仍可列出。
- 恢复原 manifest 后 reload 成功。
- 第二次启动 `Done (0.894s)`，再次加载同一故事包。
- 最终 fresh 导出物又完成一次启动、普通 reload、`save-all`、`stop`；服务端进程
  已退出且 TCP 25565 已释放。

证据：

- `.tooling/stage6/server-smoke-20260830-final/final-validation.txt`
- `.tooling/stage6/server-smoke-20260830-final/package-tree-hash-manifest.json`
- `.tooling/stage6/server-smoke-20260830-valid/commands-and-results.txt`

旧的无效 `royal_mystery` 安装物没有删除，而是可恢复地移到
`.tooling/stage6/server-smoke-20260830-valid/quarantine/royal_mystery`。既有
`run/server/darkgrey_rpg_project` 中的 `legacy_activate` 警告是另一份旧项目数据，
本轮没有修改，也不影响安装故事包的 load/list 证据。

### 8.4 Minecraft Client 兼容门禁

带 CustomNPC+：

- Oracle JDK 8 客户端进入真实 Minecraft 1.7.10 主菜单。
- DarkGrey_RPG `0.3.1.0`、UniMixins、CustomNPC+ 实际加载，画面显示
  `13 mods loaded, 13 mods active`。
- 通过可见 `Quit Game` 自然退出；日志出现 `Stopping!`，Gradle
  `BUILD SUCCESSFUL` 且 exit code `0`，无本轮 client 进程或窗口残留。
- 证据：`.tooling/stage6/client-smoke-20260830-graceful/acceptance-summary.txt`、
  `main-menu-exit0-oracle-jdk8-471.png`。

不带 CustomNPC+：

- 将目标 JAR 可恢复地移出 `run/client/mods` 后，客户端仍进入真实主菜单，
  DarkGrey_RPG `0.3.1.0` 与 UniMixins 正常，画面显示
  `12 mods loaded, 12 mods active`，证明没有 CNPC 硬依赖。
- 同样通过可见 `Quit Game` 自然退出，Gradle exit code `0`。
- 测试后 JAR 已恢复到原路径，大小 `17,830,105` 字节，SHA-256 精确恢复为：
  `B448EAE9D7B2EE7C2DC1DCD7125DFC770E5741DDEA6E54A670F4FEFFD521B03D`。
- 证据：`.tooling/stage6/client-smoke-20260830-no-cnpc/acceptance-summary.txt`、
  `postflight.txt`、`main-menu-oracle-jdk8-471.png`。

这两组 smoke 证据只证明模组加载、主菜单兼容和自然退出。后续 Priority 5 的 R2/R3
隔离验收另行证明了真实服务器连接与世界渲染，但仍不能据此声称指名、任务、复制、
收纳、多人或 CNPC 游戏行为通过。

### 8.5 Priority 5 隔离 fixture 与真实世界边界

为避免污染 `run/server/phase1-runtime-world`，本轮创建了当前 schema 的独立验收
fixture 和一次性世界副本。fixture 使用：

- Story：`acceptance_bartender`
- Actor：schema 3 individual `tavern_boss`
- Sessions：`acceptance_offer`、`acceptance_thanks`
- Task：`acceptance_slime_task`，无 legacy `activate`，Objective 默认激活并连接唯一
  Settlement

第一次真实 reload 暴露出 fixture 的项目根契约错误：`project.json` 误放了
`memberships`。运行时明确拒绝该字段；随后按 Java/Studio 共同契约修正为仅包含
`schema_version / id / display_name`，并把 Story 归属移到 schema 2 的
`resources/canonical/memberships/acceptance_bartender.json`。修正后八个 JSON 静态
验证通过，真实服务端 reload 也通过。

R2 已证明：

- 服务端在 `dgr-0310-acceptance-world-r2` 到达 `Done (0.890s)`。
- `/dgr reload` 加载 `Priority 5 Gameplay Acceptance Fixture`，无 validation error。
- `/dgr story list` 显示 `acceptance_bartender`；`/dgr task list` 显示
  `acceptance_slime_task`。
- 带 CustomNPC+ 的真实 Developer 客户端连接 `127.0.0.1:25565` 并渲染一次性世界。
- 本轮停在 joined-client 边界；没有狼绑定、Session 接受或 `ACTIVE 0/10` 证据。

R3 试图进一步完成 Case A，但中文 IME/窗口焦点截走预期键盘输入；证据记录
`GUI attempts used=0/3`，没有打开 Nominator entity GUI，也没有生成
`darkgrey_rpg_npc_identities.dat` 或 `darkgrey_rpg_nominator.dat`。因此在 R3 截止时
Case A 仍是 **未验证**，不能把热栏物品、世界截图或服务端 Done 推断为绑定成功。

后续 `live-case-a-main` 使用已验证的窗口输入通道完成了用户报告问题的修复与实机
复验：

- 1.7.10 的 `EntityInteract` 取消后会回落到 `RIGHT_CLICK_AIR`，旧实现因此把实体与
  空气都导向物品页；现改为客户端 `MouseEvent` 在 vanilla 分派前区分 ENTITY/MISS。
- 客户端不再提交不可靠的实体 UUID，只提交当前实体 ID；服务端重新解析实体并校验
  held tool、维度和 8 格距离，再返回真实 UUID。
- 右键刷怪蛋产生并持续存在的 Villager，可信打开实体页并显示
  `Villager · Villager`；对空气则打开物品页。
- 实体页和物品页均在 854x480（逻辑 427x240）与 1902x954 下通过排版复验，没有
  标签/值/按钮遮挡。
- 创造物品栏第 2 页真实出现 `DarkGrey RPG` 专属标签，包含六个模组物品。
- `spawn-npcs=false` 是刷怪蛋村民瞬间消失的直接运行配置原因，已改为 `true`；真实
  刷怪蛋生成的村民跨等待后仍在场，并被指名器成功命中。

随后继续完成了玩家 A 的 Case A/Stage 6 路径：

- 第一只 Villager 绑定 `tavern_boss`，重开 GUI 显示该 individual；重启服务端与
  客户端后仍可回读，身份 SavedData 将 UUID 与 Actor 正确关联。
- 第二只 Villager 尝试绑定同一 individual 后仍显示“个体：无”，revision 未增加；
  第一只 Villager 再次回读仍持有 `tavern_boss`，冲突拒绝成立。
- 普通右键第一只 Villager 打开 `acceptance_offer`；继续后 Task 进入 `ACTIVE 0/10`。
- 玩家真实击杀将任务推进到 `10/10`；SavedData 为 `SETTLED`、Objective
  `COMPLETED`、result port `done`。
- `acceptance_thanks` 显示 10 铜币，铜币实际进入背包；用户之后主动将其丢弃，因而
  后续 playerdata 不含铜币不能解释为发奖失败。
- Continue 正常关闭；最终 SavedData 为 Story `TERMINATED`、Session instances 空、
  reward receipt 已记录。
- 完成后真实执行 `dgr reload` 和 `save-all`；Story 仍为 `TERMINATED`，Task 仍为
  `SETTLED 10/10`，Objective 仍为 `COMPLETED`，活动 Session/continuation 为 0，
  reward receipt 为 1。

这证明 PLAN Stage 6 第 1-12、14 步和 Case A 的唯一身份语义。实体种类为两只原版
Villager，并非验收文字中的狼；显式 Transfer 仍未验证。

随后以独立离线用户名 `VerifierB` 连接同一服务器。服务端为其创建 UUID
`9d9ed6a7-15d7-35da-882e-4542e1a11f91` 的 playerdata；`save-all` 后，DGR 玩家、
Story、Session、Task 和 reward SavedData 仍只包含玩家 A，`VerifierB` 完全不存在于
这些进度集合中。因此第 13 步玩家 B 未开始对照通过，Stage 6 第 1-14 步全部闭环。

直接证据：

- `.tooling/stage6/gameplay-acceptance-20260830/fixture-summary.txt`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a/ACCEPTANCE-REPORT.md`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-r2/ACCEPTANCE-REPORT.md`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-r2/screenshots/desktop-joined.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-r3/ACCEPTANCE-REPORT.md`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-r3/screenshots/desktop-chat-test.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/ACCEPTANCE-REPORT.md`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/entity-gui-final-854x480.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/inventory-gui-final-854x480.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/entity-gui-final-large.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/creative-page2-bottom-tab.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/spawn-egg-immediate-large.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/spawn-egg-after-6s-large.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/case-a-first-after-restart.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/case-a-second-conflict-reopen.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/case-a-first-final-reopen.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/case-a-active-0-of-10.png`
- 用户截图 `codex-clipboard-11b27d4b-b35a-46b4-a359-196f4698ef26.png`（感谢会话/10 铜币）
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/POST-RELOAD-NBT.txt`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/PLAYER-B-ISOLATION.txt`

所有尝试均使用一次性世界。测试结束后 `level-name`、Project 与 Story Package 目录已
恢复默认值；`spawn-npcs=true` 作为本次村民消失修复被有意保留。TCP 25565/32145
无监听，Dedicated Server 和本轮 Minecraft 客户端均已自然退出。

### 8.6 当前门禁状态

| 接力项 | 当前状态 | 边界 |
|---|---|---|
| Priority 0 冻结点复核 | PASS | 本地/远端/标签仍指向原冻结提交；本地修订未提交 |
| Priority 1 全部 Java Probe | PASS | 38/38，与完整 build 同次执行 |
| Priority 2 Studio | PASS（当前 DPI） | 自动测试、真实迁移 UI、真实菜单导出；100%/150% 对照仍人工 |
| Priority 3 Dedicated Server | PASS | 完整包、事务 reload、重启、save/stop；不是游戏内功能证明 |
| Priority 4 Client smoke | PASS | 带/不带 CNPC 均到真实主菜单并自然退出；R2/R3 另有带 CNPC 的真实服务器连接/世界渲染 |
| Priority 5 Case A-L | PASS / GO | Case A-L 与 Stage 6 第 1-14 步全部通过；F/G/H 正式链路见 8.9 |
| Priority 6 CNPC 生命周期 | PASS（安全边界） | Cloner 与收纳真实语义通过；死亡后没有可靠稳定迁移键时保持 fail-closed + 显式 transfer |
| Priority 7 最终收口 | 本地 GO / 未发布 | 最终构建、回读、实机与回归证据齐全；尚未获得提交/发布授权 |

### 8.7 当前结论与工作树边界

本轮已把 0.3.1.0 从“冻结后未做 server/client”推进到“自动化、Studio 导出、
Dedicated Server、带/不带 CNPC 的客户端主菜单兼容均有 fresh 证据”，并修复了一个
会生成服务端不可加载包的真实导出缺陷。Priority 5 还新增了可复现 fixture、真实
reload、客户端 joined、指名器页面分流与布局、创造标签和村民生成证据；其后完成
Case A-L、三玩家隔离/重启、跨维度共享及 CNPC Cloner/收纳语义。后续客户端/服务端
验收均由开发方自行完成，没有要求用户介入；临时 `/dgr debug` 入口只调用正式 Runtime
与 Minecraft API，没有另建 Harness、Fixture Framework、Mock 或 Debug Framework。

当前最终结论为本地 `GO`。本轮没有 commit、push、tag、PR、Release 或资产上传；
原 GitHub Pre-release 仍只对应冻结提交 `cd73df31dc6b4aa6e14f202019936447beea745e`。
第 6 节列出的用户/环境脏状态全部保留，不能把本轮本地候选误写成已发布版本。

### 8.8 Case B-L 自主实机续验与有界修复

用户在 2026-08-30 授权开发方自行操作客户端/服务端，并允许在既有 `/dgr`
命令树下增加极少量临时调试入口，前提是入口只能直接调用现有 DGR 服务/GUI。本节记录
首次续验；其中曾把该限制错误扩大到正式 PLAN 功能，F/G/H 的最终纠正结果以 8.9 为准。

| Case | 结果 | 真实边界 |
|---|---|---|
| B 集体角色 | PASS | `guards` 合法成员累计 `10/10`；未绑定实体保持 `0/10` |
| C Item ID | PASS | `royal_key` 完全相同栈完成；不同 NBT 栈不计数 |
| D Item Group | PASS | `sword` 模糊组可由剑类物品完成 |
| E 多 Group | PASS | 同一铁剑同时命中 `sword`、`iron_sword`、`weapon` |
| F 跨故事逻辑 | PASS | 正式跨 Story 公共 Logic 图与 Runtime 传播已实现；玩家 A 触发、玩家 B 隔离、ONCE 不重复均通过 |
| G 条件 | PASS | 即时双出口通过；持久化等待在逻辑值动态变化后恢复并沿已连接出口继续 |
| H Task 生效条件 | PASS | 正式 Minecraft 世界昼夜绑定已接入 Task logic input；夜晚推进、白天暂停且不清零、再入夜完成 |
| I 结算优先级 | PASS | 多个条件为 true 时只选择最上方 `perfect`，Story 只沿一条路径终止 |
| J 多玩家 | PASS | A `10/10`、B `3/10`、C 未开始；服务端重启后仍隔离保持 |
| K CNPC 复制 | PASS | CNPC Cloner 副本不再解析出唯一 `tavern_boss`，原 NPC 保持身份 |
| L 收纳箱 | PASS | 生存捕获/放出恢复唯一身份；创造不移除原实体、可无限生成、继承 Group、不复制唯一 ID |

Case K 实机暴露出 CustomNPC+ Cloner 会保留源实体 UUID。DGR 旧解析器因此会把原体和
副本都解析为同一个 individual。修复后，同一世界中 UUID 相同的 CustomNPC+ 实例只允许
最早/最低 entity ID 的 canonical host 解析 individual；collective Group 仍可继承。
真实 Cloner 复验与 `DGR_IDENTITY_SAME_UUID_CLONE_GUARD=PASS` 同时通过。

Case L 又暴露出 CustomNPC+ 覆盖 `setDead()` 为死亡/复活流程，生存捕获时原 NPC 不会
真正移除，随后放出会制造重复实体。兼容层现在只在收纳捕获路径调用 CustomNPC+ 已存在的
公开 `delete()`；非 CNPC 仍使用原 `setDead()`。修复后生存模式只恢复一个带
`tavern_boss` 的实体；创造模式连续放出两个新实体时，两者只解析 `guards` Group，原实体
仍独占 `tavern_boss`。

本轮还修复了复制器模板 NBT 的重复/崩溃边界，以及复制器、收纳箱方块右键由客户端取消
导致服务端收不到交互的问题；对应 `ENTITY_TOOLS_STAGE5_PROBE=PASS`。

临时入口全部位于 `/dgr debug`，包括 `nominator clear_type_group`、item exact/group/fuzzy
绑定、Task kill event、CNPC 最近实体绑定/列出和 storage interact/release/status。它们只
调用现有 Registry、事件总线、CNPC bridge 与收纳服务，不构成新通用基础设施；正式发布前
仍需决定删除或显式标注为开发命令。

直接证据目录：

- `.tooling/stage6/gameplay-acceptance-20260830/autonomous-case-b-l/`
- `case-b-complete-10of10.png`、`case-b-negative-unbound-0of10.png`
- `case-c-exact-complete.png`、`case-c-exact-negative-0of1.png`
- `case-d-sword-complete.png`、`case-e-three-groups-complete.png`
- `case-i-perfect-settlement.png`
- `case-j-player-b-3of10.png`、`case-j-player-c-empty.png`
- `case-j-player-a-complete-after-restart.png`、`case-j-player-c-empty-after-restart.png`
- `case-k-cnpc-clone-unique-id-guard.png`
- `case-l-survival-restored-identity.png`
- `case-l-creative-infinite-group-no-unique-id.png`

自主验收结束后客户端与 Dedicated Server 均正常保存并退出。为允许 hostile-mob 验收，
`run/server/server.properties` 的 `spawn-monsters=true` 被有意保留；此前 `false` 会让召唤的
史莱姆瞬间消失。

最终 fresh Java 8 门禁为：`compileJava + testClasses + jar + reobfJar + sourcesJar +
38/38 JavaExec`，`BUILD SUCCESSFUL in 43s`，共 122 个 PASS marker。32 个 F/G/H 相关
Java 文件逐个通过 Spotless IDE hook，且定向 `git diff --check` 通过。全仓 `spotlessCheck` 仍会被既有 UI
等 21 个无关脏文件阻断；没有运行全局 `spotlessApply`，以免改写用户工作。

最终 Runtime JAR 为 `build/libs/darkgrey_rpg-0.3.1.0.jar`，`779,979` 字节，SHA-256
`D0C1273C2844DCA9DD8FBF824FC2F89EFCB0CA671E597706A9218BCA70D6259D`；sources JAR 为
`347,129` 字节，SHA-256
`8CDAA0DCAF2337D546B1AE9EB4747CFE7F2CDE4F2232A074E28517E543FDAFD5`。这些仍是本地候选，
没有提交或上传到既有开发版 Release。

因此当前精确结论是：Case A-L、Stage 6 第 1-14 步及 0.3.1.0 PLAN Definition of Done
全部满足，版本为本地未提交 `GO` 候选；发布状态仍是“未授权、未执行”。

### 8.9 Case F/G/H 正式恢复施工与最终 DoD

用户澄清后，正式产品功能与临时验收工具重新严格分界：禁止的是为了操作 Minecraft
另造通用验收基础设施，不是禁止实现 PLAN 合理需要的最小正式 Runtime、数据与事件层。
据此完成以下生产实现：

- Case F：增加项目级跨 Story 公共 Logic 图、稳定端口连接、Story Package 合并、按玩家
  UUID 的固定点传播、ONCE/REPEATABLE 启动语义，以及目标已活动时的批量 logic 更新。
- Case G：Condition 等待态与 logic input/output 持久化；`setLogicInputs` 原子重算后可从
  等待态恢复，并沿实际已连接出口继续执行。只连“是”的 false→true 与只连“否”的
  true→false 两个方向都有持久化回归，前者另有真实服务器链路。
- Case H：Task `source` 与正式世界逻辑绑定，当前生产源为 `minecraft:day` 和
  `minecraft:night`；Forge 每 20 tick 同步实际世界时间，暂停只改变 Objective gate，
  不清除已有进度。
- Studio：补齐 Logic 启动触发、Task `source`、项目级跨 Story 图编辑/重命名/删除保护，
  以及 Story Package 导出、加载和合并。只读列表在目录不存在时不再产生磁盘副作用，
  修复了迁移原子性回归。

自动化证据：

- `CANONICAL_STORY_CROSS_STORY_PUBLIC_LOGIC=PASS`
- `CANONICAL_STORY_CROSS_STORY_DYNAMIC_RESUME=PASS`
- `CANONICAL_STORY_DYNAMIC_LOGIC_RESUME=PASS`
- `CANONICAL_TASK_WORLD_LOGIC_LIFECYCLE=PASS`
- `STORY_PACKAGE_CROSS_STORY_LOGIC_MERGE=PASS`
- Studio Core `344/344`、WPF `328/328`
- Java 最终门禁日志：`build/dgr-0310-fgh-final-java-gate.log`

真实服务器/客户端链路使用
`.tooling/stage6/gameplay-acceptance-20260830/fixture-project`，证据位于
`.tooling/stage6/gameplay-acceptance-20260830/live-case-fgh-20260830/`：

1. 服务端重启前，源 Story 与等待 Story 均为 `ACTIVE`，Condition 为 false；逻辑启动
   Story 不存在。保存、停止并重启后，等待态与未启动状态保持。
2. 将源公共 Logic 置 true 后，源 Story、等待 Story、logic-start Story 均沿真实连接
   终止；重复置 true 不会重启 ONCE 目标。
3. 独立 `VerifierB` 客户端连接同一服务器后，三个 Story 与 H Task 均不存在，证明玩家
   UUID 隔离。
4. H 在夜晚推进到 `7/10`；切到白天后追加 3 次事件仍保持 `7/10 INACTIVE`；再次入夜
   后相同事件推进到 `10/10 SETTLED`。
5. Developer 通过正式维度传送进入 Nether 后，F/G Story 仍为终态、H Task 仍为
   `SETTLED 10/10`；返回主世界后状态不变，证明跨维度共享不等于世界公共进度。

最终本地结论：0.3.1.0 PLAN/DoD 为 `GO`。发布动作不属于本次授权，故没有 commit、
push、tag、PR、Release 或资产上传。

## 8.10 2026-08-31 源码上传补记

用户已授权将当前完成候选提交并推送到 `origin/codex/0.3.1.0`。本次只更新源码开发
分支及随源码提交的测试、验收记录、Report/Handoff；不创建或移动 tag，不合并
`main`，也不更新既有 `0.3.1.0_开发版` GitHub Release 与二进制资产。
