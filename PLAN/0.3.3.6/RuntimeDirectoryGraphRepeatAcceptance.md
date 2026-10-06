# Runtime 目录、流程图与 NPC 重开修正验收记录

日期：2026-10-06。版本保持 **0.3.3.6**。本轮修改 Runtime 和相关 Probe；未改 Studio、故事包格式或存档格式，未生成成品发布包。工作区原有改动保留。

## 已交付

`dist/darkgrey_rpg-0.3.3.6.jar`

- 大小：**1,976,339 字节**。
- SHA-256：`62E994457B011C2ECE0E03D69AA4B136AF7DA956A11FBACF677E0DB1566824DF`。
- 与隔离客户端、服务端实测的候选 JAR 哈希一致。构建产物与 dist 文件一致。
- 元数据：[RuntimeFollowupArtifact.json](evidence/RuntimeUi/RuntimeFollowupArtifact.json)。

## 行为变化

管理目录绘制、点击与滚动统一使用 22 逻辑像素行高。状态标记在标题右侧，窄侧栏仅显示标记，悬停提供完整状态和成员继承说明。目录列标题、文件夹、叶子与资源文字分别采用 0.98、0.92、0.85 倍；资源引用来源保留为辅助文字。

右上角依次提供启用、禁用、刷新，分别使用绿色、红色与主题色图标。启停明确发送 true/false，按安装容器操作；相应状态已经生效或请求未完成时禁用按钮。页面不再提供删除、删除确认或关闭按钮。Esc 与实际配置的背包键关闭并清理管理会话。保留最小 300×180、默认 420×260 逻辑像素和已调整窗口尺寸。

图形使用共同模型坐标和画布矩阵绘制节点、文字、连线及箭头。视口使用 OpenGL 裁切，部分越界的节点继续绘制可见部分。滚轮锚定鼠标处的模型坐标；点击使用相同变换的逆变换，且限制在图视口内。绘制结束恢复矩阵与裁切状态。适配、平移、缩放范围以及 Flow 实线 / Logic 虚线保留。

## 酒馆老板阻断原因与修复

当前“测试故事”包的启动条件与实体绑定一致。原存档的完成摘要却记录了这次故事在旧规则 **ONCE（不可重复）** 下正常结束；故事包后来修改为无条件重复。旧准入代码直接使用历史摘要的重复规则，导致当前包仍被旧规则拦截。

修复后，新一轮准入使用当前安装包的重复规则和条件，历史摘要仍保留当次运行的规则。活动故事与任务保持进度，错误结束不允许自动重开。没有实例时仍可首次启动。统一 NPC 仲裁、任务限定派发与奖励收据逻辑保持原有行为。

## 自动测试

相关七项 Probe 全部通过：`tavernRepeat0336Probe`、`runtimeInteractionUi0336Probe`、`canonicalActorArbitrationProbe`、`repeat0334Probe`、`currentNominatorWireProbe`、`utilityWindow0324Probe`、`publicOutputPriority0336Probe`。

新增 Tavern Probe 使用当前实际导出的“测试故事”包，覆盖首次启动、旧 ONCE 历史配合当前重复规则、只读候选收集、活动实例保护、拒绝后重开、完成后重开、限定交互、一次奖励收据、重复提交、存档恢复与错误结束阻止重开。还覆盖四边部分节点相交、完全越界剔除、缩放上下限的鼠标锚定、坐标往返及视口外命中限制。

追加启停协议回归覆盖连续发送 false / false / true / true，确认显式请求不反转状态，来源句柄保持稳定、源文件不被删除、启动准入随状态变化、缺少状态被拒绝以及关闭后会话失效。追加后 Probe 与完整构建再次通过。

原交互 UI Probe 继续验证 A 重新开始与 B 继续同时成为候选、取消与重复提交不推进、跨故事限定任务派发、四类资源名称及引用来源、同名资源身份、有界目录和成员传输、展开保留、扫描次数及权限检查。

完整回归执行 **95 项 Probe：43 通过，52 失败**。失败集合与上轮最终记录一致，**本轮无新增失败**；并非全套回归通过。原始上轮汇总中的 `creatorNetworkDiscriminatorProbe` 已恢复通过，见其上轮 followUp 记录及本轮结果。

