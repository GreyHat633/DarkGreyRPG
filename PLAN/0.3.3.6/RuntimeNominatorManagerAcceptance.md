# Runtime 指名器、NPC 选择与故事包管理验收（0.3.3.6）

本轮仅修改 Runtime 与对应 Java 回归，更新 `dist/darkgrey_rpg-0.3.3.6.jar`。故事包格式、稳定资源键、存档结构及已有包没有迁移或自动改写；Studio 程序未在本轮更新。工作区改动保留，没有创建成品包、发布、提交或推送。

## 实现结果

实体与物品指名器共用 `RuntimeDirectoryTree` 和 `RuntimeDirectoryVisuals`。根据安装容器的 `.dgrs.g` 类型创建故事组文件夹，独立故事与文件夹同级，成员缩进并显示层级线。列标题加粗、放大、使用主题强调色和分隔线，文件夹标题大于叶子文字。名称、类型、标签、故事与故事组名称参与搜索；搜索展开匹配项，不覆盖保存的折叠状态。

资源名称按定义显示四类中文标签。同名资源保留不同内部身份。引用成员显示 `[引用]` 及来源故事，仍可正常绑定；世界标签、当前绑定和物品提示不显示原始资源键，失效资源显示易读说明。“ID 释放”改为“释放绑定”。有界目录协议携带容器、名称与引用成员元数据，客户端和服务端同步更新编码校验；检查名称缓存随项目版本和绑定版本失效。网络协议更新要求客户端和服务端使用相同的本轮 JAR。

物理 NPC 点击由一个服务端入口收集故事开始／重启和当前 Task 的交互／提交候选，按故事聚合。收集不改变 SavedData；多故事显示中文选择窗口，一个故事直接执行，无候选保留普通行为。选择执行前重新核对玩家、实体、视线、距离、维度、全部绑定、项目快照和故事／任务状态。Task 派发限定所选故事与当前任务放置实例；旧 Task 实体监听器不再重复消费点击。既有物品提交事务、提交选择、多结算顺序和奖励收据实现继续使用。

管理界面改为连续滚动目录，传输分页只用于自动加载后续块。标题点击控制各组独立展开状态，成员选择、详情、刷新和缩放不折叠组。选择组默认显示只读流程图，选择故事显示结构化详情；保留适配、平移、缩放和 Flow 实线／Logic 虚线。普通详情使用强调标题、字段标签、缩进正文和独立滚动；故事 UID、复制 UID 与内容指纹仅在默认收起的“诊断详情”中显示。启停、删除依旧作用于整个安装容器。

管理窗口最小 300×180、默认 420×260 个逻辑像素，首次限制在约 80% 可用屏幕范围，调整后保留尺寸。只绘制可见目录行，列表和详情独立滚动。打开、手动扫描、启停和删除走已有加载事务；LIST、详情、折叠、选中和绘制只读已提交快照。移除三秒心跳及会话心跳续期，使用权限检查后的版本通知刷新打开的界面；关闭和断线清理会话，同来源句柄稳定，过期响应不能覆盖新版本。刷新保留展开、选择、滚动和图视角。

## 自动测试

最终 Runtime `build` 成功，包含编译、Spotless、Checkstyle 和 Gradle `test`；Probe 不等同于 Gradle `test`，其完整运行结果另列。

相关 Probe 通过：

- `runtimeInteractionUi0336Probe`：加载用户当前 A→B 导出包，A 可重启、B 当前 Task 可继续；候选收集前后 Story／Task NBT 相同，选择 B 只结算 B 当前实例，其他故事及其他放置实例不变，B 进入第二段对话，A 不重启，重放不再次结算。覆盖保存恢复、项目变化、绑定增删、玩家不匹配、取消、令牌过期与重复提交。
- `canonicalActorArbitrationProbe`：多个开始／重启候选、只推进所选故事、旧选择拒绝、无候选不消费，以及多个等待的非破坏行为。更新其旧身份夹具，没有降低生产校验。
- `currentNominatorWireProbe`、`canonicalStoryChooserCodecProbe`、`creatorUxProbe`、`utilityWindow0324Probe`、`publicOutputPriority0336Probe` 通过。
- `creatorNetworkDiscriminatorProbe` 更新旧全局编号断言，按通信通道与接收侧检查编号唯一性，允许同一消息在两侧注册；重跑通过。
- 目录覆盖单成员物理故事组、根级对齐、子级缩进、独立展开与搜索不改变折叠。四类标签和同名资源分类通过。
- 管理目录覆盖 29 个条目的三块传输；打开扫描一次，普通 LIST 不扫描，手动扫描一次，同来源句柄保留，版本通知只发送一次并重新检查权限。
- 管理传输额外使用测试注入的已提交快照，覆盖 65 个成员、65 条 Flow／Logic 连线和 26 条诊断：成员／连线每块不超过 32，诊断每块不超过 12，自动块请求不扫描。该部分验证传输边界；磁盘包合法性另由真实用户包加载验证。

