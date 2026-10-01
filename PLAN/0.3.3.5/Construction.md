> 2026-10-01 最新折叠交互、候选浏览与追踪 HUD 修正见 [FOLD_TASK_UI.md](FOLD_TASK_UI.md)。正式 EXE/JAR 及 SHA-256 已同步至 Delivery.json。

> 2026-10-01 编辑区精简与故事任务分组的最新交付见 [STUDIO_TASK_POLISH.md](STUDIO_TASK_POLISH.md)。历史证据保留，完整人工验收尚未标记通过。

> 2026-10-01 本次用户确认方案的最新实现、正式交付与验证见 [SEQUENCE_REVIEW.md](SEQUENCE_REVIEW.md)。Delivery.json 已更新；下文旧轮次记录保留为历史证据。

> 2026-10-01 九项截图问题的最新修复与交付见 [UI_REVIEW_FIX.md](UI_REVIEW_FIX.md) 和 Delivery.json。下文保留上一轮记录，不代表追加修复前已满足用户要求。

# 0.3.3.5 施工记录

基线：`codex/0.3.3.4 @ 515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf`。施工分支：`codex/0.3.3.5`。
用户已授权开始实施；历史删除、本地缓存及正式项目保留。基线差异保存在 `.tooling/0335/BASELINE_*`。

## 最小接线契约

- Choice：每个新选项的 `condition_port_id` 是独立 Logic 输入，`unavailable_behavior=hide|disable`，`unavailable_hint` 为静态文本。旧三字段选项无条件可选；旧 Logic 输出只保留。可用性投影不改变 line epoch、冻结正文或画面 revision；接受选择时由服务器重新判断。
- 文本：正文/提示 32767、选项 2048、角色名 256、不可选说明 2048 UTF-8 字节。在冻结前验证实际结果，异常通过独立诊断反馈解除等待，不截断或替换作者文字。
- 候选：上下文包含连接、任务 placement/objective、绑定修订和请求序号；数量上限 20，真实编码预算独立计量；共享有界页缓存。完成历史按需取得，完整快照分块原子组装，发送记录在编码及通道提交后更新。
- 使用位置：typed kind+identity/media_ref 作为键，当前内存编辑优先，未打开内容和已加载只读引用也扫描；索引可重建，项目关闭释放。稳定 node/page/option/field 定位，读取不得变更 dirty 或权限。
- Screen：`transition` 包含 type/direction/duration，层内 `morph_key` 不向用户显示。同屏唯一，复制整屏保留。独立 screen revision，重发不重演，恢复直接末态。非阻塞连续节点只投影最后目标。
- 演出：只保留当前有界源与最新目标，冷媒体有限准备 3 秒；源/目标在过渡期间声明实际媒体需求，结束释放临时需求，沿用既有 3 包/10 包规则。

## 证据边界

状态随真实实施更新。`USER_ACCEPTED=NO`；`RELEASE_READY=NO`。不会用编译或代理检查替代用户验收。

## 当前交付

2026-10-01：A—F 施工和综合场景实机复核完成，三个现场发现的缺陷已修复并重新交付。`IMPLEMENTED=YES`、`AGENT_VERIFIED=YES` 表示本记录列出的工程验证；`NEED_USER_VERIFICATION=YES`、`USER_ACCEPTED=NO`、`RELEASE_READY=NO`。没有提交、推送、PR 或 GitHub Release。文件明细见 `Delivery.json`，源码快照见 `SourceSnapshot.json`。

- Studio：`E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`，自包含 Windows x64 Release，ProductVersion `0.3.3.5`，142611670 字节，SHA-256 `A760F4A1D0A340220F561AD85CA0D9C4A395FEB4156DCD592887D2EFCB481B57`。
- Java：`E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.5.jar`，1855905 字节，SHA-256 `03A502557A7F6E26A391CBAFC0EC43811E828AD82510F41132BD78AC341B0941`。
- 最新正式 EXE 实际导出的综合包：`IntegratedStudio0335-Acceptance.dgrs`，24069 字节，SHA-256 `321803082FFFEB60D18A57799B3DA24D51216993C8B8D11AA634E7C104838CB3`。服务器部署副本完全一致，generation `7056fa944dea`。Acceptance 是候选文件名，不表示用户已验收。
- 修复前／早期单项的五个导出包保留在 `.tooling/0335/DiagnosticExports`；迁移清单和哈希见 `SupersededExports.json`。`NativeExportRoundTrip.json` 是旧诊断差异，不是最终通过报告。

