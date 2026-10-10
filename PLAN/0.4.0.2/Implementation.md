# 0.4.0.2 施工记录

2026-10-09 开工。状态 IN_PROGRESS，基线 `3bd57b62c2e9a3a7eb6f409b755599470b39c41c`，生产 .1 JAR SHA-256 `569E97050C4281EF0B89AED9EEDFEB18DE7C34CC4651E24530339C95D87F1629`。分支 `codex/0.4.0.2`；原始用户 PLAN 保留，PLAN.md 是同字节工作副本。本次授权施工，未授权 .2 成品、push、tag 或 Release。

## P0 热路径表（修改前冻结）

| 路径 | 频率／归属 | 当前扫描／重建 | 失效条件与验收 |
|---|---|---|---|
| StoryEventAdapter 四个周期入口 | 每位玩家每 10 tick；共享 overworld MapStorage | 每入口创建 service → bindAvailable → 全 Session/Story 编解码、替换容器 | 同 storage＋不可变 ProjectSnapshot 只首次绑定；新快照有限恢复；读取失败门禁先行 |
| SessionForgeManager.context | 输入、恢复、投影；同一存储 | 新 resolver 后无条件双 resolver bind | 复用同快照绑定；真正资源更新重新验证，不依赖 resolver 身份 |
| synchronizeMedia／reprojectTitles | 玩家轮询／重连 | 所有 Story 快照后过滤玩家 | 先测离线增长，再决定索引；保留离线状态 |
| TaskForgeManager.snapshots／展示 | tick、事件、客户端查询 | 世界快照后过滤玩家；部分投影已有去重 | 先实测；背包、条件采样不降频 |
| pending Flow route | 登录／周期；player＋run | null-first 提前返回；下层有独立 budget | 共用入口、幂等 claim/target/applied；Flow/Logic 共用有限预算 |
| MainThreadScheduler | 双端 START tick；静态队列 | 无界准入、一次排空、异常逃出 | 准入、FIFO、预算、异常及停止取消责任逐调用者迁移 |

## P0 队列责任表（修改前冻结）

完整文件／行号清单存 evidence/queue-call-sites.json；不能只替换 scheduler API。

| 调用类别 | 当前限流／所有者 | 成功、拒绝、取消责任 |
|---|---|---|
| Session/Story Choice、Task submit/selection、Title | player/connection/transport/run；部分 token/sequence 校验 | 不能合并；保留相关请求身份，拒绝须终结等待，执行时重验实体 |
| 指名、复制工具、Task console、包管理 | connection＋玩家＋目标实体；现有工具验证 | 正常 FIFO；明确失败回应；旧连接不写新世界 |
| Live Bridge、诊断 | server 实例＋request id；异步来源 | 回应对应请求错误，停止拒绝；不能假装空成功 |
| 媒体 server 入口/IO 完成 | 256/s、66 in-flight、2 worker＋64 queue；player/source lease | 拒绝/旧 epoch 释放 IN_FLIGHT 和 reader；完成必须重验来源与授权 |
| 媒体 client 清单/读盘/下载/上传完成 | 3/10 bundles、窗口/纹理预算；connection/包代次 | 取消必须解除下载、pin、pending；晚到结果不能复活；只读内容缓存可复用 |
| Gramophone 请求/IO/维护/客户端草稿 | 上传/下载各自限制；server、connection、upload/download | 取消释放活动状态/维护 pending；跨世界旧回调不能提交或发包 |
| UI frames、notice、receipt、snapshot | connection＋transport/token/request | 旧来源不投影；断开现有关闭/清理通道终结 UI |

## P0 环境、参考档与门槛（修改前冻结）

i7-10750H，6 核/12 线程，Windows 10，约 15.9 GiB RAM；开工可用约 3.16 GiB，E 盘可用约 43 GiB。两个既有 Java 8 服务端及两个 Studio 进程不是本轮所有物，不停止。Gradle JDK 25.0.1、ASCII cache；游戏 JDK 8u471；.NET 10 SDK 路径核验后记录。

固定 seed 40201—40205。参考档先 4 逻辑用户/100 ACTIVE Task/20 节点；探索 16/1000、32/5000、离线 0/1000/10000、100/300 节点。实际网络人数单列，不将 UUID 当客户端。先低档验证；如机器无工作对照不达门槛，保留原档与环境原因，不悄悄改阈值。预生成小区域；heap、view distance、fixture 和输入序列在配对 baseline/candidate 中相同。

完整 tick p95≤40ms、p99≤50ms；可重复 DGR ≥500ms 为阻塞，所有≥250ms定位。交互 p95≤250ms、p99≤1000ms；突发停止后 5s 内队列回近空闲。正常必测零崩溃、串写、重奖和永久等待。性能主场景每版 3 轮，预热 5min＋测量 10min；最终混合 60min＋停服冷恢复。固定探索 5 seed×2000 步。没有实际原始证据不得填 PASS。

基线 4096 个 noop 单 tick 排空，31.758 ms（首轮，包含类初始化；未观测业务丢失，不能当真实交易性能）。据此冻结两端准入 1024，server 每 START tick 最多 64 项／2ms，client 最多 32 项／2ms。只在任务之间检查时间，不抢占 Runnable；正常 FIFO，普通持续流量必须实测。后续改参数须保留原档与原因并重跑。其余门槛保持 PLAN §11。

## P0 断言来源（修改前冻结）

| 断言 | 独立预期／证据 |
|---|---|
| ≥200 次实际四轮询入口 | 反射只在测试中观察容器身份；baseline 重建计数、线程分配与耗时；candidate 初始化后零重建 |
| pending 自动恢复 | 构造合法 terminal claim；生产 recoverPendingRoutes/login/timer 入口；target run、claim/applied 与独立计数 |
| 生命周期与排队 | 固定任务编号 FIFO 台账；accepted=completed+cancelled+failed+pending；旧 epoch 不执行；Error 向外传播 |
| 媒体资源 | 真实 in-flight/lease/pin/下载状态；拒绝、取消与迟到结果后能再次下载，维持 3/10 |
| 大量任务 | 成功 ACTIVE 数、真实事件访问数、独立库存/奖励/游标台账；非空记录数替代 |
| 保存与冷恢复 | NBT/包/产物 hash、真实 mapStorage 身份、重启后 run/transport/receipt；无内容 reload 不重置 |

后续 P1—P4 记录追加于此，未执行项维持 NOT_RUN。损坏 NpcIdentity 专项保持 DEFERRED_BY_USER；人工听音、开发 CNPC 映射阻塞单列。

P0 A 层基线：200 轮×4 个实际 manager 周期入口，800 次全重建，147,053,472 B 线程分配；每轮 p50 0.806ms、p95 2.2394ms、p99 4.6144ms、max 13.5576ms。一个离线 Story、零网络客户端，不当全服务器 tick。合法 schema3 pending 经生产 recoverPendingRoutes：来源 pending 保留、目标未创建。首轮旧 coordinator 夹具 schema2 失败保留于 baseline-invalid-old-fixture.log，改当前格式夹具后 baseline-probe.log exit0；未改生产 reader。

