# 当前收口摘要（2026-10-05）

施工完成，具体门槛和产物见 Acceptance.md / Delivery.json。下文保留施工历史，早期失败与待办由本节及最终证据取代。

- CNPC 旧 stored-data 身份读取/写入已移除；绑定、解绑、检查和 Live Pick 统一走当前外部身份注册表，旧值不能复活。
- 修复任务显式 reset 与完成历史的关系，保留防重计数；修复管理器过期 revision 拒绝后的刷新与持续提示。
- 当前 Shell、资源编辑器、删除/迁移/引用和旧项目拒绝合同已完成回归：Core 491/0/0；WPF 652/0/1，唯一跳过为既有性能工作负载。
- 媒体开发 trace 补充变化时的3/10槽位/队列/保护/Ready计数；正式 Java 构建与格式/检查通过。
- 原生富内容 Copy→修复→导出→实玩、跨 Project Reference NPC-only/公开边界闭包、双活跃禁用重连与 Flow 出口拒绝、三成员独立更新、双 OP 过期操作和真实媒体故障/断线/冲突均有分层证据。
- 自包含权威 Studio 已更新且 Data 保留；最终仅清除三个源码文件的多余 EOF 空行，不改变构建语义。

## 历史施工记录

# 0.3.3.6 施工记录

日期：2026-10-04；分支 `codex/0.3.3.6`；基线 `99a3253d7186127bdbe37898824ecbd32219f136`。

## 已写入源码

- C#/Java 不可变 StoryUid、包含 Owner/Kind/Local ID 的 ResourceAddress、严格 JSON 地址 codec、Windows 安全路径投影。
- 同一份 56 条跨语言身份向量，以及创建 2048 个 UID/地址、跨 Story/Kind 隔离测试；Gradle `storyIdentity0336Probe` 接入现有构建。
- 通用项目文件事务从 Namespace 中剥离；所有现有保存/引用历史/剪贴板/导入调用点已切换。
- StoryResourceCopyMap 实现完整资源集预分配、仅新副本碰撞重映射、外部引用保留、自复制/多所有者/重复地址拒绝；尚未连接文件和图批量复制。
- 增加多 Start 草稿持久化探针，确认普通 AddNode 和底层存储边界。
- C#/Java 成员内容指纹二进制规范及 32 条独立参考向量已通过；尚未接入 Runtime 代次计算。
- 自动 Story Group 已接入项目画布的连线保存、编辑器名称快照、撤销/重做以及左侧故事导航；组成员仍仅由正式 Flow/Logic 连线推导。组图框、容器、整体 Reference/Import 尚未贯通。
- 组名和项目连线使用同一项目文件事务保存；并发修改拒绝测试验证原图与另一处修改不被覆盖。
- 本机最小 WPF 程序证实默认渲染白屏、软件渲染正常。新增进程级 `DGR_STUDIO_SOFTWARE_RENDERING=1` 诊断/恢复开关，不更改系统或用户注册表设置。

## 阶段状态

| 阶段 | 状态 | 实际边界 |
|---|---|---|
| P0 | IN_PROGRESS | 见 Technical_Contracts；尚有协议和入口审计未关闭。 |
| P1 | IN_PROGRESS | 正式创建、仓库、图引用、部分世界绑定、单 Story ZIP 已切换；其余网络/持久化和 Namespace 清理未关闭。 |
| P2 | PARTIAL | 已接入追加事务、目标选择、布局/资源映射与单次编辑历史；富图/媒体及实机矩阵未关闭。 |
| P3 | PARTIAL | 自动 Group 导航/命名/历史已接入；双容器和整体引用导入未实现。 |
| P4—P5 | NOT_IMPLEMENTED | Inventory/启停、O 管理器未实现。 |
| P6 | NOT_RUN | 无当前格式 Studio→Minecraft 端到端验收，无 0336 dist 交付。 |

下一步先关闭 P0 的当前 schema、成员 fingerprint 和调用点清单，再做 P1 全链切换。不能把新增基础类型视为 Namespace 删除完成。保留 0335 数据与权威客户端，未向 dist 推送半成品，未创建 artifacts/DGR0.3.3.6。

## 正式身份接入增量（仍在施工）