## 需求逐项落点

表内“已实现”说明代码状态；证据列区分自动校验和真实 UI 操作，不表示该条已由用户验收。

| ID | 已实现的行为／主要源码 | 校验证据 |
|---|---|---|
| A01 | `DynamicContentEditor` 在每个 Choice 选项旁显示风险，不改写文案 | Core 动态预算测试；正式 Studio 选项截图 |
| A02 | `DynamicTextBudget` 精确计算静态／当前资源名称；玩家名、等级、数量用字段预算估算，缺失名称明确未知 | Core `DynamicBudgetUsesTypedCurrentNamesAndNeverChangesAuthorText` |
| A03 | `SessionTextLimit` 和 `CanonicalSessionSavedData` 在冻结前校验实际 UTF-8 字节 | Java 2048 字节边界、超限拒绝和未冻结替代文字探针 |
| A04 | 独立 `CanonicalSessionNotice` 解除待响应状态；失败不选择、不推进、不截字 | Java 冻结／选择探针及协议往返 |
| A05 | 重标定容量 profile；使用最终 `DialogueFontScale` 与 `CanonicalDialogueLayout` | Java 枚举 GUI factor 1—32、逻辑高度 240—2160 的生产布局包络 |
| A06 | 两种不能互相替代的容量几何；一次 10% 余量；保留本地分屏 | Core 容量／分页测试；profile 的 raw/safe 为 252/226 和 174/156 |
| A07 | 未就绪头像只读取当前引用，不复用上一引用图像 | `CanonicalDialogueRenderer` 引用核对；未做独立双头像冷加载实机录制 |
| B01 | `ItemSlotStrip` 单体、组统一单格，组不展开四格 | 实机任务详情／HUD 单格截图 |
| B02 | `TaskPresentationPages` 游标跨页轮播全部有限候选 | Java 121 项完整可达探针；实机轮播出现第一页之外的 Candidate 23 |
| B03 | `ItemCandidatePopover` 250ms 展开、200ms 离开关闭，最多 5×4，末页按数量收缩 | 实机 45 项 20／20／5 三页 |
| B04 | 页脚点击、滚轮、原生 Tooltip、边缘摆放；输入由浮层先消费 | 实机点击前后页／滚轮／Tooltip；修复末页收缩离开和零滚轮吞按钮事件 |
| B05 | `TaskItemPreview` 常规快照仅摘要，具体候选按页取得 | Java 121 项探针；`TaskPageServer` 单页投影 |
| B06 | 请求／响应独立字节预算，连接／任务／目标／修订／序号授权和限流 | `TaskPresentationPage`、`TaskPageServer`、候选字节测量与超大 NBT 占位探针 |
| B07 | 16 页／4MiB 客户端缓存、4 个在途请求、16 个轮播上下文；离服清理 | Java 生命周期探针；实机反复翻页、重连及正常断开采样，最终缓存／请求归零 |
| B08 | `TaskSnapshotTransport` 完整分块、原子替换；历史摘要和目标详情按需分页 | 1000 个任务快照、2000 个历史目标探针；实机实际 NPC 交物历史完成次数 3；早期调试击杀 3／3 单独保留 |
| B09 | `CanonicalTaskNotifications` 仅在全部编码和通道提交后登记发送状态；失败限速重试 | 隔离服务器真实编码／发送故障注入：状态不变、后续重试提交；日志 task-send-fault-final.log |
| C01 | 右侧 Inspector 默认折叠、固定高度结果滚动，复用平滑折叠 | 正式 EXE 的默认／展开截图 |
| C02 | Actor／Actor Group、Item／Item Group、Session、Task、头像／画面／语音／音乐 | `ScanLoadedUsages` 与项目 detached 扫描；实机画面媒体两处引用 |
| C03 | typed kind+ID／media_ref；字段引用、声明与头像间接使用分别标注 | WPF typed 同 ID 不同种类测试；普通文本不作为引用 |
| C04 | 当前内存优先；项目磁盘未打开内容、已加载只读引用可查询 | WPF 未打开子图语音测试；项目索引读文件，不保存隐藏编辑器 |
| C05 | 按 Node／Page／Option／Field 导航；只读 Host 禁编辑，查询不改 dirty | WPF 导航前后结构／dirty 校验；实机使用位置点击定位 |
| C06 | 编辑／资源刷新时重查；序号拒绝旧结果；项目关闭取消并释放 | WPF 延迟结果拒绝测试；关闭／Dispose 路径核对 |
| D01 | 新选项稳定条件 Logic 输入；未接线默认 True | Core 新建／复制测试；Java 无条件选项；实机第三项 |
| D02 | False hide 或 disable，True 正常可选 | Java 真值表；同包双玩家菜单截图 |
| D03 | 条件卡片默认折叠；disable 时才显示说明字段；两入口共享编辑器 | WPF／正式 EXE 条件卡片截图 |
| D04 | 沿用已有 Logic 与 Objective 语义，未新增状态节点或库存监听器 | 修改清单与基线核对 |
| D05 | 服务端投影与点击再核对，禁用／未知选择不推进 | Java 禁用请求不产生选择记录；实机禁用点击仍等待 |
| D06 | 可用性 revision 独立；Logic 刷新保持 line epoch、冻结内容、screen revision | Java epoch 测试；实机刷新后图层边界保持末态 |
| D07 | 全不可用保持等待；稳定 ID 键盘跳过禁用项；自动播放不代选 | Runtime／客户端逻辑测试与源码核对；实机 Tab 跳过禁用项 |
| D08 | 增删／拖序／复制维护端口，连线删除告知，Undo 恢复 | Core 连线删除确认／撤销／复制重键测试及现有 WPF 编辑测试 |
| E01 | none／fade／slide／wipe／random_lines／morph，按效果校验方向与 0—60 秒 | C#／Java schema 和演出采样；最终同包实机 morph＋12 种普通转场／方向 |
| E02 | 目标 Screen 完整末态，转场参数随目标携带 | 纯采样 t=1 精确目标断言；实机末态截图 |
| E03 | 隐藏唯一 `morph_key`，区别于媒体引用 | C#／Java schema；同屏重复键拒绝 |
| E04 | 复制 Screen 保留对应键；同图不同对象不串配 | Core detached 旧层迁移／复制和同媒体不同键测试 |
| E05 | 每层真实锚点矩形插值、换纹理交叉淡化、增删层淡化 | Core／Java 位置宽度及换纹理测试；Minecraft 源／中间／目标矩形截图 |
| E06 | 演出与正文并行，连续非阻塞 Screen 只呈最后目标 | `CanonicalSessionScene` 独立有界目标；没有新等待节点／时间轴 |
| E07 | screen revision 独立；重发不播放，恢复末态，中断捕获当前源 | Java 重发／中断探针；实机条件刷新不重播 |
| E08 | 源＋目标需求、临时 pin；3 秒冷准备；结束和断线释放；3/10 包规则保留 | 生命周期／演出探针；真实 decoder、纹理、sprite、pin 采样；重置后与断开后归零 |
| E09 | 原画面预览区选择源、播放、返回编辑；保留对话／选项参考和编辑手柄 | 正式 EXE 播放中／末态／返回手柄／两种参考框截图 |
| F01 | 同一 Canonical Runtime，全链路 schema／协议／持久化／导出联动 | Core、WPF、Java 构建及协议探针 |
| F02 | 「介绍说明」已更新；正式 EXE 导出同包再部署复核；权威产物已提升 | 正式 EXE 导出成功 UIA 状态、文件哈希、服务器同包、两客户端及交付核验 |

