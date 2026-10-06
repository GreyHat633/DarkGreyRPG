# 0.3.3.7 验收记录

本文件保留七工作包施工时的历史结果。后续五项交互修正及当前核验见 [Corrections.md](Corrections.md)；历史交付哈希见 [Delivery.Construction.json](Delivery.Construction.json)，当前交付见 [Delivery.json](Delivery.json)。

日期：2026-10-06。分支 `codex/0.3.3.7`，基线 `d0d3f0e9f057793d1399568cc113f947fa1d86cc`。七工作包已实现；下列结果区分自动测试、实机检查和未执行的视觉矩阵。本文不表示用户已验收所有场景。

## 自动测试与构建

| 项目 | 结果及证据 |
|---|---|
| Java 完整构建 | PASS，Spotless、Checkstyle、编译、测试及 reobf JAR；`evidence/java-final-complete.log`。 |
| 追踪／偏好／预算／滚动 | PASS，`Construction0337Probe`、0332 追踪和偏好探针、0324 滚动探针；覆盖满额、关闭事件消费、重复事件、取消不补、实例／上下文、独立偏好、整图标预算、20／24／28 行高及 30／60／144 Hz 的绘制命中一致性。 |
| 真实包逻辑运行 | PASS，单 Story 包及组容器中各 Story／Session／Task，六份资源；每份 256 个八位向量，运行及恢复共 3,072 次。验证 2／3／8 输入 AND／OR、未连接输入 False、一输入损坏数据拒绝；`java-fixture-final.log`。 |
| 旧任务界面接口 | PASS，`canonicalTaskStage4SurfaceProbe`；`java-repro-and-legacy.log`。 |
| Studio Core 全量 | 492 PASS，11 项既有媒体环境用例跳过，0 失败；`studio-full-final.log` 中 Core 结果及 `Tests/0337-full_net10.0_20261006204345.trx`。 |
| Studio WPF 最终全量 | 689 PASS，1 项既有性能用例跳过，0 失败；`studio-wpf-isolated-decode.log` 和 `Tests/0337-wpf-isolated-decode.trx`。将既有图片异步解码的 Dispatcher／3 秒期限用例标为 DoNotParallelize 后，全量通过；保持原断言、期限和运行逻辑。此前两轮 688 PASS／1 失败的日志 `studio-wpf-delivered.log`、`studio-wpf-verified.log` 保留供诊断。 |
| 最终针对回归 | 35 PASS，0 失败、0 跳过，包括三图双入口、最小数量、连线删除／Undo／Redo、只读、重复反馈计时、切图／释放、真实 WPF 像素移动和虚拟化、拖放选择框及旧图片解码。 |

Core 与 WPF 最终全量合计 1,181 PASS、12 项既有跳过、0 失败。失败阶段日志保留供诊断：早期同文案提示计时边界失败已修复；一次命令误写 Gradle task 名称、重复复制 UniMixins、无效 fixture 身份及 LAN 无效登录会话不作为通过证据。图片超时出现在并行测试阶段，隔离后全量通过；未对解码耗时作更细根因归因，不将机器负载猜测写成已证实事实。

## 实机环境

Windows 原生 UIA／Win32 操作，截图由 System.Drawing 捕获。Studio 使用独立 `.tooling/0337/Studio/Data/Projects` 验收项目，另对权威 EXE 执行可逆搜索与恢复检查。未编辑用户项目。

Minecraft 使用用户提供客户端的 Forge 10.13.4.1614、1.7.10 及 Java 8 库，在仓库 `.tooling/0337/Runtime*` 创建独立配置和世界。未改用户客户端的 mods、配置或存档。最终 RuntimeH、RuntimeMP 和独立 Server 使用的 JAR 与 Delivery.json 一致。

JourneyMap 来源：`E:/灰之黑-自用/.minecraft/mods/[旅行地图] journeymap-forge-1.7.10-6.0.0-beta.4.jar`；SHA-256 `5ad875ba2e7c044d0c382639593365c4e484142b19fb638e2a6724a7d3ad7273`。默认生物图标配置实测过通知覆盖；该 beta 另有 `RabbitModelMixin` 缺失类诊断，记录于 `journeymap-beta4-diagnostic.log`。后续 RuntimeH 只将本测试副本生物图标改为 Dots，以继续交互检查；未替换原 JAR。

