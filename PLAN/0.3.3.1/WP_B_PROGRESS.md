> 2026-09-14 最终集成更新：本文件保留工作包阶段记录；后续编译修复、真实交互及最终证据以 [DELIVERY_REPORT.md](DELIVERY_REPORT.md) 为准。阶段性待办不能覆盖最终记录，未实测部分也不自动计为通过。

# WP-B 施工与证据

状态：施工中，未完成整包 WP-B，未完成 0.3.3.1。A22 公共数值拖动尚未实施。

## A11：选项及行身份

已修复的源码事实：

- 执行类型 getter 原来每次分配选项数组，现按原生/高级两种集合复用。
- Start 类型与角色选项现按值变化更新；触发行按稳定 port_id 增量更新、移动、删除，不再 Clear/Add。投影回填增加重入保护。
- 奖励物品选项在 Inspector 生命周期内复用。奖励字段修改保留行对象；增删保留未改变的前后行，删除行清理对应草稿错误；其他行修改不清除当前无效数字草稿。

涉及 CanonicalNodeInspectorViewModel.cs、.Actions.cs、.Rewards.cs。没有改变 Start 类型集合、奖励持久格式或现有高级执行语义；这些仍有本版后续工作项。

新增 DropdownIdentity0331Tests：Start 100 轮改名/Undo/Redo及重排、奖励 100 轮两侧同步/Undo/Redo及草稿保持、执行参数 100 轮更新后的选项实例保持。连同原 Inspector/奖励/执行回归，26 项通过，证据 `evidence/dropdown-fixed.trx`。首轮新样例漏初始化 Start 条目，修正后通过；原失败日志保留。

该测试验证投影与编辑事务，不等于四类 Popup 各 100 次真实鼠标操作已通过。尚未取得闪退异常栈，不声称已证明唯一根因或已彻底修复闪退。

## A18/A19

已实施参数实际控件命中分离：TextBox、ComboBox、Button、RangeBase、Thumb、列表项等保留手势，参数布局空白可用于节点拖动；补充文本内容元素父级查找保护。

已订阅 LayoutChanged 并在 Render 阶段合并 UpdateLayout / IndexPorts / RedrawConnections，统一刷新可见线和命中线；追加节点 SizeChanged 刷新及销毁时解绑。

Worker 的新增2项测试与62项原画布回归通过；主负责人审阅后补最窄修整，并运行整合后的 Release WPF 全套，502 PASS、0 SKIP、0 FAIL，见 `evidence/wpf-wpb-preview.trx`。新增测试用实际 WPF 视觉树检查控件/空白命中，并对50轮多节点移动、Undo/Redo的线端与命中线端测量。未将上述 STA 测试声称为真实鼠标验收，DPI/主题/连续Popup操作证据仍缺。

## Windows 工具限制

computer-use 初始化/list_apps 成功，基线 Studio 正常启动，可访问性树可读。截图两次失败：`SetIsBorderRequired failed: 不支持此接口 (0x80004002)`；进入故事元素点击失败：`coordinate input geometry is unavailable`。失败点击后的重新观察仍为项目首页。

上述为首次工具尝试的历史记录。用户随后授权实机演示，已完成替代工具实机操作并修正两处事件链缺陷，详见 [LIVE_DEMO.md](LIVE_DEMO.md)。

## 首批预览产物（已被实机修正版替换）

已发布自包含 win-x64 Release 到唯一权威路径 `E:/Java/MinecraftMod/DarkGrey_RPG/dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`。

- ProductVersion：`0.3.3.1-wpb-preview`
- 字节数：141841646
- SHA-256：`E2E63465198FD4CC877705DDA1717F367D8924C2F565754C86C9606BB6372E26`
- 原完整 Studio 目录备份：`.tooling/0.3.3.1/studio-before-wpb`。
- 发布后实际启动并读取可访问性树，标题显示 `0.3.3.1-wpb-preview`，见 `evidence/packaged-startup-accessibility.txt`。这是启动证据，不是鼠标交互验收。

首次启动发现窗口标题/关于硬编码旧版本，已将两处接到程序集 InformationalVersion，重打包并复验。版本展示调整后发布构建成功；502 项全套结果针对此前交互代码整合版本，未因纯版本显示再重复全套测试。

当前 0.3.3.1 未完成：A22、WP-A/C/D/E/F 和全部真实交互/视觉/性能矩阵仍待推进。没有本版模组功能更新、Git commit/push、Release 或用户验收；当前 Java 产物仍是基线。不要把这批预览当成本版最终交付。
