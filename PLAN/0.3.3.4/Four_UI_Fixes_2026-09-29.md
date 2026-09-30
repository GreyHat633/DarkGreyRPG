# 0.3.3.4 四项 UI 回归修正

状态：代码与产物已更新；USER_ACCEPTED=NO；RELEASE_READY=NO。未提交或推送。

## 改动

- 重复配置共用一个顶部固定的动画容器，模式切换从顶部向下展开，保留 220 ms CubicEase。避免上下两个容器同时收展造成向上移动。
- 修正 DynamicContentEditor 预览误继承空 TextBlock 隐藏样式的问题，设置输入区最低高度。主副标题输入恢复；其他节点共享输入控件一并修复，不恢复非台词节点的动态内容按钮。
- 物品格短数量增加深色底衬、白色阴影文字。长数量仍在格旁完整展示。
- 删除任务详情重复的任务名；当前目标、奖励等分节使用全角冒号、加粗和主题强调色。

## 验证

- WPF 回归：623 通过，1 跳过，0 失败；不包含 GroupFrame0333/GroupScale0333 专项。新增初始布局矩阵检查各类可编辑节点的叙事输入区，以及重复详情切换时顶部固定和展开高度检查。日志见 evidence/four-ui-wpf-full.log。
- Java assemble、canonicalTaskJournalProjectionProbe、task0331RuntimeProbe 成功，见 evidence/four-ui-java.log。
- Windows 原生 UIA/Win32 在 FourUiProject 隔离工程中操作：节点主副标题实际输入中文、Inspector 同步、保存文件内容确认；见 evidence/native-ui/four-title-both-edited.png。重复模式冷却页面见 four-repeat-cooldown.png；动画时序由自动化测试验证。
- 隔离服务器与 UiFix0334 玩家实机：任务详情无重复标题，分节层级、小数量 10、扣除及未绑定占位正常展示；原生悬停显示物品和数量。见 evidence/native-ui/four-task-clean.png、four-item-hover.png。
- 本轮未穷举全部主题、缩放和亮色图标组合，不宣称全部视觉验收通过。
- 隔离环境旧任务快照与修改奖励后的测试包语义不一致，曾导致旧玩家日志投影异常及拒绝新任务。保留原快照为 darkgrey_rpg_canonical_tasks.dat.pre-four-ui，重建隔离测试任务后完成上述验证。该异常另列待调查，不宣称本次已修复；正式项目和存档未改动。
- 此前 C10 长文本／语音／历史组合、C12 提交候选剩余边界验证继续保留，不能以本次修正关闭。

## 交付

- EXE：`dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`，自包含 Windows x64 Release，ProductVersion 0.3.3.4，142501590 字节。
  SHA-256：`BFF001E0F3AC5DC9DB9D8FC84232353DDC875ADEF74043DC8AC3F943A7D9FBE9`
- JAR：`dist/darkgrey_rpg-0.3.3.4.jar`，1776402 字节。
  SHA-256：`C2C7E04D7CFA99B1003ACA786DF53D849F77CC8F8A7041E3069DE46B2C31B649`
- 校验数据同步到 evidence/Delivery.json。未关闭用户原先运行的 Studio；重新启动后使用更新的 EXE。
