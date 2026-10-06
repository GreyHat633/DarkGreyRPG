# 本轮验收

证据仅采用本轮结果。技术扫尾完成；许可和未覆盖项单独记录。本轮为同机隔离，不是全新系统或 VM。局部 evidence 保留日志、TRX、截图、精确文件指纹和原始失败记录。

| ID | 场景 | 状态 | 证据/说明 |
|---|---|---|---|
| DOC-01 | 新读者只看 README | PASS | 当前版本、运行依赖、角色分工、入口、发布与候选区别已写清 |
| DOC-02 | 核对当前架构 | PASS | 对照身份、导出、公共输出、结算、来源、管理器与目录代码；CURRENT_ARCHITECTURE |
| DOC-03 | 核对包与管理流程 | PASS | 原版/CNPC/集成服务器使用 UI 导出；加载三条 Story、管理器 Enabled 两个容器 |
| DOC-04 | 文档链接与使用命令 | PASS | DocumentationLinks.json 无坏相对链接；状态、reload、指名、任务/故事调试命令实机执行 |
| BUILD-01 | 非E盘、带空格或中文路径 | PASS（限制已记录） | D:\DGR Closeout 扫尾\Source；中文 Gradle 缓存失败，同一独立缓存移到 ASCII 后保持完整检查通过 |
| BUILD-02 | 首次准备依赖 | PASS（来源有界） | 独立 Gradle/NuGet 缓存在线恢复；FFmpeg 固定 ZIP 在线下载；Git 精确 libs 校验。游戏公共文件按官方哈希重用/下载，不称全新 OS |
| BUILD-03 | 显式SDK覆盖与错误版本 | PASS | CleanPublish、PublishEnvironmentSdk、PublishPathAuthority；参数/环境/PATH 成功，缺失/9.x/x86 PE 预检拒绝 |
| BUILD-04 | 准备脚本重复、离线、失败 | PASS | ToolingFinal.log：8/8；有效文件哈希及时间不变，缺少/损坏输入不覆盖旧目录 |
| BUILD-05 | Java目标与成品运行 | PASS | class major 52；RuntimeJarAudit；实际 Forge 1614 两端加载正式 JAR；RuntimeCopies 五处哈希相同 |
| BUILD-06 | 测试实际执行范围 | PASS（详见下表） | Core 503；WPF 692+1 skip；六个当前 Probe；旧作者夹具两项未运行，不混入通过数 |
| DEP-01 | 不含CustomNPC+的基础组 | PASS | 两端仅 DGR；VanillaBoundWithSlowClick、BaseSessionPortrait、BaseCompletionFinalReceipt |
| DEP-02 | 含当前CustomNPC+的集成组 | PASS | 两端 fixed-v1+UniMixins 0.3.1；实际 Wand 创建、指名、交互；CnpcSessionPortrait、CnpcCompletion |
| DEP-03 | 依赖三处一致性 | PASS | README/入门、dependencies.gradle、@Mod/mcmod.info 的可选 after 声明与实际基础/集成行为一致 |
| LIC-01 | 项目自身许可 | OWNER_DEFERRED | 用户明确选择“暂不决定，保留待确认”；PROJECT_LICENSE_PENDING_OWNER，无自有代码许可推定 |
| LIC-02 | 第三方来源及随附范围 | VERIFIED_WITH_GAPS | THIRD_PARTY_NOTICES 与原始材料；CNPC 修订来源、FFmpeg 静态组合对应源码材料缺口分开列明 |
| LIC-03 | 打包材料 | VERIFIED_WITH_GAPS | Runtime 内 JLayer 原文/对应源码及第三方说明；Studio Docs 实际 SDK notices、FFmpeg 原文与锁定清单。无新成品分发合规宣称 |
| THEME-01 | 真实菜单Dark→Light→Dark | PASS／旧现象未复现 | 原权威、基线重建、新候选真实鼠标菜单均改变画面；未编造生产主题修复 |
| THEME-02 | System与重启 | PASS | MovedStartupSystem：System 保存重开恢复，OS 当前为浅色；没有改变 Windows 主题或注册表 |
| THEME-03 | 打开含未保存编辑的项目切主题 | PASS | ValidDirtyThemeReceipt 四次切换 dirty=true、磁盘未变；ThemeUndoValues 撤销/重做名称读回均 true；Ctrl+S 后磁盘改变 |
| THEME-04 | 两副本与写入失败 | PASS | SecondDefaultDark、TwoCopies；设置文件占用后明确失败且 SHA 不变，释放后恢复保存 |
| SMOKE-01 | 完整自包含候选目录启动 | PASS | 精确候选根 EXE，DOTNET_ROOT 指向不存在目录；实际加载模块均来自 Program；409 文件指纹与 dist 相同 |
| SMOKE-02 | 新建／保存／移动便携目录 | PASS | 默认 Project；明确外部目录真实创建/记录绝对路径；整体移动后内部相对项目退出重开恢复，外部保持原位 |
| SMOKE-03 | 实际Studio导出单包和组包 | PASS | 真实菜单生成两包、UI 编辑/撤销/重做/保存后再导出；manifest producer_version=0.3.3.7；组两成员/一连接 |
| SMOKE-04 | 专服＋匹配客户端实际故事链 | PASS | 基础与 CNPC 两组均 Story→Session→Task；TERMINATED/SETTLED，XP 从 0 到 7 |
| SMOKE-05 | 无内容变更reload、重连、正常重启 | PASS（基础组完整） | BaseReloadReceipt、BaseRestartReceipt：状态保留，XP=7，重复启动 routed=false；CNPC 另有无内容变更 reload |
| SMOKE-06 | OP／非OP管理入口 | PASS | CnpcOPManager 实际打开；CnpcPermission 记录移除唯一 OP 后 ops=[]，生存非 OP 不打开；服务端请求保留权限检查 |
| SMOKE-07 | 基础媒体与集成服务器 | PASS_WITH_AUDIO_LIMIT | 图片实际显示、媒体哈希及 Core 解码/转换通过；集成服务器会话/任务/保存退出，XP=7。未听感确认测试音 |
| DELIVERY-01 | 权威dist与Data保护 | PASS | 0.3.3.7 self-contained win-x64；Data 2661 个文件路径/大小/SHA 完全不变 |
| DELIVERY-02 | 程序与源码对应 | PASS | Delivery 指纹、Source.patch/SourceInputs；候选与 dist 全白名单409文件一致、实机功能DLL相同；五处Runtime JAR一致 |
| DELIVERY-03 | 扫尾交付时的授权与阶段状态 | PASS | 扫尾交付时未新建发布包/提交/推送/修改远端；既有 Release 五附件及本地旧成品哈希一致；未冒充用户验收。后续源码上传另有用户明确授权，见 Implementation 末尾及 Delivery.source_publication |