## 自动验证

- Core 最新聚合端口修复后的全套（CoreAggregateFinal）：470 通过，12 跳过，0 失败，共 482。
- WPF 功能套：624 通过，1 跳过，0 失败，共 625；原有两个 300 节点规模测试单独运行，2 通过。合计 WPF 626 通过，1 跳过。
- 最后聚合端口／保存／引用位置相关 WPF 子集另复跑 30 通过，0 失败；与上述全套重叠，不重复计入总数。
- C# 不重复合计 1096 通过，13 跳过，0 失败。跳过保留原测试约束，不等同于通过。
- Java 最终 `spotlessApply build construction0335Probe` 成功，包含 Checkstyle main/test。改动范围格式化，未把历史未改动文件的全局 Spotless 违例假装消除。协议、Runtime、Server Service、Client Model、Creator UX 的既有探针另有成功日志。
- 新施工探针覆盖 121 候选、超预算 NBT 占位、绑定版本失效、1000 任务完整分块、乱序未完成不替换、2000 历史目标完整遍历、冻结文本边界、禁用选择、菜单 epoch、演出重发／中断／末态与生产容量几何。
- 最终 JavaLiveFixAccepted 与 JavaLiveFixRegressionAccepted 均通过：构建、Checkstyle main/test、施工、Session Client Model／Forge Routing／Codec／Server Service／SavedData、Task Event Persistence／Forge、Story Forge Coordinator。新增实际采样结算事务与历史、精确传输关闭隔离、关闭后旧帧拒绝等回归。
- `git diff --check` 的本轮源码范围通过。没有 staging。

