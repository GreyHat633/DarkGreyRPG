# 0.4.0.0 验证记录

日期：2026-10-07—2026-10-08。自动、Windows 实机、Minecraft 实机及用户验收分别记录。

当前：代码施工及权威开发 dist 更新已完成，Data 完整保留；完整 WPF 复核为 655 通过、1 跳过、0 失败。额外 Task 过滤原生作者链与人工听音仍未关闭，用户接受为 false。

## 基线

| 检查 | 结果 | 证据 |
|---|---|---|
| 锁定 Java 依赖 | PASS | prepare-java-dependencies 实际执行，四项文件校验通过 |
| Studio Core Release | PASS：492 通过、11 跳过、0 失败 | evidence/baseline-core.log / baseline-core.trx |
| Studio WPF Release | PASS：692 通过、1 跳过、0 失败；8m51s | evidence/baseline-wpf.log / baseline-wpf.trx |
| Java build + 六项当前 Probe | PASS | evidence/baseline-java.log |
| 权威 dist 基线 | 0.3.3.7；未更新 | evidence/baseline-studio.json |

11 项 Core 跳过为已有媒体环境用例，不计 PASS。未复用旧报告的 PASS 数作为本版验证。

## P1 当前验证

| 检查 | 结果 | 证据 |
|---|---|---|
| Core Release，当前源码 | PASS：508 通过、11 跳过、0 失败 | evidence/p1-core-complete.log / p1-core-complete.trx |
| WPF 全量 | PASS：692 通过、1 跳过、0 失败；16m52s | evidence/p1-wpf-verified.log / p1-wpf-verified.trx |
| 最后一次 Core 包校验修改后的 WPF 引用/导入/导出回归 | PASS：58 通过、0 失败 | evidence/p1-wpf-package-confirmed.log / trx |
| 双端 schema/Kind/空草稿与正文保护 | PASS | CanonicalOnly0400Tests、TaskTargetContract0400Probe |
| 内存变更、Undo、复制及外部声明 | PASS | EditorRejectsNativeMutationAndRetainsUndoHistory、ProjectRequiresDeclaredTargetAndPreservesExplicitForeignReference、ResourceCopyRemapperTests |
| Kill 去重、玩家归因、原子扣除、FUZZY/EXACT、damage/NBT、多格计数、锁存及恢复 | PASS | CanonicalTaskForgeProbe；p1-java-verified.log |
| give_item 当前形态/旧形态拒绝 | PASS | CanonicalStoryActionConfigurationProbe、CanonicalOnly0400Tests |
| C# 单包/组包原生目标，六种组合 | PASS，拒绝前后 SHA-256 相同；不生成指纹结果 | evidence/p1-native-package-rejection.json / log；verify-native-package-rejection.ps1 |
| Java 当前项目、当前单包/组包及相同成员指纹 | PASS；组包重复副本 fixture 修复后通过 | evidence/p1-java-complete.log / p1-package-resumed.log |
| Java 同六个坏包拒绝且不部分安装 | PASS；六种组合，文件字节不变且无部分安装 | CanonicalOnly0400PackageProbe；p1-package-resumed.log |
| git diff --check | PASS | evidence/p1-diff-check.txt |

所有这里列出的 WPF 结果属于自动层 A，不能代替真实 Studio 键鼠作者链的 W 层验收。包由 Studio Core 的实际创建/保存/导出服务生成；未冒充通过原生 Studio 窗口导出。新 Core 数量增加 16 项，不以数量作为关闭条件。

全量 WPF 在最后的 dormant 重复 ID 防护修改之前运行；该 Core 修改随后完成全量 Core 和 58 项受影响 WPF 回归。GroupScale0333Tests 的 300 组用例由基线约 230s 增至约 700s，其他主要渲染用例约 199s 不变。差异已保留在 evidence/p1-wpf-duration-comparison.json；本批没有受控性能复测，不能据此声称性能改善或已查清变慢原因。

## 测试改接与失败尝试

