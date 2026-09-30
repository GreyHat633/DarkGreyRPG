> 本次用户临时审计新增17项问题已修复并验证；最新修复、产物及证据边界见[AUDIT1_FIXES.md](AUDIT1_FIXES.md)。以下为原计划记录，旧哈希／结论不替代本次审计。人工验收仍未通过。

> 当前状态（2026-09-23最终复核）：计划内开发者检查已闭环，人工验收待完成。以[PLAN_RECONCILIATION_0923.md](PLAN_RECONCILIATION_0923.md)、[DELIVERY_REPORT.md](DELIVERY_REPORT.md)和[CURRENT_ACCEPTANCE_GAPS.md](CURRENT_ACCEPTANCE_GAPS.md)为准。以下历史日志保留各自时间及产物边界，其中旧待办与旧哈希不代表当前状态。最近WPF全量605 PASS，新增帧采样单项PASS；Core原有12 SKIP不算通过。

# 0.3.3.3 验证记录

更新：2026-09-23；各历史记录保留各自产物/时间边界。测试证明所覆盖行为，不等同于用户验收或全部施工完成。

## 自动检查

| 范围 | 结果 | 本地日志 |
|---|---|---|
| Studio Core 全量 | 460 PASS / 12 SKIP | `.tmp/core-0333-profile-final.log` |
| Studio WPF 全量（G 仲裁修复后） | 602 PASS | `.tmp/0333-wpf-final.log` |
| 对话分页/状态、留声机、任务投影、重混淆构建 | PASS | `.tmp/0333-shared-cache-java.log` |
| 留声机共享引用、PCM重采样、文件租用与恢复 | PASS | `.tmp/0333-cache-lease.log` |
| 直接奖励投影、任务通知、Story媒体缓存/服务端 | PASS | `.tmp/0333-final-regression.log` |
| G冲突及组合事务 | 4 PASS | `.tmp/0333-g-gesture.log` |
| 组合事务及折叠连线回归（边界缓存初步） | 5 PASS | `.tmp/0333-group-bounds.log` |
| 嵌套边界传播/未变组复用、G事务、折叠连线 | 6 PASS | `.tmp/0333-group-bounds-serial.log` |
| 共享缓存失败重试及完整留声机探针、reobfJar | PASS | `.tmp/0333-cache-retry.log` |

新增两个原生 WPF 窗口测试首次并行运行时发生 PackagePart 流集合竞态（1 FAIL / 5 PASS，`.tmp/0333-group-bounds-final.log`）。该类按现有 UI 测试约定标记 DoNotParallelize 后，六项全部通过；未隐藏失败记录。最新边界修改后执行针对性回归，没有将此前602项全量结果冒充最新全量运行。

工具链：Java `E:/Java/jdk-25.0.1`，仓库本地 `.gradle-user-home`，Gradle offline；.NET `E:/Java/dotnet-sdk-10/dotnet.exe`，Release，仓库本地 NuGet/CLI/temp。实际游戏使用 Java 8、Forge 1.7.10、正式重混淆 DGR JAR 和 production CNPC。Core 的 12 个跳过不计通过。

## 原生界面证据

隔离工程 `.tooling/0333/Native/Project`，隔离游戏 `.tooling/0333/Client`。没有操作用户原先打开的 Studio 工程。Windows 原生截图接口在本会话恢复尝试后仍不兼容，使用 UIA/Win32/CopyFromScreen 后备操作；截图由实际动作后取得。

