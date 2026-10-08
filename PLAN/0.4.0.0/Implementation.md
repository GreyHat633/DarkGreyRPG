# 0.4.0.0 施工台账

状态：P1—P5 与原生缺陷修复已实施，权威开发 dist 已更新；P6 完整 WPF 复核已通过，两项原生验收缺口仍未关闭。当前状态以本文末尾、Acceptance.md 和 DELIVERY.json 为准。

## P0 基线

- HEAD 与 PLAN 固定 SHA 相同，原分支 `codex/0.3.3.7`；新建 `codex/0.4.0.0`。
- 开工前只有用户提供的 `PLAN/DarkGreyRPG_0.4.0.0_Construction_PLAN.md` 未跟踪，保持原文件；施工规格副本归本目录 `PLAN.md`。
- 已读取根 AGENTS、BUILDING、CURRENT_ARCHITECTURE、0337 Corrections、0336 RuntimeDirectoryGraphRepeatAcceptance、dgr-release-packaging skill。只授权本地开发及权威 dist 更新，不创建成品包或远端发布。
- 本机 PATH 是 .NET 8 / 游戏 Java 8；实际核实并使用 `E:/Java/dotnet-sdk-10` SDK 10.0.302、runtime 10.0.10、Gradle 启动 JDK 25.0.1。Java 固定依赖校验通过；不升级依赖。

## 首批追踪清单

| CL | 文件／符号 | 旧职责／当前替代 | 调用证据及处理 | 状态 |
|---|---|---|---|---|
| CL-05 | 两端 GraphResourceAddressCodec.Target/target | 编码/读取 registry_name Task 目标；改用 typed Address/address | Envelope 与 Runtime GraphResourceLoader 直接调用；删除目标专属原生分支 | 已修改，自动用例通过 |
| CL-05 | CurrentProjectValidator.Require / CanonicalProjectContentLoader.nativeTarget | 原生目标免声明校验；当前 membership 声明校验 | 项目内容事务、文件项目、包加载共同校验；移除豁免 | 已修改，闭包联调待补 |
| CL-05 | CanonicalTaskObjectiveSchema / CanonicalTaskRuntime.requireObjectiveTarget | 非空文本可冒充目标；改为 Kind 校验 | 作者内存编辑及 Runtime 直接构造入口；保留空草稿与 dormant | 已修改，自动用例通过 |
| CL-05 | GraphEditSession.ChangeObjectiveTarget / ResourceCopyRemapper.Rewrite | 属性修改或内存复制可跳过目标校验；复用 typed 校验 | 参数修改先验证候选，不改图或 Undo；复制先验证 detached graph | 已修改，自动用例通过 |
| CL-05 | DgrsPackageValidator.AllowDormantObjectiveIssues | dormant 的空字段例外可能覆盖错误非空字段 | 仅豁免真正的空字段；非空错误仍阻断 | 已修改，Core 回归通过 |
| CL-05 | CanonicalTaskForgeEventNormalizer | registry Kill、拾取事件与无调用别名；具体 Actor 的去重 Kill | 生产仅 EventAdapter.killEvents/creditedKiller 使用；旧 collect helpers 无生产订阅，实际收集在 synchronizeObjectives | 无调用辅助已删；现役归因保留 |
| CL-05 | CanonicalTaskInventory.matches | registry 直接相等；改为 Item/Group 绑定 | 背包计数、提交扣物与候选展示共用；保留底层 registry/damage/NBT | 已修改；库存、数量及原子扣除 Probe 通过 |
| CL-10 | CanonicalStoryActionConfiguration / CanonicalStoryForgeActionExecutor | 旧 item+metadata 解析/执行；当前 Item 世界绑定 | Action runtime 与 executor 调用；删除 legacyRegistryItem/getMetadata/giveLegacyItem | 旧分支已删；解码和内存拒绝通过 |

应用源码版本已设为 0.4.0.0；两端 graph schema 统一为 3。P5 已同步将单包 Manifest schema/format 和组包 format 升到 3；旧 dialogues/quests 字段退出，Studio 与 Java 对同一批 21 个退休输入的拒绝已通过。版本仍处于 P6 验收与交付阶段。