完整 Java Probe 使用 `--continue` 执行 94 个不同任务。初次完整运行 **41 通过、53 失败**，没有记为完整套件通过；日志中的三个失败任务先输出普通 Task 行、随后输出 FAILED 行，统计以任务身份去重，并以失败结果为准。随后网络编号 Probe 修正并单独重跑通过，按各任务最近结果为 **42 通过、52 失败**，没有将单独重跑冒充为再次执行完整套件。

失败明细和原始原因见 [FullRegressionResults.json](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/FullRegressionResults.json)。主要包括旧 Story UID／资源键、旧 Task 或成员 schema、缺失 `identity_format`、缺失显式输出顺序；另有 `dialogue0333Probe` 的排版断言、`canonicalStoryCommandProbe` 的 Journal 边界断言和部分加载夹具空值。未对所有失败完成修改前基线对照，因此不声称全部失败已证明与本轮无关，也没有放宽 Runtime 的格式拒绝规则来让旧夹具通过。旧完整 Task／物品提交 Probe 的失败意味着其广泛历史场景尚未获得本次完整绿色回归证明。

日志：

- [delivery-build.log](/E:/Java/MinecraftMod/DarkGreyRPG/.tooling/0336-runtime-ui/delivery-build.log)
- [final-build.log](/E:/Java/MinecraftMod/DarkGreyRPG/.tooling/0336-runtime-ui/final-build.log)
- [arbitration-regression.log](/E:/Java/MinecraftMod/DarkGreyRPG/.tooling/0336-runtime-ui/arbitration-regression.log)
- [scope-regression.log](/E:/Java/MinecraftMod/DarkGreyRPG/.tooling/0336-runtime-ui/scope-regression.log)
- [network-regression.log](/E:/Java/MinecraftMod/DarkGreyRPG/.tooling/0336-runtime-ui/network-regression.log)
- [full-regression.log](/E:/Java/MinecraftMod/DarkGreyRPG/.tooling/0336-runtime-ui/full-regression.log)
- [final-test-style.log](/E:/Java/MinecraftMod/DarkGreyRPG/.tooling/0336-runtime-ui/final-test-style.log)

## 代理游戏实机

复用 `.tooling/0336-runtime-ui` 一个候选根目录，运行 Forge 1.7.10 隔离客户端与专用服务端、隔离存档 `RuntimeUiWorld`、测试玩家 `DgrRuntimeUI`。通过 Win32 的真实键鼠输入操作 Minecraft 并截屏，未使用 Computer Use。候选客户端、服务端与交付 JAR SHA-256 相同。

