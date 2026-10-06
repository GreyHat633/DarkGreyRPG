# 故事组折叠、公开输出拖动与 Task 结算修正

日期：2026-10-05。版本沿用 0.3.3.6。实现、自动回归和代理实机验证完成，权威 dist 已更新。`USER_ACCEPTED=NO`，`RELEASE_READY=NO`。本报告取代此前“至少保留一个结算”和默认结算模板的描述；历史证据保留。

## 实现

- 故事组标题整行响应折叠，包括三角、文字和空白。超过系统拖动阈值转为整组排序，释放时不折叠；右侧手柄和右键改名独立处理。继续使用现有 220ms 动画，折叠保留所选故事与 Inspector。
- 画布在启动节点移动前识别排序手柄和子元素。Session、Task 的节点参数与 Inspector 共用处理，拖动输出不会移动节点。
- 排序预览淡化原行，显示输出名称卡片与有高度的落点区域，标出某项之前／之后；Task Flow 显示目标优先级。只在有效放下后提交一次历史操作。Esc、离开列表、视图卸载或失去活动窗口取消并清理。Flow 和 Logic 不能互相拖入，无上下箭头。取消依据实时屏幕指针，避免 WPF 子控件切换时的旧 DragLeave 坐标误取消有效拖动；连续鼠标移动与移出后返回均已复测。
- 共用组件在卡片外显示“公开输出”，下方仍为分别折叠的“流程输出”“逻辑输出”。空类别隐藏，两类都为空时标题也隐藏；节点内、Inspector、Project Story Graph 一致。
- 结算为非必需、非唯一节点。移除创建菜单硬编码、最后结算保护以及两端最低数量校验。新建 Task 仅通过现有目标入口创建一个默认目标，无结算和连线，目标对象为空。目标、结算都允许删至零个。
- 保留稳定 `port_id`、类别内显式顺序、旧结果槽结构拒绝和引用只读规则。Task Flow 顺序继续定义结算优先级；零结算保持 ACTIVE，不自动推进 Story。Project Story Graph 连线编辑保留。
- 完整回归发现的图片解码超时同时修正：后台解码不捕获可能失效的同步上下文，缓存更新仍明确经过编辑器 Dispatcher。

## 自动测试

| 检查 | 最终结果 | 证据 |
| --- | --- | --- |
| Core 完整套件，交付 FFmpeg | 496 通过，0 失败，0 跳过 | `evidence/TaskUIFix/task-ui-core-complete.trx` |
| WPF 完整 Release 套件 | 664 通过，0 失败，1 跳过；共 665 | `evidence/TaskUIFix/task-ui-wpf-final.trx` |
| 模板、离线包、图片修复定向复测 | 27 通过 | `evidence/TaskUIFix/task-ui-repair.trx` |
| Java 构建、Spotless、Checkstyle、测试及优先级探针 | BUILD SUCCESSFUL，PUBLIC_OUTPUT_PRIORITY_0336_PASS | `evidence/TaskUIFix/task-ui-java-verified.log` |

唯一跳过项是需要显式环境变量启用的 `FixedThreeHundredNodeWorkload` 性能基准。没有把它计为通过，也没有把历史 Java 探针全部计作本轮运行。

回归覆盖默认目标、零／一／多个结算、连续菜单创建、批量删除与撤销、删空目标、空任务保存重开和包往返、空投影、旧槽拒绝、优先级反转、打乱节点顺序、重复事件及恢复后的奖励收据。首轮完整 WPF 中的 6 处旧模板／包测试假设和 1 处图片解码失败保留在 `initial-full-wpf-failures.log`，修正后重新执行完整套件；首轮 Core 的媒体工具跳过也已通过配置随附 FFmpeg 全部复测。

## 代理原生界面与运行验收

通过 Windows UI Automation 与 Win32 真实鼠标、键盘操作隔离项目；最终候选与 dist 的程序文件一致。

