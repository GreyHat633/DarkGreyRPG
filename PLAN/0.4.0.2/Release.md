# 0.4.0.2 成品与 GitHub Release

2026-10-10，按用户明确要求“上传github并打包release”完成源码上传、本地成品和远端正式发布。开发阶段记录中的“未发布”保留其当时时点；本记录和 DELIVERY.json 的 release 字段为当前发布状态。

- 本地成品：`artifacts/DGR0.4.0.2`，根目录仅含 Mod、Studio、Docs、Verification。
- [GitHub Release v0.4.0.2](https://github.com/GreyHat633/DarkGreyRPG/releases/tag/v0.4.0.2) 已正式发布，为 Latest；五个附件均 uploaded，大小与远端 SHA-256 全部一致。
- 实现提交／标签目标：`5a116c96ad0aba2c3c1a6b21ccd29a991f3ce881`；分支 `codex/0.4.0.2`。保留已验收二进制，不为提交号重新构建；Core informational version 保留原构建基线。
- 分支随后追加此发布记录；标签保持固定实现提交，不移动旧标签、不修改既有 0.4.0.1 Release。

| 成品 | 字节 | SHA-256 |
|---|---:|---|
| Runtime JAR | 1,896,860 | `8b416c270c017873d5d18f96cbb5ca89f67ec88643809731e185776bb62043fc` |
| Studio win-x64 ZIP | 236,964,399 | `fbfc980b6f7bd1e23cf2d4bacabc69e248c2df91f12bc50ff72ac76e2a28a385` |

公开附件为 Runtime JAR、完整自包含 Studio ZIP、README.md、ReleaseManifest.json、SHA256SUMS.txt。测试报告、测试世界和个人 Data 不作为 Release 附件。源码包括实现、可复验夹具及验收证据摘要。

## 包装与来源验证

JAR／ZIP CRC、410 程序文件逐项哈希、411 文件解压一致性通过。ZIP 根目录仅 EXE，Program、Tools、Docs 完整，Data 排除。通过 Windows 原生 UIA 验证：非程序工作目录与受限 PATH 启动、DOTNET_ROOT 指向不存在目录，四个运行库模块均从本包 Program 加载；首启深色空项目、默认 Data/Projects 创建项目、相对设置、正常关闭后移动整体目录再恢复全部通过；随附 FFmpeg／FFprobe exit 0。

权威 dist 没有因包装更新。个人 Data 打包前后 2,690 文件／68,773,074 字节全部路径、大小和 SHA-256 相同，个人路径清单仅保留在忽略的任务目录。

提交比对验证 1,083 个输入（568 个 Runtime 输入及 515 个 Studio 输入）。516 个 Studio 物理输入含一个历史忽略的本机 _probe.txt，明确排除上传与打包；相对 .1 只有版本声明和 BOM 变化。提交内容仅允许 CRLF／LF 规范化，物理 Runtime 来源指纹保持 `B2F377A3EEB926651DCD0E02612E7C6BD72EC4C6F3660CDBB46E56D64C5C6E3C`。

发布流程为草稿、上传五附件、校验远端大小／SHA-256、正式发布、复核标签解析／Latest／附件。见 [远端回执](evidence/github-release.json)、[成品清单](evidence/release-manifest.json) 与 [包装核验](evidence/release-package-verification.json)。

## 验证范围与保留项

Runtime build／42 Probe，Studio Core510 PASS、WPF667 PASS／1 SKIP。当前 JAR 双真实客户端持续观察的 60 分钟／72000 测量 tick、76440 个生产队列作业与严格冷重启通过；线性历史 reader 60000 条回归、实际过期响应重发和异常停止清理有当前哈希记录。正式三组服务端参考与一组原生帧对照保持其冻结检查点和组件继承范围。

累计压力长测 max607.8875ms、冷恢复压力 max653.2519ms 及高 ACTIVE／目标数档超参考门槛均保留；不作为任意规模达标的证明。损坏 NpcIdentity 专项 DEFERRED_BY_USER、人工听音 NOT_RUN、旧开发 CNPC 映射 BLOCKED_HISTORICAL 不被发布关闭。正式 CNPC 环境通过记录独立保留。发布不替代用户全量验收。
