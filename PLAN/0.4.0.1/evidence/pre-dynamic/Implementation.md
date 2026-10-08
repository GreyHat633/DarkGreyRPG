# 0.4.0.1 实施记录

基线：`codex/0.4.0.0 @ be0b180008fe58f2f1a6d1724a5a06d9884e4be8`。
施工分支：`codex/0.4.0.1`。开工时仅施工 PLAN 未跟踪，无生产差异。

## 容量合同

计量单位为世界中不同实体 UUID 的记录数；独立常量
`NominatorSavedData.MAX_ENTITY_BINDINGS = 4096`，不改变目录／网络的 512 条上限。
每条最多 32 个集体角色、一个个体角色；当前地址限定 ASCII local ID 63 字符和固定 Story UID。
4096 条最大字段记录实测未压缩 NBT 为 17,649,756 字节，gzip 为 120,544 字节。
当前字段已受上述长度和组数限制；本轮将 32 MiB 作为工程测量参考，并采用 4096 条的保守规模上限，
没有另加字节准入机制。这不是 Minecraft 强制的实体数量或文件大小上限，也不是必须永久固定的产品规则。
2026-10-08 交付后复核实际 Forge 字节码：MapStorage 调用 readCompressed，该方法使用
NBTSizeTracker.field_152451_a，其匿名实现不执行大小计数限制。此前将 32 MiB 称为 Minecraft 读取预算
不准确，现予以更正；最大字段实测与真实 MapStorage 往返结果不变。原始字节码证据保存在本地隔离日志，
摘要见 evidence/storage-budget-correction.json。
未来可以采用动态规模／实际字节预算或分片存储，仍须保证读写合同一致、合法旧数据可读取，
以及容量拒绝早于任何相关身份表修改。当前程序仍使用 4096，未在本次问答中调整。

服务在两张身份表的既有锁内，计算目标新增／移除与转移源是否消失的最终净变化，
预检后再修改。旧宿主残留集体角色时不减记录数。已有 UUID 更新、无操作和解绑保持可用。
下层 put 与 reader、writer 使用同一独立上限；writer 在修改输出前检查。
容量拒绝使用现有响应的 `binding_capacity_reached`。不新建协议或控制台。

## 读取失败合同

无文件：无参数构造新建可用对象；MapStorage 的 String 反射构造为待读取、不可用。
完整当前 schema 7 解码成功：可无损保存 pendingRaw，资源尚未绑定时不执行业务。
资源绑定成功：原有 Session／Story／continuation 可运行。
压缩读取未到达 reader 或 reader 失败：所有 getter、绑定、调度、结果路由、dirty 与 writer
受同一可读门禁保护。writer 必须早于输出修改拒绝；MapStorage 先序列化后打开原文件。
再次读取失败不替换旧内存，但该实例不能继续执行或写回；只允许测试中显式读回已知有效备份。
合法 pending 的资源暂缺不等于磁盘读取失败，保留当前重试绑定合同。
隔离单位是这份 Session／Story 文件及其依赖者，不自动删坏前文缓存，不半恢复，不复活旧 reader。

## 修复前实证

`baseline-probes.log`：Task 原入口首项 `task.resource.schema` 失败；Story 首项
`story.resource.invalid` 失败；其余三个指名 Probe 与 Session SavedData Probe 完整通过。
原日志保存在 `.tooling/0401/Logs`，各任务失败保留非零退出码，未计入最终 PASS。

`storageBaseline0401Probe` 使用真实 Minecraft MapStorage 与压缩 NBT，只有 ISaveHandler
是隔离文件系统适配器，无 Minecraft 替身。512 条保存重启可读；513 条写出成功，重读拒绝。
坏 gzip、坏 schema、单条坏 line_contexts 都产生可用空对象；隔离测试强制 dirty 后原文件被改写。
这是本轮实测故障，不能解释为已证明正式用户世界发生过覆盖。
历史复现入口不纳入修复后验证清单。

## 测试／验收映射

R1 → 实际容量与服务预检 Probe；R2 → 实际 MapStorage 故障 Probe、现行持久化及恢复 Probe；
R3 → 两份核心入口的逐方法当前夹具维护；R4 → 当前过滤自动验证、有限原生与人工听音矩阵。
原生／人工项目分别记录，不将头less 测试计作原生操作或听音。
最终执行入口为 `scripts/verify-0401.ps1`；历史失败入口不纳入最终清单。

## 两份混合 Probe 的逐方法去向

下表中的每个方法均保留在原入口中，只有整个方法成功返回才打印 `BEHAVIOR 方法名=PASS`。
`canonicalTaskRuntimeProbe` 最终实际执行 10 个方法，`canonicalStoryServerServiceProbe` 执行 6 个方法。
任一断言失败会使对应 JavaExec 与验证脚本非零退出，没有删掉失败入口或把异常改成 SKIP。

