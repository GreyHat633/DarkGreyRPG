# Studio 界面整改与三层公开端口改革：验收与交付

> 后续修正：Project 连线编辑已恢复，公开输出上下按钮已移除，左栏拖动反馈已改进。最新交付和验收见 [UIFollowupAcceptance.md](UIFollowupAcceptance.md)；本页保留此前记录。

日期：2026-10-05。版本：0.3.3.6。状态：开发实现及代理验收完成，权威 dist 已更新；`USER_ACCEPTED=NO`，`RELEASE_READY=NO`。没有创建成品包、Release、标签、提交或推送。原工作区改动保留。

本报告对应本轮确认的 15 项整改计划，取代 `Acceptance.md` 中整改前的测试数量与交付哈希。实现说明见 [UIReformImplementation.md](UIReformImplementation.md)，机器记录见 [Delivery.json](Delivery.json)。本轮证据集中在 [evidence/UIReform](evidence/UIReform)，整改前的交付记录保存在 `PreviousDelivery.json`。

## 自动验证

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Core 完整套件，使用交付 FFmpeg | 495 通过，0 失败，0 跳过 | `reform-core-final9.log`、`reform-core-final9.trx` |
| WPF 完整 Release 套件，软件渲染 | 659 通过，0 失败，1 跳过；共 660 | `reform-full-final13.log`、`reform-final13.trx` |
| Java 构建、变更范围 Spotless、Checkstyle、测试 | BUILD SUCCESSFUL；公开输出/结算优先级探针通过 | `java-build.log` |
| 三层输出排序、改名、稳定连线、打乱节点数组、DGRS 往返 | 通过，亦包含于 Core 全量 | `port-roundtrip.log` |
| 恢复旧 Story/Session/Task 的严格拒绝 | 三类均不改文件、不替换当前正常项目；包含于 WPF 全量 | `reform-final13.trx` |

WPF 唯一跳过项是原有的 `FixedThreeHundredNodeWorkload` 性能基准；没有增加 Ignore 来隐藏失败。跨语言身份、指纹与动态文本探针亦已运行，记录保留在 `.tooling/0336-identity/reform-java-current.log`。自动测试覆盖 Flow/Logic 分类排序、空卡片隐藏、引用资源只读、删除保护、统一历史、事务失败回滚及输出合同校验；这些结果与下面的实际键鼠验收分开记录。

## 原生 Studio 验收

使用 Windows UI Automation、Win32 输入及屏幕截图，测试资源均位于独立验收项目。没有以控件树存在代替交互结果。

| 场景 | 实際检查结果 | 本轮证据 |
| --- | --- | --- |
| 新建故事与资源 | UID 输入栏移除；名称初始为空，焦点占位行为、空白不可提交；六类方括号标签；用途说明 | `new-story.png`、`name-focus.png`、`six-resource-labels.png`、`item-kind-final.png` |
| 资源选择 | 点击 Inspector 保持当前资源；侧栏空白、画布空白清除资源选择；点节点切换 Inspector | `selection-native6.json`、`selection-native6.png` |
| 剪刀 | 持续模式、Ctrl 临时模式、持续模式按 Ctrl 退出；文本 Ctrl 不剪线 | `scissors-native.json`、`scissors-text-native.json` |
| 菜单分类 | Story 终止、Session 结束、Task 结算在流程；标题在演出 | 四张 `*-menu-verified.png` |
| 输出编辑 | Session/Task 节点与 Inspector 编辑现有输出、保存及键盘撤销重做；稳定身份与目标由持久化数据断言核验 | `session-native-undo4.json`、`task-native-undo.json` |
| 最后结算保护 | 多个时逐个删至一个，再删被阻止；批量删除和剪切不能删光 | `task-last-settlement-native.json`、`task-batch-protection-native.json` |
| 两种主题与窄栏 | 深色实际编辑、浅色窄 Inspector 两类输出卡片与按钮可用 | `task-last-settlement-restored.png`、`narrow-inspector-light.png` |
| 故事组布局和导航 | 右键改名；组卡片与独立故事顶层排序；框内空白拖动保持成员相对位置、一次撤销恢复 | `group-rename-fixed.png`、`group-rename-reorder5.png`、`group-move-native.json` |
| 本地组删除 | 删除后 3→1，连续 Ctrl+Z/Ctrl+Y 恢复/重删，最后恢复 | `group-delete-undo6.json`、`group-restored-ui6.png` |
| 混合引用组删除 | 确认列出本地故事与完整引用容器；解除引用；撤销恢复包；外部原始包哈希不变 | `mixed-delete-confirm.png`、`mixed-group-native.json` |
| 组外依赖 | 明确列出阻断依赖，全部资源及引用文件不变 | `group-dependency-blocked.png`、`group-dependency-native.json` |

