---
name: dgr-release-packaging
description: Maintain DGR Studio's organized portable deployment layout during program updates, deployment-script changes, and explicit release or packaging requests. Create a versioned local product folder with the Minecraft mod JAR and complete Studio ZIP only when the user authorizes packaging; development and source uploads do not authorize it.
---

# DGR 目录分类、程序更新与成品打包

修改 Studio 发布目录、便携存储目录、部署脚本，或执行程序更新、成品打包时使用本 skill。分类规则同时适用于权威 `dist`、独立解压副本和成品 ZIP；读取本 skill 或更新 `dist` 不构成生成新成品的授权。

## 用户授权与用词

- 在 DGR 项目中，用户明确要求 `release`、`发布`、`打包` 等操作，意味着交付一个新的版本化成品文件夹，沿用下面的固定结构；不只运行一次构建命令或提供裸 EXE。
- **没有用户明确的成品打包／发布指令，禁止自行生成或发布成品文件夹。** 普通功能开发、修复、测试、验收、版本更新、任务完成和 GitHub 源码上传均不自动授权此操作。
- 仅讨论发布、询问规则、要求记住或修改本 skill、明确禁止发布，都不是执行打包的指令。此前某轮打包授权不延续为以后每轮开发都自动打包。
- 本约定默认是本地成品交付；GitHub 源码上传、GitHub Release、标签、远端附件上传分别依照用户明确要求执行，不从本地打包请求推断授权。

## 成品位置与结构

默认交付路径：`E:\Java\MinecraftMod\DarkGreyRPG\artifacts\DGR<当前版本号>`。例如当前版本为 `0.3.3.5` 时，文件夹名为 `DGR0.3.3.5`，`DGR` 与版本号之间不加空格。

成品根目录只放分类文件夹，不平铺 JAR、ZIP、说明或报告。固定结构如下，文件名中的版本使用实际当前版本：

```text
DGR<版本>/
  Mod/
    darkgrey_rpg-<版本>.jar
  Studio/
    DarkGreyRPGStudio-<版本>-win-x64.zip
  Docs/
    README.md
    ReleaseManifest.json
    SHA256SUMS.txt
  Verification/
    Tests/
    截图、路径审计、解压核验及实际操作记录
```

- `Mod`：Minecraft 客户端可直接放入 `mods` 使用的正式 Runtime JAR，不能用源码 JAR 或开发用 JAR 代替。
- `Studio`：完整便携 Studio ZIP，完整解压后直接启动根目录 EXE，无需开发仓库或另装 .NET SDK／运行时。
- `Docs`：用户说明、成品版本、来源和校验清单。`SHA256SUMS.txt` 的相对路径以 `DGR<版本>` 根目录为基准，如 `Mod/...jar`、`Studio/...zip`；移动文件后同步修正说明链接和清单。
- `Verification`：测试结果、截图、时序、目录审计等验证证据；测试输出统一放其 `Tests` 子目录。不要塞进 Studio 程序或用户数据目录。

使用实际当前版本，核对 Gradle 模组版本、JAR 元数据、Studio ProductVersion；不要为打包自行递增版本。用户明确指定版本或目标路径时按其要求处理。同名目录已存在时保留旧成品，不静默覆盖、删除或更改版本号；需要替换而用户未指定处理方式时，说明目录冲突再询问。

## Studio 固定分类

权威路径仍是 `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`。程序根目录只允许这一个启动文件，其余文件按用途进入以下目录；新用户可见目录沿用 PascalCase，不另建同义目录：

```text
DarkGreyRPGStudio/
  DarkGreyRPGStudio.exe
  Program/
    DarkGreyRPGStudio.dll
    DarkGreyRPGStudio.deps.json
    DarkGreyRPGStudio.runtimeconfig.json
    其余程序集、托管与原生运行库、语言资源子目录
  Tools/
    FFmpeg/
      ffmpeg.exe
      ffprobe.exe
      LICENSE
      README.txt
  Docs/
    PortableStorage.md
    StudioProgramFiles.json
    PackageContents.json（成品 ZIP 内）
    运行库许可及第三方说明
  Data/（本副本用户数据，成品 ZIP 排除）
    Config/
    Projects/
    Cache/
    Logs/
    Temp/
    Exports/
    MigrationBackup/（仅明确授权的旧数据迁入）
```

