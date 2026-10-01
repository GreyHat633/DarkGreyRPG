# 前置条件、框选反馈与图片动画交付记录

2026-10-01。本次按用户确认方案实现；AGENT_VERIFIED，USER_ACCEPTED=NO，RELEASE_READY=NO。未提交或推送 Git。本记录取代 UI_REVIEW_FIX.md 中本次涉及的旧动画语义。

## 实现

- 每个选项保留独立卡片；前置条件为真正的启用勾选框。关闭保留设置、端口、连线，灰色呈现且运行时忽略；重新启用、撤销重做与持久化使用同一状态。节点和 Inspector 共用编辑组件。说明原文为“置灰后，即使前置条件不满足，该选项依然可见。”。旧连接推断为启用，未连接为关闭；启用未接线在配置与导出校验中报错。流程输入在上，条件按选项排序。
- 框选单个节点显示属性，多选显示数量和名称，无选择显示空状态。选中集合未变化不发送重复通知；渲染帧合并更新，多选不创建属性编辑器，同一单选复用编辑器。没有恢复全项目使用位置扫描。
- 图片属性下增加可添加、删除、拖动排序的动画列表；进入与退出效果名称分开，每步包含类别、效果、方向、时长、等待。新增默认 0.5 秒、等待 0。各图片独立计时，图片内按序播放；退出完成保持隐藏。新画面执行自己的序列，台词翻页和重复快照不重启。平滑衔接独立设置，只承接上一画面实际可见布局；提前推进取消旧步骤。
- Studio 预览、保存/导出、Java 解析和客户端使用一致序列语义。旧图片 enter/exit 和整屏配置迁移为图片序列，旧平滑配置转为平滑衔接。超出传输预算明确报错，不截断步骤；媒体缓存与加载约束未改变，渲染线程未增加磁盘扫描。

## 自动验证

证据集中在 [evidence/sequence-review](evidence/sequence-review)。

| 检查 | 结果 |
| --- | --- |
| Core 最终全套 | 475 通过，12 跳过，0 失败 |
| WPF 全套 | 627 通过，2 失败，1 跳过 |
| WPF 修正后相关组复测 | 83 通过，0 失败；与全套重叠，不能累加 |
| Java build / Checkstyle / construction0335Probe / codec / SavedData | 全部通过 |
| Windows x64 自包含 Release publish | 成功 |

WPF 全套发现畸形图中空节点导致新增灰色条件索引异常，已加保护并复测通过。另一个折叠动画中间高度的时序断言在单独复测通过；未为它改变产品逻辑。逐测试取最新结果为 629 通过、1 跳过，但这不等于一次完整全套无失败运行。既有跳过项仍保留，不算通过。

Java 探针覆盖条件启停与真假、动画序列与迁移、网络编解码、重复快照/存档恢复以及容量超限拒绝。Core/WPF 覆盖保存、撤销重做、双视图、端口顺序、畸形数据与动画采样。

## 原生 Studio 与 Minecraft

使用 Win32/UIA 与真实鼠标键盘，在隔离验收项目和本地 Minecraft 服务端/客户端执行。截图位于 evidence/native-ui；只以下列对应证据证明相关结果，目录中的其他调试截图不统一解释为通过。

- 条件关闭、灰色线、撤销、重做、节点内恢复：`sequence-condition-disabled.png`、`sequence-condition-undo.png`、`sequence-condition-redo.png`、`sequence-condition-inline-enable.png`。
- 动画列表增加、删除、拖动排序并保存核对：`sequence-animation-list.png`、`sequence-animation-reordered.png`、`sequence-animation-added-bottom.png`。新增步骤实际数据为 0.5 秒/0 等待，拖动后数组顺序改变，撤销恢复。
- 最终正式 EXE 重开保存项目：`sequence-formal-fit.png`，三句台词编辑器和前置条件状态正确恢复。
- 正式 EXE 实际鼠标完成 3 轮单选到四选框选，以及最后空选：`sequence-marquee-single-held.png`、`sequence-marquee-multiple-held.png`、`sequence-marquee-empty.png`。360 次集合不变的鼠标移动无额外编辑器构建；6 次集合变化处理为 4.0159–10.4503 ms，平均 7.8503 ms。多选与空选构建数为 0，切回单选共新建 2 次，初次相同单选复用。原始记录为 [SequenceFinalSelectionTrace.jsonl](evidence/SequenceFinalSelectionTrace.jsonl)。这些是本机 Inspector 更新计时，不是端到端帧延迟保证。
- Studio 预览三图片并行进入/退出、延迟与退出保持隐藏：`sequence-preview-*.png`。
- Minecraft 同一画面左图淡入后淡出、中图滑出后滑入、右图擦入后等待擦除：`sequence-game-start-1500.png`、`sequence-game-start-3000.png`、`sequence-game-start-5500.png`。时间为触发命令后的采样点，区域触发存在约半秒偏移。
- 翻到第二/第三句不重播，下一画面同图重新执行与中图平滑衔接：`sequence-game-page-two.png`、`sequence-game-page-three.png`、`sequence-game-pages-1500.png`、`sequence-game-pages-10500.png`。
- 旧画面右图步骤尚未结束即推进：`sequence-game-early-300.png`；新画面立即执行自己的动画。
- save-all 后停服重启并重新登录：`sequence-game-before-restart.png`、`sequence-game-restored.png`、`sequence-game-restored-stable.png`；退出的左右对应两张图片保持隐藏，只剩右侧目标图片，没有重播或复现。日志为 `SequenceMinecraftRestoreClient.log` / `SequenceMinecraftRestoreServer.log`。

导出文件 [SequenceReview.dgrs](SequenceReview.dgrs) 包含 15 个画面、149 个图片、131 个动画步骤、5 个条件开关；保存会话与导出会话内容一致，导出中无旧画面动画格式。见 [SequenceDataAudit.json](evidence/SequenceDataAudit.json)。实机运行用同一导出包和配套 JAR。上述验证不代替用户个人验收，也不代表所有项目规模和硬件环境的长期性能保证。

## 正式交付

正式路径已覆盖更新，启动路径核对为 dist 下 EXE；ProductVersion **0.3.3.5**，自包含 Windows x64 Release。

| 文件 | 大小（字节） | SHA-256 |
| --- | ---: | --- |
| `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe` | 142682838 | `A7D44D674A103EE49E9BB37E8BD0CE91948A2211D7F3CEC816A28076E67CAFC1` |
| `dist/darkgrey_rpg-0.3.3.5.jar` | 1863968 | `A3D9E5A28435A74D53CE85EE3A4497C2924D121FEA15F97DBE2810EAA09A2024` |
| `PLAN/0.3.3.5/SequenceReview.dgrs` | 15937 | `38F0E37E50CE51289B05884D3BED79526DB04F8A68ECAC27E06E09DEDAA16935` |

机器可读记录：[Delivery.json](Delivery.json)、[SequenceArtifactHashes.json](evidence/SequenceArtifactHashes.json)。测试项目与运行环境位于 `.tooling/0335`，未修改用户正式项目；测试证据按本记录保留。
