# 0.3.3.7 实现记录

施工日期：2026-10-06。基线 `d0d3f0e9f057793d1399568cc113f947fa1d86cc`，分支 `codex/0.3.3.7`。开工时仅施工计划未跟踪。

范围：WP-A～WP-G。保持最多三个、仅未来新接任务自动追踪、不补旧任务、两套 HUD 独立布局。沿用现有身份和动态端口事务。更新权威 dist，未授权创建成品包或远端发布。

验证结果及滚动页检查表记录于 Acceptance.md；产物核验记录于 Delivery.json。

七个工作包已实现。Java 构建、真实导出包运行探针、Studio 自动回归以及关键单人／联机实机检查已经执行；矩阵覆盖范围及未实测项分别记录于 Acceptance.md。用户验收尚未进行。

## 实现落点

| 工作包 | 最终实现 |
|---|---|
| A | `TaskTrackingSelection` 消费关闭／满额期间的新实例事件并去重；保留三个上限、手动选择和上下文恢复。`TaskTrackerBodyLayout` 按整行／整图标块分配正文预算，`TaskTrackerHud` 为每个故事和任务标题预留空间。 |
| B | 新增第四个「任务」设置页；新任务追踪默认开启、两套 HUD 默认右侧，各自持久化。关闭读取实际背包 KeyBinding，支持鼠标绑定，结束交互并保存。 |
| C | 删除 Minecraft 占位标签及静态页；旧标签选择回落到输出页，真实外部游戏重载入口保留。 |
| D | 普通列表使用当前像素偏移、实际行高和局部裁切；候选弹层保留传输分页，在当前页内平滑移动。Studio 使用 WPF 像素滚动并保留虚拟化；补充既有 UI skill 的滚动规范。 |
| E | Story／Session／Task 的 AND／OR 共用 `LogicInputsEditor` 双入口；默认／最低两输入，稳定 ID，连接删除确认，输入和连接原子撤销。真实单包及组包进入 Java 三种 Runtime 验证。 |
| F | 约三秒反馈、相同消息替换重新计时、切图／卸载／释放清理；预览与接收共用精确候选规则。补上 WPF 拒绝预览不触发 Drop 的反馈路径，并区分鼠标放开与 Esc 取消。 |
| G | 两套 HUD 各自选择左右，通知左进左出；自有绘制顺序为标题、追踪、通知，通知位于最后。单独管理绘制状态，目标 JourneyMap beta.4 实机验证覆盖。 |

## 交付

权威客户端保持 `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`，根目录仅启动 EXE，程序集在 Program、工具在 Tools、文档在 Docs、个人数据在 Data。更新由 `studio/package-studio.ps1` 执行，按程序白名单更新，不清空 Data。

部署脚本增加已退出进程过滤及被旧映像占用的程序文件替换处理：先将旧程序文件保存在仓库 `.tooling/wpf-build`，安装失败则恢复；检查目标和备份绝对路径，排除 Data，拒绝目录链接。

版本为 0.3.3.7；实际 EXE、程序集、Runtime JAR 校验见 Delivery.json。代码尚未提交或上传，未创建成品发布目录。