P2 准入修订（实施前记录）：普通入口仍 1024；另保留最多 256 个完成回调槽位，进入同一个 FIFO、共用原执行预算，不插队。原因是已有 server 媒体 66 和留声机 65 个有界 IO 工作必须能完成已接收事务，不能被普通网络请求挤掉。client 有界媒体/传输 IO 同理。总排队上界 1280；完成槽位仍满则明确失败并执行幂等清理，不能宣称无界保证。需新增普通满载时完成回调准入、顺序和取消回归；原参考性能门槛不变。

P1 第二热点实测：修复重复绑定后，增加 1000 个离线 ACTIVE Story，同样 800 次生产查询分配仍升至 491,560,104 B，每轮 p50 1.4753ms/p95 2.5014ms/p99 3.6574ms。这来自 synchronizeMedia 世界快照再过滤，并非重复恢复。根据该证据新增 Story store 的玩家索引，保留全量保存入口；索引随创建/替换、读回、指定删除和批量卸载更新，不删除离线实例。

## P3 新发现：Task 派发与节点属性复制

实际隔离 Forge 的 1000 ACTIVE Task / 16 逻辑用户 / 每任务 20 objective / 每 20 tick 每用户一个事件，独立进度台账正确，但旧候选完整 tick max 2117.2764ms、p99 2001.8541ms。事件入口以 persistedState 编码整个世界两次，线程栈记录在 ignored tooling 的 active1000-thread-sample.txt。本次移除派发的全量比较，改用 accept / markError 的权威变更结果，只观察新 SETTLED 候选的完成历史；全量移除、重载仍保留原清理合同。第一轮修复 max 843.7718ms，尚未闭合，不当作通过。

第二轮分解计时与 components-stack-1/2 显示，每玩家 synchronizeWorldLogic / synchronizeObjectives 先复制全世界 Task 快照，派发重索引还反复经 CanonicalGraphNode.getProperties → JsonParser 复制属性。新增 transient 玩家索引，仅引用同一 store 对象；start、replace、discard 与完整读回维护它，完整存档的排序与内容不变。节点属性保持深复制，直接复制 JSON 树，并冻结可变 Number、保持数字 token 字面，不共享可变对象。

同档短时复验完整 tick p95 64.5335ms / p99 87.5047ms / max 124.3274ms（512MiB heap，预热 600 tick，采样 600 tick）。可重复 ≥500ms 原反例已消失；该档仍超过参考档门槛，只是压力探索结果，不冒充正式时长 / 最终字节验收。基准与每个候选的 JAR / driver SHA 和原失败分别保留，后续最终候选须重跑。

TaskHotPath0402Probe 验证 constructor / getter 的嵌套 JSON 和可变 Number 隔离、数字 token、不可变 Map、dispatch no-op 的 dirty / 序列化，及 5 seed × 2000 操作的 Task 实例索引 / run / 独立 objective 计数台账。它是 A 层索引模型，不冒充完整游戏业务探索。CheckpointIndex Runtime build 与继承 30 + 新增 4 个 Probe 均 exit0；早期 CheckpointTaskFix 的脚本把单个 build 字符串错误展开为字符任务，原日志保留，已改为 string[] 并重跑。

## Studio 开发 dist 更新

Core 最终 510 PASS / 0 SKIP；WPF 完整回归 667 PASS / 1 SKIP（668），软件渲染，11.2643 分钟。此前超短看门狗与一次临时文件替换失败的未完成记录保留，不能替代完整成功记录。实机隔离副本正常恢复当前项目，普通菜单导出单包 / 完整组包；UI 截图、本机包字节与哈希分别留存。没有修改编辑器业务逻辑。

用户已保存关闭后提升权威 dist。正常部署重建产生不同 Core DLL，哈希门槛拦下该次提升确认，未当成已验收字节。随后按同一程序白名单原样提升已验证候选，410 项逐文件匹配。权威 EXE ProductVersion 0.4.0.2，204288 B，SHA256 8E569CC39547BCA0BB9E537BFC4BD42E048318B5720657735176C8F30CB97829。首次与最终更新前后 Data 的 2690 文件 / 68773074 B 哈希完全一致；个人文件名清单仅留 ignored tooling，公开证据保存聚合与清单 hash。本轮未创建成品包、标签或远端 Release。Runtime 整体验收继续。

## P3 混合业务、奖励归档与同 JVM 世界切换

新增 MixedBusiness0402Probe：5 seed（40201—40205）各 2000 步，2 个独立世界、相同 4 个 UUID，生产 SavedData / 库存匹配与原子移除 / TransactionJournal 的混合操作。独立整数台账逐步核对库存、XP、累计收据、run、进度、结算与取消；含提交失败、重复奖励、no-op、无内容 bind、冷 NBT 恢复。证据层 A，不冒充实体／联网／客户端测试。首轮夹具时间戳早于激活时间被正确拒绝，原失败留存，修正事件时间后 10000 步通过。

专项断言另发现 grantReward 从待奖励变为 SETTLED 后未立即观察完成历史。reward-history-before-fix.log 保留最小失败。现仅观察本次新结算实例，重复奖励不改 dirty、不重复归档、不扫描全世界；修复后专项与 10000 步通过。Runtime build 本身通过。额外单独调用的 canonicalTaskEventPersistenceProbe 使用退休 Task schema，被现行边界拒绝；该历史测试不在继承的现行 30 项名单中，未恢复退休 reader，后续按现行名单重跑。

原生 ClientC 在同一 PID 53512 内，通过正常菜单新建 WorldA0402 / WorldB0402（seed40201、creative、flat），实际 20 次 A↔B 切换，22 次加载（含一次重开同世界）。独立服务端 verifier 每事件核对同 UID／同 UUID／维度0 的 run 和 20 个 objective 进度，A 首 run 保持 1791528069496 / Task 1791528069556，B 有自己独立 run。所有切换无失败哨兵；正常菜单返回主菜单后队列 1182 accepted / 1182 completed / 0 pending / 0 failed。保留 jsonl、世界 ledger、截图及 client frame 原始数据。此批 JAR D43882…、driver 0B57A… 属奖励历史修复前检查点，最终字节仍须验证。

新增服务端混合驱动用于实际 MapStorage、库存事务、实体提交、奖励、玩家实体重建、保存及 2×/4×队列突发。第一轮宿主所在 chunk 未加载，spawn 返回 false，保留失败；修正为准备期加载隔离 chunk 后重试。此驱动尚未完成 60min 或冷恢复，不记 PASS。

正式对照进度：baseline01 full p95 76.7863/p99 89.6317/max144.2152 ms；baseline02 p95 82.189/p99 89.5242/max336.3168 ms。baseline02唯一≥250ms位于完整采样10181（对应服务tick16180），事件Forge跨度336.3100ms，四用户 worldLogic/objectives/dispatch 合计约250.59ms；相邻100tick区间GC累计增加83ms，不能把83ms全部归给该tick。定位至基线周期Task事件路径，原操作计时与CSV保留。

