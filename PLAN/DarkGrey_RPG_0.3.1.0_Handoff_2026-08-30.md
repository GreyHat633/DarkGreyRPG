# DarkGrey_RPG 0.3.1.0 开发接力 Handoff

> 给下一对话：这是 0.3.1.0 的开发版冻结点，不是最终完成声明。
>
> 工作目录：`E:\Java\MinecraftMod\DarkGrey_RPG`
>
> 分支：`codex/0.3.1.0`
>
> 接力起点：`cd73df31dc6b4aa6e14f202019936447beea745e`

> 2026-08-30 接手续开发更新：Priority 0-4 已完成；带/不带 CustomNPC+ 的真实
> 主菜单与自然退出已通过，Priority 5 的隔离 fixture 也已真实 reload，带 CNPC 的
> Developer 客户端已进入一次性服务器世界。后续已可信通过 Nominator 实体/空气
> 分流、双分辨率排版、专属创造标签和刷怪蛋村民持续存在；Case A 的唯一绑定、
> 第二实体冲突拒绝、重启回读已用两只 Villager 实证，玩家 A 的 Stage 6 第 1-12 步
> 已贯通到 10 铜币与 Story `TERMINATED`，完成后 reload 保持终态，独立 `VerifierB`
> 登录也证明未启动隔离。Stage 6 第 1-14 步与 Case A-L 已全部完成自主实机验收。
> Case F 的跨 Story 公共 Logic、Case G 的持久化等待后动态恢复、Case H 的实际世界
> 昼夜绑定均已作为正式产品功能完成；玩家 B 隔离、服务端重启及跨维度共享也已实证。
> 当前 PLAN/DoD 为 GO；2026-08-31 用户已授权把完成候选源码提交并推送到现有
> `codex/0.3.1.0` 开发分支。该授权不含 tag、PR、Release 或二进制资产更新。

## 0. 接手后先做什么

按以下顺序读取，不要先修改代码：

1. `PLAN/DarkGrey_RPG_0.3.1.0_Construction_Plan.md`
2. `PLAN/DarkGrey_RPG_0.3.1.0_Development_Report_2026-08-30.md`
3. 本 Handoff
4. `docs/0.3.1.0_ARCHITECTURE.md`
5. `docs/0.3.1.0_MIGRATION.md`
6. `docs/0.3.1.0_ACCEPTANCE.md`
7. `docs/0.3.1.0_RELEASE_NOTES.md`

然后执行只读检查：

```powershell
Set-Location -LiteralPath 'E:\Java\MinecraftMod\DarkGrey_RPG'
git status --short
git branch --show-current
git rev-parse HEAD
git ls-remote origin refs/heads/codex/0.3.1.0
git ls-remote origin 'refs/tags/0.3.1.0_开发版^{}'
```

预期本地 `HEAD`、远端分支和 peeled tag 均为：

```text
cd73df31dc6b4aa6e14f202019936447beea745e
```

如果不一致，先调查，不要 reset、rebase 或覆盖工作树。

## 1. 不能破坏的现有工作树

接力前已有以下非本任务状态：

```text
 M .codex/config.toml
 M AGENTS.md
 D examples/phase4_project/actors/test.json
 D examples/phase4_project/dialogues/tavern_complete.json
 D examples/phase4_project/dialogues/tavern_offer.json
 D examples/phase4_project/quests/kill_10_slimes.json
 D examples/phase4_project/stories/tavern_slime_request.json
 D examples/phase4_project/stories/uncategorized.json
?? .dotnet-home/
?? PLAN/DarkGrey_RPG_Studio_2.1.3_Plan.md
?? PLAN/构思.docx
?? PLAN/验收清单_0.3.0.0_0.3.1.0.md
```

本 Report 和 Handoff 生成后也会是 `PLAN/` 下的新未跟踪文件。
其中 `验收清单_0.3.0.0_0.3.1.0.md` 在交接文档生成期间并发出现，来源未判定，
同样必须保留。

规则：

- 不运行 `git reset --hard`、`git clean` 或广域 `git checkout/restore`。
- 不把上述文件加入 0.3.1.0 提交。
- 只使用显式路径 `git add -- <paths>`。
- 提交前必须检查 `git diff --cached --name-status` 和
  `git diff --cached --check`。
- 未获新授权时，不提交/推送本交接文件，不创建新标签，不覆盖现有 Release，
  不合并 `main`。

## 2. 当前已完成的实现边界

可以把下列内容视为已实现、需回归而非推翻的架构：

