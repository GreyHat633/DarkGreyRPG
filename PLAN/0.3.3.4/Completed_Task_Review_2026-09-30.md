# 0.3.3.4 已完成任务回顾修正

USER_ACCEPTED=NO，RELEASE_READY=NO。本轮不提交、不推送，不修改用户正式故事包和存档；测试使用既有隔离 speaker-buttons-world。

## 原因与修正

完成历史原先只记录标题、简介、次数、结算时间和内部结果端口，并把 objectives 写为空列表。服务端又以该历史列表替换结算任务投影，导致客户端只能显示“最近结果：dynamic_port_...”和“此记录仅保留已结算摘要”。上一轮按钮验证没有把这个历史内容缺陷当作阻断项，这是遗漏。

现改为：

- 结算时保留实际 COMPLETED 目标的描述与最终进度；不会把未走支线或未完成目标标为完成。
- 页面显示任务简介、完成次数、“已完成目标：”和逐项目标／进度。内部 result 仍用于原有记录与匹配，玩家界面不再显示结果端口 ID。
- 历史目标仅保存轻量文本与数字，不复用运行状态，不触发奖励、重新计数或提交操作。已有原生物品格及进行中任务逻辑保留。
- 旧历史若仍有同一激活轮、同一结算时刻和结果的匹配运行存档，会在现有绑定流程中补回目标；不会增加完成次数。没有匹配数据时显示“此旧记录未保存具体目标，无法还原当时的完成详情。”，不按当前任务定义编造历史路径。
- 保持既有完成历史 schema=1，字段为兼容追加；Canonical TaskInstance 身份、运行存档、奖励凭据及报文大小边界不变。

## 验证

构建及探针通过：assemble、canonicalTaskEventPersistenceProbe、canonicalTaskJournalProjectionProbe、canonicalTaskJournalIntegrationProbe。新增检查覆盖目标随运行记录移除后保留、重启去重、旧记录匹配补回、结果不匹配时拒绝补回、未走支线排除，以及客户端不显示内部结果 ID。

日志：`evidence/completed-task-build.log`、`evidence/completed-task-final-probe.log`。本轮相关文件 git diff --check 通过。CAS 预检返回 SCHEDULER_PAUSED，Main 完成跨模块修正及整合。

实机全部使用 windows-native-ui 技能的 Win32 输入和截图：

| 场景 | 结果 | evidence/native-ui 证据 |
| --- | --- | --- |
| 原隔离世界的旧记录 | 补回目标 3/3，完成次数仍为 1，无原始结果端口 | history-task-legacy.png |
| 新完成的三目标任务 | 显示三个实际目标及 3/3、4/4、5/5 | history-three-goals-stable.png |
| 缩小窗口与鼠标滚动 | 回顾区域滚动，按钮独立保留 | history-compact-top.png / history-compact-bottom.png |
| 客户端和服务器 JVM 重启 | 三个目标与次数仍保留，未重复计数 | history-restart-multi-full.png |
| 单目标记录 | 显示“击败三只猪”和已完成 3/3 | history-single-goal.png |

测试使用配套构建、隔离测试包及角色 Speaker0334；新任务通过隔离服务器 debug 事件结算。没有将人工事件验证当作真实击杀操作回归。GUI 缩放为 2，覆盖普通及缩小窗口；不扩大为所有缩放、字体和物品历史组合均已实机验收。

关闭隔离客户端与服务器后恢复八项原测试参数、配置、包和 MOD，哈希逐项匹配，见 `evidence/completed-task-restore.json`。

## 交付

- 权威 JAR：`E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.4.jar`
- mcmod.info 版本：`0.3.3.4`
- 大小：`1,789,832` 字节
- SHA-256：`85552EC69DE674475A6AC81C53BB2C4969B7BD93CF99E67342860D31A1102F12`
- 交付 JAR 与实机所用构建哈希一致。客户端与服务端配套使用该 JAR；Studio 本轮无变更。

此前 C10、C12 和媒体公网／多人、严格帧分布等未覆盖项继续保持开放。旧记录已经丢失的目标数据无法可靠恢复，本轮不直接修改正式世界来填造数据。
