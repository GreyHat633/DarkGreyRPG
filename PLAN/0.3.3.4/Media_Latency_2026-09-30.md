# 0.3.3.4 媒体延迟定位与修复（2026-09-30）

状态：IMPLEMENTED_VALIDATION_PARTIAL_NOT_USER_ACCEPTED。USER_ACCEPTED=NO；RELEASE_READY=NO。
本轮只修改 Story 媒体路径及默认关闭的诊断；NPC 名称样式、任务按钮、Gramophone 不在本轮范围。

## 已确认原因与修复

1. 服务端旧路径每发送 32 KB 都经 materialize 重新校验整个文件。改用有界的 MediaTransferReaders：同一请求复用打开的顺序读取实例，一次完整校验，持有 generation lease；最多 64 实例，15 秒空闲过期，完成/失败/断线释放。取消后没有新增取消报文，遗留实例通过空闲超时释放。每次请求仍在 IO 前后检查授权，报文和大小边界不变。
2. 失败退避占住包内文件任务位置。改为跳过退避项，先选当前显示需求，再选后台资源；每包仍只有一个文件任务。失败项保留未完成，5 秒后重新具备重试资格，不能让整包冒充 Ready。
3. 解码完成后原先进入通用主线程队列，多个纹理可能集中上传。独立上传队列每个客户端 tick 最多上传一张；解码前预约数量及像素预算（最多 4 个、16,777,216 像素，按解码上限保守预约），预算满则保留需求稍后重试。图片尺寸上限、通用网络队列不变。
4. 解码前和上传前复核连接 generation 与当前显示需求；读文件期间独立 pin，过期结果 flush 并归还预算。保留原有 30 秒闲置纹理回收及有限纹理缓存，不扩大到全故事常驻。

诊断以 JVM 参数 `-Ddgr.mediaTrace=true` 启用，正常默认关闭。记录 frame_received/first_draw、文件检查/命中/准备、网络请求偏移、reader_open_verify、解码排队/耗时、上传 tick/耗时、丢弃和纹理释放。故事/帧与媒体通过 frame_media 关联，传输通过请求 ID 关联；at_ns 仅适合同一 JVM 内比较。audio_prepare 只是播放命令入队耗时，不代表扬声器实际出声。

## 可重复的性能结果及边界

测试使用隔离 Server/Witness/WitnessLocal，独立 media-latency-world/MediaTest。四张确定性随机 1024×1024 PNG（单张约 3.15 MB），一段 120 秒 OGG（190,101 字节）。均由隔离 Studio 工程导出，不修改正式故事包或用户存档。Windows 实机输入、窗口与截图仅用 UIA/Win32。

| 场景 | 观测结果 | 证据边界 |
| --- | --- | --- |
| 顺序读取微基准 | 3,148,927 字节 / 97 块，旧路径 2086.0006 ms，新路径 28.4089 ms | 两者都用已物化的暖磁盘；不是网络总耗时。旧理论校验量 305,445,919 字节，新路径 3,148,927 字节，按实际调用次数计算，非底层 IO 计数器 |
| 联机故障资源 | 02:33:03 首图进入失败退避；02:33:04 音频完成，02:33:05 开始第二张图片 | 临时独占锁住隔离服务端文件；证明失败项不再阻塞后续项；直到所有 5 项完成才 package_ready |
| 联机冷缓存 | 修复后单张 3.15 MB PNG 仍约 24 秒 | 32 KB 停等传输反复经过客户端/服务端 tick 与网络往返。重复校验修复没有消除此吞吐瓶颈；未修改传输协议或并行窗口 |
| 联机整包 Ready 后重新启动 | 没有新网络下载，四图按需解码/上传总排队约 78～272 ms | 同一 JVM 实际操作；并非所有网络/硬件场景 |
| 单机冷缓存 Start | 443.364 ms 整包 Ready，5 次 local_import，0 次 network_chunk | 本地磁盘准备，图片尚未解码；进入世界前没有该故事媒体预加载 |
| 单机 Ready 后首次四图 | 上传 tick 1146、1147、1148、1149，第一张约 126 ms，最后约 271 ms | 单张上传 25～30 ms；后台解码及 tick 预算仍会让多图逐张出现 |
| 单机隐藏超过 30 秒 | 02:41:43 隐藏，02:42:13 回收 4 张纹理，02:42:22 再显示；0 次重新下载 | 磁盘缓存未删除；重新解码/上传约 77～225 ms，属于当前保留策略的正常代价 |
| 快速切换 | 02:43:12 切至空画面后 2 个结果 decode_discard，没有继续上传；再显示恢复这 2 张 | 原生两次点击；截图为空画面，随后四图恢复 |

