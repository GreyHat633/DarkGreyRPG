# DGR 0.3.3.1 工程交付记录

日期：2026-09-14。分支 `codex/0.3.3.1`，基线 `64dd990484a8cf2639013a41a7d4fe960e6b7409`。本次已完成代码集成、正式产物生成、自动回归和下列实机检查；未提交/推送 Git，也未创建 Release。`USER_ACCEPTED=NO`。本文件的最终状态取代工作包文档中的阶段性“待集成/编译受阻”记录。

## 交付文件

| 文件 | 版本 | 字节 | SHA-256 |
|---|---|---:|---|
| `E:/Java/MinecraftMod/DarkGrey_RPG/dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe` | 0.3.3.1 | 141925078 | 60193AAFA8F6F621C275A6290A42F9C147A8D267FC7AF114BB8D647ACAF7E87A |
| `E:/Java/MinecraftMod/DarkGrey_RPG/dist/darkgrey_rpg-0.3.3.1.jar` | 0.3.3.1 | 1164558 | 9D1833897BCD97DD2B6C515BFF965CD5D579F2A21A61ABDF060CC8DA379FA903 |

Studio 为 Windows x64 Release 自包含单文件客户端，已提升到唯一权威目录并重新启动。整个目录中的 media-tools 随客户端保留。JAR 为 reobfJar 正式模组，不包含验收 HTTP harness；原始构建产物在 `build/libs/darkgrey_rpg-0.3.3.1.jar`，与 dist 哈希一致。`DELIVERY_FINAL.json` 与 `DELIVERY_MOD_FINAL.json` 是机器可读清单。

## 实现范围

A01—A25、D01—D10 的代码、测试、实机证据映射见 `AUDIT_TRACEABILITY.md`，节点字段/介绍覆盖见 `NODE_AUTHORING_COVERAGE.md`。

- 节点类型标题、帮助、正常字段内联编辑、水印、执行/指令切换；空执行草稿可从画布新建，严格导出仍拒绝未配置执行。
- 下拉集合稳定、拖动与控件命中分离、撤销后的端口几何刷新；数值拖动/直接输入/取消，以及画面四边四角缩放和图层排序。
- Actor Inspector 内头像差分编辑；台词/音乐共用本地 OGG 播放、暂停、拖进度。实际试听暴露的 TimeSpan 格式异常已修复。
- 任务提交绑定角色和物品，真实 NPC 交互候选选择、分页、一次性票据和距离复核；收集锁存、整数方块区域。
- 项目图谱直接 Flow/Logic 接线；Start 精确流程入口、命名终止出口；schema 6 单次路由及故障恢复，ACTIVE 禁重入和逻辑边沿。
- DGRS 结构索引与媒体流校验、不可变磁盘代际、按需物化与分块传输。两条媒体工作线程、64 排队容量、最多 66 个请求横跨服务器排队/磁盘处理/回发的在途预算。包退役同时撤销会话媒体和标题；坏基础项目重载保留已运行快照。

在线留声机按计划延期。没有新增第二 Runtime，Task 内仍为 Logic；身份域内两处修改仅为新增提交角色引用的校验/重命名，不改变身份语义。

## 最终自动检查

| 检查 | 结果 | 证据 |
|---|---|---|
| Core Release | 446 PASS，0 FAIL，1 SKIP | `evidence/tests/core-final.trx` |
| WPF Release | 522 PASS，0 FAIL，0 SKIP | `evidence/tests/wpf-final.trx` |
| Java 完整 build | PASS，含 Spotless、Checkstyle、reobfJar | `evidence/tests/java-final.log` |
| task0331RuntimeProbe / storyBoundary0331Probe | PASS | 同上 |
| dgrsMediaStreaming0331Probe / dgrsMediaLifecycle0331Probe | PASS | 同上 |
| mediaTransfer0330Probe / sessionPresentation0330Probe / title0330Probe | PASS | 同上 |
| Windows Junction 安装拒绝、清理不穿透 | PASS，使用项目 Java 8 工具链 | `evidence/media/windows-junction-java8.txt` |

历史 B3 迁移测试缺少 `DGR_B4_MIGRATION_FIXTURE` 专用样例而跳过，没有计入通过。先前相关 Story coordinator/server/package probes 的故障注入、重复目标运行身份和循环预算结果见 `WP_D_JAVA.md`。最后一次 Studio 仅清理行尾空白后重新发布，EXE 哈希与已测版本一致。

## 实机检查

