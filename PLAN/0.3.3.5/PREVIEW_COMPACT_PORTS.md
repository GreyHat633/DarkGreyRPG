# 预览按钮与选择端口布局调整

2026-10-01。ProductVersion 0.3.3.5。实现及代理验证完成；USER_ACCEPTED=NO，未发布 GitHub。

## 结果

- 共用画面编辑器中，「预览」移至图片列表下方工具栏左侧，「▲」「▼」在右侧；原卡片下方按钮及占位删除。节点与 Inspector 同一组件，整画面一次播放、提前停止、自动恢复编辑保持原语义。
- 选择端口改为独立左右两列：左侧流程输入之后只列启用的条件输入，右侧从首行列全部选项输出。条件和自身输出不再强制同高。
- 两列等宽、中间8 DIP；固定24 DIP行高、20 DIP端口控制区，名称单行省略和完整 tooltip。节点宽232 DIP和两侧锚点横向位置稳定；旧逻辑输出兼容区保留。
- 不修改存档、端口ID、网络或Java源码；本轮配套JAR沿用上一轮核验文件。

## 自动回归

166 项 WPF 通过，0失败、0跳过，25秒。涵盖选择端口与选项顺序、启停及撤销重做、0/部分/全部条件输入、长中文/英文标签、画面编辑、预览、已有连线及工作区。原“对应条件与输出同高”断言替换为两列独立排列及24 DIP间距断言。

[回归日志](evidence/preview-compact/PreviewCompactWpf.log)、[自包含发布日志](evidence/preview-compact/PreviewCompactPublish.log)、[核验记录](evidence/preview-compact/Verified.json)。本轮没有Java改动，未重复运行Java构建。

## 真实鼠标与状态读回

正式路径EXE，隔离IntegratedProject和独立settings；Win32鼠标/键盘、UIA状态读回，未操作用户项目。

- [预览位置](evidence/native-ui/compact-preview-location.png)：节点与Inspector左侧「预览」、右侧排序箭头。
- [Inspector播放](evidence/native-ui/compact-preview-running.png)：真实鼠标点击，UIA ToggleState=On；画面在动画中。再次点击停止，[恢复编辑](evidence/native-ui/compact-preview-stopped.png)。
- [节点播放](evidence/native-ui/compact-node-running.png)：点击节点工具栏预览，整画面动画运行。
- [自动结束](evidence/native-ui/compact-node-complete.png)：12秒序列播放完成，三处预览控制ToggleState均Off，布局与编辑手柄恢复。测试夹具存在节点重叠，临时拖走遮挡节点以操作；之后撤销该位置变化。
- [紧凑端口](evidence/native-ui/compact-choice.png)：输出首行与流程输入同高；已有第二选项条件输入紧接流程输入。
- [长名称](evidence/native-ui/compact-long-enabled.png)：实际输入长中文加英文，名称省略且节点与端点不横移。该截图的条件未启用，文件名不代表启用验证。
- [条件启用](evidence/native-ui/compact-condition-enabled.png)：开启第一选项条件，已有第二条件下移一行并保持原连接；[撤销](evidence/native-ui/compact-enable-undo.png)、[重做](evidence/native-ui/compact-enable-redo.png)均读回核验。
- [标题手柄排序](evidence/native-ui/compact-option-reorder.png)：第三选项移至第一，右列输出随顺序变化，条件列仍紧凑；原线跟随相同端口。已检查撤销和重做。
- [保存重开](evidence/native-ui/compact-reopened.png)：Ctrl+S、关闭隔离窗口、重启正式EXE，进入同一会话；新顺序和原有条件连线保留。

无条件/全部条件及旧逻辑输出由自动回归覆盖；本轮实机为部分条件布局，未把截图检查记成全量人工验收。

## 正式文件

Studio：`E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`

- ProductVersion：`0.3.3.5`
- 大小：`142699734`字节
- SHA-256：`388106E49C7D2524F01AB513E4B3C39FA5090D595A226A199960D462FE8F11B4`
- 正式EXE与本轮自包含Windows x64 Release候选哈希一致。

配套JAR：`E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.5.jar`

- 大小：`1874444`字节
- SHA-256：`F211F39077D5FDE15FECF588D0B1A63D47B79D7E084907D7D27B64748CF71D38`
- 沿用上一轮，无Java变化。
