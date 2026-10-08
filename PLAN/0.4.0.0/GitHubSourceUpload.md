# 0.4.0.0 GitHub 源码上传

日期：2026-10-08。用户授权上传开发源码，后续追加六项修正、指名器异常隔离、拖动与字体修复。本轮在现有分支追加源码和验证摘要。

- 仓库：`GreyHat633/DarkGreyRPG`
- 分支：`codex/0.4.0.0`
- 开发基线：`51582ae8fe8a71214f2306ecd08492129171cff5`
- 原实现提交：`99f3df91de56fd8ffc6122414988fcac06f777ff`；本轮修复基于已上传的 `011a6613489e549b1eb2fd47d3d2a04dfc1268dd`。新增提交由本地上传收据记录并与远端分支 HEAD 核对。

上传包括 0.4.0.0 当前 Runtime／Studio 实现、退休清理、六项修复和回归、架构与构建说明、原始施工 PLAN 及验收／交付摘要。原始 PLAN 和冻结副本 SHA-256 保持 `1F7D45AAB67D8394C96ED4E0FE15C55B2C52370C0DD97D7371D6089F1764FCFA`。原始截图、日志、TRX、Data 指纹和工具目录仅保留在本地；不上传用户项目、运行目录或生成产物。

当前完整 Core 499 通过／11 跳过／0 失败，WPF 667 通过／1 跳过／0 失败，Runtime 构建及 22 个当前 Probe 通过。Windows 原生拖动、旧表／损坏表故障隔离、当前指名及游戏选择／任务链见 [本轮摘要](DragCrashFixes.md)、[六项摘要](SixPreUploadFixes.md)、[验收](Acceptance.md) 和 `DELIVERY.json`。未单独跑完的原生组合与历史旧夹具 Probe 失败单列，不冒称全矩阵完成。

权威 Studio ProductVersion 0.4.0.0；410 个程序文件与验证候选相同，2,690 个 Data 文件、68,773,074 字节在本次部署前后指纹相同。EXE 204,288 字节，SHA-256 `F68E1D6FAB7A4717089B394C9842BEDCFD7EAEC93382E76D86E0E5725E9001B3`；更新后主 DLL SHA-256 `542F8B01708A84BBF0D9A938F10E5949621549C19BE50EFE363A8FF152BED3EF`。Runtime JAR 1,843,025 字节，SHA-256 `0D971907C95602AE94544B684D27E145014A9B5A4F9D1A79DEDF1E6508FF8CD9`。

最终 692 个生产源码指纹的清单 SHA-256 为 `7fdbb6ea318486497a02e1021ce23a0d31c4522beed224b70dbd722677664ad5`；源码对应本轮修复、三个 JAR 审计及正式 Studio 主 DLL。原 schema 3 测试世界保留，拒绝输入未迁移或清空，新的正常测试在隔离世界完成。

额外物品过滤原生作者链和人工听音继续记录为未关闭，用户接受保持 false。本轮不创建成品 ZIP、标签或 GitHub Release。推送结果保存在本地上传收据，最终远端提交与本地分支 HEAD 相同才算上传完成。
