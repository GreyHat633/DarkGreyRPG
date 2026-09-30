# 0.3.3.4 NPC 名称与任务按钮修正

本轮仅处理游戏 GUI 的两项反馈，保留媒体修复、任务规则及已有 Studio 交付。USER_ACCEPTED=NO，RELEASE_READY=NO；未提交或推送。

## 修改

- NPC 名称在头像框对应的横向范围内居中，使用主题强调色和粗体；头像未加载时也保持相同位置。分割线仍覆盖原来的完整宽度。
- 长名字按头像列宽裁切，悬停显示完整名称；没有头像时保留左侧名字区域，匿名台词不绘制名字。
- 任务底部使用现有 GuiRpgButton：背景、边框、悬停与选中反馈。“查看已完成”改为“已完成”，切入已完成列表后按钮显示“进行中”。
- “追踪／取消追踪”随追踪状态更新，并保留计数和最多三项限制。无可选任务时禁用，已完成列表中隐藏。按钮位置随任务窗口移动和缩放更新；内容底部预留按钮空间。

## 验证

离线构建、对话呈现及任务布局探针通过：`assemble`、`canonicalSessionClientModelProbe`、`speakerTaskLayoutProbe`。日志见 `evidence/speaker-buttons-build.log`。本轮五个源文件的 `git diff --check` 通过。

Windows 原生 Win32 操作隔离客户端／服务器，使用新建 `speaker-buttons-world`、测试角色 Speaker0334 和独立导出包。头像为合成测试图片，没有修改用户正式故事包、世界或存档。

| 检查 | 实际结果 | native-ui 证据 |
| --- | --- | --- |
| 名称与头像居中，分割线完整 | 通过 | speaker-dialogue-short.png |
| 长名字完整提示 | 原生移动鼠标后显示全名 | speaker-dialogue-long-tooltip.png |
| 无头像与匿名台词 | 布局正常 | speaker-dialogue-no-portrait.png / speaker-dialogue-anonymous.png |
| 蔚蓝、灰黑、浅白主题 | 名字位置、粗体及主题颜色正常 | speaker-dialogue-short.png / speaker-dialogue-dark.png / speaker-dialogue-white.png |
| 空列表禁用追踪 | 通过 | speaker-task-empty.png |
| 追踪与取消追踪 | 实际点击，0/3 与 1/3 往返更新 | speaker-task-untracked.png / speaker-task-tracked.png |
| 已完成与进行中切换 | 实际点击，列表及按钮文字变化；完成记录可见 | speaker-task-completed-view.png / speaker-task-completed-result.png |
| 缩小任务窗口 | 按钮与正文区域分离；新位置实际点击有效 | speaker-task-compact-dark.png / speaker-task-compact-untracked.png / speaker-task-compact-completed.png |

以上截图位于 `evidence/native-ui/`。实机 GUI 缩放为 2，任务窗口覆盖普通尺寸及接近最小尺寸；不将这些结果扩大为所有分辨率、GUI 缩放和字体组合均已验收。

测试包首次启动因新增角色未加入资源声明被拒绝；补齐隔离包声明并重启后完成验证。没有为通过测试放宽生产资源校验。测试结束后停止隔离进程，恢复原测试参数、包、配置和 MOD，八项恢复哈希一致，见 `evidence/speaker-buttons-restore.json`。

## 交付

- 权威 JAR：`E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.4.jar`
- mcmod.info 版本：`0.3.3.4`
- 大小：`1,788,793` 字节
- SHA-256：`C2B2B230CFB8D9EDB133B7EEA06EAA041F4762AAA36C8AFC4DD029F375289F13`
- 与实机使用的构建一致。Studio EXE 本轮没有变更，现有交付信息保留在 `evidence/Delivery.json`。

原 C10 长文本／语音／历史组合、C12 剩余候选边界及媒体公网／多人压力和严格帧分布等待验项保持开放，本轮 UI 修正不替代这些验证。