早期候选D43882检查点两轮5min+10min各12000样本，full p95/p99/max分别5.0079/7.5085/53.6793ms与5.1/7.8988/56.347ms。奖励归档修复后4D289C检查点第一轮 full5.4373/9.5288/83.0911ms通过，独立台账与正常保存均通过，golden driver SHA A465D83A1C0B63EE757EB5CCB938DFA985FA329ECC4851AD0D32A508E6BA7C76。baseline03构建跨入开头测量，后续也运行了隔离短样例，保留为探索、不纳入正式重复；baseline04补跑。未降低固定100ACTIVE/4逻辑用户/20objective的输入，也未修改主比较门槛。

真实混合S短样例终于经完整Story宿主启动Task，1min和正常保存冷恢复均通过每tick库存、XP、累计收据、run、progress和status，以及真实村民提交的不足量和重复拒绝、2×/4×队列。首版直接Task没有Story宿主，生产continuation正确拒绝并清理，保留失败；更正生成器为3个当前格式Story，各自正式start、Task、结算或零结算等待。初始1min full p95 3.42/p99 67.4045/max370.4961ms，冷1min3.137/65.7145/292.2572ms，仅正确性通过，峰值须用新增原始tick和操作阶段记录复现定位，60min仍未跑。

最新Runtime4D289C、原生PID11652也完成20次A/B切换和22次加载，冷启动继承早期真实世界进度，source/Task run和每objective独立计数均保持。正常退回主菜单queue accepted2116/completed2116/pending0/failed0；正式公开聚合及逐次操作为evidence/0402-native-world-switches.json。未包含真实媒体在途，不能以此关闭媒体项。

CheckpointMixedHistory Runtime build exit0，37个现行Probe中仅MediaLifecycle0402Probe失败。它等待线程池getActiveCount/queue近似读数，存在取走工作与标记执行间的竞态；已改为等待该批已提交作业的完成计数（2个门闩工作+实际迟到生产工作），失败日志保留，重复与全套复验待跑。未因单个测试失败先改生产媒体实现。

### 最新定向与预检证据（2026-10-09 16:23）

复杂 Logic / Flow 共用预算新增真实 public Logic 扇出、双 NOT / AND 与每分支两个 Flow 下游。10 分支正常达到固定点且随后 no-op，100 分支受同一 256 预算约束；保存重读和无关 UUID 仍可服务。修复线程池近似空闲竞态后的 mediaLifecycle0402Probe 连续20个独立JVM exit0；37项现行Probe全集 exit0。首次全集 build 的两条星号 import 被 checkstyle 拒绝，改显式 import 后 build exit0，未修改对应生产行为。

baseline04 干净完整12000样本 p95/p99/max84.8121/110.9259/171.575ms，台账和正常保存通过。Candidate-final-reference-02 full5.41/9.6017/67.6399ms，但媒体重复验证跨过预热窗口（到15:55:~38），该轮开头有竞争，排除正式重复；输入、原始数据与污染原因保留。后续正式轮次整段独占，不能仅看低分位就忽略污染。

S短混合 raw-metrics full4.1721/76.9393/437.3042ms。最大值在首tick，其中initialize393.1685ms（准备宿主/物品绑定及同tick12条Story启动）；不把冷夹具启动当常规热轮询。分阶段原始CSV保留；提交包括实际库存/奖励事务 checkpoint，并单独记录每用户耗时。暂不宣称60min通过。

Normal-Authored-preflight：最新Runtime4D289C、隔离正式Forge/CNPC依赖原字节、driverEA222DD4…；真实注册DGR402ClientD/E，两名真实图形客户端。普通Studio菜单导出的原单包/组包均同字节加载，实际单包两名玩家均通过CNPC实体交互、Title等待、LINE图片/头像、点击Choice、两项区域、collect2锁存、实际submit2、一次XP7、Task SETTLED与Story TERMINATED。第一名清空背包后的submit0拒绝（锁存仍2），补2后成功；重复实体交互无重复奖励。native截图与每玩家run/progress/库存/XP、三个真实网络媒体请求、纹理记录留在tooling。源lease恢复到每Story1、服务队列0。正常finish/save/stop，全维度卸载完成。此例组包只有加载，**没有完成组包业务链**；driver旧结果的笼统package措辞以此边界为准，新驱动改成single-package。

为后续60min准备MixedAuthored隔离驱动：正常作者资源对象与字节不重写，通过测试专用union添加三条合法混合Story；4逻辑用户与2真实注册用户分别算。新原生观察器只读取媒体活跃/退役、pin、bundle、网络请求与纹理数/像素，键鼠依旧Win32。新fixture摆位避免两玩家互相挡住视线，NPC保持测试坐标；这是测试准备，不是生产自动移动。持续及冷恢复待执行。

### 原生连接、媒体在途与正式加载边界（2026-10-09 17:14）

此前MixedAuthored的测试union仅有内存资源而没有实际安装资格；真实loader正确拒绝Story start，失败世界Candidate-mixed-authored-60保留，不记持续通过。改成三份当前格式单Story负载包，先用实际StoryPackageLoader/SnapshotMerger验证，再与两份普通Studio导出包一起安装。同一源actor/item按现行ownership及外部metadata一致引用；生成负载明确TEST_ONLY，不能冒充普通作者导出。验证日志validated-mixed-load-packages-v2.log通过。Runtime未放宽reader/资格策略。修正版60min case Candidate-mixed-authored-60-valid，ready16:37:02，Runtime4D289C、driverD644D69B，4逻辑用户，12条由Story实际宿主启动的混合Task，实际两客户端F/G分别注册，当前仍进行中。

最终Runtime的两名原生F/G通过普通单包的完整实体交互、会话、Choice、区域、collect2锁存、submit2、奖励7XP、Task结算及Story终止，各恰好2收据。F清空后的不足量提交不变进度、XP与G的库存；恢复物品后结算时G仍ACTIVE且有2物品/0XP/0收据；随后G独立结算。原生死亡/点击Respawn替换玩家实体，run/progress/库存/XP/收据保持；移除宿主再创建并转移绑定后原生提交成功。无内容reload保持两玩家全部run/progress/库存/XP/收据。

实际媒体worker受控90s门闩制造在途：F请求4条，源lease1→5，在真正UI断连X并加入Y后释放门闩，旧媒体reservation归0、源lease回1、客户端retired/downloading/queue归0，Y没有旧会话。随后9次正常UI连接替换，加第一次共10次、10个不同NetHandler identity；返回X恢复同Story run和pending会话，客户端明确2个取消且0failed。网络边界公开聚合evidence/0402-native-network-boundaries.json由原始观测重新断言生成；暖缓存pin允许按媒体生命周期保留，不谎称所有cache/pin必须0。

