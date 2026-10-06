# Studio 排序反馈、刷新稳定性与资源引用修正

日期：2026-10-05。版本保持 **0.3.3.6**。本轮仅处理确认的五项修正，保留当前 Task 规则及 Project 图连线能力。自动测试、代理实机和用户验收分别记录。`USER_ACCEPTED=NO`；未创建成品包、发布、标签、提交或推送。

实现、自动回归、代理实机和权威 dist 更新完成。最终候选与 dist 的程序 DLL 字节相同，Data 在部署和启动检查前后均未变化。

## 实现

- 输出排序按 [Unity Animated reorder 的相邻项让位方式](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/UIElements.BaseListView-reorderMode.html)调整。原行隐藏，相邻行移动，目标保留等高浅色轮廓空位，仅有一个跟随鼠标的输出行预览；Task Flow 显示目标优先级数字。列表总高度不变，命中计算始终使用拖动前的行位置。空位也能接受 OLE 放下，避免视觉位置正确但松开未提交。
- 节点参数、Inspector、Project Story Graph 复用同一组件。手柄独占手势，无上下箭头；拖动过程中不修改数据，原位放下不提交，有效排序只提交一次 Undo。Esc、离开列表、跨类别、窗口失活及视图卸载清理预览并取消。
- 用户可见“公开输出”统一为“输出端口”。Studio 自有对话框确认／创建／选择在左，取消在右；三按钮取消最右，保留 Enter、Esc 和危险操作默认选择。
- 原闪缩来自资源刷新无条件重发角色／物品通知，视图据此清空并重建参数编辑器。现在只通知实际变化的列表，原地更新相关下拉选项，按资源、节点、端口身份保留编辑器、输出 ViewModel、折叠状态、焦点和未提交文本。新数据上下文直接同步展开状态，220ms 动画只用于用户切换。另修正水印每次 Arrange 重建裁剪，以及引用包折叠区在 LayoutUpdated 重新 Measure 的循环。
- 引用窗口改为一个按故事归属组织的折叠目录。仅显示故事自身拥有的资源，过滤目标已拥有／已引用项，不把引用资源再次投射到借用故事文件夹。紧凑行显示六类中文标签，搜索名称、类型、故事名称，搜索期间展开匹配文件夹，清空后恢复展开状态；资源双击确认，标题只折叠。
- 本地资源走对应类型的本地引用服务；外部资源经过完整 Provider 和归属校验。确认时重验来源、目标成员关系，失败回滚成员文件，成功进入既有 Undo/Redo。操作消息使用故事名与资源名，内部身份继续用于诊断。选择窗口、只读预览、Project 引用包资源行、工具提示及辅助功能名称不显示资源 UID。
- 引用保持只读，编辑内容、名称和 Task 优先级仍需先导入。没有新增持久化字段、迁移、自动改写已有 Task 或用户文件。默认 Task 仍只有一个目标，目标和结算可删空；零结算保持 ACTIVE；Task Flow 显式顺序决定结算优先级。Project 新增、改接、剪线能力保留。

## 自动测试

证据目录：[evidence/OutputReferenceFix](evidence/OutputReferenceFix)。

| 检查 | 本轮结果 | 证据 |
| --- | --- | --- |
| Core 完整 Release 套件，随附 FFmpeg | 496 通过，0 失败，0 跳过 | `core-complete.trx` |
| WPF 最后补漏前完整 Release 套件 | 673 通过，0 失败，1 跳过，共 674 | `wpf-complete-final.trx` |
| 排序、刷新、引用及 UID 补漏定向回归 | 17 通过，0 失败 | `focused-no-uids.trx` |
| WPF 最终完整 Release 套件 | 673 通过，0 失败，1 跳过，共 674；8 分 59 秒 | `wpf-delivery-complete.trx` |
| Gradle build、Spotless、Checkstyle、test 及当前端口优先级／真实导出包探针 | BUILD SUCCESSFUL；两个当前探针标记均 PASS | `java-delivery-export.log` |

