# Studio 折叠交互与任务界面修正

2026-10-01。本轮实现用户确认的五部分方案；未新增存档字段或网络协议。用户个人验收仍未标记通过。

## 实现

- 图片属性保留外层卡片；属性、动画使用整行无额外边框折叠标题。动画统一 +/− 工具栏、选中边框、增删排序与双视图共享选择；预览画面位于外卡片下方右侧，保持整画面一次播放、再次停止及结束恢复编辑。
- 选择选项独立折叠，默认第一项展开，新增自动展开；状态按选项 ID 保存在编辑会话。台词详细设置改用统一 220 ms CubicEase/EaseInOut 高度组件，支持反向，节点跟随正文高度变化。
- 故事固定箭头槽、标题固定起点，字号为任务的 1.1 倍并加粗；任务选择使用背景和细竖线。恢复候选悬停网格分页、标准物品提示，轮播共享时钟改为 1.5 秒且不因悬停暂停。
- 追踪图标与持有数量同行，GUI 打开时继续在前景 GUI/遮罩下绘制。保留现有单次 HUD 绘制、世界/玩家检查和状态恢复。
- 实机追加修复：折叠标题取消系统默认亮色 ToggleButton 模板；候选末页保留固定面板范围，避免鼠标因面板缩短而意外离开。Escape 可先关闭候选面板。

## 验证

- WPF 93 通过、0 失败；覆盖 UiReview0335、CanonicalGraphEditorView、LinePagesAuthoring、SessionScreenEditor0331。见 evidence/fold-ui/FoldWpf.log。
- 新增回归覆盖动画共享选中/统一删除/撤销、选项稳定 ID 折叠及运行数据不变。高度回归对展开收起连续采样，每帧断言节点高度等于基础高度加正文高度（2 DIP 容差），检查中间高度和原连线对象保持；现有反向动画回归通过。
- Java spotlessJavaApply、build、construction0335Probe 通过；自包含 win-x64 Release 发布成功。日志见 evidence/fold-ui。
- Windows 原生鼠标实测动画选中、顶部删除及撤销、拖动第二步至第一步及撤销；排序后选择边框跟随原动画。预览按下、自然结束弹起及再次点击停止已查看截图。
- 原生鼠标实测属性/动画和选项折叠；第一选项默认打开，折叠后节点与 Inspector 同步；第二选项独立展开。台词详细设置展开、收起及间隔 90 ms 再次点击反向，最终恢复收起状态。
- Minecraft 1.7.10 独立测试服/客户端：故事展开折叠、选中任务、45 候选跨页浏览、网格标准 tooltip、悬停继续轮播；聊天、背包和任务 GUI 下 HUD 仍存在，背包遮罩正常使其变暗，图标与数量同行。
- 缓存上限采用既有代码约束：16 页 / 4 MiB、4 个在途请求；本轮实机验证跨页功能，未进行缓存极限压力测试。没有声明完成所有模组组合、所有缩放比例或用户个人验收。

## 实机证据

以下均在 evidence/native-ui 中；修复过程中的失败/定位截图不是最终通过证据。

- fold-final-headers.png：深色整行标题、外层图片卡片、右下预览。
- fold-final-animation-reordered.png / fold-final-animation-reorder-undo.png：真实拖动排序、选择跟随与撤销。
- fold-animation-selected.png / fold-animation-deleted.png / fold-animation-undo.png：统一删除与撤销（样式修复前的行为证据）。
- fold-final-preview-start.png / fold-final-preview-finished.png / fold-final-preview-stopped.png：预览状态变化。
- fold-final-choice-before.png / fold-final-choice-collapsed.png / fold-final-choice-second.png：选项折叠和双视图。
- fold-final-details-open.png / fold-final-details-closed.png / fold-final-details-reversed.png：详细设置与快速反向最终状态；连续高度证据在 WPF 回归中。
- fold-tasks-collapsed.png / fold-tasks-expanded.png / fold-final-task-open.png：故事层级、固定标题位置、任务选中标记。
- fold-final-grid.png / fold-final-grid-second.png / fold-final-grid-last.png：候选网格跨页及标准 tooltip；末页面板修复后的验证。
- fold-chat-final.png / fold-inventory-hud-verified.png：聊天及背包覆盖下 HUD；fold-final-game.png：最终 JAR 的图标/数量同行。

## 正式交付

- `E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`
  - 版本：0.3.3.5；大小：142695638 字节。
  - SHA-256：`78EA0F35A9E36350D266AAE1CBDE42991C7B5FD081B026A9D46989D534BFC56F`。
- `E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.5.jar`
  - 版本：0.3.3.5（模组版本）；大小：1870571 字节。
  - SHA-256：`2724799EB40195620AC79A3B7823B00C9093F1D61FDB2F74E7D85606C4CD7C92`。