| 原测试/Probe | 保留行为/处理 | 当前证据 |
|---|---|---|
| CanonicalTaskObjectiveAuthoringTests 的原生目标正向断言 | 改为原生目标拒绝；typed Actor/Item/Group 的作者断言保留 | Core 全量 |
| ResourceCopyRemapperTests | 不再保留原生目标；保留资源映射、来源身份及正文不替换 | Core 全量 |
| CanonicalTaskForgeProbe | 旧 registry/拾取辅助退出；实际库存、事务、管理器隔离、Journal、恢复、前置条件、致死归因改接 DGR key | Java 当前 Probe |
| CanonicalStoryActionConfigurationProbe | 删除 legacyRegistryItem 接受；保留签名整数、XP、消息等当前行为 | Java 当前 Probe |
| WPF 目标选择/同步样本 | 将裸 actor/item/group ID 改为当前地址；保留双入口同步与 Undo 断言 | WPF 全量及 35 项首次针对复测 |
| LegacyStoryBoundary0331Tests | 只更新当前 graph schema；稳定公开端口、未保存输入、重开及 Flow 持久化继续验证 | WPF 全量及包回归 |
| CurrentIdentityFixture | 替换默认同 ID 目标，避免重复追加；导出 producer_version 更新为 0.4.0.0 | 当前双端项目/包验证 |
| CanonicalStoryCommandProbe | 原源码文本断言未处理格式化换行，基线同样不满足；只修正空白处理，不删除 Journal 边界检查 | p1-semantic-verified.log |

失败日志均保留，没有覆盖为 PASS：Core 首轮暴露旧样本/Manifest graph 版本声明，随后修复；WPF 首轮 9 个失败，包含 8 个旧样本和 1 个目录搬移 IO 拒绝，35 项针对复测及全量通过。两次最后 Core 复测分别在不同 AtomicFileWriter.Replace 用例遇到“无法删除要被替换的文件”，同份代码第三次全量通过；未修改 IO 实现或排除用例，原因未确定。

Java 构建/Probe 尝试先后暴露格式检查、Probe 异常类型、Forge registry 头less注册、checked IOException 和 detached properties 测试样本错误，均保留日志并修正；没有恢复旧目标路径。首次扩大 Probe 未传 groupFixtures 参数，配置失败且未运行测试。首次 group fixture 生成重复默认目标，已修正生成器；普通 group 作者草稿没有连通 Start，不能用作运行 Start 的正向样例，运行验证改用 runtime-group fixture，没有放宽 Story Runtime。

## P1 检查点当时尚未关闭

P3—P6 清扫和完整验收矩阵、实际 Story give_item 世界绑定/防重、真实实体指名/点击提交、同包原生 Windows/Minecraft E2E、Data 推广前后对比、权威 dist 更新均 NOT_RUN。旧 Runtime/作者业务、Choice 输出、Manifest 空列表仍存在，不能将本批通过解释为单栈版本完成。

当前没有 0.4.0.0 完成交付或用户接受声明。

## P2 自动验证

- `p2-java-current.log`：build 与 Nominator0400、EntityDgrIdentityResolver、Stage4、0323、ItemContainer、B4NetworkDiscriminator、CurrentNominatorWire 共 7 个 Probe 通过。
- `p2-current-actions.log`：再次 build；当前统一操作服务及 CNPC Probe 通过。宿主编号/UUID、距离、维度、权限/工具、三类 revision、包闭包拒绝均没有改变绑定；实体绑定、明确转移、解绑、无实体资源释放通过。
- 实际 Minecraft MapStorage 保存/重载通过。旧 schema3、schema4 的空/非空 type_groups、未知字段、损坏 UUID、负 revision、损坏压缩文件共 7 个样本，经缓存失败对象、强制 dirty、saveAllData 后，原字节完全相同；输出 NBT sentinel 保持。日志内 Minecraft 捕获并打印的异常是这些负向样本的预期证据。
- A 指名僵尸/B 未指名僵尸/C 指名骷髅证明具体身份。CNPC 旧内部存储仍被忽略、解绑不复活、客户端数据不改；同 UUID 宿主保护仍验证。
- 物品入口不依赖不可用的实体 SavedData；失败窗口/修订/闭包/转移请求保槽物品，成功 bind/transfer/FUZZY/unbind 返回正确数量。原容器 Probe 的关闭、库存满返还及取消语义继续验证。
- 本批第一轮编译暴露 CNPC Probe 的旧 overload；修正为现行 Resolver，没有恢复旧入口。一次任务配置尝试因工具脚本未指定 UTF-8 而没有插入 Gradle task；日志保留，随后修正并实际运行。
- 旧 8/9 注册为空；现役完整 channel 的 30 项按消息方向校验，无冲突，10—13、19/20 及其他当前编号保持。Open 的旧 marker 和尾随字节拒绝。

