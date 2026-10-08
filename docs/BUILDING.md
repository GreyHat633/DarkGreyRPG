# 源码构建

以下针对当前 0.4.0.0。Windows 上使用 **PowerShell 7** 从仓库根运行（本轮 7.6.5）；发布脚本使用现代 .NET API，不能直接照搬到 Windows PowerShell 5.1。路径可以包含空格或中文。首次需联网；先恢复再谈缓存离线构建，不从作者 bin／obj／dist／完整 .tooling 复制输出。

## 工具与依赖

| 用途 | 当前工具 | 区别 |
|---|---|---|
| 启动 Gradle | wrapper 9.3.1；本轮 JDK 25.0.1 | 不是游戏 Java；不能用 Java 8 启动该 wrapper |
| 构建插件 | GTNH settings convention 2.0.20；blowdryer tag 0.2.2 | 保持已提交版本，不运行自动升级 |
| Runtime 编译 | convention 配置的 Java 8 目标；自动提供所需 8／17／21 工具链 | 以构建日志、class major 52 核实；工具链与启动 JDK 分开 |
| Minecraft | 1.7.10、Forge 10.13.4.1614、Java 8 | 正式 JAR 两端加载验证 |
| Studio | .NET 10 x64 SDK，本轮 10.0.302 / runtime 10.0.10 | net10.0-windows WPF；成品自包含，用户不装 SDK |
| 媒体转换 | Gyan FFmpeg 9.0 full build | 固定版本、来源及哈希见 studio/media-tools.lock.json |

安装／解压工具到你选择的位置。SDK 获取入口：[Microsoft .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)、JDK 从相应发行方取得。不要把私人绝对路径提交到 gradle.properties。Gradle 按 JAVA_HOME 启动，可在本机用户 Gradle 配置或命令参数明确指定可用工具链；首次恢复会下载尚缺工具链。

### IDEA 的 Gradle JDK

IDEA 的 `1. Run Client` 使用项目的 Gradle JVM 配置。若该配置是 `#GRADLE_JAVA_HOME`，它依赖 `org.gradle.java.home`；从仓库移除本机 JDK 路径后，需要在 IDEA 实际使用的 Gradle 用户目录内的 `gradle.properties` 配置此属性，或在「设置 → 构建、执行、部署 → 构建工具 → Gradle → Gradle JVM」选择已安装的兼容 JDK。命令行构建成功不能代替这项 IDEA 运行验证。

例如在本机配置中写入 `org.gradle.java.home=C:/Tools/jdk-25`，将路径替换为实际安装目录；该配置不提交到源码。当前 wrapper 由 JDK 25 启动，`runClient` 的游戏进程仍使用 Java 8 工具链。出现「找到无效的 Gradle JDK 配置」时，先核对 IDEA 选择的 Gradle 用户目录和 JDK 路径，不通过升级 wrapper 或切换游戏 Java 来处理。本轮恢复本机配置后，已从 IDEA 实际运行 `1. Run Client` 到 Minecraft 主菜单。

仓库 libs 当前跟踪精确 JLayer 1.0.1、CustomNPC+ 1.11.1-fixed-v1 与 UniMixins 0.3.1。运行 `scripts/prepare-java-dependencies.ps1` 校验，缺失时按锁定来源准备 JLayer／UniMixins；CNPC fixed-v1 的公开对应来源尚未确认，缺失或不匹配时需提供精确合法副本，不自动找新版代替。CNPC／UniMixins 不嵌入 DGR JAR，JLayer 无重定位嵌入并带原文与源码。仓库跟踪二进制本身也是分发，详见 [第三方说明](../THIRD_PARTY_NOTICES.md)。

## Runtime：联网构建与有限当前回归

```powershell
$env:JAVA_HOME = '你的 Gradle 启动 JDK 目录'
$env:GRADLE_USER_HOME = '你选择的纯 ASCII 缓存目录（允许空格）'
.\scripts\prepare-java-dependencies.ps1
.\gradlew.bat build --no-daemon --no-configuration-cache --max-workers=1
.\gradlew.bat canonicalTaskForgeProbe taskCandidate0400Probe nominator0400Probe nominatorCurrentActions0400Probe canonicalTaskView0400Probe publicOutputPriority0336Probe construction0337Probe taskLayout0337Probe --no-daemon --no-configuration-cache --max-workers=1
```