WPF 唯一跳过项为显式启用的 `FixedThreeHundredNodeWorkload` 性能基准。Java 的 `test` 任务没有独立 JUnit 用例数，不以 Gradle 任务数代替测试用例数。当前探针验证同次成立、显式优先级反转、JSON／位置扰动、延迟事件、重复输入、最终结果及奖励收据恢复，以及零结算保持 ACTIVE。

额外尝试的两个历史独立 Java 探针未通过：`canonicalTaskRuntimeProbe` 的旧 Task schema／结果槽夹具被正式 Runtime 拒绝；`storyPackageLoaderProbe` 使用已停止支持的目录包夹具，没有可加载的归档后发生空指针。原始失败保留在 `java-related.log`。它们不属于 Gradle `build` 的 test 任务；本轮未将其记作通过，也未放宽旧结构拒绝或添加兼容转换。`offlineOriginProbe` 同次运行通过。

## 代理原生 UI 验收

使用 Windows UI Automation 和 Win32 鼠标／键盘操作隔离候选目录 `.tooling/0336-ui-followup/Studio`，没有操作用户项目内容。候选复用同一目录及其隔离 Data。

- 实际拖动 Session、Task 的 Flow／Logic，覆盖节点内与 Inspector；Project Story Inspector 验证 Flow、Logic。深浅主题、较窄 Inspector、缩放及已选中／未选中节点均检查。截图见 `gap-*.png`、`inspector-*-preview2.png`、`light-*.png`、`narrow-task-preview.png`、`zoom-*-preview.png`、`project-*-preview-final.png`。
- 读回排序、节点坐标、端口 ID、名称和已有连线；一次撤销恢复原始数据，重做恢复排序。Esc、离开列表后返回、Flow 拖入 Logic、原位放下、窗口失活均无持久化变化。Project Logic 最后通过“保存全部”核对落盘；主页的 Ctrl+S 是“保存当前资源”，不能作为此场景的保存全部命令。`native-model-results.json` **77 项全部为 true**。
- 已有输出卡片展开和收起时分别新建 Task，各采集 **45 帧**，输出区域逐帧像素差均为 0。证据为 `native-frame-results.json`、`task-created-open-final.png`、`task-created-closed-final.png`。自动测试同时确认连续创建 4 个 Task 后原编辑器、输出 VM、行实例与折叠状态不变；新增角色更新选项，未提交文本及焦点保留。
- 新建 Task 对话框验证空名称不可提交、占位和按钮次序，见 `task-dialog-final.png`。目标空对象、零结算与多结算规则由本轮完整 Core／WPF 和当前 Java 探针回归覆盖；不把历史原生结算创建截图重复记作本轮操作。
- 同组本地角色引用成功，外部单故事角色与外部组角色组引用成功；跨组本地 Session、Task 引用成功。实际搜索故事名显示六类标签，按类型搜索、100 个角色中的名称检索、叶子双击确认、重复项过滤、非拥有资源不重导出到借用文件夹均检查。见 `ref-search-six-visible.png`、`ref-type-search.png`、`ref-many-search.png`、`ref-local-success.png`、`ref-external-group-success.png`。
- 引用 Undo 依次 3→2→1→0，Redo 0→1→2→3，成员 JSON 与预期一致。保存关闭重开后同组、跨组和外部来源的五类引用均恢复，`reopen-results.json` 五项为 true。重复身份、来源归属变化、回滚、六类引用和导出往返由定向服务回归覆盖。
- Project 真实鼠标剪线、重新新增连线和撤销均完成，`project-wire-*.json` 验证连接数量和稳定端口。改接入口及引用来源只读继续由完整 WPF 回归验证；本轮没有把旧版原生改接截图计入新证据。
- 最终补漏后再次发布同一候选，实机展开 Project 引用包的角色、物品、会话、任务分类。六类资源行及辅助功能名称均不含内部 UID；悬停显示“外部群 [角色组]”。见 `project-reference-labels-final.png`、`project-reference-tooltip-final.png`、`project-reference-labels-uia-final.json`。工具提示文本以截图人工检查为证，未把 UIA 没有单独暴露的 Popup 虚报为自动读取成功。

本轮实机截图与 77 项数据核对不等于用户验收；未标记 `USER_ACCEPTED`。帧采样只证明所记录的输出区域和操作窗口，没有据此宣称所有窗口、性能负载都已覆盖。

