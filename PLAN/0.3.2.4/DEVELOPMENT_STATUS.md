# DGR 0.3.2.4 施工与验收记录

> 2026-09-11 用户截图复核：六项修正见 [USER_REVIEW_FIXES.md](USER_REVIEW_FIXES.md)。原始工程现已找到并修正；实际运行包及“新的世界”存档兼容迁移已应用。下文旧的“仅隔离包已修复/工程未找到”描述属于先前阶段，已被本轮证据取代。用户验收仍为 NO。

状态：`LOCAL_ACCEPTANCE_CANDIDATE_DELIVERED`；M0–M11 已实现/自动验证，M12 游戏实机已验证，Studio Gate F/G 未完成。

`0.3.2.4_TECHNICALLY_COMPLETE=NO`；`0.3.2.4_USER_ACCEPTED=NO`。

计划：`PLAN/DarkGrey_RPG_0.3.2.4_Construction_PLAN.md`。
基线：`879fcfc60b6b4876ea1b673541186f63dc8f2792`；分支：`codex/0.3.2.4`。
未创建提交、推送、PR、Tag 或 GitHub Release。原有配置、AGENTS.md、历史 PLAN 删除及未跟踪文件保留。

## 本地交付

| 文件 | 版本 | 大小 | SHA-256 |
| --- | --- | --- | --- |
| `E:\Java\MinecraftMod\DarkGrey_RPG\dist\darkgrey_rpg-0.3.2.4.jar` | mcmod.info 0.3.2.4 | 977078 字节 | 024F8A29A694668C157FF7B7E348ACE1E459EFBE6CFD1DB98880913DDE839F81 |
| `E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe` | ProductVersion 0.3.2.4 | 141729500 字节 | 0B96B95401DA6820CCF4F98295C548EB998DF6813218554EA39841AD474CB660 |

JAR 与 build/libs 输出哈希一致，不包含 Probe/验收辅助 Mod。Studio 为 self-contained Windows x64 Release 单文件客户端，已从 .tooling 候选提升到上述唯一权威路径，源/目标哈希一致。

Studio 发布使用现有 csproj，命令参数 `-c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=0.3.2.4 -p:FileVersion=0.3.2.4 -p:InformationalVersion=0.3.2.4`。严格保持文件对话框例外，窗口标题/关于文案仍沿用 B4，不能据标题判断 EXE 是否最新。

用户原有 Studio 正在运行，直接覆盖被占用。已保留旧 EXE 到 `.tooling/0.3.2.4/studio-previous.exe`，将占用中的文件同目录改名为 `DarkGreyRPGStudio.previous-0324.exe` 后放入新 EXE，没有强制关闭用户原进程。原进程仍执行旧版本；从权威路径下次启动是新版本。

## 实现

| 模块 | 结果 |
| --- | --- |
| M1 Entity | 去掉关闭按钮；底部左指名/右解绑；当前实体与右侧 ID释放在信息条；Esc/重映射背包键关闭，搜索焦点优先，弹窗阻断下层输入。 |
| M2 Item | 左侧两组标题/槽/按钮各自居中；背包与快捷栏同一起点；右侧四个真实装备槽；资源释放有独立区域，不命中列表。总计 2+27+9+4=42 槽。 |
| 装备安全 | 槽映射 39/38/37/36；每槽上限1；调用 Forge isValidArmor；目标槽 shift-return 不将普通物品塞入装备槽；保留服务端原有返还与批量语义。背包键关闭仍走原版 GuiContainer 流程。 |
| M3 Scroll | 短时指数收敛、无越界/回弹；绘制/命中共享整数像素偏移；GL scissor 限制 viewport；刷新保留查询、包、资源和滚动状态；geometry 更新不重建 browser。 |
| M4 Task | Esc/重映射背包键关闭，去掉 Esc 返回提示；只改 GUI，缓存和推送不变。 |
| M5 Studio | 仅显示文件名将冒号改点；Export/Import/Reference 独立目录配置；成功选择后记忆，取消不写；保存前读最新设置，保留旧字段。 |
| M6 Content | 校验当前 run/client 测试包后，只修正第二个 interact_actor 描述为“与酒馆老板对话”；生成隔离验收包，用户原包未修改。 |
| M7–M10 Windows | Entity/Item/Copier/Task 独立拖动/缩放、最小尺寸、屏幕夹取；字体/图标/槽像素不缩放；center ratio+逻辑宽高持久化；只在手势结束/关闭写盘，原子替换并保护其他窗口设置。 |

共享实现：UtilityWindowGeometry、UtilityWindowSettings、UtilityWindowChrome；TaskLayout 新增用户窗口矩形入口；Copier 更新按钮坐标/宽度，保留模板顺序与服务器索引。Nominator modal 优先，Item 真实槽交互优先于窗口手势，拿着物品时不启动窗口拖动。

## 自动验证

- 生产源码修改前：build + **54 项自包含 JavaExec Probe PASS**，`evidence/baseline-standard.log`。
- 最终全量：**build + 58 项 JavaExec Probe PASS**，`evidence/full-regression.log`，1m38s。包括既有 B2 DGRS 外部夹具、全部原有回归、新增 SmoothScroll0324Probe、UtilityWindow0324Probe、Objective0324Probe。
- 新增实际包 Probe 单独输出 `RUNTIME_TRANSITION=PASS`、`AUTHOR_DESCRIPTION_FIXED=PASS_ISOLATED_PACKAGE`，没有改 Runtime。
- Studio：**49/49** 文件名、目录/旧设置兼容、主题与离线工作流相关测试通过，`studio-dialogs-fixed.trx`；Release publish 成功。
- Checkstyle、编译、测试、打包成功。Spotless 限定本次 Java 改动，使用 `scripts/0324-client-format.gradle`；不宣称全仓格式债务已消除。
- `scripts/verify-0324-freeze.ps1` 通过：Canonical Runtime、DGRS schema、网络/身份核心、任务缓存差异为0；Studio 仅7个文件白名单。MainWindow 只注入设置服务，OfflinePackages 只区分两种 picker，语义差异已审查。
- 产品源码 `git diff --check` 通过。