- Studio 实际组合按钮、Enter 提交长台词、点击画布后保存、原生导出文件对话框；正文 534 字符且无手动换行。导出 `evidence/native-ui/Native0333Long.dgrs`。
- 同一个包复制到隔离客户端 StoryPackages 后，正式 Forge 实际进入存档；通过 `/dgr session play` 启动已放置 Session aggregate。该路径不证明正常 NPC/Story 触发链。
- `long-start.png`：最小逻辑 320×240 下的第一屏；`long-page2.png`：点击正文后的下一本地屏；`long-history.png`：第一屏打开历史即可访问 END0333；`long-auto.png`/`long-auto-result.png`：开启自动后实际进入第二作者页。
- `settings-minimum.png`：设置页最小窗口；`game-session.png`：留声机世界模型及对话界面。
- `.tooling/0333/game-profile.log` 的 `DIALOGUE_STANDARD_PROFILE`：普通字体、正文横向 80..296、纵向 175..221、引号14、wrap125、rows3、raw375、safe337、W6、i2、CJK9。

上述截图来自共享缓存最终修复之前的 JAR；只支持未发生后续逻辑变化的对话/设置/模型结论，不作为最新留声机共享缓存实机证据。

## 尚未关闭的验收

本轮已新增同包双客户端会话/历史/本地翻页/字号隔离实测，见 E2E_AND_VISUAL_REVIEW.md；使用最新正式客户端JAR和隔离开发服务端，不能扩大为全部多人矩阵已通过。

当前仍需跨服/跨维度及最终构建完整多人矩阵、主观听音、多源完整音量组合、复杂分组/性能等。正常NPC任务链、同服会话中重进、IME、奖励/HUD、红石续播、试听隔离、100草稿及在线来源已在后续记录补测；以 CURRENT_ACCEPTANCE_GAPS.md 为当前缺口。声音解码和 PCM 回归不等同于实际听音。`USER_ACCEPTED=NO`、`RELEASE_READY=NO`。

## 2026-09-22 导出边界回归

- StoryPackageTests 9 PASS：.tmp/0333-export-blank-regression.log。
- Core首次全量461 PASS/1 FAIL/12 SKIP：既有头像测试以空正文构造样例，提前命中新校验；补齐正文后原头像断言保留。修复后462 PASS/12 SKIP：.tmp/0333-core-export-fixed.log。
- 发布 .tmp/0333-publish-export-fixed.log，无新增可空警告。
- 新EXE原生实测空草稿可保存、导出拒绝、无输出文件：evidence/native-ui/0922-blank-rejected.png。其余本日原生矩阵见0922-NATIVE-REVIEW.md。


### 2026-09-22 任务原生回归修复

- `.tmp/0333-reward-port-fixed.log`：canonicalTaskJournalProjectionProbe PASS；包含真实运行时验证后的 objective logic_status 直连奖励回归。
- `.tmp/0333-task-hud-fix.log`：taskTracking0332Probe、canonicalTaskJournalProjectionProbe、reobfJar PASS。
- 原生发现直接奖励端口筛选错误及 HUD 覆盖任务详情，两者已修复；后者的最新正式 JAR 原生复验尚在进行。

- `.tmp/0333-task-pending-display.log`：严格/容错投影、待绑定原始NBT只读保护、玩家隔离回归PASS；正式JAR原崩溃存档复验及恢复匹配包PASS。
- 原生三次击杀→3/3结算→三个唯一奖励收据→重进后次数1/经验与收据不变，详见0922-NATIVE-REVIEW.md及evidence/0922-*-check.json。

- 原生组复制发现并修复组空白点击焦点不在画布导致Ctrl+C/V/Delete失效；6项针对性回归通过，新EXE固定交付并实测复制、原子Undo/Redo和固定start整次粘贴拒绝。最新Studio SHA-256见ARTIFACTS.json，早期EXE证据为历史构建。
- 计分板长名字与640×480共存实测发现HUD宽度未重排，已修复并以同存档复验通过；最新JAR2ef3aa66546ba0d05252c5261c3eb63f86bc55903645ccd694a838149661d28e，1694371字节，taskTracking0332Probe/reobfJar通过。
- 新增正式Studio导出1/2/3/5选项包，原生确认居中、分页、长项Home/PageDown/End完整阅读、自动等待选择、Esc菜单及死亡/重生恢复；普通台词自动倒计时前台重置仍待独立补验。
- 满背包真实击杀后延迟奖励提示、保存重进、清石头恢复、原子XP/指名NBT物品发奖与再次重进幂等原生通过；详见0922-full-reward-*-check.json及原生验收记录。65宝石、经验8和收据在最后重进前后不变。

