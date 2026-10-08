# 0.4.0.0 指名器异常隔离、拖动与字体修复

2026-10-08，接续已上传的六项修正，版本保持 0.4.0.0。本轮按用户批准的方案修复旧指名表读取异常、左侧拖动闪烁和故事组成员排序，并处理后续报告的指名器字体发花问题。

## 崩溃原因与处理

原测试世界的 `darkgrey_rpg_nominator.dat` 为 schema 3，而当前程序只接受 schema 4。拒绝读取本身符合精简后的合同；遗漏的是实体指名器打开入口没有隔离异常，异常经主线程队列逃逸到服务端 tick。此前已有当前格式世界的实机指名样例，但没有覆盖这条失败路径。原世界未删除、未迁移、未加载；原文件仍为 391 字节，SHA-256 `E9F5683573580BA79DB1864A91B299D86B0B805ED902A822044594CBD6744752`。

新增明确的 `nominator_data_unavailable` 错误。实体和物品打开入口在各自操作边界报告错误；统一操作与快照响应保留 token／sequence，失败响应结束客户端 pending。加载失败不会换成空表，不标记脏数据，也不部分修改 NPC 身份。实体检查、复制器／收纳箱适配器、解绑及命令相关调用已审查并在身份修改前读取指名数据。全局 tick 调度器没有增加吞掉全部异常的兜底，schema、网络注册与已删除类型规则保持。

对 Forge MapStorage 的实际读取顺序进行了字节码复查：SavedData 实例在解压／NBT 读取前创建。未完成读取及保存的 readFailure 都被拒绝；损坏 gzip 不会被当成可用空表。四种拒绝向量的真实入口／队列、后续正常任务、关联失败响应和客户端 pending 清理由新增 Probe 覆盖。

## 拖动与字体

故事列表和资源库以稳定视口及真实鼠标坐标判断边界，拖动期间保留同一预览实例和原始行几何。子控件的临时 DragLeave 不再销毁预览；预览不参与命中，按可见视口裁切。真正离开列表时恢复原行及相邻行，返回时复用预览；滚动同步几何偏移。保留单个跟随预览、等高空位和 120 ms 让位动画，系统关闭动画时直接换位。

组标题手柄仍移动整组；展开后的故事手柄仅在本组成员内排序。顶层独立故事保持顶层作用域。使用已有 NavigationOrder 保存稳定 Story ID，只替换当前作用域条目，不改分组、连线或引用包字节。有效落下提交一次全局 Undo；原位、Esc、失活及视图／分组变化取消。

指名器目录标题和故事组此前自动套用 `§l` 伪粗体，小字号中文叠绘会发花；搜索占位文本又绕过统一像素对齐／纹理采样。已去掉额外伪粗体并统一渲染路径，保留既有 Minecraft 1.7.10 位图字体和层次颜色。最终 JAR 已在真实游戏窗口检查，证据为 `drag-crash-font-final-entity-catalog.png` 和后续 CNPC 指名器截图。

## 自动验证

| 检查 | 本轮结果 | 本地证据 |
|---|---|---|
| 完整 Core | 499 通过／11 跳过／0 失败 | `evidence/drag-crash-core-full.trx` |
| 最终完整 WPF | 667 通过／1 跳过／0 失败，9m23s | `evidence/drag-crash-wpf-delivery.trx` |
| 最终 Runtime | 构建及 22 个当前 Probe 通过 | `evidence/drag-crash-runtime-with-font.log` |
| 精简来源审计 | 65 个退休路径在三种 JAR 的 class／Java 条目均为零；692 个生产源码指纹 | `evidence/drag-crash-delivery-source-provenance.json` |

Runtime Probe 覆盖指名器拒绝边界、当前操作、实体／物品身份、实体工具、网络注册／编解码、当前包准入、Session 执行／服务端／客户端／持久化和当前 Task 候选／视图。两个历史 Probe（CanonicalTaskRuntimeProbe、CanonicalStoryServerServiceProbe）仍使用旧 schema／旧身份夹具，尝试运行失败；原文件恢复，失败日志保留，不将它们计入 22 个当前通过项。

## 原生验证范围

所有新增游戏测试使用隔离目录和新建测试世界的副本。故障样本仅注入隔离副本；正常链没有通过改写绑定表来制造结果。原生桌面交互使用 Windows UIA／Win32，游戏使用实际键鼠输入，SavedData 用 NBT 读回核对。

