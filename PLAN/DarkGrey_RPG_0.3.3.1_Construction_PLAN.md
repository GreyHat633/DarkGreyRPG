# DarkGrey_RPG 0.3.3.1 施工 PLAN

> **状态：工程候选已交付（2026-09-14）；实现、回归和实机范围见 [交付记录](0.3.3.1/DELIVERY_REPORT.md)。完整验收矩阵未全部关闭，USER_ACCEPTED=NO。**
> 目标版本：`0.3.3.1`
> 施工基线：`codex/0.3.3.0`
> 基线提交：`64dd990484a8cf2639013a41a7d4fe960e6b7409`
> 基线树：`dba507aab1d64bf3342172163354acda701b0f47`
> 建议施工分支：`codex/0.3.3.1`
> 编制与分支核验日期：2026-09-14
> 仓库：`GreyHat633/DarkGrey_RPG`

## 执行摘要

本版修复 0.3.3.0 人工审计暴露的作者体验、图编辑交互和媒体读取问题，并落实后续已经确认的跨 Story 边界模型。**不是重做 0.3.3.0，也不是只给 Inspector 美化。**

交付必须同时覆盖四条链：

1. **作者链**：节点本体、Inspector、介绍说明、验证、撤销、保存、导出使用同一份语义。
2. **故事链**：Story 内部定义公共边界，项目故事图谱直接连接这些边界，服务端真正运行跨 Story Flow / Logic。
3. **任务链**：物品提交使用“目标物品 + 提交对象”；收集目标完成不回退；区域使用方块坐标，玩家仅看目标坐标。
4. **媒体链**：DGRS 保持自包含；服务器媒体以磁盘数据、索引和有界缓冲提供，不再整包常驻 JVM 堆。

本版必须交付可运行的 Studio 和模组、逐项审计对照、真实交互测试、截图与性能证据。**编译通过和探针通过不能替代作者操作验收。**

---

## 目录