### 2026-09-22 正常任务链与最小窗口容量补验
- 实际区域启动任务→击杀牛→结算→带头像Session接续通过；无需debug task start。
- 640×480 / 150% / 头像：338容量三行、61W分60+1页、完整原句记录；原版强制Unicode切换后同原句重排并读至UNICODE_END。
- 证据及限定见evidence/native-ui/0922-NATIVE-REVIEW.md。完整矩阵仍未关闭，USER_ACCEPTED=NO。

### 2026-09-22 留声机原生补验
- 四方位世界模型、背包与玩家预览、半径0/1/128保存重开、同位置删除重建不继承范围显示，见原生记录。
- WASAPI扬声器回环：BGM0/Master0静音，Voice0不影响留声机，Music/Players0仍有留声机输出；低幅源BGM50与Master50单独作用结果相当。未将削顶样本或绝对声压比计为线性公式完整通过。
- GR-03：半径0/1/16/128六面内外48个有效扬声器回环样本全部通过，明确白名单与被排除干扰样本见0922-range-boundary-summary.json及原生记录；已补角点方块标记。
- Session语音跨四个本地显示页扫频连续，下一作者页才切换880Hz；Voice/BGM静音相互隔离，原版Music/Players等分类0仍有输出，Master0静音及恢复实测。见0922-session-* JSON与原生记录；仍不冒充主观听音。

2026-09-22 23:55：补齐指名器两处/复制器一处残余阴影绘制；相关偏好/指名器探针及正式重混淆构建通过，指名器浅色同位置原生对照通过。最新JAR 1694369字节 / 0e531da04212158c2b65213e72953192d080a91c675fbaa2b2e697419439c806。正式Studio导出NPC包→空手右键启动新任务→实际击杀→头像Session正常接续；会话中正常保存重进，历史保留，任务/XP/全部收据严格不变。证据与无效尝试边界见 evidence/native-ui/0922-NATIVE-REVIEW.md。人工听音及未完成矩阵仍保留待验，不宣称全计划完成。

## 2026-09-23 排版重复测量与红石/试听续播

- 新增现有 Dialogue0333Probe 计数验收，5000字符长句首次5000次Metrics；1000次不变布局/逐字读取额外0次；真实改宽再5000次。格式化后完整dialogue0333Probe通过（.tmp/0333-layout-count-formatted.log）。本次first 3.3804ms、1000帧9.9523ms为该模型探针单次观测，不是游戏帧率、修改前后基准或磁盘IO证据。
- 现有诊断开关启用后的正式JAR中，00:07:44开始音源55141951-ca3c-44a5-b90c-b19b03e5424f。早期diag-baseline/off/on采在音源真正START之前，零值无效，不计暂停证明。
- ready-baseline/on有440Hz（0.00063293/0.00062928），ready-off RMS/peak均0。同一正式音源00:08:27 PAUSE seconds=43.22133333253448，00:08:36 RESUME为完全相同位置，没有第二次START。由输出与位置共同证明红石暂停后续播；不冒充主观听感。
- 原生点击试听生成独立96849961-c8a6-4586-a3d8-5133548f80ea源；00:09:38关GUI只STOP该源（45.664秒），正式5514源没有STOP，关窗后440Hz0.00063094持续。双同频源相位会叠加/抵消，不用合成幅度推断音量倍数。
- 冷启动首次准备约47秒才START，记录为实际等待现象；尚未隔离下载/全曲分析分别耗时，不能把初次零输出归咎为红石实现故障或据此宣称缓存命中。


## GR-09 原生100份未保存草稿压力验收