Normal-Authored-Y亦以最终Runtime及原正式CNPC依赖完成F/G的独立单包链，库存0、XP7、收据2。正常finish保存/停服。其旧driverB132结果仍有泛称each package措辞，只代表单包业务、组包加载；随后用修正driver冷启动同一保存世界继续组包和跨维度，不替换包字节。初次直接travelToDimension夹具遗漏原版portal cooldown，立即被传送回去导致等待超时，明确NOT_PASS；测试驱动补同原版300tick冷却，生产代码不改。

最新测试驱动另修真Idle：零在线Story、不主动调用四个manager恢复/事件入口；仅离线存量保持全部NBT并验证dirty不变。初始化每条ACTIVE写heartbeat，避免50条慢启动被10s看门狗误当停顿。golden性能比较driver A465D83A字节保持；这些新驱动不混入已完成比较。相关build日志driver-idle-portal-fix.log及driver-native-group.log通过。

### 恢复施工与新 HUD 反馈（2026-10-09 20:50）

用户已指令继续，且明确之前部分 Minecraft 进程由本人关闭。这些退出登记为人工关闭，不能计为 Runtime 崩溃／闪退。Qualification 重启曾消费上次残留 prime 指令，因原生玩家尚未加入触发测试断言；原失败保留，启动脚本新增残留指令检查，未修改生产准入。另一次测试辅助 WM_CLOSE 关闭 LWJGL 窗口却未确认 Java 退出，只对该未使用隔离 ClientH 定向清理；不冒充正常菜单退出或产品稳定性结论。

Runtime 4A455FBF 检查点已完成真实1000 ACTIVE／16逻辑用户／20节点、5000 ACTIVE／32逻辑用户／20节点及 Idle／Static 0/1000/10000 梯度。5000档真实保存与独立进度断言通过，但完整tick p95 7.923ms／p99 421.7989ms／max 515.962ms，属于压力性能不达标；短档且与其他测试并行，不能替代正式参考比较。1000档短复验57.6745／71.8842／123.7727ms。同版本60分钟 MixedAuthored 完成72000tick，4逻辑用户／12混合Task，两个真实图形客户端在其中登录、推进普通Studio单包并退出；不是全程两个玩家在线。队列69120 offered／executed相等，每tick库存／XP／收据／run／状态台账及正常保存通过；完整tick8.2254／125.019／2614.3568ms，峰值与冷恢复仍需闭合。阶段原始样本已备份，后续重启不覆盖这轮。

该轮真正原生F/G分别完成同字节普通单包的CNPC交互、Title、LINE、Choice、两个区域、Collect2锁存和Submit2；不足量保留锁存／不扣物，F结算时G仍独立ACTIVE／背包2／XP0，之后G独立结算。各库存0／XP7／累计收据2，无内容reload及20次原生跨维度保留run和进度。已有4A组包冷读继承较早4D的组包创建记录；不混称为最终版本首次创建组包。

Boundary-Native-4A phase-corrected：32个真正LiveBridge请求各返回自身request id和失败；隔离的只读诊断故障及包管理reload故障在实际GUI中终结loading，下一正常请求恢复，业务Session／Task NBT不变。故障注入只在测试driver，诊断故障不是磁盘坏存档。Qualification-Native-4A-R2 cleared-stale-command：两真实玩家与三份明确标注generated的当前格式容器，真实media worker受控90s；原生Choice在64条在途且未就绪时进入Task，随后释放worker。无内容、Disabled/Enabled、Error、Conflict、真实内容更新、卸载及恢复的完整序列通过，F另一Story及G另一Story的完整Session／Story NBT保持。退休history按现行ERROR合同阻止自动重启；测试明确使用已有管理员reset后建立新run，没有修改生产history以凑PASS。

用户截图新反馈：任务通知标题仍强制§l、1.25倍，标签0.85倍，卡片另按窗口高度整体分数缩放；先前对话字体修复没有覆盖这一分支。900×640、GUI2、同中文的旧4A JAR原生对照复现重影。修复移除通知强制加粗及全部通知文字分数缩放，复用限定作用域的最近邻／像素对齐字体绘制；短窗口改为排队额外卡片，保留三卡上限、合并、去重和完整阅读时序。追踪器换行、行高和物品行正文按实际像素对齐字号布局，正文不再绕过统一绘制。

修复后Runtime SHA256 6455840D283DE21EDC008CCE34C0E366591E4D3A4703D0E9046A9EB6E216B15B；与4A逐JAR条目比较只有TaskNotificationCards和TaskTrackerHud及其内部类共6个class变化，所有服务端／业务class完全同字节，正式JAR无本轮Probe／driver。build及通知／字体／布局定向检查通过，37项现行Runtime Probe重新全部通过。ClientH同900×640原生对照及三通知／125%截图中标题重影消失，nearest=true、filtersBindingBlendRestored=true、linearRestore=true。这是独立C层的本地显示夹具，无服务端业务变更；不当作普通作者链、媒体时序或整版最终性能验收。不同GUI字号／短窗口进一步检查、最终候选正式比较及持续冷恢复仍在进行。Studio交付字节不变。

### 第二次字体反馈与统一绘制（2026-10-09 22:45）

用户随后指出玩家状态诊断页也有严重重影。根因不是同一个局部修复失效，而是此前对话／HUD修复没有覆盖其他绘字入口：诊断标题强制§l，原版GuiTextField和提示浮层使用阴影，选项／任务列表用原始分数字号，剧情图标签继承连续zoom矩阵，剧情标题仍使用阴影及小数缩放，半透明工具窗口还会透出底层HUD文字。

本轮让DgrUiText成为普通Runtime界面的统一入口，复用DialogueFontDrawing的最近邻、像素定位与纹理过滤／binding／blend恢复。普通界面层级用颜色和间距表达，取消强制§l；保留作者文本及用户对话背景透明度合同。GuiDgrTextField保留原版编辑、滚动、光标和选择状态，绘字使用单次无阴影绘制；指名搜索、诊断玩家名和留声机输入框全部切换。运行时提示浮层与提交选择按钮不再绕回原版绘字。选项和任务行使用实际像素对齐字号，剧情图先结束几何zoom矩阵再画文字，标题采用可兼容像素网格的字号与完整换行。三种主题的普通工具表面改为不透明，阻断底层HUD透字。

原生C检查覆盖诊断页、输入全选／删除、物品／实体指名器、留声机、普通Studio单包的Title／LINE／History／Choice、125%选项／任务、三主题与分数zoom剧情图。3B466509检查点中这些界面未见用户截图的重复笔画；后续修复只改通知卡片主类及内部类，其余界面class与最终候选逐字节一致。F的普通单包Title／会话／Choice是真实业务链，新增Task尚ACTIVE／未领取奖励；本地HUD三卡夹具单列，不冒充业务网络路径。指名器原生解绑／重新绑定后两玩家原组包Kill仍SETTLED2、Zero仍ACTIVE0、XP／收据均0；最后实际保存与停服断言通过。最终字体候选客户端另通过20次跨维度及实际死亡／点击Respawn，保存的run／进度／奖励台账保持。