单机本轮 8 个对话帧：接收到首次绘制 3.424～14.122 ms；14 次解码 42.114～56.499 ms；12 次上传 24.727～36.083 ms。记录到一次超过 50 ms 的绘制间隔（50.928 ms）。这些是本轮样本范围，不是 FPS 分位统计，也不能证明正式整合包中不存在其他主线程停顿。

旧 JAR 基线运行有自动播放及重连中断，因此不报告其全包总耗时或严格同条件帧间隔改善比例。原始日志保留在 `.tooling/0334/MediaLatency`；当前证据不支持“整体快了约 70 倍”或“绝对零延迟”。

## 验证与证据

最终 `assemble` 与下列 7 项生产路径探针均通过（见 evidence/media-latency/final-verification.log）：
- mediaLatency0334Probe：实际旧/新读取、payload 相同、完成/取消/15 秒超时释放、64 实例上限、旧 generation 持有到最后 reader 释放。
- mediaQueue0334Probe：失败退避跳过、当前需求优先、每包单文件、预算数量/像素有界和复用。
- storyMediaCacheIndexProbe / storyMediaServerProbe：3 包并发、10 包名额、受保护等待、共享、整包 Ready、Start/关联图等既有规则。
- mediaTransfer0330Probe：传输边界、乱序/重复与完整性、活动 pin。
- dgrsMediaStreaming0331Probe / dgrsMediaLifecycle0331Probe：流式物化、并发/更新 lease、重启重校验、损坏恢复、generation 回收及非全量 Heap 驻留。

实机截图位于 evidence/native-ui：media-baseline-four-images、media-baseline-hidden、media-baseline-repeat、media-fixed-loading、media-fixed-four-ready、media-fixed-warm、media-local-four、media-local-hide、media-local-after-30s、media-local-fast-switch、media-local-demand-restored、media-local-final。
精简诊断、缓存记录、汇总和源文件哈希位于 evidence/media-latency。早期基线无开发诊断，baseline 的空 timing 文件不代表执行过计时。

隔离服务器已停；原 server/witness 启动参数、server.properties、隔离 Native0334.dgrs 和原测试 MOD 已恢复并逐字节核对包/参数。客户端 CloseMainWindow 后仍有无窗口 Java 进程，已核对隔离路径后终止。因此本轮不能把客户端正常退出清理称为实机通过；断线/超时/引用回收由日志和探针分别支持。

## 剩余问题与待验

- 冷缓存联机停等吞吐仍明显偏低；本轮不将这项关闭。需要独立验证有界传输窗口或媒体专用传输调度，保持授权、大小和并发约束。
- 正常按需解码及单张 GPU 上传仍有真实耗时；保留既定 30 秒纹理回收规则，所以很久之后同图重新出现仍可能逐张刷新。
- 严格同条件修复前/后帧时间分布、长音频实际出声延迟、实机 3/10 包压力矩阵、解码中断线/正常退出完整矩阵仍未覆盖；不以探针代替这些实机结论。
- 原 C10 长文本/语音/历史组合、C12 提交候选剩余边界，以及 NPC 名称与任务按钮待办均保留。

## 交付

- 权威 JAR：`E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.4.jar`
- mcmod.info 版本：**0.3.3.4**；大小：**1,786,880 字节**。
- SHA-256：`C258400D3BF7CB1232BF687C81878F56EFB4B3DE4E4AC1BC6EF359DD1B49D7C7`。
- Studio 本轮未修改；既有权威 EXE 不变。无提交、推送或用户验收标记。

后续更新：本文“冷缓存联机停等吞吐”已由 [Media_Window_2026-09-30.md](Media_Window_2026-09-30.md) 的有界窗口修复及新实机 A/B 更新；本文数字保留为该阶段记录，最新交付以 Delivery.json 为准。