- Project schema 3、图资源 schema 2、成员清单 schema 4、Actor schema 5、Item schema 2 采用 identity_format=story-uid-v1，严格拒绝旧根与缺失标记。旧项目打开自动转换入口已断开。
- Story 创建分配不可变 UID；Actor/Item/ItemGroup/Session/Task 创建入口分配隐藏地址。实际资源仓库保存/重开、owner/kind 拒绝、删除补偿与跨 Story 引用测试通过。
- 图中声明的资源字段采用结构化地址；任务原生注册目标独立使用 registry_name 对象。动态内容 DGR2 使用结构化引用，DGR1 不再兼容。
- Studio 序列化器生成的两 Story 项目已被 Java 正式加载器完整读取。实机候选已创建“实机新身份会话”，落盘地址、schema 与 marker 已读回验证。
- NPC、Item、Nominator SavedData 已接入新版本和结构化地址；Nominator 失败读取改为全部验证后发布，避免部分替换。
- 以上不代表 P1 全部完成：Namespace 业务类、容器、部分绑定/网络/选择器调用与历史测试仍需清理。此前 Core 491 / WPF 61 属于身份切换前证据，不是当前全量通过证明。

## 当前单 Story 容器接入

- 当前 DGRS manifest schema/format_version 均为 2，显式 identity_format=story-uid-v1；单包严格一个 canonical Story 和 Membership。C#/Java 同步拒绝旧标记。
- 导出移除 legacy Story 入口，地址路径按 owner/kind/local 投影。CurrentProjectInventory/Validator 从 Namespace 服务分离，导入不再写 NamespacePolicy；整体 Import 新 UID 尚未实现。
- 实际 Studio ZIP 经 Java 安装扫描暴露合并器旧 ID 读取，已定点修复，并保留原有冲突诊断改动。同一 consumer.dgrs 重跑通过，见 current-container-java.log。
- CurrentGraphIdentity0336Tests 当前 9 PASS；尚不是全量测试或 .dgrs.g / Minecraft 实机验收。

## Story 内容追加与实机增量

- StoryContentCopyService：复制全部 Owned，预分配地址，内部 typed references 重写，外部引用保持，节点/动态边界/page ID 重新分配；不写项目级边。节点布局、分组框、屏幕编辑元数据纳入同一文件事务。
- 验证媒体存在/内容有效；同工程 content-addressed 媒体共用项目内持久字节，删除源 Story 不删除媒体。更多实际媒体/聚合端口测试仍需补齐。
- Studio 项目菜单“迁移故事内容”选择已有本地目标；StoryContentCopyHistory 将文件事务与图/资源树放入一个历史项。Core 3 PASS，WPF 历史 1 PASS。
- 原生窗口已完成选源、选目标、复制与菜单 Undo/Redo，落盘 Start 数为 2/1/2，源 8 文件 hash 不变。观察到默认目标布局未落盘时重叠，已增加目标实际布局输入并通过自动测试，等待更新候选再验。
- 写测试时一次 ENOSPC 导致零字节文件，已完整重写并重新测试。用户明确要求回收式清理后，6 个历史 Studio ZIP（1439666112 字节）通过 SendToRecycleBin 移入回收站，源路径与回收站条目均验证。保留当前候选、权威 dist、源码和 Data。

### 当前增量：草稿修整与整组文件

已实现：多 Start 保存/多余副本删除/末节点保护、导出前拒绝非法结构；修复复制后保存再撤销与共享布局；当前 UID 导出文件名；C# DgrsGroupManifest/Validator/Exporter 扁平整组容器；Studio 导出路由；整组只读 Reference 文件、目录和历史捕获。单组包含混合 Flow/Logic、共享角色去重、缺失/重复/断开/额外数据拒绝的 Core 测试通过。Java 组 Reader/Loader、整组 Import、完整 UI 分组图和最终权威客户端尚未交付。

Java 已新增 StrictPackageJson 和 StoryGroupPackageReader，并通过 C# 实际导出文件和 6 个损坏文件探针。成员不完整、重复 UID、额外条目、断开图、缺失身份 marker、重复 JSON field 均拒绝。当前 StoryPackageLoader 仍是旧 first-winner/旧包 fallback，下一步必须改为完整 inventory，两侧冲突阻断；不得把 Reader 完成当作 Runtime 完成。

### Runtime inventory / generation 增量

已替换 first-winner 与单包旧内容 fallback：两种容器统一进入完整 inventory，禁用及可信清单但坏载荷均参与成员冲突；冲突双方都阻断。普通禁用保留已验证定义与原成员对象，仅禁止新 Start。扫描整体失败保留原 revision，全部容器不可用时清空旧运行定义。语义成员指纹已在 C#/Java 实际容器比对；共享物理 ZIP 使用独立成员 owner leases。整组 Reference / Export 已接入，Import / 管理器仍未完成。