- 用户的单故事和故事组包复制到隔离目录并正式加载。原目录没有写入测试包，源文件与隔离副本的 SHA-256 相同：[OriginalPackageHashes.json](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/OriginalPackageHashes.json)。
- 从 B 选择并绑定 A 的引用角色：资源行显示 `[角色] 测试员A [引用]` 及“来源：故事组A”；当前绑定显示类型与名称。证据：[10-reference-name.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/10-reference-name.png)、[11-binding.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/11-binding.png)。
- Vanilla 生物与 CustomNPC+ 均验证 A→B 点击路径。真实 CustomNPC+ 右键开始 A，对话结束后 B 进入交互 Task，再右键出现“A 重新开始／B 继续”；选择 B 后完成任务并进入 B 第二段对话。证据：[42-cnpc-actor-start.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/42-cnpc-actor-start.png)、[44-final-cnpc-choices.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/44-final-cnpc-choices.png)、[46-cnpc-b-continued.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/46-cnpc-b-continued.png)。头顶标签为 `[角色] 测试员A`。
- 过期选择实测未推进 B，重新右键获取新令牌后选择 B 成功。保存后的状态记录在 [NativeLedgerAfterB.json](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/NativeLedgerAfterB.json) 和 [NativeLedgerAfterCustomNpcB.json](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/NativeLedgerAfterCustomNpcB.json)；测试期间运行过不同激活实例，不把多次不同激活的历史收据误称为重复提交。
- 物品指名器显示 `[物品] 铜币`、`[物品组] 剑`；真实背包金粒完成铜币绑定，提示显示 `[物品] 铜币`，没有原始资源 ID：[59-item-bind-name.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/59-item-bind-name.png)、[60-item-tooltip-name.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/60-item-tooltip-name.png)。角色组分类另有自动验证，未将其记为角色组实体绑定实机通过。
- 管理界面检查深浅主题、组默认流程图、成员详情、标题字段层次、诊断默认收起／展开，确认无分页与“只读图”按钮。证据：[32-final-group-fit.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/32-final-group-fit.png)、[33-final-story-details.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/33-final-story-details.png)、[52-final-dark-group.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/52-final-dark-group.png)、[55-diagnostics-default.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/55-diagnostics-default.png)、[56-diagnostics-expanded.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/56-diagnostics-expanded.png)。
- 默认窗口及真实拖动缩至 300×180 通过，GUI 缩放 1 和 2 已检查；Minecraft 中文字体下设置缩放 3 实际折回 2，单独记录，不冒充实际倍率 3。证据：[08-manager-minimum.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/08-manager-minimum.png)、[70-gui-scale-one.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/70-gui-scale-one.png)、[71-scale-one-minimum.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/71-scale-one-minimum.png)。窗口缩小保留图视角，需要时可点击“适配”。
- 手动刷新保留展开状态和成员选择：[34-refresh-preserves-fold-and-selection.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/34-refresh-preserves-fold-and-selection.png)。空闲 43.2 秒加载日志数量 6→6：[IdleScanEvidence.json](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/IdleScanEvidence.json)；精确扫描次数另由自动 Probe 验证。
- 临时加入 15 个无效容器，与两个有效容器组成超出原 12 项分页容量的目录。自动载入后可连续滚动到末尾并展开故事组：[63-directory-after-chunks.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/63-directory-after-chunks.png)、[64-large-directory-group.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/64-large-directory-group.png)。临时容器随后送回收站，手动重扫恢复错误数 0 并保留组展开：[65-final-clean-inventory.png](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/65-final-clean-inventory.png)。

隔离存档、脚本、日志及证据保留供复查，候选客户端已停止，专用服务端正常保存并退出。没有修改用户正在使用的游戏存档或 `run/client/mods`。

## 交付与用户验收

| 文件 | 版本 | 字节 | SHA-256 |
| --- | --- | ---: | --- |
| `dist/darkgrey_rpg-0.3.3.6.jar` | 0.3.3.6 | 1,972,534 | `0dc43d497175a85f38f9e809387cb03b64216e9bdb20f4009e58febaa576cab9` |

已核对 JAR 中的 `mcmod.info` 为 0.3.3.6，构建、交付、候选服务端、候选客户端四份哈希一致：[RuntimeArtifact.json](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/RuntimeArtifact.json)。18 个确认无用的临时文件共 595,937 字节，只送入回收站：[RecycledTemporaryFiles.json](/E:/Java/MinecraftMod/DarkGreyRPG/PLAN/0.3.3.6/evidence/RuntimeUi/RecycledTemporaryFiles.json)。

用户验收尚未进行。本报告分别记录自动测试、代理实机和用户验收；完整回归仍有 52 个最近失败结果，不宣称完整套件已通过。跨故事物品提交、复杂多目标组合和全部历史存档场景的全面实机覆盖仍有限，需结合失败清单继续评估；当前用户 A→B 导出包的阻塞路径已经自动及真实 CustomNPC+ 双重验证。
