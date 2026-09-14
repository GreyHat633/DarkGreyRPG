> 2026-09-14 最终集成更新：本文件保留工作包阶段记录；后续编译修复、真实交互及最终证据以 [DELIVERY_REPORT.md](DELIVERY_REPORT.md) 为准。阶段性待办不能覆盖最终记录，未实测部分也不自动计为通过。

# WP-C Java runtime and player presentation

状态：部分完成：Java WP-C 实现已覆盖候选选择，定向验证曾通过；共享媒体整合当前阻断最新编译，实机验收未完成。本文件只记录 Java WP-C 范围。

## 已实现

- `CanonicalTaskRuntime` 的 `submit_item` 同时要求 `item` 和 `actor_id`，事件匹配同时核对两者；区域 `dimension_id`、中心 XYZ 和 `radius` 只接受有限的 32 位整数，区域比较使用整数方块边界。
- `CanonicalTaskForgeManager.synchronizeObjectives` 使用 1.7.10 `MathHelper.floor_double` 采样玩家方块坐标。
- `CanonicalTaskForgeManager.handleEntityInteraction` 校验实际服务端实体仍存活、同世界、距离不超过 6 格且可见，并从 `EntityDgrIdentityResolver` 获取服务端 actor 身份；单一候选直接提交，多个候选发出短期服务端选择 capability。选择响应重新验证实体 UUID/ID、维度、距离、存活、身份、ACTIVE/run activation 和背包事务；一笔实体交互最多提交一个目标。已取消的 Forge 交互事件不会再进入 Task 路径，避免 Story 先消费时重复扣除。
- 新增 `CanonicalTaskSubmitChoiceStore`、`CanonicalTaskSubmitChoiceFrame`、`CanonicalTaskSubmitChoiceSelection` 和非暂停客户端选择窗；choice token 单次使用、60 秒过期，错误/过期响应不会扣物品。
- `CanonicalTaskSubmit` 与旧 `CanonicalTaskPresentationServer.submit` 保留协议/API 兼容形状但均不再执行扣物品；任务菜单改为展示“与指定角色交互提交物品”。
- Task Journal/UI 投影增加提交对象 actor 标识和区域 XYZ；不向玩家投影维度 ID、半径或 `dimension_note`。
- `CanonicalProjectContentLoader` 对提交目标检查 actor 资源声明。

## 探针

新增 `Task0331RuntimeProbe` / `task0331RuntimeProbe`，覆盖：错误 actor、缺 actor、正确 actor、完成后锁存；负数中心/零半径；`-0.1` 方块向下取整；小数区域坐标拒绝。

## 验证边界

`git diff --check`：通过（WP-C 修改范围）。

`compileJava`、`compileTestJava`：PASS。

`task0331RuntimeProbe`：PASS（actor 绑定、错误/缺失 actor、完成锁存、负数坐标、`-0.1` 向下取整、小数区域拒绝、多候选单次 capability、错误 token、过期和重放拒绝、选择帧/响应编解码）。

`task0330Probe`：PASS（收集锁存、区域边界、提交事务恢复/重放、协议截断）。

`canonicalTaskJournalProjectionProbe`：PASS。

`canonicalTaskForgeProbe`、`canonicalTaskEventPersistenceProbe`、`canonicalTaskJournalIntegrationProbe`：PASS（既有 Forge 规范化/管理器、持久化恢复和 Journal 兼容回归）。

尝试运行：

```text
JAVA_HOME=E:\Java\jdk-25.0.1
GRADLE_USER_HOME=E:\Java\MinecraftMod\DarkGrey_RPG\.gradle-user-home
gradlew.bat testClasses task0330Probe --offline --no-daemon --no-configuration-cache --max-workers=2 --console=plain
```

首次尝试曾被共享 worktree 的未跟踪媒体文件 `src/main/java/darkgrey/rpg/project/packages/DgrsGenerationStore.java` checked exception 阻断；该文件随后修复，本 WP-C 未修改。候选选择加入后，最新共享编译又被媒体工作包 `CanonicalMediaServer.java` 调用 `LoadedStoryPackage.retainMediaRequest/releaseMediaRequest` 的可见性错误阻断。全局 `spotlessJavaCheck` 仍报告其他媒体/历史脏文件格式差异；WP-C 文件未出现在剩余报告中。未声称真实 Minecraft 客户端验收。

## 剩余风险

- 多个 ACTIVE 提交目标现已接入任务专用候选选择界面；实际 Minecraft GUI 操作、选项排序体验和交互事件跨适配器顺序仍需实机验证。
- `CanonicalTaskSubmit` 仍注册以容纳旧客户端，但服务端处理器明确拒绝远程扣除；旧客户端需通过真实实体交互完成提交。
- 本探针无法替代 Forge 服务端中实际 `EntityInteractEvent`、CustomNPC+ 迁移身份和网络连接的实机验证。