22:23原生短窗口恢复到三张通知时，发现新补入卡片尚未排版就被绘制，触发TaskNotificationCards空指针。已将准入／过期／缩减放在排版前，排版后只调整位置、不再补入新卡片。新增确定性回归覆盖一／两张→三张、FIFO、排队更新在首帧可读和完整阅读时长；原生连续12次短／普通窗口切换通过。22:38另一次退出的明确栈位于测试driver的HUD JSON reader：测试脚本直接写文件导致读取到空JSON，改为临时文件完整写入后原子移动。此夹具不在生产JAR中；两次失败分别保留，不算人工关闭，也不混称为同一Runtime崩溃。

最终Runtime SHA256 **2629CA06F85C1114438637EC8EF5F111E935827FF4FED041E167534D2FB6DD48**，1891058B。与4A比较共31个UI class变化，其他条目完全同字节；生产JAR无本轮Probe／driver。build与完整39项Probe均exit0，FontPolicy扫描693个生产class，只允许统一helper中的一个原始FontRenderer绘字入口，防止未管理的阴影／文本框／按钮重新进入。GUI1／2／3、Unicode、125%／150%以及缩容排队的实际画面已复验；GUI3／125%的完整图来自原生F2渲染缓冲截图。GL记录nearest=true、filtersBindingBlendRestored=true、linearRestore=true。证据及准确检查点归属见evidence/0402-font-native-evidence.json。

同一客户端单调时钟下50次实际诊断查询正确回应，p95 199.7119ms／p99及max199.8158ms；请求号逐条匹配、错误为空，达到默认交互门槛。计时从请求编码前到对应回应被界面接受，不把物理鼠标按下之前的时间算入，也不相减服务器时钟。此批客户端为3B检查点，诊断／网络class与最终候选一致，记录见evidence/0402-native-interaction-50.json。

第二轮MixedAuthored的60min／72000tick、4逻辑用户／12混合Task、69120队列操作独立台账通过；实际网络峰值2，客户端只在其中部分时段在线。该轮使用6455840D，full p95 5.2008／p99 143.6371／max823.2539ms，构建与其他原生测试共跑，属于持续正确性／压力记录，不作正式参考门禁。3B冷启动同一世界、3600tick复验及两原生玩家冷读旧run／XP7／收据2通过，full10.0868／166.4161／826.9463ms同样是共跑短样例。所有阶段原始CSV已分别保存，不能把冷恢复输出覆盖成60min结果。4A至最终候选的服务端业务字节相同，继承这些S层正确性结论；不称历史进程运行了最终2629字节。

最终2629正式参考对照已启动：固定golden driver A465D83A，基线0.4.0.1的569E9705，100 ACTIVE／4逻辑用户／20节点／512MiB／seed40201／0真实联网玩家；基线与候选各三轮交替，每轮5min预热＋10min测量。所有隔离客户端和其他测试世界先正常退出；正式轮次期间不运行构建、Probe或其他游戏场景。完成结果尚未出齐，不提前标整版PASS，Studio权威交付字节保持。

23:45复验准备：首组正式基线p95/p99/max为75.8900/86.5268/132.7947ms，首组2629候选为4.8543/7.1143/56.7980ms，12000个完整tick且无≥250ms峰值，候选达到原冻结门槛。第二基线为77.0589/88.8883/174.1735ms，第二候选仍在运行；第三组尚未结束，不能提前报告三组通过。

诊断截图中有效Task被显示为“资源已缺失”属于独立的只读投影错误：当前task_resource_id、session_resource_id及wait_resource_id写入structured compound，诊断仍getString。源码已用现行ResourceAddressNbt读取合法compound，原始字符串仅作为旧有容忍诊断的可读字段保留，不进入运行恢复；坏compound仍留结构异常，查询不写NBT。新增真实Task/Session codec生成数据及等待名称回归，验证合法资源、损坏kind和源NBT不变。尚未构建；正式对照继续冻结2629。完成全部六轮后构建该修改并跑42项Probe，随后按JAR条目证明只读诊断与测量组件的差异边界。

剩余原生测试只准备测试源与脚本，尚未冒称执行：两份当前合法、各有连通双成员的generated Flow组夹具用于真实F/G登录pending与在线周期恢复，禁止直接调用Forge recover入口；正式CNPC受害实体及另一个未绑定CNPC用于组任务和原生转移确认。夹具生成严格保留协议producer字面量、连通组及当前schema，generated来源在名称和receipt明确披露，不修改正式reader。新分段计时只在隔离driver，准备区分测试断言、测试I/O与业务调用；现行golden driver A465不替换。

维度数量纠正：既有20次单向切换等于10次往返，未达到规格的20次往返。Acceptance保留PARTIAL，最终准备40次切换复验。历史20次记录保留，不改数字凑通过。人工听音、NpcIdentity损坏专项和历史开发CNPC映射状态继续保留原分类。

### 正式对照完成及最终诊断复验（2026-10-10 01:06）

冻结2629的三组交替正式对照已完整结束，均5min预热＋12000完整tick／10min测量，golden driver A465未改。候选p95分别4.8543／4.8406／4.9846ms，p99 7.1143／7.1858／8.7407ms，最大56.7980／61.3604／59.9040ms，无≥250ms样本；基线p95 75.8900／77.0589／75.8901ms。原始样本hash、p50、内存和队列汇总见0402-final-reference-metrics.json。其他探索与MixedAuthored的≥250ms峰值完整列于0402-exploration-metrics.json，其中phase100同步业务提交／奖励占用仍需单独定位，不能笼统归因GC或测试I/O。整版继续IN_PROGRESS。

诊断structured资源投影已完成。首次回归的旧DynamicContentText烟测夹具还写字符串item_id，改为当前合法compound；此处仅修测试，不放宽reader。随后42项Probe全部exit0，spotlessApply及build通过；中间的烟测失败与Spotless失败日志分别保留。最终Runtime为3756B66F2D81118960FC57FC1E7AE6902577B385CDC89F91A9D43E0BE0FEC178／1891641B。与冻结2629只有4个只读诊断class变化，所有字体UI、tick、队列和业务条目同字节，正式对照只按组件边界继承；来源清单见0402-final-source-provenance.json。

Pending-Plain-R2使用同一3756 Runtime及测试driver A730，正常保存重启后两名真正原生客户端完成首次target恢复与已有target保持；在线建立pending后由生产周期入口恢复，不直接调用Forge recovery。两段220tick稳定检查维持target、离线无关对象及clean dirty，最终tick997／phase3／peak2 PASS并正常停服。两份包是明确标注generated的现行合法连通Flow组，非Studio普通作者导出，详细证据见0402-pending-final-native.json。此前CNPC同时连接出现Forge handshake／原版Minecraft reconnect异常，未作责任归因；另一次纯Forge尝试因测试driver writeToNBT MCP/SRG映射不匹配退出，已用反射修测试，旧失败保留。