第一次 WPF 完整运行遇到旧规模测试 60/90 秒超时并留下中止日志；既有 Screen 测试的预期改为比对已经提交的 detached morph key 快照。关闭实机负载后把两个规模组独立运行、超时 300 秒，均通过。失败／重跑的历史证据仍在 `.tooling/0335`，最终结果复制到 `evidence/automated`。

## 综合同包实机记录

环境：隔离 `.tooling/0335/Server`、`Client`、`Witness`；正式交付 EXE 使用独立设置和 IntegratedProject。客户端账号 `Native0334`、`UiFix0334`。所有缓存／临时工具位于 E 盘。夹具节点与 45 个 item 绑定由脚本准备，正式 EXE 实际编辑、保存、导出；不冒充从零手绘或逐个手工绑定。

1. 正式 EXE 实际定位 Choice，查询引用、播放独立源预览、切换对话／选项参考框、返回编辑；中文 IME 组合和提交“测试”成功。原生 Nominator 将 Actor 绑定实际 Villager。灰黑、浅色、蓝色主题分别检查菜单与原生 Tooltip。
2. 最新 EXE 实际修改 Choice 文案并保存，再恢复并保存，触发聚合端口同步；原生菜单导出显示成功。最终包所有 Story 节点同方向端口 order 唯一，ID 保持稳定。逐字节核对部署副本后 reload 成功；服务器 02:00:26 记录 generation 更新为 `7056fa944dea`，之后最终触发不再出现端口冲突。
3. 实际进入区域触发 Story→Task。45 候选菜单按 20／20／5 分页，点击、滚轮、原生 Tooltip、ESC 浮层关闭均核实；重复 10 轮、20 次翻页有操作和缓存采样。主题、重复翻页与故障注入子项使用最终 Java 构建、但发生于最后 Studio 聚合顺序修复前的导出夹具；与最终包的 item／item group／media 七个条目逐字节相同。最后同包完整故事与这批子项证据分别记录。
4. 实际持物对 NPC 交互，消耗一件匹配物品，Task SETTLED、progress=1，正常接入第一会话；Task 公共 Logic 经 Story 进入 Session 条件。主玩家 True 显示三项；第二玩家走独立旁路、False 隐藏一项并禁用一项。禁用点击不推进、Tab 跳过禁用有单项证据。
5. 1026 字台词实际本地分页，同一个作者页的语音 backend 只启动一次；第二作者页换头像并重新启动一次。三张复制 Screen、十个同图不同 key 对象加一个半透明对象，共 11 层，morph 移动／缩放／交叉淡化和后续独立会话接线均可观察。
6. 从第一 Choice 进入第二 Session，最终同包逐一复跑 fade、四方向 slide、四方向 wipe、水平／垂直 random_lines、none，共 12 种普通演出；各自保留中间与末态截图及实际操作时间。最后 Choice 正常进入 terminate，Story 状态为 TERMINATED、wait NONE。
7. 历史界面显示实际 NPC 交物完成次数 3。早期 `emit_kill` 调试生成的 3／3 击杀历史只是独立分页子项证据。最终综合链路不借用该调试事件代替交物。
8. 实际重置第二玩家 Story 后，旧会话关闭、演出／音频 pin 清除；等待后纹理为 0。实际重连无旧会话，最后两个客户端通过原生 Disconnect 退出，页缓存、请求、轮播、纹理、decoder、sprite、scene/audio pin 均为 0。

## 现场发现并修复的缺陷

- `CanonicalTaskSavedData.sampleObjective` 成功提交实际交物／持续采样结算后未 captureCompletions，重复 Story 会覆盖任务实例导致历史丢失。现在在 inventory commit 成功、替换并重建索引后捕获历史；失败提交不留历史，重复采样不重复计数。自动回归和实际 NPC 历史复核通过。
- `CanonicalSessionForgeManager.cancelByStory` 只取消服务端对象，客户端未收到关闭。现在向旧 snapshot 的精确 transport／Story 发送关闭，其他玩家／故事不受影响；客户端记录关闭传输 fence，拒绝旧帧复活。自动隔离／旧帧回归和实际 reset、重连清理通过。
- `AggregatePortProjection` 独立 Flow／Logic 聚合的 order=0 冲突，正式编辑保存后导出包被既有 Story Runtime 拒绝。现在仅在生产聚合端口时消解同方向重复 order，保留有效顺序与稳定 ID，不放宽 Runtime 规则。Core 全套及相关 WPF 子集通过；最新正式 UI 导出和最终整个故事通过。

