# 测试记录（施工中）

## 基线

- Core：453 PASS / 12 SKIP / 0 FAIL。
- WPF：587 PASS / 0 FAIL。
- 计划指定的十项 Java 探针：PASS。dgrsMediaLifecycle0331Probe 的 reparse root 子检查 SKIP_UNAVAILABLE。
- 原始日志与 TRX：`evidence/baseline-*`。

## 首批实现

- playerPreferences0332Probe、utilityWindow0324Probe、canonicalSessionClientModelProbe、sessionPresentation0330Probe：PASS（preferences-java.log）。
- dialogueBacklog0332Probe、偏好/会话模型/编解码/服务端/b4 网络编号回归：PASS（history-java.log）。
- taskTracking0332Probe、taskNotificationsPlanProbe、canonicalTaskJournalProjectionProbe、task0331RuntimeProbe：PASS（tracker-java.log）。
- Studio 折叠与 Frame 定向测试：5 PASS（studio-frames.trx）。
- 集成 Core：453 PASS / 12 SKIP；集成 WPF 首轮：583 PASS / 6 FAIL，均为新 Frame 菜单与旧固定菜单断言冲突。保留失败 TRX；后续修正断言并重跑，不覆盖原始结果。

这些是自动化证据，不代表 UI、听感、双客户端或用户验收。

## 当前工作树最终自动检查

- Studio WPF：593 PASS、0 FAIL、0 SKIP（evidence/final-wpf.trx）。包括新增两条 Frame/Search 测试；折叠及旧编辑器回归仍在全量集中。
- Frame/Search 定向：5 PASS（authoring-final.trx），覆盖一次撤销、完整组复制新成员身份、sidecar 与 runtime 文件隔离、page_id 排序后定位、异步陈旧结果保护及后台脱离 UI 模型的读取。
- Java：不带定向 Spotless init script 的完整 `build` PASS（final-java-regression-complete.log）；之前发现的通配符 import 和混合换行问题已最小修复。旧失败日志保留。
- 同次完整构建的 11 项探针全部 PASS：playerPreferences0332Probe、dialogueBacklog0332Probe、taskTracking0332Probe、canonicalSessionClientModelProbe、canonicalSessionNetworkCodecProbe、canonicalSessionServerServiceProbe、taskNotificationsPlanProbe、task0331RuntimeProbe、storyMediaCacheIndexProbe、storyMediaServerProbe、dgrsMediaLifecycle0331Probe。
- dgrsMediaLifecycle0331Probe 保留工具环境限制：reparse-root 子检查 SKIP_UNAVAILABLE、open handle count=-1；不将不可用指标当 PASS。
- 当前 scope scan PASS，64 个 tracked/untracked 源文件受扫描（final-scope.log）；此脚本是有限静态检查，不覆盖所有语义冻结边界。
- `git diff --check -- src studio build.gradle.kts gradle.properties` 无空白错误；日志只有换行转换提示。
- Core 最近一次完整运行：453 PASS、12 SKIP、0 FAIL（integration-core 证据），之后 Core 产品源码未变。
- 最新 Studio self-contained win-x64 Release 已在最后一次 WPF 修改后重新发布；产物哈希见 artifacts.json。

仍未执行：三主题游戏实机矩阵、同包 DGRS 导出/加载、双客户端、在线平台解码/真实发声、完整视觉门和本版压力验收。没有因自动回归通过而将这些项升为 PASS。

## 2026-09-17 原生 UI 补验

Windows UIA/Win32/GDI 已完成部分真实交互，详见 NATIVE_UI_ACCEPTANCE.md。设置窗口三主题、偏好控件/持久化/恢复默认/改键、Studio 分句与分组拖动撤销/保存重开已获得实机证据。搜索长结果显示 FAIL。三主题全窗口矩阵、同包、双客户端及听音仍未完成。

## 2026-09-17 UI-0332-01 修复复验

搜索列表禁止横向滚动，结果内容横向 Stretch，路径和片段明确换行。原生 UI 使用同一项目实际搜索并双击结果：316 像素容器中结果行宽 316；拖动分栏缩至 256 后行宽 256，路径及正文完整换行；第二句定位仍成功。证据：evidence/native-ui/113-fixed-results.png、114-fixed-navigate.png、115-fixed-narrow.png 及对应 UIA 树。UI-0332-01=FIXED_VERIFIED。此前 FAIL 是修复前记录，予以保留。

AuthoringSearch0332Tests：3 PASS / 0 FAIL（evidence/search-fix.trx）。self-contained win-x64 Release 已更新权威 dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe；ProductVersion 0.3.3.2，133909999 字节，SHA-256 7965B4980FA1A1CF7E2421A7B59F6C0D718EDC643E8A5F0D9D53DE35CF4613AA。原生复验运行的正是这个 EXE。

本次只关闭有明确复现证据的搜索布局缺陷。E/F 未实施和同包、非空任务/历史、双客户端、听音等未覆盖项保持开放；整体验收仍不完整，USER_ACCEPTED=NO，RELEASE_READY=NO。

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