| Task 方法 | 保留的当前行为／夹具调整 |
|---|---|
| parallelTreasuresKeepIndependentSuccessors | 两条并行 Flow 及各自后继；物品改用真实当前 DGR 地址 |
| canonical0310TaskSemantics | 开始激活、提交、奖励和当前物品匹配；目标字段使用当前 schema 3 |
| parallelSequentialAndTypes | 并行、顺序、击杀／收集／提交类型；不恢复旧原生注册名目标 |
| priorityAndUnconnectedFalse | 优先结算与未连接条件为 false；旧多输入结算 fixture 拆成现行单输入结算节点 |
| activationSettlementAndPostSettlement | 激活、结算和已结算拒绝事件；公共 Logic 输入替代退休 activate 节点类型 |
| overflowAndParallelSameType | 同类并行与溢出分派；断言使用事件的当前 DGR 目标 |
| prerequisiteActivationSemantics | 前置开关、完成锁存、激活与再关闭；已结算修改输入明确被拒绝，未再结算 |
| dormantUnselectedObjectiveSemantics | 未选择的 dormant 目标不得执行、占用或领奖 |
| immutableSnapshotRestore | detached 快照、恢复与持续逻辑；当前结算公开端口带 display_order |
| malformedFailsClosed | 错 schema／字段／重复公开端口／非法前置等仍拒绝；负向样例自身使用合法当前图结构 |

| Story 方法 | 保留的当前行为／夹具调整 |
|---|---|
| entrySessionTerminationAndRestart | 启动、子会话、终止与恢复；真实 ST UID 和 Session 地址、schema 3 |
| typedTriggerSelection | 各当前触发类型与不匹配拒绝；有效 Actor 定义、当前身份地址 |
| durableConditionResume | 等待条件、持久游标、保存恢复及重试，保持相同活动实例 |
| errorCancelsSessionChildren | Story 错误清理对应子会话，不串其他玩家实例 |
| packageUninstallClearsRuntimeInstances | 卸载清理活动实例；重新安装不抹除 once 历史，同玩家被阻止，新玩家可启动 |
| repeatEntryGates | 8 条启动路径及两类重复规则；拒绝请求不得更改游标／子会话／revision |

退休内容只限旧目标、旧 activate 类型、旧结算端口结构及“卸载后抹除 once 历史”的期望。
生产 reader 没有为测试兼容恢复这些格式。两份 Probe 中混合的现行业务断言仍完整执行。

## 实际消费者与错误边界

`CanonicalSessionSavedData` 的公共包装方法先验证读取状态，再访问 store、Story、continuation、
terminal route、start observation 或 line context。合法 pending 的绑定失败可以重试，读取失败不可以。
line context 还验证 transport 对应的实例及 Story／Session 资源身份。

| 入口 | 本版保护／验证 |
|---|---|
| MapStorage get、getOrCreate、全部绑定／调度／结果／dirty／writer | 统一 requireReadable；9 类真实文件故障、再次读失败、sentinel 和强制 dirty Probe |
| Task manager、玩家同步、Task 展示 | 父 Session 文件检查先于 Task 数据／收据创建、推进及展示；稳定隔离日志按玩家去重 |
| Story 查询／恢复 | 既有事件边界受控拒绝，读取不可用的相同查询日志不按 tick 重复输出 |
| Forge serverStarting | 启动阶段包代际协调只捕获明确的文件不可用；继续注册命令和启动服务器，不标成功协调 |
| 包代际协调 | Session 读取先于相邻 registry／Task 存储创建；失败不更新代际或清理其他实例 |
| /dgr | 捕获明确隔离异常并回传现有错误提示；真实专服 reload、集成服 reload 与普通命令验证 |
| 包管理网络请求 | 既有请求级失败响应保留 sequence；不可读取的 activeCount 返回不可用值；当前 opening 失败清除 pending 并显示中文错误，真实 O 键故障请求已验证 |
| Live Bridge | 请求级捕获隔离异常并回传 request_id 与失败；状态投影先检查文件，不输出空进度；直接 Bridge 故障请求尚未注入验证 |
| 只读诊断 | 已观察对象通过受保护 writer；磁盘快照严格验证当前世界 codec，拒绝坏 root／context；直接诊断故障请求尚未实跑 |

首次专服实测暴露 serverStarting 缺口，其失败日志单独保存。补齐后同一损坏文件可以正常启动服务器，
强制 dirty 的 saveAllData 与保存停服均保持原 SHA-256；普通 time 和 reload 拒绝反馈正常。
集成服务器也实际进入损坏文件世界并完成上述保存／退出验证。

## 过滤作者边界

节点与 Inspector 的目标编辑走 `SelectedObjectiveItem`／`ObjectiveItemOptions` 和 DGR 资源选择，
`CanonicalGraphNodeControl.xaml` 未提供 metadata/damage 编辑控件；Inspector 的目标属性实现也没有对应 setter。
Core 的 `CanonicalTaskObjectiveSchema.MetadataProperty` 是格式验证入口，不是作者 UI。
因此只将该作者填写步骤记为 `N/A_NO_AUTHORING_ENTRY`。没有新增过滤 UI。
`canonicalTaskForgeProbe` 使用真实 Minecraft InventoryPlayer／ItemStack，覆盖当前 DGR 个体、EXACT／FUZZY、
damage 允许与排除、跨槽计数、不足数量原子拒绝及过滤扣除保留被排除物品；该受控 fixture 不冒充 Studio 导出。

