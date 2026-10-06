# 0.3.3.6 技术合同与盘点

日期：2026-10-04。施工输入为 `../DarkGreyRPG_0.3.3.6_Construction_PLAN_FINAL.md`。
状态：P0 盘点、P1 正式身份切换及 P2 追加复制均在实施中。下文区分已验证合同和待实现设计，不能用本文替代运行验收。

## 基线与工作区

- 开工 HEAD：`99a3253d7186127bdbe37898824ecbd32219f136`，原分支 `codex/0.3.3.5`。
- 本地和远端查询未发现 `codex/0.3.3.6`，在原工作区创建该分支，保留全部已有修改。
- 原有生产改动：`README.md`、`build.gradle.kts`、`CommandDarkGreyRpg.java`、`StoryPackageSnapshotMerger.java`；原有未跟踪诊断类 `StoryPackageConflictDiagnostics.java`、`StoryPackageErrorPage.java` 和 `StoryPackageDiagnosticsProbe.java` 继续保留。
- 原有 AGENTS、配置及大量旧 PLAN 删除/未跟踪文件不属于本轮变更。不执行 broad staging、push 或成品发布。
- 本轮使用当前进程直接施工，无 Worker 结果；原生 Studio 实机证据见 EvidenceIndex，Minecraft 实机仍未执行。

## 已实现的身份值合同

1. `StoryUid` 是不可变值对象，严格接受 `ST-XXXX-XXXX-XXXX-XXXX`。字符表 `23456789ABCDEFGHJKLMNPQRSTUVWXYZ` 共 32 字符；16 个数据字符对应 80 bit。两端均用加密随机源均匀选取字符，创建时检查调用方提供的可见 UID 集合；最多 128 次碰撞重抽后明确失败。只有新对象分配 API，无修改已有对象 UID 的 API。
2. `ResourceAddress` 是 `(StoryUid, ResourceKind, LocalId)`。当前实现的 canonical 类型为 actor、item、item_group、session、task；Actor 个体/群体共用 actor。Dialogue/Quest 在旧代码仍有残留，正式入口审计尚未结束，不能声称已移除或已覆盖。
3. Local ID 合同为 `[a-z][a-z0-9_]{0,62}`，新建为 `r` 加 16 个随机字节的小写十六进制，隐藏于普通作者界面。限制为小写 ASCII，避免 Windows 路径的大小写别名。UID 和 Local ID 均不 trim、不做大小写修复、不接收旧 Namespace。
4. 唯一 JSON 地址合同为 `{"story_uid":"ST-2345-6789-ABCD-EFGH","kind":"actor","local_id":"r17"}`。字段顺序读取无关，输出按此顺序；缺字段、重复字段、未知字段、null、数字、旧字符串 ID、未知 kind、尾部内容拒绝。C# 使用 JsonConverter，Java 使用 Gson streaming reader/writer，不用会吞重复字段的树解析地址。
5. 自有资源路径 `stories/<StoryUID>/resources/<kind>/r-<local_id>.json`；引用定义 `referenced_resources/<StoryUID>/<kind>/r-<local_id>.json`。固定 `r-` 文件前缀防止 Windows CON/PRN 等设备名。路径是地址的投影，不能由路径反推/更改 UID。
6. 共用验证数据 `schema/identity-0336-vectors.jsonl`；C# 与 Java 8/Gson 2.2.4 对同一数据验证接受/拒绝、输出 JSON 和路径。
7. 这些类型已接管当前创建、仓库和图引用，并接入部分 SavedData/单 Story 容器；剩余 Namespace、网络、历史与容器调用点尚未清理完。

## 文件事务与追加复制基础

内部字符串索引使用地址的无损投影 `<StoryUID>~<kind>~<local_id>`，通过 ResourceAddress.ToKey/FromKey 验证。它不接受 Namespace 或裸 ID，不是第二种身份；外部 JSON 地址仍为三字段对象。

## 成员指纹字节规范（已实现算法，尚未接入投影）

