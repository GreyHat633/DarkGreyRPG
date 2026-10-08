# 0.4.0.0 GitHub 源码上传

日期：2026-10-08。用户授权上传开发源码，并要求先完成最终六项修正。

- 仓库：`GreyHat633/DarkGreyRPG`
- 分支：`codex/0.4.0.0`
- 开发基线：`51582ae8fe8a71214f2306ecd08492129171cff5`
- 实现提交：`99f3df91de56fd8ffc6122414988fcac06f777ff`；最终上传含此提交及交付来源记录，远端分支 SHA 与本地 HEAD 核对。

上传包括 0.4.0.0 当前 Runtime／Studio 实现、退休清理、六项修复和回归、架构与构建说明、原始施工 PLAN 及验收／交付摘要。原始 PLAN 和冻结副本 SHA-256 保持 `1F7D45AAB67D8394C96ED4E0FE15C55B2C52370C0DD97D7371D6089F1764FCFA`。原始截图、日志、TRX、Data 指纹和工具目录仅保留在本地；不上传用户项目、运行目录或生成产物。

完整 Core 499 通过／11 跳过／0 失败，WPF 665 通过／1 跳过／0 失败，Runtime 构建及 8 个会话 Probe 通过。Windows 原生和实际游戏选择恢复链结果见 [六项摘要](SixPreUploadFixes.md)、[验收](Acceptance.md) 和 `DELIVERY.json`。

权威 Studio ProductVersion 0.4.0.0；410 个程序文件与验证候选相同，2,690 个 Data 文件、68,772,849 字节前后指纹相同。EXE 204,288 字节，SHA-256 `F68E1D6FAB7A4717089B394C9842BEDCFD7EAEC93382E76D86E0E5725E9001B3`；更新后主 DLL SHA-256 `D78EDA6AE9FB9FDF9686665C27A9ECAE318C8096A32EAA2FE07DD0193C9CDF4A`。Runtime JAR 1,838,497 字节，SHA-256 `601B337231C534FEF43FCE159A1CE7F646B92B3B5C8C68558AEDD141904541FD`。

额外物品过滤原生作者链和人工听音继续记录为未关闭，用户接受保持 false。本轮不创建成品 ZIP、标签或 GitHub Release。推送结果保存在本地上传收据，最终远端提交与本地分支 HEAD 相同才算上传完成。
