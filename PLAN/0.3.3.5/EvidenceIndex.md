> 2026-10-01 最新折叠交互、候选浏览与追踪 HUD 修正见 [FOLD_TASK_UI.md](FOLD_TASK_UI.md)。正式 EXE/JAR 及 SHA-256 已同步至 Delivery.json。

> 2026-10-01 编辑区精简与故事任务分组的最新交付见 [STUDIO_TASK_POLISH.md](STUDIO_TASK_POLISH.md)。历史证据保留，完整人工验收尚未标记通过。

> 2026-10-01 本次用户确认方案的最新实现、正式交付与验证见 [SEQUENCE_REVIEW.md](SEQUENCE_REVIEW.md)。Delivery.json 已更新；下文旧轮次记录保留为历史证据。

> 2026-10-01 九项截图问题的最新修复与交付见 [UI_REVIEW_FIX.md](UI_REVIEW_FIX.md) 和 Delivery.json。下文保留上一轮记录，不代表追加修复前已满足用户要求。

# 0.3.3.5 证据索引

最终综合结果以 IntegratedStudio0335-Acceptance.dgrs 和 acceptance-* 截图为准。其他列出的早期单项操作仅证明对应子项，不冒充最终同包全链路。目录内其他截图包含定位、失败与修复前状态，不能统一解释为通过。