- Story Flow 是 Story 内唯一流程执行源。
- Project Story Graph 只编辑跨 Story ◆ 逻辑连接，不是第二套 Flow。
- Flow：输出单目标、输入可 fan-in；Logic：输入单来源、输出可 fan-out。
- 所有连接使用 stable port ID，不使用显示名作为主键。
- Task 是纯逻辑，禁止重新引入【激活】、Task Flow、并发、返回/入口。
- Objective 动态 gate 暂停时不清进度；Settlement 只选最上方第一个 true。
- NPC ID 只存外置 WorldSavedData，禁止写回普通实体 NBT。
- Item ID/Group 与 Actor individual/collective 已有正式 Schema。
- Story Package loader/reload 是服务端 authoritative、事务式、失败保留旧定义。
- `give_item` 只接受 DGR `item_id`，由服务端执行并防重复发奖。
- CustomNPC+ 是可选依赖，Runtime 不得增加直接 `noppes.*` 编译依赖。
- 无可靠 old UUID → new UUID 证据时，CNPC 唯一身份迁移必须 fail-closed，
  使用指名器显式 transfer。
- 素体和编辑器只是 0.3.1.0 占位物品，禁止借机扩展完整 Native NPC 系统。
- 六个模组物品使用 `DarkGrey RPG` 专属创造标签页。
- Nominator 客户端在 `MouseEvent` 中区分 ENTITY/MISS；实体请求只发送 entity ID，
  服务端负责解析真实 UUID 并复核 held tool、维度和距离。
- `run/server/server.properties` 的 `spawn-npcs=true` 是必须保留的运行修复；否则原版
  村民会在生成后被服务端规则立即移除。

## 3. 当前发布状态

- GitHub Pre-release 名称与标签：`0.3.1.0_开发版`
- URL：<https://github.com/GreyHat633/DarkGrey_RPG/releases/tag/0.3.1.0_%E5%BC%80%E5%8F%91%E7%89%88>
- 分支：`origin/codex/0.3.1.0`
- 提交：`cd73df31dc6b4aa6e14f202019936447beea745e`
- Release 资产：Runtime JAR、sources JAR、Studio EXE，远端均为
  `state=uploaded`，hash 见 Report。

不要把这个开发版 Release 改名为最终版，也不要删除或覆盖资产。最终 0.3.1.0
是否沿用标签、创建新候选或开 PR，应在完成全部门禁后由用户明确决定。

## 4. 推荐的续开发顺序

### Priority 0 — 冻结点复核（已完成）

1. 核对 Git/脏状态/当前进程。
2. 确认没有 DarkGrey_RPG Java 进程，没有 25565 listener。
3. 检查 JDK、Gradle cache、.NET SDK 路径。
4. 不修改代码，先跑冻结提交的全量自动门禁。

### Priority 1 — 全部 38 个 JavaExec Probe（已完成）

接手续开发最终结果：`compileJava + testClasses + jar + reobfJar + sourcesJar +
38/38 JavaExec` 同次通过，122 个 PASS marker，`BUILD SUCCESSFUL in 43s`。
日志：`build/dgr-0310-fgh-final-java-gate.log`。首次 35/38
暴露的三个夹具/任务定义问题已做有界修复；不要恢复对用户已删除 examples 的依赖，
也不要重新引入 Task `activate`。

使用 Java 8 和 E 盘 Gradle cache：

```powershell
$env:JAVA_HOME='E:\Java\jdk1.8.0_471'
$env:GRADLE_USER_HOME='E:\Java\gradle'

$probeTasks = Select-String -LiteralPath 'build.gradle.kts' `
  -Pattern 'tasks\.register<JavaExec>\("([^"]+)"\)' |
  ForEach-Object { $_.Matches[0].Groups[1].Value }

if ($probeTasks.Count -ne 38) {
    throw "Expected 38 JavaExec probes, found $($probeTasks.Count)."
}

.\gradlew.bat build @probeTasks `
  --offline --no-daemon --no-configuration-cache --max-workers=1
```

要求：

- 保存完整命令和所有 PASS marker。
- 任一失败先判断是代码回归、环境还是测试夹具，不要为了 PASS 绕过真实语义。
- 修复必须是有界修复；修复后重跑失败 probe、相邻 probe 和完整门禁。
- 注意网络 discriminator：Dialogue `0-7`、Nominator `8-13`、Copier template
  action `14`，禁止再次冲突。

### Priority 2 — Studio 冻结版复验（已完成，DPI 对照除外）

