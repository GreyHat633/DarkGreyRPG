# 在线媒体验证（未完成）

2026-09-17 本机无凭证 HTTP 探测：

- 网易云 `https://music.163.com/song/media/outer/url?id=416892104.mp3`：302 后返回 200、audio/mpeg、Content-Length 5559319。临时播放 URL 已从证据中遮去。只证明本次响应，不证明整曲、试听属性、Java 解码或可听音频。
- QQ `https://y.qq.com/n/ryqq/songDetail/0039MnYb0qxYhV`：curl 20 秒连接超时（退出 28）。不能据此断言平台不支持，也不能声明接入成功。
- 原始 HTTP 证据：`evidence/netease-http.txt`、`evidence/qq-http.txt`。
- 当前未添加第三方音乐 API 服务、登录凭据或解码依赖。

QQ_MUSIC_SUPPORTED_SCOPE=NOT_VERIFIED
NETEASE_SUPPORTED_SCOPE=HTTP_RESPONSE_ONLY
JAVA_DECODING=NOT_RUN
PAUSE_RESUME=NOT_RUN
AUDIO_LISTENING_CHECK=NOT_RUN

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