- 100份本地ffmpeg生成的0.2秒MP3，以不同标题元数据产生100个不同内容哈希，共215692字节。通过真实AWT文件对话框逐一导入；001手工步骤，002–100由run-draft-stress.ps1逐次UI操作并校验缓存文件。没有调用产品私有load入口，没有点保存或上传确认。
- 0923-draft-stress.jsonl逐项记录；每10次实际就绪截图0923-stress-NNN-ready。最终100个源SHA与100个缓存SHA集合严格相等，无重复副本；峰值100份/215692字节。0923-draft-stress-imported.json记录通过。
- 导入前/中途/完成/退出后，服务端Media/Gramophone两份原媒体与Devices.properties大小、SHA完全不变，证明本场景没有自动上传或修改设备配置。关GUI重开恢复“已保存本地音乐”，未把draft100写回设备。
- 正式设备音源55141951持续，压力结束回环仍为原440Hz，幅度0.00062749；导入过程没有新的START/STOP。00:27:48正常保存卸载世界，00:27:49正式源STOP；之后程序自动删除本上下文100份草稿，closed.json记录数量0。没有手动删除缓存制造通过。
- 只证明100份小文件导入、设备保护、无自动上传和上下文关闭回收；不冒充128MiB压力、30分钟TTL等待、大文件速度或网络/解码次数测量。其他旧上下文残留按其独立租用/回收规则保留。


## GR-12 在线来源及关闭取消（1024×768）

- 正式JAR0e531da0，窗口客户区1024×768、GUI Scale2。原本地正式设备通过红石暂停在56.7467秒，其他原版声音分类为0，为在线试听隔离输出。
- 网易云无效ID99999999999999实际报“无法确认网易云曲目免费播放状态”；QQ无效mid00000000000000实际报“平台媒体标识无效”。两个失败未保存，服务器两份媒体/Devices.properties与100草稿前基线大小和SHA完全一致（0923-online-failures-server-after.json）。不据此限制明确保存规范化来源的既有契约。
- 网易云416892104官方详情返回fee0、347428ms；实际产品下载完整音轨，界面5:47，缓存5559319字节。0923-netease-actual-output实际RMS0.3392；重开该未保存来源命中当前上下文缓存并从0重新试听。没有将单次缓存命中扩大为HTTP/解码计数测量。
- 第一次网易云“准备后关窗”操作实际发生在START21秒后，所以仅计正常关窗停止，不能计准备中取消。
- QQ有效001ES52Q3qN0u7：点击试听后450ms内Esc关闭，再打开仍为旧本地设备来源；00:38:38回环全0，期间没有新的试听START，不复活旧结果。之后明确重新输入并点击，00:38:49独立新源开始，实际输出RMS0.3232；00:39:54关窗后STOP，00:39:59回环全0。对应0923-qq-cancel-*、qq-valid-*。
- 在线音乐两次满音量回环peak达到1，仅证明实际有声与停止，不声称音质/失真或主观听感通过。没有保存在线草稿替换设备源。
- 生存模式切换会隐藏客户端范围框，恢复创造重新出现；集成服主机仍拥有服务端命令权，且首次右键未准确命中设备，不能把这次操作报告为真实OP权限撤销验收。0923-range-permission-no-edit文件名不是通过结论。
- 00:45:17正常保存卸载世界，所有音源STOP。人的主观听音仍待确认。

### 2026-09-23 最新独立专服实机追加

正式 JAR 0e531da0（完整身份见 ARTIFACTS.json），真实生产 Forge 专服、双客户端：NPC各自任务隔离、150%/100%同句4/2分页、同服重连/完全退出重开、自动播放历史/聊天/背包/Esc/Windows失焦暂停、多人Gramophone个人时间轴与单方试听隔离、非OP且生存的权限失效，均补充原生UI与权威快照证据。综合包Native0333Integrated.dgrs经真实Studio导出，f9263346d0984bfd8c1f837c5a045bce81ad249d4e4e43d18593c1280bda2151；两端NPC+各自牛猪目标已实际完成。主端+7XP、UpdatedGem0333×1与单收据；同包4/2本地分页、运行中下界往返均完整权威快照不变。综合包音频/选择仍在验收中，不等于全部计划通过。详细有效/无效样本界线见evidence/native-ui/0922-NATIVE-REVIEW.md。