CRC 回归通过（group-inventory-crc-java.log）：坏载荷保留可信成员声明，坏清单不猜 UID；重复清单字段拒绝。新增独立 Story 完成摘要，退役移除游标但保留完成数、终态与重复资格，未恢复旧定义游标；任务完成历史与奖励收据保持独立。group-history-java.log 为定点证据，不代表完整实机通过。

空间维护：另有 12 份临时 organized/portable publish 目录共 7,122,596,914 字节移至回收站。相同分区回收本身未释放空间；随后仅对这批可恢复副本做 NTFS 压缩，12 项退出码均 0，apphost 字节校验不变，可用空间约 3.66 GB。未清空回收站。

## 2026-10-04 当前身份与引用刷新补充

- 世界状态严格切换当前 schema：Session world 7，Session/Task/Story instance 2，Player RPG 与 Task completion history 2；地址字段写结构化 NBT，旧根版本拒绝。拒绝候选前不覆盖现有待绑定状态。针对性 Java probe 通过，实机重启持久化仍在验证。
- 引用包增删更新现有 ProjectGraph Host，保留位置与历史；历史恢复提交重新投影当前 provider；项目图和引用操作按历史序号协调。`reference-refresh.trx` 3 通过。
- 共享聚合参数替换补充所有 membership 使用者的 typed Actor/Item 依赖，覆盖未放置聚合节点的使用者；取消不落盘，Undo/Redo 同步目录和已打开工作区。`clipboard-parameter-dependencies.trx` 10 通过。
- 原生窗口已验证整组只读图包含两故事、一条内部连线，双击进入成员并返回。截图 `studio-reference-whole-graph.png`、`studio-reference-member-graph.png`、`studio-reference-return-group.png`。
- 当前 Core 全量结果 281 通过、223 失败、12 跳过；旧测试身份夹具及生产残余兼容入口仍需逐项处理，不能引用历史绿色结果代替当前结果。
- 第二轮六个已完成发布的临时目录进入回收站并确认源路径不存在；仅压缩这些已回收目录，EXE hash 不变，未清空回收站。

- 实机发现并修复 Java CanonicalGraphResource.CURRENT_SCHEMA_VERSION 仍为 1 的遗漏，统一为 2；加载器使用同一常量。原扫描成功不代表可运行，StoryGroupPackageProbe 现追加真实载入 Story/Task 的运行实例构造。`current-runtime-execution.log` 通过，6 个坏容器拒绝。
- 双端运行：A 停留 Session 时仅更新 B，日志 A UNCHANGED/B UPDATED；禁用整包后 B 不启动，A 实际点击继续后 TERMINATED；管理器启用恢复 B 启动。重启同世界后 A 完成次数仍为 1，返回触发区域不重播。证据见 Delivery.tests.active_story_runtime。无媒体内容和 Task 奖励的完整实机覆盖仍待做。


## Current identity follow-up

- Removed Studio Namespace policy/migration/rename-map implementations and Java DgrResourceId plus namespace-origin warning APIs. Java legacy Story/Quest/Dialogue entry points now reject retired documents. Current project provenance remains independent of resource ownership. Feature-only Namespace migration tests/probes retired; other regression coverage is being converted to current fixtures.
- Actor saved identity rename is rejected. Duplicates allocate a fresh hidden address under the same owner.
- Export preflight rejects blank public boundary names/IDs across Story, Session and Task, including unplaced owned resources and provider definitions; rejected archive export preserves the prior file. Native definitions cannot shadow different provider bytes at the same address.
- Graph and membership canonical path collisions no longer load or overwrite another identity. Actor/Item repositories have equivalent occupation guards. Arbitrary file names remain supported through content identity discovery.
- Current Session/Story chooser network decoding validates Story UID and Session address kind; media descriptors share the Story UID validation primitive.
- Latest full Core refresh: 362 passed, 134 failed, 11 skipped (507 total). This is incomplete construction, not full acceptance. Targeted current lifecycle/boundary, Actor, Item/pages, envelope/metadata and path-protection suites passed. Authoritative dist still awaits promotion; no finished-product release was created.


## Current project services and regression follow-up