接手续开发最终结果：Core `344/344`、WPF `328/328`；Story Package focused
`5/5`。真实迁移窗口、真实菜单拒绝 `royal_mystery` 的跨 Story `enter_story`、
以及真实菜单成功导出 `kingdom_route` 均已有证据。100%/150% 视觉对照仍是人工项。

```powershell
$env:windir=$env:SystemRoot
$env:DOTNET_CLI_HOME='E:\Java\MinecraftMod\DarkGrey_RPG\.dotnet-home'
$dotnet='E:\Java\dotnet-sdk-10\dotnet.exe'

& $dotnet test `
  'studio/src/DarkGreyRPG.Studio.Tests/DarkGreyRPG.Studio.Tests.csproj' `
  --framework net10.0 --no-restore --configuration Release `
  --logger 'console;verbosity=minimal'

& $dotnet test `
  'studio/src/DarkGreyRPG.Studio.Wpf.Tests/DarkGreyRPG.Studio.Wpf.Tests.csproj' `
  --framework net10.0-windows --no-restore --configuration Release `
  --logger 'console;verbosity=minimal'
```

预期基线：Core `337/337`，WPF `326/326`。测试数变化必须解释。

随后重新运行真实窗口验收，不只运行 ViewModel test：

```powershell
& 'studio/qa/2.1.3-resource-creation-ui-acceptance.ps1'
& 'studio/qa/0.3.0.0-migration-ui-acceptance.ps1'
```

必须补充冻结提交上的 Story Package 菜单导出实窗证据：选择 Story、导出、检查
manifest、资源闭包、版本 `0.3.1.0` 和目标目录，然后关闭 Studio 并确认无残留进程。

### Priority 3 — Dedicated Server smoke（已完成）

接手续开发结果：最终 `kingdom_route` 源包与安装包六文件 hash 和完整目录列表相同；
manifest SHA-256 为
`C102B6D90230E5110CD3112DE04F22E4396103FE7000783F00EDCB0A839B23ED`。
有效包启动、正常 reload、坏 manifest 拒绝并保留旧定义、恢复、第二次启动、
`save-all`、`stop` 和 25565 释放均通过。直接证据位于：

- `.tooling/stage6/server-smoke-20260830-final/final-validation.txt`
- `.tooling/stage6/server-smoke-20260830-final/package-tree-hash-manifest.json`
- `.tooling/stage6/server-smoke-20260830-valid/commands-and-results.txt`

当前准备状态：

- `run/server/eula.txt`：`eula=true`
- `run/server/mods/+unimixins-all-1.7.10-0.3.1.jar`
- `run/server/mods/CustomNPC-Plus-1.11.1-fixed-v1.jar`

建议先用冻结 JAR启动 Dedicated Server：

```powershell
$env:JAVA_HOME='E:\Java\jdk1.8.0_471'
$env:GRADLE_USER_HOME='E:\Java\gradle'
.\gradlew.bat runServer `
  --offline --no-daemon --no-configuration-cache --max-workers=1
```

验收记录必须包含：

1. Forge、DarkGrey_RPG `0.3.1.0`、UniMixins、CustomNPC+ 实际加载日志。
2. 服务端出现 `Done`，25565 监听且进程对应正确。
3. 故事包安装目录和加载摘要。
4. `/dgr reload` 正常包成功。
5. 故意损坏包 reload 失败，旧定义仍可用。
6. `save-all` 后 `stop`，进程退出且端口释放。
7. 重启后 Player/NPC/Item/Group 数据仍在。

不要把 `compileJava`、probe 或“服务端启动到 Done”描述为游戏内功能通过。

### Priority 4 — Minecraft Client smoke（已完成；世界连接另有 Priority 5 证据）

接手续开发结果：

- 带 CustomNPC+：真实 Minecraft 1.7.10 主菜单，13/13 mods active，通过可见
  `Quit Game` 自然退出，Gradle exit code `0`。
- 不带 CustomNPC+：临时隔离 JAR 后真实主菜单 12/12 mods active，同样自然退出；
  JAR 已按原路径、`17,830,105` 字节和 SHA-256
  `B448EAE9D7B2EE7C2DC1DCD7125DFC770E5741DDEA6E54A670F4FEFFD521B03D`
  精确恢复。
- 后续 R2/R3 已由带 CustomNPC+ 的 Developer 客户端真实连接一次性服务器世界并
  渲染画面；这只补齐连接/渲染边界，不代表 Priority 5 的指名或任务行为通过。

直接证据位于：

- `.tooling/stage6/client-smoke-20260830-graceful/acceptance-summary.txt`
- `.tooling/stage6/client-smoke-20260830-no-cnpc/acceptance-summary.txt`
- `.tooling/stage6/client-smoke-20260830-no-cnpc/postflight.txt`

`run/client/mods` 已有相同的 UniMixins 与 CustomNPC+。使用 Java 8：

```powershell
$env:JAVA_HOME='E:\Java\jdk1.8.0_471'
$env:GRADLE_USER_HOME='E:\Java\gradle'
.\gradlew.bat runClient `
  --offline --no-daemon --no-configuration-cache --max-workers=1
```

