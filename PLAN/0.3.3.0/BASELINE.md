# 用户澄清更新（2026-09-12）

只删除旧独立触发器节点，现用开始配置和启动路径保持不变。此前记录的
“激活契约缺口/等待用户确认”判断已撤销；下文保留为 P0 原始审计历史，不代表当前阻塞。
P1 已完成，当前结果见 [P1.md](P1.md) 和已修正的施工计划。以下所有 P1 等待状态均为作废的历史判断。

# 0.3.3.0 P0 开工记录

日期：2026-09-12。

```text
BASELINE=82c6443238e41ce820f076e09d65d3cb26bb2236
BRANCH=codex/0.3.3.0
P0=EXECUTED_WITH_DOCUMENTED_BASELINE_REPAIRS
P1=WAITING_FOR_ACTIVATION_CONTRACT
0.3.3.0_CONSTRUCTION=IN_PROGRESS
0.3.3.0_USER_ACCEPTED=NOT_REQUESTED
```

本次用户要求开始施工。未发布 GitHub、未创建 Release，未认定用户验收。
工作区已有历史验收文件删除及未跟踪数据全部保留；初始状态见
`evidence/initial-worktree.txt`。开工前 `src` 和 `studio` 无未提交源码改动。

## 基线验证与修复

| 项目 | 结果 | 证据 |
| --- | --- | --- |
| 原始 Runtime build | FAIL：两个文件的 Spotless 格式错误 | `evidence/runtime-baseline.log` |
| 修复后的完整 Runtime build | PASS，未跳过 Spotless/Checkstyle/test | `evidence/runtime-after-format.log` |
| Core Release tests | 429 通过，1 跳过，0 失败 | `evidence/core-baseline.trx` |
| WPF Release tests | 原基线及版本修正后均 485 通过，0 失败 | `evidence/wpf-baseline.trx`, `evidence/wpf-version-fixed.trx` |
| 全部注册 JavaExec probes | 首轮 56/58，通过匹配 fixture 复跑后 58/58 | `evidence/probes-baseline.log`, `evidence/probes-fixture-retry.log` |
| Scope P0 | PASS，仍报告旧架构残留 | `evidence/scope-p0.txt` |
| Scope 自测 | PASS，含 Task Flow 和 network discriminator 负例 | `evidence/scope-self-test.txt` |
| 最终 Scope 负例 | 正确拒绝当前含旧架构的基线 | `evidence/scope-final-negative.txt`, `evidence/scope-negative-test.txt` |
| Windows x64 自包含单文件打包 | 原始工程会产生 B4 版本；版本修正后重新打包 | `evidence/studio-package-baseline.log`, `evidence/studio-package-version-fixed.log` |

Core 跳过的是需要显式 `DGR_B4_MIGRATION_FIXTURE` 的外部 B3 迁移测试，
不是本轮回归失败，也不能计为 PASS。

原始 exact SHA **不能直接通过完整 build**，不隐去这一事实。必要的 P0 修复：

1. 仅格式化 `CanonicalGraphResourceLoader.java` 与 `CanonicalGraphResourceLoaderProbe.java`。
   使用 `scripts/0330-baseline-format.gradle` 限定两个目标；diff 审阅及去空白比较确认
   无 token 变化。未运行全仓库自动格式化。
2. `studio/src/DarkGreyRPG.Studio/DarkGreyRPG.Studio.csproj` 的 Version、AssemblyVersion、
   FileVersion、InformationalVersion 仍为 B4；修正为已交付基线 `0.3.2.4`。
   不将无新功能的基线标记为 0.3.3.0。

原始 `scripts/verify-0324-freeze.ps1` 字节保持不变，作为历史冻结证据。
新 Scope 固定比较 exact SHA，没有降低历史脚本要求。

## Probe fixture 边界

- `dgrsPackageRuntimeProbe` 在测试内部要求 Story `kill_slimes`。当前游戏包虽叫
  `kill_slimes.dgrs`，实际 Story 为 `GreyHat_:test_story`，因而首次断言失败。
  复跑使用 `.tooling/0.3.2.0_B2/real-minecraft-20260903/story-packages/kill_slimes.dgrs`
  的本轮隔离副本。
- `objective0324Probe` 的 Gradle 默认参数指向整个 `PLAN/0.3.2.4/evidence`。
  包加载器将 `user-review-fixes` 子目录视为旧目录包，因缺少 manifest 失败。
  复跑使用 `kill_slimes-description-corrected.dgrs` 的独立目录副本，
  通过 `scripts/0330-baseline-probes.gradle` 覆盖测试参数。
- 两个原始包和当前游戏目录均未更改；副本哈希见 `evidence/probe-fixture-hashes.json`。
- 首轮 58 个任务清单见 `evidence/baseline-probes.txt`。日志中的部分异常由负面测试
  故意触发，应以对应 Gradle task 退出状态判断。

## 可复现命令