## 下一批已查到的生产入口

| CL | 当前证据 | 后续处理／状态 |
|---|---|---|
| CL-01—03 | DarkGreyRpg 正式构造 Canonical 三个 manager；CommandDarkGreyRpg 与 ProjectSnapshot/Repository 仍保留旧业务参数/集合 | P3 已完成现役消费者脱钩、旧定义/执行/SavedData 删除及实际 JAR 类扫描 |
| CL-04 | CanonicalJournalService.getJournal/openJournal 仍调用旧 adapter、QuestJournalEntry、S2CQuestJournal；DialogueNetwork 的 3/4 仍注册旧 Journal | P3 已改接当前 Task 展示、运输及窗口，旧 3/4 留空 |
| CL-06 | NominatorSavedData.typeGroups/getTypeGroups/readFromNBT/writeToNBT；EntityDgrIdentityResolver.resolve 仍按 entityType 追加组 | P2 已删除类型规则；schema4 失败隔离与实际 MapStorage 无写回通过 |
| CL-07 | NominatorNetwork 注册旧实体写请求 8、旧物品写请求 9；统一操作 19/20 已存在；Open 10—13 仍需保留 | P2 已删除旧 mutator，8/9 留空；现行 19/20 实际操作断言通过 |
| CL-08 | ShellViewModel 的 _currentDialogue/_currentQuest、打开和 draft 分支；CanonicalStoryLifecycleService._legacyStories 参与删除阻断检查 | P4 已保留当前删除/引用/草稿职责并脱钩；旧 Shell、DTO、Reader、View 退出 |
| CL-09 | SessionChoiceSchema.ValidateLegacyLogicPorts 仍允许已选择 Logic 输出；当前条件输入及 Flow 必须保持 | P4 已定向删除旧输出；条件输入/Flow/选择历史保持 |
| CL-11 | 两端 RequiredResources.Dialogues/Quests 及路径枚举仍存在 | P5 已双端更新 single/group 合同并严格拒绝旧字段 |

特别记录：Minecraft MapStorage.loadData 捕获 readFromNBT 异常后可能仍缓存并返回已构造的半空 SavedData；仅在 reader 抛异常不足以保证原文件不被后续 dirty 写回。P2 必须验证失败实例隔离及 get/storage 入口保护，不能将空对象作为成功加载结果。证据是本机 Gradle 解包的 Minecraft MapStorage.java 和 NominatorSavedData.get(MapStorage)。

## P1 时点的历史边界

P1 的生产修改与自动验证正在收口，CL-05/10 尚未计作整个版本验收闭合。P2—P6、原生 Windows/Minecraft 实机和同包实机 E2E 尚未执行。按 PLAN 11.1，中间不兼容构建只在隔离开发目录使用，未推广到权威 dist。

CL-01—04、06—09、11 尚未关闭；AUDIT_ONLY 未判定项保留。

## P2 清扫结果与 P3 入口台账