上述是自动验证 A，尚未执行游戏真实点击 W/M。正式世界没有加载或写入；`.tooling/0400-nominator-world-*` 是独立测试文件。


## P3 与 P4 增量证据

- P3 build、当前 Project 原子 reload、Task View codec、任务 Journal 投影/Stage4/命令/通知、网络注册、指名 MapStorage/实际 actions、Resolver/CNPC、Studio 实际导出身份及单包/组包六负向：13 Probe PASS（`p3-p4-current-combination.log`）；整体日志因独立 P4 Session 旧夹具失败为 EXIT1，不能称整组成功。
- 已构建 Runtime JAR 无 63 个退休类及其内部类，`p3-built-jar-retired-class-audit.json`。
- P4 Choice Core 21、WPF 51、当前 lifecycle/Core Project 30 项 PASS；Session Java 与 CL-08 工作区清理待继续。
- dist、成品目录、正式项目/世界和远端未更新；原生 UI/Minecraft/P6 尚未运行。


## P4/P5 与 P6 当前进展

| 范围 | 实际结果 | 证据 |
|---|---|---|
| Core 全量检查点 | 498 通过 / 11 跳过 / 0 失败 | evidence/p6-core-confirmed-final.trx |
| WPF 全量检查点 | 653 通过 / 1 跳过 / 0 失败 | evidence/p6-wpf-final.trx |
| P5 包/Manifest Core | 38 通过 | evidence/p5-core-package-confirmed.trx |
| 同一份 Studio 单包/组包，C# 21 个拒绝输入 | PASS，字节未改写 | evidence/p5-same-package-csharp-final.log |
| 同包 Java 6 个原生目标 + 15 个 Manifest 变体 | PASS，无回写/部分注册 | evidence/p5-package-verified.log 中 CanonicalOnly0400PackageProbe；该次合并命令的旧 Loader/Diagnostics 夹具失败另记 |
| 原生 Story/Session 创建与 Enter | PASS，隔离 Data | evidence/p6-ui-current-receipt.json |
| 原生 Story 搜索 | PASS，3→1→3 | evidence/p6-ui-search-result.json |
| 原生 Choice 创建/条件/保存 | PASS，输出 Flow、输入 Logic，未产生输出 Logic | evidence/p6-ui-current-receipt.json |
| 权威 dist / 用户 Data 指纹 | 尚未提升/核验 | 不把候选计为交付 |
| Minecraft 本轮实机矩阵 | 基础链已运行，完整 E2E 仍在继续 | 见下方最新记录 |

退休测试与保留混合测试的映射见 evidence/p4-test-retirement.json。跳过项均单独记录，不计 PASS；原始失败日志保留。

## 2026-10-08 实机与最终回归检查点

