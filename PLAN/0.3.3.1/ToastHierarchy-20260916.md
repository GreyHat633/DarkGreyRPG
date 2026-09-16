# 任务提示层级与完整动画 — 2026-09-16

## 实现范围

仅修改客户端 TaskNotificationCards 和 TaskNotificationsPlanProbe；服务端事件字段、任务判定、故事包和 Studio 未作本次修改。

- 任务名白色加粗，两行独立区域；下一步正文独立两行；辅助信息 85% 字号灰字。超长按 Unicode code point 截断并加省略号。
- 按任务实例与目标 ID 合并通知；先收集同批事件，再移除完成目标。终态清空下一步和旧完成摘要。多目标保留第一项，补充其余数量。
- 600 ms smoothstep 滑入、完整阅读 4000 ms、600 ms 滑出，使用 System.nanoTime。完全离屏才移除。
- 更新延长阅读，入场不重播；退出中更新从当前偏移平滑返回。最多三张，额外任务排队，出队卡片从入场开始计时；空位以 600 ms 平滑补位。
- 保持原有去重集合及纯展示边界。小窗口统一缩放通知区域，为三张卡片保留空间。

## 自动化

`spotlessJavaApply taskNotificationsPlanProbe assemble` 通过。构建日志：`evidence/ExperienceLogs/toast-hierarchy-build.log`。

覆盖原服务端接取、数量变化静默、完成、失败、初始同步静默，以及客户端：

- 完成旧目标/激活新目标的两种事件顺序、多目标、完成单事件正文。
- 终态清除旧目标、终态优先于同批目标事件、重复消息不延长停留。
- 长文本/Unicode/省略号、入场单调性、600 ms 入场与完整四秒阅读、完整退出才移除。
- 退出中更新位置不跳变、平滑返回、第三张上限、第四张排队及平滑补位。

## 实机证据及边界

独立 Minecraft 世界 `ToastHierarchyFinal0916`，位于 `.tooling/0.3.3.1/ExperienceClient/saves`。使用隔离测试 harness 和原生 Windows 截图；本次游戏通过 Gradle runClient 启动。

服务端实际创建 `GreyHat_:KillSlimes` 任务。最初尝试直接伤害测试 NPC 未推进击杀计数，因此最终由仅存在于测试 harness 中的 toastKills 操作提交三个 CanonicalTaskEvent.killEntity 事件。任务系统实际判定旧目标完成、激活与酒馆老板对话目标，客户端接收原有网络通知。随后调用真实客户端实体互动路径；服务端最终任务为 SETTLED，两项目标均 COMPLETED。

这验证通知/任务状态/网络/渲染集成，不构成完整战斗流程验收。未将客户端伪造通知作为真实任务证据。

- `evidence/ToastHierarchy-Received.json`：接取状态。
- `evidence/ToastHierarchy-Next.json`：旧目标 COMPLETED，新目标 ACTIVE。
- `evidence/ToastHierarchy-FinalState.json`：toast-final 与 toast-scale3 均 SETTLED。
- `evidence/live/ToastHierarchy-Received.png`、`ToastHierarchy-Next.png`：GUI scale 2，标题、下一步、完成摘要分区可见。
- `evidence/live/ToastHierarchy-Scale3.png`、`ToastHierarchy-Completed.png`：GUI scale 3，卡片与对话并存、终态正文无过时下一步，未见卡片内文字裁切。

完整动画录制脚本被自动审批拒绝，仅返回 `blocked by policy`，没有具体理由；未绕过此拒绝，未生成录像。动画时序、退出返回、三卡交接依赖确定性自动化检查；本次静态截图不冒充完整动画实机录像。多目标/极长文本/失败/三卡交接没有逐项实机录像。

## 正式产物

`E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.1.jar`

- 1,244,840 bytes
- SHA-256: `9C0ECBEDBB4AB7F9467DCCF88E6ED715026E6F2EDF29D55BB1D29C7093F44CBE`
- 与 build/libs 的构建产物哈希一致，见 `evidence/ToastHierarchyArtifact.json`。
- USER_ACCEPTED=NO。功能和构建已交付，完整动画录像验证仍缺失。