头为 ASCII `DGR-STORY-CONTENT-V1` + NUL，随后写 UID 文本、32 位记录数。文本为大端 int32 UTF-8 字节数 + 严格 UTF-8 字节。记录按 role/identity 的 UTF-16 ordinal 排序，每个记录写 role、identity 与运行 JSON；同 role/identity 重复拒绝。JSON 的 null/false/true 分别为单字节 n/f/t，字符串为 s+文本，数字为 d+大端 IEEE754 double 位模式（-0 归零，只允许有限值且绝对值不超过 2^53-1），数组为 a+int32 个数+按原顺序子值，对象为 o+int32 个数+按 ordinal 排序的键文本/子值。重复 JSON 字段拒绝。最多 4096 记录、64 层、64 MiB 编码。最终 SHA-256 小写十六进制。

`schema/story-fingerprint-0336-vectors.jsonl` 的 32 个期望向量由独立 Python hashlib/struct 参考实现产生，C# 与 Java 8/Gson 2.2.4 读取同一文件。算法调用者必须只投影运行内容；容器名、路径和编辑器布局排除。该投影工厂与 Runtime generation 接线尚未完成。

`NamespaceFileTransaction` 的内容是通用项目内文件事务，已迁入 `Core/IO/ProjectFileTransaction.cs`，更名为 `ProjectFileTransaction` / `ProjectFileChange` / `ProjectFileTransactionException`。所有原调用点和测试已切换，不保留旧类型别名。原有边界保持：预期字节检查、项目内路径和 reparse point 检查、验证后复查、普通异常回滚、回滚错误报告；这是进程内事务，不承诺进程崩溃时多文件原子性。

`StoryResourceCopyMap` 先接受完整自有资源集和目标已有资源集，按 Owner + Kind + Local 建立不可变映射。可保留的目标 Local ID 先全部预留，只有碰撞的新副本重新分配。外部引用原样保留，重复迁移用目标已存在的新副本集合再次避让。此类只分配地址，不写文件、不改图，不构成迁移功能已交付。

草稿边界已实测：`GraphResourceRepository` 可以保存并重开两个不同 node ID 的 Start；`GraphScopePolicy` 仍报 `graph.scope.required_node.duplicate`。`GraphEditSession.AddNode` 拒绝第二个 Start 且不产生历史。因此不能循环调用普通 AddNode 来实现迁移；后续需要一个保留草稿的批量追加事务，并接入编辑历史。

现有历史入口：`GraphEditSession` 私有 Commit 管理图快照；`ShellViewModel.ReferenceHistory.cs` 配合 `EditHistoryClock` 管理跨文件操作；`CanonicalGraphResourceSaveCoordinator` 与 Story clipboard 已使用通用项目文件事务。批量复制需组合这些现有边界，尚未新增完整提交入口。

## 后续持久化与容器合同（待 P0 收口）

- project、resource、package、network、SavedData 需统一当前身份协议标记，旧输入明确拒绝且不得回存空对象。具体 root schema 版本与所有入口尚未切换。
- 当前 C# 容器契约：单成员 format=`dgrs`；整组 format=`dgrs.g`；两者 format_version=2、identity_format=`story-uid-v1`。组 manifest 仅有 format、format_version、identity_format、display_name、connections、members；members 为完整当前单成员 manifest 数组且不含成员级 story_logic_graph，外层唯一 `resources/group-connections.json` 保存完整组内 Flow/Logic。没有 Group UID。最长后缀优先识别。Java 组读取仍待接入。
- 扁平载荷使用 `manifest.json`、`story_graph.json`、`stories/<uid>/story.json`、`stories/<uid>/membership.json`、上述 resources/reference 路径和共享 media；不嵌套 ZIP，不给 Group 永久 UID。
- 关系继续复用 `CanonicalStoryLogicConnection.InterfaceKind`，Flow/Logic 都参加无向连通分量；源/目标方向用于执行和媒体遍历。资源引用不参加连通算法。`CanonicalStoryLogicGraphRepository` 已经读取 OfflineProviderCatalog 验证只读来源边界。
- 名称快照、完整 Reference Group 闭包和版本一致的导出快照待实现，不能复用资源 membership 的 AddReference 来判断 Story 图端点能力。
- 当前 Java `DgrsArchiveReader` 上限为 4096 entries、每 entry 64 MiB、总展开 256 MiB。Group 不解除这些上限。两端 reader/网络请求预算的完整审计尚未完成。
- 当前 fingerprint v1 包含 project 与文件路径字节，不能直接当作成员 fingerprint。需实现跨语言语义规范化与共同测试向量，剔除布局/名称/容器属性且保留剧情顺序；此项仍未完成，禁止先切换 Generation key。