| CL | 准确文件/符号 | 旧职责/当前替代/证据 | 处理与验证 |
|---|---|---|---|
| CL-06 | NominatorSavedData.typeGroups/get/add/remove/read/write | 类型统一归组；当前具体 UUID 的 NominatorEntityBinding | REMOVE；根仅 schema4 当前字段，失败隔离；实际 MapStorage 7 负向无写回与新绑定保存/重载 PASS |
| CL-06 | EntityDgrIdentityResolver.resolve(Entity,String,...), NominatorService.bindEntityTypeGroup/safeType | 类型查询与规则写入；当前 3 参数 Resolver 和 bindEntity | REMOVE；实际僵尸 A/B、骷髅 C、优先级/多组/去重/CNPC PASS；entityType 诊断保留 |
| CL-06 | S2CNominatorEntityOpen.typeGroups 及 Proxy/GUI 构造参数 | 类型规则响应；其余 Open 快照仍是当前读取 | REMOVE 字段、marker 断代；现役快照 codec 与拒旧 PASS；未改 GUI 布局 |
| CL-07 | C2SNominatorEntityBind / C2SNominatorInventoryBind | 已被 C2SNominatorAction 替代的写入口，注册 8/9；生产 GUI 已用 19/20 | 类与 handler/注册 REMOVE；权限、距离、revision、具体资源闭包与返物在实际当前 execute 入口 PASS |
| CL-07 | CommandDarkGreyRpg.debug clear_type_group | 类型规则命令写入口，无当前职责 | REMOVE；其他调试服务保留 |
| CL-04 | CanonicalJournalService.getJournal/openJournal | Canonical Task 经 LegacyAdapter→Quest DTO→S2CQuestJournal | DETACH_THEN_REMOVE；改接当前 CanonicalTaskJournalEntry 与已有 CanonicalTaskPresentationServer/GuiCanonicalTaskScreen；只保留编排职责 |
| CL-04 | DialogueNetwork 3/4、QuestClientController、GuiQuestJournal | 旧 Journal 运输/菜单；快捷键本已开当前 GuiCanonicalTaskScreen | 改接命令后 REMOVE，3/4 留空；增加窄当前菜单打开消息，不复制任务投影或页面请求体系 |
| CL-03 | CommandDarkGreyRpg legacy constructor/processQuest/processDialogue/补全 | 旧子命令与 null 旧 manager 参数；正式 startup 使用三 Canonical manager | 改接所有当前调用者后 REMOVE；保留当前命令能力与权限 |

P2 结束时自动闭合；该时点 P3—P6 尚待执行。后续进展以文末最新记录为准。所有隔离测试文件保持在 `.tooling`；没有成品发布或远端操作。


P3 删除前逐类清单见 `evidence/p3-runtime-entry-audit.json` 的 53 个旧业务定义/执行器/旧 SavedData 与 10 个专属外围类，均登记准确路径。外部依赖逐条改接：ProjectSnapshot/Repository 去旧集合与无效解析入口；单包 reader 与 merger 只建立现行快照；Nominator 列表保留当前 Canonical Story；Live Bridge 移除无正式构造的旧 play-test 分支，仅保留既有 Canonical 快照、reload、pick、locate。当前字体/历史/设置/阅读容量、CNPC、底层物品与媒体路径不在删除清单。

`ProjectRepositoryProbe` 的旧格式正向部分不保留；当前 reload 原子提交、Actor 字段拒绝、revision、只读 Live 快照与坏输入原文件保护迁移到现行文件入口。`Studio21VerticalSliceProbe` 只验证退休 StoryNode/DialogueExitBranch 执行，随职责退出；现役 Session completion router/Story server/Task projector 回归继续保留。旧 Journal adapter/merge/定长 Quest transport 测试退出；现役目标过滤、身份、作者描述/动态正文和正常只读投影改接当前 Journal/UiProjection/transport。


## P3 自动结果与 P4 进行状态

CL-01—04 的现役调用已脱钩，63 个准确登记的退休 Runtime 源文件已删除。`p3-p4-current-combination.log` 中 build 与 13 个 P3/当前身份/指名回归全部通过；同批新增 P4 Session Runtime Probe 的旧夹具错误独立修正，未据此宣称 P4 完成。任务日志夹具的目标、公开端口、settlement 与持久身份改为当前合同，保留过滤、历史、只读投影及奖励断言。当前生产 JAR 的 retired class 检查为零，见 `p3-built-jar-retired-class-audit.json`。

P4 已去掉 Choice 输出校验兼容、编辑重建、复制标签、节点兼容输出 UI 和 Runtime 求值；合法无条件初始化 helper 改名为 InitializeWithoutConditions，三字段形态与条件默认保留。核心 21、WPF 51 项 PASS；引用确认测试改为当前 Flow 输出。Story/Actor lifecycle 已脱离 StoryRepository，使用当前 membership；退休根目录只检测 JSON 是否存在，不解析业务，不迁移/删除。其自动回归 30 项 PASS，旧 ownership mirror 测试改为拒绝且原文件不变。Shell/ProjectService/主页和专属类的 CL-08 仍在清理。