在仓库根目录运行；缓存及临时目录均固定到 E 盘：

```powershell
$env:JAVA_HOME='E:\Java\jdk1.8.0_471'
$env:GRADLE_USER_HOME='E:\Java\gradle'
$env:TEMP='E:\Java\MinecraftMod\DarkGrey_RPG\.tmp'
$env:TMP=$env:TEMP
.\gradlew.bat build --offline --no-daemon --no-configuration-cache --max-workers=1

New-Item -ItemType Directory -Force -Path `
  '.tooling/0.3.3.0/fixtures/objective0324', '.tooling/0.3.3.0/fixtures/legacy-dgrs'
Copy-Item 'PLAN/0.3.2.4/evidence/kill_slimes-description-corrected.dgrs' `
  '.tooling/0.3.3.0/fixtures/objective0324/'
Copy-Item '.tooling/0.3.2.0_B2/real-minecraft-20260903/story-packages/kill_slimes.dgrs' `
  '.tooling/0.3.3.0/fixtures/legacy-dgrs/'
$probes=[regex]::Matches((Get-Content build.gradle.kts -Raw),
  'tasks\.register<JavaExec>\("([^"]+)"\)') | ForEach-Object { $_.Groups[1].Value }
.\gradlew.bat @probes -I scripts/0330-baseline-probes.gradle `
  '-PdgrsPath=E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\0.3.3.0\fixtures\legacy-dgrs\kill_slimes.dgrs' `
  --offline --no-daemon --no-configuration-cache --max-workers=1 --continue

$env:windir=$env:SystemRoot
$env:DOTNET_CLI_HOME='E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\wpf-build'
$env:NUGET_PACKAGES=Join-Path $env:DOTNET_CLI_HOME 'nuget-packages'
$env:TEMP=Join-Path $env:DOTNET_CLI_HOME 'temp'
$env:TMP=$env:TEMP
& 'E:\Java\dotnet-sdk-10\dotnet.exe' test `
  studio/src/DarkGreyRPG.Studio.Tests/DarkGreyRPG.Studio.Tests.csproj --configuration Release
& 'E:\Java\dotnet-sdk-10\dotnet.exe' test `
  studio/src/DarkGreyRPG.Studio.Wpf.Tests/DarkGreyRPG.Studio.Wpf.Tests.csproj --configuration Release
& studio/package-studio.ps1
& scripts/inventory-0330-legacy.ps1
& scripts/verify-0330-scope.ps1 -SelfTest
& scripts/verify-0330-scope.ps1 -Phase P0
```

Gradle wrapper 是 9.3.1，构建 JVM 由已有 `org.gradle.java.home` 指定到 JDK 25；
probes 实际使用已有 Gradle Java 8 toolchain。不要把 shell 的 JAVA_HOME 当作全部工具链版本。

## Scope 检查边界

- 检查 tracked 修改/删除及未跟踪生产文件，固定 exact baseline。
- 冻结 identity、Nominator、Utility Window、aggregate port projection/factory 及旧 freeze script。
- 比较所有既有 network registration/channel，允许添加新 route，禁止改旧 route。
  discriminator 唯一性仍由现有 network probe 验证。
- 检查完整 Task registry entries，包括 allowed kinds 与显式 ports；Runtime Logic-only
  由 `canonicalGraphResourceProbe` 验证，不能以正则扫描代替执行测试。
- 检查显式新增 compatibility 标志及禁止的媒体 ID 词；它是保守预警，不是任意语义兼容层的形式证明。
- P0 允许已知旧架构存在，仅输出库存；P1 要求 Trigger 清理，P5/Final 还要求 Narration 清理。
- 默认 `Final`；当前基线执行默认模式必须 FAIL。残留数的单位是匹配生产文件，并非精确调用路径数。
- 未来若必须调整包含旧 Trigger 引用的 Identity 文件，应增加审阅过的窄例外，不能直接放开目录。

当前剩余 21 个 Runtime、37 个作者侧候选文件；Task interact_actor 不作为全局禁词。

## Studio 交付及剩余工作

正式路径仍为 `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`。
ProductVersion：`0.3.2.4`；大小：`141729494` bytes；SHA-256：
`31176A2B65D920FF4875DF5BA5783363C51E0135A80FF0528284B7E6F60F1869`。
机器可读记录：`evidence/studio-delivery-baseline.json`。
原 EXE 备份在 `.tooling/0.3.3.0/studio-before-p0/`。
本次仅修正基线版本元数据并重新打包；没有新增 UI，不宣称新 UI 的视觉验收。

P1 的生产激活依赖见 `LEGACY_TRIGGER_INVENTORY.md`。已请求明确删除 Trigger 后的
启动规则，尚未收到答复；不冒然移除现有 NPC/Logic 入口。
P2 的 DESIGN-GATE-A/B/C 也已提问，均保持 OPEN；D/E/F 留待对应阶段。
P1–P10 未完成，最终用户审核未发生。
