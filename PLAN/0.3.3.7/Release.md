# DarkGrey RPG 0.3.3.7 发布记录

本次按用户明确指令生成 `artifacts/DGR0.3.3.7` 并发布 GitHub Release `v0.3.3.7`。沿用 0.3.3.7，不覆盖既有 0.3.3.5 成品与发布。

源码位于 `codex/0.3.3.7`；标签指向本次完整施工与五项交互修正的源码提交。成品 `Docs/ReleaseManifest.json` 记录确切来源提交、文件大小与 SHA-256，`Verification/GitHubRelease.json` 记录远端发布及附件核验。

## 成品

- `Mod/darkgrey_rpg-0.3.3.7.jar`：正式混淆 Runtime，Minecraft 1.7.10／Forge 10.13.4.1614。
- `Studio/DarkGreyRPGStudio-0.3.3.7-win-x64.zip`：完整自包含 Windows x64 Release，根 EXE、Program、Tools、Docs；不含 Data。
- `Docs`：说明、发布清单及 SHA-256 校验清单。
- `Verification`：本地验收证据、Tests 下的测试报告、压缩与独立启动核验；含本机路径审计，未作为 GitHub 附件上传。

ZIP 从权威 `dist/DarkGreyRPGStudio/Docs/StudioProgramFiles.json` 白名单生成。补齐 .NET 许可与第三方声明，部署脚本同步保留这些文件。JAR 与 Studio 程序集沿用本轮已构建、实机验证的字节。

## 核验

- Runtime JAR 元数据版本 0.3.3.7，954 个条目完整读取，JAR 与 Studio ZIP 的 CRC 检查通过。
- Studio ZIP 409 个条目，包含内容哈希清单；全部程序文件按大小与 SHA-256 对应权威 dist；没有 Data 或调试符号。
- 独立解压后用无效 DOTNET_ROOT 启动，hostfxr、hostpolicy、coreclr、WPF 图形库均从解压副本 Program 加载；首次为空项目与 Dark 主题。
- 原生 Ctrl+N 创建默认 Data/Projects 项目，设置保存相对路径；关闭并移动整个目录，重启后恢复该项目。
- 随附 FFmpeg／FFprobe 9.0 可执行，退出码均为 0。
- 当前 Studio WPF 691 通过／1 项既有跳过；Core 492 通过／11 项既有跳过；共 1,183 通过、12 跳过、0 失败。
- Java 构建及布局、追踪预算、滚动、3,072 个逻辑运行／恢复向量、服务端 CLOSE 探针通过。五项修正的原生交互与覆盖边界见 `Corrections.md`。

GitHub 只上传源码及正式 JAR、Studio ZIP、README、ReleaseManifest、SHA256SUMS 五个附件。个人 Data、原始截图、路径审计及测试机器报告保留本地，不进入源码与公开附件。

JourneyMap beta.4 默认全屏地图选项 O 与管理器默认 O 存在绑定冲突，应在游戏按键设置中为二者选择不同绑定。Studio 浅色启动通过；本轮主题菜单切换未生效，保留为已知待调查项。完整实机矩阵及帧级动画量测尚未覆盖，详见修正记录。