## P4/P5 自动与原生 UI 核验进展

当前 Core 全量 498 通过、11 跳过；WPF 全量 653 通过、1 跳过。测试退役、混合夹具迁移及替代关系见 evidence/p4-test-retirement.json，实际跳过不计通过。原始失败和修正后结果分别保存。

P5 单包 Manifest schema/format 与组包 format 均为 3。旧字段的 DTO、路径遍历、冲突诊断、导出空根目录退出。两端严格拒绝旧空/非空字段和旧版本；C# 补齐必需元字段、嵌套重复字段检查与包错误封装。相同 Studio 导出经六个原生目标和十五个 Manifest 变体验证，不回写原包、不部分注册。合法包的媒体、成员闭包和组原子准入继续保留。

Session 当前夹具保留选择历史、公开 Logic、页游标、存档恢复、服务端 fencing、客户端和 Forge 路由断言；旧 sessions-only 存档 wrapper 转为负向样本，读取拒绝保留已有有效状态。包加载的旧 Phase 3 全集合回滚断言更新为当前容器 Error/Conflict 隔离行为；不恢复旧 reader。

原生 UI 使用 Windows UI Automation / Win32，在 .tooling/0400-studio-candidate 的独立 Data 中实际创建 Story/Session，Enter 提交，搜索 3→1→3，创建 Choice 并启用条件、保存并读回 Flow 输出与 Logic 输入。没有编辑技术身份，未触碰正式用户项目。

以上为此前 P6 检查点；当前候选不等同已交付。

## P6：2026-10-08 当前检查点

后续原生补充样例：资源列表 Enter 已以 PreviewKeyDown 修复，新增真实模板键盘回归先红后绿（32 项），重建候选后通过真实按键验证。Studio 界面完成集体角色、三选项 Choice、Story／Task 公共 Logic 边界、两个有顺序的结算与零结算任务，保存、验证、导出同包；原版专服已确认跨物种 0→1→2、条件刷新不改变 line_epoch／presentation、顺序 0 结算与零结算 ACTIVE。后续进度与最终边界以下文更新及 Acceptance 为准。

- Core 最终全量：498 通过、11 跳过、0 失败；WPF 当时最终全量：653 通过、1 跳过、0 失败。准确日志为 `p6-core-confirmed-final`、`p6-wpf-final`。后续资源 Enter 修复另记针对回归，不能借用这次全量作为修改后的结果。
- Runtime 正式 JAR 的退休类检查为零，精确依赖与 AUDIT_ONLY 的 KEEP／REMOVE_DEAD_BRANCH 结论分别见 `p6-retired-dependency-audit.json`、`p6-audit-only-decisions.json`。
- 实机发现 TaskCandidateIndex 对个体 Item 调用 Group 查询，并残留 registry fallback。按 Address Kind 分派、删除旁路；当前候选 Probe 覆盖个体、空 Group、dormant、非法 Kind、revision 和 121 项分页。build、候选、Forge Task 与 Journal 回归通过。
- 同一份原生 Studio 导出的单包与组包，在隔离原版单人、原版专服和 CNPC 专服完成角色交互／会话／选择／收集／提交／击杀／7 XP 基础链。单人重进和 CNPC 正常停服重启保留结算前进度；无内容修改 reload 后 XP 仍为 7。专服包禁用／启用、冲突隔离及未冲突组运行有单独证据。
- CNPC RCON 召唤失败与缺省召唤的显示缺失仅为失败尝试；正式正向用模组原生 NPC Wand 创建。CNPC 未指名实体的真实死亡计数为 0，已指名实体真实死亡完成目标；角色转移确认与取消保护原宿主。
- 补充 Story、Session、两个 Task、个体／集体 Actor 和 Item 已经由原生 Studio 创建。资源 Enter 的修复、回归和候选原生复核通过。
- 集体角色跨物种、条件选择、多／零结算、双玩家及作者追加／外部来源链的后续结果见下文。音频日志只证明播放源状态，不计为听音通过。
- 最新自包含候选已用正式部署脚本更新，候选 Data 哈希完全一致。权威 dist 仍未提升，8348 为既有权威 Studio 进程；未关闭用户进程、未创建成品包或远端发布。