| 范围 | 实际结果 | 证据 |
|---|---|---|
| Task 候选个体／Group／dormant／Kind／revision／121 项分页 | PASS；删除个体误查 Group 与 registry fallback | evidence/p6-task-candidate-fix.log |
| Loader／Diagnostics／Session 当前夹具与包拒旧 | PASS | evidence/p5-runtime-confirmed.log |
| 真实 Studio 组包完整准入、禁用、冲突、CRC 与重复单包 | PASS | evidence/p6-same-ui-group-confirmed.log |
| 当前网络注册、28 个保留通道，3/4/8/9 空洞 | PASS | evidence/p6-network-current.log |
| 原版单人基础链、结算前重进、无变更 reload | PASS；进度 2/1/0/2 恢复，结算后 XP=7 | evidence/p6-singleplayer-active-darkgrey_rpg_canonical_tasks.json、p6-singleplayer-player-after-reload.json；对应 resumed／settled 截图 |
| 原版专服基础链与扣物 | PASS；煤炭 4→2，结算 7 XP | evidence/p6-base-server-submitted-two.png、p6-base-server-player-after-reload.json |
| CNPC 原生 NPC 基础链与唯一宿主 | PASS；真实未指名死亡=0、指名死亡=1，煤炭 4→2 | evidence/p6-cnpc-real-session-media.png、p6-cnpc-real-submitted-two.png、p6-cnpc-real-unique-host-protected.png、p6-cnpc-negative-kill-entities.json、p6-cnpc-bound-real-kill-one.png |
| CNPC 正常停服／重启后的活动任务与奖励 | PASS；UNCHANGED generation，恢复 ACTIVE/TASK 后结算，reload 后 XP=7 | evidence/p6-cnpc-before-reconnect-tasks.json、p6-cnpc-real-active-after-server-restart.png、p6-cnpc-player-after-reload.json |
| 两专服组包禁用／启用、单包冲突、未冲突组、恢复 | PASS；相关容器对称阻断，组成员运行不受影响，恢复 3 故事 | evidence/p6-base-server-group-disabled.png、p6-base-server-conflict-unrelated-group.json、p6-cnpc-real-group-disabled.png、p6-cnpc-conflict-unrelated-group.json |
| 最新 Studio 自包含候选 Data | PASS：部署前后路径／大小／SHA 相同 | evidence/p6-candidate-data-before-final-publish.json、p6-candidate-data-after-final-publish.json |
| 资源列表 Enter | PASS：PreviewKeyDown 提前处理；先红 1，再绿 32，并在最新候选真实按键打开 Session | evidence/p6-ui-enter-resource-verified.png、p6-resource-enter-red.log、p6-resource-enter-green.log、p6-ui-resource-enter-fixed.png |

早期 CNPC 最小召唤及原生 RCON 的失败尝试、尚未真正击杀的攻击截图、未连接成功的重进截图，不计通过。成功证据使用上述 `real`／`final` 文件及 SavedData；原文件保留。单人测试曾受到夜间环境攻击并死亡，最终库存不能用于证明剩余煤炭数量；扣物使用死亡前截图和两专服当前存档。声音没有实际听取，不把启动日志计为听音通过。

## 同包原生补充样例

Studio 界面新建 `ST-5UTG-DFCB-NQAQ-X4QF`、集体角色“山贼”、个体角色、物品、Session、两份 Task；通过节点菜单、Inspector、原生端口拖线完成图，保存并验证后从真实导出对话框生成 `0400 补充验收.dgrs`。导出及五个隔离安装副本 SHA-256 为 `2C7834FE85DB945811A2E1128C5199E34277D817974616D5CC375572BB35BFEE`。该正向图没有通过改写 JSON 或 fixture 生成。

- E2E-A 当前实机链通过：未指名僵尸 B 交互时 Story absent；真实击杀 B 仍为 0，具体指名僵尸 A／骷髅 C 分别增加至 1／2。Nominator SavedData 记录两份具体 UUID 的同一集体角色地址。
- E2E-C 基础实机链通过：未连接条件可选，False 隐藏／置灰，Story 公共 Logic 更新后 True 选项恢复；line_epoch 前后均为 3，presentation 字节相同，原生禁用点击没有推进 Flow。Task 公共 Logic 经第二个 Session placement 的正常边界显示为 True。
- 两个结算条件同时 True，实际 `result_port_id` 选择 `display_order=0` 的出口；零结算 Task 的持久状态为 ACTIVE，Story 等待 TASK 是预期设计。
- 同世界第二玩家已运行同故事；禁用当前包后，第二玩家实际击杀增加至 1，第一玩家 SETTLED/2 与另一零结算 ACTIVE/0 保持。新玩家阻断测试尚在继续。

