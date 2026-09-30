# 0.3.3.3 施工状态

- 基线：`codex/0.3.3.2` / `702a096ddf7523867d545c02cb9d7bd915036d3b`。
- 施工分支：`codex/0.3.3.3`，2026-09-20 从原工作树创建；开工时 src/studio 产品源码无 tracked diff。既有 PLAN 删除、未跟踪材料和 .codex 修改保留。
- Delegation Capability Preflight：`PROJECT_NOT_RESOLVED`；INITIAL_LOCALIZATION_COMPLETE、INITIAL 检查结论 MAIN。未分派 Worker，主代理负责实施和集成。
- 范围：施工 PLAN 的 A/B/C/D/E 与 37 条追踪要求，尚未关闭。
- 当前：施工中。自动测试仅证明各自覆盖的行为，不代表完整实机验收。
- USER_ACCEPTED=NO；RELEASE_READY=NO；CONSTRUCTION_COMPLETE=NO。固定路径已同步最新 0.3.3.3 自包含 EXE 与重混淆 JAR，见 DELIVERY_REPORT.md。

## 已执行验证

- Java compileJava、playerPreferences0332Probe：PASS。
- Java canonicalSessionClientModelProbe、dialogueBacklog0332Probe：PASS（旧回归）。

## 待完成

同包双客户端、实际声音矩阵、正常 NPC/Story 触发链、任务与留声机实机矩阵、复杂组合手势/IME/性能、37 项证据表及最终产物同步。不得用自动测试总数代替这些验收。

## 2026-09-21 恢复点

- Core 全量 460 PASS / 12 SKIP；WPF 最新全量 602 PASS。
- 新增分页/容量、组合决策及事务、G 与修饰键/框选仲裁回归；直接奖励顺序/去重/负经验/未绑定物品回归；留声机共享引用释放回归均通过。
- Java Story 媒体缓存/服务端、任务通知、任务投影、留声机、对话状态等相关探针通过，日志位于 `.tmp/0333-*.log`。
- 最小逻辑尺寸 320×240、150% 字号、有头像的标准几何：换行宽 125、3 行、RAW=375、SAFE=337。正式客户端 FontRenderer 日志已核对普通字体；Unicode 数值表来自字体资源提取，仍待原生模式对照。
- 原生 Studio 实际编辑保存并导出 534 字符长台词，正式 Forge 单客户端已加载同包；手动本地翻页、自动推进至下一作者页、首屏打开历史可见完整原句末尾 END0333。证据在 `evidence/native-ui/`，包为 `Native0333Long.dgrs`。
- 修复容量警告被旧 Validate.Count 调用当错误、导致正文不可编辑/导出的问题；新增 Core 与 WPF 回归。
- 留声机设备和已保存曲目试听现在共享下载/分析媒体引用，播放器时间轴仍独立；最新共享实现尚未完成原生多设备验收。
- 组合树/删除保护/原子 Undo/Redo 已实施；G 手势冲突已修复。自动 bounds 已缓存直接成员和子组输入，仅变更组及祖先重新计算边界；6项针对性回归通过，仍需大图原生性能实测。
- 当前源码尚未提交；保留无关 PLAN 删除、用户资料和原先打开的 Studio。没有 push 或 Release。

- 留声机失败媒体释放后丢弃，重开可重试；共享消费者仍受引用保护。对应探针及 reobfJar 通过。
- 37项追踪已写入 REQUIREMENTS_TRACEABILITY.md；未完成验收逐项保留。SOURCE_SNAPSHOT.json 记录本次产物对应的改动源码 SHA-256，基线提交不冒充已提交实现。
- 早期655070ec构建的正式JAR双客户端实际通过会话启动、历史、本地翻页和字号隔离验证；服务端为隔离开发运行环境。具体动作、截图和剩余边界见 E2E_AND_VISUAL_REVIEW.md；最终服务端正常保存退出。

## 2026-09-22 原生验收续作