## P6：2026-10-08 本轮原生复核与交付准备

在真实 Studio 追加 A 内容到原有两个节点的非空 B，得到六个节点；一次 Undo 精确恢复原文件，一次 Redo 精确恢复追加结果。随后通过原生界面配置 B 的个体物品、EXACT/FUZZY 组、收集与提交目标。真实指名器配置当前世界绑定，实机确认跨槽合计 2、移除后完成不回退、不足提交不扣物、菜单查看和错误角色不提交、正确宿主只扣所需 2、结算 XP 21→28，后续 reload／再次到达均不重复奖励。详见 `p6-native-inventory-receipt.json` 与独立的原始 SavedData。

双玩家 Disabled 样例补齐：旧实例在禁用期间继续击杀，另一玩家完成实例及零结算任务不变；新玩家同一宿主在禁用时 absent，启用后实际进入 Session。原生候选浮层显示 Coal／Charcoal 两个成员和 Tooltip；121 项跨页及额外 damage／metadata 过滤仍分别由当前自动 Probe 证明，未伪称为两成员原生浮层的跨页或附加过滤结果。

作者链补齐草稿切换、Enter 增页、清空后 Backspace 删除空页、按钮组合和 G 组合、搜索、角色三处使用位置。非空页 Backspace 不合并是当前合同，失败尝试不计 PASS；中文粘贴也不计为实际 IME 组合。

独立 External0400 项目加载完整只读 Group，资源引用不产生本地项目连线或扩组；当前公开边界连接后，原生导出包含本地故事及两个来源成员的三故事闭包。Reference 原包字节／UID 保留，Import 分配两个新 Story UID。实机发现引用包更新后可视端口未刷新，加入真实 CanonicalGraphEditorView 回归先红 1，再绿 47，修复 Project 端口通知并重新发布候选；最新候选原生复核确认更新移除／新增端口均立即显示，无项目重开。

CNPC 新隔离单人世界实际启动、reload 六故事（含三故事引用闭包），正常 Save and Quit 保存停服；早期未成功进入重开世界的坐标尝试保留为 NOT_PROVEN，不计通过。原版专服用后续原生演出包实际显示标题、画面层和新表情头像，选择后进入 Task。设置、历史、包管理器改为 H 键打开／关闭均有原生截图；测试玩家临时 OP 已撤销。未修改独立 Gramophone channel 或当前快捷键 handler，该条件性回归未触发。

本轮候选为 Windows x64 自包含 Release 0.4.0.0；`p6-studio-port-candidate-binaries.json` 包含 EXE、主 DLL、Core DLL、程序白名单来源。该次部署前后候选 Data 的 124 个文件路径／大小／SHA 完全一致。最终 JAR/source JAR/dev JAR 的 class 与 `.java` 退休条目均为零，来源清单为 `p6-final-source-provenance.json`。

权威 Data 已读取 2,661 个文件、64,212,068 字节的更新前指纹。8348 的 CIM／Win32 状态存在不一致，退出时间仍为空，未杀进程或替换权威文件；用户选择自行保存并关闭，仍等待实际退出，结束进程未获授权。修改后完整 WPF 为 654 通过、1 跳过、1 次 AtomicFileWriter 文件替换失败；失败项所在导航类针对复跑 19 项全部通过。全量不是零失败，原失败及复跑日志均保留。

另外以当前 Runtime JAR 启动隔离 Client Audio／DGR0403，只启用已有 `dgr.mediaTrace` 开发日志，真实宿主交互进入同一份后续演出包。三项媒体经网络下载和哈希校验，音乐／语音各提交一次播放命令，日志为两条 `audio_prepare / play_commands_queued`。实际画面及表情同时显示，最后通过原生 Disconnect／Quit 正常退出该新客户端。该证据证明后端命令已排队；仍没有实际听音确认。额外 Task metadata 过滤原生验证和权威交付仍未关闭。

## P6：2026-10-08 权威开发交付

