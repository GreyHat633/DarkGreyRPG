# 0.3.3.6 最终证据索引

> 后续修正：Project 连线编辑已恢复，公开输出上下按钮已移除，左栏拖动反馈已改进。最新交付和验收见 [UIFollowupAcceptance.md](UIFollowupAcceptance.md)；本页保留此前记录。

> 2026-10-05 后续整改：最新测试、实机结果和交付哈希见 [UIReformAcceptance.md](UIReformAcceptance.md) 与 [Delivery.json](Delivery.json)。本页保留整改前的历史验收记录，原测试数量及二进制哈希不再代表当前 dist。

最终状态以 Acceptance.md / Delivery.json 为准。全部文件在 evidence/ 下；后半段为保留的历史记录。

- full-core-current-final-identity.trx：491通过。
- full-wpf-current-contracts.trx：652通过、既有1跳过。
- current-media-budget-trace-build.log：最终 Java 构建/检查及 CNPC 探针。
- current-namespace-audit.txt：最终旧身份关键词审计；参数名 fullId 表示当前 typed key。
- current-rich-copy-result.json、rich-copy-runtime-completed.json：富内容非空复制、修复、正式导出和实际完成。
- current-reference-closure-result.json：原生整体引用、NPC-only单包、公开边界四成员闭包。
- current-two-active-disable-result.json、current-flow-disable-result.json：两玩家禁用/重连/完成及 Flow 正对照。
- current-three-media-runtime-result.json：只更新 B 保留 A/C。
- current-two-op-result.json：过期确认拒绝与刷新。
- media-budget-observation.json：真实3下载/10 Story槽、全保护排队与完成释放。
- media-budget-retry-result.json：实际读取故障、部分就绪、重试恢复、hash验证。
- media-disconnect-inflight-result.json、media-conflict-inflight-result.json：下载中断线/冲突，无异步复活。
- media-conflict-manager.png：Enabled/Disabled/Conflict/Error实际同时显示。
- current-task-runtime-result.json、current-integrated-runtime-result.json：双端 Task 奖励防重。
- authoritative-studio-final-promotion.json、final-artifact-readback.json：权威程序版本/hash及Data保持。
- recycled-final-staging.json、recycled-old-candidate-program.json、recycled-conflict-test-duplicate.json：回收站清理凭据；原 Data/项目未清理。
- Delivery.json 的 test_packages：全部正式 Studio 测试导出路径、成员、大小和SHA-256。

## 历史施工证据

# 0.3.3.6 开工证据索引

- `evidence/identity-foundation.trx`：C# 共用向量/隔离/分配测试，3 通过。
- `evidence/core-foundation.trx`、`evidence/core-foundation.log`：首次通用事务剥离及草稿探针后的 Core 全量回归，482 通过、12 跳过。
- `evidence/java-identity-gradle.log`：正式 Gradle 编译 main/test 并执行 storyIdentity0336Probe，56 向量与 2048 分配通过。
- `evidence/core-foundation-final.trx`、`evidence/core-foundation-final.log`：包括资源复制映射后的 Core 全量回归，485 通过、12 跳过、0 失败。
- `evidence/java-final.log`：四个新 Java 文件已按白名单格式化；正式 Gradle main/test 编译、身份探针、全量 spotlessJavaCheck 均通过。
- `evidence/wpf-foundation.log`、`evidence/wpf-foundation.trx`：首次 WPF 全量运行长期不结束，已核验并终止本次 testhost；642 个已完成测试通过，整轮 ABORTED，不能算全量 PASS。
- `evidence/wpf-diagnostic.log`、`evidence/wpf-diagnostic/wpf-diagnostic.trx`、其 Sequence XML：带 60 秒单项空闲超时的全量诊断，595 个已完成测试通过；停在 `GroupFrame0333Tests.VisibleRenderingDuringMoveAndPageCollapsePreservesGroups`，超时 ABORTED，根因未确认。
- `evidence/wpf-affected.log`、`evidence/wpf-affected/wpf-affected.trx`：本次涉及的保存、引用历史、剪贴板、Story 工作区等筛选回归，80/80 通过。
- `evidence/identity-callsite-inventory.txt`：当前生产目录中 56 个旧身份直接命中文件，仅为搜索清单，非全链移除证明。开工第一次宽搜索的 67 文件含测试，不能写成 67 个生产文件。
- `schema/identity-0336-vectors.jsonl`：两端共用输入与预期 JSON/路径；56 条。源码新增或结构性测试不等于实机验收。

后续结果记录于 Delivery.json，不使用不存在的截图或正式导出包作为证据。WPF 全量未通过的原因尚未做基线对照，不宣称它一定是既有问题或本轮回归。

