# 0.4.0.1 验收记录

2026-10-08。R1／R2／R3 代码实施完成；本轮按用户批准方案完成 Runtime 自动扩缩收尾，Studio 沿用已交付版本；技术验收未闭合。
42 项为 37 PASS、1 精确 N/A、3 NOT_RUN、1 BLOCKED。用户尚未接受整体验收；没有源码上传或公开发布。

证据层级：A 为自动测试／真实 Minecraft 类 Probe；M 为实际 Forge 游戏／服务端操作；W 为 Windows
候选程序的键鼠／UIA；H 为人工听音。本轮没有 H PASS。MapStorage Probe 只适配 ISaveHandler 隔离文件系统，
没有替换 Minecraft NBT／MapStorage／InventoryPlayer。真实服务器容量／强制 dirty 检查使用独立 acceptance driver，
正式 JAR 不含 driver 或开发桩。

容量原生驱动首次按类型选到 Forge 的 perWorldStorage，生产维护处理 mapStorage，导致等待超时；
该失败世界与日志保留且不计通过。修正只涉及验收驱动：明确选择 MCP／SRG mapStorage 字段，
并断言它返回的指名对象与正常无参 get() 是同一实例，然后从新世界重跑全部阶段。

## 最终验证

Runtime build exit 0，30 个 Probe 全部实际执行、exit 0；两份维护的核心 Probe 分别完成 10／6 个行为方法。
Core 510 PASS、0 FAIL；WPF 667 PASS、0 FAIL、1 SKIP（显式 opt-in 的 ScaleComparison0334Tests，
缺少 DGR_SCALE_OUTPUT；不是本版 mandatory 验收）。两项实际 300 节点渲染已运行。
结果与命令见 [tests.json](evidence/tests.json)、[runtime-commands.json](evidence/runtime-commands.json)。
早期 baseline 失败、Spotless 首次失败、两次中止 WPF 和 native harness 失败保留，均未累计为最终 PASS。

最新动态 JAR：`569E97050C4281EF0B89AED9EEDFEB18DE7C34CC4651E24530339C95D87F1629`，1,856,126 B。
旧 opening／双玩家原生验证使用 `B2A2DFE7…`，旧容量／CNPC／过滤使用 `EF427861…`；历史两者只差
ClientPackageManager.class 的对照归档到 evidence/pre-dynamic。本轮存储／身份／服务类已改变，
保留历史原生验证范围，不称为最新 JAR 直接运行。最新 JAR 完整重跑 build／30 Probe，并另跑
6500 条真实 Forge 保存重启、编辑、五分钟缩容、再次保存重启。
见 [dynamic-runtime-delta.json](evidence/dynamic-runtime-delta.json)、[dynamic-capacity.json](evidence/dynamic-capacity.json)。

## 42 项矩阵

