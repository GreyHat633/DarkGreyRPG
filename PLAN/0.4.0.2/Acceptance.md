# 0.4.0.2 验收记录

状态 **DEVELOPMENT_COMPLETE_WITH_RECORDED_LIMITS**。当前开发交付 Runtime **1896860 B**、SHA256 **8B416C270C017873D5D18F96CBB5CA89F67EC88643809731E185776BB62043FC**；build／42项Probe通过，698个生产class只有唯一受管原始绘字入口。Studio Core510、WPF667／1skip通过，权威dist已提升并保留Data。原施工规格仍为PLAN.md；过程、失败反例和修复详见Implementation.md。最终结果与原始文件hash见evidence/summary.json及reproducibility.json，交付索引见DELIVERY.json。未创建本版源码推送、标签、成品包或GitHub Release。

字体已统一单次无阴影绘制、像素定位和最近邻采样，普通工具页面遮住底层HUD；覆盖通知、追踪、诊断、输入框、浮层、图标签与标题，三主题和GUI1/2/3等原生证据保留。字体／GUI／媒体／tick／队列等组件与已测检查点同字节。正式三组服务端参考测量使用冻结2629，客户端一组帧对照使用冻结1B；后续改变的只读诊断、收据日志、线性历史reader和停止兜底有各自当前hash回归，不能声称历史测量进程曾加载当前JAR。组件边界见0402-final-source-provenance.json。

当前8B最终混合78000总tick（5min预热＋60min／72000测量）、76440个生产队列作业全部执行。两名真实客户端持续观察，仅在明确记录的G退出重连窗口短暂N=1，F连接保持；重新登录后两人库存、XP、收据、run与进度保持。预热中两条实际普通作者链分别F XP21→28／receipt6→8、G XP14→21／4→6；G首次未提交的库存2／ACTIVE原状态保留，第二次实际交互仅结算一次。另一次当前8B原生链捕获真实CHOICE，并在ACTIVE／SETTLED各重发3份实际CHANNEL过期响应，无重复奖励或等待挂起。最终严格冷启动6000tick恢复独立逻辑台账及两原生台账，正常停止journal／recovered context均0。

当前累计收据压力完整tick为p95 10.6380ms／p99 177.8476ms／max 607.8875ms；保存后冷恢复为p95 12.0691ms／p99 198.2444ms／max 653.2519ms；一万静止实例空闲为p95 0.2170ms／p99 1.2725ms／max 44.6508ms。此混合夹具每200tick强制重启12条业务并同步checkpoint／save／log，其压力峰值完整列于profile-Final-linear-*，不是正式参考门槛。预热6000样本、最大523.1905ms及全部尖峰另列warmup文件。冷读旧版可重复2.44～3.45秒ArrayList.contains历史扫描已改为保序线性重复检查，60000条严格回归及真实冷读通过；受控崩服证明停止兜底不吞异常且维护引用归零。历史共同运行、未闭合归因和失败夹具保持原分类，不猜测GC原因。

证据层A为接口／模型，S为实际Forge服务端，N为真正联网客户端，C为原生UI。历史搁置、人工感知和容量边界单列，不相互替代。

## 完成条件

