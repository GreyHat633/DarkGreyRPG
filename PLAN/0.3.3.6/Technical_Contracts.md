# 0.3.3.6 当前技术合同

2026-10-05 收口。实现与验收见 Implementation.md、Acceptance.md、Delivery.json；开工时的未实现盘点保存在 evidence/technical-contracts-opening-history.md，不代表当前状态。

## 基线与范围

HEAD 基线 99a3253d7186127bdbe37898824ecbd32219f136；施工分支 codex/0.3.3.6。保留原脏工作区，本轮未提交、推送或生成成品包。原生 UI 使用 UI Automation/Win32，Minecraft 使用实际客户端与专用/集成服务器。

## 身份合同


1. `StoryUid` 是不可变值对象，严格接受 `ST-XXXX-XXXX-XXXX-XXXX`。字符表 `23456789ABCDEFGHJKLMNPQRSTUVWXYZ` 共 32 字符；16 个数据字符对应 80 bit。两端均用加密随机源均匀选取字符，创建时检查调用方提供的可见 UID 集合；最多 128 次碰撞重抽后明确失败。只有新对象分配 API，无修改已有对象 UID 的 API。
2. `ResourceAddress` 是 `(StoryUid, ResourceKind, LocalId)`。当前实现的 canonical 类型为 actor、item、item_group、session、task；Actor 个体/群体共用 actor。旧 Dialogue/Quest 载荷不参与当前正式创作/执行入口；Session/Task 为当前资源。
3. Local ID 合同为 `[a-z][a-z0-9_]{0,62}`，新建为 `r` 加 16 个随机字节的小写十六进制，隐藏于普通作者界面。限制为小写 ASCII，避免 Windows 路径的大小写别名。UID 和 Local ID 均不 trim、不做大小写修复、不接收旧 Namespace。
4. 唯一 JSON 地址合同为 `{"story_uid":"ST-2345-6789-ABCD-EFGH","kind":"actor","local_id":"r17"}`。字段顺序读取无关，输出按此顺序；缺字段、重复字段、未知字段、null、数字、旧字符串 ID、未知 kind、尾部内容拒绝。C# 使用 JsonConverter，Java 使用 Gson streaming reader/writer，不用会吞重复字段的树解析地址。
5. 自有资源路径 `stories/<StoryUID>/resources/<kind>/r-<local_id>.json`；引用定义 `referenced_resources/<StoryUID>/<kind>/r-<local_id>.json`。固定 `r-` 文件前缀防止 Windows CON/PRN 等设备名。路径是地址的投影，不能由路径反推/更改 UID。
6. 共用验证数据 `schema/identity-0336-vectors.jsonl`；C# 与 Java 8/Gson 2.2.4 对同一数据验证接受/拒绝、输出 JSON 和路径。
7. 这些类型已接管当前创建、仓库、图引用、容器、网络及 SavedData；旧格式输入明确拒绝。


## 当前持久化边界

| 载荷 | 当前合同 |
|---|---|
| Project | schema 3 + identity_format story-uid-v1 |
| Story/Session/Task | schema 2 + identity_format；Story UID，其余 structured address |
| Membership | schema 4；owned/referenced structured addresses |
| Actor / Item | schema 5 / 2 + identity_format |
| DGRS | format_version 2；dgrs 单成员，dgrs.g 完整组 |
| Dynamic content | DGR2 typed address；旧 DGR1 拒绝 |
| NPC/Item/Nominator SavedData | schema 2/2/3；结构化 NBT address |

内部索引 `<StoryUID>~<kind>~<local_id>` 是 ResourceAddress 的无损投影，不接受旧 Namespace ID。生产搜索审计见 current-namespace-audit.txt：剩余 Namespace 是拒绝说明；DGR1 为拒绝检测。两个 fullId 参数名指当前 typed key，未保留旧 FullId 属性/旧身份兼容入口。CNPC stored data 的 legacy actor id 不再读取；Nominator/外部身份注册表统一提供绑定、解绑、检查，解绑不能复活旧数据。