Project 图已移除普通组合与剪刀入口，禁止新增、改接或剪断连线；故事组仍只由连通关系产生。引用定义的名称、公开端口和 Task 优先级保持只读；本地布局元数据独立处理。自动测试覆盖比上述实际点击记录更细的空类别、引用资源及失败回滚分支，不宣称每个分支都有单独实机录像。

## Runtime 实机与台词

在独立专用服务器及实际客户端中，以相同目标同时满足两个 Task 结算条件。第一份任务显式优先级选择 `perfect`，第二份颠倒优先级后选择 `ordinary`；测试图的 JSON 节点与边数组已反转。每次只推进对应 Story 分支一次，每份任务只有一个奖励收据。

追加重复事件后没有再次结算；停止服务器、重启并重新连接后再次发送事件，最终状态保持一致。NBT 比较结果：`TaskStateByteEquivalentAfterRestart=true`，两份任务各一个奖励收据，玩家经验总数重启前后均为 14。证据：`settlement-live.txt`、`live-nbt-before-restart.json`、`live-nbt-after-restart.json`、`runtime-restart-assertions.json`。延迟条件和单次求值稳定点的细分组合另由 Java 探针验证。

长中文、英文与混排文本在真实客户端按当前窗口分页，未扩大对话框。动态玩家名实际展开为 `Native0336`；将窗口缩至 820×620 后，正文按可用行数继续分页并显示“最后一句：分页内容完整结束。”，点击继续后该 Story 成为 `TERMINATED`。证据：`reform-dynamic-small-first.png`、`reform-dynamic-small-page2.png`、`reform-dynamic-last.png`、`dynamic-story-final-state.txt`；前一轮普通长台词截图仍在上级 evidence 目录的 `reform-dialogue-*.png`。

Studio 估算使用 1280×720、GUI 缩放 2、默认对话设置及游戏字宽/分页规则；动态内容明确为估算。长文本没有推荐容量输入上限，实际分页以运行窗口为准。

## 严格读取与数据保护

按本轮计划，不转换旧 Task 多结果槽，也不补齐缺失或冲突的公开输出排序。旧资源会给出具体合同错误；程序不自动修改或删除原文件。权威 Studio 启动时曾暴露旧项目恢复异常，已增加打开前只读校验及清晰错误处理，并重新构建、发布和全量回归。

最终权威 EXE 启动保持响应，显示旧项目的 `graph.output.order.required` 和“文件保持原样”；没有加载一半后修改项目。证据：`dist-launch-final.png`、`dist-launch-final.json`、`dist-runtime-modules.json`。该旧项目需要使用新合同的资源，程序不会替用户迁移。

最终程序替换前后 Data 共 2,619 个文件、59,830,770 字节，变化数为 0；启动后再次核验原有 267 个项目文件，变化/缺失数为 0。见 `dist-data-final-audit.json`、`dist-project-files-final-preserved.json`。发布前一次失败启动产生的诊断日志保留，未冒充发布导致的数据变化。

## 权威交付

Studio 使用自包含 Windows x64 Release 目录部署。根目录只有 apphost EXE，子目录为 Program、Tools、Docs、Data；程序白名单 405 项全部存在。实际加载的 `coreclr.dll` 位于 Program。见 `portable-layout.json`、`publish-dist-startup-final.log`、`DeliveryFiles.json`。

| 文件（仓库相对路径） | 版本 | 大小（字节） | SHA-256 |
| --- | --- | ---: | --- |
| `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe` | 0.3.3.6 | 204288 | `8e8bf253b16b1516616d890b8bd604a907cf9d607469e3785d4ab113a5b3b20c` |
| `dist/DarkGreyRPGStudio/Program/DarkGreyRPGStudio.dll` | 0.3.3.6 | 1716224 | `f9a083a192d2200f11289c184c684adf28b914471a056884993c231362da60ce` |
| `dist/DarkGreyRPGStudio/Program/DarkGreyRPG.Studio.Core.dll` | 保留现有程序集版本 | 1303040 | `a5c7af122ccbc29cbb6bc7c21bac25113248ef8df97b040b3b5652149b807653` |
| `dist/darkgrey_rpg-0.3.3.6.jar` | 0.3.3.6 | 1953461 | `b273b422808eb43f015ad9b574702c07e9e52eba5e2be09151d6311d0e85458e` |

空间不足阶段仅处理已确认的测试/发布临时文件，回收操作保持可恢复；未清空回收站。最终发布使用了对 `.tooling/wpf-build` 中已核实暂存目录的无损 NTFS 压缩以腾出空间，没有压缩或搬移用户项目及 Data。

自动测试、代理实机结果如上；用户尚未亲自验收。本报告不代表用户接受，也不代表成品发布。
