# P10 首轮实机验收记录（2026-09-13）

后续收尾已完成；本文件保留首轮证据边界，旧待办状态以 [P10_REMAINING.md](P10_REMAINING.md) 为准。多 DPI 已由用户取消。

本记录替代 P10 旧版中的“桌面工具无法启动、无实际点击证据”结论。用户明确授权替换验收方法后，使用 Windows UI Automation、Win32 鼠标/键盘、CopyFromScreen，以及隔离 Minecraft 客户端中的验收辅助 Mod 完成实际操作。辅助 Mod 只存在于 `.tooling/0.3.3.0/p10` 的验收客户端，不进入正式 JAR。

## 验收环境与证据边界

- Studio：实际启动 `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`；设置及测试项目位于 `.tooling/0.3.3.0/p10/desktop`，未操作用户业务项目。
- Minecraft 1.7.10：隔离目录 `.tooling/0.3.3.0/p10/live-client`，新建平坦世界 `DGR0330Acceptance`，玩家 Developer；使用真实渲染、服务端任务处理、安装的 DGRS 包和音频引擎。
- 游戏辅助接口仅绑定 localhost:33061。截图来自 Minecraft framebuffer；台词推进和选项选择另用 Win32 实际点击，非仅调用业务方法。
- 音频证据为游戏引擎实际声源 playing 状态及生命周期前后状态，不代表人耳试听通过。
- 本机 GetDpiForWindow 返回 119；记录当前实际缩放，未冒称完成 100%/150%/200% 多 DPI 矩阵。窄窗受产品最小窗口尺寸限制。
- 原始路径均相对仓库根目录。`.tooling` 的临时生成工具曾修正测试数据，不作为可复现产品样例发布；以保留的最终项目、安装包、日志及操作后状态为准。

## 实机发现并修复

1. 标题、音乐、画面节点被通用 HasEditableFields 判定遗漏，真实属性面板未出现：纳入类型判定，增加实际 WPF Window 可见控件测试。
2. 属性面板空错误文本占据大块空白：仅在 Inspector 范围折叠空 TextBlock，前后截图确认。
3. Studio 标题栏和关于窗口仍显示旧版本：更新为 0.3.3.0。
4. 画面连续 DragDelta 累加造成鼠标移动 30 px、图层移动约 90 px：改为鼠标相对初始位置的绝对位移，拖动中同步边框和手柄；缩放保持左上角位置。
5. 当前 canonical 角色没有头像编辑入口：增加头像与表情编辑窗口，复用现有媒体导入与 Actor 编辑撤销历史，保存后刷新资源；旧 schema 与只读资源仍受约束。
6. 重载后资源指纹变化的拒绝异常从区域路由/呈现恢复溢出，导致服务端 tick 崩溃：在 Forge 入口捕获并保持拒绝，避免自动重置或绕过指纹校验；重复触发日志限频。保留原崩溃世界，修复后同一世界可进入并保持拒绝。
7. 节点保持选中时，保存成功但属性栏仍显示“未保存”：补齐活动编辑器状态通知，并以不重新选择节点的测试及实机保存验证。
8. 高级选项/目标前置条件的局部 CheckBox Style 遮蔽主题样式：显式继承主题 CheckBox 样式，修复深色主题的文字对比度。

Start 配置实现未改；旧独立 Trigger 清除边界继续由 Final Scope Guard 检查。

## Studio 实际操作

证据目录：`.tooling/0.3.3.0/p10/desktop/`。

