# 0.3.3.0 剩余施工与实机收尾

状态：剩余工程实现和本轮约定的客观实机验证已完成。多 DPI 测试由用户明确取消。用户视觉审核及主观听感不由代理代签，USER_ACCEPTED=NO，RELEASE_READY=NO。

本记录承接并更新 P10_DESKTOP.md 的未完成项目。所有新操作均在 `.tooling/0.3.3.0/p10` 隔离项目、设置和 Minecraft 世界进行。没有修改用户业务存档、Start 配置实现或全局系统音量。

## 本轮发现和修复

实际死亡/复活测试发现：服务端仍在 TITLE 等待，但复活后客户端没有标题。Minecraft Entity.equals/hashCode 使用 entityId，复活的新 EntityPlayerMP 复用该 ID，WeakHashMap 因此复用了旧呈现状态，并跳过重新投影。

CanonicalTaskPresentationServer 现以弱引用核对实际玩家对象；对象替换后移除旧键并新建呈现状态；提交请求也拒绝复用旧连接状态。没有修改资源 ID 或实体身份规则。

新增 RespawnPresentationCacheProbe，纳入既有 title0330Probe：两个不同但 equals 相等的玩家对象不能共用已投影缓存；同一对象仍复用缓存。修复后在真实客户端中重新执行死亡、复活、断线、重连，标题恢复并完成后续流程。

## 剩余项目验证结果

证据路径以下均相对 `.tooling/0.3.3.0/p10/`。

| 项目 | 实际操作与后置状态 | 证据 |
|---|---|---|
| 图层排序 | 通过文件选择导入第二张 JPEG，点击上移、保存，再下移、保存；数组次序和 z 同步变化，移回后全部图层字段恢复一致 | desktop/39-layer-sort-before.png、40-layer-sort-up.png、41-layer-sort-down.png；sort-before/up/down.json |
| 多物品+XP 作者面板 | 实际打开组合奖励，显示苹果 3、钻石 2、XP 7；编辑苹果数量到 4 后保存，再改回 3 保存，另外两项保留 | desktop/42-combined-reward.png、44-combined-reward-restored.png；combined-reward-edited/restored.json |
| 组合奖励发放 | 实际 /give 与 DGR bind_exact 命令绑定两种物品，再清空验收玩家背包；进入任务区域并完成阶段目标，得到苹果 3、钻石 2、XP 7 | combined-reward-live.json；live-remaining-fixed.log |
| 组合奖励结算/重连 | 第二目标完成后结算；断线重连仍为相同数量；存档只有一条该奖励交易凭据，reward latch=1，两目标 COMPLETED，任务 SETTLED | combined-reward-reconnect.json；remaining-task-saved-evidence.json |
| 标题并发 | 同一区域同时启动三个不同 Story，按 queue0→queue1→queue2 播放，各自完成后才输出 COMPLETED；播放结束后无残余标题 | title-queue-once-final.json；live-remaining-fixed.log；live-client/screenshots/queue-QA0330_queue*.png（前轮同序列截图） |
| 标题死亡/复活 | 死亡前 death_title/token=1，死亡清除，复活恢复同一标题/token=2；播放结束后服务端流程完成 | title-death-respawn-fixed.json；live-client/screenshots/remaining-title-before-death.png、remaining-title-respawn-fixed.png；日志 COMPLETED QA0330:death_title |
| 标题断线/重连 | 重连后同一 reconnect_title 以新 token=4 恢复（断线前 3），随后正常完成 | title-reconnect-fixed.json；live-client/screenshots/remaining-title-reconnect.png；日志 COMPLETED QA0330:reconnect_title |
| 第三方 BUFF | 实际安装 Blood Magic；DGR 目录识别其真实来源 AWWayofTime。通过三个 Story 的 give_buff 执行 Boost：一级/约 30 秒→二级/约 60 秒→负数增减移除 | bloodmagic-catalog.json；bloodmagic-effects-live.json；live-remaining-fixed.log |
| 音频淡入 | 真实游戏引擎声源音量随两秒 fade_in 增长至配置音乐音量；非仅调用假后端 | audio-fade-in-live.json |
| 音频淡出 | 实际点击 speaker 台词，进入 stop music 节点；声源音量由约 .44 逐步降为 0，随后 playing=false；旁白流程继续 | audio-fade-out-live.json；audio-audible-fade-state.json |
| 实际输出信号 | Windows 扬声器回环固定时序采样。440 Hz 测试信号在 2.5/3/3.5/4/4.5/5 秒幅度约为 .147/.107/.074/.046/.017/.00065，确认逐渐衰减 | audio-fade-out-timed.wav；audio-timed-analysis.json；audio-fade-440-extracted.ogg（仅提取测试频段，不能当作完整混音试听） |