- [aggregate-fixed-export-verified](evidence/native-ui/aggregate-fixed-export-verified.png)：最终正式 EXE 导出成功；部署包逐字节一致，聚合端口方向内顺序无冲突。
- [acceptance-package-task-active](evidence/native-ui/acceptance-package-task-active.png)：最终同包实际区域触发 Story→Task。
- [acceptance-npc-crosshair](evidence/native-ui/acceptance-npc-crosshair.png)：实际 NPC 交物结算后进入长台词会话。
- [acceptance-long-local-page2](evidence/native-ui/acceptance-long-local-page2.png)：1026 字长台词本地第二页；同一作者页语音只启动一次。
- [acceptance-true-choice](evidence/native-ui/acceptance-true-choice.png)：Task Logic 经过 Story 进入 Session；True 玩家显示三项。
- [acceptance-false-choice](evidence/native-ui/acceptance-false-choice.png)：独立 False 玩家隐藏一项、禁用一项并显示说明。
- [acceptance-second-session](evidence/native-ui/acceptance-second-session.png)：继续选择进入第二条会话。
- [acceptance-second-choice](evidence/native-ui/acceptance-second-choice.png)：第二会话全部普通转场后进入最终 Choice。
- [acceptance-story-finished](evidence/native-ui/acceptance-story-finished.png)：最终同包正常结束故事。
- [acceptance-final-history-detail](evidence/native-ui/acceptance-final-history-detail.png)：真实 NPC 交物历史完成次数 3；与早期调试击杀历史区分。
- [acceptance-witness-reset-english](evidence/native-ui/acceptance-witness-reset-english.png)：实际故事重置关闭旧会话；场景与音频占用随后归零。
- [final-choice-ime-composition](evidence/native-ui/final-choice-ime-composition.png)：正式 Studio 中文输入法组合测试。
- [final-choice-ime-commit](evidence/native-ui/final-choice-ime-commit.png)：正式 Studio 实际提交中文输入。
- [final-light-candidate-tooltip](evidence/native-ui/final-light-candidate-tooltip.png)：浅色主题原生候选 Tooltip。
- [final-blue-candidate-tooltip](evidence/native-ui/final-blue-candidate-tooltip.png)：蓝色主题原生候选 Tooltip。
- [final-light-choice-false](evidence/native-ui/final-light-choice-false.png)：浅色主题 False 菜单。
- [final-blue-disabled-click](evidence/native-ui/final-blue-disabled-click.png)：蓝色主题禁用点击不推进。
- [final-main-disconnected](evidence/native-ui/final-main-disconnected.png)：主客户端正常断开后到主菜单；最终采样缓存归零。
- [final-witness-disconnected](evidence/native-ui/final-witness-disconnected.png)：第二客户端正常断开。
- [final-choice-false](evidence/native-ui/final-choice-false.png)：False: hidden A, disabled B with hint, unconnected C enabled。
- [final-disabled-click](evidence/native-ui/final-disabled-click.png)：Disabled click does not advance; paired server state in LiveState.log。
- [final-choice-keyboard](evidence/native-ui/final-choice-keyboard.png)：Tab skips disabled B and focuses C。
- [final-line-active](evidence/native-ui/final-line-active.png)：Source rectangle before morph。
- [final-transition-mid](evidence/native-ui/final-transition-mid.png)：Actual intermediate rectangle position and size。
- [final-choice-true](evidence/native-ui/final-choice-true.png)：True menu and unchanged target rectangle。
- [final-fixed-next1](evidence/native-ui/final-fixed-next1.png)：Footer button reaches page 2。
- [final-fixed-lastpage](evidence/native-ui/final-fixed-lastpage.png)：Page 3 has 5 candidates, footer remains anchored。
- [final-fixed-wheel-lastpage](evidence/native-ui/final-fixed-wheel-lastpage.png)：Wheel reaches last page with native TNT tooltip。
- [final-preview-mid](evidence/native-ui/final-preview-mid.png)：Formal EXE explicit-source preview moving rectangle。
- [final-preview-end](evidence/native-ui/final-preview-end.png)：Formal EXE target and dialogue reference。
- [final-preview-return](evidence/native-ui/final-preview-return.png)：Return to editing restores layer handles。
- [final-preview-dialogue-options](evidence/native-ui/final-preview-dialogue-options.png)：Both dialogue and choice reference frames。
- [final-usage-visible](evidence/native-ui/final-usage-visible.png)：Typed media query produces two field locations。
- [final-export-saved](evidence/native-ui/final-export-saved.png)：Actual formal EXE export success。
- [final-samepackage-line-native](evidence/native-ui/final-samepackage-line-native.png)：Dialogue from deployed native Studio export。
- [final-samepackage-native-true-confirmed](evidence/native-ui/final-samepackage-native-true-confirmed.png)：Native public gate True shows all options。
- [final-samepackage-witness-false-clean](evidence/native-ui/final-samepackage-witness-false-clean.png)：Witness independent False gate。
- [final-samepackage-page3-final](evidence/native-ui/final-samepackage-page3-final.png)：Native export group last page after reconnect/respawn。
- [acceptance-effect-fade-mid](evidence/native-ui/acceptance-effect-fade-mid.png)：最终同包实际 fade 转场 mid 状态。
- [acceptance-effect-fade-end](evidence/native-ui/acceptance-effect-fade-end.png)：最终同包实际 fade 转场 end 状态。
- [acceptance-effect-slide-left-mid](evidence/native-ui/acceptance-effect-slide-left-mid.png)：最终同包实际 slide-left 转场 mid 状态。
- [acceptance-effect-slide-left-end](evidence/native-ui/acceptance-effect-slide-left-end.png)：最终同包实际 slide-left 转场 end 状态。
- [acceptance-effect-slide-right-mid](evidence/native-ui/acceptance-effect-slide-right-mid.png)：最终同包实际 slide-right 转场 mid 状态。
- [acceptance-effect-slide-right-end](evidence/native-ui/acceptance-effect-slide-right-end.png)：最终同包实际 slide-right 转场 end 状态。
- [acceptance-effect-slide-up-mid](evidence/native-ui/acceptance-effect-slide-up-mid.png)：最终同包实际 slide-up 转场 mid 状态。
- [acceptance-effect-slide-up-end](evidence/native-ui/acceptance-effect-slide-up-end.png)：最终同包实际 slide-up 转场 end 状态。
- [acceptance-effect-slide-down-mid](evidence/native-ui/acceptance-effect-slide-down-mid.png)：最终同包实际 slide-down 转场 mid 状态。
- [acceptance-effect-slide-down-end](evidence/native-ui/acceptance-effect-slide-down-end.png)：最终同包实际 slide-down 转场 end 状态。
- [acceptance-effect-wipe-left-mid](evidence/native-ui/acceptance-effect-wipe-left-mid.png)：最终同包实际 wipe-left 转场 mid 状态。
- [acceptance-effect-wipe-left-end](evidence/native-ui/acceptance-effect-wipe-left-end.png)：最终同包实际 wipe-left 转场 end 状态。
- [acceptance-effect-wipe-right-mid](evidence/native-ui/acceptance-effect-wipe-right-mid.png)：最终同包实际 wipe-right 转场 mid 状态。
- [acceptance-effect-wipe-right-end](evidence/native-ui/acceptance-effect-wipe-right-end.png)：最终同包实际 wipe-right 转场 end 状态。
- [acceptance-effect-wipe-up-mid](evidence/native-ui/acceptance-effect-wipe-up-mid.png)：最终同包实际 wipe-up 转场 mid 状态。
- [acceptance-effect-wipe-up-end](evidence/native-ui/acceptance-effect-wipe-up-end.png)：最终同包实际 wipe-up 转场 end 状态。
- [acceptance-effect-wipe-down-mid](evidence/native-ui/acceptance-effect-wipe-down-mid.png)：最终同包实际 wipe-down 转场 mid 状态。
- [acceptance-effect-wipe-down-end](evidence/native-ui/acceptance-effect-wipe-down-end.png)：最终同包实际 wipe-down 转场 end 状态。
- [acceptance-effect-random-horizontal-mid](evidence/native-ui/acceptance-effect-random-horizontal-mid.png)：最终同包实际 random-horizontal 转场 mid 状态。
- [acceptance-effect-random-horizontal-end](evidence/native-ui/acceptance-effect-random-horizontal-end.png)：最终同包实际 random-horizontal 转场 end 状态。
- [acceptance-effect-random-vertical-mid](evidence/native-ui/acceptance-effect-random-vertical-mid.png)：最终同包实际 random-vertical 转场 mid 状态。
- [acceptance-effect-random-vertical-end](evidence/native-ui/acceptance-effect-random-vertical-end.png)：最终同包实际 random-vertical 转场 end 状态。
- [acceptance-effect-none-mid](evidence/native-ui/acceptance-effect-none-mid.png)：最终同包实际 none 转场 mid 状态。
- [acceptance-effect-none-end](evidence/native-ui/acceptance-effect-none-end.png)：最终同包实际 none 转场 end 状态。