修复前 Final／Verified 导出确实失败，日志保留 `story.port.order`。最早夹具多个 End 汇到同一 Flow 输入触发现有路由歧义，通过独立桥接 Action 修正夹具。首轮第三方 CustomNPC+ `/summon` 失败于 `tempInvisIds`，故本轮实际绑定使用 vanilla Villager，不宣称已修复第三方模组。夹具强制默认模式造成死亡、IME 中文模式拦截游戏命令、未保存中间修改导致文档恢复 clean 等定位过程均保留，失败截图不算通过。空 Project 启动提示、旧 Forge 更新查询与签名提示仍在日志中。

## 真实采样与基线比较

计数采样 instrumentation 只读取隔离 Java 进程状态；故障注入是单独的测试 agent，两者均未打包进入交付 JAR。任务故障 agent 只命中指定测试玩家：编码失败与发送失败各一次，previous／notification／generation／revision 等已提交状态保持不变，后续 revision=4／7 分别重试提交。该证据不证明公网接收确认。

| 正式 JAR | 样本 | GUI draw CPU 中位／P95（ms） | draw 起点间隔中位／P95（ms） |
|---|---|---|---|
| 0.3.3.4 | 15.017 秒／901 帧 | 0.7275／1.2144 | 16.641／18.0844 |
| 0.3.3.5 | 15.021 秒／901 帧 | 0.7104／1.2100 | 16.646／18.3086 |

同一 Windows 主机、1280×800 客户区、11 层、相同 1026 字正文、60 帧上限的暖场景。测量的是真实 Session GUI draw 方法 CPU 时间和连续 draw 起点间隔，不是 GPU FPS；世界背景不完全相同，不能推广成所有机器不卡。早期错误基线包和 Merchant 覆盖窗口已排除，使用发布路径的 0.3.3.4 JAR 作基线。

最终全转场采样最大页缓存 6、27660 字节，在途 1；前一综合跑最大 32126 字节。纹理最多 3，decode pending 2／queue 1／upload 1，scene pin 2／audio pin 1。静态源 11 层，random_lines 最多 32 strips×11=352 个输出 sprite，不改变 96 个源 sprite 捕获预算。最终两个客户端断开样本相关计数全部归零。原始 CSV 和汇总 `LiveMetrics.json` 保留。它们不是长期 soak 结果。

## 待用户审计与未覆盖边界

工程施工与上述自动／本机实机复核已完成；仍为 `USER_ACCEPTED=NO`、`RELEASE_READY=NO`。未覆盖其他显示器／DPI、第三方 CustomNPC+ 故障修复、跨公网延迟／断线竞态完整矩阵、长时稳定性和人工语音听感。语音素材是有效 Villager 音效，证明播放与本地分页不重启，不冒充中文长台词配音验收。头像切换已实际观察，但未单独覆盖任意两张头像冷加载时序。

## 收尾状态

隔离主服务器已 save-all、正常保存玩家和世界并卸载三维度；主／第二／基线客户端与基线服务器均已退出，测试监听端口已释放。客户端 instrumentation 启动参数已撤下，附加 server agent 随测试服务器退出卸载。正式 Studio 仍开着综合工程供审阅，其他用户进程保留。最终日志 `LiveServerFinal.log`、`LiveNativeFinal.log`、`LiveWitnessFinal.log` 与原始 CSV 已归档；旧失败材料仍可追溯。

四个 WPF 超时产生的 `.dmp` 转储合计 9997106432 字节，是之前所说“约 10 GB”的来源，并非正式客户端大小。历史直接删除命令曾被自动审批阻止，仅返回 `blocked by policy`。2026-09-30 23:46（Asia/Shanghai），按用户要求使用 Windows `SendToRecycleBin` 将四个文件移入 E 盘回收站，当时逐项核验路径、大小和原目录；历史记录保存在 `RecycledTestDumps.json` 与 `RecycleBinVerification.json`。

2026-10-01 最终复查：原测试目录 `.dmp` 数量仍为 0，但历史记录中的四个回收站路径目前均已不存在，Shell 回收站枚举也未找到相应转储；消失原因未核实，不能再声称可恢复。本轮没有永久删除或清空回收站。当前核验见 `FinalShutdownAudit.json`。日志、TRX、源码、正式 EXE/JAR 和用户数据保留。
