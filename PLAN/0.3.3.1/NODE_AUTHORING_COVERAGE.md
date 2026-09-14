# 0.3.3.1 节点作者能力覆盖

最终实现记录；Core/WPF 全套结果见 DELIVERY_REPORT.md。这里的“对齐”指生产控件/绑定/命令和自动回归覆盖，不等同每字段均已做完整实机鼠标排列。

| 节点类别 | 内联与 Inspector 能力 | 生产落点与检查 |
|---|---|---|
| 目标 | 描述、水印、类型、前置逻辑；数量/物品/角色；提交物品+角色；整数维度/XYZ/半径 | CanonicalObjectiveEditor；目标作者/元数据 Core 测试、WPF、真实 NPC/区域 |
| 奖励 | 默认一条物品，条目类型/物品/有符号数量、增加删除、可空列表 | InlineCanonicalNodeEditor、Inspector Rewards；Core/WPF、真实下拉100次 |
| 执行 | 物品、经验、生命、传送、消息、BUFF/MOD扩展及单行指令；原生/指令草稿恢复 | Inspector Actions + 内联字段；Core/WPF、空节点创建、下拉100次 |
| 台词 | 角色/旁白、正文、头像差分、语音导入/清除/试听 | SourcePath + AudioPreviewControl；Actor/音频 WPF、实际 OGG 播放暂停拖进度 |
| 音乐 | 播放/停止、引用、循环、淡入淡出、本地试听 | 同一音频服务；Core/WPF、实机 Session 音乐 |
| 标题 | 主/副标题、淡入/停留/淡出 | 内联/Inspector；title0330Probe、真实标题阻塞 |
| 画面 | 图层列表、引用/导入/删除、变换、参考框；完整构图器位于 Inspector | SessionScreenEditor；8控制点及排序真实 Undo |
| Story 开始 | 多条稳定名称/类型/对应参数，可重复，流程驱动 | Start rows；StoryBoundary tests、下拉100次、跨包 Flow |
| 终止 / 会话结束 / 逻辑边界 | 名称、稳定端口、父图投影更新 | CanonicalStoryBoundaryProjection；Core 边界重命名/删除撤销/打包回归 |
| 与/或、选择、结算 | 动态端口增删、名称与优先级，沿用既有命令 | 注册表 + 共享 Inspector；Core/WPF |
| 会话/任务聚合 | 资源摘要、真实边界、双击进入、帮助 | 共享 graph host；聚合与导航回归 |
| 项目图谱 Story | 投影名称/端口、布局/视口/接线、查看/进入；不修改故事内容 | ProjectGraphViewModel.Canonical；引用源只读及 Connect/Undo/Redo/Reopen 三项专项测试 |

`CanonicalNodeInspectorViewModel.Help.cs` 为正式节点提供用途、端口和必要补充。介绍说明默认折叠；不同目标/执行子类型按实际语义切换帮助。未知类型不会被当作已支持类型。保存状态不占用该帮助区域。

真实全字段、全部缩放/主题/DPI 的组合矩阵未全排列执行，不宣称 NODE_INSPECTOR_PARITY_GATE 的人工全矩阵已关闭。
