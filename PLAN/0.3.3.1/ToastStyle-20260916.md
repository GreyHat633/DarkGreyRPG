# 任务提示样式和入场动画

- 提示从屏幕右边界外向左滑入，280 ms 三次缓出，按单调时间计算；同一任务合并事件不重启动画。停靠位置、四秒到期、最多三张与去重规则不变。
- 与任务窗口共用 WINDOW_PANEL、BORDER、SELECTED_BORDER、TEXT、SECONDARY 配色，使用灰黑底、灰色细边框和深灰内容区；删除蓝色标签。
- taskNotificationsPlanProbe 通过：入场起点、单调向左、无越界回弹、停靠终点、合并不重启，以及既有消息去重、三张上限、过期清理。Spotless、assemble 通过。
- 更新 dist/darkgrey_rpg-0.3.3.1.jar；大小与 SHA-256 见 evidence/ToastStyleArtifact.json。本次没有重新运行游戏实机，不将自动化动画计算检查描述为视觉验收。