| 条件 | 当前状态 | 证据和剩余边界 |
|---|---|---|
| AC01 接续与合同 | PASS | 原 PLAN 保留；基线、分支、版本与技能记录；未恢复退休 reader 或旧指名配额 |
| AC02 可比较输入 | PASS | 冻结golden A465、100 ACTIVE／4逻辑用户／20目标／512MiB／seed40201，三组独立世界交替对照完整结束；每轮5min预热＋10min／12000完整tick，N=0 |
| AC03 稳定轮询 | PASS（A/S） | ≥200 轮实际四入口，零重复容器恢复；当前42项 Probe 通过 |
| AC04 pending／重载 | PASS（A/S/N） | 同快照、失败隔离、无内容 bind；真实 Enabled/Disabled/Error/Conflict、内容更新、卸载和恢复边界通过 |
| AC05 热点测量 | PASS（已测边界） | 0/1000/10000静止／离线轴及1000/5000 ACTIVE、100/300目标探索已记录；当前8B空闲10000和混合／冷恢复完整scope，初始化／vanilla900tick保存／测试reset及存储压力分开，见metrics.csv |
| AC06 null-first | PASS（A/S/N） | Pending-Plain-R2 两名原生玩家在真实保存重启后登录恢复；在线周期恢复通过，测试未直接调用Forge恢复入口，见0402-pending-final-native.json |
| AC07 幂等 | PASS（A/S/N） | 已有target run保持；登录／周期恢复后分别稳定观察220tick，无重复target，离线无关对象和dirty保持；正常作者单包库存／奖励／收据冷恢复保留 |
| AC08 共用预算 | PASS（A） | 当前格式 Flow 20、环20、深300，受控错误及无关玩家；新增10分支 Logic/双NOT/AND/Flow固定点及100分支共享256预算、冷读/无关UUID均通过；真实世界资格边界另记 |
| AC09 队列预算 | PASS（A/S） | 1024+256 有界 FIFO、64/32 与2ms；受控时钟、Runtime/Error；S 层128/256短突发 |
| AC10 回应与取消 | PASS（A/S/N/C） | 容量/epoch/failure codec；真实诊断/包GUI故障有正确request id并恢复，50次原生查询全部匹配且无错误 |
| AC11 媒体所有权 | PASS（A/S/N） | 真实3并发/10保护/共享引用、在途退出与迟到归零；D12补测先确认Y世界加载，再释放X的5个旧在途，Y世界／连接／GUI不变，X队列0、全部source lease回1；见evidence/final-late-D12 |
| AC12 异常隔离 | PASS（A/S/N） | Runtime清理、Error外传；实际Bridge32次/60s限频及诊断/包GUI注入故障结束请求，后续正常请求恢复 |
| AC13 实体／维度 | PASS（S/N/C） | 最终3756原生40次维度切换＝20次往返，进行中Zero Task及已结算Kill的run/status/progress、Story状态、库存/XP/收据保持；真实死亡/按钮重生、R3双身份服务准备的CNPC更替通过 |
| AC14 同 JVM 世界切换 | PASS（S/N/C） | 3756同PID42848，20次A↔B／21次加载；新增8B同PID46760两次切换／三次加载及正常菜单退出，覆盖新增stopped清理兜底，各世界run/独立台账/索引通过，见0402-world-final-native.json及0402-linear-history-and-stop.json |
| AC15 网络更替 | PASS（S/N/C） | 3756十次X/Y及D12先加载Y再释放X旧在途通过；当前8B长测中实际G退出重连、F连接保持、G新连接和业务台账不变通过；旧超时／握手栈保留，不强归责DGR |
| AC16 两名联网玩家 | PASS（N/C/S） | 普通单／组包各自推进；当前8B两个真实客户端全程观察，单次受控G退出重连窗口明确单列；库存/XP/收据/run稳定 |
| AC17 ACTIVE 梯度 | PASS（探索） | 实际10/100/1000/5000 ACTIVE、1/4/16/32逻辑用户及100/300目标已测；1000/5000压力档部分超参考门槛，不能宣称均达标 |
| AC18 库存／提交 | PASS（A/S/N/C） | 5seed×2000独立业务台账；实际绑定CNPC提交、库存不足不扣、仅一次奖励；8B真实F21→28／收据6→8与G14→21／4→6，两苹果各扣一次，严格保存冷读保持 |
| AC19 正常作者包 | PASS（S/N/C） | 普通UI同字节单包完整链保留；最终3756组包两原生玩家各完成2次真实CNPC击杀、条件Choice、零结算ACTIVE并保存冷读；实际指名GUI转移/解绑/重绑且源组身份保留，见0402-cnpc-final-native.json |
| AC20 包资格 | PASS（A/S/N/C） | 真正Enabled/Disabled/Error/Conflict、内容更新、卸载/恢复及无内容reload通过；错误历史需显式重置，未绕过生产资格 |
| AC21 媒体／帧 | PASS（A/S/N/C） | 11合法生成Story、2×1Mpx图/Story及ogg，真实3并发/10保护/排队/共享释放通过；同源世界／同包／相同渲染配置，1组原生客户端基线／1B候选冷、热及5分钟预热后10分钟采样均无≥250ms尖峰；不是3组正式服务端参考，见0402-final-native-frame-comparison.json |
| AC22 持续运行 | PASS（正确性，压力边界单列） | 当前8B 78000总tick／72000测量、76440个生产队列作业全完成；两原生客户端与实际退出重连台账通过，随后6000tick严格冷读、停止维护引用0/0；性能详见当前profile与warmup |
| AC23 固定种子 | PASS（发现闭环） | 5×2000模型、原生组合／字体12次窗口往返；历史扫描及跳过stopping的新发现已定点修复，60000条、受控异常、真实世界／连接切换与当前最终长／冷运行通过；失败夹具全部保留 |
| AC24 Bridge／诊断 | PASS（S/N/C） | 实际限频、只读投影故障和包reload故障注入；错误对应request id/loading结束，恢复后可查询，Task/Session NBT保持 |
| AC25 正式性能 | PASS | 候选三轮p95 4.8543/4.8406/4.9846ms，p99 7.1143/7.1858/8.7407ms，最大61.3604ms，无≥250ms；基线p95 75.8900/77.0589/75.8901ms。原始样本、p50、内存和来源见evidence/0402-final-reference-metrics.json |
| AC26 最终回归 | PASS | 当前8B build／42项Probe；Studio Core510、WPF667／1skip；698class唯一受管绘字入口，无测试driver进入生产JAR；当前存储／停止／真实交易／过期响应／长时／冷读通过；历史原生项按组件边界继承 |
| AC27 开发交付 | PASS | Runtime build/libs与完整来源指纹、测试hash对应；Studio权威dist已提升且当前EXE核验，Data保留；DELIVERY.json和reproducibility.json可追溯 |
| AC28 如实报告 | PASS | 压力边界、测量版本、warmup尖峰、新发现、失败尝试和历史搁置分别记录；开发完成不等同成品Release |