最终3756诊断页的真实查询分别由ClientF和ClientG各执行50次，全部响应id匹配、loading正常结束。F开启字体GL诊断，p95／p99为149.9064／151.2812ms；G未开启字体诊断／samplingProbe开关，p95／p99为150.1056／150.6052ms，均达到交互门槛。合法Task名称及等待内容正常，不再误报资源缺失；Story和Task密集详情原生截图未见此前重复笔画。各40sec稳定诊断页帧间隔，F2431样本p95 16.8352／p99 16.9742／max18.0804ms，G2400样本p95 16.7833／p99 16.8869／max17.5834ms，无≥250ms；包含限帧／vsync，不作媒体冷热或相同场景基线比较。截图与原始csv见0402-font-final-diagnostic.json。

01:00补充验证脚本误将op请求的user参数写成player，触发测试夹具Owned native profile断言；隔离服务器自动保存退出。完整crash／日志／失败文件保留为native-final-script-parameter-FAIL.txt，并已正常恢复为native-final-R2，G原Task activation／进度保持。修正脚本后G的无字体诊断开关查询和采样通过。此错误在测试命令与driver，不归类为生产字体失败；旧字体证据不被覆盖。Studio权威程序和Data未改，未创建或发布.2成品包。

### 最终原生边界与测试夹具收尾修正（2026-10-10 02:02）

3756普通组包的两名原生玩家分别完成2次真实EntityCustomNpc击杀，第二Session的条件Choice以及零结算Task ACTIVE0，XP／库存／收据均0。测试NPC原本为Friendly，实际CNPC GUI改为Aggressive后真实鼠标伤害被接受；未用Villager替代。实际指名GUI占用提示、转移确认、解绑及重绑通过，源宿主保留组身份。R2解绑后的即时server快照落后于GUI，原样保留，不把它作为S解绑断言；R3等待观察刷新后补齐S结果。宿主更替夹具此前直接改NpcIdentity一张表，GUI转移过的目标会残留另一表选择；改为生产NominatorService双表准备后，新旧实体暴露的身份及双方Task业务字段通过。此处修测试准备，不改生产身份操作。真实死亡／按钮重生、40次单向维度切换＝20次往返及原Task字段保持通过；普通保存停服后两原生客户端冷读也保持。详见0402-cnpc-final-native.json。

最终ClientC同PID42848、Runtime3756、driver C0D4经菜单完成20次WorldA/B切换／21次加载，当前世界的独立运行号、台账与索引归属断言通过，随后正常退出。10次最终X/Y连接切换也完成，Y无旧DGR会话界面。实际源X先有7个在途与三个生成包lease4/3/3；断开后释放worker，归零在途与队列，各source lease回1。第一Y连接超时，成功重试在release之后，故此批只证明退出释放及十次替换，不证明Y已经加载后才release的最终组合。目标超时及两次控制脚本收尾判断错误保留，未强归因生产网络栈。详见0402-world-final-native.json、0402-network-final-native.json。

final3756-clean-profile在12000tick收尾拒绝：测试脚本未在当轮发作者链finish，严格的Fresh native authored assertions未通过。所有中途四逻辑用户与两原生用户的已观察字段保留；原native单包XP7／receipt2／run一致。此批没有输出完整分段缓冲，不计完整性能／验收PASS。R2又在初始化发现逻辑用户库存与独立台账不一致：上一失败轮的cycle先在内存准备3个物品并发布新台账，模拟实体不在服务器注册玩家清单，收尾断言提前退出而未checkpoint它们。问题位于测试夹具保存顺序。保留失败世界及profile阶段日志／failure，不通过改台账或补物来伪造冷恢复；这份失败测试世界不能直接复用于通过性重启。

测试driver现在在每轮台账发布前checkpoint这四个测试实体，并在验收失败时导出可用采样。修改不进入生产JAR。driver build通过；新的Final-mixed-driver-checkpoint-validation隔离世界分别1200tick运行与1200tick保存重启，四用户run／库存／XP／收据／状态独立断言及cold恢复均PASS。第一轮full p95/p99/max 3.1449/77.2163/414.4798ms，第二轮2.4214/75.9678/235.6917ms；是夹具回归／非参考档，reference_gate=false，不能当成原10min／旧长时峰值关闭。旧≥500ms峰值分段归因、最终Y已加载后在途release，以及相同场景客户端基线／媒体冷热仍未闭合。原人工听音／历史搁置分类保持。

当前生产Runtime仍3756／1891641B、Font/GUI条目与已实测冻结检查点相同、build及42项Probe对应的生产源不变。最新源码输入清单记录测试driver变化；Studio权威交付及Data不变，无.2成品发布。所有本轮拥有的游戏客户端已用菜单正常退出，测试服务器均已停服。

### 收据累计测量与最终迟到回调（2026-10-10 03:58）

低收据的Final-mixed-driver-checkpoint-validation十分钟／12000tick完成：p95 1.1235ms、p99 70.6204ms、max241.1484ms，正确性／队列断言通过，没有≥250ms；不是正式参考门槛通过。随后审计历史混合世界的独立台账和实际player NBT：Candidate-mixed-authored-60-valid曾被后续中断轮次复用，最新文件不再对应其早先冷读PASS；Final-mixed-authored-60-resumed也存在未保存的夹具库存差异。两者不通过修改台账重判PASS。累计测量另建隔离副本，只从实际玩家文件登记起始XP／全部收据，再经正常reset/start建立新工作负载；明确cold_restore_asserted=false，既有收据未删除。

新的完整tick分段覆盖journal读／gzip／fsync、checkpoint写／读、Forge player-save回调及fixture I/O；父子范围相互包含，禁止相加。R4首次准备因测试代码experienceTotal的MCP/SRG映射失败，改用反射。R5保留初期565.1445／533.9927ms业务尖峰，之后物理宿主提交断言失败；夹具宿主落点从y5改为实际地面y4，新增宿主dead／health／actor／visible失败证据。没有把未经证实的具体死亡原因写成生产结论。

生产收据校验由反复编译正则改为同语法逐字符校验；SHA256输入字段及长度前缀不变，十六进制转换改为直接字符编码。失败隔离、累计收据、gzip NBT schema、FD.sync／原子替换和玩家写后读回验证全部保留。D12 Runtime／1892301B对应build及42项Probe通过（AfterReceiptValidation），包含10000随机字符串及每个位置的非法字符与冻结语法对照；生产JAR扫描694class、唯一受管绘字入口。

Final-mixed-authored-cumulative-R6／finalD12-cumulative-profile-10独占隔离服务器、12000tick、四逻辑用户十二混合Task，队列11520/11520全执行，逐tick独立库存／XP／run／进度／状态／累计收据断言通过。两个真正客户端只验证此前原生作者链的恢复（各XP7／receipt2），没有声称本轮重新跑过其完整链。完整tick p95 7.778ms、p99 196.8182ms、max1451.8031ms。≥500ms共8个：初始化1416ms，以及最初约65秒的7个四用户同tick同步提交／奖励批次；其中task commit合计452～513ms、journal gzip53～66ms、checkpoint写238～296ms（内含Forge回调81～153ms）。余下约8m55未再达到500ms，但全部120个≥250ms和初期失败门槛保留，不以热状态平均值关闭。数据见profile-Final-mixed-authored-cumulative-R6-finalD12-cumulative-profile-10.json。

