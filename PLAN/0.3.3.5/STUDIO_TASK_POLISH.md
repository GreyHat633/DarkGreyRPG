# Studio 编辑区与任务故事分组交付记录

日期：2026-10-01。对应用户确认的《Studio 编辑区精简与任务面板分组调整》。本记录取代前轮关于预览按钮、停用条件连线和任务列表展示的描述。未提交或推送 Git；USER_ACCEPTED=NO。

## 本次实现

- 图片属性内分为“属性”和“动画”两张子卡片，默认前者展开、后者收起，沿用 220 ms 折叠组件。会话内共享展开状态；“添加动画”与标题同行且自动展开，没有数量后缀。
- 预览为一次性 ToggleButton，停止或结束恢复编辑图层；切换上下文、图片、参数及卸载控件时停止计时。“返回编辑”和常驻来源下拉框已移除。平滑衔接沿反向 Flow 寻找最近画面、跳过普通节点并防环；多来源在预览时选择。Minecraft 动画时序不变。
- 停用前置条件会在同一撤销事务中移除入线，隐藏端口，保留设置。重新开启需接线。加载时清理明确停用的遗留连线；没有启用字段的旧数据仍按连线推断。启用未接线继续报配置问题。修复了画布缓存端口及撤销时先恢复数据、后出现端口导致线条不显示的两个问题。
- 物品轮播与 HUD 共享候选组件和 1.2 秒时钟；悬停持续轮播，标准物品提示跟随当前图标。移除候选弹窗、滚轮拦截及自加 tooltip 文案。预取下一批并在等待时保留图标。
- 进行中和已完成任务按稳定故事归属分组，默认折叠；任务/目标可点击，保留会话展开、选择及滚动状态。删去分页及目标前后按钮；列表与详情滚动接近末尾请求后续页，提供加载和重试反馈。关闭重开保留已加载历史页，数据版本或连接变化时失效。
- 服务端投影及历史保存故事归属、名称、故事包标识；同名故事附加包标识区分。旧记录从旧身份字段恢复，无法恢复的归入“未归属故事”；后加载的故事名称会回写历史快照。任务身份、激活和提交语义未变。

## 自动验证

日志保存在 `evidence/polish/`：

| 验证 | 最终结果 |
|---|---|
| Construction0335Tests | 6 通过，0 失败；包含断线撤销、明确停用遗留连线清理及未接线校验 |
| UiReview0335 / SessionScreenEditor0331 / CanonicalGraphEditorView | 82 通过，0 失败；包含双视图卡片状态、一次预览、真实画布线条的断开/撤销/重做 |
| Gradle build + construction0335Probe | 通过；含 Checkstyle、故事分组/同名区分/旧身份恢复与已有动画语义探针 |
| 自包含 Windows x64 Release publish | 成功，正式路径已替换 |

以上为本轮相关回归，不能与旧报告测试数相加为一次全量执行。测试项目仍有既存 analyzer 建议，不影响通过结果。

## 原生鼠标验证证据

仅操作 `.tooling/0335` 的隔离项目、客户端和服务器，没有关闭用户的“测试项目”窗口。

- `evidence/native-ui/polish-cards-collapsed.png`、`polish-animation-expanded.png`：属性/动画子卡片、标题同行和两视图共享状态。
- `polish-preview-playing.png`、`polish-preview-finished.png`：实际点击预览，按钮保持按下；等待完成后按钮弹起、编辑手柄和图层恢复。
- `polish-final-all-conditions-hidden.png`、`polish-final-wire-undo.png`、`polish-final-wire-redo.png`、`polish-final-wire-restored.png`：最终 EXE 实际取消勾选、Ctrl+Z、Ctrl+Y；端口与黄线同步隐藏/恢复。`polish-condition-*` 是排查阶段，不能作最终通过证据。
- `polish-last-choice-ready.png`：保存关闭后重新启动正式 EXE，再打开选项，停用状态与端口隐藏保留。
- `polish-hover-a.png`、`polish-hover-b.png`：鼠标停在同一图标，候选物品及其标准提示继续改变，无候选弹窗。
- `polish-hud-a.png`、`polish-hud-b.png`：右侧追踪图标继续轮播。
- `polish-final-game-task.png`：首次打开显示折叠故事；`polish-final-history-expand.png`、`polish-final-history-select.png`、`polish-final-history-reopen.png`：实际展开、选择、关闭重开，选择和详情恢复，无旧分页按钮。

本轮实机覆盖一个真实故事、多个历史任务和 45 个候选物品。多故事同名及旧身份回退另有自动探针。尚未逐项做原生鼠标验证的组合：多来源平滑选择、长历史/目标列表跨多页、故意断网后的失败重试、故事包卸载后的完整 UI 流程及第三方模组自定义 tooltip 的全部组合。因此这里记录实现与已取得的证据，不宣称完整人工验收通过。

## 正式文件

| 文件 | 版本 | 大小（字节） | SHA-256 |
|---|---|---:|---|
| `E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe` | ProductVersion 0.3.3.5 | 142687446 | 3A6C0349542A230692858AEB9F37CA383725735EA568754079EBA44D0A9ACB5D |
| `E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.5.jar` | 0.3.3.5 | 1870075 | D62C5E8A6428EA9550D107C40A07C3C53FE2BC0AE8010AFE32AA55F3AA034405 |

机器可读校验：`evidence/polish/artifacts.json`。用户仍打开的旧 Studio 进程继续使用旧映像，保存并重新打开正式路径后才会加载本轮版本。被该进程占用的旧映像暂存于 `.tooling/0335/StudioPreviousRunning-Polish.exe`，没有强行终止用户程序或永久删除它。