使用隔离项目和存档，原用户项目/世界没有被样例覆盖。Windows 操作为 UIA/Win32 真实控件和鼠标；Minecraft harness 的交互操作调用真实 playerController 网络交互或 GUI 鼠标处理，服务器命令仅用于布置样例、传送、补发测试物品和重载。

- Start、目标、奖励、执行各 100 次真实下拉切换：`evidence/live/dropdown-*-100.json`。另有节点切换、文本失焦和撤销检查；四份 100 次计数并不意味着每轮都穿插所有动作。
- 单节点真实拖动、Undo/Redo 50 轮：`node-drag-undo-redo-50.json`。多节点及端口/命中几何 50 轮由 WPF 交互测试覆盖。
- 数值 Changed/UndoExact/EscapeExact 均 true；画面八个控制点逐一改变尺寸并精确撤销；图层拖排可撤销。
- Actor 差分导入、命名、保存、Undo/Redo；OGG 播放/暂停/拖进度。系统回环 WAV 实际录得约 440 Hz 音频，截图和数据见 `ogg-preview-*`。
- 两个实际 CustomNPC+ 实体依次交付两个任务：背包 2→1→0，分别结算；收集目标在背包清空后保持结算。移远后旧候选拒绝扣物；整数区域达成。证据为 `game-first-submit-fixed.json`、`game-second-submit-fixed.json`、`game-stale-distance-rejected.json`、`game-region-settled.json`。
- 0.3.3.1 最终源码 runClient 客户端跨包 Flow 在 18:44:13 输出 `DGR0331_FLOW_TARGET_REACHED`；目标包旧样例缺终止出口造成的失败日志保留，正确样例重载后的成功才作为证据。
- 标题淡入时没有会话帧，完整动画后出现 Session；OGG/头像/画面真实播放。媒体图片中的“DGR 0.3.3.0”是沿用的验收 PNG 内容，实际模组版本由启动日志确认是 0.3.3.1。
- 坏包重载保留旧包；有效更新后的旧会话 GUI、语音、音乐全部停止。卸载/重新安装、退出并重启集成服务器均完成。最终证据为 `media-generation-retired-final.json`、`media-unloaded-final.json`、`media-server-restart-final.json`、`live-0331-final.log`。
- Dark/Light、常用和较小工作区截图已检查。主窗口 1463×942；请求较小 1200×800 时系统应用了窗口最小宽度。宿主窗口 DPI 实测 119（约 125%）。

## 性能与证据边界

8 MiB 与 32 MiB 同条目数媒体扫描，最终探针的大包增量常驻堆为 1336 字节、扫描峰值增量约 1.34 MiB；测量受 GC/采样噪声影响，零/负差值不能解释为没有分配。8 个并发物化请求落到同一路径，缓存重建和代际租约检查通过。

实机记录了堆、工作集、私有字节、1297 个进程句柄、61442 字节缓存文件，以及服务器滚动 100 tick 的 p95/max。一个稳定运行观察窗 p95 约 7.18 ms、max 8.44 ms；这些是单人测试世界的观察值，不能当成多玩家或大媒体负载基准。32 KiB 在途常量属于单块大小；总媒体请求预算由 66 个在途请求限制，不能把单块误报成进程总媒体内存。

以下仍是验收覆盖缺口，不能标记为全部 Gate 通过：100%/150% 物理 DPI 未实测；没有全排列逐字段鼠标操作矩阵；多玩家大包压力、真实磁盘满/断流与修复前后 Minecraft tick 基准对照未完成。JVM MXBean 的句柄数不支持（-1），已补 Windows 进程句柄记录，但不是逐文件句柄归因。Windows 符号链接创建不可用，已补 Java 8 Junction 安装/清理实测。

因此本次交付是已实现、已构建并完成上述回归的工程候选；完整计划验收与用户个人验收仍未宣称完成。阶段性失败日志均保留，带 `final`/`fixed` 的最终记录按上面指引使用。

最后范围复核：已删除 Runtime 为旧测试保留的终止出口回退。终止必须有非空 port_id 和 display_name；旧探针在测试数据构造处明确补齐字段，新增缺出口负测。完整 build 及 Story runtime/instance/server/Forge coordinator/session router/actor arbitration/package loader/title 等相关探针全部通过，见 evidence/tests/java-strict-final.log。

移除回退后再次启动最终源码客户端、恢复独立存档并真实点击会话选项，18:57:49 再次到达跨包 Flow 目标，见 evidence/live/live-strict-final.log 与 game-strict-final.json。实机进程已正常关闭并保存独立世界。
