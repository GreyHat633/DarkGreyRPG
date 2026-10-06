# 0.3.3.7 五项交互修正：实现与验证

日期：2026-10-06 至 2026-10-07（Asia/Shanghai）。沿用 0.3.3.7，完成已确认的 40% 列表比例及直接名称输入框方案。当前交付以 `Delivery.json` 为准；七工作包施工时的旧清单保留为 `Delivery.Construction.json`。

## 修改结果

- 管理器读取当前绑定，同一键盘或鼠标绑定关闭，保留 Esc／背包键。关闭消耗排队按键、结束拖动、保存窗口与画布位置，沿用服务端 CLOSE。
- 任务设置使用三个平级名称：任务追踪、任务追踪显示位置、任务弹窗显示位置。独立绘制复选框，固定“任务追踪默认开启”的文字起点；自动追踪仍仅作用于未来新接任务。
- 文字大小按钮从标题起点下移 `FONT_HEIGHT + 4`，说明和点击区域同步调整。
- 任务上下／左右布局均为可分配内容区域列表 40%、详情 60%，保留间距、最低尺寸及独立滚动，绘制和命中共用布局。
- 三种图的 AND／OR 共用卡片组件，标题无数量，右邻 +／−。手柄排序，名称框 Enter／失焦提交、Esc 恢复；空名称或重名保留有效值。
- 按稳定端口 ID 增量更新行对象，双入口同步名称、顺序和选择。选中后由标题 − 删除，至少保留两个；带线删除确认，端口与线组成一次 Undo。
- 复用动态端口移动事务与 120 ms 让位预览：隐藏原卡片、等高空位、单个预览。空白、其他编辑位置、切图、卸载和窗口失活清理选择；标题动作保留删除目标，手柄不移动画布节点，取消拖动恢复预览。
- 无协议、端口格式或数据迁移，运行语义保持。

## 自动验证

| 验证 | 结果与证据 |
|---|---|
| Java 构建、40% 布局、偏好、追踪预算、滚动 | PASS；`evidence/corrections-java-final.log`。上下／左右、窄尺寸、绘制命中、比例取整误差≤1 像素。 |
| 真实包逻辑运行 | 单 Story／组容器各 Story、Session、Task，3,072 个运行／恢复向量 PASS；同上日志。 |
| 服务端 CLOSE | `tavernRepeat0336Probe` PASS，关闭后旧会话不能 LIST；`evidence/corrections-manager-close-probe.log`。 |
| 卡片及端口重点回归 | 14 PASS；`evidence/corrections-wpf-final-focused.log`、`Tests/0337-corrections-final-focused.trx`。六组合、双投影、行对象保留、校验、选择清理、只读、带线移动／删除、单次 Undo／Redo、持久化、真实 WPF 提交／恢复／失焦及预览。 |
| Studio WPF 全量 | 691 PASS、1 项既有性能用例跳过、0 失败；`evidence/corrections-wpf-all.log`、`Tests/0337-corrections-all-wpf.trx`。 |
| Studio Core 全量 | 492 PASS、11 项既有媒体环境用例跳过、0 失败；`evidence/corrections-core-all.log`、`Tests/0337-corrections-all-core.trx`。 |
| 自包含 Windows x64 Release 构建、推广 | PASS；`evidence/corrections-publish-final-candidate.log`、`evidence/corrections-dist-promotion.log`。 |

早期 `corrections-java.log` 的旧 `presentation0322Probe` 因旧 Story UID 样本失败；本轮独立 `taskLayout0337Probe` 及最终构建通过。早期日志不计为最终通过结果。

## 实机检查

使用 Windows UIA／Win32、真实键鼠与窗口截图。Studio 编辑隔离项目；游戏使用 `.tooling/0337/RuntimeH` 独立世界、配置及 mods，未修改用户 Minecraft 客户端。下表证据在 `evidence/` 下；编号前缀分别为 `corrections-runtime-` 和 `corrections-studio-`。

