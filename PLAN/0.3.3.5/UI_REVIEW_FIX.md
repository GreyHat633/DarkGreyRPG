# 0.3.3.5 九项界面问题修复

本记录对应 2026-10-01 用户八张截图提出的九项问题，覆盖本次追加修复。此前 Construction.md 的实现记录不能替代本次检查。正式文件已更新，用户个人验收仍由用户决定。

| 问题 | 修复与检查 |
| --- | --- |
| 使用位置控件和可读性 | 改为带箭头的下拉展开区，复用 220ms 折叠动画。结果显示故事、会话/任务、节点名称和用途；不再显示 membership、字段路径等内部标识。仅加入资源列表不计为节点调用。实机展开并点击“说话角色”结果，成功定位台词节点。 |
| 选择选项缺少层级 | 每个选项独立卡片，含选项序号、文本和“前置条件”；节点与 Inspector 共用编辑器。 |
| 两个编辑区不同步 | 原“条件设置”实际是编辑区展开状态，两个投影各自保存，造成展示不一致；参数本身走规范模型。现在共享每个选项的展开状态，逐项验证置灰、说明文字及撤销在两个投影同步。另测选择提示、音乐、画面、任务目标、故事动作的模型更新通知。 |
| 冗长标签和技术说明 | 长标签改为“置灰”，删除 Logic 端口技术说明；改用灰色简述“条件不满足时，勾选则显示为不可选；不勾选则隐藏。” |
| 输入端口排序 | 固定端口优先，随后动态端口按逻辑顺序排列；选择节点流程输入在最上方，条件输入遵循选项次序，撤销后顺序一致。 |
| 框选切换 Inspector / 卡顿 | 框选过程及鼠标松开收尾均不切换 Inspector，下次明确单击才切换。取消节点选择时的全项目媒体使用扫描，并跳过重复选择同一节点时的 Inspector 重建。真实鼠标发现并修复收尾事件遗漏，回归覆盖 Begin→Update→Complete。未宣称所有机器的操作耗时为零。 |
| 图片动画的归属 | 移除顶部整屏特效编辑器。每张图片的属性下方分别配置“进入效果”和“退出效果”，分别使用淡入/淡出、滑入/滑出等名称，各有时长与方向。预览起点改为“从空白开始”或具名接续画面。Studio、导出和游戏均支持逐图片动画；旧整屏数据保持兼容。 |
| 使用位置范围错误 | 仅角色、物品及相应资源组显示使用位置；会话、任务、台词、选择等节点不显示。 |
| 无意义的头像使用菜单 | 删除 Inspector 的“台词头像使用”媒体查询下拉框。保留每句正常的头像/表情设置。 |

## 验证

- 本轮较宽的 WPF 回归：77 通过；最终鼠标完整过程与图编辑器回归：66 通过。中间的 18 项控件检查和 6 项 Core 检查也通过；这些范围有重叠，不相加为独立测试总数。
- Java build、Checkstyle、construction0335Probe、会话编解码与存档探针通过。增加逐图进入/退出、不同持续时间、持久化往返、32 张图片完整效果配置的长度边界检查。
- 最终正式 EXE 上验证：真实鼠标框选不变更 Inspector，随后单击结束节点切换；卡片展开和置灰在节点/Inspector 同步；资源使用位置能展开和跳转。
- 正式 Studio 导出 `UIReview-PerImage.dgrs`，由更新后的隔离服务器与 Minecraft 客户端加载。采集进入和退出各 0.5/1.5/2.5/4.5 秒画面：四秒淡入、两秒滑入、立即显示独立执行；移除图片退出、保留图片持续显示。测试进程随后关闭，未修改用户的游戏存档。
- `git diff --check` 通过。没有提交或推送 Git。未生成内存转储。

## 证据

- `evidence/native-ui/ui-review-delivered-cards.png`：正式客户端的卡片、前置条件、置灰与双视图。
- `evidence/native-ui/ui-review-authoritative-marquee.png` / `ui-review-explicit-click.png`：完整框选后保持 Inspector，单击后切换。
- `evidence/native-ui/ui-review-resource-usage.png` / `ui-review-usage-jump.png`：可读使用位置与跳转。
- `evidence/native-ui/ui-review-layer-effects.png`：单图属性下的独立进入/退出效果。
- `evidence/native-ui/ui-review-game-enter-*.png` / `ui-review-game-exit-*.png`：游戏中真实动画采样。
- `evidence/ui-review/`：本轮通过的测试结果、构建和游戏日志。失败的中间检查保留在 `.tooling/0335`，不计入通过结果。
- `UIReviewSourceSnapshot.json`：交付时工作区已修改/新增源码哈希；此前的 SourceSnapshot.json 保留为上一轮记录。

## 正式交付

Windows x64 自包含 Release：`E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`

- ProductVersion：`0.3.3.5`
- 大小：`142627030` 字节
- SHA-256：`F2A0C2A49CC60BABEBD0A69A5D5B4622ABC3661607AF694E7DF13084EF4DC46F`

同步更新模组：`E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.5.jar`

- 大小：`1860438` 字节
- SHA-256：`CF07F57F1DB8B8C97251FB65FD05F8F58D6EFA15BDB6478DF982D85C361A23FB`

Studio 与 JAR 需一起更新，才能在游戏端使用新增的逐图片动画数据。