汇总收据为 `evidence/p6-native-collective-choice-receipt.json`；原生图快照、绑定、死亡及 Task SavedData 见 `p6-ui-native-supplemental-*`、`p6-collective-*-confirmed-*`、`p6-two-player-*`、`p6-disabled-active-player-isolation-tasks.json`。早期一击后实体仍存活的截图、窗口失焦后未打开 Session 的尝试以及临时误指名测试玩家的操作不计 PASS；测试玩家已通过真实指名器解绑。过期网络报文的拒绝仍由当前 Session 自动 Probe 证明，未冒称实际发出了过期 C2S。

以上为先前检查点；后续结果如下。权威 dist 尚未提升，用户接受保持 false。

## 本轮补齐与准确边界

| 范围 | 实际结果 | 证据 |
|---|---|---|
| 非空 B 追加、一次 Undo／Redo | PASS：2→6 节点，Undo／Redo 原字节相等 | evidence/p6-native-append-undo-redo-receipt.json |
| 个体 Item、EXACT／FUZZY 组、跨槽收集与原子提交 | PASS：两槽不同 damage 合计 2；完成后移除不回退；不足／菜单／错误角色不扣；正确角色 3→1；XP 21→28 一次 | evidence/p6-native-inventory-receipt.json |
| 两成员候选浮层／原生 Tooltip | PASS；未冒称为多页原生测试 | evidence/p6-native-group-candidate-popover-two-members.png、p6-native-group-candidate-tooltip.png |
| 额外 Task metadata／damage 过滤 | 自动 PASS，原生样例 metadata 为空；基线与当前作者 UI 无对应编辑入口，原生验证未关闭 | evidence/p6-task-candidate-fix.log、p6-native-extra-filter-authoring-boundary.json |
| 双玩家实例与 Disabled 新旧运行 | PASS；禁用旧实例继续，新玩家 absent；启用相同玩家／宿主进入 Session | evidence/p6-two-player-disabled-receipt.json |
| 作者双入口、草稿／页键／组合／搜索／使用位置 | PASS；非空页 Backspace 不合并、中文粘贴不计 IME | evidence/p6-native-author-chain-receipt.json |
| 完整只读 Group 资源引用、公开边界、闭包导出、Import | PASS；资源引用不扩组，边界导出三成员，Reference 原字节，Import 新 UID | evidence/p6-native-reference-import-receipt.json、p6-reference-public-boundary-graph.json |
| 更新引用包后的可视端口通知 | 修复 PASS：先红 1，再绿 47，原生移除／新增立即刷新，不重开项目 | evidence/p6-reference-port-refresh-red.trx、p6-reference-port-refresh-green.trx、p6-reference-port-native-fix-receipt.json |
| CNPC 集成服务器最小启动／正常保存退出 | PASS；六故事 reload 包含原生三故事引用闭包；重开尝试未计 PASS | evidence/p6-cnpc-integrated-minimum-receipt.json |
| 标题／画面／头像表情／选择后 Task | PASS；后续原生演出包独立保留，未替代此前库存包证据 | evidence/p6-native-presentation-receipt.json |
| 设置／当前历史／改键打开关闭包管理器 | PASS；真实 H 打开及关闭，未重新弹出；隔离测试 OP 已撤销 | evidence/p6-native-runtime-windows-receipt.json |
| 声音 | 原生演出包三项媒体网络校验后，音乐／语音各提交一次播放命令；未实际听取，不计听音 PASS | evidence/p6-native-presentation-receipt.json、p6-audio-native-backend.log、p6-audio-trace-complete.log |
| 最新候选 Data 与产物来源 | PASS：124 文件更新前后不变；三 JAR 的 class／Java 源退休条目为零 | evidence/p6-candidate-data-before-port-publish.json、p6-candidate-data-after-port-publish.json、p6-final-source-provenance.json |
| 修改后 WPF 全量 | 最新完整复核 PASS：655 通过／1 跳过／0 失败；原 IO 失败项本次也 PASS。此前 654／1／1 及导航类 19 项复跑记录保留 | evidence/p6-wpf-clean-full-recheck-receipt.json、p6-wpf-clean-full-recheck.trx、p6-wpf-after-native-fixes.trx、p6-wpf-navigation-io-recheck.trx |
| 权威交付 | PASS：用户确认关闭后，原生核验 8348 为无窗口的 deleting 残留；更新到 0.4.0.0，410 个程序文件与候选相同；2,661 个 Data 文件更新前后路径／大小／SHA 完全一致 | evidence/p6-authority-promotion-receipt.json、p6-authority-binaries.json、p6-authority-data-before-promotion-final.json、p6-authority-data-after-promotion.json |

