> 2026-09-14 最终集成更新：本文件保留工作包阶段记录；后续编译修复、真实交互及最终证据以 [DELIVERY_REPORT.md](DELIVERY_REPORT.md) 为准。阶段性待办不能覆盖最终记录，未实测部分也不自动计为通过。

# 0.3.3.1 受影响文件与符号

来自已核对精确基线的计划第 4 节；实施时按符号增量更新，不将预期当已修改。

| 节点定义 | `GraphNodeDefinition.cs`、`GraphNodeDefinitionRegistry.cs` | 尚无统一用途/端口帮助；`terminate` 无命名边界元数据；`action` 中文已改名 |
| 两侧编辑 | `CanonicalNodeInspectorViewModel.cs`、`.Actions.cs`、`.Rewards.cs`；`CanonicalInlineNodeEditorControl.xaml`；`CanonicalStoryWorkspaceView.xaml` | `HasEditableFields` 与 `HasInlineFields` 覆盖漂移；内联分支仍主要对应旧属性；不能只改一个布尔白名单 |
| 标题/撤销 | `GraphEditorHostViewModel.cs` 中 `GraphEditorNodeViewModel.Update`、`ApplyLayoutSnapshot` | 标题直接用资源名/执行子类型；布局撤销与连线重画时序待修 |
| 画布命中 | `CanonicalGraphEditorView.xaml.cs`、`CanonicalGraphNodeControl.xaml.cs`、`FlowPortControl.cs` | Header-only 拖动；端口几何依赖实际布局；下拉控件已有部分防拦截，不能说完全没有保护 |
| Task 定义 | `CanonicalTaskObjectiveSchema.cs` | `collect_item` / `submit_item` 共用字段集，提交没有角色；含 `dimension_note`；区域数值当前接受 double |
| Task 游戏 | `CanonicalTaskForgeManager.java`、`CanonicalTaskRuntime.java`、`CanonicalTaskPlayerTransactions.java` | 已有收集完成不回退和提交事务基础；提交尚以 UI 请求为主；区域采样 posX/Y/Z |
| 玩家任务 | `CanonicalTaskUiProjection.java`、任务消息/Journal 投影、`GuiCanonicalTaskScreen.java` | 需找到实际呈现链增加 XYZ，不因 Journal 命名相似改错未使用路径 |
| Start | `StoryStartSchema.cs`、`CanonicalStoryStartConfiguration.java` | 正式三种启动条件和稳定条目存在；无新“流程驱动”；旧 `enter_story` 不能直接冒充新语义 |
| 聚合 | `CanonicalAggregateNodeFactory.cs`、`AggregatePortProjection.cs` | 现有工厂服务 Session/Task；可复用边界模式，不能把固定 Flow 输入顺手套给 Story |
| 项目图谱 | `CanonicalProjectStoryGraphService.cs`、`CanonicalStoryLogicGraphRepository.cs`、`ProjectHomeViewModel.cs`、`ProjectGraphView.xaml/.cs` | Logic 连接编辑/存储仍在；可视 Edges/统计依赖旧 transitions；不是所有 Runtime 联动都被删 |
| Story 服务端 | `CanonicalStoryRuntime.java`、`CanonicalStoryServerService.java`、`CanonicalStoryForgeManager.java`、`CanonicalSessionSavedData.java` | 当前终止直接标 TERMINATED；路由清理后返回；Logic 传播按外部输入整体变化运行，不等于每个启动条件的精确上升沿 |
| 头像 | `ActorPortraitDialog.cs`、`ActorEditorViewModel.cs` | 有名称列表及数据，不等于数据丢失；但弹窗、术语、缩略图与可见性不合要求 |
| 画面 | `SessionScreenEditor.cs` | 已有 width/height、单个右下角 9×9 手柄及列表 MaxHeight=130；应补全，不谎称毫无缩放或列表无限增长 |
| 音频 | `CanonicalStoryWorkspaceView.xaml/.cs`、`ProjectMediaStore.cs` | 有导入与引用；“导入并播放”实际设置节点媒体，不等于 Studio 试听 |
| DGRS 最前端 | `DgrsArchiveReader.java` | 打开 ZIP 时全部条目读入 `Map<String,byte[]>`；`readBytes()` 再 clone，内存问题从这里开始 |
| 包快照/常驻 | `StoryPackageSnapshotReader.java`、`StoryPackageLoader.java`、`LoadedStoryPackage.java` | required bytes 含媒体，包对象再次 clone 并保留；从整份数组切 32 KiB 网络块 |
| 媒体校验 | `StoryPackageMediaValidation.java`、`MediaPayloadValidation.java`、`StoryPackageContentFingerprint.java` | 不只有魔数：PNG CRC/尺寸、OGG 页校验/EOS 等已存在，改流式不能削弱 |