正式 Runtime：`build/libs/darkgrey_rpg-0.4.0.0.jar`。`-dev.jar` 与 `-sources.jar` 不供玩家安装。`build` 包括 convention 的现有检查；**独立 JavaExec Probe 不由 build 全部执行**。记录每项执行和退出码，普通 Test 没发现测试时不能计为 Probe 通过。上列是本轮有限当前合同集，不是全部历史协议验收。

`runtimeInteractionUi0336Probe`／`tavernRepeat0336Probe` 另要求此前特定作者故事包；没有该包时记录未运行，不拿任意示例代替其断言。本轮真实最小故事链由 Studio 实际导出后在隔离游戏端验证，范围与旧作者夹具探针分开。

上述联网恢复成功、所需依赖及工具链都已缓存后，可在相同命令追加 `--offline`。新的独立缓存缺少内容会失败，这是正常的缺依赖报告；不要删断言或关闭检查使它显示绿色。参数依赖外部旧夹具的历史 Probe 单独记录 N/A 或未运行，不恢复退休协议。

本轮 Windows/JDK 25/Gradle 9.3.1 的中文缓存路径触发 Worker 的 response-file classpath 解析失败；主类用直接 classpath 可加载。将**同一独立缓存**移到纯 ASCII 路径后，带空格与中文的源码路径保持不变，完整 build（含 Checkstyle）通过。缓存路径由本机环境指定，不写入源码，不关闭检查；首次失败和修正后结果分开记录。

## Studio：准备、测试、完整便携目录

```powershell
# 首次：下载固定上游 ZIP，核验 ZIP 和四个随附文件后提升到工具目录。
.\studio\prepare-media-tools.ps1
# 或从已合法取得的同版本目录／本地 ZIP 准备：
.\studio\prepare-media-tools.ps1 -SourceDirectory '你的 FFmpeg 目录' -Offline
.\studio\prepare-media-tools.ps1 -ArchivePath '你的 ffmpeg-9.0-full_build.zip' -Offline
# 已验证工具无需重复下载；全新离线缺工具会明确失败。
.\studio\prepare-media-tools.ps1 -Offline

$dotnetExe = '你的 .NET 10 SDK\dotnet.exe'
$env:DOTNET_CLI_HOME = Join-Path $PWD '.tooling\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PWD '.tooling\nuget-packages'
$env:windir = $env:SystemRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
& $dotnetExe test studio/src/DarkGreyRPG.Studio.Tests/DarkGreyRPG.Studio.Tests.csproj -c Release
& $dotnetExe test studio/src/DarkGreyRPG.Studio.Wpf.Tests/DarkGreyRPG.Studio.Wpf.Tests.csproj -c Release
.\studio\package-studio.ps1 -DotnetPath $dotnetExe
```

发布脚本发现顺序：`-DotnetPath` → `DGR_DOTNET_PATH` → PATH 的 dotnet；接受 SDK 根目录或可执行文件。发布前检查稳定 .NET 10 SDK、HostModel、Windows x64 apphost 模板、分发声明，均取自**实际选中的 SDK**。不扫描全盘，不安装系统工具。输出默认权威 `dist/DarkGreyRPGStudio`；测试候选用 `-OutputDirectory '你的隔离输出路径'`。

默认媒体目录为仓库 `.tooling/media-tools/ffmpeg`。准备脚本 `-DestinationDirectory` 可指定管理目录；构建属性 `-p:MediaToolsDirectory=完整路径`、发布参数 `-MediaToolsDirectory` 可覆盖，四文件必须一起提供。准备先验证后提升，失败不覆盖现有工具。更换已存在目录时保留同级 previous-ffmpeg 备份。

完整输出保持根 EXE、Program、Tools、Docs；更新 Data 不遍历、不覆盖。根 apphost 直接加载 Program 中 DLL，不能只拷贝 EXE。运行与文档／许可文件纳入 Docs/StudioProgramFiles.json 白名单；按 AGENTS.md 打包权限，本轮构建不新建 artifacts 成品或修改 GitHub Release。

`studio/qa/closeout-tooling-tests.ps1` 覆盖参数错误、准备重复／离线／损坏来源及旧文件保留。测试与 Probe 必须记录实际范围；本轮 [验收表](../PLAN/0.4.0.0/Acceptance.md) 还区分同机隔离、自包含模块读回及实际 UI／Runtime 验证。
