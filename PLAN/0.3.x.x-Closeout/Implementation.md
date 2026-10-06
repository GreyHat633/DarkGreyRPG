# 0.3.x.x 扫尾施工记录

基线：`codex/0.3.3.7`，`462b1cd17566a1f42c76bfc94ee9e6042e8ae8d4`。候选沿用 0.3.3.7。

输入计划位于 `PLAN/DarkGreyRPG_0.3.x.x_Closeout_Construction_PLAN.md`。用户所列 E 盘根目录路径不存在，采用仓库中同名输入。开工时工作区仅有该未跟踪计划；保留原文。

范围：现行资料、构建可移植性、依赖与许可、主题菜单、同机隔离构建与真实最小链路。既有 `artifacts`、标签和 Release 不改动；扫尾阶段不生成对外成品、不推送。后续用户追加源码上传授权，见末尾记录。

## 开工事实

- Gradle wrapper 9.3.1，GTNH settings convention 2.0.20；`gradle.properties` 固定私人 JDK 路径。
- Studio 目标 `net10.0-windows`；现有 SDK 10.0.302 / runtime 10.0.10。发布脚本固定私人 SDK 路径。
- FFmpeg 9.0 full Gyan build；准备脚本目前直接覆盖后再验版本，需改为先验证再提升。
- CNPC 编译依赖注释称必需，生产桥接通过反射且入口声明 `after:customnpcs`；运行结论待本轮实机。
- 主题 VM 有命令与持久化测试；菜单使用 Window 祖先绑定。根因尚待实际菜单证明。
- 未发现根项目许可证；已向版权方集中询问一次，工程继续。
- 同机非 E 盘隔离将使用 D 盘带空格/中文路径；C 盘剩余空间不足。它不是全新系统或 VM。

本轮证据统一存于局部 `evidence/`，不沿用历史 PASS。

## 本轮结果

技术扫尾完成，版本保持 0.3.3.7；许可与听感验证单独列明。扫尾交付时没有创建发布包、提交、推送或修改既有 Release。当前输入计划原文保留。

- README 改为当前 Story／Session／Task 使用入口；新增 CURRENT_ARCHITECTURE、BUILDING、GETTING_STARTED。九份相关旧文档仅加历史说明，没有历史清仓或恢复旧协议。
- 去掉 gradle.properties 的两个私人 JDK 路径。Gradle wrapper／插件／目标版本／检查任务保持不变。
- Studio 发布入口支持参数、DGR_DOTNET_PATH、PATH，验证稳定 .NET 10 与 Windows x64 PE apphost；HostModel、模板和声明都来自实际选中 SDK。三个发现入口均实际发布成功。
- 固定 FFmpeg 9.0 来源与归档／四文件哈希；支持显式媒体目录、准备、离线重用、失败不覆盖有效副本。新增 Java 依赖精确校验入口，不以任意上游 CNPC 替代 fixed-v1。
- THIRD_PARTY_NOTICES、锁定材料与运行库声明纳入便携 Docs；Runtime 带第三方说明及原有 JLayer LGPL 原文、对应源码。CNPC／UniMixins 原始授权材料留存 third_party/licenses。
- 真实菜单在原权威程序、基线重建及新候选均正常切换，未复现旧记录的主题问题。因此没有编造主题生产修复；补了实际 Popup 命令绑定与保存回归。未保存编辑经过四次主题切换仍保留，真实撤销／重做与保存成功。
- 实际导出发现 producer_version 仍写死 0.3.3.6；改为读取 StudioBuildInfo 的程序集版本。本轮单包和组成员均读回 0.3.3.7，新增版本断言。

## 隔离构建与验证边界

源码从基线 Git 内容及本轮差异放到 `D:\DGR Closeout 扫尾\Source`，没有复制作者 bin／obj／完整 .tooling。使用独立 Gradle、NuGet 和 CLI home 恢复依赖。现有安装 SDK／JDK 是显式工具输入；Git 跟踪的精确 libs 是源码依赖输入。

本轮命令宿主为 PowerShell 7.6.5；构建手册明确要求 PowerShell 7，区分 Windows 自带的 5.1。Git 跟踪的少量历史 .tooling 夹具随基线保留，但没有用它们作为缓存或本轮 PASS 证据。

首次 Gradle 恢复、Minecraft 重编译及工具链下载实际执行。中文 Gradle 缓存路径导致 Checkstyle Worker response-file 主类解析失败；直接 classpath 能加载。移动同一个独立缓存至 `D:\DgrCloseoutCache\Gradle` 后，中文／空格源码路径保持不变，完整 build 成功，保留检查。最终离线 build 复用本轮独立缓存，29 个任务 up-to-date，不能称为第二次首次恢复。

Core 503/503；WPF 692 通过、1 项未启用的可选性能测试跳过；8 项工具链准备回归和六个当前 JavaExec Probe 通过。早期 WPF 超时不是 PASS：渲染用例有限时间完成后，Popup 测试使用独立 STA 宿主避免已有 Application/dispatcher 互相干扰，保留全部断言，最终完整套件成功。两项依赖旧作者包的历史 Probe 未运行。

游戏安装用官方 Minecraft／Forge 元数据和文件哈希核对；部分公共游戏库／资源从既有安装按官方 SHA-1 精确重用，其余联网准备。没有复制私人存档或配置。686 个资源对象实际核对。这是同机隔离环境，不能称为全新 OS／VM 或断网首次安装。

## 真实故事闭环

CurrentIdentityFixture 的 closeout 模式仅创建最小创作项目；`.dgrs`／`.dgrs.g` 由真实 Studio 项目菜单导出，不用工具构造包代替 UI 导出。最小内容包括两成员关联组、一条单故事、角色、头像、两秒测试音、会话选择、位置任务和一次固定 7 XP 奖励。