| 检查 | 结果 | 记录 |
|---|---|---|
| 相关七项 Probe | 通过 | `.tooling/0336-runtime-ui/followup-regression.log` |
| 新增启停回归与 build | 通过 | `.tooling/0336-runtime-ui/followup-final-build.log` |
| 完整 Probe 集及 build | Probe 有 52 项既有失败；build 任务通过 | `.tooling/0336-runtime-ui/followup-full-regression.log` |
| Spotless、Checkstyle、test、reobfJar | 通过 | 最终独立 build 日志 |

完整失败名单与差异：[FollowupFullRegressionResults.json](evidence/RuntimeUi/FollowupFullRegressionResults.json)。

## 代理实机

通过 PowerShell / Win32 原生输入操作独立 Forge + CustomNPC+ 客户端与服务端。服务端使用当前导出包和 **TavernFollowupWorld 隔离副本**；原世界和正在运行的用户客户端未被更改或关闭。隔离副本内将玩家身份映射给测试账号，以保留现有完成历史、任务与绑定进行复现。测试结束已保存并关闭隔离进程。

| 实机检查 | 结果与证据 |
|---|---|
| 旧正常结束记录重开 | 右键原绑定酒馆老板进入对话：[81](evidence/RuntimeUi/81-followup-tavern-restart.png) |
| 拒绝后再次重开 | 选择拒绝后再次右键进入对话：[84](evidence/RuntimeUi/84-followup-refusal-repeat.png)；[保存证据](evidence/RuntimeUi/TavernFollowupLedger.json) |
| 最小窗口、窄目录状态、顶部按钮 | 300×180 逻辑像素，状态同排：[90](evidence/RuntimeUi/90-followup-manager-minimum.png) |
| 字体与四边裁切 | 字体随节点同步放大，左/右部分节点可见：[92](evidence/RuntimeUi/92-followup-graph-zoom-right.png)、[95](evidence/RuntimeUi/95-followup-graph-zoom-text-right.png)；上边：[94](evidence/RuntimeUi/94-followup-graph-top-clipping.png)；下边：[96](evidence/RuntimeUi/96-followup-graph-bottom-clipping.png) |
| 图节点点击 | 选择正确成员，文件夹保持展开：[97](evidence/RuntimeUi/97-followup-graph-node-hit.png) |
| 整组启停与刷新 | 禁用后启用并刷新，容器及图保留：[98](evidence/RuntimeUi/98-followup-group-disabled.png)、[99](evidence/RuntimeUi/99-followup-refresh-preserves-group.png) |
| 刷新保留图视角、展开与选择 | [刷新前](evidence/RuntimeUi/106-followup-camera-before-refresh.png)、[刷新后](evidence/RuntimeUi/107-followup-camera-after-refresh.png) |
| 深浅主题、100% 字号 | 灰黑图见 91 / 95，浅白同排完整状态见 [101](evidence/RuntimeUi/101-followup-manager-light-inline-status.png)；文字设置保持 1.0 |
| 实体、物品指名器及引用来源 | [实体](evidence/RuntimeUi/102-followup-entity-nominator.png)、[引用](evidence/RuntimeUi/103-followup-reference-font.png)、[物品](evidence/RuntimeUi/105-followup-item-resource-font.png) |
| GUI 缩放与关闭 | GUI 2 下 Esc 和 E 关闭；改绑背包键为 R 并重启客户端，GUI 1 下 [管理界面](evidence/RuntimeUi/108-followup-gui-scale1.png) 按 R 后 [返回世界](evidence/RuntimeUi/109-followup-remapped-R-closes.png) |
| 空闲时加载及按钮稳定 | 管理界面空闲约 36.76 秒，包加载结果日志条数 24→24，无额外发布。精确扫描次数另由 Probe 验证：[FollowupIdleCheck.json](evidence/RuntimeUi/FollowupIdleCheck.json) |

客户端与服务端原始日志保存在 `evidence/RuntimeUi/FollowupClient.log`、`FollowupClientRemapped.log`、`FollowupServer.log`。首次启动、完整任务奖励结算、活动进度及多故事取消/重复提交主要由自动回归验证；本轮代理实机实际执行的是原酒馆绑定的历史重开和拒绝后重开。

## 用户验收

**待用户验收**。代理实机和自动测试结果不替代用户验收。用户当前已运行的客户端仍是原加载的 JAR，需要在使用更新 Runtime 后重启 Minecraft 才会加载本轮代码。