真实 CNPC 集成服使用正常 UI 库存包的受控派生 fixture；保留生产者、身份与 schema，仅给 collect／submit
注入 damage=3 并修改测试显示文本。1 件 damage=3 加 2 件 damage=4 时显示 1/2，不足量不扣；
再补 1 件 damage=3 后真实角色点击提交，正常 Story Task SETTLED、7 XP，背包剩下两件 damage=4。
重复角色点击、无内容 reload 与保存退出后仍只有一次奖励。辅助 debug Task 因父故事结束取消，没有冒充第二次结算。
最初改了不支持的 producer 的负向派生包被正常拒绝，失败包与记录保留，未计通过。

## 有限原生验收

原版单人正常单包经 Story→Session→Choice→Task，Choice 保存冷恢复保留前文；正式 CNPC 集成服完成
真实 NPC 个体／集体角色、显式转移、解绑、重指名和保存重启。NPC fixture 的 ReturnStartPos 曾使旧 NPC
回到地下，这不是绑定丢失；修正测试实体 StartPosNew 后，真实 NPC 保存坐标及宿主身份正常。

实际 Studio 导出的三成员联动组包不修改字节，部署到专服及两个客户端。A 到 Choice、B 保持第一句 LINE；
A 选择后进入 Task，B 不推进。无内容 reload、正常停服重启、两端重连后 A 仍是 Task 0/2，B 恢复第一句。
存储中两个测试 UUID、transport ID 独立。专服新世界的初始进度快照仅在隔离 Normal0401 夹具中显式归档，
没有处理正式世界或损坏文件作为修复。

Studio 原生键鼠／UIA 验证搜索、组内和整组排序、Undo／Redo、Esc、鼠标离开返回、失活、工作区切换；
取消时顺序与文件不变。只读引用组的成员／整组拖动不改项目定义和包字节；公开端口可见。
资源列表排序后，Task 拖入画布增加正确资源节点，单次 Undo 还原原图，Redo 恢复；Actor 拖到参数框保存
精确资源身份，单次 Undo 还原。保存、退出候选、冷启动后顺序与图定义相同。

人工听音仍 NOT_RUN。开发客户端实际加载锁定 CNPC JAR，在初始化因 `field_78804_l` 缺失失败；
证据指向第三方 SRG 字段与 MCP 开发映射冲突，未改第三方 JAR，未进入世界，不计开发 CNPC 链通过。
直接 Live Bridge／只读诊断故障请求，以及正常未注入包的完整库存奖励、Disabled 新旧实例与零结算游戏链
仍待补；自动现行断言通过不替代这些 M 证据。完整状态见 Acceptance.md 的 42 项矩阵。

## 来源和交付

Studio 是本轮冻结候选，自包含 win-x64 Release；之后只追加 Java 的启动／展示／Live Bridge 隔离边界与包管理 opening 错误反馈。
Java 最终构建和 30 个 Probe 重新完整执行。Studio Core 510 PASS；WPF 667 PASS、1 个显式性能对比 opt-in SKIP。
WPF 两项实际 300 节点渲染测试已执行；早期人为中止和过短超时中止记录未计为最终 PASS。

最终正式 Runtime JAR SHA-256 为 `B2A2DFE7F83570D77BD40DCA96C8A048B8B3069397CD1C302342981B94130896`。
容量／CNPC／过滤早先实测使用 `EF427861…` 候选；逐项比较两份 JAR 的 776 个解压条目，唯一变化是
`ClientPackageManager.class`。存储、服务端、库存及 CNPC 相关字节相同；没有把旧候选实测改称为最终 JAR 实测。
完整 build／30 Probe、原生 opening 隔离反馈与双玩家组包重启均使用最终 JAR。

用户确认保存关闭后，使用现有 package-studio.ps1 更新权威 dist。内核残留进程经用户确认、exit_code=0、
process_deleting=true、无窗口核实为已关闭，未强杀。410 项程序文件与候选逐字节一致；
Data 2690 文件、68,773,074 字节，部署前后路径／大小／SHA-256 完全一致。
原始个人 Data 清单保存在本地 `.tooling/0401/Logs`，公开摘要仅保留数量和一致性。
Core DLL 的自动 informational version 含基线 SHA，它不是最终实现提交；以编译输入物理字节指纹确定本轮来源。

回退必须恢复匹配程序与世界／DGR 数据备份。0.4.0.1 的大于 512 条合法表不保证可被旧版 reader 读取，
不能仅替换旧 JAR 或自动缩表。未创建成品 artifacts、未推送、未移动标签、未发布 Release。
