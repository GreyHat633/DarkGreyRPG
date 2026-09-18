# 0.3.3.2 阶段施工报告

这是可运行的阶段性施工产物，不是 Construction PLAN 的完整交付。完整需求状态见 REQUIREMENTS_TRACEABILITY.md。

## 已写入工作树

玩家偏好/三主题色板、会话字号及设置预览、分项 Session 音量；三任务本地追踪、持有数与任务权威进度分离、当前保留终态任务只读页；250 条内存对话记录、Choice 成功回执、长选项换行/滚动/键盘选择；Studio 每句折叠、编辑 sidecar 分组框/拖动/撤销/完整组复制、异步项目搜索及稳定 page_id 定位。

尚缺：A–D 的完整 UI 覆盖和部分搜索/归档分支；E/F 留声机、两平台音乐、上传与独立媒体生命周期未实施；同包和双客户端听音验收未执行。这些需求没有被撤销或移出本版范围。

## 阶段产物

构建源：分支 `codex/0.3.3.2`，HEAD `66732365aacb426cbec96d40bb55a990aa758d55` 加当前未提交工作树。没有新的构建提交，不把基线 SHA 冒充包含本版修改的提交。未推送、未创建 Release。

| 文件 | 版本 | 字节 | SHA-256 |
|---|---|---:|---|
| `E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe` | ProductVersion 0.3.3.2 | 142126806 | `AA93C9734E1CE293E2BC0D03A7860674619D1B7D4AB5A9925EE8381B1E07FF9F` |
| `E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.2.jar` | 0.3.3.2 | 1281556 | `47B0E55387FFAD855D19D294A78014A34F7086D49CB08226CE0BE25ED2628216` |

Studio 是 self-contained Windows x64 Release；最后一次修改后已重新 promote，并现场验证指定路径的版本/大小/哈希。原 dist Studio 在首次 promote 前保留到 `.tooling/0332/PreviousStudio`。

## 验证状态

```
ENGINEERING_IMPLEMENTED=NO
AUTOMATED_REGRESSION=PASS (已实现工作树；不包括未实现功能)
E2E_SAME_DGRS=NOT_RUN
DEDICATED_SERVER_TWO_CLIENTS=NOT_RUN
QQ_MUSIC_SUPPORTED_SCOPE=NOT_VERIFIED / NOT_IMPLEMENTED
NETEASE_SUPPORTED_SCOPE=NOT_VERIFIED / NOT_IMPLEMENTED
AUDIO_LISTENING_CHECK=NOT_RUN
VISUAL_REVIEW=PARTIAL_FAIL
USER_ACCEPTED=NO
RELEASE_READY=NO
```

自动证据：Java 完整 build 与 11 个新/回归探针通过；WPF 593 通过；Core 最近一次 453 通过/12 跳过。详见 TEST_RESULTS.md，跳过或不可用指标不计通过。

2026-09-17 按用户要求改用 Windows 原生 UIA/Win32/GDI，已实际操作权威 Studio 和隔离 Minecraft。分句/搜索跳转/分组拖动撤销/保存重开、设置/改键及任务历史空态获得实机证据；搜索长结果截断未通过。完整记录见 NATIVE_UI_ACCEPTANCE.md。开发 runClient 不等于 dist JAR 安装验收，同包、双客户端和听音仍未运行。此前 sky 截图错误不再作为无法操作的阻塞。


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
