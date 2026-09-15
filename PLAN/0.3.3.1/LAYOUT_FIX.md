# 0.3.3.1 节点属性空白修复

2026-09-15。用户指出开始、终止、标题节点有大块无意义空白。这是共用属性模板缺陷，并非三个独立节点的高度配置。

## 原因与修复

- 多个不属于当前节点的空校验 TextBlock 始终参与 StackPanel 测量，每个空提示仍占一行，累计形成字段前后的大块空白。
- 节点内联属性、右侧 Inspector、任务目标编辑器中的空文本现在折叠；有文本时恢复显示。嵌套奖励模板、启动条件模板的错误提示显式引用同一规则。
- 画面预览的 Viewbox 原来固定高度 190，窄节点中预览缩小后仍保留空白。现在按实际宽高比测量，上限仍为 190。下方复选项改为自动换行，避免窄节点横向裁切。
- 未改变图数据、端口、执行逻辑或保存格式。

## 验证与范围

- WPF Release 全套：526/526 通过，结果 `.tooling/0.3.3.1/layout-tests/layout.trx`。
- 新增测试遍历注册表中所有非兼容、非 Project 节点定义，以默认数据实际测量内联控件，要求空绑定文字不占高度。
- 额外验证终止节点第一个字段距属性控件顶部不超过 20 DIP；错误文字出现、清空后高度相应增长、恢复。
- 验证 210 DIP 窄节点的预览高度按 320:180 缩放，复选项不越过控件左右边界。
- 实机使用交付路径 EXE，在 `.tooling/0.3.3.1/live-project` 隔离项目以画布 100% 缩放检查。开始条件改为单个角色交互，折叠、重新展开参数，检查节点收缩。部分测试节点为避免遮挡进行了移动，不修改用户项目。
- [开始（最终客户端）](evidence/live/layout-start-final.png)、[终止](evidence/live/layout-terminate-100.png)、[终止折叠](evidence/live/layout-terminate-collapsed.png)、[标题](evidence/live/layout-title-100.png)、[开始折叠](evidence/live/layout-start-collapsed.png)、[开始重新展开](evidence/live/layout-start-reexpanded.png)。
- [台词和选择](evidence/live/layout-line-choice-100.png)、[目标和奖励](evidence/live/layout-objective-reward-100.png)、[画面最终上半部](evidence/live/layout-screen-final-top.png)、[画面最终下半部](evidence/live/layout-screen-final-bottom.png)。
- 公共文本规则的实机图先于最后的画面 Viewbox 调整；开始和画面最终图重新从最终客户端拍摄。大画面节点分两次平移查看，不能把视口外区域当作已在单张截图检查。
- 本记录证明上述空白修复和相应回归，不宣称所有数据组合、所有分辨率均已人工验收。USER_ACCEPTED=NO。

## 最新交付

本信息取代 DELIVERY_REPORT.md、READABILITY_FIX.md 中旧 EXE 哈希。

- `E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`
- Self-contained Windows x64 Release；ProductVersion `0.3.3.1`
- 大小 `141925078` 字节
- SHA-256 `511E1AEF3722D6756E915758D4039C00B2D30F20556DD032B5AC3EDAA071D11D`
- candidate 与权威交付文件哈希一致。首次关闭遇到隔离项目的保存全部对话框，保存后正常退出，再完成最终文件复制与启动验证。