## 游戏实机

隔离目录：`.tooling/0.3.2.4/live-client`；Minecraft 1.7.10/Forge、Java8、已有 CustomNPC-Plus 修复版。验收辅助 Mod 监听 `127.0.0.1:32461`，在真实游戏线程调用 GUI 事件，经实际网络进入服务端；截图来自游戏 framebuffer。不是物理鼠标全流程或外部多人服验证。

| Gate | 已验证证据 |
| --- | --- |
| Entity/Task 关闭 | 实体指名，Esc、重映射 R 关闭，搜索框可输入 r；Task 重映射关闭；01–03、10–11 截图及 JSONL。 |
| Item/Armor | 42 真实槽；铁头盔拒绝胸甲槽、可放入/取出头盔槽；批量指名保持 windowId；Esc 返还；04–05、20–22 截图及 JSONL。 |
| Smooth Scroll | 滚轮目标与位置分离；动画中命中可见行；独立包/资源滚动；释放结果刷新保持查询/选中/滚动；15–16 截图、scroll-smoke.py。 |
| Objective | 实际杀3只绑定史莱姆，Task GUI 从击杀目标切至“与酒馆老板对话”，推送 revision 连续变化；10–11 截图，live-client-2.log 的 LIVE_TASK_RUNTIME。 |
| Dialogue | Choice 上 Esc 原版暂停，库存覆盖不推进；真实死亡、重生、退出重连保持节点；06–09 截图及日志。 |
| Drag/Resize | 四类窗口移动/缩放/关闭重新打开；12 系列；拖动期间无写盘，FPS 55–60。连续缩放无写盘，FPS 59–61，resize-performance.json。Copier 删除确认状态阻断拖动。 |
| Restart/Resolution | 真正退出 Minecraft 进程、重启后四类窗口各自恢复；640×480 安全夹取；13–14 系列、restart-results.json。 |
| Identity regression | NPC 转移确认/取消、孤儿释放、实体解绑及全局组释放；ItemID 转移确认/取消、EXACT/FUZZY、物品解绑/组释放、批量保持窗口；17–22 系列、JSONL。 |

实机只操作隔离世界和测试夹具；没有修改用户原世界、运行目录故事包或原 Studio 工程。末轮游戏运行已退出，隔离 Studio 测试进程也已退出，用户原有 Studio 保留。

## M6 来源边界

- 当前原包：`run/client/darkgrey_rpg_story_packages/kill_slimes.dgrs`，实时 SHA-256 `9B0F08C48BD702FE1F6060C52A455790DD7B0151E677EE72101C87852669EABD`。
- 隔离修正版：`PLAN/0.3.2.4/evidence/kill_slimes-description-corrected.dgrs`，SHA-256 `2D18468E5ED257E0C711741BD1A7B6449AE7071FB4AC8A4F7DDB322DE6AC0D4F`。
- 只修改 task `GreyHat_:KillSlimes` 节点 `node_4feb6ff8c4944e078f9914cd808c9f17` 的 `properties.description`。逐条检查 archive entries，其他条目字节一致；还原这一属性后整个 Task JSON 与原件相同。
- 精确可编辑 GreyHat Studio 源工程未找到；找回后仍须同样修正文案。旧 B4 工程不能作为该源工程。
- Worker 初报“当前包与旧 audit-trace 原包字节相同”未通过 Main 实时哈希复查；采用上述实时哈希，旧原包 82E289... 只作为历史证据。

## 未完成门槛

Studio Gate F/G **NOT_VERIFIED**：EXE 启动成功，辅助功能树/项目目录框可读取，但 Computer Use 截图、点击、赋值分别遭遇 0x80004002、coordinate input geometry unavailable、UIA CacheRequest 0x80070057。详见 `evidence/studio-ui-limit.md`。

未完成实际导出保存对话框默认文件名、Import/Reference/Export 三目录实际选择及重启恢复。49项自动测试不替代这些实机门槛。故不能标记全计划 technically complete 或 AGENT_REAL_MACHINE_VERIFIED，也不能标记用户验收。

## 失败尝试保留

- 首轮外部 Probe 缺 dgrsPath；A 旧夹具含已废弃 flow_judgment；名字固定的 B2 Probe 不能直接用 GreyHat 包。标准54项基线独立通过，最终改用正确 B2 夹具，实际 GreyHat 包新增专用 Probe。
- Studio 首编译缺 System.IO 引用，修复后49/49与Release通过。
- 验收辅助代码读取 private debugFPS 首编译失败，改为反射读指标；不属于生产源码。
- 死亡重生后玩家远离史莱姆生成区，首次杀怪夹具找不到实体；传送回隔离区后真实击杀/推送通过。
- 重启检查持有 Copier 时请求 Item Nominator 被正常拒绝；切换指名器后完成剩余恢复检查。
- 实体解绑检查误把之前击杀留下的组绑定也算入夹具数量；改为验证A保留/B解绑，再验证资源释放清除全组。
- 物品回归脚本点击搜索框开头后退格未清空旧字串；移动到末尾后正确重跑全部 Item 回归。保留失败脚本/JSONL，不计为通过。

`.tooling/0.3.2.4` 为实际运行脚本位置；`tools/` 是原始脚本与辅助 Mod 的证据副本，含早期失败/修复脚本，不能视作一键顺序执行清单。