最低证据：

- `fml-client-latest.log` 中模组版本、依赖和注册无错误。
- Minecraft 主窗口可见并进入测试世界/服务器。
- 干净退出；若 1.7.10 留下非守护线程，记录进程、日志和安全终止方式。

必须分别测试：

1. 带 CustomNPC+。
2. 临时隔离 CustomNPC+ 后无硬依赖启动。

移动/隔离 mod 前必须解析并验证目标在 `run/client/mods` 内，且测试后恢复；不要
删除原 JAR。

### Priority 5 — PLAN Case A-L 游戏内矩阵

以下是 Case A-L 的可复现测试世界、已完成实证与剩余边界：

当前可直接复用的 fixture：

- `.tooling/stage6/gameplay-acceptance-20260830/fixture-project`
- Story `acceptance_bartender`
- individual Actor `tavern_boss`
- Sessions `acceptance_offer`、`acceptance_thanks`
- 无 `activate` 的 Task `acceptance_slime_task`

真实边界已经推进到：修正后的 fixture 通过 `/dgr reload`，Story/Task 可列出，带
CustomNPC+ 的 Developer 客户端可连接并渲染一次性世界；`live-case-a-main` 又真实
证明了实体/空气页面分流及 854x480、1902x954 排版。之后用两只 Villager 完成
`tavern_boss` 唯一绑定、第二实体冲突拒绝、重启回读，并通过实际交互贯通 Session、
Task `0/10` -> `10/10`、Settlement、感谢会话、10 铜币和 Story `TERMINATED`。

Case A 精确边界：唯一身份语义已通过，但实体种类是 Villager，不是计划示例中的狼；
显式 Transfer 尚未做。奖励确实进入背包，用户随后主动丢弃；不要把后续 playerdata
缺少铜币误判为奖励失败。Stage 6 第 14 步已通过：完成后 `dgr reload` 保持 Story
`TERMINATED`、Task `SETTLED 10/10`、Objective `COMPLETED`、零活动 Session 和一个
reward receipt。随后独立 `VerifierB` 登录同一服务器，`save-all` 后其 UUID 不存在于
任何 DGR 玩家、Story、Session、Task 或 reward SavedData；第 13 步也已通过。

最新直接证据：

- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-r2/ACCEPTANCE-REPORT.md`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-r3/ACCEPTANCE-REPORT.md`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-r3/screenshots/desktop-chat-test.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/ACCEPTANCE-REPORT.md`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/entity-gui-final-large.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/inventory-gui-final-large.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/creative-page2-bottom-tab.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/spawn-egg-after-6s-large.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/case-a-first-after-restart.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/case-a-second-conflict-reopen.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/case-a-first-final-reopen.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/case-a-active-0-of-10.png`
- 用户截图 `codex-clipboard-11b27d4b-b35a-46b4-a359-196f4698ef26.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/POST-RELOAD-NBT.txt`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-a-main/PLAYER-B-ISOLATION.txt`

当前实机运行仍用于最终 SavedData 核验；收口时正常停止服务端/客户端，将
`level-name`、Project 与 Story Package 目录恢复默认值，并保留 `spawn-npcs=true`。

用户最新明确要求：后续游戏内验收由开发方自行操作客户端/服务端，不再要求用户手动
介入。允许在现有 `/dgr` 命令系统上增加极少量临时调试入口，并直接调用既有 DGR
服务/GUI；不得为验收另造通用 Harness/Fixture Framework/Scenario/Mock/Debug
Framework。该限制不适用于 PLAN 正式产品功能所需的最小生产 Runtime、数据或事件实现。

- A：狼绑定唯一 `tavern_boss`，交互触发，第二实体冲突拒绝。
- B：多个 `guards`，合法成员累计 10，非成员不计数。
- C：`royal_key` 相同栈匹配，不同 NBT/耐久/名称不误匹配。
- D：sword Group 的铁/金/木剑 fuzzy 匹配。
- E：同一铁剑同时属于 sword/iron_sword/weapon。
- F：玩家 A 跨 Story 逻辑启动，玩家 B 不启动。
- G：Condition 双分支和两个单分支等待语义。
- H：夜晚 7/10，白天暂停不清零，再入夜完成。
- I：多个 Settlement true 只走最上方一条。
- J：A 完成、B 3/10、C 未开始，重启后隔离保持。
- K：CNPC Cloner/复制宝石不复制唯一 NPC ID，原 NPC 保持。
- L：生存捕获/放出保留身份；创造无限生成、Group 继承、NPC ID 不复制。

Stage 6 的第 1-14 步已全部实机通过。

2026-08-30 自主续验结果：

- Case B-E：PASS。Group 合法/非法计数、Item exact 的 NBT 负例、fuzzy sword 和同一
  铁剑三 Group 均有真实任务进度证据。
- Case F：PASS。项目级公共 Logic 图、跨 Story Runtime 传播、玩家 A/B 隔离与 ONCE
  不重复均已通过。
- Case G：PASS。即时分支通过；false→true 与 true→false 两个单出口等待方向都有持久化
  回归，false 等待跨服务端重启并沿连接出口恢复另有实机证据。
- Case H：PASS。实际夜晚 `7/10`，白天事件被 gate 丢弃且保持 `7/10`，再入夜完成
  `10/10 SETTLED`。
- Case I：PASS。多个 Settlement 条件为 true 时只走最上方 `perfect`。
- Case J：PASS。A `10/10`、B `3/10`、C 未开始，服务端重启后保持。
- Case K：PASS。修复 CNPC Cloner 保留 UUID 导致副本窃取 individual 的问题；原体保持
  `tavern_boss`，副本无唯一 ID，Group 语义不受影响。
- Case L：PASS。修复 CNPC `setDead()` 进入复活流程导致捕获重复的问题；生存恢复唯一
  身份，创造无限生成只继承 Group、不复制唯一 ID。

证据集中在：
`.tooling/stage6/gameplay-acceptance-20260830/autonomous-case-b-l/`。重点截图为
`case-b-complete-10of10.png`、`case-c-exact-negative-0of1.png`、
`case-e-three-groups-complete.png`、`case-i-perfect-settlement.png`、
`case-j-player-a-complete-after-restart.png`、`case-k-cnpc-clone-unique-id-guard.png`、
`case-l-survival-restored-identity.png`、`case-l-creative-infinite-group-no-unique-id.png`。

最终 fresh Java 8 门禁：`compileJava + testClasses + jar + reobfJar + sourcesJar +
38/38 JavaExec`，`BUILD SUCCESSFUL in 43s`，122 个 PASS marker。32 个 F/G/H 相关
Java 文件逐文件 Spotless `IS CLEAN`，定向
`git diff --check` 通过。全仓 `spotlessCheck` 会被既有 UI 等 21 个无关脏文件阻断；
禁止为此运行全局 `spotlessApply`。

fresh Runtime JAR 为 `build/libs/darkgrey_rpg-0.3.1.0.jar`，`779,979` 字节，SHA-256
`D0C1273C2844DCA9DD8FBF824FC2F89EFCB0CA671E597706A9218BCA70D6259D`；sources JAR 为
`347,129` 字节，SHA-256
`8CDAA0DCAF2337D546B1AE9EB4747CFE7F2CDE4F2232A074E28517E543FDAFD5`。

临时命令均在 `/dgr debug` 下，只直连现有服务：Nominator type-group 清理、item
exact/group/fuzzy 绑定、Task kill event、CNPC 最近实体绑定/列出、storage
interact/release/status。最终发布前删除或明确保留为开发命令。

### Priority 6 — CNPC 生命周期判定（已完成安全边界）

对 CustomNPC+ 1.11.1 fixed-v1 重点记录：

- Cloner/复制宝石生成的新实体 UUID。
- 死亡/复活前后 UUID 和 CNPC 内部稳定字段。
- 原实体、复制体、复活体在 DGR Registry 中的 host 状态。
- collective Group 是否按设计继承。
- individual NPC ID 是否只属于原 host。

Cloner 与收纳箱已完成真实 CNPC 实测。CustomNPC+ Cloner 会复用源 UUID，因此 DGR
现在对同一世界中 UUID 相同的 CNPC 实例采用 canonical-host fail-closed：只有最低
entity ID 的原 host 可解析 individual，复制体只保留合法 collective Group。收纳捕获
改走 CNPC 已有公开 `delete()`，避免 `setDead()` 触发其死亡/复活流程。

仍未建立死亡后自动把 individual 迁移到某个“复活体”的机制：没有发现同时满足跨重启
稳定且不会误绑无关实体的 CNPC 键，按约束保持 fail-closed + 显式 transfer，不新增通用
生命周期系统。

### Priority 7 — 最终收口（本地完成，发布待授权）

Priority 1-6 证据已齐全，当前本地 PLAN/DoD 为 `GO`。已完成：

1. 更新 `docs/0.3.1.0_ACCEPTANCE.md`，逐项填写 Case A-L 实证。
2. 更新 README、Architecture、Migration、Release Notes 中的最终边界。
3. 重新构建 Runtime JAR、sources JAR 和 Studio EXE。
4. 回读最终 ZIP/JAR/EXE 内容、版本、大小和 SHA-256。
5. 核对 Java class major 为 52，JAR 无硬 CNPC 类引用，六枚图标存在。
6. 核对 `git diff --check`、暂存路径和无关脏状态。
7. 向用户报告本地 GO；commit/PR/Release 仍须另行取得授权。

F/G/H 最新直接证据：

- `.tooling/stage6/gameplay-acceptance-20260830/live-case-fgh-20260830/pre-restart-latest.log.gz`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-fgh-20260830/latest.log`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-fgh-20260830/dimension-latest.log`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-fgh-20260830/13-verifierb-connected.png`
- `.tooling/stage6/gameplay-acceptance-20260830/live-case-fgh-20260830/15-dimension-nether.png`