基础客户端／专服仅装 DGR，不装 CNPC／UniMixins；原版村民经指名器实际绑定、右键启动 Story→Session→Task，目标完成后 Task=SETTLED、Story=TERMINATED、XP=7。无内容变更 reload、断开重连、正常 save-all/stop/restart 后仍为这些结果，重复启动 routed=false。CNPC 集成两端均加精确 fixed-v1 与 UniMixins 0.3.1，通过 CNPC 自己的 Wand 创建实体、DGR 指名器绑定及实际交互，同一任务链结算为 7 XP。空的 ops.json 和生存模式下 O 不打开管理器，OP 可打开／关闭。

单故事先导出为 SHA-256 `538e43d4d3e8195d9b70df4885443fe55b1e19a5b7b776702e93b2279e96639a`，基础／CNPC 专服使用此确切副本；真实 UI 将终止显示名改为“扫尾完成”、切主题、撤销／重做并保存后再次导出，哈希为 `a0724dd310341641d8d6f33e4b3e596aac4d512cfc6d008e2666a93269af264f`。集成服务器使用后一个确切 UI 导出，加载、原版实体交互、会话、任务、保存退出成功，玩家 XP=7。组包两成员、一条 Logic 连接，哈希 `9c2033ccf1c00e6f282aae2a36300950e1119a322d7f130a260aa8b394a74269`；两端均准入三条故事。

媒体头像在基础、CNPC、集成会话实际显示；缓存 PNG／OGG 与导出内容哈希一致，Core 的媒体转换与解码测试通过。没有监听确认测试音的听感，不能将“有 OGG 文件”称为音频实际可听。CNPC 原有的两项缺纹理日志单独保留，未造成上述链路中断。没有 JourneyMap 冲突／长期负载测试。

## 便携与交付

默认新项目在 Data/Projects；普通外部测试项目导入本地。真实新建明确外部项目后记录绝对路径，移动完整 Studio 至 `Studio Moved` 后外部项目仍从原位置打开；内部 Smoke Final 记录相对路径、退出重开恢复三条故事与编辑后的名称。新副本默认 Dark，两份副本独立保存 Light／Dark。锁住设置文件模拟写入失败，画面显示失败信息、文件 SHA 不变；释放锁后保存恢复。

最终 SDK 环境变量发布到 `Studio Verified`，PATH 发布到权威 dist；409 个白名单程序文件逐一哈希完全相同，实测 `Studio Moved` 的 Studio/Core DLL 也与最终发布一致。权威 Data 的 2661 个文件逐一比较路径、大小、SHA-256，全部保持不变。EXE、功能 DLL、Core、清单和 Runtime JAR 的精确指纹在 Delivery.json。

工作区另一次 build 的 JAR 与隔离实测 JAR 只在四个文本资源的换行上不同，逐项 ZIP 内容比对没有 class 差异（WorkingTreeJarDifference）。最终将隔离实测的确切 JAR 提升到工作区 `build/libs/darkgrey_rpg-0.3.3.7.jar`，便于用户取得同一已测副本；不覆盖 artifacts 旧成品。

既有 `v0.3.3.7` Release 的 ID、标签目标、发布时间、五个附件 ID／大小／digest／更新时间读回一致；artifacts 中原 JAR 和 ZIP 哈希保持不变。

按用户随后明确授权，5 个已失效测试转储、6 个重复发布暂存目录和2张失去前台焦点的无效截图移入 Windows 回收站，共 5,270,279,561 字节。无效截图没有用于通过声明，采集辅助脚本增加前台校验。保留回归测试、TRX、日志、有效截图、源码、Data、项目与旧发布。此前直接清理命令被自动审批以“策略阻止”拒绝，未冒充删除成功；回收站路径已核验原位置不存在。基础客户端游戏窗口已关闭，但其隔离 JVM 残留进程未强制终止；两个专服正常保存并停服，CNPC 客户端正常退出。

## 单独保留的待确认项

- PROJECT_LICENSE_PENDING_OWNER：已说明 MIT/GPL 区别；版权方明确选择“暂不决定，保留待确认”，不生成项目根 LICENSE。
- PATCHED_BINARY_SOURCE_PENDING：CNPC fixed-v1 精确修订来源未查明；不将最新版上游材料冒充对应源码。
- SOURCE_MATERIALS_PENDING：FFmpeg GPL 静态组合的完整外部库对应源码／分发材料未核清；不宣称公开再分发材料全部齐全。
- 音频听感、两项旧作者夹具 Probe、独立新 OS／VM 不在本轮通过声明内。

## 后续源码上传

2026-10-07，用户在确认成品无需重新分发后明确要求“上传github”。该指令授权将本次源码、文档、回归测试及第三方原始材料提交并推送至既有 `codex/0.3.3.7` 分支；不修改标签、Release 或既有成品。

上传前逐一核对 Delivery 中的 50 个源码与材料输入，路径、大小和 SHA-256 均与扫尾验收时一致。上传不引入新的生产代码变更，沿用本轮已执行的构建与测试；提交不包含 dist、artifacts、Data、缓存或局部 evidence。推送结果记录在 Delivery.json 的 source_publication 字段。

源码提交 `2dedd6994b6327fd4887a9e45d926d28c7e04808` 已推送，远端分支指向该提交。`v0.3.3.7` 标签仍指向基线，Release 的五个附件及本地原 JAR／ZIP 指纹保持不变。随后仅补交本节、验收表和 Delivery 的上传结果，不改变已测源码。