## 场景矩阵

| 场景 | 当前结果 |
|---|---|
| ST01 真正无故事空闲／仅离线存量 | PASS（S），0/1000/10000轴及当前8B 10000静止／离线复验；正常存档和dirty保持，初始化与900tick vanilla保存scope单列 |
| ST02 在线静止＋离线0/1000/10000 | PASS（A/S），最终服务端组件同字节，零重复全量恢复 |
| ST03 混合 ACTIVE Task | PASS（A/S），10/100/1000/5000与多目标探索完成，压力档超门槛单列 |
| ST04/ST05 实际双玩家 | PASS（N/C/S），F/G单包与组包交错、不同Story独立；各进程hash分别记录 |
| ST06 同提交点并发 | PASS（N/C/S），正常CNPC宿主，F不足量保留锁存且G不变，分别结算，无重复奖励 |
| ST07 pending 恢复 | PASS（A/S/N），真实冷启动双玩家登录和在线周期恢复；已有target保持、无关对象保持、稳定220tick幂等，生成夹具来源单列 |
| ST08 Flow/Logic 预算 | PASS（A），Flow/复杂Logic共同256预算、固定点、冷读与独立玩家已通过 |
| ST09 跨维度 | PASS（N/C/S），3756最终40次切换＝20次往返，双方暴露的Task状态/run/进度、Story状态/库存/XP/收据保持；另有最终LINE/真实Choice各2次单列 |
| ST10 同 JVM 世界A/B | PASS，3756同PID42848菜单20次切换／21次加载；新增8B同PID46760菜单2次切换／3次加载覆盖停止兜底，正常退出 |
| ST11 连接替换／迟到 | PASS，3756十次替换＋D12先加载Y再释放X的5个旧在途，Y未复活X会话／纹理，source lease全部归1；首次目标超时和R2原生脚本作用域失败均保留 |
| ST12 死亡／实体重建 | PASS（S/N/C），3756真实death/按钮respawn及双身份服务准备的CNPC更替，双方暴露的业务字段保持；旧R2夹具单表转移残留单列，不算生产GUI结果 |
| ST13/ST14 重载／包资格 | PASS（A/S/N/C），Qualification-Native-4A-R2；最终服务端组件同字节 |
| ST15 正常单/组包库存与零结算 | PASS（N/C/S），普通导出同字节，单包库存奖励既有链保留；3756最终组包双方各2次实际CNPC击杀，零结算ACTIVE，冷读保持 |
| ST16 突发／异常／慢任务 | PASS（A/S），有界FIFO／64/32/2ms；当前混合78000tick执行76440项，Runtime隔离／Error外传／实际崩服清理通过；不承诺抢占单个慢任务 |
| ST17/ST18 多媒体／3/10保护 | PASS（A/S/N/C），Media-Boundaries-Z实际压力/排队/共享、未就绪推进及基线／1B候选冷热帧比较通过；实际并发3／保护10的资源压力与1组帧比较分别记录 |
| ST19 在途退出／停止 | PASS（A/S/N），源lease/pin/worker释放、共享保留、迟到不复活；最终跨服单列ST11 |
| ST20 抖动／重复请求 | PASS（A/N/C/S），8B从真实CHOICE捕获合法action，原生正常推进后通过实际CHANNEL延迟60客户端tick，在ACTIVE与SETTLED各发3份旧响应，均正常回应、不重复奖励、不挂等待且G不变；仅应用层延迟／重复，未宣称TCP丢包测试 |
| ST21 Bridge／诊断 | PASS，Boundary-Native-4A直接限频、诊断/包GUI故障回应与恢复，未冒称磁盘坏SavedData修复 |
| ST22 60min＋冷恢复 | PASS（正确性／已测边界），当前8B 72000测量＋6000严格冷恢复；真实双玩家退出重连和保存台账保持，全部压力尖峰／warmup另列，非正式参考门槛 |
| ST23 正式CNPC代表链 | PASS（S/N/C），普通单包既有链及3756最终组CNPC击杀、条件Choice、冷读通过；原生GUI转移确认、源组保留、解绑/重绑经C/S复验，R3更替改用正确双表准备 |
| ST24 固定种子探索 | PASS（已测边界），A模型10000步、S实际多用户／任务／世界组合及新发现闭环；输入seed、真实Task数量、进程JAR和driver分别记录 |

