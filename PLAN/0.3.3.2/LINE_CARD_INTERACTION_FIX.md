# 台词卡片动画与选择修复（2026-09-17）

- 分句主体采用 220ms CubicEase 高度展开/收起；裁剪内容而非缩放文字。可从当前高度反向动画，结束后恢复自然高度；尊重 Windows 客户区动画开关。
- 卡片边框/内边距优先接收选择事件，阻止画布将其解释为节点拖动。标题仍可折叠，拖拽把手仍只负责排序。
- 点击外部节点、画布空白、编辑器空白取消选择和锚点。两处编辑器按 host/node 识别为同一选择上下文；顶部 +/− 保留选中项供操作。
- 选中视觉继续仅细蓝边框，不改变卡片及正文底色。

验证：LinePagesAuthoringTests 7 PASS（含边框 routed event、编辑器及外部空白清除、动画中途反向和最终高度）。
Windows 原生鼠标：interaction-06-edge 卡片边缘选择、正文仍展开；interaction-07-blank 画布空白清除；interaction-09-other-node 切换节点清除。后两项 − 按钮 disabled。
真实 EXE UIA 动画采样：112,107,84,69,50,43,42 像素，约 195ms 到稳定终态。见 evidence/line-animation-native.json。

权威交付：E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe
ProductVersion: 0.3.3.2
Length: 142134998 bytes
SHA-256: 58D04E97194A591191611CA60FCB50B7242635D165D185EF620BE61A844E5AA1

本轮没有修改 Mod。没有提交或推送；不代表整份 0.3.3.2 计划最终验收。