## 追加迁移和文件事务

StoryContentCopyService 分配完整资源/节点/页面等复制映射，保留外部资源引用，不复制源外部 Story 线。目标非空、资源碰撞、未使用资源、媒体及动态文字均受测。ProjectFileTransaction 进行预期字节、项目内路径/reparse point 检查、写前复查和异常回滚；不承诺断电时多文件原子性。

StoryContentCopyHistory 合并图、Membership、资源、布局及屏幕 metadata；允许多 Start 草稿保存与普通编辑修复。可运行导出严格拒绝结构错误。Undo/Redo 不覆盖其他资源独立保存。单删/批量删保留最后 Start；Story 删除事务移除指向自身的项目线并保留其他线。

## Group、Reference 和容器

Flow/Logic 共同派生无向连通分量；方向用于执行和媒体可达遍历。没有 Group UID、手工 membership 或 Group Runtime。单文件完整 Group 引用生成成员只读视图；单资源引用不扩组；跨 Project 公共边界连接触发完整来源闭包。整组 Import 分配新 UID 并重映射内部关系。

ZIP 平铺复用成员载荷、共享 media，外层完整 group-connections；不嵌套 ZIP。校验完整路径表、成员唯一、资源闭包、组图端点、连通分量与共享资源字节一致性；源快照变化取消提交。Java reader 保持 entry/展开/网络预算，先验证后整体发布。

成员语义指纹使用 DGR-STORY-CONTENT-V1、严格规范化运行 JSON 与 SHA-256；跨语言32个共享向量。容器文件名、编辑布局不改变成员代次；实际三成员包仅更新 B 时 A/C 会话和媒体保留。

## 管理与执行资格

Inventory 覆盖全部已安装物理容器，包括 Disabled 和可确定声明的 Error。UID 反向索引对称阻断全部涉事容器，不自动选 winner，不回退旧获准快照。状态优先 Error > Conflict > Disabled > Enabled，保留用户启用意图与诊断。

Disabled 仅挡 NEW/RESTART，已有实例可恢复/完成；跨 Story 新启动也检查资格。Conflict/Error/删除退役相应执行状态，保留完成历史和奖励防重。恢复 Enabled 不自动重开 ERROR。O 管理器请求由服务端校验 OP、revision、来源 handle 和分页预算；撤权关闭，过期操作拒绝并刷新。

## 媒体生命周期

Story 级 generation/租约，不以 Group 作为缓存单位；3 并发、10 Story 槽，下载占槽，运行项保护，超额排队，共享内容由最终租约释放后回收。管理器/只读图浏览不预热。文件 hash 校验、请求代次与资格失效阻止断线/更新/冲突后的异步复活。

仅在已有 MediaLatencyTrace.ENABLED 开发开关下记录变化的 package_budget 计数；不增加普通玩家配置。实际12 Story/共享大 PNG 覆盖上限、全保护排队、单项失败重试、部分就绪、断线和冲突；LRU/末租约/传输重排由政策探针补充。

## 交付与限制

权威自包含 dist 已更新，最新部署前后 Data 2647 文件一致；EXE/DLL/JAR 最终读回见 evidence/TaskUIFix/DeliveryFiles.json。Core 496、WPF 664 通过；1 个显式启用的性能基准跳过。最新代理原生 UI、Forge 双端和交付记录见 TaskUIFixAcceptance.md；USER_ACCEPTED=false，无成品 Release。
# 当前 Task 与公开输出补充合同

2026-10-05 修正：Task 的目标、结算均无最低数量。新建仅一个默认目标，无结算和连线。结算非唯一，可在“流程 → 结算”自由添加；零结算运行保持 ACTIVE。公开输出按稳定 port ID 绑定，Flow 与 Logic 各自保存显式顺序；Task Flow 顺序决定唯一结算优先级，Logic 只排序展示。节点参数与 Inspector 共用拖动编辑器，卡片外标题为“公开输出”。详见 [TaskUIFixAcceptance.md](TaskUIFixAcceptance.md)。