## Studio 权威交付

用户已保存关闭后提升到 `E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`。ProductVersion **0.4.0.2**，**204288 B**，SHA256 **8E569CC39547BCA0BB9E537BFC4BD42E048318B5720657735176C8F30CB97829**。410个程序白名单文件与已验证候选一致；2690个Data文件／68773074 B首次与最终提升前后完整哈希一致。个人路径清单仅在 ignored tooling，公开证据为聚合和清单hash。

Core510 PASS／0 SKIP；WPF667 PASS／1 SKIP。普通菜单导出的单包 SHA256 `DD020AE635F999F0D5EED7C4E7AE4DB49F44C5CAEF28C9A73BCFCA7D9B6E695A`，组包 `6474CE58D92B4DC8F102290957B75B741FDA2917DA261A1020A450C5039D11CB`。两者同字节经实际游戏运行；普通作者导出与generated压力夹具来源分开，正式CNPC组击杀／原生GUI转移通过见ST23。

## 保留状态

损坏 NpcIdentity 专项 **DEFERRED_BY_USER**；人工听音等感知项仍 **NOT_RUN**；开发 CNPC 锁定Jar的映射初始化阻塞仍 **BLOCKED（历史）**，正式CNPC环境测试另算。没有发布 .2 成品或修改既有 .1 Release。