| 场景 | 已确认结果与证据 |
|---|---|
| 默认 O、改绑 K、鼠标右键 | 同键打开／关闭，关闭后等待 2 秒无立即重开。K：29／30／31；O：37／38；鼠标：41／42／43。H 背包键关闭见 32；Esc 关闭也执行。 |
| 管理器位置保存 | 拖动、关闭、重开位置保留，39／40 及 `corrections-runtime-window-state.properties`。 |
| 勾选不位移 | 15／16 的文字区像素差为 0，`corrections-checkbox-pixel-check.json`。 |
| 游戏三主题及字号 | 灰黑／浅白／蔚蓝 17～21；150% 按钮、说明及后续滑条 48。 |
| 窄窗口与滚动命中 | 260×260 逻辑尺寸任务设置 51／56；文本滚动 58，点击仍可见的 100% 按钮后属性读回 `ui.textScale=1.0`，见 59。 |
| 任务 40% 布局 | 左右与长详情 44／45／46，上下 47。详情独立滚动至第八个目标，比例另有布局探针。 |
| 双入口带线排序 | Story AND 节点内 07／08、Inspector 11／12，顺序同步、线端点跟随。`corrections-studio-drag-readback.json`：节点边界前后均为 `502,430,162,254`。 |
| 取消选择、最低数量 | 空白取消后两入口 − 读回禁用，13；OR 两输入禁止删除，25；新增 26。 |
| 带线删除、确认与撤销 | 确认 15、取消读回 22、实际点击“是”删除 23。Session 删除后将焦点放回画布，一次 Ctrl+Z 恢复端口和线，Ctrl+Y 再删除，33／34。名称框内保留文本自身撤销。 |
| 三种图、名称同步与焦点 | Story 02、Session 32、Task AND／OR 36。`corrections-studio-focus-readback.json`：改名后输入框 RuntimeId 不变且仍有键盘焦点。 |
| 校验、取消与重开 | 重名恢复 37、Esc 恢复 38、Esc 取消拖动且顺序不变 39；重启后 Task 首输入仍为“任务首条件”，41 及保存 JSON。 |
| Studio 深浅主题 | 深色 05／32；隔离设置指定 Light 后的实际浅色卡片 42。40 的菜单调用后仍为深色，不计作浅色通过证据。 |
| 权威程序启动 | 恢复原“测试项目”，搜索 3→0→3 并恢复原值；`corrections-dist-startup-readback.json`、`corrections-dist-01-startup`、02／03。未编辑用户资源。 |

JourneyMap 使用用户指定的 `journeymap-forge-1.7.10-6.0.0-beta.4.jar`，SHA-256 `5ad875ba2e7c044d0c382639593365c4e484142b19fb638e2a6724a7d3ad7273`。其“全屏地图选项”也默认绑定 O，原版按键槽冲突导致初次 O 无法打开管理器；仅在隔离客户端将该 JourneyMap 绑定设为 NONE 后，O 验证通过。未替用户改绑定。

早期 Runtime 05～11 是 O 冲突，27／28 仍在暂停菜单；Studio 17～20 为确认框操作未正确关闭的诊断；Runtime 54 被后来启动的验收窗口遮挡。这些图片保留，但对应通过结果以上表指定的后续有效证据为准。

## 覆盖边界

- 六组合、双投影的完整状态及端口事务由自动回归覆盖；实机未逐一重复 6×2 入口的每一种操作组合。
- 本轮未重复联机、长任务列表及物品图标的全部实机矩阵；七工作包历史记录保留，当前长详情样本不含奖励图标。
- CLOSE 代码路径及服务会话探针通过，未抓取实机网络包逐包核对。
- 动画代码与预览回归通过；未做帧级闪烁／帧时间量测，也未逐项完成浅色排序、所有缩放及失活中途拖动矩阵。
- Studio 浅色启动已验证；菜单切主题此次未生效，记录为既有菜单交互的待调查项，不计作通过结果。

## 当前交付

权威路径：`E:/Java/MinecraftMod/DarkGreyRPG/dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`。

- ProductVersion `0.3.3.7`；EXE `204288` 字节。
- EXE SHA-256：`eb4975e1ee5d37c80c0250102f40087c3d6d68649004d7233ce77ecc7c551566`。
- Program/DarkGreyRPGStudio.dll SHA-256：`dd2bb6db3d349a2faf14819459b7e3e65ac618546c47bb68da13c48953e04e25`，与实机候选一致。根 apphost 未变，因此 EXE 哈希与上次相同，程序集已更新。
- 推广前全部 `2661` 个 Data 文件按大小和 SHA-256 验证保留，见 `corrections-dist-data-before.json`、`corrections-dist-verification.json`；启动检查正常产生运行日志。
- Runtime `build/libs/darkgrey_rpg-0.3.3.7.jar`，`1992738` 字节；SHA-256 `2700738aaf5325643c982396f1239d3959575c063586d96e0f5fb223cb169b7b`，本轮 RuntimeH 副本一致。

以上是施工交付时的记录。随后用户明确要求成品打包与 GitHub Release；本次发布记录见 [Release.md](Release.md)，成品与确切来源清单保存在 `artifacts/DGR0.3.3.7`。