自动日志和 TRX 见 `evidence/automated`；需求落点、失败重跑与尚未覆盖矩阵见 `Construction.md`。
- [采样与正式基线比较](evidence/automated/LiveMetrics.json)：真实 GUI draw CPU、帧起点间隔、缓存／纹理／pin 数量。
- [编码与发送故障注入](evidence/automated/task-send-fault-final.log)：失败发送状态不变、随后重试提交；只发生于隔离服务器。
- [最终综合操作记录](evidence/automated/FinalIntegratedObservations.json)：代理观察与原始日志／截图定位，不代表人工验收。
- [最终正常保存与卸载](evidence/automated/LiveServerFinal.log)。
- [历史回收站核验](evidence/automated/RecycleBinVerification.json)：此前四个转储移入回收站的记录。
- [当前收尾核验](evidence/automated/FinalShutdownAudit.json)：测试进程与端口已关闭、转储原目录已空；历史回收站路径目前已不存在，原因未核实，不能保证恢复。
NativeExportRoundTrip.json 是修复前 Routed→Final 的诊断差异，不能解释为最终导出通过；最终文件一致性以 Delivery.json、FinalIntegratedObservations.json 为准。
完整测试夹具、原始日志与启动辅助工具保存在 `.tooling/0335`。RCON 凭据只在隔离服务器配置内，不复制进审计材料。
源码快照见 `SourceSnapshot.json`；正式产物及同包一致性见 `Delivery.json`。

`CARD_TASK_UI.md`：最新卡片交互、详情缓存、候选网格和紧凑 HUD 修正；正式文件哈希以当前 Delivery.json 为准。

PORTS_HUD_UI.md：最新选择端口共同行、全部生效目标追踪与故事/任务卡片；当前正式哈希见 Delivery.json。

PREVIEW_COMPACT_PORTS.md：最新预览工具栏位置与选择端口两侧紧凑排列；正式EXE哈希以Delivery.json为准。