| 样例 | 已执行结果 | 主要证据前缀 |
|---|---|---|
| 修改前正式 JAR，新原版单人世界 | 实体／物品指名器实际打开 | `drag-crash-baseline-*` |
| 最终正式 JAR，原版单人 | 实体指名、归组、转移、解绑、重新指名、搜索；物品个体及精准／模糊组；保存后读回当前表 | `drag-crash-sp-*`、`drag-crash-font-final-*` |
| 开发客户端，原版单人 | 实体绑定恢复及物品界面实际打开 | `drag-crash-dev-sp-*` |
| 开发客户端＋正式 JAR 原版专服 | 实体／物品正常操作与精准归组，正常停服重启后恢复；真实 Choice→Task 链，任务 SETTLED，进度 1／1／2／2，煤炭 4→2，XP=7 | `drag-crash-dev-server-base-real-*`、`drag-crash-dev-server-base-*-final.json`、`drag-crash-dev-server-base-choice-current.png` |
| 正式 JAR＋CustomNPC+ 专服 | 用原生 NPC Wand 创建真实 `customnpcs.CustomNpc`，指名／归组／转移／解绑／重新指名，物品个体／精准组及转移／解绑；正常停服重启后角色恢复 | `drag-crash-cnpc-dedicated-*`、`drag-crash-cnpc-server-*-saved.json` |
| 正式 JAR＋CustomNPC+ 单人 | 真实 NPC 指名／归组／转移／解绑／重新指名；进程重启后角色与组恢复；物品转移、解绑、重新指名读回 revision 9 | `drag-crash-cnpc-sp-process-restarted-binding.png`、`drag-crash-cnpc-sp-item-real-*`、`drag-crash-cnpc-sp-items-final-real.json` |
| 原版单人故障副本 | 原 schema 3 与 schema 4 损坏样本各反复打开三次，提示取消；后续命令及物品界面可用，无崩溃；拒绝文件与 NPC 身份文件哈希不变 | `drag-crash-legacy-fault-*`、`drag-crash-damaged-fault-*`、`drag-crash-*-preserved-after-save.json` |
| CustomNPC+ 专服故障副本 | 两种样本分别通过真实村民入口三次拒绝，随后命令及物品界面可用；正常停服后两文件哈希不变 | `drag-crash-dedicated-legacy-final-*`、`drag-crash-dedicated-damaged-confirmed-*`、`drag-crash-dedicated-*-preserved.json` |
| 最新 Studio 原生 | 两列表四边／角落／往返／停留，11 个位置各连续 8 帧稳定；组内与整组排序、Undo／Redo、保存重开、资源换位、Esc 取消、画布与属性框拖入 | `drag-crash-*-stable-edge-frames.json`、`drag-crash-navigation-reopen.json`、`drag-crash-*-handoff-readback.json` |

原始失焦、坐标错误、未退出／未保存和未命中实体的尝试均保留，不按文件名计 PASS。RCON 线程直接召唤 CNPC 的失败不是本轮正常链证据，最终使用玩家原生 NPC Wand 的服务端 tick 路径。没有修改第三方 CNPC JAR，也没有把其开发桩类带入最终模组。

这是实际执行样例的覆盖范围，不宣称全部环境的笛卡尔组合均已跑完。开发客户端＋CustomNPC+ 的完整原生操作矩阵、每一种工具／命令的故障原生组合，以及搜索／失活／引用组导航的所有拖动组合仍待单独原生验证；对应当前自动回归和调用审查不能代替这些实机项。原有额外物品过滤原生作者链、人工听音缺口继续保留，用户接受仍为 false。

## 交付与来源

最新 Windows x64 自包含 Release 已通过正常脚本提升到权威 `dist/DarkGreyRPGStudio`。用户保存关闭后，部署前后的 2,690 个 Data 文件、68,773,074 字节及每个相对路径／大小／SHA-256 完全相同；410 个程序文件逐项匹配验证候选。EXE ProductVersion 0.4.0.0，204,288 字节，SHA-256 `F68E1D6FAB7A4717089B394C9842BEDCFD7EAEC93382E76D86E0E5725E9001B3`。根 apphost 字节未变，真正更新的主 DLL 为 1,458,688 字节，SHA-256 `542F8B01708A84BBF0D9A938F10E5949621549C19BE50EFE363A8FF152BED3EF`。

配套 Runtime JAR 为 `build/libs/darkgrey_rpg-0.4.0.0.jar`，1,843,025 字节，SHA-256 `0D971907C95602AE94544B684D27E145014A9B5A4F9D1A79DEDF1E6508FF8CD9`。生产源码清单 SHA-256 为 `7fdbb6ea318486497a02e1021ce23a0d31c4522beed224b70dbd722677664ad5`，基于 `011a6613489e549b1eb2fd47d3d2a04dfc1268dd` 加本轮修复；上传后的提交与清单另由本地上传收据核对。

详细产物／Data 证明在 `evidence/drag-crash-authority-promotion-receipt.json`、`drag-crash-authority-program-file-provenance.json` 及 `DELIVERY.json`。原始 PLAN 和旧验收记录保持，当前结果另行追加。GitHub 仅上传现有 `codex/0.4.0.0` 源码及摘要；本轮未创建成品 ZIP、标签或 GitHub Release。