最终 Studio EXE：`dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`，`141,270,742` 字节，
SHA-256 `91963B5D4CF65244CEA0AC2C57DA67735F086E4C8357785856984E0FD1E6C5E7`。

本轮没有 commit、push、tag、PR、Release 或资产上传；既有开发版 Release 仍对应冻结
提交，不能把本地 GO 候选误写成已发布版本。

## 5. 高风险代码审查点

继续开发时优先关注这些边界，避免回归：

- `NominatorSavedData` schema 2 必须继续兼容 schema 1。
- 客户端 GUI 只能使用服务端发送的有界 catalog snapshot。
- 所有 C2S codec 必须限制数量/文本长度、拒绝 malformed/trailing bytes。
- 服务端处理必须再次验证权限、held item、slot、entity UUID、dimension、distance
  和 revision，不能相信客户端 GUI 状态。
- exact type group 只用于安全的精确 registry type，不能隐式铺到 CNPC/DGR wrapper。
- Storage survival restore 在身份保留异常时必须拒绝，不能静默 transfer。
- Copier/Creative Storage 生成实体时必须清除 UUID 和 individual NPC ID。
- Story Package reload 必须先完整验证再原子替换；失败不能部分更新。
- 奖励 receipt 必须保证 `give_item` 不可因重放重复领取。
- 玩家数据按 UUID；跨维度共享不等于世界公共进度。

## 6. 证据表述规则

接手对话必须继续严格区分：

- 自动测试/Probe：只证明确定性代码契约。
- Build：只证明编译、格式、静态检查和产物生成。
- Dedicated Server `Done`：只证明服务端加载与生命周期。
- Client 启动：只证明客户端兼容和窗口可见。
- 实际 UI/游戏操作：才证明指名、复制、收纳、任务、多人、CNPC 行为。

任何未实际完成的步骤都写“未验证”，不要用相邻证据推断通过。

## 7. 完成接力时应交付

下一对话结束前至少提供：

- 修改摘要和精确文件范围。
- Java/.NET/Studio/Server/Client/游戏内证据分栏。
- PLAN Stage 与 Case A-L 的完成表。
- 未完成项、阻塞条件和可直接执行的下一步。
- 最终产物路径、大小、SHA-256。
- Git 本地/远端 commit、branch、tag、Release 资产回读结果。
- 明确说明哪些原有脏文件被保留。

如果上下文再次过长，更新本 Report/Handoff 或生成新日期版本，不要覆盖历史证据。

## 8. 2026-08-31 源码上传补记

本 Handoff 随完成候选源码进入 `origin/codex/0.3.1.0`。既有
`0.3.1.0_开发版` tag/Release 仍对应冻结提交 `cd73df3`；继续工作时不得把开发分支
的新源码提交误写成已经更新的 Release，也不要在没有新授权时合并 `main`。