| ID | 状态 | 层级 | 实际结果／范围 |
|---|---|---|---|
| A01 | PASS | A | 原生产类＋真实 MapStorage：512 可往返，513 写后拒绝重读；真实 gzip，与旧替身实验分开 |
| A02 | PASS | A | 默认 4096／每档 512，75% 自动增、低档 50% 连续 300s 缩；无最终配额／字节预算；目录／网络仍 512；8192 最大字段 35,299,420 B，gzip 240,647 B 可重读 |
| A03 | PASS | A | 0／1／511／512／513／3071／3072／3455／3456／4095／4096／4097／8192 实际 MapStorage 往返；4097 改为自动扩容 |
| A04 | PASS | A | 可控时钟等待重置、最低档、多档直降、再增长；实际槽位 16384→256，绑定／索引／顺序／revision／dirty／NBT／文件不变 |
| A05 | PASS | A | 既有双锁顺序，成功批次只按最终净数量调整；冲突失败不改状态；转移源残留组、净零、净减及解绑通过 |
| A06 | PASS | A | 当前请求错误边界、token／sequence／pending 回归通过；删除退休容量异常分支，不改协议；坏对象维护跳过，强制 dirty 不覆盖文件 |
| A07 | PASS | M | 最新 JAR 隔离 Forge 6500 条保存重启及编辑；降至 100 条，真实五分钟后 8704→4096、三表槽位 16384→256；纯压缩不改存档，随后编辑保存再重启通过；独立 driver |
| A08 | PASS | A/M | 当前指名／物品 Probe，实际 CNPC 个体、角色组、转移、解绑、重新指名；目录预算未扩大 |
| B01 | PASS | A/M | 真实反射构造→压缩读失败→MapStorage 缓存→get／save；原版专服、集成服复验 |
| B02 | PASS | A | gzip 坏在 reader 前，String 构造待读取不可用；没有可用默认空状态 |
| B03 | PASS | A | root／schema／实例类型等严格拒绝，解码后才替换内存，不半加载 |
| B04 | PASS | A/M | 单条 line context 损坏隔离整个相关文件；同一故障文件真实游戏保存后字节保留 |
| B05 | PASS | A | 有效实例再次读取失败保留旧内存但封禁业务和 writer |
| B06 | PASS | A/M | writer sentinel 不变；强制 dirty 的实际 saveAllData 和停服前后故障 SHA 相同 |
| B07 | PASS | A | get／bind／start／resume／结果路由／history／dirty／writer 公共入口逐项检查 |
| B08 | PASS | A | 真正无文件新建可用；合法 pendingRaw 原样保存，暂缺资源可重试绑定 |
| B09 | PASS | A/M | 正常 LINE／Choice／continuation 自动恢复；集成服 Choice 前文与头像、双玩家 LINE 重启正常；不替代听音 |
| B10 | PASS | A/M | 首次 startup 未捕获缺口已修；坏存档专服可启动、集成服可进入，普通 time 命令正常，reload／O 键中文拒绝，无 tick 崩溃 |
| B11 | PASS | A/M | transport／Story／Session 错配受控拒绝；双玩家不串；有效备份仅隔离测试中显式恢复 |
| T01 | PASS | A | Task／Story 原入口首个失败及未执行部分保留；不继承旧 PASS |
| T02 | PASS | A | Implementation.md 逐方法当前／退休映射，入口精确；旧 activate／裸字符串／旧结算未复活 |
| T03 | PASS | A | Task 10 个当前行为方法完整执行，包括前置、dormant、并行、快照和拒绝 |
| T04 | PASS | A | Story 6 个当前行为方法完整执行，包括 8 启动路线、重复、条件、子会话、资格和恢复 |
| T05 | PASS | A | verify-0401.ps1 包含两核心＋R1/R2 共 30 Probe，失败非零，日志检查实际任务执行 |
| T06 | PASS | A | 最终 Runtime build／30 Probe、Core 510、WPF 667 完整；1 个可选 SKIP 另列 |
| V01 | N/A_NO_AUTHORING_ENTRY | A/W | Task metadata/damage 无正式作者控件／setter；只豁免填字段步骤，不新增 UI |
| V02 | PASS | A/M | 受控派生当前包：DGR 个体、EXACT／FUZZY、damage、跨槽、1/2 不足量拒绝、角色提交后只扣两件允许项、排除项保留 |
| V03 | NOT_RUN | H | 未收到实际人员／设备听音确认；语音、BGM、静音、分页／冷恢复不重播均不能由日志签署 |
| V04 | BLOCKED | M | 开发客户端加载真实锁定 CNPC JAR，初始化 NoSuchFieldError field_78804_l；未进世界，未改第三方包 |
| V05 | NOT_RUN | A/M | 指名／工具自动、命令、包 GUI、实际 CNPC 拒绝代表已跑；直接 Live Bridge／只读诊断故障请求未注入，不宣称所有边界完成 |
| V06 | PASS | A/W | 搜索、组内／整组作用域，Esc、离开返回、失活、切换；取消无额外排序／文件变更；只读引用结构不改 |
| V07 | PASS | A/W | 资源排序后 Task 画布放置、Actor 精确参数；单次 Undo、Redo、保存与冷启动顺序／定义一致，引用只读 |
| V08 | PASS | W/M | Studio 真实单包和三成员联动组包，同字节部署；正常 Story→Session→Choice→Task，双玩家独立与专服重启 |
| V09 | NOT_RUN | A/M | 自动锁存／提交／奖励／Disabled／零结算通过；受控过滤包真实一次 7 XP、reload／保存通过；正常未注入包的完整库存奖励、Disabled 新旧实例和零结算游戏组合尚未跑完 |
| D01 | PASS | A | be0b180… 接续至 codex/0.4.0.1；认可的 UI 改动保留，未混入多版本 |
| D02 | PASS | A/W | Runtime／Studio／Core／正常导出生产者 .1；schema 3／4／7 原样，身份不改 |
| D03 | PASS | A | 含未跟踪新增文件的物理输入清单、依赖、实际 JAR／EXE／DLL／白名单指纹；Core 基线信息不冒充实现提交 |
| D04 | PASS | A/M/W | 退出码、方法映射、故障 hash、程序／包 hash、native 摘要及本地 raw 指纹齐全；不夹带世界或个人 Data |
| D05 | PASS | A | 已更新权威 dist；410 程序文件逐字节等同候选；EXE／主 DLL／Core DLL／白名单读回 |
| D06 | PASS | A | 用户保存关闭后更新；Data 2690 文件、68,773,074 B，路径／大小／hash 完全相同；故障注入仅 .tooling/0401 |
| D07 | PASS | A/W | 自包含候选首启 Dark、本地 Data、当前项目保存冷重开；锁定工具调用和实际媒体导入自动测试通过，未冒称额外 GUI 导入动作 |
| D08 | PASS | A | 实施／验收／交付／用户接受／发布分开；无 .1 artifacts／push／tag／Release，旧发布资料未改 |