## 2026-09-23 03:35 验收续记

综合同包主端全链已完成；双端不同分页、运行中跨维度、A→空白B→A历史隔离与会话恢复通过。跨服前后完整会话、任务、两人XP/事务image四组严格相等。灰黑100%字号640×480 GUI2的透明度0/100端点亦完成。详细证据及无效样本见evidence/native-ui/0922-NATIVE-REVIEW.md。
仍有CURRENT_ACCEPTANCE_GAPS.md列出的视觉组合、生命周期/性能及人工听感范围；CONSTRUCTION_COMPLETE=NO、USER_ACCEPTED=NO、RELEASE_READY=NO。

2026-09-23 04:30追加：真实客户端临时计数验证布局缓存，详细范围及Agent撤销失败/重复计数排除见中央review。两个旧进程正常退出后无Agent重启，clean-restart-comparison.json完整四组均true。随后进入B服有一次原版连接屏NPE，报告保留；新PID84112能成功加入。251条独立Studio正式导出包已加载并由区域启动，仍在逐句推进，尚不记通过。

## 2026-09-23 跳过系统 DPI 后续作

- 用户明确免除修改 Windows 系统 DPI 的验收；当前 DPI 保持不变。
- 300 节点/300 分组实际 WPF 布局中位 63.600ms → 18.047ms；优化组画刷/视图复用和查找。相关回归又捕获重复节点 ID 导致的边界计算异常，已修复。
- 最终 WPF 全量 605 PASS / 0 FAIL / 0 SKIP（1m44s），见 evidence/test-results/wpf-final-0923.trx。不是只沿用旧602项结果。
- 缓存策略探针通过；追加192MiB真实磁盘文件验证超预算和过期闲置文件由后台自动删除，仍被占用文件保留。控制idle时间，不声称真实30分钟等待或下载解码。
- 网络竞争隔离探针复现上游队列同位置NPE；现场线程因果仍为推断，未修改底层网络。
- 固定EXE 0.3.3.3 / 142429910字节 / bbe9d7b46ab6a1f35631bb427e5838e6ee1e87c75286c2054f1ebc0ddf93aa22。已从固定路径启动，原生搜索嵌套组、拖动、保存和撤销坐标验证通过。
- 实际节点/Inspector交替编辑同步，草稿折叠展开不丢失，保存后Ctrl+Z还原前一文本、Ctrl+Y恢复草稿文本。完整记录见 RESUME_0923.md；整体验收仍未关闭。

2026-09-23 留声机续作：实际方法计数发现首次本地下载全曲分析重复2次，现复用传输完成后的已校验元数据，实机变为1次。移除不随进度续期的65秒消费端上限，保留传输层60秒无进展超时；尾块元数据、错误哈希、空闲超时回归及重混淆构建通过。新正式JAR 1694413字节 / a9518ff07701a89a152649b416f93542260a0e19bb5243fe2f829cdfe3eceef6。草稿排序、同步、撤销保留文本已补实机证据。详见 CACHE_LIVE_0923.md，整体计划仍未全项关闭。

留声机续作最终实测：4.8MB冷下载实际文件写入73.8秒后自动试听；一次下载只一次完整分析；QQ/网易云各首次下载分析一次、互相换源返回无新增请求或分析；关闭试听start/stop配对7/7，断开后当前上下文13.8MB缓存自动回收。诊断客户端正常退出。证据与无效输入样本边界见 CACHE_LIVE_0923.md，未关闭其余验收矩阵。