- [1. 依据、优先级与施工边界](#1-依据优先级与施工边界)
- [2. 人工审计 1—25 逐项落地表](#2-人工审计-125-逐项落地表)
- [3. 已确认语义：不得重新打开的设计问题](#3-已确认语义不得重新打开的设计问题)
- [4. 当前源码定位与共因](#4-当前源码定位与共因)
- [5. 工作包与施工顺序](#5-工作包与施工顺序)
- [6. WP-A：统一节点作者界面](#6-wp-a统一节点作者界面)
- [7. WP-B：图编辑交互修复](#7-wp-b图编辑交互修复)
- [8. WP-C：任务目标与玩家呈现](#8-wp-c任务目标与玩家呈现)
- [9. WP-D：跨 Story 图谱、边界与运行时](#9-wp-d跨-story-图谱边界与运行时)
- [10. WP-E：头像、音频与画面编辑](#10-wp-e头像音频与画面编辑)
- [11. WP-F：DGRS 媒体读取与生命周期](#11-wp-fdgrs-媒体读取与生命周期)
- [12. 数据变更、开发数据与版本收尾](#12-数据变更开发数据与版本收尾)
- [13. 测试、视觉与性能验收](#13-测试视觉与性能验收)
- [14. Definition of Done 与交付清单](#14-definition-of-done-与交付清单)
- [附录 A：节点介绍说明覆盖与写作要求](#附录-a节点介绍说明覆盖与写作要求)
- [附录 B：来源索引](#附录-b来源索引)

---

# 1. 依据、优先级与施工边界

## 1.1 本 PLAN 的依据

**用户原始审计**：`0.3.3.0审计(1).docx`，25 页、25 项；与本对话同名无 `(1)` 的上传版本内容一致。本版沿用原审计编号，不重新编号掩盖遗漏。重点视觉证据包括：第 18—19 页撤销后连线错位、第 20 页头像配置弹窗、第 21 页语音编辑、第 22—23 页画面缩放与布局。

**讨论后的最终决定**：以本对话用户最后确认的要求为准，包括“流程驱动”启动条件、可命名【终止】、默认折叠【介绍说明】、物品提交角色、上升沿启动、标题阻塞、媒体磁盘驻留方案。

**源码基线**：编制时通过 GitHub 重新查询分支 HEAD，仍为以上 `64dd990…`；提交信息为 `feat(0.3.3.0): publish current construction snapshot`。此前的《0.3.3.0 人工审计 25 项源码核验报告》仅用于定位；其后被用户更新的修复建议不得优先于最终决定。

**证据边界**：本 PLAN 编制阶段进行的是文件、截图与源码核对，没有在用户 Windows / Minecraft 环境运行本版。本文中的性能门槛与测试是施工验收要求，不是已取得的结果。

## 1.2 冲突处理顺序

```text
用户在本轮讨论中的最终明确决定
  > 原始人工审计中未被后续修改的要求
  > 本 PLAN 的落实规则
  > 当前实现与旧版本文档
  > 助手此前已被推翻的建议
```

以下旧方案**不得再作为施工依据**：

- 图谱完全只读、只扫描 `EnterStory` 得到连线。
- 图谱顶部两个下拉框组成的“跨 Story 公共逻辑桥”作者入口。
- 每个 Story 天然自带一个固定 Flow 输入。
- 新增专门的 `EnterStory` / 跳转节点来代替图谱接线。
- 物品提交不需要角色、从任务菜单任意地点远程提交。
- 给物品收集新增“是否锁存”开关。
- 标题默认非阻塞。
- 将“逻辑一直 True”或“任一输入有变化”当作重复启动条件。
- 只改 Inspector 就认定新增属性完整落地。
- 把全部 `triggers` / `interact_actor` / `enter_region` 字符串一概视为应删除的旧触发器。

最后一项尤为重要：**现行【开始】的角色交互、进入区域、逻辑条件是本轮确认保留的启动条件，不得借旧清理计划删除。**废弃的独立节点路径与现行 Start 条目不是同一概念。

## 1.3 不变的基础

- Task 内部仍然只有 Logic；不加入 Flow、Story【执行】或完成事件 Flow 端口。
- 【奖励】仍是 Task 内的一次性资源结算包，支持多项物品/经验，支持正负增量；不能改回 Story 外部奖励或目标属性奖励栏。
- NPCID、ItemID、GroupID、身份迁移、指名器的 ID释放 / 实体解绑 / 物品解绑等语义不变。
- 单玩家、单 Story 同时最多一个 ACTIVE run；不能通过另一个启动入口插入第二条正在执行的 Flow。
- 已冻结的任务服务端权威 + 客户端只读缓存架构不变；不能回到打开菜单先清空再全量请求。
- 已冻结的会话 ESC、聊天/背包/其他 GUI、死亡与重连基础行为不变。
- DGRS 自包含，媒体指纹为内部标识，不引入用户可见 AssetID / ImageID / MusicID / BuffID / RewardID。
- 不新建第二套 Studio、通用编辑器框架、脚本语言或新旧双 Runtime。
- 不全局改现有连线基数、保存模型与资源引用语义来迁就某个 UI 修复。

## 1.4 明确不做

本版不开发环境留声机、红石播放、网易云/QQ 音乐链接解析、在线音乐平台适配、全服音频仲裁、Region/Environment 资源系统、货币/声望、每日任务、组队共享任务。审计 #25 的在线留声机方向记录为后续需求；**当前服务器媒体内存问题必须在本版修复**。

不重新挑选所有媒体格式/码率，不借内存修复放宽所有包大小限制，也不声称“在线播放不消耗流量或客户端缓冲”。

---

# 2. 人工审计 1—25 逐项落地表

下表是施工跟踪的最低集合。每一项必须在最终报告有 `实现位置 / 测试 / 截图或复现证据 / 结果`，不得以一个总 PASS 代替。

| 编号 | 原审计问题 | 0.3.3.1 最终处理 | 工作包 |
|---|---|---|---|
| A01 | 节点标题被资源名/执行子类型取代 | 全局“类型 [附加名]”，同步节点头与节点 Inspector；【终止】同样遵守 | A、D |
| A02 | “任务整体说明”冗长 | 统一为“任务说明”，不改说明内容含义 | A、C |
| A03 | 提交/区域参数只在 Inspector | 全字段节点内编辑；提交按最终“目标物品 + 提交对象”模型 | A、C |
| A04 | 目标描述顺序、假默认值、含义不清 | 描述在类型之前；真实空草稿 + 水印；短说明表明是玩家看到的目标文字 | A、C |
| A05 | 区域排版、维度备注 | 复用 Start 的 XYZ 分组；删除备注字段；方块坐标；玩家仅看 XYZ | A、C |
| A06 | 目标类型下拉顺序 | 实体击杀、角色交互、物品收集、物品提交、区域到达 | A |
| A07 | 奖励节点无内联编辑，默认零条 | 节点内可编辑奖励包；新建默认一条物品条目；允许删至零条 | A |
| A08 | 奖励物品裸下拉框 | 统一“奖励物品”等字段标签与控件排版 | A |
| A09 | 节点仍叫“动作类型” | 所有作者界面统一“执行类型”；内部 `action` 不为文案改名 | A |
| A10 | 新执行参数未进节点 | 原生各类型、BUFF/MOD扩展、指令模式逐字段对齐 | A |
| A11 | 下拉框闪退/选择丢失 | 复现事件链，修集合/失焦/选择刷新根因，禁止延迟掩盖 | B |
| A12 | 示例文字进入真实数据 | 全 Studio 待输入自由文本核查；水印不保存、不导出、不清除用户内容 | A |
| A13 | 说明文字层级混乱 | 统一小一号、次级颜色、换行；错误提示独立，不能一起弱化 | A |
| A14 | “生命增减”等标签别扭 | 按“生命值（增减）（1 点 = 半颗心）”等正式用语统一 | A |
| A15 | 高级选项只是悄悄增加下拉项 | 勾选即进入指令执行模式；关闭恢复原生模式；切换可撤销、不暗删草稿 | A |
| A16 | 指令名称、斜杠、冗长说明 | “指令执行”；作者输入 `/…`；只保留指定短提示；两端校验同步 | A |
| A17 | 跨 Story 架构/图谱衔接残缺 | 以最终边界投影 + 图谱直接 Flow/Logic 接线模型完成作者、打包、运行闭环 | D |
| A18 | 黑色空白区域不能拖动 | 所有节点非交互空白区可拖；控件/端口有自己的手势 | B |
| A19 | Ctrl+Z 后连线错位 | 布局操作整体结束后重算端口锚点、线条和命中几何 | B |
| A20 | 头像弹窗、差分命名、列表难用 | Actor Inspector 内编辑；默认头像 + 头像差分；可识别的缩略图列表 | E |
| A21 | 语音/音乐仅 Inspector、不能试听 | 节点内入口与参数；共用本地播放/暂停、可拖进度条和时长 | A、E |
| A22 | 数字不能左右拖动 | 公共数值控件，左右增减、可直接输入、一次手势一次撤销 | B |
| A23 | 画面缩放不可用 | 在现有宽高模型上补齐四边四角、参数同步和可靠命中 | E |
| A24 | 参考框、图层排序与预览布局差 | 对话框/选项框开关对称；参考真实游戏布局；拖拽排序；列表不挤走预览 | E |
| A25 | 留声机在线方向、媒体资源占用 | 在线留声机明确延期；已存在的 DGRS 媒体整份入堆问题本版修复 | F |

### 后续讨论新增的必交付项

| 编号 | 最终要求 | 工作包 |
|---|---|---|
| D01 | 新增 Start 启动条件“流程驱动”；每条映射一个图谱 Flow 输入，不设固定默认入口 | D |
| D02 | 【终止】可命名；稳定边界身份；图谱出口名同步，标题始终“终止 [名称]” | A、D |
| D03 | 图谱只允许节点布局、视口与连线操作；节点新增/删除/内容修改仍由项目/Story 管理 | D |
| D04 | 所有正式节点有默认折叠的“介绍说明”，取代节点 Inspector 保存状态行 | A |
| D05 | 物品提交增加“目标物品”，原目标对象改“提交对象”并选择角色 | C |
| D06 | 收集完成不可逆；Task 仍为 Logic 图，后续目标按现有前置逻辑激活 | C |
| D07 | ACTIVE Story 所有 Start 均禁重入；流程驱动仅启动被命中的对应分支 | D |
| D08 | 可重复 Story 的逻辑启动严格 False→True；持续 True 不重启，重登/重载不伪造边沿 | D |
| D09 | 标题阻塞直至动画结束，保留现有行为 | 回归 |
| D10 | 接受磁盘驻留 + 流式校验 + 按需解包缓存 + 分块传输，不改变 DGRS 自包含 | F |

---

# 3. 已确认语义：不得重新打开的设计问题

## 3.1 跨层级边界

| Story 内部声明 | 故事图谱中的 Story 端口 |
|---|---|
| 【开始】的一条“流程驱动”启动条件 | 一个 Flow 输入 |
| 【开始】角色交互 / 进入区域 / 逻辑条件 | 不直接新增图谱端口 |
| 一个【逻辑输入】 | 一个 Logic 输入 |
| 一个【逻辑输出】 | 一个 Logic 输出 |
| 一个【终止】 | 一个 Flow 输出 |

**没有“流程驱动”条目就没有图谱 Flow 输入。**多个条目就是多个可区分入口，不把它们合并成固定 `flow_in`。

图谱连线类型只允许同类：Flow→Flow、Logic→Logic。继续保持：

```text
Flow 输出：最多一个目标；Flow 输入：允许多个来源。
Logic 输出：允许多个目标；Logic 输入：最多一个来源。
```

同一 Flow 输出只能选一条去向，不表示所有不同启动条件可以并发启动同一 Story。

## 3.2 Story 生命周期与逻辑边沿

- 未启动：有效启动条件可创建当前玩家的首个 run。
- ACTIVE：所有 Start 条件均不能再次启动；不排队、不抢当前流程、不重置正在进行的任务。
- TERMINATED + once：以后不再启动。
- TERMINATED + repeatable：新的有效触发才创建新 run，并重置该 run 的内部进度/执行状态。
- 流程驱动命中哪个启动条目，就从【开始】对应的那个流程输出继续；不激活其余输出。
- Logic False→True：一次启动尝试；True→True：无触发；True→False：更新观察值，等待下一次 False→True。
- 在 ACTIVE 期间发生的启动尝试直接拒绝，不在 Story 结束后补发。
- B 结束不能把其启动条件的已观察 True 清成 False 来制造新边沿。
- A 新 run 重置内部状态产生 False，随后再次变 True，才可再次启动可重复的 B。

“可循环”按用户描述理解为现有 **可重复 / `repeatable`** 策略，不再添加第二个相同含义的开关。

## 3.3 任务、区域与标题

**物品收集**：检查当前背包；满足要求后目标完成，本 run 内不回退；不扣物品。不增加锁存选项。这里的“进入下一目标”是现有 Logic 前置条件被满足，不是给 Task 创造顺序 Flow 游标。

**物品提交**：目标物品引用资源库物品；提交对象引用资源库角色。与匹配的实际实体交互时，服务端检查 ACTIVE 目标与当前背包，足够则扣除完整所需数量并完成；不足一件不扣。不是“负数奖励最多扣到零”的宽松规则。

**区域到达**：使用所在方块坐标，负坐标按向下取整；维度仍参与服务端判定；XYZ/半径使用整数方块单位与含边界立方体。玩家任务菜单只显示目标 XYZ，不显示维度 ID、备注或半径。

**标题**：淡入、停留、淡出全部结束才推进下个节点。不能为提高流畅度改成非阻塞。

## 3.4 界面原则

- 普通字段不能仅在 Inspector 编辑；节点是正常作者入口，不是空壳。
- 标题保留类型：`会话 [酒馆聊天]`、`执行 [物品给予]`、`终止 [隐藏结局]`。
- 节点 Inspector 的“已保存/未保存”行替换为 `▸ 介绍说明`；默认折叠。
- 帮助内容准确解释用途、必要场景、端口和容易误用的行为；不写入故事数据。
- 【画面】完整构图器、Actor 头像列表可以留在 Inspector 的合理空间中，但节点不能因此缺少可用的参数/入口/状态。

---

# 4. 当前源码定位与共因

下列是核验后的起点；施工时用符号定位，不把行号当稳定接口。完整固定提交链接见附录 B。

| 区域 | 关键文件/符号 | 基线事实与应处理的差异 |
|---|---|---|
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
| 缓存历史清理 | `StoryPackageLoader.cleanupRuntimeResidue()` | 当前 reload 会清理 `.dgrs-runtime` 历史残留；如复用目录必须替换这条破坏性清理路径 |

### 本版必须保留的诊断谨慎性

A11 的 Popup 偶发失败，已有具体可疑刷新链但未实机确定唯一根因；A19 的布局时序与事件订阅缺口置信度高，但仍要复现验证。不得在修复报告中把“怀疑”写成“已经证明”。

---

# 5. 工作包与施工顺序

## 5.1 WP-0：开工基线与一次性定位

1. 读取仓库根及实际修改目录中的 `AGENTS.md`；按真实可用工具执行仓库要求的分工，不伪造 Worker 或工具调用。
2. 确认 worktree、HEAD、未提交修改；保护用户已有改动，不执行清理式 reset。
3. 从本 PLAN 的精确基线创建施工分支。若远端有更新，先生成差异表并调整源码定位，不能偷偷换基线。
4. 备份当前测试项目、测试存档与已安装 DGRS，记录路径；备份不是新 Runtime 兼容层。
5. 运行原版构建/测试/现有有效探针，记录基线已有失败与环境限制；不把旧失败归功于本版修复，也不跳过。
6. 生成三份轻量清单：审计追踪表、节点字段对齐表、受影响文件/符号表。直接用 Markdown/测试数据，不造管理平台。

**退出条件**：基线可追踪；所有 A01—A25、D01—D10 有归属；用户已确认语义不再列为待拍板 Gate。

## 5.2 推荐顺序

| 顺序 | 工作包 | 原因与阶段验收 |
|---|---|---|
| 1 | WP-B 图交互根因修复 | 先稳定下拉、草稿、拖动、布局撤销，避免后续所有新增控件建立在错误事件链上 |
| 2 | WP-C Task 契约与呈现；WP-D Story 边界/运行契约 | 先固定提交角色、区域与跨 Story 数据和测试，再接 UI；独立调查可并行，共享文件由主负责人合并 |
| 3 | WP-A 通用字段、标题、帮助、所有节点对齐 | 逐域完成真实两侧编辑，不能只改 ViewModel |
| 4 | WP-D 图谱端口与直连闭环；WP-E 媒体作者工具 | 用统一画布手势和已定边界实现，不再新建逻辑桥表单 |
| 5 | WP-F DGRS 读取链 | 可由独立负责人提前调查/实现；与 WP-D 的包格式修改合并时由主负责人持有集成权 |
| 6 | 全链路、恢复、安全、性能测试与视觉修整 | 在真实运行包和真实发布目录上验收 |

同一工作包内做“契约 → 一条可运行路径 → UI → 回归”，不要所有 Schema 改完才第一次构建。每个节点/控件阶段均需截图，不把视觉审查推迟到最后一次。

---

# 6. WP-A：统一节点作者界面

## 6.1 字段完整性审计，不是白名单补丁

从正式节点注册、子类型 Schema、实际 Runtime 读取字段三处取并集，建立以下矩阵：

```text
Scope + node type + subtype + property
作者用途 / 是否内部字段
节点内：可见、可编辑、默认、验证
Inspector：可见、可编辑、默认、验证
双向同步 / Undo-Redo / 保存重开 / DGRS
```

`HasInlineFields` 补上节点名只是入口修复；模板、事件、数据源、验证和撤销必须一起完成。**不能只枚举注册表中的静态字段**，遗漏 Actions 扩展、奖励列表、画面层、Start 动态条目、头像差分和语音引用。

两侧共用同一字段语义/命令/验证。允许密度不同、用同一组小型复用控件；不为了消除 XAML 重复开发通用低代码表单框架。

### 必查覆盖

| 类别 | 本体与 Inspector 都必须具备的正常作者能力 |
|---|---|
| 目标 | 描述、类型、前置条件；适用数量/物品/角色；提交物品+角色；区域维度/XYZ/半径 |
| 奖励 | 条目列表、类型、物品选择、正负数量、增加/删除 |
| 执行 | 原生类型和对应参数；生命、经验、物品、传送、消息、BUFF、MOD扩展、指令模式 |
| 台词 | 角色、正文、头像差分选择、语音引用/导入/清除、试听入口 |
| 音乐 | 播放/停止、媒体引用、循环、淡入淡出、本地试听 |
| 标题 | 主标题、副标题、淡入/停留/淡出参数；显示阻塞含义 |
| 画面 | 图层/资源状态、增加/删除/选择入口、常用变换参数、进入构图器入口；完整预览在 Inspector |
| 开始 | 每个启动条目的名称、类型与适用参数；可重复；新流程驱动条目 |
| 终止/结束/逻辑边界 | 公共名称与必要编辑；节点类型保留；公共端口投影同步 |
| 与/或、选择、结算等动态端口 | 现有端口增删/命名操作不退化；介绍解释准确；不能因为无普通 TextBox 而不提供说明 |
| 会话/任务聚合 | 正确资源摘要、边界、导航和帮助；不在聚合上复制整个子资源数据 |
| 图谱 Story 投影 | 名称、端口、帮助和查看/进入故事；不得编辑其内部内容 |

复杂媒体控件可提供一个明确“在 Inspector 编辑”入口，但**不能把普通参数全折成摘要**，再次违背节点内可编辑要求。

## 6.2 标题与命名

统一格式：

```text
无附加名：节点类型
有附加名：节点类型 [附加名]
```

类型从 `(scope, type)` 取得；附加名来自资源显示名、执行子类型或用户命名边界。方括号只属于显示格式，不写回资源名或边界名。

- 终止名称在节点内、Inspector 修改均同步图谱出口。
- 改名只改显示文本，不换节点 ID / `port_id`，不断已有线。
- 避免 `终止 [终止 [普通结束]]` 和重复包裹。
- 空草稿时仍显示“终止”，不能连节点类型一起消失。
- 名称冲突沿用公共边界的有效性规则，不能按列表下标给线路重绑。

## 6.3 默认值与水印

所有待填写的自由文本统一审计：目标描述、台词、消息、指令、MOD ID/BUFF名、标题文本以及其他正常输入项。**示例不是默认内容。**

- 新建目标描述为空；展示“玩家将在游戏中看到的目标文字。”作为辅助说明。
- 目标描述排在目标类型之前。
- 空且未聚焦显示水印，聚焦隐藏水印；真实已输入文本绝不能因点击而清空。
- 中文输入法组合态、负号、小数未完成态不立即写进运行数据或被强制改回。
- 草稿允许暂时为空；保存草稿不能凭空补“消灭史莱姆”；导出可执行内容时正常验证必填项。
- 执行类型切换保留共用文本，不把其他类型的无关字段偷偷带入包；适用草稿按现有编辑事务保存，避免切回丢失输入。
- 系统生成的稳定身份、启动条件自动编号、合法数值默认值不属于示例文本清理目标。

**不要全局取消 Validation 以实现空草稿。**将编辑草稿、正式字段提交、运行包校验按既有编辑层最窄地分离。

## 6.4 奖励默认与列表

新建【奖励】默认一条 **物品** 条目，物品为空待选择，数量工程默认 `+1`。不自动选择项目第一个物品。点击加号可继续添加，允许删至零条；空奖励包合法地无资源变更，不应永久挡住 Task 结算。

物品字段明确标“奖励物品”；条目类型、数量、删除控件对齐。正负增量语义与现有奖励执行器一致；不能把 `-3` 当成非法配置。一次奖励包结算仍只执行一次，不因列表重排产生重复发放。

## 6.5 执行文案和高级模式

正式标签：

```text
执行类型
物品数量（增减）
生命值（增减）（1 点 = 半颗心）
经验值（增减）（XP points）
持续时间（增减）（秒）
等级（增减）（等级从 1 开始）
```

较长单位允许排为相邻的小字说明，不能靠截断“看起来整齐”；节点与 Inspector 语义一致。

高级选项关闭：原生执行类型。开启：直接出现 **指令执行** 配置，不要求再从普通下拉找一次。一次切换一个撤销事务；关闭时保存指令草稿并恢复先前原生类型，不暗自变成另一条已执行内容。

作者输入完整单行 `/…`。Studio 和服务端在一致边界校验；交给 Minecraft 命令执行器时只作一次所需的前导斜杠处理，不把文本重新解释成脚本。保留现有受信包、权限上下文和服务端身份控制，客户端不能借表单修改获得任意命令执行能力。

常驻辅助提示只用：

> 除非必要，否则建议优先使用 Studio 原生节点功能。

技术权限说明放开发文档，不堆在普通 Inspector。指令模式详情可放折叠【介绍说明】，不另造警告弹窗系统。

## 6.6 默认折叠的【介绍说明】

节点 Inspector 用以下结构取代保存状态行：

```text
终止 [隐藏结局]
▸ 介绍说明

节点属性
出口名称：[隐藏结局]
```

- 默认折叠；点击展开/再点击折叠；不是弹窗或必须悬停才能阅读的 Tooltip。
- 本 PLAN 的轻量 UI 落实规则：**新选择节点时折叠，同一节点的参数刷新不重置用户展开状态**。不持久化至 Graph/DGRS，不影响 Dirty 或 Undo。
- 帮助展开后纳入受限可滚动内容区域，不让固定标题行因长说明占满 Inspector，参数始终可达；折叠后不留大块空白。
- 所有正式节点均覆盖，包含无参数节点、必需固定节点、聚合节点、图谱 Story 投影；不能只遍历可新增菜单。
- 节点功能/端口帮助按 `(scope, type, subtype)` 解析；共享概念共用文本，差异处准确说明。
- 【执行】随类型、BUFF 模式变化；目标随目标类型变化；Start 解释各启动条件；动态端口按当前名称与类型说明。
- 说明元数据归 Studio，优先扩展既有节点定义或相邻的轻量帮助定义；不把中文帮助复制进每个节点 Properties。
- `已保存/未保存` 仅从节点 Inspector 展示移除；不删实际脏状态、保存保护和退出询问逻辑。

帮助应包含“用途 / 必要场景 / 端口 / 重要规则”，不要求所有节点机械显示四个空标题。详细程度以清楚为准，不堆重复术语。附录 A 给出最低准确性要求。

## 6.7 验收

建立 `NODE_INSPECTOR_PARITY_GATE`、`NODE_HELP_COVERAGE_GATE`：

- 每个正式字段有两侧操作或明确的大型编辑器例外，不接受空白例外。
- 两侧编辑相互同步，保存/重开/撤销/资源改名后仍一致。
- 切换所有子类型不出现遗留字段、错位、焦点跳失、非法默认。
- 每个节点都能展开说明；文案必须人工对照运行行为检查，字符串非空测试不代表正确。
- 所有新增/改动 UI 有 Dark/Light 与窄 Inspector 截图。

---

# 7. WP-B：图编辑交互修复

## 7.1 下拉框闪退与选择丢失（A11）

最小复现集包括目标类型/目标资源、执行类型、奖励条目类型、Start 条目类型。录制快速开关与“刚编辑文本后立即打开下拉”的情形。

调试时记录：Popup 打开/关闭、鼠标按下/抬起、焦点转移、失焦提交、SelectionChanged、ItemsSource 实例、DataContext/选中节点、集合重建。记录用开发日志，不能在发布版每帧刷。

优先检查并修复：

- getter 每次产生新选项数组；
- 一次字段更新导致整个条目集合 Clear/Add；
- 失焦提交重建当前控件，导致正在进行的 Popup 点击丢失；
- 控件事件/画布事件重复捕获；
- 刷新回填触发另一次业务提交。

保持稳定选项与条目身份、增量刷新、去除重入。不得使用固定 sleep、禁用快速点击、增大点击延时或吞掉所有异常当修复。

**验收**：上述四类下拉分别连续快速操作至少 100 次，穿插文本失焦、Undo/Redo、节点切换；有效选择不丢失、不闪退、不把鼠标动作变成节点拖动。无法稳定复现时必须保留调查记录，不伪报确定根因。

## 7.2 节点拖动命中（A18）

节点标题及非交互空白内容区均可拖。TextBox、ComboBox、按钮、CheckBox、滚动条、端口、图片缩放手柄、音频进度条、数值拖动区域仍独占对应手势。

- 用户点击/选字/拉下拉不能移动节点。
- 不把整个参数区一概禁止拖动；间隙和背景可拖。
- 单击选择不立即变拖动，遵守合理移动阈值。
- 拖动原有 Shift 插线、框选、多节点移动、缩放坐标变换不回归。
- 图谱 Story 节点复用同一手势规则，但增删/编辑能力由图谱权限限制。

## 7.3 Undo 后线几何（A19）

核查 `ApplyLayoutSnapshot → X/Y 通知 → Canvas.SetLeft/Top → 端口 TranslatePoint → 重画` 的布局时序，以及 `LayoutChanged` 是否被视图正确订阅。

修复要求：一次布局事务应用完所有节点位置后，合并调度一次布局完成后的锚点刷新；同时刷新可见线、命中线、端口命中区。若需要 `UpdateLayout`，只在整组更改后最窄使用，不能每一个 X/Y Setter 全图强制重排。[T04]

同样覆盖：参数展开导致节点变高、动态端口增加、标题换行、字体/DPI变化、图谱投影边界更新。不能只特判 Ctrl+Z。

**验收**：单/多节点移动，Undo/Redo 50 轮；改变缩放、平移、节点参数展开后重复。线始终贴合端口，无需手动再拖一下；撤销只恢复编辑数据，不保存派生的曲线点。

## 7.4 公共数值编辑（A22）

沿用已有 TextBox 外观，补充左右拖动调值。点击可输入；非文本编辑状态拖过阈值进入调整；已在编辑态时保留选字/输入法。交互区须清晰可发现，不得只悄悄支持标签拖动而用户要求的数值框无效。[T06 仅作参考，不替代本条交互要求]

- 向左减少、向右增加；依字段定义选择整数/小数步进与范围。
- 一次拖动只提交一个 Undo 记录；Esc 取消手势，恢复原值。
- 不在每次 MouseMove 写盘、全图重载或验证全部资源。
- 维度可负、资源增量可负；半径非负；物品需求数量正整数；宽高不可负；时长/位置按所属字段规则。
- 输入框空值、单独负号等仅是草稿；不能强制变零再覆写原数据。
- 内联与 Inspector 使用同一控件行为；拖数字不能同时拖节点/画布。

---

# 8. WP-C：任务目标与玩家呈现

## 8.1 物品提交：两个独立资源引用

正式表单顺序：

```text
目标描述
目标类型：物品提交
需求数量
目标物品：资源库物品选择
提交对象：资源库角色选择
前置条件
```

保留 `item` 的物品含义，增加角色引用（优先沿用现有 `actor_id` 字段语义）。不将同一 `target` 字段按 UI 状态一会儿存物品、一会儿存角色。两端 Schema、加载验证、资源依赖、引用完整性、复制/导入/导出均区分这两项。

候选来自当前允许引用的物品/角色资源，继续遵循现有 ItemID/GroupID 和 Actor 身份匹配能力；不新增手填物理实体 UUID 模型，也不破坏 NPCID 迁移后仍可提交的能力。引用包资源继续只读，不伪造本地资源。

旧 `submit_item` 没有 `actor_id`：一次性开发转换时保留物品/数量/描述，提交对象留待作者明确选择；禁止猜为酒馆老板或资源库第一项。草稿可保存，缺目标的可执行任务禁止导出。

## 8.2 游戏提交路径

```text
真实玩家与实体交互
 → 服务端验证实际实体及其 DGR 身份
 → 查本玩家当前 ACTIVE 提交目标
 → 匹配提交对象
 → 按既有物品规则检查当前背包
 → 足够：同一次事务完整扣除 + 完成目标
 → 不足：不扣、不完成
 → 推送任务状态；由 Task Logic 决定下一目标/奖励/结算
```

复用 `CanonicalTaskInventory`、`CanonicalTaskPlayerTransactions`、现有 SavedData 提交收据与恢复机制；不要把安全事务降级为“先改任务，稍后扣物品”。

必须移除原任务菜单“任意地点提交”的入口，关闭/改造对应远程提交消息路由，不能只隐藏按钮而旧包仍可绕过角色要求。任务菜单仍可展示待提交内容与对象，但不获得脱离实体交互的扣物品权限。

工程收口规则（不增加作者开关）：

- 只以交互发生前已 ACTIVE 的提交目标为候选；一次交互不能完成一个目标后又顺带递归完成刚激活的下一提交目标。
- 同一次物理交互不应重复走两个扣除路径。
- 若同一角色有多个可提交目标，复用现有候选选择/重验证模式让玩家选一个；禁止按遍历顺序偷偷提交全部或重扣同一堆物品。
- 成功提交消费本次提交操作；不要把该次点击再送给刚由提交激活的后续对话/Story Start，造成跳过内容。
- 不匹配的实体不应被 DGR 无条件占用交互；无库存的匹配目标保持待提交，可给简短缺少物品反馈。
- 多别名/Group 匹配到同一目标时先去重；服务端检查实际距离、存活/当前世界和已建立的交互合法性，不相信客户端传来一个 actor_id 就扣物品。

以上是实现防误提交所需边界，不能扩成新的 NPC 对话/交易系统。

## 8.3 收集与前置逻辑

保留当前背包计数与 COMPLETED 不回退，补测试而非新增运行模式。背包数量未达标时显示当前数，不把历史捡到数量当存量。

收集完成后其他目标/奖励/结算按 Task Graph 求值；没有连接的目标不凭空按画布位置成为“下一项”。未激活目标继续对玩家隐藏。新 Story run 重置所属 Task/目标，不影响别的 Story 的进度。

## 8.4 区域与坐标呈现

删除 `dimension_note` 的正式写入、校验、视图和无用途序列化；一次性清理当前开发数据，无兼容 Runtime。

```text
blockX = floor(player.posX)
blockY = floor(player.posY)
blockZ = floor(player.posZ)

同一维度
且 centerX-radius <= blockX <= centerX+radius
且 centerY-radius <= blockY <= centerY+radius
且 centerZ-radius <= blockZ <= centerZ+radius
```

采用当前 1.7.10 环境中的方块取整方法，不能用负数截断。中心与半径用整数；判定计算使用足够宽的类型避免加减溢出。Radius=0 对应中心那个方块；Radius=1 是每轴中心±1 的含边界范围。按玩家所在方块判定，不改成脚部 AABB 相交、球形或二维圆。

区域字段按已存在 Start 视觉语言分组：维度一行、XYZ 一组、半径一行。玩家呈现只增加可选目标 XYZ：

```text
□ 前往矿井入口
  坐标：120 / 64 / -350
```

任务菜单不展示维度 ID、半径或新造的维度翻译；目标文字仍由作者说明地点语境。技术字段在 Studio、调试与服务端内部可用。

通过真正的 Task UI 投影/消息链同步坐标；未激活目标不能提前泄露坐标。旧快照不能覆盖新快照；关闭重开任务窗口不能先清空缓存。

## 8.5 Task 验收用例

- 两个下拉候选种类正确；两侧编辑、保存、导入导出完整保持 item+actor。
- 背包跨槽位足够、刚好、不足；不足零扣除；成功只扣 required。
- 错误角色、错误距离、已完成目标、旧 activation/run、重发请求均不扣物品。
- NPCID 迁移到新实体后匹配新实体，旧实体不能凭残留客户端状态提交。
- 两个提交任务共用角色/物品，选择其中一个只扣一次；同一点击不级联提交下一阶段。
- 崩溃恢复既不重扣，也不把未扣物品的目标直接补成完成；复用事务的恢复证据必须重跑。
- 收集达标后丢弃/提交/重登不回退；新 run 才重置。
- 区域测试正负坐标、`-0.1`、六面/八角边界、半径 0、错误维度；XYZ 显示一致且无 ID 泄露。

---

# 9. WP-D：跨 Story 图谱、边界与运行时

## 9.1 作者模型与允许操作

Story 资源是唯一内容定义。项目图谱的 Story 节点与端口从资源派生，不保存一份可以独立编辑的 Story 副本。

允许：节点选择/移动、视口平移/缩放、搜索/适配、连线/重连/断线、撤销重做、查看介绍与详情、进入对应 Story。

不允许：在图谱空白处新建 Story 节点、删除 Story 节点、复制出第二个 Story 投影、编辑节点内部内容或直接改公共端口名。Story 新建/删除仍走项目资源操作；进入故事后才编辑内部图。引用包 Story 的只读约束不变。

删除顶部“跨 Story 公共逻辑桥”表单。既有已保存 Logic 边要转换并直接画到端口上，不能删 UI 时把数据一起丢掉。

## 9.2 Start“流程驱动”

新增正式启动类型 **流程驱动**。本 PLAN 采用建议内部键 `flow_driven`；若有同义正式键可复用，最终只保留一个，不用 legacy `enter_story` 兜底。

每条条目拥有既有样式的稳定身份、名称和内部 Flow 输出。该条目在父级图谱额外投影成一个 Flow 输入，二者通过同一条目身份对应。

```text
图谱：A 的出口 → B 的「来自第一章」输入

B 内【开始】：
  流程驱动「来自第一章」输出 → 首次介绍
  流程驱动「隐藏入口」输出   → 隐藏线
  角色交互「导师」输出       → 其他入口
```

命中「来自第一章」只走其输出。其余两个不触发。没有 Flow 驱动条目的 Story 没有父级 Flow 输入，这是合法独立故事，不应一律报“孤立错误”。

Start 内部仍不增加一个可被 Story 内节点反向连接的 Flow 输入；外部输入属于父级图谱，不破坏【开始】节点形状。

“逻辑条件”短说明统一为：**逻辑输入端口为 True 时启动。**折叠介绍中进一步准确写明 False→True、ACTIVE 禁重入及可重复规则，不在常驻提示中堆技术文字。

## 9.3 终止与稳定端口

【终止】新增公共出口名称及稳定 `port_id`，沿用 Session【结束】的边界表达方式。终止节点仍只有内部 Flow 输入，不伪造 Story 内部 Flow 输出。

```text
Story 内标题：终止 [隐藏结局]
图谱 Story 出口：隐藏结局
```

新增、改名、删除/撤销边界时，图谱增量同步。改名/重排不改变身份；删除端口只处理相关线，不能把旧连线按序号挪给下一个端口。Undo 必须恢复原身份、原连线和布局；复制边界节点必须分配新身份。

节点/边界身份均由工具管理；不增加用户输入的 StoryPortID 系统。

## 9.4 Project 连接数据

将现有 Logic-only 项目连接契约窄扩展/收敛为唯一的项目 Story 连接模型，支持：

```text
source_story_id + source_port_id
  → target_story_id + target_port_id
interface_kind = Flow | Logic
```

可沿用现有边 ID/布局文件机制。名字与端口数组不再作为第二份权威定义，验证时从 Story 边界解析。具体类名由现有代码最小改造，不并排保留“新的 Flow 系统 + 旧的 Logic 桥 Runtime”。

只允许在同一图谱层连接公开端口。Logic 输入缺线沿用既有默认语义；Flow 输出缺线表示该 Story 在此结束，不是必须强行连接一个后继 Story。

图谱画线、统计、诊断统一读取此连接模型，移除仍基于空 `EnterStory transitions` 的统计与误报。显示端口类型、有效目标和真实连接，不保留没有端口的装饰性 Story 卡片。

## 9.5 服务端跨 Story Flow

在终止时记录准确出口身份；一次源 run 的终止只产生一次跨 Story 启动尝试。

```text
源 Story 到达命名终止
 → 保存本 run 的终止结果
 → 查询这个出口在当前包集合中的 Flow 去向
 → 校验目标 Story 和精确 flow_driven 条目
 → 按目标现有 Start eligibility 尝试启动
 → 仅从对应条目输出继续
```

- B ACTIVE：不启动、不排队、不改变 B；A 仍正常结束。
- B once 已结束：不启动；A 不因此变 ERROR。
- B 未启动或 repeatable 已结束：按有效新触发创建 run。
- 目标引用缺失/端口类型错误是内容验证问题，与“目标正在运行”的正常拒绝分开诊断。
- 不因重连、恢复终态快照、重载或重复通知再启动 B。
- 跨 Story 路由接入既有单玩家状态/持久化协调路径，不创建第二套 Story 调度器。

工程上必须将“终止出口已路由/已拒绝”与源 run 绑定，并使用现有 SavedData 的最窄可恢复记录。禁止只保存一个全局 `bool routed`，导致新 run 永远不能再次转场。不能仅凭口头“一次性”宣称异常恢复可靠；需要故障注入验证。

终止后的清理不得提前销毁仍被其他 Story 读取的终态公共 Logic；现有“终态可读，真正新 run 才重置”规则继续成立。

## 9.6 精确 Logic 上升沿

现有 `propagateStoryLogicTrusted` 按目标外部输入 Map 是否变化来决定重试，这不够精确。本版应按**具体启动条件最终求值结果**检测 False→True，而不是任意输入变化都重新试所有仍为 True 的条件。

需要区分：

- 正常公共 Logic 值持续传播，用于正在运行的条件/其他业务；
- Start 只消费本启动条目的上升沿；
- ACTIVE 时拒绝启动，但仍更新观察值，不能把边沿积压到结束后。

每玩家、每 Story、每稳定 Start 条目保存必要的已观察状态；它不是新增用户变量，也不能随 B 的 run reset 错误清空。首次从未观察过的条件按 False 基线处理；恢复已有状态时不能把持久 True 伪装成首次 True。

A 新 run 重置输出为 False 的变化必须真正进入传播路径。即使 A 在同一 tick 很快又走到 True，也不能只发布最终 True 而丢掉中间的 False。可用现有有序状态更新/短队列处理，不建立通用事件总线平台。

同一 Story 的多个条件同轮有效时仍只允许一个 run；沿用稳定条目顺序/既有选择规则，测试和说明一致。Flow 循环与 Logic 传播使用现有有限步安全保护；不能通过禁止所有跨 Story 循环来偷换 repeatable 语义，也不能无界递归锁死服务器。

## 9.7 包边界与协作

图谱新连线必须真正进入 DGRS，不只保存在 Studio 视觉布局。

沿用目前“源 Story 拥有其向外连接”的导出归属，将 Flow 和 Logic 同样处理：导出 A 包携带 A 的出边；B 是明确的外部目标，不把整个 Project 或 B 的全部媒体递归打进 A。

同步处理 exporter、manifest 资源声明、Java loader、包集合 merger、fingerprint、引用包/导入包路径。加载单包可以保留外部引用待包集合验证；**实际激活路由前必须完成目标 Story/端口/类型验证**。不因为 A 安装早于 B 就永久丢弃 A 的连接。

源 Story 来自只读引用包时不得在图谱偷偷修改其出边；需要可编辑的本地源。连接到外部目标只能使用其已暴露的真实边界。

保留已验证包集合原子替换/失败保留旧集合的行为。禁止同一连接由 A、B 两包重复执行；已有 Logic-only 项目数据一次性转换，随后只有新 canonical 路由。

## 9.8 Story 验收矩阵

1. B 无 flow_driven：图谱无 Flow 输入；增加两条只增加两个准确命名输入。
2. A 两个【终止】，B 两个 flow_driven：分别接线并运行，各走各的入口，未选出口不触发。
3. A/B 公共 Logic 输入输出直连、改名、重排、删除/撤销、保存重开，身份与线保持正确。
4. 图谱新增/删除/复制节点被禁止；移动/接线/断线/重连和 Ctrl+Z 正常；进入故事仍可导航。
5. B ACTIVE 正在任务中，收到另一入口 Flow/Logic：当前任务不重置，无第二条 Flow、无排队；A 正常结束。
6. B once 已完成：所有新触发无效；B repeatable：新的 Flow 或新的 Logic 上升沿才启动。
7. A True→B 完成→A 持续 True：B 不重启；其他无关输入变化也不能启动 B。
8. A 新 run False→之后 True：repeatable B 启动第二次；once B 不启动。
9. True 状态下重登、服务器重启、包重载：不伪造边沿、不重放终止出边。
10. 同一 tick False→True、ACTIVE 期间边沿、多入口同轮、循环安全保护均有确定结果。
11. A/B 分别导出与安装，Flow/Logic 和媒体依赖正确；缺包/改端口给明确诊断；无整项目递归打包。
12. 故障发生在 A 终止、路由记录、B 启动前后：恢复不重复触发副作用或永久丢失已提交的合法启动，使用具体持久化证据说明边界。

---

# 10. WP-E：头像、音频与画面编辑

## 10.1 Actor 头像编辑

将 `ActorPortraitDialog` 的正式入口与能力移至现有 Actor Inspector，不再让用户先开独立配置弹窗。系统文件选择对话框仍可用于导入，这不等于恢复旧头像编辑弹窗。

统一用“默认头像”“头像差分”；删除作者界面的“表情”“表情变体”用词。数据内部 `portrait_variants` 不为文案改名。

默认头像预览与导入/更换/清除；差分列表显示名称、缩略图、选中态并能增删编辑。列表独立受限滚动、预览不被列表挤走。保留原有差分数据和撤销能力，不能因为重做界面先清空全部差分。

重命名差分要同步受影响台词引用，或使用现有明确的引用完整性处理；不可静默失效后显示另一张头像。媒体 GC 仍保护有效引用、未保存草稿和 Undo 可恢复引用。只读 Actor 不可修改但可查看。

## 10.2 音频本地试听

一个共用的 Studio 试听服务/控件用于台词配音与音乐：播放/暂停、可拖动进度条、当前/总时长，媒体名与清除/替换入口。

- 试听只播放本地项目媒体，既不上传、不触发故事、不改变游戏运行游标。
- 音乐节点的“播放/停止”是运行配置，试听播放/暂停是编辑器操作，两者必须明显区分。
- “导入并播放”若不能实际本地播放就改名；可统一导入后不自动发声，用户点试听。
- 拖进度能定位；解码/波形分析若需要不能阻塞 WPF UI；本版不强制新增波形图功能。
- 一次只保留一个作者试听声源，播放另一个停止前一个；替换/清除媒体、离开对应编辑上下文或关闭项目时释放播放器/文件句柄。
- 暂停和拖进度不产生 Graph Dirty/Undo，不进入 DGRS。
- 不能只用 Windows 上不保证支持 OGG 的播放器壳宣称完成：必须用本版实际 OGG 文件，在**打包后的 Studio** 里验证可播放/暂停/定位。
- 优先复用现有转码/解码依赖；若需额外依赖，做许可、部署和离线可用核对，选择最小实现，不把它变成媒体播放器平台。

节点内显示媒体状态和实际试听操作入口，Inspector 用更舒适的宽度展示同样能力。

## 10.3 画面操作与布局

保留“每个【画面】定义完整图像构成”的语义：执行下一画面整体替换；空层列表清空；不引入 ImageID 或隐藏单图的新节点。

现有右下角手柄扩展为选中层四边四角八个手柄，明确边框与光标；边只改一个方向尺寸，角改两个方向。对边/对角在拖动时保持几何上的预期固定；锚点坐标、归一化位置、width/height 同步计算。禁止因锚点不是左上角而缩放时图片跳位。

- 原有预设仍只是预填变换，不成为左/中/右身份槽。
- 预览缩放、不同 DPI 下手柄保持可点击尺寸；边界外图片仍能通过列表/参数选中调整。
- 零/负尺寸禁止；默认不偷偷添加翻转或强制等比例语义。
- 一次拖动一次 Undo；Esc 撤回；数值输入和拖动结果一致。

Inspector 用现有区域内的分区布局：固定可用的预览区、独立滚动的图层区、选中项参数区。不新增“第二 Inspector”概念。不让十几张图把预览挤出正常可见空间；窄窗仍能滚动到所有字段。

图层显示名称/缩略图/层级；支持直接拖拽排序，保存后的 z/order 与运行时绘制一致，Undo 恢复整次排序。不要只改列表顺序而不改实际图像叠放。

预览下方两个开关仅叫 **对话框 / 选项框**，围绕预览中心轴对称排布。两者是只读参考，不可拖动或缩放，不进入 DGRS。折叠/隐藏参考不清除真实运行 UI。

选项框依据 `CanonicalChoiceLayout` 与 `CanonicalDialogueLayout` 的实际布局比例投影到预览；横向居中、位于对话框上方，不再硬编码右侧矩形。预览对应的是明确的参考布局，不承诺知道每位玩家自定义后的所有 UI 位置，也不为此新增联网预览系统。

## 10.4 媒体验收

- 默认头像、多个差分、改名、删除/撤销、重开、台词选择，图像不串。
- 实际 OGG 试听、暂停、定位、文件结束、切换/关闭，不卡 UI、不残留声源/文件锁；发布目录单独测试。
- 1/10/20 张图片的列表、预览和属性可用；拖序与游戏 z 一致。
- 8 个手柄 × 不同锚点 × 缩放 × Undo，图片几何正确。
- 开关名字/对称布局/参考位置与游戏截图对照；没有层时也能看参考。

---

# 11. WP-F：DGRS 媒体读取与生命周期

## 11.1 目标与非目标

**目标：消除完整媒体在服务器 JVM 堆中的不必要常驻与层层复制。**DGRS 继续带图片、头像、音乐和配音；不要求联网、不改成外部路径包。

目标结构：

```text
DGRS（用户安装、权威完整包，磁盘）
 → 有界读取/完整验证
 → Story/Task/Session 等结构 + 媒体索引（内存）
 → 首次需要媒体时流式解包（受控磁盘缓存）
 → 32 KiB 分块读取/发送（有界在途内存）
```

该选择利用 Java 8 已有 ZIP 条目流、增量摘要和定位文件读取能力。[T01][T02][T03] 不把“主流”当免测试理由；缓存策略与限制是 DGR 的工程落实。

## 11.2 必须覆盖完整分配链

不能只删 `LoadedStoryPackage` 的 Map。以下每一层都要审计与修正：

1. `DgrsArchiveReader` 打开时全部条目入堆、`readBytes` clone。
2. `StoryPackageSnapshotReader.requiredBytes` 和 Result 携带媒体完整内容。
3. 解包目录读取 `StoryPackageLoader.readDeclaredBytes/readBounded`。
4. `StoryPackageMediaValidation` 和 `StoryPackageContentFingerprint` 要求全部 byte[]。
5. `LoadedStoryPackage` 再 clone、长期保存与 getter 再复制。
6. 网络服务读取与队列、客户端暂存是否重新造成同量级全文件复制。

最后一项只做本版必要的有界传输回归，不借机重写客户端播放系统。

结构 JSON 允许有界加载和解析；媒体改为描述符/可打开读取源。不要保留一个名字叫缓存、实际仍是所有媒体 `byte[]` 的大 Map，也不要搬到无界 direct buffer / 内存映射后宣称“堆省了所以成功”。

## 11.3 校验不能缩水

包加载阶段仍完整核对：必需文件、路径、重复条目、资源类型/引用、声明媒体与可达媒体一致、每个媒体指纹、大小和容器完整性。

- 使用固定缓冲顺序计算 SHA-256，保持原媒体指纹语义。
- 包内容指纹保持既有排序、编码、内容范围；以相同输入得到相同结果的黄金测试证明“只改读取方式”。WP-D 导致内容变更时则正常得到新包指纹。
- 保留目前 PNG chunk CRC、IHDR/尺寸限制、IEND，OGG 页 CRC/Vorbis 标识/EOS，JPEG 已有结构检查的最低保护，不退化为只看后缀/魔数。
- PNG 大 chunk 边读边校验，OGG 页只保留有界页/缓冲，不能在验证器里绕回完整文件数组。
- 服务端验证容器不等于解码所有图片成像素或音乐成 PCM；不得在主 tick 解码大型媒体。
- 条目数量、单条目和累计解压大小按实际读取计数，不能只信 ZIP 声明；保留安全上限。本版不随意放大目前限制来掩盖实现缺陷。

流式验证仍需读过文件、消耗 I/O/CPU，不承诺加载零耗时。关键是有界内存、可取消、明确错误和不阻塞正常游戏 tick。

## 11.4 按需解包与分块

压缩媒体第一次被合法请求时，从受信包源顺序解包到受控 `.part`，完成校验后发布为只读缓存文件。缓存键按媒体指纹去重；同一内容来自不同故事包不重复解包。

随后用定位读取取得所需块，保留当前网络块长 **32768 字节**，不为了优化随意扩大消息。最后一块、乱序请求、合法重试都正确；禁止每个块重新从 ZIP 条目开头解压。

同一指纹并发请求合并到同一次物化，不允许每个玩家各开一个整文件解包任务。服务端设有界任务队列、并发与在途字节预算，连接中断/包卸载/请求失效能取消并释放。

文件 I/O、解压、散列不得放在服务器主 tick、Netty 事件循环或 WPF UI 线程。世界状态与发包合法性检查按既有线程边界执行，不能在工作线程直接操作玩家状态。

校验失败、磁盘满、权限错误、取消时只清理自己创建的临时文件；不能发布半文件或误删包源。暂时未准备好要与客户端现有重试/超时流程衔接，不用“发空块”假装成功。

## 11.5 包代际：不能拿旧索引读被覆盖的新包

当前整份字节脱离原文件，虽耗内存，却天然避免源包覆盖后读错内容。改磁盘读取必须补上这项保障；否则重载失败保留的旧包对象会指向新坏文件。

工程落实：为每个已接受的包 generation 保持**不可变、可校验的磁盘读取源**。优先采用受控的私有归档快照/已验证源句柄策略；Windows 原地覆盖与失败回退必须有实际测试。不能仅用原始安装路径 + offset 当可信索引。

建议实现为私有 generation 归档快照：以有界拷贝生成，验证候选后原子发布；运行时媒体按需从该 generation 读取。该快照不是第二份作者源，也不让用户维护；它会额外占用磁盘，必须在报告真实列出，而不是宣称“没有任何额外存储”。若采用其他更窄方案，必须同样证明原地替换、坏包回退和运行中请求一致性。

- 候选包无效：现有已接受 generation 和读取源仍可用。
- 新 generation 有效：原子切换索引；在途请求固定旧 generation 或明确取消重试，不能混发两代字节。
- 旧 generation 无有效引用和在途请求后清理；不永久保留所有历史包。
- 授权仍来自已安装包/当前许可的媒体引用；猜中指纹不能读取任意缓存或任意文件。

## 11.6 磁盘缓存清理与目录安全

若使用 `.dgrs-runtime`，**先替换当前 `cleanupRuntimeResidue()` 在 reload 时整目录删除的旧行为**。不得新缓存一建好就被下一次重载删除。

缓存内至少区分源代际、已完成媒体、临时文件的用途；所有路径由 DGR 管理，禁止任意用户路径、`../`、绝对路径、符号链接/Windows reparse point 逃逸。

生命周期：

- 正在读取、解包、等待发布或服务在途请求的文件不能删除。
- `.part` 在失败/取消后清理；启动时安全清理真正遗留的临时文件。
- 媒体缓存可按最近使用与总磁盘预算回收，未引用且无在途使用的优先删除；删除后需要时可重建。
- 包卸载后不再为新请求授权；安全结束/取消旧请求后回收相关 generation。
- 清理仅针对受控缓存，不删除用户 DGRS、其他包或 Studio 项目原始媒体。
- 不在每次普通保存/reload 无差别清空全部缓存，导致所有玩家重复下载/解包。

预算、并发数、块缓冲等集中为少量技术常量/现有配置，不新增复杂控制面板；给出测试环境、选值依据与最坏在途内存估算。不得把前面讨论过的“1 GB / 5 首”等直接套到服务器所有媒体。本版不重新定义已有客户端按类别保留政策，只保证不被本次改动破坏。

## 11.7 可验证的内存与安全验收

建立基线与修复版对照，使用同样 JSON 结构、媒体数与媒体总量变化的合法 DGRS。

- 包安装但无人请求媒体：常驻对象只有结构与索引，堆中不存在这些媒体完整内容数组。
- 同样条目数、媒体字节量显著增大：稳定堆占用不随媒体总字节近似同比增长；记录峰值/稳定值，不把 ZIP 压缩体积当解压后资源量。
- 并发下载增加：内存受队列/并发/在途块预算约束，不受每位玩家整首歌大小乘法支配。
- 校验与加载阶段也不整包聚集；只检查最终常驻是不够的。
- 相同媒体两包共享、并发首次请求只物化一次；可重建媒体缓存删除后重新请求可正确重建；服务器运行时使用中的 generation 源不得被清理。
- 坏 hash、损坏 PNG/OGG、重复路径、超长条目、未知大小 ZIP、路径穿越、断流/磁盘满均有负测。
- 原地覆盖、坏包重载保留旧包、新旧代际交接、客户端中断、卸载、服务器重启均通过。
- 记录 JVM 堆、文件句柄数、缓存磁盘字节、最大队列与 tick 影响；不能只贴“分块发送 PASS”证明流式加载。

---

# 12. 数据变更、开发数据与版本收尾

## 12.1 本版允许的契约变化

| 内容 | 变更 |
|---|---|
| Start | 正式 `flow_driven` 条目及对应父级入口投影 |
| Story terminate | 稳定公共出口身份、名称；终态快照能标明实际出口 |
| Project Story 连接 | 统一 Flow / Logic 类型连接，替代 Logic-only 作者表单数据入口 |
| submit_item | 物品 + 提交角色 + 数量 |
| reach_region | 删除 `dimension_note`；方块整数值；玩家投影新增可选 XYZ |
| Story 持久化 | 必需的启动边沿观察与单次终止路由记录，绑定玩家/Story/稳定条目/run |
| 作者帮助 | 仅 Studio 定义，Graph/DGRS 不增加一份帮助文本 |
| 媒体 | 读取源、索引、缓存属于实现变化；媒体引用及 DGRS 自包含不变 |

版本号、Schema 文件、C#/Java 读取写入、测试 fixture 统一更新；不要为了每个 UI 文案分别升级格式。对上述真实契约变化采用明确 canonical 版本，禁止静默把旧不完整配置当有效新配置。

## 12.2 一次性开发转换

不保留新旧 Runtime 并跑，但也不能用“无需兼容”作为丢弃作者数据的借口。

- 先备份测试项目；明确限定源格式、目标格式、处理文件和结果。
- 旧终止生成稳定出口元数据，尽可能保留作者已有名称；不能重开一次生成一次新 ID。
- 旧 Logic 边转换为统一连接的 Logic 类型，保持端点 ID，不重新按名字猜。
- `dimension_note` 删除；整数形式小数可无损转换，非整数中心/半径不得默默四舍五入，列为待作者修订的现有数据问题。
- 旧提交目标缺角色列为显式未选择；不自动绑定任何 NPC。
- 旧示例与真实用户文字无法可靠区分时，保留已有非空内容，只改新建行为；不批量清空作者台词/名称。
- 旧 `enter_story` 等影响本次连接/启动的废弃路径定点退出正常加载链；不在全仓开展与本版无关的遗迹大清扫。
- 旧存档无法无损适配的新字段给清楚诊断与测试存档处理说明，不新增永久 fallback，不破坏其他故事/身份存档。

## 12.3 Scope Guard

新增本版检查，但不要弱化/改写历史版本 Guard 来冒充过去通过。按类型/Scope/运行路径检查，不能全文搜一个词就判失败。

最低检查项：

```text
Task Flow 端口数 = 0
Story 投影固定默认 Flow 输入数 = 0
投影 Flow 输入数 = 对应 Story flow_driven 条目数
投影 Flow 输出数 = 对应 Story terminate 边界数
普通节点帮助缺失数 = 0
节点/Inspector 未说明的字段差异数 = 0
节点标题类型缺失数 = 0
玩家区域 UI 维度 ID / radius 输出 = 0
远程绕过角色的 submit 路径 = 0
服务器生产媒体整文件常驻 byte[] 路径 = 0
新增永久旧格式 fallback / 第二 Runtime = 0
```

这些是验收目标，不允许通过硬编码计数返回零。当前三种正式 Start 类型和 Task 的 `interact_actor` 必须保留，不被字符串检查误删。

---

# 13. 测试、视觉与性能验收

## 13.1 构建与运行命令

在用户现有 Windows 工具链中，先确认路径真实存在；不要因本机环境不同安装一套不必要的大型 SDK。以下沿用现有项目命令入口：

```powershell
# 在施工分支仓库根目录运行
$ErrorActionPreference = 'Stop'

# 路径若与现有机器不同，使用该机器已配置的同版本工具链。
$env:JAVA_HOME = 'E:\Java\jdk1.8.0_471'
$env:GRADLE_USER_HOME = 'E:\Java\gradle'
.\gradlew.bat build --offline --no-daemon --no-configuration-cache --max-workers=1
if ($LASTEXITCODE -ne 0) { throw 'Runtime build failed' }

$env:windir = $env:SystemRoot
$dotnet = 'E:\Java\dotnet-sdk-10\dotnet.exe'
& $dotnet test 'studio/src/DarkGreyRPG.Studio.Tests/DarkGreyRPG.Studio.Tests.csproj' --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Studio Core tests failed' }
& $dotnet test 'studio/src/DarkGreyRPG.Studio.Wpf.Tests/DarkGreyRPG.Studio.Wpf.Tests.csproj' --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Studio WPF tests failed' }
& '.\studio\package-studio.ps1'
if ($LASTEXITCODE -ne 0) { throw 'Studio packaging failed' }
```

显式运行 `build.gradle.kts` 中与本版相关的 JavaExec 探针，不能默认 `build` 已运行全部。保留仍有效的旧测试；仅因本版正式废止的契约调整测试，并在报告写明原因，不为凑绿删除失败测试。

新增一个本版聚合验证入口，实际依赖 Task/Story/Package/Network/Media 新测试与现有相应回归；入口名称可以统一为 `verify0331`，但必须在实现后才作为可执行命令交付。

## 13.2 最低闭环场景

创建独立 0331 验收项目，保留用户测试项目不被样例覆盖。

**场景一：任务到跨故事**

```text
A：与酒馆老板交互
 → 会话 [酒馆聊天]
 → 标题（阻塞）
 → 任务：收集物品
 → 任务/目标：向酒馆老板提交指定物品
 → 阶段奖励 / 结算
 → 终止 [进入第二章]

图谱：A.进入第二章 → B.来自第一章（流程驱动）
B：只走对应入口 → 区域到达 → 会话 → 终止
```

验证节点内完成配置、玩家交互提交、标题不被跳过、区域只显示坐标、图谱真路由。任务内部依赖用 Logic，不按示意图新增 Task Flow。

**场景二：逻辑重复启动**

A 的公开状态经 B 的逻辑输入连接 B 的 Start 条件。首次 False→True 启动，持续 True 不重复；A 新 run 重置再变 True 才启动可重复 B；B ACTIVE 和 once 终态均不重入。穿插退出/重登/服务器重启。

**场景三：媒体与恢复**

Actor 默认头像/差分、台词配音、Session 音乐与多层画面共同运行；结束/死亡/重连遵守原会话行为。服务器使用无常驻整媒体的传输链，客户端迟到资源不阻塞游戏。试听与游戏播放状态互不污染。

## 13.3 视觉质量门

必须在真实打包 Studio 和实际 Minecraft 中截图；用原审计截图作为对照，不能只测离屏控件或单独 Demo。

至少覆盖 Dark/Light、常用桌面尺寸和较小工作区、100%/150% DPI，图画布不同缩放与窄 Inspector。具体机器分辨率记录在证据中，不声称未测试尺寸通过。

必拍：

- 节点标题（会话/任务/执行/自定义终止），正常与长中文名。
- 所有本版新增/修改属性在节点内和 Inspector 的成对状态；普通/高级、原版BUFF/MOD扩展。
- 空自由文本与水印、中文输入中、验证错误、负数、下拉打开。
- 每一类节点的【介绍说明】折叠；简单和复杂说明展开，参数仍可达；切换节点正确。
- 提交物品+角色、奖励空/一项/多项、区域 XYZ 布局。
- 图谱两个以上 Story、零/多个 Flow 入口、多个命名出口、Logic/Flow 同时存在；直连操作录像。
- Actor 默认头像/多差分；配音与音乐试听/暂停/定位。
- 画面 1/10/20 图层、八方向手柄、拖序、参考框双开/单开/关闭。
- 撤销前/撤销后/重做后线条，不能靠截图前再次手动拖动“摆正”。
- 玩家任务坐标、提交交互、标题阻塞和跨 Story 后续内容。

检查标准：标签和控件对齐、间距一致、无中文裁切、无横向溢出、无重复保存状态大标题、说明小字可读、预览不被挤走、弹出层不抢错输入。出现问题必须修改后重新拍，禁止“有截图=已通过”。

## 13.4 报告用语

可以：`IMPLEMENTED`、`TESTED`、`NEEDS_USER_VERIFICATION`；必须列实际环境与证据。

不可以：未启动真实 Studio 却写“人工视觉验收通过”；未测试 Minecraft 却写“实机闭环通过”；用脚本代替用户写 `USER_ACCEPTED=YES`。

---

# 14. Definition of Done 与交付清单

## 14.1 工程完成必须满足

- [ ] A01—A24 全部实现并有证据；A25 明确拆为已修媒体问题与延期留声机需求。
- [ ] D01—D10 全部落地；没有重新引入旧 EnterStory、固定 Story Flow 输入或 Task Flow。
- [ ] 节点/Inspector 属性矩阵全覆盖，所有正式节点有可折叠且准确的介绍说明。
- [ ] Story 图谱直接连接、端口同步、导出、服务端路由、重复/拒绝/恢复语义完整。
- [ ] 提交角色不能被旧远程入口绕过；收集与区域规则正确。
- [ ] 下拉、数字拖动、节点拖动、撤销连线不存在已知阻断问题。
- [ ] 头像、音频、画面工具能在发布目录正常操作，不只在开发环境可用。
- [ ] DGRS 从打开到校验、保存索引、缓存、发送均不存在整包媒体常驻；有内存、并发、代际与清理证据。
- [ ] 构建、Core/WPF 测试、相关旧探针、新探针和 Scope Guard 实际通过。
- [ ] 两主题/尺寸/DPI 的截图与操作记录完成，问题经过修整重验。
- [ ] 不影响指名器身份、任务缓存、会话退出/恢复等冻结基础；残留风险如实列出。

达到以上可标记 `0.3.3.1_ENGINEERING_COMPLETE=YES`。用户亲自审计确认后才可标记 `0.3.3.1_USER_ACCEPTED=YES`。

## 14.2 最终交付

提交清楚的功能分组 commit，不将所有修复压成无法定位的大快照；每次提交保持边界可审查。给用户交付：

1. `0.3.3.1` 模组与可运行 Studio 打包产物，真实路径、版本与构建提交一致。
2. 施工总结：精确基线/最终 SHA、修改范围、数据转换说明、已执行命令与结果。
3. A01—A25 / D01—D10 逐项验收表与节点字段/介绍覆盖表。
4. 测试日志、截图/录像索引、媒体内存与缓存测量报告。
5. 未解决项和受环境限制未验证项；没有则说明检查范围，不笼统宣称“绝无问题”。

建议仓库落地文件（施工时创建，不代表当前已存在）：

```text
docs/0.3.3.1/BASELINE_AND_SCOPE.md
docs/0.3.3.1/AUDIT_TRACEABILITY.md
docs/0.3.3.1/NODE_AUTHORING_COVERAGE.md
docs/0.3.3.1/STORY_GRAPH_CONTRACT.md
docs/0.3.3.1/MEDIA_LIFECYCLE_AND_MEASUREMENTS.md
docs/0.3.3.1/ACCEPTANCE.md
```

文档可以合并以避免维护负担，但事实、测试与证据不能遗漏。

---

# 附录 A：节点介绍说明覆盖与写作要求

这是说明内容的验收规范，不是把下面示例不经核验全部粘进 UI。说明必须对应最终运行行为；不可借写说明偷偷改变既有语义。

## A.1 全部正式节点

| 图域 | 必须覆盖的节点 |
|---|---|
| 故事图谱 | 自动投影的 Story 节点及动态 Flow/Logic 公共端口 |
| Story | 开始、终止、会话、任务、执行、标题、与、或、非、条件判断、流程判断、逻辑输入、逻辑输出 |
| Session | 起始、台词、选择、音乐、画面、与、或、非、条件判断、流程判断、逻辑输入、逻辑输出、结束 |
| Task | 目标、奖励、与、或、非、逻辑输入、逻辑输出、结算 |

从注册表核查实际集合，正式新增节点不能漏掉。已废弃且不对正常作者开放的节点不为本版新增介绍入口。

## A.2 容易写错的说明

| 节点/类型 | 必须清楚的内容 |
|---|---|
| 开始 | 条目各有出口；匹配一条只走一条；ACTIVE 禁重入；可重复不等于持续自动循环 |
| 流程驱动条目 | 父图输入来自这条声明；不带固定总入口；外部 Flow 进入后从该条内部输出继续 |
| 逻辑条件条目 | 输入为 True 满足条件；首次/再次触发遵循上升沿，已运行时不再启动 |
| 终止 | 结束当前 Story 的本次运行，记录命名出口；父图已连线时尝试后继入口；不代表所有出口一起执行 |
| 会话聚合 | 进入该 Session；具体【结束】决定返回哪个 Flow 出口；公开 Logic 边界来自子图 |
| 任务聚合 | 任务开始/等待结算；结算选择结果映射 Flow 出口；Task 内部依然无 Flow |
| 与/或/非 | 说明布尔运算和动态输入；无连线输入的实际默认语义准确，不能写“自动忽略”而 Runtime 视为 False |
| 条件判断 | Flow 到达时检查 Logic，True 走“是”、False 走“否”；基线中所选出口无连线存在等待行为，应准确保留/说明，不能只写“没线自动结束” |
| 流程判断 | 流程经过则继续，同时本 run 执行状态变 True；不是有 Logic 输入的二分分支，也不是只闪一帧的事件脉冲 |
| 逻辑输入/输出 | 子图内部方向与父级公开方向的对应；传布尔值，不产生 Flow、不自动发奖励 |
| 收集 | 当前背包数量，不扣除；达标目标结束不回退 |
| 提交 | 指定物品、数量与角色；交互且足够才整笔扣除；不足不部分提交 |
| 区域 | 玩家所在方块、指定维度与立方体；玩家 UI 坐标与技术维度区分 |
| 奖励 | Task 内 Logic 条件下每 run 一次；多条物品/经验；负数扣除但不透支；不是严格物品提交替代品 |
| 结算 | 整个 Task 的最终结果；多个结果按现有确定规则选择，不能写所有 True 结果同时发出 |
| 执行 | Flow 驱动一次操作；正负增量、单位、范围按子类型；不是 Task 逻辑奖励节点 |
| BUFF给予 | 时长和等级均为增量；0 不变；结果任一≤0移除；等级对作者从1计；MOD扩展按当前身份规则 |
| 指令执行 | 单条 `/…`，受信服务端执行；不增加循环/宏；常驻提示仍只保留用户要求的短句 |
| 标题 | 全部动画结束后继续 Flow，明确阻塞 |
| 台词 | 角色/旁白、默认头像/差分、可选配音；配音与编辑器试听不同 |
| 音乐 | 游戏 Session 播放/停止/替换/循环；本地试听不会执行故事 |
| 画面 | 全量图层快照替换，不是对上一节点补丁；空画面清空；预览参考 UI 不是真实图层 |
| 结束 | 结束当前 Session，返回命名边界；不同于 Story【终止】和 Task【结算】 |

说明可按“用途 / 端口 / 必要补充”组织。简单节点数句话足够，复杂节点详细但默认折叠；禁止未经代码/已确认设计支持的泛泛保证。

---

# 附录 B：来源索引

## B.1 用户材料与决定

- **U01**：`0.3.3.0审计(1).docx`，25 页，A01—A25 按原编号引用；嵌入图用于布局和交互对照。
- **U02**：本对话后续用户确认：维度备注删除/玩家坐标、节点属性对齐、图谱直接接线、Start“流程驱动”而非固定入口、【终止】命名与保留类型。
- **U03**：本对话后续用户确认：所有节点默认折叠【介绍说明】替代节点保存状态；帮助准确清晰，不挤占 Inspector。
- **U04**：本对话七项确认：提交目标物品+角色、收集不回退、方块坐标、ACTIVE 禁重入、False→True 可重复启动、标题阻塞、DGRS 媒体读取修复。
- **U05**：此前《DarkGrey_RPG_0.3.3.0_Audit_25_Items_64dd990.md》作为源码定位辅助，其早期建议在与 U02—U04 冲突时已被本 PLAN 取代。

## B.2 固定提交源码

下列链接均锁定 `64dd990484a8cf2639013a41a7d4fe960e6b7409`。它们用于证明现状和提供施工落点，不表示其中旧行为高于最终产品决定。

- **S01**：[`studio/src/DarkGreyRPG.Studio/ViewModels/Graph/GraphEditorHostViewModel.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/ViewModels/Graph/GraphEditorHostViewModel.cs)
- **S02**：[`studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalGraphNodeControl.xaml`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalGraphNodeControl.xaml)
- **S03**：[`studio/src/DarkGreyRPG.Studio.Core/Graphs/Resources/CanonicalAggregateNodeFactory.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio.Core/Graphs/Resources/CanonicalAggregateNodeFactory.cs)
- **S04**：[`studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalStoryWorkspaceView.xaml`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalStoryWorkspaceView.xaml)
- **S05**：[`studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalInlineNodeEditorControl.xaml`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalInlineNodeEditorControl.xaml)
- **S06**：[`studio/src/DarkGreyRPG.Studio/ViewModels/Graph/CanonicalNodeInspectorViewModel.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/ViewModels/Graph/CanonicalNodeInspectorViewModel.cs)
- **S07**：[`studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/CanonicalTaskObjectiveSchema.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/CanonicalTaskObjectiveSchema.cs)
- **S08**：[`src/main/java/darkgrey/rpg/task/forge/CanonicalTaskForgeManager.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/task/forge/CanonicalTaskForgeManager.java)
- **S09**：[`src/main/java/darkgrey/rpg/creator/CanonicalTaskUiProjection.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/creator/CanonicalTaskUiProjection.java)
- **S10**：[`src/main/java/darkgrey/rpg/command/CommandDarkGreyRpg.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/command/CommandDarkGreyRpg.java)
- **S11**：[`studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/GraphNodeDefinitionRegistry.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/GraphNodeDefinitionRegistry.cs)
- **S12**：[`studio/src/DarkGreyRPG.Studio/ViewModels/Graph/CanonicalNodeInspectorViewModel.Rewards.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/ViewModels/Graph/CanonicalNodeInspectorViewModel.Rewards.cs)
- **S13**：[`studio/src/DarkGreyRPG.Studio/ViewModels/Graph/CanonicalNodeInspectorViewModel.Actions.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/ViewModels/Graph/CanonicalNodeInspectorViewModel.Actions.cs)
- **S14**：[`studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalGraphEditorView.xaml.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalGraphEditorView.xaml.cs)
- **S15**：[`studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/CanonicalStoryActionSchema.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/CanonicalStoryActionSchema.cs)
- **S16**：[`studio/src/DarkGreyRPG.Studio.Core/Graphs/Editing/GraphEditSession.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio.Core/Graphs/Editing/GraphEditSession.cs)
- **S17**：[`studio/src/DarkGreyRPG.Studio/Views/Graph/SessionScreenEditor.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/Views/Graph/SessionScreenEditor.cs)
- **S18**：[`src/main/java/darkgrey/rpg/story/canonical/runtime/CanonicalStoryActionConfiguration.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/story/canonical/runtime/CanonicalStoryActionConfiguration.java)
- **S19**：[`src/main/java/darkgrey/rpg/story/canonical/forge/CanonicalStoryInstantActions.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/story/canonical/forge/CanonicalStoryInstantActions.java)
- **S20**：[`studio/src/DarkGreyRPG.Studio.Core/Graphs/Resources/CanonicalProjectStoryGraphService.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio.Core/Graphs/Resources/CanonicalProjectStoryGraphService.cs)
- **S21**：[`studio/src/DarkGreyRPG.Studio.Core/Graphs/Resources/CanonicalStoryLogicGraphRepository.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio.Core/Graphs/Resources/CanonicalStoryLogicGraphRepository.cs)
- **S22**：[`studio/src/DarkGreyRPG.Studio/ViewModels/ProjectHomeViewModel.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/ViewModels/ProjectHomeViewModel.cs)
- **S23**：[`studio/src/DarkGreyRPG.Studio/Views/ProjectGraphView.xaml`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/Views/ProjectGraphView.xaml)
- **S24**：[`studio/src/DarkGreyRPG.Studio.Core/Packaging/StoryPackageExporter.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio.Core/Packaging/StoryPackageExporter.cs)
- **S25**：[`src/main/java/darkgrey/rpg/story/canonical/forge/CanonicalStoryForgeManager.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/story/canonical/forge/CanonicalStoryForgeManager.java)
- **S26**：[`studio/src/DarkGreyRPG.Studio/Views/ProjectGraphView.xaml.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/Views/ProjectGraphView.xaml.cs)
- **S27**：[`studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalGraphNodeControl.xaml.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalGraphNodeControl.xaml.cs)
- **S28**：[`studio/src/DarkGreyRPG.Studio/Views/FlowPortControl.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/Views/FlowPortControl.cs)
- **S29**：[`studio/src/DarkGreyRPG.Studio/Views/ActorPortraitDialog.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/Views/ActorPortraitDialog.cs)
- **S30**：[`studio/src/DarkGreyRPG.Studio/ViewModels/ActorEditorViewModel.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/ViewModels/ActorEditorViewModel.cs)
- **S31**：[`studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalStoryWorkspaceView.xaml.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalStoryWorkspaceView.xaml.cs)
- **S32**：[`src/main/java/darkgrey/rpg/client/session/CanonicalDialogueLayout.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/client/session/CanonicalDialogueLayout.java)
- **S33**：[`src/main/java/darkgrey/rpg/client/gui/GuiCanonicalSessionScreen.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/client/gui/GuiCanonicalSessionScreen.java)
- **S34**：[`src/main/java/darkgrey/rpg/project/packages/LoadedStoryPackage.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/project/packages/LoadedStoryPackage.java)
- **S35**：[`src/main/java/darkgrey/rpg/media/CanonicalMediaServer.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/media/CanonicalMediaServer.java)
- **S36**：[`src/main/java/darkgrey/rpg/media/CanonicalMediaClient.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/media/CanonicalMediaClient.java)
- **S37**：[`studio/src/DarkGreyRPG.Studio.Core/Media/ProjectMediaGarbageCollector.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio.Core/Media/ProjectMediaGarbageCollector.cs)
- **S38**：[`studio/src/DarkGreyRPG.Studio.Core/Packaging/StoryPackageMedia.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio.Core/Packaging/StoryPackageMedia.cs)
- **S39**：[`AGENTS.md`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/AGENTS.md)
- **S40**：[`build.gradle.kts`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/build.gradle.kts)
- **S41**：[`studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/GraphNodeDefinition.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/GraphNodeDefinition.cs)
- **S42**：[`studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/StoryStartSchema.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/StoryStartSchema.cs)
- **S43**：[`studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/AggregatePortProjection.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/AggregatePortProjection.cs)
- **S44**：[`studio/src/DarkGreyRPG.Studio.Core/Media/ProjectMediaStore.cs`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/studio/src/DarkGreyRPG.Studio.Core/Media/ProjectMediaStore.cs)
- **S45**：[`src/main/java/darkgrey/rpg/client/session/CanonicalChoiceLayout.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/client/session/CanonicalChoiceLayout.java)
- **S46**：[`src/main/java/darkgrey/rpg/client/gui/GuiCanonicalTaskScreen.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/client/gui/GuiCanonicalTaskScreen.java)
- **S47**：[`src/main/java/darkgrey/rpg/task/runtime/CanonicalTaskRuntime.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/task/runtime/CanonicalTaskRuntime.java)
- **S48**：[`src/main/java/darkgrey/rpg/task/forge/CanonicalTaskPlayerTransactions.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/task/forge/CanonicalTaskPlayerTransactions.java)
- **S49**：[`src/main/java/darkgrey/rpg/story/canonical/runtime/CanonicalStoryRuntime.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/story/canonical/runtime/CanonicalStoryRuntime.java)
- **S50**：[`src/main/java/darkgrey/rpg/story/canonical/server/CanonicalStoryServerService.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/story/canonical/server/CanonicalStoryServerService.java)
- **S51**：[`src/main/java/darkgrey/rpg/session/persistence/CanonicalSessionSavedData.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/session/persistence/CanonicalSessionSavedData.java)
- **S52**：[`src/main/java/darkgrey/rpg/project/packages/DgrsArchiveReader.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/project/packages/DgrsArchiveReader.java)
- **S53**：[`src/main/java/darkgrey/rpg/project/packages/StoryPackageSnapshotReader.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/project/packages/StoryPackageSnapshotReader.java)
- **S54**：[`src/main/java/darkgrey/rpg/project/packages/StoryPackageLoader.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/project/packages/StoryPackageLoader.java)
- **S55**：[`src/main/java/darkgrey/rpg/project/packages/StoryPackageMediaValidation.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/project/packages/StoryPackageMediaValidation.java)
- **S56**：[`src/main/java/darkgrey/rpg/project/packages/StoryPackageContentFingerprint.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/project/packages/StoryPackageContentFingerprint.java)
- **S57**：[`src/main/java/darkgrey/rpg/media/MediaPayloadValidation.java`](https://github.com/GreyHat633/DarkGrey_RPG/blob/64dd990484a8cf2639013a41a7d4fe960e6b7409/src/main/java/darkgrey/rpg/media/MediaPayloadValidation.java)

## B.3 外部技术参考（只用于实现，不替代用户语义）

- **T01 — Java 8 ZipFile**：条目 InputStream 与句柄关闭；不要求全条目缓存。[Oracle 官方文档](https://docs.oracle.com/javase/8/docs/api/java/util/zip/ZipFile.html)
- **T02 — Java 8 DigestInputStream / MessageDigest**：随读取增量计算摘要，不必先构造完整文件数组。[DigestInputStream](https://docs.oracle.com/javase/8/docs/api/java/security/DigestInputStream.html)；[MessageDigest](https://docs.oracle.com/javase/8/docs/api/java/security/MessageDigest.html)
- **T03 — Java 8 FileChannel**：按位置读取普通缓存文件。[Oracle 官方文档](https://docs.oracle.com/javase/8/docs/api/java/nio/channels/FileChannel.html)
- **T04 — WPF UpdateLayout**：布局会延迟，避免每个细小更改都重排；读取最终位置应在相关属性变更完成后。[Microsoft 文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.uielement.updatelayout?view=windowsdesktop-10.0)
- **T05 — WPF Expander**：可折叠内容，折叠时保留标题；展开内容的尺寸与滚动需正确约束。[Microsoft 文档](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/expander)
- **T06 — Unity Numeric Field**：参考成熟数值编辑/拖动反馈，不机械照搬其标签手势来遗漏用户要求的数值框操作。[Unity 官方设计文档](https://unityeditordesignsystem.unity.com/components/numeric-field)

---

## 最终施工指令

**从精确 0.3.3.0 基线增量修复。先保护并统一现有语义，再把属性、端口和媒体操作真正接到作者界面与运行链。所有用户已经确认的规则按本文执行，不再重复提交设计选择题。调查未确定的技术根因并给证据，不靠假默认值、延迟、兼容分支或空测试把问题隐藏。**