| 操作/状态 | 证据与结果 |
|---|---|
| 新建项目、打开目录、文件选择 | 原生 UI 操作；项目及导入文件实际落盘 |
| MP3、WAV 导入 | 实际文件选择并调用交付的 FFmpeg；生成项目内 OGG，保留 source master；`04-music-import-mp3.png`，项目 resources/media 与 media_sources |
| Actor 默认头像及变体 | `11-actor-editor.png`、`12-actor-variant-import.png`、`13-actor-saved-reopened.png`；导入 JPEG 变体“微笑”，撤销移除、重做恢复，保存重开及 Actor JSON 确认 |
| 台词角色、头像变体、语音、文本 | `24-line-properties.png` |
| 标题、音乐及属性分组 | `03-title-fixed.png` → `06-title-polished.png`；`04-music-import-mp3.png` |
| 画面列表与预览 | `05-screen-before-polish.png` → `07-screen-polished.png`、`17-screen-final.png` |
| 拖拽修复 | `drag-before.json` → `drag-after.json`，图层边界移动 30×15 px；x=.5→.597982512843194、y=.5→.587095566971728，宽高不变；`18-fixed-drag.png` |
| 缩放、撤销、重做 | `resize-final.json`、`resize-final-undo.json`、`resize-final-redo.json`；缩小 20×10 px，宽 .7→.634678324771204、高 .8→.741936288685515；撤销精确恢复，重做精确复现；`21-resize-final.png` |
| 对话框/选项框参考开关 | `22-dialogue-off-choice-on.png`、`23-both-overlays-off.png`，及默认对话框开启图 |
| 大窗/最小窄窗 | `25-large-window-line.png`、`26-narrow-window-line.png`；当前 DPI，未覆盖跨显示器 DPI 切换 |
| 区域目标、任务说明 | `27-region-objective.png`、`31-task-description.png` |
| 保存指示器 | `28-save-indicator-fixed.png`：修改目标描述后保存，无需重选便显示已保存 |
| 奖励空/经验 | `29-empty-reward.png`、`30-xp-reward.png`；多物品+XP 组合尚未完成截图验收 |
| 执行普通/高级 | `33-negative-xp-normal.png`、`34-advanced-open.png`、`35-command-mode.png`；界面输入 -7，打开命令类型并输入测试文本；此处不宣称该命令已经在游戏执行 |
| BUFF Vanilla/MOD | `36-buff-vanilla.png`、`37-buff-mod.png`；已打开两个作者面板，MOD 为界面示例值，不代表真实 MOD 效果发放成功 |

`32-action-normal.png` 包含一次误点节点内下拉后的展开状态；普通模式以 33 为准。早期 14 等探索截图不作为对应功能通过证据。截图 33—37 中主题修复前的黑色高级选项文字为 Before，`38-theme-after.png` 为最终交付客户端的 After：文字与主题一致、控件比例正常。

## Minecraft 实际操作

截图目录：`.tooling/0.3.3.0/p10/live-client/screenshots/`；状态 JSON 在 `.tooling/0.3.3.0/p10/`。

| 场景 | 结果及证据 |
|---|---|
| 标题阻塞 | `01-title.png`、`title-live.json`；标题期间没有 Session frame；约 7 秒（1+5+1）后进入台词 |
| 画面、头像、台词 | 安装真实 DGRS 后 `03-package-scene.png`、`scene-live.json`，画面与头像完成加载；speaker 对话显示 |
| 音乐及当前语音 | `scene-live.json` 中实际 music/voice 声源均 playing=true |
| 死亡→复活 | `death-respawn-live.json`、`04-death.png`：死亡所有声源停止；复活恢复同一 line epoch，音乐 playing=true，voice=false |
| 断线→重连 | `disconnected-live.json`、`reconnected-live.json`：断线全部停止，重连恢复音乐，不重播当前台词 |
| 真实点击推进 | `native-click-line.json`、`native-click-choice.json`、`native-click-end.json`；speaker→旁白→选项→结束；`05-native-click-narration.png`、`06-choice.png`；结束清除画面和音频 |
| 任务说明/隐藏前置 | `07-task-hidden-prerequisite.png` 初始只有第一目标，`08-task-phase-reward.png` 第一目标完成后第二目标才显示 |
| 阶段经验与结算 | `task-saved-evidence.json`：两个目标 COMPLETED，任务 SETTLED、结果 done；reward latch=1；两次独立激活的两个交易 receipt，各 7 XP，总计 14，与玩家 XpTotal=14 一致；`10-task-completed-final.png` 不再列出进行中的该任务 |
| 重载拒绝不崩溃 | 原崩溃报告 crash-2026-09-13_04.19.42-server.txt；修复后 `rejected-cursor-survives.json`，同一被拒绝世界仍可进入，未绕过指纹拒绝 |

原始未安装包的本地资源不提供网络媒体；早期 02 截图缺少图片不算通过。测试 Task 聚合结果端口最初误设 Logic，已改为 Story Flow 后导出及游戏结算；此为 fixture 修正，未更改产品聚合边界。

## 尚未关闭的验收项

- Studio 多物品+XP 奖励组合、图层上下排序完整操作链。
- 标题并发排队及标题阶段死亡/重连的完整实机时序矩阵（已有自动探针，不能冒称全部实机通过）。
- 完整多 DPI/跨显示器矩阵及逐项 Before/After 视觉审核。
- 音频人耳试听、淡入淡出主观效果；第三方 MOD BUFF 实际注册项的游戏内使用。
- 用户最终视觉审核。

P10_DESKTOP_VERIFIED=PARTIAL_WITH_REAL_ACTIONS

USER_ACCEPTED=NO

RELEASE_READY=NO

此前 Computer Use 初始化失败已通过用户授权的替代方法消除，不再列为施工阻塞。CAS 责任包保持 ACTIVE；没有宣称整个版本 100% 完工，也没有 Git 提交、推送或 Release。