- 用户授权使用 Windows 自带 UI 控制。新增范围持久/草稿隔离/试听缓存、组合拖动及原子撤销、G 新建/显式组嵌套/拆父组/重启恢复证据，详见 evidence/native-ui/0922-NATIVE-REVIEW.md。
- Studio 已实际连好区域进入触发会话并导出 Native0333Region.dgrs（2871 字节，SHA-256 768b54e6aa655dfc3cb2eb9245145ba00fdfe61c22c4c3f708574a477f23f84f）；客户端运行验证进行中。


- 设置三主题、窗口拖动/尺寸、透明度及历史完整性新增实机证据。原生任务测试发现并修复 objective 端口预览筛选错误、HUD 覆盖任务详情；重建正式 JAR，原生验证正负经验预览与小屏滚动访问。详情见 0922-NATIVE-REVIEW.md；任务发奖、长文本项目符号、声音与其余矩阵仍待完成。

- 最新 8d3f5ba3... JAR 已实测不匹配旧存档展示诊断、恢复匹配包后原任务继续显示；真实击杀3/3完成，三个奖励唯一收据及重进经验/收据不变。未改任务快照绑定语义。
- TX 原生补验 Enter/Shift+Enter、真实 IME 候选、多行粘贴拒绝、Undo/Redo、336/337/338容量边界、超限导出、g/G正文与关闭重开；详情及未覆盖项见 0922-NATIVE-REVIEW.md。施工仍未全项关闭。

- 组空白焦点修复已发布至固定Studio路径；原生G跨组/移出/取消/失焦/中途切换、组复制和固定节点保护新增证据。Studio现为1c2beee5...，任务JAR仍为8d3f5ba3...；产物完整哈希见ARTIFACTS.json。

## 2026-09-22 22时续作

- 当前固定Studio仍为1c2beee5... / 142429398字节，正式JAR为2ef3aa66... / 1694371字节（以ARTIFACTS.json完整哈希为准）。此前8d3f5ba3...为历史验收构建。
- 正常区域启动任务→击杀→结算→头像会话通过；最小窗口150%容量/本地分页/ForceUnicode新增实机证据。
- 留声机四面外观、范围0/1/128及删除重建、实际本地导入上传、扬声器回环静音和分类隔离新增证据。六面播放边界正在核验。
- 仍未关闭完整PLAN验收；CONSTRUCTION_COMPLETE=NO，USER_ACCEPTED=NO，RELEASE_READY=NO。所有证明范围见evidence/native-ui/0922-NATIVE-REVIEW.md。

2026-09-22 23:55：补齐指名器两处/复制器一处残余阴影绘制；相关偏好/指名器探针及正式重混淆构建通过，指名器浅色同位置原生对照通过。最新JAR 1694369字节 / 0e531da04212158c2b65213e72953192d080a91c675fbaa2b2e697419439c806。正式Studio导出NPC包→空手右键启动新任务→实际击杀→头像Session正常接续；会话中正常保存重进，历史保留，任务/XP/全部收据严格不变。证据与无效尝试边界见 evidence/native-ui/0922-NATIVE-REVIEW.md。人工听音及未完成矩阵仍保留待验，不宣称全计划完成。
2026-09-23 00:28：正常NPC链、重进幂等、红石同位置续播、试听关窗隔离、100份未保存草稿自动回收已补原生证据；排版1000帧额外测量0次探针通过。剩余范围以 CURRENT_ACCEPTANCE_GAPS.md 为准，未关闭整体施工。

2026-09-23 01:31：最终生产 JAR 独立 Forge 专服双客户端实机通过正常 NPC→各自击杀任务→Session 隔离；640×480 Unicode同句150%分4页/100%分2页；本地翻页和见证端重连前后完整会话/任务/XP/收据严格相等。主端末页点击只结束自己。证据见 native-ui/0922-NATIVE-REVIEW.md 最新章节及0923-release-state-comparison.json。整体计划仍有 CURRENT_ACCEPTANCE_GAPS.md 所列缺口，USER_ACCEPTED/CONSTRUCTION_COMPLETE/RELEASE_READY 仍为NO。