联机使用独立 Forge 服务器，绑定 `127.0.0.1:25637`、独立世界、离线测试身份。首次 LAN 会话被登录认证拒绝后改用该服务器，成功接入、接取、关闭开关、取消追踪及重连；Windows 防火墙提示选择取消，未增加放行规则。

## 实机结果

| 场景 | 已确认结果／证据 |
|---|---|
| 长任务与两个短任务 | 三个标题均保留，第一任务仅压缩自己的正文，完整正文仍在任务详情；`runtime-28-hud-budget`、`runtime-59-task-detail`。 |
| 独立左右及通知动画 | 四组合实测：右／右 25，左／右 32，左／左 33～35，右／左 36。左侧进入、停留、退出分别记录；任务窗口上的通知见 37、80。 |
| 小地图覆盖 | 25、32、78 中通知覆盖 JourneyMap；追踪与通知没有相互避让。此结论仅针对上述精确 JAR。 |
| 设置及背包关闭 | 第四「任务」页 30；默认 E 31、改绑 H 53～54、鼠标右键 65～66 及联机 76～77；关闭后画面为世界，未打开背包。 |
| 字号／窄窗口／恢复默认 | 125% 与 150% 记录 70、71；缩到 854×518 外窗时三标题保留 72；恢复默认后保留三项选择且偏好回到默认 73。 |
| 详情平滑位移 | 62 为滚动前、63 为约 35 ms 中间帧、64 为再等待约 200 ms 后；正文逐步移动，边界裁切，固定栏未移动。 |
| 联机追踪规则 | `network-tracking-sequence.json`：满额后仍 3；关闭后接取仍 3；手动取消后 2；重新开启仍 2；以后新接 network6 才变 3。保存身份明确排除 network4、disabled5。 |
| 联机重连 | 90 确认断开、91～92 确认重连；前后 task／tracking 键完全相同，自动开关仍关闭。`network-reconnect-result.json` 为 Equal=true，客户端／服务器最终日志保留。 |
| Studio 双入口 | 最终客户端节点内增加到 3，Inspect 增加到 4，撤销回到 2；06～10。按钮加减字符可见。 |
| 删除有线输入 | 23 为明确一条连接的确认；24 删除最早输入，保留输入 2／3；25 撤销恢复输入 1／2／3 和对应连线。随后再撤销新增输入并保存测试项目。 |
| 无效拖拽短反馈 | 最终原生拖放：28 为拒绝悬停、29 为松手反馈、30 为约 3.3 秒后消失；31 为 Esc 取消后清理。最终布局 32 中提示位于工具栏下方，文案完整且不遮挡按钮。 |

Studio 切图清理由 WPF 回归证明；33／35／40 的原生操作当时仅选择资源，并未即时打开另一张图，不作为切图清理的通过证据。15、21、26 等调试阶段静态截图不能代替最终动态结论。11 的 Session 资源拖放实际是创建合法引用节点，随后撤销，不能作为“拒绝参数拖放”的通过证据。Runtime 38～41 为最小化时的无效截图，已排除。

## 普通滚动页检查表

以下“实现复核”只证明代码机制，不等于该页面所有视觉场景已实测。共同数学测试和 WPF 真实控件测试另列，避免重复填写全页 PASS。

