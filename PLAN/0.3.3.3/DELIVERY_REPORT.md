> 最新 Studio 修正：[STUDIO_GROUP_DELETE_FIX.md](STUDIO_GROUP_DELETE_FIX.md)，组合右键删除连同嵌套内容删除，正式 EXE 已更新。

> 最新：拆除生命周期、金色线框及稀疏光点见 [GRAMOPHONE_REMOVAL_FIX.md](GRAMOPHONE_REMOVAL_FIX.md)。当前 JAR 哈希以 ARTIFACTS.json 为准。

> 最新：世界线框修正及连续实机验证见 [WORLD_RANGE_VERIFIED.md](WORLD_RANGE_VERIFIED.md)，此前范围验收结论由本报告替代。

> 最新留声机范围/BGM修正及本轮实机证据见 [GRAMOPHONE_RANGE_FIX.md](GRAMOPHONE_RANGE_FIX.md)。

> 最新游戏内追加修正及验证边界见 [DIALOGUE_RENDER_FIXES.md](DIALOGUE_RENDER_FIXES.md)。下方旧批次实机记录不代表新JAR已实机复验。

# 0.3.3.3 审计修复交付

2026-09-23 追加修正：空白画布菜单移除无对象的组合、复制和参数操作；右键空白清除旧选择，仅保留添加与粘贴节点（项目总图为添加故事）。菜单沿用同一 Fluent 样式。节点及组合框菜单保持一致。最新针对性 WPF 71/71 PASS，日志 `evidence/canvas-menu-regression.log`；首次运行 70/71，详细原因未输出，保留原日志，不计通过；单独菜单用例及整组复跑均通过。本追加修正未进行新的原生鼠标实测，之前实机截图不作为本次新菜单的证明。

2026-09-23：临时审计1的17项问题已逐项修复及针对性验证。原计划的历史结论按本次用户审计重新复核，当前以[AUDIT1_FIXES.md](AUDIT1_FIXES.md)为准。

| 路径 | 版本 | 字节 | SHA-256 |
|---|---|---:|---|
| `E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe` | 0.3.3.3 | 142434518 | `998484a1ded5d2328ee01fa5134d89d3a2a37e9f8c408fac971c3846689fd311` |
| `E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.3.jar` | 0.3.3.3 | 1700204 | `67a5b7423c87ec594568ff63e1759af4bd1287181b13a697e4f595c6c5b5d9b2` |

Studio为固定路径的自包含Windows x64 Release。SOURCE_SNAPSHOT.json记录当前92个源码/构建文件的SHA-256，基线与分支未改变，工作树未提交。用户原有Studio窗口仍使用旧进程；保存后重新打开固定EXE即可使用新版本。

本轮WPF针对性71 PASS、Core针对性14 PASS、4个Java相关探针及构建通过，另有原生菜单、选择、分组拖动、主题、对话和实际音频源验证。测试范围及候选版本边界详见审计报告；原有12个Core SKIP不计通过。

USER_ACCEPTED=NO；RELEASE_READY=NO；整体人工验收仍待完成。原来QQ/网易云/本地听感待验继续保留，系统DPI依用户要求豁免。没有提交、push、上传或创建Release。