- Removed four retired legacy-project migration implementations and their migration-only tests; CurrentProjectRejectionTests verifies old project rejection preserves both source bytes and the active project. This retires automatic old-format upgrade, not current Story-to-Story content copy.
- Reference refresh preserves current layout/history and persisted native edges across provider removal/reappearance; saving while a referenced endpoint is absent fails without dropping its edge file. Cross-Story graph parsing requires current version 2 and rejects duplicate fields.
- CurrentProjectValidator permits only explicitly empty dormant Objective target/description strings under the existing unconnected-output rule. Connected empty targets and malformed fields remain invalid. Six DGRS package regression tests passed.
- Story discovery determines root presence from parsed content IDs. A valid membership with a different filename belongs to its declared Story; malformed files remain visible without inventing a matching opposite root.
- ProjectService Actor save/create/copy/reference/delete now uses current canonical Story membership. Save commits Actor and membership together with expected bytes, preserves display ordering, and leaves the document dirty after an injected membership-write failure; exact rollback verified. Copies allocate fresh identities and retain portraits/tags. Existing identity rename remains rejected.
- Full Core refresh: 450 passed, 32 failed, 11 skipped (493 total). Full WPF run completed: 371 passed, 277 failed, 1 skipped (649 total), superseding prior hangs. Subsequent targeted WPF current-fixture suites passed 73/73 and 96/96; no new full-suite pass is claimed.
- Updated dynamic-content shared vectors to current structured addresses/DGR2 while preserving DGR1 inputs as rejection cases. C# and Java shared-vector checks passed (29 vectors).
- Candidate and authoritative Studio binaries still await rebuild/promotion with these changes. Real Task/reward/media and integrated-client acceptance remain outstanding. No finished-product package or remote publication was performed.

- Follow-up full WPF: 563 passed, 85 failed, 1 skipped. Targeted controls 123, resource Shell 19, Actor Shell 2, Story lifecycle 7, Project/discovery 6 and offline workflows 8 subsequently passed. No full-suite pass claimed.
- Nominator current network boundary now transmits typed owner/local addresses; action NBT requires current identity marker. Catalog/binding protocol has explicit current magic and rejects old framing. CurrentNominatorWireProbe and full Java build passed.
- Authoritative Studio was rebuilt/promoted to dist with 2383 Data files byte-identical across deployment. ProductVersion 0.3.3.6, EXE 204288 bytes, SHA256 8e8bf253b16b1516616d890b8bd604a907cf9d607469e3785d4ab113a5b3b20c. Assembly hash and preservation inventories recorded separately; full actual acceptance remains incomplete.


### 后续实机与当前合同回归

- 修复完整容器导入读取媒体 ZIP 路径错误：实际条目为 `media/...`。单 Story / Group 媒体导入均通过，原字节保留。
- 修复 Windows 保存对话框重复追加 `.dgrs.g`：使用单段过滤器，按最终完整文件名执行覆盖确认。权威 Studio 原生导出已验证。
- 实际两成员媒体 Group 更新 B 时 A 的代次、活动 Session / 台词位置不变，蓝色立绘保留；语音具有真实下载、校验和播放请求日志。该证据不等同于人耳听音验收。
- 第二名实际 OP 禁用该 Group 后，A 继续显示并能通过原生点击正常终止；第二名玩家进入触发区域未启动被禁用成员。
- 双 OP 过期删除确认验证发现客户端清单刷新缺陷：服务端已拒绝删除，但旧列表和短暂提示不清楚。现已自动请求最新列表并保留错误提示，重新部署双客户端实测通过，目标文件 SHA-256 不变。
- 当前 Story 删除改为同一事务移除指向该 Story 的项目级连线；其他连线保留，删除失败可完整回滚。15 项相关测试通过。
- M2 旧自动迁移用例改为拒绝并保留原字节；Actor 生命周期改测当前身份链。13 项通过。
- Gate E/F 的旧 Dialogue/Quest 草稿合同已替换为当前 Session/Task 最小资源创建、独立编辑、所属身份、重复保护、成员写失败回滚与引用稳定性。资源草稿 UI 的保存/丢弃/Undo 仍由 WPF 层验证。Story 删除套件已改测当前生命周期，不恢复旧格式 fallback。
- Core 全量启用随附 FFmpeg：491 通过，0 失败，0 跳过。套件由 493 调整为 491，变化来自上述退休合同移植，不使用 Ignore 排除失败。WPF 全量另行记录。
- 磁盘低至约 1.4 GB 时，将本次四份 `portable-publish-*` 暂存副本无损 NTFS 压缩后送入回收站，源目录确认移除；可用空间回升至约 2.2 GB。没有永久删除或清空回收站。
