# 选择端口对齐与任务追踪卡片调整

2026-10-01；版本 0.3.3.5。实现与代理验证记录，不代表用户验收。未发布 GitHub。

## 实现

选择节点首行仅流程输入，各选项共享左右两列的一行；未启用条件左侧留空。条件标签移除前缀，端口 ID 不变。文本不换行、超宽省略，悬停显示完整名称。圆形和菱形均使用固定命中区中心作为连线锚点，消除图形描边引起的小数像素偏差。

HUD 取消单一目标过滤，读取所追踪任务的全部生效目标。按稳定故事归属分组，故事外卡片、任务内卡片、目标条目；像素书本/旗帜、圆点区分层级。目标和数量使用正文色。外卡片 alpha=235/255，内卡片不透明。宽度不超过 min(200,屏宽/4)，高度不超过屏高/3；先保留能容纳任务的标题及一个完整目标，再填充其余目标，剩余数量明确显示。

未修改任务运行、互斥规则、存档格式和网络协议。追踪仍是任务级。

## 自动验证

- WPF：105 通过，0 失败，0 跳过。覆盖选择端口布局、禁用/撤销恢复、端口锚点及相关编辑器回归。
- Java：离线 build、Checkstyle、canonicalTaskRuntimeProbe、construction0335Probe 通过。
- 新增四种宝藏并行收集测试：每次从独立初态完成其中一种，检查该分支后续生效、其余三目标继续生效、任务保持活动；四种分别验证。
- 自包含 Windows x64 Release publish 成功。
- 初次回归暴露旧布局 margin 断言及圆形/菱形锚点小数差异；调整断言和锚点后上列最终运行通过。未将初始失败计作通过。

日志：[WPF](evidence/ports-hud/PortsWpf.log)、[Java](evidence/ports-hud/PortsJava.log)、[发布](evidence/ports-hud/PortsPublish.log)。

## 真实界面操作与截图

使用隔离项目、隔离 Minecraft 客户端/服务器及原生 Win32 鼠标键盘，运行正式路径产物。

- [选择端口](evidence/native-ui/ports-aligned.png)：流程输入独立首行，第二选项条件输入与自己的输出同高；标签无“条件”前缀。
- [浅白 150%](evidence/native-ui/ports-hud-white150.png)：收集任务图标和数量同行，正文色清楚。
- [灰黑 100%](evidence/native-ui/ports-dark100-three.png)：同一任务三个目标同时显示在一个任务卡片内。
- [蔚蓝 125%](evidence/native-ui/ports-azure125.png)：两个完整目标及“另有 1 个目标”。
- [小窗口任务溢出](evidence/native-ui/ports-two-tasks.png)：原生点击另一任务并开启追踪后，“另有 1 个任务”，未裁切第二任务。
- [同故事两个任务](evidence/native-ui/ports-two-max.png)：原生最大化客户端后，一张故事卡片内有两张任务子卡片；第二任务显示两个目标及剩余一项目标提示。截图卡片约 396×318 物理像素，GUI scale=2，对应约198×159逻辑像素，符合200宽与约167高限制。
- [前景遮罩](evidence/native-ui/ports-journal-now.png)：任务 GUI 打开时 HUD 保留并位于遮罩后方。

## 验证边界

本轮未完成全部主题×文字缩放笛卡尔组合、同名多故事及三任务的真实鼠标矩阵；未逐项实机重跑中英文长短名称、排序、保存重开和全部撤销重做。多故事/旧历史分组有 Java 回归；四宝藏及独立后续有运行层回归，未在实机逐种拾取演示。以上不记为实机通过。

## 正式产物

- Studio：`E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`
  - ProductVersion：`0.3.3.5`；142699734 字节
  - SHA-256：`1F45A67992CF4D6A03CC422D186168B9504D0075DCE720A9C09AC96290835202`
- JAR：`E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.5.jar`
  - 1874444 字节
  - SHA-256：`F211F39077D5FDE15FECF588D0B1A63D47B79D7E084907D7D27B64748CF71D38`

保留用户原 Studio 进程；重新启动正式 EXE 才会使用新版代码。旧在用文件保留于 `.tooling/0335/StudioBeforePorts.exe`。仅隔离 Minecraft 测试目录同步 JAR，未替换用户其他整合包。
