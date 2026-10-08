# 0.4.0.1 成品与 GitHub Release

2026-10-09，按用户明确要求“上传github，打包至artifact并在github上release”完成源码上传、成品交付和远端发布。
此前 Implementation／Acceptance／DELIVERY 与历史证据中的未发布记录保留其发布前施工时点含义；发布授权不等同于关闭验收缺口。

- 本地目录：`artifacts/DGR0.4.0.1`，根目录仅含 Mod、Studio、Docs、Verification。
- [GitHub Release v0.4.0.1](https://github.com/GreyHat633/DarkGreyRPG/releases/tag/v0.4.0.1)，正式发布且为 Latest，五个附件均 uploaded。
- 标签指向 `f3380ed8da180d39f2dafdf9bfc7667526825efc`，实现分支 `codex/0.4.0.1`；本次沿用已验证二进制，没有为提交号重新构建或递增版本。
- 源码分支追加此发布记录，成品与标签仍固定上述实现提交，没有移动既有标签或修改旧 Release。

| 文件 | 字节 | SHA-256 |
|---|---:|---|
| darkgrey_rpg-0.4.0.1.jar | 1,856,126 | `569e97050c4281ef0b89aed9eedfeb18de7c34cc4651e24530339c95d87f1629` |
| DarkGreyRPGStudio-0.4.0.1-win-x64.zip | 236,964,681 | `44338891dba3c691e2677108611a75f0fcb1b757f4438616d139a270ae8252fb` |

公开附件为正式 Runtime JAR、完整自包含 Studio ZIP、README.md、ReleaseManifest.json、SHA256SUMS.txt。
原始测试、截图、路径审计、世界和个人 Data 不作为 Release 附件上传。本地 Verification 保存包装与既有测试证据。

## 包装验证

JAR／ZIP CRC、410 程序文件逐项哈希、411 文件解压一致性通过；ZIP 根目录只有 EXE，Program、Tools、Docs 结构完整，Data 完全排除。
原生 UIA 实测非程序工作目录启动，DOTNET_ROOT 指向不存在的任务目录，限制 PATH 后四个运行模块实际从本包 Program 加载。
首次深色空项目、本地 Data/Projects 默认项目、相对设置、正常关闭后移动整个文件夹再恢复均通过；随附 FFmpeg／FFprobe exit 0。
权威 dist 未因包装重建或替换，Data 2,690 文件、
68,773,074 字节，打包前后路径／大小／SHA-256 完全相同。

1,053 个物理输入的已验证哈希保持不变；1,052 个提交输入与 Git 内容仅有 CRLF／LF 规范化差异。
源树中一个历史忽略的 Settings/_probe.txt 为非编译／非复制的本机探测标记，明确排除，未上传或进入 ZIP。
Core informational version 保留原构建基线；修复来源以物理指纹、源码提交比对和程序哈希为准。

发布流程为草稿→上传五个附件→核对远端大小与 SHA-256→发布→复核公开 Release、标签解析、附件及 Latest。
见 [github-release.json](evidence/github-release.json) 和本地 ReleaseManifest、SHA256SUMS。

## 验证范围

Runtime build／30 Probe，Core 510 PASS，WPF 667 PASS／1 可选 SKIP 保持原实际范围；最新 Runtime 的隔离 Forge
6500 条保存重启、100 条实际约 301.8 秒縮容、16384→256 槽位及再次保存重启已通过。本次新增包装便携核验。
原矩阵保持 37 PASS、1 N/A、3 NOT_RUN、1 BLOCKED。人工听音、开发 CNPC 映射阻塞、直接 Bridge／诊断故障请求，
以及未注入作者包的完整库存奖励、Disabled、零结算原生组合未闭合；既有历史原生 JAR 范围保持原样。
发布未将这些项签署为全量验收通过，user_accepted 仍为 false。