- Session 未选中节点内排序成功；Task 节点内及 Inspector 排序成功，检查 89% 缩放、较窄窗口与深浅主题预览。落点与实际顺序一致。缩放拖动后一次撤销、重做的数据分别与操作前、操作后完全相同；节点坐标、端口身份、名称和原 Story 连线目标未变。见 `native-assertions.json`、`inline-session-preview.png`、`zoom-drag-after.png`、`narrow-inspector-preview.png`、`light-drag-preview.png`。
- Esc、拖出列表后重新进入、列表外释放、Flow 拖向 Logic 均取消；无排序和节点位置写入，无残留预览。普通节点移动保持可用，撤销后节点坐标精确恢复。最终补测见 `strict-cancel-result.json`、`final-drag-result.json`、`strict-cancel-reentered.png`、`final-valid-drag-preview.png`。
- 标题文字和空白均能折叠，完整标题行覆盖到右侧拖动手柄之前。标题拖动改变顶层顺序而不折叠，保存重启后保留；右键改名及撤销正常，所选故事和 Inspector 保持。见 `full-row-final-folded.png`、`full-row-final-open.png`、`group-renamed.png`。
- 实际新建 Task 只有目标，目标对象为空；通过“添加 → 流程 → 结算”连续创建两项，第二次入口仍启用。逐项删除至零结算，目标也可删除。删空、撤销、重做的节点数为 0→1→0；保存关闭重开后画布节点数和“公开输出”标题数均为 0。见 `default-objective-view.png`、`two-settlements.png`、`empty-reopen-ui.json`、`empty-task-reopened.png`。
- Project Story Graph 实际显示卡片外标题；内部新增、改接、剪线能力沿用上一轮已恢复的实现。本轮结构读取与完整回归确认没有恢复旧只读限制。

运行验收使用安装本轮 JAR 的隔离 Forge 1.7.10 `ReformServer` 与 `Client2`。事件由 RCON 调试命令注入，结果通过服务端状态和真实存档 NBT 核对；这不替代用户在自己的项目中验收。

- 三份任务先进入 ACTIVE。空图任务无结算，始终停在 Story 的 TASK 等待状态。
- 两份多结算任务的目标同次完成，使两个条件同时成立。节点及连线 JSON 顺序被打乱；显式顺序不同，最终分别选 `perfect`、`ordinary`。
- 每份完成任务只记录一次 Story 完成、一次奖励收据。经验 0→14，重复事件、断开重连、服务端重启及再次重复事件后仍为 14。
- 重启前后这三份 Task 的状态、最终端口、结算时间和收据结构完全一致；零结算依然 ACTIVE，完成历史为空。见 `runtime-assertions.json`、`runtime-snapshots.json`、`installed-jars.json`。

**用户验收尚未进行。** 没有记录为 USER_ACCEPTED。

## 交付与数据

已运行 `studio/package-studio.ps1` 更新权威自包含 Windows x64 Release。实际启动确认加载 `dist/DarkGreyRPGStudio/Program/DarkGreyRPGStudio.dll`，版本 0.3.3.6，程序响应正常；根目录仅 apphost EXE，405 项程序清单无缺失。复用一个候选目录，未创建成品包、发布、标签、提交或推送。

部署前后 Data **2,647 个文件、63,812,263 字节全部不变**；启动后原项目文件哈希也全部不变。工作区既有改动保留。见 `dist-data-audit.json`、`dist-projects-startup-audit.json`、`dist-startup.json`。

| 文件 | 字节数 | SHA-256 |
| --- | ---: | --- |
| `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`，ProductVersion 0.3.3.6 | 204288 | `8e8bf253b16b1516616d890b8bd604a907cf9d607469e3785d4ab113a5b3b20c` |
| `Program/DarkGreyRPGStudio.dll`，ProductVersion 0.3.3.6 | 1730048 | `94f3e253d55b4e91a1de59bf874b95b8be953357efe2146821992b958e497f7c` |
| `Program/DarkGreyRPG.Studio.Core.dll` | 1302528 | `59202b2509764f31e8697753f37b0f12e761c61f2f1b9cc01952d7071f383729` |
| `build/libs/darkgrey_rpg-0.3.3.6.jar` 与 `dist/darkgrey_rpg-0.3.3.6.jar` | 1953317 | `b41b414285f9e28d96ebddd64825ddc4690220b53648d69711ac4bb5ee1a5798` |

本轮 8 个已经完成交付的发布暂存目录，合计 4,748,393,164 字节（约 4.42 GiB），通过 SendToRecycleBin 移入回收站并确认原路径消失。保留程序、项目、证据与主构建缓存，未清空回收站。见 `recycled-staging.json`。