用户明确确认已关闭全部 Studio 后，重新读取原生扩展进程状态：8348 无窗口、退出码为 0、IsProcessDeleting 为 true。此前仅凭 CIM 残留记录和未触发的等待信号认定应用仍运行，判断过严；该残留已是应用关闭后的内核清理状态。未调用任何进程结束 API。审计为 `p6-studio-closed-native-process-audit.json`，更新前证明为 `p6-authority-exit-before-promotion.json`。

随后通过现有 `studio/package-studio.ps1` 使用 SDK 10.0.302，将当前 Windows x64 自包含 Release 更新到权威 `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`。EXE ProductVersion 为 0.4.0.0，204,288 字节，SHA-256 为 `F68E1D6FAB7A4717089B394C9842BEDCFD7EAEC93382E76D86E0E5725E9001B3`；主 DLL 为 `BC91F1AE8A4733808CB9FA6F948FB5A14500F3E3B3BA0682D45BE8586BE508BE`。全部 410 个程序文件逐项匹配此前已验证候选，根文件仅 apphost，Program／Tools／Docs／Data 分类完整。

即时更新前和更新后，Data 的 2,661 个文件、64,212,068 字节及所有相对路径／大小／SHA-256 完全一致。没有启动权威副本编辑用户项目。正式 Runtime JAR 为 0.4.0.0、1,836,489 字节，SHA-256 为 `9543CC1F67063A5FE9ADB1633641B84B7CBE868B6A59ED3D505BCEB5194FCCF0`；七份原生导出验收包的格式／ZIP 完整性及各自哈希已写入 `p6-delivery-artifacts-and-test-packages.json`。详细交付证明为 `p6-authority-promotion-receipt.json`。

该记录只确认开发 dist 已交付，不替代完整版本验收。为核查此前唯一 IO 替换失败，当前正在以相同已编译测试程序集复跑完整 WPF；此前全量失败与 19 项针对复跑仍保留。额外过滤的原生作者链没有现成编辑入口，基线与当前源证据为 `p6-native-extra-filter-authoring-boundary.json`；人工听音也仍未确认。未创建成品 ZIP／artifacts、提交、推送、标签或远端 Release，用户接受保持 false。

完整 WPF 复核随后结束：655 通过、1 跳过、0 失败，耗时 8m18s；先前 IO 失败项 `ShellOpensProjectHomeThenCurrentStoryInspector` 本次完整运行也通过。最新日志／TRX 为 `p6-wpf-clean-full-recheck`，解析收据为 `p6-wpf-clean-full-recheck-receipt.json`。没有修改 AtomicFileWriter 或把先前失败删除；源指纹与正式交付 DLL 均未因复核改变。当前 Core 为 498 通过、11 跳过、0 失败，完整 WPF 与 Runtime 当前合同自动验证均通过。代码施工标记 IMPLEMENTED，权威开发交付标记 DEVELOPMENT_DIST_DELIVERED，原生附加过滤和人工听音仍单列未关闭。

## 2026-10-08 上传前六项修正（最终方案）

已按用户最终六项方案删除选择提示、增加清空问题、固定结束菜单、开放本地故事出口改名、统一左侧让位拖动和筛选引用类型。实现及验证范围见 [SixPreUploadFixes.md](SixPreUploadFixes.md)。真实游戏冷恢复漏失台词上下文的问题已修复，以既有静默 LINE 帧先于 CHOICE 帧发送，Choice 文本为空、网络结构不变，最后显示页及动态文本快照由回归覆盖。

最终 Core 499／11／0、WPF 665／1／0，Runtime 构建和 8 Probe 通过。最新自包含 Release 已提升到权威 dist，410 程序文件匹配候选，2,690 Data 文件更新前后全部指纹相同。生产来源清单 689 文件的 SHA-256 为 `a1f4cd003dac37e4202f3fadeea45814907e09fcb06df107e93e7d904efd3312`。原 PLAN 及冻结副本字节保持。后续源码分支上传含此六项与验证摘要，额外物品过滤原生作者链和人工听音仍单列未关闭。