## 测试实际数量

| 执行项 | 本轮最终结果 | 证据 |
|---|---|---|
| Java build（完整现有检查） | 首次恢复后完整成功；最终离线成功，29/29 up-to-date | CleanJavaWithAsciiCache.log、CleanJavaFinal.log |
| 普通 Java Test | 没有发现普通单测，不计为六个 Probe | 构建 Test 报告与日志 |
| 六个当前 JavaExec Probe | 6 个实际执行，退出码 0 | ProbesFinalExactSource.log |
| Core MSTest | 503 通过，0 失败/跳过 | CoreComplete.log、TestsFinal/CoreComplete.trx |
| WPF MSTest | 692 通过，0 失败，1 跳过；共693 | WpfVerified.log、WpfVerified/WpfVerified.trx；8.6804 分钟 |
| 工具链准备回归 | 8 通过、0失败 | ToolingFinal.log |
| 旧 runtimeInteractionUi0336/tavernRepeat0336 Probe | NOT_RUN：缺少确切作者故事包 | 无替代夹具冒充通过 |

初期 Checkstyle 中文缓存失败、WPF 超时、无 Show 的 Popup 断言失败、错误测试夹具导出拒绝均是本轮诊断过程，不能计入成功。夹具重复端口/标签/组边界保存问题只修夹具。真实游戏输入的全角地址、聊天长度、原版 summon/TP 参数问题修正后重试，不归为 Runtime 成功或生产缺陷。

## 清理与剩余边界

用户授权后，5 个失败转储、6个重复发布暂存目录和2张失去前台焦点的无效截图移入回收站，共5,270,279,561字节；原位置已验证不存在，TRX/日志/有效截图保留。无效截图未用于验收。源码回归、Data、项目、既有发布和历史合同保留。直接删除/终止的组合命令先被自动审批阻止，未执行。

基础游戏窗口关闭后残留本轮隔离 JVM 未强制终止；两个专服已正常保存/停止，CNPC 客户端已退出。未运行用户真实服务器、未改用户世界。

许可由用户暂缓选择；CNPC fixed-v1 修订来源及 FFmpeg 对应源码材料缺口仍待补。音频听感、两项旧夹具 Probe、全新 OS/VM、JourneyMap 和长期负载均不属于本轮通过范围。