## 隔离导出包与 Runtime

从原生操作后保存的隔离项目导出 `NativeReferencedGroup.dgrs.g`，同时保留其外部单故事、故事组依赖。正式 Java StoryPackageLoader 加载这三个归档，共 **5 个 Story、4 个 Task**；构造 Task 运行实例并保存恢复，零结算仍 ACTIVE。日志同时出现 `STUDIO_REFERENCE_EXPORT_SINGLE_GROUP_TASK_RUNTIME_PASS` 和 `PUBLIC_OUTPUT_PRIORITY_0336_PASS`。各归档及 Runtime JAR CRC 校验通过。

这是正式加载器／Runtime 的 Java 进程验证。本轮没有重跑 Minecraft 联机客户端或专用服务器，此前真实 Minecraft 验收继续作为历史证据保留，不能算作本轮新实机结果。

## 交付、数据与清理

使用 `studio/package-studio.ps1` 更新最终候选及权威 `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`。实际启动权威 EXE，窗口显示 0.3.3.6，加载对应 `Program/DarkGreyRPGStudio.dll`，正常关闭；`dist-launch-final.json` 记录路径、响应状态和加载程序集哈希。根目录只有 apphost EXE，405 项程序白名单文件齐全。Runtime 构建产物为 `build/libs/darkgrey_rpg-0.3.3.6.jar`，dist 中配套 JAR 字节相同，JAR CRC 和 `mcmod.info` 版本均核对通过。没有生成成品 ZIP 或 `artifacts/DGR…` 目录。

| 文件 | 版本 | 字节数 | SHA-256 |
| --- | --- | ---: | --- |
| `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe` | 0.3.3.6 | 204288 | `8E8BF253B16B1516616D890B8BD604A907CF9D607469E3785D4AB113A5B3B20C` |
| `Program/DarkGreyRPGStudio.dll` | 0.3.3.6 | 1743360 | `C157B49BE505FF57DE231A563C88B1EEFAC99D74EB146F4759EA9D556EC168B5` |
| `Program/DarkGreyRPG.Studio.Core.dll` | 程序配套 Core | 1304064 | `870616EF7ABBEDFB33D72338E5E4909E7D58A55A4CA670FB36A98A0FBA665899` |
| `build/libs/darkgrey_rpg-0.3.3.6.jar` | 0.3.3.6 | 1953317 | `B41B414285F9E28D96EBDDD64825DDC4690220B53648D69711AC4BB5EE1A5798` |

apphost EXE 负责加载 Program 中的程序集，功能修改体现在 DLL 中；同版本的宿主 EXE 哈希未变不代表 DLL 未更新。Core 自身程序集 ProductVersion 为 `1.0.0+99a3253d7186127bdbe37898824ecbd32219f136`，Studio 和 Runtime 版本没有提升。

Data 基线 **2,651 文件、63,815,496 字节**。正式更新仅替换程序白名单，Data 完整性按每个相对路径、大小和 SHA-256 比较；最终部署及实际启动关闭后 **变化 0、缺失 0、新增 0**。完整核对见 `final-integrity.json`、`delivery.json`；原始逐文件基线保留在 `.tooling/0336-ui-followup/OutputReferenceFix/data-before.json`。

只回收本轮已确认不用的发布暂存目录，保留 SDK／NuGet 缓存、候选 Data、隔离项目、测试证据和已有工作区改动；每个绝对路径先确认位于 `.tooling/wpf-build` 内，无链接、无 Data、无运行程序，再送入回收站并验证来源路径消失。不清空回收站，回收站占用不描述成已释放的磁盘空间。

共回收 **20 个发布暂存目录、11,871,293,182 字节（约 11.87 GB）**。20 个来源路径均消失，E 盘回收站中对应 20 个元数据与负载全部存在。见 `recycle-all-staging-result.json`、`recycle-bin-payload-proof.json`。这些暂存来自反复的自包含候选构建及目录整理；最终候选只保留一个目录，未回收更早且未确认用途的发布暂存。

旧报告中的文件校验值属于对应历史轮次；最终权威信息由本报告及 [Delivery.json](Delivery.json)记录。