| 页面／区域 | 最终机制及命中／边界 | 验证范围 |
|---|---|---|
| 规范任务目录 | SmoothScroll 当前像素、实际行高、局部裁切；选择／折叠使用相同偏移，切页和范围变化重约束。 | 数学探针＋实机目录／选择；长目录动画中点击待补。 |
| 规范任务详情 | 当前偏移更新 DetailBlock 和物品点击范围；完整文本／图标局部裁切；历史分页 loading／failed 防重复。 | 实机连续位移、正文完整；网络失败／重试和动画中候选点击待补。 |
| 对话记录 | 实际字体行高、像素位移、区域内轮询；Home／End 明确跳转。 | 实现复核，共同滚动探针；长记录实机待补。 |
| 设置四页 | 同一偏移绘制／命中、内容区域裁切、切页归零；滑条保持调值语义。 | 四页结构、任务页、字体、关闭及偏好实测；三主题窄页矩阵待补。 |
| 指名器目录／资源 | 已有 NominatorBrowser 两份 SmoothScroll，20 像素实际行高、同偏移命中、嵌套优先和范围重约束。 | 保留现有机制，源码复核及既有接口探针；全页实机待补。 |
| 玩家状态列表／详情 | 24／12 像素行高，独立偏移、视口内点击与滚轮、部分行裁切。 | 实现复核，共同探针；实机待补。 |
| 故事包列表／详情 | 实际目录行高／12 像素详情，独立区域裁切与命中。图区域滚轮缩放保留。 | 实现复核；包图缩放和长目录实机待补。 |
| 复制器模板 | 当前偏移更新可见按钮及部分行；仅视口内点选，删除确认阶段不滚，关闭不受列表裁切影响。 | 实现复核，共同探针；长模板实机待补。 |
| 物品候选窗口／弹层 | 24 像素当前页移动，图标部分行严格裁切，视口外不悬停；分页按钮保留，加载中不重复翻页。 | 实现复核及整图标预算探针；物品混合目标实机待补。 |
| 长对话选项 | GuiWrappedChoiceButton 使用真实行高及部分行裁切，保留选项点击语义。 | 共享组件复核；长选项实机待补。 |
| 旧任务日志 | 12 像素行高、范围约束及部分行裁切，切状态直接归零；非规范入口仍保留。 | 完整编译及 Stage4 接口探针；旧入口实机待补。 |
| 任务提交选择 | 显式按钮分页保持原语义，普通滚轮只交给内部候选弹层。 | 实现复核，不将明确翻页改成伪滚动。 |
| Studio 项目／资源树 | Loaded ScrollViewer 行为，虚拟化容器使用 Pixel 单位，不关闭 CanContentScroll 或 recycling。 | 真实 500 行 WPF 列表，约 50 ms 中间偏移及收敛 48 像素，已实例化项少于 100。 |
| Studio Inspect／节点长列表／底部 | 最近实际 ScrollViewer 消费轮询，原生坐标命中；卸载、鼠标按下、导航键停止旧动画，尊重系统动画设置。 | 全量／针对 WPF 回归与原生双入口；所有嵌套组合实机待补。 |

## 矩阵覆盖边界

TRK-01～13、15 有受控身份／事件探针，TRK-02～07、11、14另有单人或联机实测。LOG-01～11 覆盖三图双入口、连接事务、导出运行与恢复；复制及其他既有图行为由原回归覆盖。STU-01／02 有自动测试及原生检查。DRP 规则、计时及释放有 WPF 测试，原生拒绝、松手反馈、自然消失及 Esc 取消已确认。LYR-01～04、08／09 有目标组合画面及时序。

仍未执行全量原生视觉矩阵：三个不同故事／三个都很长／混合物品图标／计分板，活动通知中换边及 F1／对话恢复，设置三主题与未绑定背包键，所有列表的动画中点击、历史失败重试，高刷新率实机，以及多条启动条件／奖励的全部拖放落点。对应 HUD-03～05／09、SET-06／08 部分／09～11 部分、SCR-03～16 部分、DRP-05～09 部分、LYR-05～07／10～11 部分不能据其他测试笼统填写 PASS。

## 复现与交付核验

`Fixtures/Program.cs` 通过正式 Authoring／Inspector 动态输入入口构图并由 Studio Exporter 导出；`Fixtures/Packages/Single/fixture.dgrs` 和 `Group/fixture.dgrs.g` 是实际测试输入，不是成品发布包。可重新运行 `dotnet run --project PLAN/0.3.3.7/Fixtures/Fixture.csproj -c Release -- <全新测试项目目录> <全新测试包目录>`。Story UID 每次新建会不同。

Java 复现：`gradlew.bat construction0337Probe logicInputs0337Probe taskTracking0332Probe playerPreferences0332Probe smoothScroll0324Probe build`，探针缓存写 build；使用别的真实导出包时传 `-PlogicInputsFixture=<包目录>`。

权威 EXE 版本、大小和 SHA-256，以及 Program 程序集、Runtime JAR 和实测副本校验记录于 Delivery.json。初次和最终推广前后的 2,661 个 Data 文件逐项内容一致，最终对照在启动前完成（`data-final-preservation.json`）。Program DLL 与候选及 Release 输出哈希相同。权威 EXE 启动并完成搜索／恢复原值检查：`authoritative-final-smoke.json`、42。程序根目录仅 EXE，Data 未被部署清空。用户正式验收与完整视觉矩阵仍待补，不将开发交付等同于全项验收。
