# 留声机与任务完成摘要：继续施工记录（2026-09-17）

当前为施工/工程验证记录，不是 USER_ACCEPTED 或 Release。

## 已接入的代码

- 环境留声机方块、服务端 OP/创造权限及距离/实例/修订检查；加载设备索引，不扫描全部世界方块。
- 每设备个人播放、立方体范围、0.6 秒淡入淡出、红石暂停续播；配置来源未变化时保留播放器。
- QQ / 网易云匿名单曲适配、HTTPS 白名单及受控短链重定向、整曲时长核对、MP3 逐帧解码。
- 本地 MP3 草稿、Windows 原生文件选择框、包络、试听及拖动进度；显式确认后才分块上传。
- 服务端指纹去重、原子配置清单、设备持久所有者、24 小时无引用宽限回收；区块卸载不删除持久引用。
- 客户端独立缓存、活动拥有者、跨进程存活锁、已知残留文件回收；未碰 Story 的 10 包/3 并发策略。
- 任务真实 SETTLED 时产生独立最小摘要；重置保留、重复完成计数、玩家隔离；不改奖励收据与任务 Runtime schema。

## 当前实机事实

Windows UIA/Win32/GDI 控制实际游戏及文件对话框；证据在 evidence/native-ui。

1. 网易云 https://music.163.com/song?id=416892104：取得并完整解码约 347.45 秒。QQ https://y.qq.com/n/ryqq/songDetail/001ES52Q3qN0u7：约 214.10 秒。两首均通过游戏配置保存后播放；WASAPI 输出与参考曲目的归一化匹配约 0.797 / 0.884。只录系统输出，没有使用麦克风。
2. 这是当时 Paulscode 后端的播放证据。多设备通道保护审查发现原版池会复用暂停通道，因此当前改为留声机独立 JavaSound 软件混音器；新后端仍需下列复验，不能将旧录音冒充新版后端验收。
3. 本地 tone.mp3 经 Windows 原生选择框导入；未确认前服务器只有 109 字节配置清单，无音频文件。确认后保存 16761 字节 blob，SHA-256 = 5B7DA878274DEFA96951DF0F41B08EE95A04DB8458F8A20EA40AF29043136978，与源文件一致。关闭配置再打开仍显示已保存本地音乐。截图 169–180。
4. 首次新客户端加载旧设备时发现递归区块加载导致 StackOverflow；已把恢复移至服务端 tick，原存档重进成功。失败日志保留在 .tooling/0332-gramophone/game-local.log；成功重进在 game-local-retry.log 与截图 156–165。
5. 试听拖动的重复音源创建已改为松开后恢复；修复后需新版界面复验。

## 自动检查

- 完整 build + gramophone0332Probe + canonicalTaskEventPersistenceProbe + canonicalTaskJournalProjectionProbe + task0331RuntimeProbe：PASS，日志 .tooling/0332-gramophone/build-regression.log。此构建早于最后的软件混音器替换，不能作为该替换的最终构建证明。
- 独立缓存锁及崩溃残留回收、真实 MP3 解码/跳转、共享所有者保护、pin/延迟回收、包长度限制、坏音频拒绝：PASS（cache-probe.log）。
- 新混音器编译 PASS（mixer-compile.log）。并行运行两个 Gradle 构建时出现共享输出竞争，已改为依次构建/启动；这次失败不冒充产品测试通过。

## 正在收尾的验收

- 新混音器的系统输出匹配、实物红石暂停/恢复和范围离开/返回。
- 独立服务端双客户端错时进入、单方离开与权限。
- 多设备同曲/异曲重叠、试听拖动不累积音源、显示范围与 GUI 缩放。
- 本地未保存退出、同指纹复用、坏包/失败不替换旧曲、恢复与缓存篡改。
- QQ/网易云实际短链样例、不同受限曲目的失败路径；两平台并非全曲库保证。
- 最终全量构建、正式 JAR 晋升、验收矩阵同步。Studio 没有在本轮改动，权威 EXE 保持上轮已交付版本。

ENGINEERING_IMPLEMENTED=PARTIAL
USER_ACCEPTED=NO
RELEASE_READY=NO

## 2026-09-17 本轮施工交付更新（取代上文旧的 E/F 未实施状态）

留声机 E/F 主体及任务完成摘要归档已接入。当前仍为工程候选，不代表整份计划完成或用户接受。

- Windows 原生 UI 控制隔离独立服务器、Developer 和 Witness 两个客户端。QQ 免费样例在新独立混音器播放；12:55:43 暂停于 38.826666666 秒，12:56:10 同实例恢复于完全相同位置。约 27 秒断电没有丢失播放位置。
- Witness 12:58:28 进入范围，从 0 秒开始。Developer 12:58:43 离开，自己的实例在 192.693333 秒停止；Witness 的实例没有停止/重建。Developer 返回后 12:59:23 创建新实例，从 0 秒开始。
- 系统输出与完整参考音轨的归一化相关系数：QQ 0.828570、恢复后 0.801428、仅 Witness 留在范围时 0.711290；网易云 0.473097。录音包含原版音乐；这只是客观播放证据，主观音质验收仍未执行。对应 JSON、生命周期日志及 WAV 在 evidence/ 和 evidence/native-ui/。
- 原生 UI 范围线框截图 251–252；网易云试听拖动截图 259–262。拖动只停止旧试听一次，在松开后创建一个约 176.028 秒的新试听；关闭编辑器停止该试听，没有停止设备播放。
- 新混音器的 48 kHz 重采样、440 Hz 音高/幅度、缓存存活锁/崩溃残留、共享持久引用/pin/GC、解码跳转及包长度检查通过。完整 build 与 gramophone0332Probe、canonicalTaskEventPersistenceProbe、canonicalTaskJournalProjectionProbe、task0331RuntimeProbe、playerPreferences0332Probe、dialogueBacklog0332Probe、taskTracking0332Probe 全部通过。见 evidence/gramophone-final-build.log。
- 范围脚本 PASS，现有 Story 10 包/3 并发策略保持。正式 JAR 含 MP3 解码器及 LGPL 许可/源码归档。

交付文件：`E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.2.jar`，1,667,283 字节。
SHA-256：`718E2D347E7FA4A94A7D189DD884CDC81F225D4B169DDCE2E6C8AA03880E0968`。

实机运行的是每进程冻结的开发 JAR，防止后续编译覆盖运行文件。最终构建另含音频 cursor 代次竞争防护；该保护已通过最终构建，但本轮不是正式重混淆 dist JAR 的安装验收。Studio 本轮没有变更。

尚未关闭：正式 Studio 同一 DGRS 到正式 JAR 安装的端到端、非空任务/记录全流程、多个设备同曲/异曲压力、权限撤销/破坏、实际短链、大量导入/断传/磁盘失败、完整搜索/缩放组合和主观听音。不能把本轮双客户端结果扩写为这些项目已通过。

USER_ACCEPTED=NO
RELEASE_READY=NO
