# 台词折叠连线闪烁修复

日期：2026-09-18。

原因：台词伸缩动画触发节点 SizeChanged，布局刷新逐帧调用 RedrawConnections，删除并重建全部连线 Path 和透明点击区域。

修复：布局刷新复用现有连线和点击区域，只在端点坐标变化时同步更新几何。端点未移动时连几何对象也保持不变；正在拖拽的连线由手势逻辑继续管理。保留台词伸缩动画。

验证：

- CanonicalGraphEditorViewTests 与 LinePagesAuthoringTests：70 通过，0 失败。新增实际 WPF Window 动画回归，连接三个台词节点，连续收起、展开、收起，逐帧检查全部连线、点击区域及固定端点的几何对象保持稳定。现有拖线、重连等测试同时通过。
- Windows 原生 UIAutomation 操作已交付 EXE，使用隔离 Native0332Acceptance 项目的已连接开始/台词节点，连续点击四次折叠按钮。GDI 连线中段屏幕采样 96 帧，像素变化 0 帧；卡片高度从 112 经过中间高度到 42，证实动画仍在执行。采样区域排除节点边框和焦点样式变化；这属于局部连线验证，不是整个图的逐像素验收。
- 证据：`evidence/wire-collapse.trx`、`evidence/wire-collapse-native.json`、`evidence/native-ui/wire-after.png`。
- 定向 `git diff --check` 通过。保留工作区已有改动。

交付文件：`E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`

- ProductVersion：0.3.3.2
- 文件大小：142134998 bytes
- SHA-256：A4063688302945D23CFF64BC5CA00E6E3FF4703BC0332938E7A4E9D5E4BE03A4

已替换固定交付路径，保留原先打开的用户窗口；该旧进程须保存项目并重新打开才能加载新客户端。此记录不代表用户已验收整个 0.3.3.2。