原始失败、失焦或坐标错误截图保留，文件名不能单独证明成功。本轮额外过滤原生验证及人工听音缺口保持明确未关闭，未创建成品包、ZIP、标签、远端 Release，未替用户确认接受。

权威开发交付已完成；此前“等待 8348 退出／尚未提升”的记录保留为历史检查点。没有结束用户进程，也没有把残留 PID 当作运行窗口。完整 WPF 复核已结束并通过，耗时 8m18s；已交付、自动验证和用户接受分别记录。

## IDEA 运行入口修复

2026-10-08 用户报告 `1. Run Client` 显示「找到无效的 Gradle JDK 配置」。此前 0.3.x 构建整理从仓库 `gradle.properties` 移除了本机 `org.gradle.java.home`，但 IDEA 仍选择 `#GRADLE_JAVA_HOME`，所用 Gradle 用户目录也没有提供该属性。这是本机配置迁移与 IDEA 验证的遗漏；命令行构建通过不能证明 IDEA 的运行入口正常。

已在 IDEA 实际使用的 Gradle 用户目录中恢复本机 `org.gradle.java.home`，未把私人 JDK 路径写回源码。通过现有 IDEA 的 `1. Run Client` 实际启动，Gradle 使用 JDK 25，Minecraft 使用 Java 8，原生窗口已到达 Minecraft 1.7.10 主菜单（18 个模组加载并活动）。未进入世界；此结果不替代额外物品过滤原生验证或人工听音。证据为 `evidence/idea-run-client-jdk-fix-receipt.json` 与 `evidence/idea-run-client-main-menu.png`。GitHub 上传因用户要求暂缓，尚未提交或推送。

## 2026-10-08 上传前六项修正（最终方案）

六项修正已实现并验证；详见 [六项结果](SixPreUploadFixes.md)。完整 Core 为 499 通过／11 跳过／0 失败，WPF 为 665 通过／1 跳过／0 失败，Runtime 构建和 8 个会话 Probe 通过。最新实机覆盖问题清空后再报告、三种菜单置底、故事 Flow／Logic 改名及保存重开、展开组整体排序、资源滚动排序和取消、资源拖入画布／属性框、四种本地引用入口，以及游戏 Choice 存档恢复后的台词／角色／头像和选择完成链。

外部引用四类型及确认时错类型拒绝、分页、冷客户端恢复、旧非空 prompt 忽略和正常保存／导出移除由自动回归覆盖，引用包与原候选字节比较相同。真实游戏发现的恢复上下文缺失已修复，原失败记录保留。新收据为 `evidence/six-fixes-delivery-validation-receipt.json`。

权威 Studio 已再次更新，Data 的 2,690 个文件／68,772,849 字节前后指纹相同，410 个程序文件匹配验证候选；Runtime JAR 与交付哈希见 `DELIVERY.json`。上传范围审计后恢复 `codex/0.4.0.0` 源码上传。原额外物品过滤原生作者链和人工听音缺口继续保留，用户接受仍为 false；没有创建成品 ZIP、标签或 GitHub Release。