2026-09-23 03:10：综合包已真实Studio导出并部署最终生产专服；两个玩家分别通过NPC及牛/猪实际击杀进入同一会话。4/2本地页完整权威快照一致；运行中的Unicode原句经下界往返，Dimension0→-1→0，完整会话/任务/XP/收据不变。正在继续该同包的音频与选择。最新Studio候选PID55628；Main31764、Witness74696、Server86648。详细过程与脚本失败样本已记录中央review。
2026-09-23 03:50：主端综合故事链已结束；A→空白B→A恢复会话与历史、四组权威快照严格一致；三主题透明度端点追加，正文RGB独立于底板alpha。实际窗口640×480/960×640/1280×800及GUI1/2有新样本，但GUI1 Unicode字形欠清晰，保留视觉缺口，不能宣布全矩阵通过。Witness现GUI2、Unicode OFF、浅白150%、底板100%，停在综合Unicode作者句。B服PID56772，A服仍86648，两个客户端及Studio PID不变。无产品源码变更；本轮只增加隔离验收工具、样本与记录。
2026-09-23 04:12：临时诊断取得真实Session排版缓存证据（resize1次，后续2828次draw新增measure0；本地逐字不新增measure）；Main真实NPC再接活动任务，历史往返accept/save/rebuild不变。诊断remove未在Forge/Mixin生效，重复安装计数排除，两个客户端已正常退出并无Agent重新启动。最新Main86636、Witness49120；重启前后完整权威四组快照相等。详见中央review修正说明，整体性能验收仍未关闭。
2026-09-23 04:43：DP-15独立B服251条FIFO实机通过，历史001..250→002..251；作者页249→250/epoch250→251，任务/玩家XP及Inventory/事务严格不变。Studio导出包13972字节，f3266b02a10c72ce0b539e629c684cc11355b4c2612d1e3bd2e22dc1141dd271。最新Witness PID84112在B服251末句，Main86636在A服活动任务；两服86648/56772，Studio55628当前HistoryProject。连接屏一次崩溃及GUI1 Unicode可读性仍为缺口，计划未全部完成。

2026-09-23 DPI豁免后的续作完成一轮：系统DPI按用户要求跳过；WPF全量605通过。修复300组移动布局开销（中位63.600→18.047ms）及重复节点ID边界计算异常；新固定Studio已发布并原生验证组搜索/拖动/保存/撤销和台词双入口编辑/草稿折叠/UndoRedo/完全退出重开。缓存受控策略及192MiB真实文件回收通过；上游NetworkManager竞争窗口隔离复现，但现场因果仍为推断。详见RESUME_0923.md。当前固定EXE 142429910字节，SHA256 bbe9d7b46ab6a1f35631bb427e5838e6ee1e87c75286c2054f1ebc0ddf93aa22；JAR不变。整个PLAN仍未全项关闭。

2026-09-23 留声机续作：实际方法计数发现首次本地下载全曲分析重复2次，现复用传输完成后的已校验元数据，实机变为1次。移除不随进度续期的65秒消费端上限，保留传输层60秒无进展超时；尾块元数据、错误哈希、空闲超时回归及重混淆构建通过。新正式JAR 1694413字节 / a9518ff07701a89a152649b416f93542260a0e19bb5243fe2f829cdfe3eceef6。草稿排序、同步、撤销保留文本已补实机证据。详见 CACHE_LIVE_0923.md，整体计划仍未全项关闭。

留声机续作最终实测：4.8MB冷下载实际文件写入73.8秒后自动试听；一次下载只一次完整分析；QQ/网易云各首次下载分析一次、互相换源返回无新增请求或分析；关闭试听start/stop配对7/7，断开后当前上下文13.8MB缓存自动回收。诊断客户端正常退出。证据与无效输入样本边界见 CACHE_LIVE_0923.md，未关闭其余验收矩阵。