## 继续施工证据（按源代码阶段区分）

- `evidence/core-group-fingerprint.trx`：身份正式切换前 Core 491 PASS / 12 SKIP。
- `evidence/group-transaction.trx`：身份正式切换前分组事务/历史相关 WPF 61 PASS；实际目录以同名 log 中 Results 路径为准。
- `evidence/render-probe.png`：独立最小 WPF 默认与 SoftwareOnly 渲染对照。
- `evidence/group-native-result.json`：隔离候选组名保存、折叠/展开原生读回。
- `evidence/current-resource-identity.trx`：当前新身份 Core 8 PASS；不替代全量回归。
- `evidence/current-story-dialog.trx`：Story 只读 UID 与旧身份拒绝 2 PASS。
- `evidence/current-identity-studio-build.log`：正式 Studio 项目编译通过。
- `evidence/current-fixture-generation.log`、`evidence/java-current-resource-probe.log`：Studio Core 真实文件生成与 Java 正式加载器对读；探针还在扩充 SavedData 检查，最终以最新日志为准。
- `evidence/current-identity-native-created-session.json`：候选 UI 创建会话后读回的当前 schema、marker、结构化地址。
- `evidence/current-identity-native-created-session.png`：当时窗口截图，保留了尚未清理的内部地址展示，因此不是最终 UI 验收。

上述 candidate ProductVersion 仍是 0.3.3.5，没有提升权威 dist，也没有进行 Minecraft 新版 E2E。

## 当前单 Story 容器

- current-resource-identity.trx/log：当前 9 PASS，含真实单 Story ZIP 导出/重开及格式拒绝。
- current-container-fixture.log：studio/tools/CurrentIdentityFixture 生成当前项目及 consumer.dgrs；路径 .tooling/0336-current-fixture/PackageProject。
- current-container-java.log：Java 正式安装扫描/合并读取该 ZIP，通过；不是客户端游玩验收。
- current-authoring-settings.trx/log：当前 WPF 身份/设置子集 30 PASS。

## Story 追加与回收清理

- story-content-copy.trx/log：3 项 Core 测试通过。
- story-copy-history.trx/log：WPF 一次历史/资源树往返/目标节点分离测试通过。
- story-copy-native-applied.json/png、story-copy-native-source-before.json：真实候选复制及源文件 hash；初版截图揭示布局重叠，不能作为最终布局 PASS。
- recycled-history-zips.json、recycle-history-verification.json：6 个历史 ZIP 的原路径、大小、SHA-256 与回收站条目核验；未清空回收站。

- story-copy-native-history.json：更新候选的菜单 Undo/Redo 实际落盘检查通过。
- story-copy-native-reopened.json/png：重启后两个 Start、已复制资源树仍在，节点位置已分开；该截图取代初版重叠截图作为此场景证据。

- `evidence/story-copy-draft.trx` / `.log`：WPF 定向 7 PASS，含多 Start 保存修整、末节点保护、后续保存后连续撤销、保留无关 Story 布局、当前 UID 导出文件名。
- `evidence/current-copy-core.trx`：身份/内容迁移 Core 12 PASS。
- `evidence/group-container-core.trx` / `.log`：组容器 1 + 当前身份 9 PASS；整组 Reference 往返和共享资源目录去重。
- `evidence/group-studio-build.log`：Studio 编译 0 warning / 0 error；非权威 dist 交付证明。

- `evidence/group-container-core.trx` 最新重跑扩大至 15 PASS（组/身份/迁移/多 Start 草稿边界），前文 10 PASS 为较早子集。
- `evidence/group-container-java.log`：Java 编译及 storyGroup0336Probe PASS；C# GroupProject2 导出双成员容器可读，6 个损坏容器拒绝。尚未接入 StoryPackageLoader 扫描与运行。
# 最新 Task 与输出拖动修正

- [TaskUIFixAcceptance.md](TaskUIFixAcceptance.md)：当前实施、自动测试、原生 UI、Forge 双端及权威交付记录。
- `evidence/TaskUIFix/task-ui-core-complete.trx`、`task-ui-wpf-final.trx`：Core 496 PASS；WPF 664 PASS、1 opt-in SKIP。
- `evidence/TaskUIFix/native-assertions.json`、`runtime-assertions.json`：稳定端口、节点坐标、一次 Undo/Redo、零结算 ACTIVE、优先级、重启与一次奖励核对。
- `evidence/TaskUIFix/DeliveryFiles.json`、`dist-data-audit.json`、`dist-startup.json`：正式路径及 EXE/DLL/JAR 哈希、Data 保持、原生启动。
- `evidence/TaskUIFix/PreviousDelivery.json`：保留本轮更新前的交付记录；当前版本规则以零结算修正报告为准。
