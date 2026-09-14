> 2026-09-14 最终集成更新：本文件保留工作包阶段记录；后续编译修复、真实交互及最终证据以 [DELIVERY_REPORT.md](DELIVERY_REPORT.md) 为准。阶段性待办不能覆盖最终记录，未实测部分也不自动计为通过。

# 0.3.3.1 WP-B 实机演示与修正

用户已授权实机演示。本次使用 PowerShell UIAutomation 读取实际控件，Win32 鼠标输入、SendKeys 键盘输入与 CopyFromScreen 截图。项目为 `.tooling/0.3.3.1/live-project` 隔离副本；未编辑用户原项目。运行的是权威 dist EXE。环境见 `evidence/live/environment.json`。

## 实机发现并修复

1. 参数空白命中识别已修正，但画布 MouseDown 仍要求标题命中，导致实际拖动不启动。移除重复的标题限定，并补充真实 WPF 路由事件回归。
2. 单节点拖动未开启布局事务，仅多选调用 BeginLayoutMove，因此实机移动后撤销菜单禁用。统一单选和多选事务入口，补充单节点 Undo/Redo 位置断言。

针对最终交互代码的 64 项画布测试全部通过，见 `evidence/live-single-drag-fix.trx`。此前 502 项全套结果属于首批预览，不能替代本次新增修复的验证。

## 已取得的真实操作证据

- Start 参数空白鼠标拖动：屏幕坐标从 (696,330) 到 (756,270)，移动 +60/-60；Ctrl+Z 精确恢复原位，Ctrl+Y 精确恢复移动后位置。见 `evidence/live/single-drag-final.json` 及 `10-undo-fix-before.png`、`11-single-drag.png`、`12-single-undo.png`、`13-single-redo.png`。截图中可见连线端点随拖动、撤销、重做保持连接；命中几何由自动测试覆盖，未做实机像素级测量。
- Start 类型下拉菜单：10 次交替点击角色交互/进入区域，逐次读取真实 SelectionPattern 验证选值，节点位置不变。见 `evidence/live/start-dropdown-10.json`。
- Objective 类型下拉菜单：10 次交替点击物品收集/实体击杀，逐次读取选值，节点位置不变。见 `evidence/live/objective-dropdown-10.json`。
- 保留最终演示窗口，已保存到隔离项目，见 `evidence/live/19-final-story.png`。

先前 `drag-result.json`、`undo-redo-result.json` 的未移动结果不能算撤销通过；`fixed-drag-undo-redo.json` 中 UndoRestored=false，RedoRestored=true 也只是无变化，不能算重做通过。这些失败证据保留；最终通过结果以 `single-drag-final.json` 为准。

## 当前产物与未完成边界

最终产物清单见 `DELIVERY_LIVE_FIX.json`，已覆盖首批预览的同路径 EXE。

- ProductVersion：0.3.3.1-wpb-preview
- 文件大小：141841646 字节
- SHA-256：3103AA2C0D53FC59788A7A4BFA9A68D1B2B3E96D80EFEF3394893E99B7A420EE

本轮仅补充 WP-B 实机演示与两处修复。四类下拉菜单各 100 次压力矩阵、全部节点类型与 DPI/主题矩阵尚未完成；没有断言闪退根因已完全排除。A22 与 WP-A/C/D/E/F 仍 OPEN。0.3.3.1 工程未完成，USER_ACCEPTED=NO，无 Release、commit 或 push。
