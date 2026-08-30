# DarkGrey_RPG 0.3.1.1 Development Report

> **0.3.1.1 已完成本轮 Studio 修缮范围。**

## 证据边界

- 本报告确认源码实现、自动化测试与 Release 构建结果。
- 第 14 章 Case S1-S19 尚未在真实 Studio 窗口中逐项人工执行；不能用 WPF 测试替代 Dark / Light、DPI、鼠标拖线、视觉闪烁、Alt 白框、保存重启等实机证据。
- U1（纵向空间体感）、U2（Alt 白框现场）、U3（用户截图中的具体连接错误）仍列为真实 Studio 复验项；本轮没有放宽既定 Graph cardinality。
- 本轮未修改 canonical serializer 或 Java parser，因此未触发 Java build / Runtime probe。

## 已完成范围

- Workspace：普通资源刷新改为稳定对象与增量同步；保留展开、选择、活动 editor 和图内存状态；图导航不再以保存作为切换门槛。
- Shell：移除冗余应用级故事/设置侧栏、全屏设置页与大标题；窗口标题升级为 0.3.1.1；保留可折叠、可调高度 Bottom Dock。
- Graph：节点/连接增量视觉更新；节点编辑/删除菜单；聚合节点双击导航；拖入 ghost；实线拖线；单线/多线重接；多线事务化 Undo；剪刀与 Left Alt；连接删除确认；稳定端口 anchor。
- Authoring：固定节点退出 palette；Session Choice 恢复；新 Session Start 仅保留 Flow 输出并对旧 Logic 输出给出兼容警告；Settlement 自动使用首个未占用“结果 N”。
- 角色/物品：创建角色先选择个体/集体；普通 UI 使用 DGR 资源身份；Give Item 只允许个体 Item；Objective 使用 DGR 角色/物品选择器。
- Start：至少保留一条启动方式；存在两条时初始项可删；新建类型不再提供 `enter_story`，旧数据仍显示兼容状态。
- 参数：Start、Objective、Action、Line 节点内显示可折叠中文摘要；角色/物品可拖入兼容参数；非法类型不修改图并显示中文轻提示，拖拽不切换 Inspector。
- 中文与诊断：普通端口 display name 中文化且稳定 ID 不变；内部 NodeId/InspectorId 不再作为普通主信息；Validation 在 WPF 边界显示中文说明、稳定 code 与技术详情 fallback。

## 自动化与构建

- Core：345/345 PASS，0 fail，0 skip。证据：`TestResults/Core/Core-0.3.1.1-final.trx`。
- WPF：350/350 PASS，0 fail，0 skip。证据：`TestResults/Wpf/Wpf-0.3.1.1-final.trx`。
- Release solution build：PASS，0 error；20 个既有 MSTest analyzer warning。
- `git diff --check`：PASS；仅显示 Git 的 LF/CRLF 后续转换提示，无 whitespace error。
- Studio 交付 EXE：`dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`（Windows x64、自包含、单文件）。
- `studio/src/DarkGreyRPG.Studio/bin/Release/net10.0-windows/DarkGreyRPGStudio.exe` 是依赖系统 .NET Desktop Runtime 的普通构建输出，不是交付文件。
- EXE FileVersion / ProductVersion：`0.3.1.1` / `0.3.1.1`。
- 交付 EXE 大小：`141324502` bytes（约 134.78 MiB）。
- 交付 EXE SHA-256：`2B0284C832482293B94DD9C8E0A43416CA509BC3735B6E973A84AFACB9A0EB81`。
- 无系统 .NET 启动探针：PASS；将 `DOTNET_ROOT` / `DOTNET_ROOT_X64` 指向不存在的目录并设置 `DOTNET_MULTILEVEL_LOOKUP=0` 后，交付 EXE 持续运行 8 秒，随后由探针主动关闭。

## 人工 Studio acceptance

| 范围 | 当前结果 |
|---|---|
| S1-S19 | PENDING：需要真实 Studio 窗口逐项操作并留存结果 |
| Dark / Light | PENDING |
| 1100×700 / 1700×980 | PENDING |
| 100% / 125% / 150% DPI | PENDING |
| 保存、关闭、重开 | PENDING |
| U1 / U2 / U3 现场复验 | PENDING |

## Git 状态

- 分支：`codex/0.3.1.1`。
- 0.3.1.1 实现提交：`eec4325`（`feat(studio): complete 0.3.1.1 repair scope`）。
- 源码、测试及本报告已推送到 `origin/codex/0.3.1.1`。
- 未创建 tag / GitHub Release，未合并 `main`。
- `.codex/config.toml`、`AGENTS.md`、`.dotnet-home` 与其它用户文档未纳入提交。
