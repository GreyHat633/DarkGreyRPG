# 0.3.3.2 Windows 原生 UI 实机记录

日期：2026-09-17。结论：**部分交互通过，整体验收未通过**。本轮没有修改产品源码，没有以自动测试代替实机操作。

## 环境与证据范围

按用户要求使用 Windows 自带 UI Automation、user32 键鼠输入和 GDI CopyFromScreen。只定位本轮 Studio/Minecraft 进程；每次操作后重新读取窗口并留图。执行器为 `.tooling/0332-ui/native-ui.ps1`，原始截图和 UIA 树保存在 `evidence/native-ui/`。这解除此前 sky 截图接口不兼容造成的操作阻塞，不表示修复了 sky 本身。

- Studio：权威 `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`，ProductVersion `0.3.3.2`，142126806 字节，SHA-256 `AA93C9734E1CE293E2BC0D03A7860674619D1B7D4AB5A9925EE8381B1E07FF9F`，本轮再次核验。
- Minecraft：当前工作树 Gradle `runClient`，Forge 1.7.10；这是源码开发运行验证，**不是 dist JAR 安装验收**。启动日志 `evidence/native-ui/game-launch.log`。
- UI 创建的独立项目 `.tooling/0332-ui/Project`；独立游戏 `.tooling/0332-ui/Client`，UI 创建 Creative `New World`。未打开用户生产存档。
- Studio 窗口 1463×942；游戏外窗 1298×846、客户区 1280×800、GUI Auto。不是多分辨率完整矩阵。

## 实际操作及结果

以下 PASS 只覆盖描述中的动作，不代表整条需求全部通过。

| 检查 | 结果 | 操作后的证据 |
|---|---|---|
| 新项目、故事、会话和两句台词 | PASS | `08`、`12`、`18`、`25`–`29`；通过 UI 创建和输入，Ctrl+S 写入 canonical JSON |
| 每句折叠与双编辑区域同步 | PASS | `27-collapse`：Inspector 点击第 1 句折叠，inline 与 Inspector 同时呈现“展开第 1 句”；添加第 2 句保留第 1 句 |
| 未保存第 2 句的搜索与跳转 | PASS | `31-search-result` 命中 BetaUnique0332，稳定 page_id；`32-search-navigate` 双击后定位台词，Inspector 展示第 2 句 |
| 搜索长结果显示 | **FAIL** | `31`、`32`：结果 viewport 宽 316，行宽 504，路径/片段截断，出现横向滚动；没有完整换行 |
| 分组框拖动及一次撤销 | PASS | `43`–`48`：创建包含 Start 的框，拖动标题后框与 Start 同步 +50/+40 屏幕像素，外部台词不动；一次 Ctrl+Z 还原；`frame-before.json` 与 `frame-undo.json` 全文相同 |
| 保存、关闭 EXE、重开项目 | PASS | `85-studio-reopened` 自动恢复隔离项目，`88`/`89-session-fit` 两句正文、page_id 与框保留；分句恢复默认展开 |
| 游戏真实建档进入世界 | PASS | `49`–`56`：Singleplayer → 新建 → Creative → 世界；`106-save-quit` 保存并返回主菜单 |
| 三主题切换 | PASS（设置窗口） | `59` 灰黑、`60` 浅白、`61` 蔚蓝，实际点击后显示变化；不代表所有游戏窗口覆盖 |
| 透明度和字号预览 | PASS（设置预览） | `64`/`69`：0% 背景、125%；`70-font150`：150%；设置面板本身仍可读 |
| 阅读速度边界 | PASS（设置预览） | `72-reading-zero` 显示“立即显示”；`73-reading120` 显示 120 字/秒 |
| 分项音量、关闭重开持久化 | PASS（控件/配置） | `65`–`69`，voice/sessionMusic 各约 0.497；关闭重开保留主题/透明度/字号；不代表实际听音通过 |
| 恢复默认 | PASS（偏好） | `74-reset` 与 `preferences-after-reset.properties`：CHARCOAL、0.8、1.0 字号、30 字/秒、三音量 1.0；窗口布局值保留 |
| P 打开关闭设置、聊天不劫持 | PASS | `59`/`68`；`79`/`80-chat-p`：聊天输入 p，仍在聊天框，随后 ESC 取消，未发送 |
| 原版 Controls 改键 | PASS | `94`–`100`：历史绑定 H，设置 P→K；K 开/关见 `100`/`101`，旧 P 不再打开见 `102`；options.txt 验证 LWJGL 键值 35/37/23 |
| I 任务窗口和已完成页 | PASS（空态） | `76-task` → 点击 `77-completed` 显示“暂无已完成任务”，I 关闭；没有真实任务数据，不计 Tracker/归档通过 |
| H 历史窗口及 ESC 返回 | PASS（空态） | `103-history-open` 显示“暂无对话记录”，`104-history-esc` 回世界；不计 250 条/跨会话隔离通过 |

截图序号中的早期 frame 点击重试没有产生框，不计成功；最终成功以 `43-frame-invoked` 起算。中文 IME 曾吞掉 Minecraft 字母输入，切换目标窗口输入语言后重试成功。Studio Alt+F4 已关闭窗口，随后执行器尝试捕获已销毁窗口报错；重启动及恢复截图证明关闭/重开结果，不将该执行器错误归因于产品。

## 未通过项和剩余门槛

1. **UI-0332-01：搜索结果长路径与片段被横向截断。** 重现：本记录项目搜索 BetaUnique0332，观察左侧结果。预期：限定结果行宽并完整换行，正文可直接阅读。实际：316 像素容器内生成 504 像素行。本轮未修复。
2. 同一个 `.dgrs` 的 Studio 正式导出→游戏安装→剧情推进仍未运行。本项目仅为编辑交互夹具，未连接完整剧情；不得称为端到端包。
3. 非空 Tracker/历史、长选项、任务完成归档/重置、只读引用搜索、分组缩放/复制、多分辨率及改键冲突组合仍需验证。
4. E/F 留声机、平台音乐、本地上传和独立媒体生命周期尚未实施，因此其专服双客户端与真实听音不能验收。

```
NATIVE_UI_EXECUTION=PASS
VISUAL_REVIEW=PARTIAL_FAIL
E2E_SAME_DGRS=NOT_RUN
PACKAGED_JAR_UI_ACCEPTANCE=NOT_RUN
DEDICATED_SERVER_TWO_CLIENTS=NOT_RUN
AUDIO_LISTENING_CHECK=NOT_RUN
ENGINEERING_IMPLEMENTED=NO
USER_ACCEPTED=NO
RELEASE_READY=NO
```

原始证据中有操作前后和重试截图，不把截图数量当作通过用例数量。实现缺口仍属于原 Construction PLAN，没有因本轮部分验证而缩减范围。

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