同一R6正常保存停服后finalD12-cold-3严格冷读3600tick通过，四用户初始库存3／XP6020／receipt1720分别匹配保存台账；双方真实联网恢复原作者链不变。p95 8.5409ms、p99 304.4462ms、max998.2184ms，37个≥250ms、3个≥500ms仍单列。原生世界60秒帧样本3600个，p95 17.1999ms、p99 18.7271ms、max20.7125ms，无≥250ms；这是混合运行期间的观察，不是客户端基线对照。

D12的最终晚到回调R3已通过：X的5个真实在途与source lease>1保持，先经实际菜单连接Y并确认世界／连接／GUI已加载，再释放Xworker。Y身份不变、GUI空、running bundle0／texture0，X在途0／队列0／全部13个source lease回1。原始JSON与生成包manifest复制至evidence/final-late-D12。首次R2脚本因嵌套作用域的GameInputHandle不匹配被前景保护拒绝，finally释放任务导致包已热，故重新生成R3媒体内容，未复用其热缓存伪造在途。辅助Final-Key显式传入已校验句柄；未绕过前景检查。旧目标端口参数误用也保留，并给AuthoredServer加入server.properties端口一致性检查。

相同世界副本／配置／媒体的0.4.0.1与候选帧对照开始。基线第一次因测试驱动调用仅.2提供的storySnapshots接口崩服，栈及世界保留；基线兼容路径跳过该额外诊断列表、队列指标为兼容占位，不作基线队列覆盖证明。正式.1 Runtime字节未改。修正后冷媒体60秒3596帧p99 16.9763ms、max176.1064ms，热重放60秒3630帧p99 16.9528ms、max23.1473ms，均无≥250ms；持续热状态和候选比较仍在进行。

后续代码准备改为BEST_SPEED gzip写出同一NBT格式，以降低日志压缩；在快速编码超过原8MiB压缩准入限制时回退原压缩方式，避免改变已有准入。增加原gzip恢复、vanilla读快gzip及2048累计收据完整性回归。测试驱动允许保留完整冷／预热采样后单独统计测量窗口，并提供明确记录的提交间隔参数；默认仍为原四用户同tick批次，不能把分散事件与原突发当作相同输入。上述新修改此时尚未构建，不能声称修复已验收。Studio程序／Data未改，没有.2成品发布。

### 2026-10-10 05:04：交易日志和最终客户端帧收尾

最终 Runtime 1B764E（1896766 B）构建及42项现行Probe通过，原始命令、退出码和日志保留于 `.tooling/0402/Final/AfterJournalScope`。TaskReceiptKey 改为逐字符检查原64位小写hex合同；收据计算仍保留原SHA-256输入和长度前缀，只去掉String.format。交易日志沿用schema、原子替换、FD.sync和玩家checkpoint，使用BEST_SPEED gzip；超过原8MiB边界时回退原压缩实现。最近256份日志缓存仅保留UUID、已验证文件SHA和receipt，每次仍读取文件原始字节；变更字节必须重新严格解码，不能靠mtime或长度跳过损坏检查。JournalContext包含规范化路径和journal，恢复标记跟随journal身份；服务器停止清理两张weak表。vanilla兼容、2048累计收据、同长度同mtime坏文件、缓存上限与驱逐后恢复均有当前字节回归。

同源世界、相同故事包和渲染配置的一组原生基线／候选帧比较已完成（VSync开启、renderDistance=2、guiScale=2）。冷加载最大帧间隔基线176.1064ms／候选168.3663ms；热重放23.1473／27.7727ms。5分钟预热后实际累计帧间隔均超过600秒，p95基线16.8057／候选16.8158ms，最大27.0923／19.5318ms，无≥250ms。此证据仅覆盖已观察场景，不能替代三组正式服务端参考或高渲染负载。候选停止后journal/recovered context均为0，未依赖GC估算。原始帧CSV、元数据和hash见 `evidence/0402-final-native-frame-comparison.json`。

测试夹具复查发现旧mixed在单个END回调直接处理4名用户的提交＋奖励，绕过生产队列预算；旧D12累计峰值保留为直接夹具压力。新默认在相同phase=100同时投递4份业务到生产serverScope，由原64项／2ms预算执行，保留FIFO和逐tick独立业务台账；不通过错开到达或减少目标改善数字。短测首次误用MixedLifecycle，把已有作者世界的7-story快照替换为3-story，离线原生玩家保存的journey资源缺失而初始化失败；日志及隔离世界完整保留。这是测试输入不相容，修正为MixedAuthored加载原包。最终60分钟及随后严格冷恢复仍待本轮实际结束，不提前填PASS。

### 2026-10-10 05:32：最终原生交易与来源核对

1B候选真实原生G重新经CNPC、Title/LINE、Choice、区域、collect和实际提交完成普通单包。初次提交右键没有命中绑定宿主，库存2、收据2和ACTIVE状态保持；从侧面再次真实交互后库存0、XP7→14、收据2→4，新Task run1791580077810。F的原Task run1791545476933、XP7/收据2保持。图片和server原始状态保留于Final1B-queued-authored-60；此项是当前1B原生交易，不以74旧重放替代。

新默认提交入口短测完成4800tick（1min预热＋3600测量），每tick独立库存/run/XP/收据断言、实际玩家实体更替、重复/不足量和FIFO通过；最大347.5418ms，无≥500ms。18个≥250ms测量峰值均位于每200tick夹具强制重启12条业务及检查点/存储/日志阶段，完整scope已保留，p99 144.5433ms属于累计收据压力而非正式参考。最终65分钟新运行已启动，尚未结束。

来源审计确认复制世界的level.dat实际RandomSeed=40202，server.properties亦为40202；旧runner输入seed=40201仅是默认命令参数。原始输入文件不改，新增observed-world-inputs.json记录两者。MixedAuthored实际固定3条Story×4逻辑用户＝12Task/周期，runner的Active=10、Nodes=20用于其他场景，不能当作本场景真实规模。未来runner输入额外保留configured_level_seed及mixed_tasks_per_cycle。

ST20补测入口仅放入test driver：从真实CHOICE捕获合法action，原生UI正常推进后，经真实DialogueNetwork CHANNEL延迟发送有限重复请求；不直接调用服务端handler、不给生产GUI加入口。当前长测仍运行冻结84AB driver，新增夹具及空闲保存scope将在长测结束后构建和复验，不混淆已启动进程的hash。

### 2026-10-10 07:14：冷恢复热点、异常停服及当前字节验证

1B的65分钟混合运行完整结束，78000总tick／72000测量tick，76440个作业全部通过生产有界队列执行。两个原生客户端持续在线，F XP7／收据2、G XP14／收据4保持；正常停服journal与recovered context均0。测量p95 10.6138ms、p99 159.8777ms、最大403.5524ms，360个≥250ms、无≥500ms。尖峰位于每200tick测试夹具强制重启12个Task和checkpoint／save／log，完整scope保留，不将该累计收据压力称为正式参考通过。预热另有1398.7／1441.9ms初始化与640.6ms早期批量保存峰值，未从原始数据删除。

