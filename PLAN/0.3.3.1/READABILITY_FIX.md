# 0.3.3.1 深色主题文字修复

2026-09-15。用户指出终止节点 Inspector 的“介绍说明”和输入提示为黑字，深色背景不可读。此前截图检查漏掉了这个问题。

## 修改

- 两处帮助 Expander 显式绑定主题主文字颜色。
- 全部 TextInputWatermark 提示持续绑定所属输入框 Foreground，不再保存初始化时的黑色，也不再叠加 0.7 透明度。修复适用于所有调用该提示行为的输入框。
- 项目图谱、任务目标、音频、立绘和会话画面编辑器显式使用主题文字资源；媒体错误文字使用主题错误色。
- 浅色背景和强调色背景继续使用相应主题对比色。

## 验证

- Release WPF 全套 523/523 通过。新增回归覆盖同一提示实例在文字资源更新前后切换颜色，且不改变输入值。原始结果：`.tooling/0.3.3.1/readability-tests/readability.trx`。
- 实际启动权威交付 EXE，在隔离测试项目通过 UIA/鼠标操作选中节点、展开帮助、切换 Dark → Light → Dark，并逐张查看截图。
- [项目图谱](evidence/live/readability-home-dark.png)、[终止节点](evidence/live/readability-terminate-dark.png)、[浅色](evidence/live/readability-terminate-light.png)、[切回深色](evidence/live/readability-terminate-dark-return.png)。
- [立绘](evidence/live/readability-actor-dark.png)、[音频](evidence/live/readability-audio-dark.png)、[画面](evidence/live/readability-screen-dark.png)、[画面下方字段](evidence/live/readability-screen-fields-dark.png)、[任务目标](evidence/live/readability-objective-dark.png)。
- 上述实机页面的深色背景文字可读。图画布部分使用适应缩放，截图中的 Inspector 为正常字号；未把缩小图节点视为逐字验收证据。
- USER_ACCEPTED=NO；这是修复验证记录，不代替用户验收。

## 最新交付（替代 DELIVERY_REPORT 中旧 EXE 哈希）

- 路径：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`
- Self-contained Windows x64 Release，ProductVersion：`0.3.3.1`
- 文件大小：`141925078` 字节
- SHA-256：`5D58FFBA1C9B0C9793F0F6A74998DDAB60D505C2B169ACBD8CF8CFA21EAB9A88`
- candidate 与交付目录 EXE 哈希一致；Java JAR 未修改。