## 采样与测试中的纠正

- 第一轮区域 fixture 使用 repeatable，停留区域会再次运行；最终队列验证改用 once 的独立测试数据，避免重复激活影响单轮统计。未改变 Start 产品实现。
- 标题断线 JSON 中 disconnected 是 quit 调用即时返回值，title 字段可能尚未经过下一 tick 清理；它不证明菜单仍渲染标题。重连新令牌与后续完成有独立证据。
- 最初回环录音全静音，排查发现隔离客户端 master=0；通过实际游戏声音设置改为 30%，之后重新采样。系统扬声器音量和其他应用音量未修改。
- 回环包含整机混音，其他声音和超过浮点满幅的峰值不能归因于 DGR，也不能据此宣称没有削波。仅用目标 440 Hz 频段验证淡出；不以混合录音代替用户主观听感审核。
- Studio 文本框聚焦时 Ctrl+Z 被文本框处理，一份探索文件 combined-reward-undo.json 未反映图操作撤销，不作为通过证据。组合数量恢复以明确改回 3 并保存的 restored 文件为准。
- 初次全量回归未传入本版本已有迁移 fixture 配置，旧 Actor schema 3 包被正确拒绝；使用 scripts/0330-p5-probes.gradle 和 P5 迁移后的 kill_slimes 包重跑全部 65 探针，全部通过。
- Blood Magic 原始 JAR 不能直接放入 MCP 开发运行时；通过现有 RFG deobf 依赖机制加载成功。这是验收环境适配，没有改写第三方 Mod 逻辑。

## 验证与交付

- 全部 65 Java 探针 + build：PASS，java-remaining-final-correct-fixtures.log，BUILD SUCCESSFUL in 2m 30s。
- Final Scope Guard：PASS，scope-remaining-final.log。
- git diff --check：PASS。
- Studio 本轮无产品代码修改，沿用已经实机启动的自包含 Windows x64 Release；既有 WPF 497 通过，Core 442 通过/1 项既有跳过，不冒称本轮重复运行。
- Studio：`dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`；ProductVersion 0.3.3.0-P10；141837534 字节；SHA-256 `5AD862117BE288ACE552B1CEBB5CA3DB646A7C14AC1280A3F2B1212C71E1D9B9`。
- 最新 JAR：`build/libs/darkgrey_rpg-0.3.3.0-P10.jar`；1108644 字节；SHA-256 `AE208306AC8090D0820436CDA423E8D3DBCE8ED2308BCC639DEEFF88FB8FF437`。

## 第三方验收依赖来源

Blood Magic 1.7.10-1.3.3-17 来自项目发布页 [Modrinth](https://modrinth.com/mod/blood-magic/version/1.7.10-1.3.3-17)；原始文件 SHA-1 `9B519305B01BC64498F0049D524FE1ACF6B3EE76` 与发布 API 一致。只用于隔离验收，不进入 DGR 交付。

回环工具基于 [NAudio 的 WasapiLoopbackCapture 官方示例](https://github.com/naudio/NAudio/blob/main/Docs/WasapiLoopbackCapture.md)，临时使用 NAudio.Wasapi 2.2.1，仅在 .tooling；没有调用麦克风。

ENGINEERING_COMPLETE=YES

OBJECTIVE_DESKTOP_ACCEPTANCE=PASS

MULTI_DPI=EXCLUDED_BY_USER

USER_ACCEPTED=NO

RELEASE_READY=NO

仅剩用户最终视觉与主观听感确认；不再遗留本轮已授权的上述功能实测。没有 Git 提交、推送或 Release。