随后1B严格冷恢复6000tick通过正确性，但首次phase60提交出现3449.08ms同步耗时；独立复制正常检查点复测仍有2442.97ms。最初诊断脚本提前在heartbeat60启动客户端，尚未等业务回调结束，因此当时“未复现”判断撤回。再以客户端启动前的线程栈确认：String.equals→ArrayList.contains→decodeTerminalRoutes→Session bind→Story onTaskSettled。约9768条合法终结历史的逐项重复扫描构成二次复杂度，不能归因GC或只解释成夹具初始化。生产reader改用临时HashSet检查重复，继续按原输入List保留顺序；terminal与pending两组都适用，不删历史、不变schema。新增60000条（40000 terminal／20000 pending）往返、顺序／源NBT／dirty及两组重复拒绝回归，decode约46.13ms。

诊断过程中一次测试finish在队列／媒体未排空时抛断言，实际Forge崩服跳过FMLServerStoppingEvent，留下10个journal／10个recovery引用。生产代码将现有停止清理提取为同一幂等方法，并由FMLServerStoppedEvent兜底调用；不吞原异常。8B隔离受控LinkageError验证在崩服前8个journal、崩服后两表均0，异常和Forge强制STOPPED日志保留。新8B同一PID46760经真实菜单两次A/B切换、三次加载，各世界独立run／台账／索引通过，随后正常退出；此前3756的20次切换证据按原hash保留。

最终生产Runtime **8B416C270C017873D5D18F96CBB5CA89F67EC88643809731E185776BB62043FC／1896860 B**，与1B仅主Mod与Session NBT codec两个class不同。AfterLinearHistory build／42项Probe通过；698个生产class、唯一受管原始绘字入口、无测试driver进入生产JAR。相对2629的组件继承边界和源码清单见0402-final-source-provenance.json，存储和停服由当前hash复验，不冒称历史进程加载过8B。

当前8B真实CNPC普通作者链复验完成：F XP21→28／收据6→8、两苹果仅扣一次；真实CHOICE捕获后延迟60个客户端tick，通过实际CHANNEL在ACTIVE与SETTLED分别发3次过期响应，六次均回应，无等待挂起、重复奖励或G变化。证据0402-linear-native-packet-replay.json。8分钟混合9600tick、两真实客户端断言及正常停止清理通过；p95 11.7076ms、p99 171.0624ms、max804.4247ms，49个≥250ms、3个≥500ms保留，仍为累计收据压力。

原生测试失败另存：观察JSON直接写时遭Windows读文件共享冲突，改为临时文件原子替换并有限重试；NPC先前未进入客户端追踪、Villager挡住提交点、源NPC每tick锚定使临时移位无效，改为客户端连接后经正确双表服务重建宿主并清理隔离区测试实体。1.7.10客户端实体UUID与服务端不同，命中断言改用实际共享网络entity id。一次Forge握手NPE保留原栈，正常返回菜单后顺序重连成功，未归责DGR。期限内未完成的fixture及断言崩服均保留FAIL，不计完整PASS；不改生产准入或放宽真实提交。

8B最终65分钟混合运行已启动（linear-final-60），严格保存台账冷恢复初始断言通过，两真实客户端连接，G另一次原生普通链XP14→21／收据4→6通过，F28／8保持。当前仍在运行，最后60分钟、退出重连、随后冷读与空闲检查尚待真实结束；此条不提前宣布整版完成。Studio权威交付未改，未创建.2推送、标签、成品包或Release。

### 2026-10-10 07:39：Windows测试输出竞争修正与重新长测

Final-linear-closed-60的linear-final-60在17210tick后测试心跳原子替换遭AccessDeniedException，Forge异常停服；维护两表均0，保留FAIL与完整栈，不计完整长测。第一次只给Authored写入加重试的短占用验证，又定位Mixed测试夹具直接写同一heartbeat的FileSystemException。统一Authored／Mixed／普通Server三处输出为测试专用Stability0402TestFiles：先写临时完整文件，再原子替换，至多19次1ms等待，持续失败继续外传；完整tick采样包含重试耗时。原生只读采样器改用允许删除共享的FileStream读取。生产Runtime始终8B同字节，未修改用户数据／业务校验。

Final-linear-sharing-pass真实Forge双客户端2400tick及所有业务断言通过；主动400次短占用（每次请求10ms）触发19个替换重试，全部恢复。Final-linear-sharing-bounded主动占用2秒，明确耗尽有限重试，测试失败并停止、维护引用归零；是预期失败边界，不算业务PASS。证据0402-observation-file-sharing.json，首个修补失败也保留。运行这些短验证时不宣称其峰值可作独占性能比较。

最终服务器driver655B775B841B0F025690D93C3BBC527D212309B6F003A485BA340701C14FC557；已打开的两客户端driver为CE1DB733695E969C08D9C8BFE63F9AFDF2A741D7554A1BB3398FAC435F250F0D。逐entry比较只有服务器输出夹具及关联内部类5个entry变化，观察／packet／frame／core全部同字节，客户端不冒称加载过655B，见0402-final-client-driver-component.json。最新driver build通过，当前来源指纹重新记录；42个Probe覆盖的生产字节及Probe实现未改变。

新最终世界Final-linear-robust-60从Final-linear-session-60正常停止检查点独立复制，全部TestWorld文件和独立台账哈希验证，不复用17210tick失败现场，不补库存改台账。5min预热＋60min测量正在进行。两名玩家都在本轮经真实NPC／LINE／Choice／区域／背包／提交完成普通单包：F21→28XP／6→8收据，G14→21／4→6，两枚苹果各扣一次。G首次实际交互未提交时仍ACTIVE／库存2／奖励不变，保存原现场后第二次真实交互结算一次；未重置任务绕过校验。新首次phase60回调68881600ns，其余三名约8～10ms；这是当前观察，不提前宣布整轮完成。

### 最终开发交付闭合

当前8B最终78000总tick／72000测量、76440生产队列作业完成；正式采样p95 10.6380ms／p99 177.8476ms／max 607.8875ms，全部scope尖峰及预热保留。受控G原生菜单退出后N=1，再登录N=2，F连接不变、G新连接的业务台账保持。随后6000tick严格保存重启p95 12.0691ms／p99 198.2444ms／max 653.2519ms，两原生玩家和四逻辑台账初始值、run／库存／XP／收据／进度保持，停止两张维护表均0。当前8B 10000静止／离线空闲复验p95 0.2170ms／p99 1.2725ms／max 44.6508ms，正常停止通过。build及42项Probe、完整源码指纹、Studio权威EXE当前核验与全部公开证据汇总闭合；状态DEVELOPMENT_COMPLETE_WITH_RECORDED_LIMITS。原人工听音NOT_RUN、NpcIdentity损坏专项DEFERRED_BY_USER及开发CNPC历史BLOCKED保留，没有本版远端或成品发布。