- `Program` 包含完整自包含运行环境，语言资源保留原有子目录结构。DLL、运行配置 JSON 不回到根目录；调试符号与开发构建缓存不进入正式交付。
- `Tools` 收纳随程序运行的外部工具；FFmpeg 可执行文件及其许可、说明共同放在 `Tools/FFmpeg`。新增工具按工具名称建子目录，调用方使用统一路径服务。
- `Docs` 收纳程序说明、许可和文件清单，不能混入用户设置或测试证据。
- `Data/Config` 保存设置、主题和最近项目；`Projects` 保存项目及其媒体、引用故事包和恢复文件；`Cache` 保存缓存；`Logs` 保存日志；`Temp` 保存导入及媒体预览暂存；`Exports` 保存默认导出。项目随身资源留在项目内，不拆到其他分类目录。
- 新建项目默认 `Data/Projects`，允许用户输入完整路径或浏览选择外部位置。用户主动创建的外部项目及其媒体、引用和恢复文件留在所选目录，位置记录在本副本 `Data/Config` 设置中；重启、最近项目和重新打开均不得自动复制回默认目录。其他未由本副本创建的外部项目仍先导入再编辑。内部路径相对保存，自选外部路径绝对保存；删除 Studio 文件夹不会删除用户单独存放的外部项目。
- 开发候选、SDK／NuGet 缓存、发布暂存和回归输出留在仓库 `.tooling` 等开发目录，不混入正式 Studio。授权的成品验证证据归成品 `Verification`。
- Studio 根目录以启动 EXE 的实际位置确定，不依赖工作目录。根 apphost 直接加载 `Program/DarkGreyRPGStudio.dll`，不增加包装启动器，不把运行库向系统临时目录解压；托管和原生库查找仍须完整可用。
- Studio 代码通过 `StudioStoragePaths` 获取根目录、数据及媒体工具路径；不能把 `AppContext.BaseDirectory` 当作整理后的程序根目录，也不能假定 FFmpeg 在根目录。调整分类必须同步调用路径、部署脚本、说明及清单。
- 新副本只读取本地 `Data` 设置，不读取 Windows 用户设置、不自动搜寻其他副本。首次启动默认深色，已保存的主题选择仍有效。程序目录不可写时明确报错，不回退用户目录或系统临时目录。
- Minecraft 的 `DarkGreyRPG/Project`、`StoryPackages`、`Cache`、`Config` 属于游戏运行数据，继续由游戏目录管理，不迁入 Studio `Data`。明确选择的外部导出或部署位置仍可使用。

## 更新与打包时保持分类

- 正式 Studio 更新使用 `studio/package-studio.ps1`，默认更新权威 `dist`；测试候选可通过 `-OutputDirectory` 指定独立目录。原始 `dotnet publish` 的平铺输出仅作为中间产物，不能直接覆盖正式目录或用于 ZIP。
- 更新前关闭目标副本，只替换程序文件并完整保留已有 `Data`。禁止对整个程序目录使用镜像清空、递归删除再复制，或拿新副本的 `Data` 覆盖旧副本。
- 已有程序文件以 `Docs/StudioProgramFiles.json` 白名单管理；只清理已知且已过时的程序文件。旧平铺副本可从旧白名单迁移；未知文件不得因为扩展名或位置而被批量删除。删除／移动前核对绝对路径处于目标目录内，拒绝借目录链接写入外部位置。
- 成品 ZIP 从整理后的程序白名单生成，排除整个 `Data`，附 `Docs/PackageContents.json` 记录内容校验。`dist` 与 ZIP 保持同一程序结构，不为压缩包另做一套布局；全新解压后才创建本副本 `Data`。
- 旧项目与设置迁入仅在明确授权时执行；`Data/MigrationBackup` 保存原设置及日志备份，原项目目录保留。普通启动、程序升级或打包不能自行重复迁入。
- 更新已有成品必须有用户明确替换授权；版本未变化时不另造版本号。只有 Studio 布局改变而 Java 无改动时沿用已验证的配套 JAR，保持其字节与 SHA-256。

## Studio 完整性

- 使用正式自包含 Windows x64 Release 发布目录的完整内容，保留相对目录结构。随包包含媒体导入所需的 `Tools/FFmpeg/ffmpeg.exe`、`ffprobe.exe` 及其许可和说明文件；不能只压缩主 EXE。
- 不把个人项目、玩家存档、测试数据、缓存、凭据或本机设置混入 Studio ZIP。
- Studio 使用自包含目录发布，不使用会向系统临时目录解压的单文件发布。完整携带托管和原生运行库以及媒体工具。
- `dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe` 仍是项目规定的权威可运行 Studio 路径。开发构建、测试及依现有交付约束更新 `dist`，与创建 `artifacts\DGR<版本>` 成品文件夹是不同操作，不构成自动打包授权。

## 核验与交付记录

核对 JAR 版本及压缩完整性，核对 ZIP 内容与正式发布目录一致。在隔离目录实际解压，验证 Studio 无外部 .NET 安装依赖地启动，以及随附媒体工具可运行；不得用“已生成 ZIP”替代独立可用性核验。

涉及布局或更新规则的变更，额外检查：Studio 根目录只有 EXE、`Program` 运行库完整、媒体工具从 `Tools` 运行、用户写入均归本地 `Data`；更新前后已有 `Data` 的文件数量、大小和 SHA-256 不被部署改写。成品 ZIP 不含 `Data`，白名单与实物路径一致；移动整个解压文件夹后能够恢复项目。只改 skill 时校验 skill 即可，不为规则编辑触发构建或打包。

成品目录附简短使用说明、文件大小及 SHA-256 清单，记录 Studio ProductVersion、来源提交和实际验证结果。交付时链接成品目录、JAR、Studio ZIP 和校验记录；准确区分本地打包、远端发布与用户验收。
