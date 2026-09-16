# 菜单、预览重入及全局台词速度

- 画布右键为“复制 / 粘贴”，各含“节点 / 参数”；Inspector 保留参数操作并使用相同二级分组。“添加节点”改为“添加”。快捷键和剪贴板语义不变。
- 标题选项改为“播放完再继续流程”，等待行为不变。
- 修复 SessionScreenEditor 在 Loaded 前完成解码时丢弃 Bitmap、留下永久 loading 标记的问题。有效节点/项目的结果保存在缓存中，挂载后绘制；UI 缓存更新明确切回 Dispatcher。旧节点/项目的异步结果仍被身份检查拦截。
- 台词新增布尔属性 custom_text_speed，缺省 false；旧文件即使包含 text_speed，也默认跟随玩家全局速度。Studio 的“自定义显示速度”勾选后显示 0–120 字/秒滑块和数字框，关闭时保留草稿值但不使用它。节点内和 Inspector 同步并支持撤销。
- 游戏 P 键打开非暂停的设置窗口，复用任务窗口配色、拖动和四角缩放机制。共享角标补齐三角形六点。当前仅含台词显示速度，默认 30，0 为立即显示；窗口布局和速度保存到 DarkGreyRPG/Config/darkgrey-rpg-windows.properties。输入框/其他游戏 GUI 打开时不抢按键。
- 服务端在会话帧中用 textSpeed=-1 表示跟随本地设置；自定义速度仍为 0–120。解析器、网络校验、客户端复制及显示链路保持该标记。实际速度在进入一句台词时确定。

## 验证

- WPF 30 项：解码早于 Loaded、控件销毁重建、异步导入身份保护、自定义开关同步与撤销、剪贴板等均通过。
- 菜单/图编辑器 70 项：首轮 68 通过，2 项旧测试仍断言 6 个顶层条目；按新要求更新为 4 个条目并检查二级菜单内容后，两项复测通过。
- Java 会话客户端、网络编解码、会话运行时、窗口与设置持久化共 4 个 probe 通过。覆盖全局值、0/120 覆盖值、-1 网络往返及速度落盘。最终 assemble / Spotless 通过。
- 原生 Windows UI：独立 ExperienceProject 打开会话、退出再进入，列表缩略图和画面预览正常；右键显示新的复制/粘贴分组。证据见 evidence/live/DialogueSettings-StudioReentered.png、DialogueSettings-CopyMenu.png。
- Minecraft 实机：独立 ExperienceClient / ExperiencePlan0916B 世界中按 P 打开，30 调到 74 字/秒，拖动右下角缩放，关闭再打开保持数值和尺寸，磁盘确认保存。证据见 DialogueSettings-PKey.png、DialogueSettings-Resized.png、DialogueSettings-Reopened.png。截图在最后补齐角标第六点之前拍摄，最后角标改动已重新构建。
- IDEA 原生输入未响应，实机改由同项目 Gradle runClient 启动，不称为 IDEA 点击启动通过；未修改用户项目/世界。未穷举全部故事包或全部 GUI 功能。

## 交付

正式 Studio：dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe，ProductVersion 0.3.3.1，133849071 bytes，SHA-256 85CB06E150BAE46146C091BE16B6506DF47F4E353760DF237E683EABFD86C943。

模组：dist/darkgrey_rpg-0.3.3.1.jar。最终大小和 SHA-256 见 evidence/DialogueSettingsArtifacts.json。保存后重启 Studio 和游戏；已有故事包未自动重导出，自定义速度配置需要重新导出包。