## 存储与原生证据

R1、R2 的真实加载／保存使用各 Probe 的隔离 gzip 文件。故障 Session 文件 SHA-256
`5EAA367033B858537562248599711BD9F2920652A6B1F22DF527A90C03C93828`，集成服／专服
强制 dirty 保存、普通操作和正常退出后保持不变。原始 baseline 确实重写过该隔离坏文件；不推断正式用户数据事故。

正常单包 hash `F8B400CA…`，正常联动三成员组包 `ACF60FB1…`；实际专服／客户端部署相同字节。
过滤 fixture hash `84AEBA20…`，来源正常库存导出 `98961124…` 后明确注入 metadata.damage=3，
生产者与 schema 保留。不能用受控包代替正常 UI 同字节链。

详见 [native-summary.json](evidence/native-summary.json)、[native-raw-evidence.json](evidence/native-raw-evidence.json)。
后者只保留本地原始文件路径／大小／hash；raw 日志、截图、世界保留在隔离目录，没有复制进入可上传文档。
初始 startup 缺口、错误 producer 夹具、无效 debug UID／长聊天命令、最小化黑图、失焦菜单及 NPC 返回坐标
等失败记录都保留，未当作 PASS。

## 剩余项

- V03：需真实人员完成 PLAN §7.3 听音；此前集中请求未收到声音结果。不能把关闭 Studio 的答复当作听音接受。
- V04：开发 CNPC 启动阻塞；固定第三方 JAR 与 MCP 开发映射的兼容处理超出本轮“不改第三方 JAR”约束。正式 CNPC 游戏链已通过，不能替代开发链。
- V05：补直接 Live Bridge／只读诊断故障请求；源码门禁和公共存储故障 Probe 已覆盖，但不同请求边界的实际执行还不齐。
- V09：补正常未注入包的完整库存奖励、Disabled 新旧实例和零结算 M 组合；当前自动结果和受控包 M 结果分别保留。

这些项不阻止记录 R1／R2／R3 实施与开发 dist 交付，阻止“全量技术验收通过”。
后续应继续同一份矩阵，不能把未运行项改作 N/A，也不能未经用户授权公开发布。
