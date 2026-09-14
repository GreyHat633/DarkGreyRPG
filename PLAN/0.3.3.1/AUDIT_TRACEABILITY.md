# 0.3.3.1 审计追踪表

状态：实现与工程回归已落地，2026-09-14 更新。逐项证据按实际覆盖范围记载；完整环境验收矩阵仍以 DELIVERY_REPORT.md 的证据边界为准，USER_ACCEPTED=NO。

| 编号 | 要求与归属（原计划） | 实现 | 测试/视觉证据 | 结果 |
|---|---|---|---|---|
| A01 | 节点标题被资源名/执行子类型取代；全局“类型 [附加名]”，同步节点头与节点 Inspector；【终止】同样遵守；A、D | 统一类型标题与附加名称投影 | Core/WPF final；final-dark-common.png | IMPLEMENTED / VERIFIED_SCOPED |
| A02 | “任务整体说明”冗长；统一为“任务说明”，不改说明内容含义；A、C | 统一任务说明文案 | Core/WPF final | IMPLEMENTED / VERIFIED_SCOPED |
| A03 | 提交/区域参数只在 Inspector；全字段节点内编辑；提交按最终“目标物品 + 提交对象”模型；A、C | 目标节点和 Inspector 共用目标编辑器，提交物品/角色齐备 | Core/WPF final；game-first-submit-fixed.json | IMPLEMENTED / VERIFIED_SCOPED |
| A04 | 目标描述顺序、假默认值、含义不清；描述在类型之前；真实空草稿 + 水印；短说明表明是玩家看到的目标文字；A、C | 描述优先、空草稿、水印与严格导出校验 | Core/WPF final；watermark-clip-fixed.png | IMPLEMENTED / VERIFIED_SCOPED |
| A05 | 区域排版、维度备注；复用 Start 的 XYZ 分组；删除备注字段；方块坐标；玩家仅看 XYZ；A、C | 整数方块坐标、XYZ 分组、玩家只显示 XYZ | task0331RuntimeProbe；game-region-settled.json | IMPLEMENTED / VERIFIED_SCOPED |
| A06 | 目标类型下拉顺序；实体击杀、角色交互、物品收集、物品提交、区域到达；A | 五类目标固定顺序 | WPF final；dropdown-objective-100.json | IMPLEMENTED / VERIFIED_SCOPED |
| A07 | 奖励节点无内联编辑，默认零条；节点内可编辑奖励包；新建默认一条物品条目；允许删至零条；A | 奖励内联条目编辑与默认一条物品 | Core/WPF final；dropdown-reward-100.json | IMPLEMENTED / VERIFIED_SCOPED |
| A08 | 奖励物品裸下拉框；统一“奖励物品”等字段标签与控件排版；A | 统一奖励字段标签 | WPF final | IMPLEMENTED / VERIFIED_SCOPED |
| A09 | 节点仍叫“动作类型”；所有作者界面统一“执行类型”；内部 `action` 不为文案改名；A | 作者界面执行类型文案 | WPF final；dropdown-action-100.json | IMPLEMENTED / VERIFIED_SCOPED |
| A10 | 新执行参数未进节点；原生各类型、BUFF/MOD扩展、指令模式逐字段对齐；A | 各执行子类型内联参数与 Inspector 对齐 | Core/WPF final | IMPLEMENTED / VERIFIED_SCOPED |
| A11 | 下拉框闪退/选择丢失；复现事件链，修集合/失焦/选择刷新根因，禁止延迟掩盖；B | 稳定选项/行对象，避免失焦重建集合 | 四份 dropdown-*-100.json；WPF final | IMPLEMENTED / VERIFIED_SCOPED |
| A12 | 示例文字进入真实数据；全 Studio 待输入自由文本核查；水印不保存、不导出、不清除用户内容；A | 独立水印 Adorner，不写入数据；可见性与裁剪修复 | NumericDragAndWatermark0331Tests；watermark-clip-fixed.png | IMPLEMENTED / VERIFIED_SCOPED |
| A13 | 说明文字层级混乱；统一小一号、次级颜色、换行；错误提示独立，不能一起弱化；A | 帮助次级字号颜色，错误保持独立 | WPF final；Dark/Light 实机截图 | IMPLEMENTED / VERIFIED_SCOPED |
| A14 | “生命增减”等标签别扭；按“生命值（增减）（1 点 = 半颗心）”等正式用语统一；A | 生命/经验/BUFF 等正式标签 | WPF final | IMPLEMENTED / VERIFIED_SCOPED |
| A15 | 高级选项只是悄悄增加下拉项；勾选即进入指令执行模式；关闭恢复原生模式；切换可撤销、不暗删草稿；A | 高级开关切换指令模式并保留原生/指令草稿 | Core/WPF final | IMPLEMENTED / VERIFIED_SCOPED |
| A16 | 指令名称、斜杠、冗长说明；“指令执行”；作者输入 `/…`；只保留指定短提示；两端校验同步；A | 单行斜杠指令，双端校验 | Core/WPF final；Java build | IMPLEMENTED / VERIFIED_SCOPED |
| A17 | 跨 Story 架构/图谱衔接残缺；以最终边界投影 + 图谱直接 Flow/Logic 接线模型完成作者、打包、运行闭环；D | 统一项目 Flow/Logic 图谱、边界投影及跨包路由 | ProjectGraph0331Tests；game-cross-package-flow-final.json；live-0331-final.log 18:44:13 | IMPLEMENTED / VERIFIED_SCOPED |
| A18 | 黑色空白区域不能拖动；所有节点非交互空白区可拖；控件/端口有自己的手势；B | 控件与空白区命中分流，画布拖动事务 | GraphInteraction0331Tests；node-drag-undo-redo-50.json | IMPLEMENTED / VERIFIED_SCOPED |
| A19 | Ctrl+Z 后连线错位；布局操作整体结束后重算端口锚点、线条和命中几何；B | 布局完成后更新端口/曲线/命中几何 | 50 轮 WPF 多选几何回归；50 轮真实单节点 Undo/Redo | IMPLEMENTED / VERIFIED_SCOPED |
| A20 | 头像弹窗、差分命名、列表难用；Actor Inspector 内编辑；默认头像 + 头像差分；可识别的缩略图列表；E | Actor Inspector 内头像与差分编辑 | ActorPortraitEditor0331Tests；actor-rename-verified.json | IMPLEMENTED / VERIFIED_SCOPED |
| A21 | 语音/音乐仅 Inspector、不能试听；节点内入口与参数；共用本地播放/暂停、可拖进度条和时长；A、E | 节点语音/音乐入口，共享本地 OGG 试听 | LocalAudioPreviewService0331Tests；ogg-preview-*.png；ogg-preview-fixed-loopback.wav | IMPLEMENTED / VERIFIED_SCOPED |
| A22 | 数字不能左右拖动；公共数值控件，左右增减、可直接输入、一次手势一次撤销；B | 数值框拖动、直接输入、提交/取消、一次撤销 | NumericDragAndWatermark0331Tests；numeric-final.json | IMPLEMENTED / VERIFIED_SCOPED |
| A23 | 画面缩放不可用；在现有宽高模型上补齐四边四角、参数同步和可靠命中；E | 四边四角缩放，正常释放与取消区分 | SessionScreenEditor0331Tests；screen-eight-handles.json | IMPLEMENTED / VERIFIED_SCOPED |
| A24 | 参考框、图层排序与预览布局差；对话框/选项框开关对称；参考真实游戏布局；拖拽排序；列表不挤走预览；E | 对称参考框开关、图层拖排、稳定预览区域 | screen-layer-reorder.json；screen-reference-guides-toggled.png | IMPLEMENTED / VERIFIED_SCOPED |
| A25 | 留声机在线方向、媒体资源占用；在线留声机明确延期；已存在的 DGRS 媒体整份入堆问题本版修复；F | 流式包校验与受预算约束的按需读取；在线留声机延期 | dgrsMediaStreaming0331Probe；dgrsMediaLifecycle0331Probe；媒体实机证据 | IMPLEMENTED / VERIFIED_SCOPED |
| D01 | 新增 Start 启动条件“流程驱动”；每条映射一个图谱 Flow 输入，不设固定默认入口；D | Start 流程驱动稳定条目及对应图谱入口 | StoryBoundary0331Tests；storyBoundary0331Probe；跨包实机 | IMPLEMENTED / VERIFIED_SCOPED |
| D02 | 【终止】可命名；稳定边界身份；图谱出口名同步，标题始终“终止 [名称]”；A、D | 命名终止稳定出口与同步投影 | StoryBoundary0331Tests；跨包实机 | IMPLEMENTED / VERIFIED_SCOPED |
| D03 | 图谱只允许节点布局、视口与连线操作；节点新增/删除/内容修改仍由项目/Story 管理；D | 共享图画布，仅布局/视口/合法接线；引用源只读 | ProjectGraph0331Tests | IMPLEMENTED / VERIFIED_SCOPED |
| D04 | 所有正式节点有默认折叠的“介绍说明”，取代节点 Inspector 保存状态行；A | 正式节点默认折叠介绍说明 | CanonicalNodeInspectorViewModel.Help.cs；WPF final | IMPLEMENTED / VERIFIED_SCOPED |
| D05 | 物品提交增加“目标物品”，原目标对象改“提交对象”并选择角色；C | 提交物品与提交角色分别配置 | Task0331RuntimeProbe；两个真实 NPC 交付 | IMPLEMENTED / VERIFIED_SCOPED |
| D06 | 收集完成不可逆；Task 仍为 Logic 图，后续目标按现有前置逻辑激活；C | 收集完成锁存；Task 仍只有 Logic | task0331RuntimeProbe；game-second-submit-fixed.json | IMPLEMENTED / VERIFIED_SCOPED |
| D07 | ACTIVE Story 所有 Start 均禁重入；流程驱动仅启动被命中的对应分支；D | ACTIVE 禁重入；Flow 命中精确入口 | storyBoundary0331Probe；Forge coordinator probes | IMPLEMENTED / VERIFIED_SCOPED |
| D08 | 可重复 Story 的逻辑启动严格 False→True；持续 True 不重启，重登/重载不伪造边沿；D | 每条启动条件记录 False→True 边沿与运行身份 | Forge coordinator probes；schema 6 恢复回归 | IMPLEMENTED / VERIFIED_SCOPED |
| D09 | 标题阻塞直至动画结束，保留现有行为；回归 | 保留全标题动画阻塞并清理退役标题 | title0330Probe；title-blocking-final.json；title-completed-session-final.json | IMPLEMENTED / VERIFIED_SCOPED |
| D10 | 接受磁盘驻留 + 流式校验 + 按需解包缓存 + 分块传输，不改变 DGRS 自包含；F | 磁盘 generation、流校验、按需缓存与分块传输 | 媒体两个专项探针；generation/unload/restart 实机证据 | IMPLEMENTED / VERIFIED_SCOPED |