## 管理与运行边界

- 当前 `StoryPackageLoader` 以 package ID map 和 source-name map 持有已加载定义，支持保留旧快照；它不满足全文件 Inventory 和对称冲突要求。必须先形成完整来源清单，再按真实成员 UID 反向索引判重，包含 Disabled 和可知 Manifest 的 Error 来源。
- 主状态优先 Error > Conflict > Disabled > Enabled，保留所有诊断和用户启用意图；不自动选 winner，不允许 Conflict fallback 回旧获准快照。
- 普通 Disabled 只挡 NEW/RESTART，已有获准实例继续；Conflict/Error/显式删除撤销相关 Story 资格并停止活动执行，保留完成历史和防重收据。恢复 Enabled 只恢复资格。
- `CanonicalStoryForgeManager` 有 Session/Task start、cancelByStory 和 rising Logic restart 路由；所有启动/恢复/retire 调用点完整盘点仍待完成。不能在单个 UI/命令入口上放开关后声称统一 admission 已实现。
- O 键复用 `ClientQuestKeyHandler` 与现有网络基础，只在正常游戏无文本输入上下文打开。每种管理读写请求必须有服务端 OP 检查、session/revision、有限分页和准确来源 handle；具体 packet discriminator、字节预算、权限撤销清理尚待实现。
- 媒体继续以 Story 为单位维持 3 并发/10 槽位，Group 详情和管理扫描不预热；文件租约、成员 generation、请求版本和阻断状态共同防止异步复活。

## P0 未关闭项

正式 Dialogue/Quest 入口取舍、全部旧身份读写点的逐项处置、图节点/动态端口/page/option/layer ID 的复制映射、统一当前 schema 标记、成员 fingerprint 字节规范及向量、全部运行 admission/恢复入口、网络/管理预算与实际双端验收。P0 不能标记完成，P1 不能标记贯通。

## 当前已落地版本（替代上文早期盘点的未切换状态）

| 边界 | 当前版本 |
|---|---|
| Project | schema 3 + identity_format story-uid-v1 |
| Story/Session/Task graph | schema 2 + identity_format；Story UID 字符串，其余 structured address |
| Membership | schema 4 + identity_format；owned/referenced structured addresses |
| Actor / Item | schema 5 / 2 + identity_format |
| DGRS 单 Story | schema 2、format_version 2、identity_format；恰好一 Story/Membership，完整依赖闭包 |
| Dynamic content | DGR2 typed structured address；DGR1 拒绝 |
| NPC / Item / Nominator SavedData | schema 2 / 2 / 3 + identity_format；结构化 NBT address |

单 Story 当前载荷沿用 canonical 仓库角色目录，目录内资源文件使用 ResourceAddress.RelativeDefinitionPath；不从文件名派生身份。C# 单包/Java 扫描互通已验证。Group 需复用这些载荷，单独添加扁平外层成员描述与完整边；不得声称已完成 Group 格式。

追加复制使用 StoryContentCopyService + WPF StoryContentCopyHistory；底层完整图快照允许多 Start 草稿，普通 AddNode 与可运行导出校验仍严格。图、资源、Membership、布局、分组框和屏幕编辑 metadata 合入同一文件事务。当前仅同项目可编辑源/目标；外部 Import 新 UID 另行实现。

### 追加迁移草稿与容器增量

- Story 多 Start 草稿允许继续保存；单删和批量删保留最后一个 Start。单 Story/整组导出在触碰目标前拒绝多 Start。
- 复制历史识别编辑器保存基线，允许后续编辑保存再连续撤销；布局事务仅合并此次复制涉及的 graph/frame keys，保留其他资源保存。
- 组 ZIP 扁平复用角色目录，引用共享定义必须字节相同；完整路径表、成员 UID 唯一性、闭包、组图端点和单连通分量是读取门槛。导出源快照变化会取消提交。
- 整组 Reference 使用一个文件，目录对成员提供只读视图；解除引用按物理容器整体处理。整组 Import 新 UID、Java 组加载、运行时管理器和实机验证尚未完成。
