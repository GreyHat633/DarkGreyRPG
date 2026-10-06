# 2026-09-22 Windows 原生实机验证（进行中）

使用 Windows UI Automation / Win32 输入与 CopyFromScreen；Studio 隔离候选和正式固定路径同 SHA，正式 JAR 单客户端，用户原有 Studio PID 76620 未操作。此记录是代理执行证据，不是 USER_ACCEPTED。

## 留声机

- GR-02：`0922-range-on/closed/reopened/off/off-closed.png`。在已保存半径 16 设备上开启范围，关闭 GUI 后仍可见，重开显示“隐藏范围”，再次关闭显示后世界不再显示框。
- GR-04：`0922-radius-draft/draft-close/draft-reopen.png`。半径 16 改为 1，不保存关闭，再打开恢复 16；正式配置未被草稿覆盖。
- GR-06/07（已有有效 QQ 曲目）：`0922-preview-shared/preview-close/preview-reopen/preview-cached.png`。点击试听出现波形、进度和暂停按钮；关闭再开试听归零，设备仍显示正在播放，再点试听立即开始。缓存前后清单 `../0922-cache-preview.json` 和 `../0922-cache-reopened.json` 无路径/大小/修改时间差异；媒体文件 3426219 字节。文件复用已观测，未单独计数全曲分析调用，不能据此宣称全部 GR-07 性能矩阵通过。
- GR-01 部分：`0922-world/gramophone-open.png` 可见独立木底座、唱片、唱臂及号角。四朝向/手持尚未覆盖。
- GR-10 部分：设备播放与试听并存、关窗不停止设备已观测；多个设备及异曲尚未覆盖。
- 尚未实际听音，界面“播放中”不证明声音质量、分类音量或 Master 数学关系。

## Studio 组合

- GP-08/19 部分：`0922-group-drag/group-undo/group-redo.png` 从组底部空白拖动 (-60,-30) 像素，起始节点与组一起移动、连线更新，Ctrl+Z 一次还原、Ctrl+Y 一次重做。
- GP-17 部分：`0922-group-protected-delete.png` 选中包含固定起始节点的组按 Delete，组、起始节点和边均保留。
- GP-14 部分：`0922-line-g-group.png` 中文输入法接管 G 显示候选，没有组合副作用；Escape 取消，切英文后继续。文本框 IME 提交仍需独立覆盖。
- GP-01/02：右键菜单实际新增第二台词，Ctrl 选择两个未分组台词，英文 G 创建一组；`0922-two-lines-selected/two-lines-g.png`。
- GP-06/07：点击两个组空白（第二个 Ctrl 点击）显式选择，点工具栏组合，新建父组且保留两子组；`0922-select-two-groups/nested-created/nested-fit.png`。适应视图纳入整棵树及全部节点。
- GP-16/19 部分：父组右键取消组合，仅父框消失，两子组、三节点和原边保留；Ctrl+Z 一次恢复父组。`0922-parent-menu/ungroup-parent/ungroup-undo.png`。
- GP-20 部分：Ctrl+S 后 sidecar 持久化三个组，其中父组 members=[]、groups=[两子组ID]，两个叶子组分别含 start 和两台词。没有把父子关系写成运行节点。重启恢复仍待后续动作。

截图及 UIA 树均位于本目录 `0922-*.png/.txt`。测试工程原有长台词导出包保留；新增空台词仅用于组合测试，不能代替正常 Story 触发用包。

- GP-20 续：关闭隔离 Studio 后以相同设置重新启动，进入会话并适应视图，父子组三层框及三节点/连线恢复，见 0922-reopened-nesting.png。展开状态回到默认，自动 bounds 重新适应长台词。
- 正式客户端最小化恢复后输入不响应，窗口可重绘；关闭窗口请求也未退出。16:09 世界已保存记录确认后，仅终止经命令行校验的隔离 PID 83148，并重新启动正式 JAR（region-0922.log）。不把这次强制退出记作正常退出验收。


## 正常 Story 入口端到端

- Studio 实际连线 Start 条件1 → Session，Inspector 设置 repeatable、区域 (-104,70,207)、半径3、维度0，并保存导出。
- 第一包 Native0333Region.dgrs 被包加载器接受，但运行时启动拒绝组合测试的空台词。日志 `.tooling/0333/region-0922.log` 16:28:46 明确指出 `session.line.property.required`。保留此失败样例。
- 在 Studio 填写空台词后再次导出 Native0333RegionValid.dgrs，2890 字节，SHA-256 `952aa87df25e79f9879a2e6f243ce1be4804f4ff328500d3e5014781e44e8074`。同文件安装至隔离客户端 StoryPackages；`dgr reload` 接受更新。
- 使用原版 tp 离开区域再进入（未执行 session start 命令），正常区域触发链显示 BEGIN0333 长台词。截图 `0922-outside-region.png`、`0922-region-trigger-result.png`；日志16:31:34/16:31:50对应两次位置变化。
- 后续修复：Studio 导出前检查所打包的 Session 所有台词非空（含未连接节点），草稿仍可保存编辑；拒绝时不改动已有输出。StoryPackageTests 新增空串/空白正文回归，两者通过。此修复仍需最新 EXE 原生复核。

- 最新 EXE e4bb98dc938c561c5facad4435a54d26de77092c010aac3087bc17b2a39fc91b 原生复核：清空测试台词→保存成功→导出 BlankRejected0333.dgrs，被明确拒绝（0922-blank-rejected.png）；Test-Path=False。正文已恢复并保存（0922-restored-valid-fixture.png）。

## 设置、窗口与历史续验

- 正式 JAR 655070ec... 上实际测试 1280×800、640×480、1600×900 客户区，GUI Scale 2；小窗口 150% 字号正文仍在底板内。未测试其他 DPI 或 Unicode 强制模式。
- 小窗口切换白/蓝主题；文本页将速度移到最右端显示“立即显示”；声音页仅 Voice/BGM 两项。大窗口实际拖动标题、拉伸右下角，内容与底栏仍可访问。
- 透明度 0% 时正文预览仍可见、底板消失，100% 恢复底板；没有头像，不能据此证明头像透明度。恢复默认后为深色、100%字号、80%底板、速度30、等待3。
- 多次尺寸/字号改变后打开历史，仍只有一条完整原作者页，含 BEGIN0333 与 END0333。证据：0922-dialogue-small、0922-small-white/blue/text/speed-max/sound、0922-settings-dragged、0922-resized-text、0922-opacity-zero/full、0922-settings-defaults、0922-history-after-resizes。

## 任务实机发现与修复

- create-task-fixtures.py 生成隔离短目标、长目标、三个并行目标，Studio 加载并实际导出。它们不是 UI 手工创建任务节点的证据。任务通过 dgr task start 调试命令启动，不冒充正常 Story 任务推进。
- 测试 Session 原先没有连接结束节点，先后出现 session.flow.unconnected 和 Selected End is not a Flow output on the aggregate。补齐 Session End、Story 聚合出口及 terminate 后由 Studio 导出 Native0333TasksComplete.dgrs。离开可重复区域后，会话正常结束，I 打开正式任务面板。
- 旧 JAR 的 I 面板目标下缺失直接奖励（0922-i-real）。查明 TaskRewardPreview 错用 logic_out，而目标正式输出是 logic_status。修正端口并修正旧探针；新增会先由 CanonicalTaskRuntime 校验的完整任务样例，防止投影测试再次使用不合法端口。
- reward-port-fixed 探针通过，新 JAR e7e1d7e2fb9d5fd99deba3ff6d59ffc54e6cfd77eeebf661ec55d42d319e37c6（1693231字节）实机显示“经验 +5、经验 -2”，隐藏零值（0922-reward-fixed-task）。该哈希为中间验收构建，后续 HUD 修复会替换。
- 启动另外两项后，三项追踪均保留，三个并行目标都在详情列出，仅第一个显示直连奖励。小窗口明确提示“另有10行”，未静默丢弃。
- 同时发现 HUD 绘制在 I 面板之上遮挡进度（0922-multi-details）；已移除菜单上方的 HUD 绘制及其旧滚轮接管，HUD 溢出引导改为“任务菜单中查看”。任务详情原滚动保留。最新 taskTracking0332Probe、canonicalTaskJournalProjectionProbe、reobfJar 通过，原生复验进行中。
- 长中文目标目前会将项目符号单独换到一行，仍需评估紧凑排版；当前不得把 TK-01/TK-02 整项判定通过。计分板、实际杀敌/物品提交/背包满、绑定改名矩阵仍未完成。
- 两次重启前均先在游戏菜单保存退出世界，日志确认所有维度卸载。窗口关闭后 Java 后台未及时退出，核对隔离进程命令行后终止残留；未操作用户旧 Studio。

- 最新 HUD 修复正式 JAR c06ac801190364c4d873dbfd4aec46020d5bbe66c7b2febf1c81352d6263f635（1692904字节）原生复验通过：0922-hud-fixed-multi 显示三目标和独立右侧计数，无 HUD 覆盖；0922-small-task-fixed → 0922-small-task-scroll 为 640×480 下真实滚轮操作，第三目标及 0/5 可访问。

## 定义更新与存档不匹配诊断

- 原三个任务写了 `Pig`，正式 Forge 事件标准化为 `minecraft:pig`，因此不能作为实际击杀匹配的有效样例。第一轮 0/3 不能证明计数回归。隔离平台已扩为 3×3。
- 修正目标后导出 Native0333TaskKills.dgrs，reload 触发 `task.snapshot.resource`。这些任务由调试命令使用独立 story_instance_id 启动，不属于实际 Story 实例，故该次 Story 代际退役没有移除它们。原始崩溃：hud-0922.log / crash-2026-09-22_17.48.07-server.txt。
- 第一处异常来自 Journal 展示投影：新增 projectForDisplay，对不匹配记录显示任务ID/原状态/诊断，不套新目标、不修改快照；严格 project 和 CanonicalTaskRuntime 仍拒绝不匹配。
- 原存档重启复验进一步暴露 bind 阶段失败（recovery-0922.log / crash-2026-09-22_17.56.30-server.txt）。新增展示边界保护：pendingRaw 已保留时，只读解码为不可用记录，提示恢复匹配故事包；不丢弃数据，不放宽 bind，不让 UI tick 异常关闭服务器。此保护原生复验进行中，不宣称不匹配任务已经可继续运行。
- 新增回归验证 pending 展示不改变 NBT、玩家过滤、原状态保留、无伪造目标/完成。0333-task-pending-display.log 投影探针和 reobfJar PASS。
- 长中文 HUD 改为先分离项目符号再折行，避免孤立符号一行；待最新 JAR 原生复核。

- 新 JAR 8d3f5ba3fa0ade59ea1aa0ab4f0e016aa94e5bee7c305cf9523fc1330bbae75b 原生恢复：原崩溃存档成功进入，0922-pending-journal 显示原任务ID与 task.snapshot.resource 诊断，服务器继续运行。恢复 Native0333TasksComplete.dgrs 后 reload，0922-restored-tasks 恢复原三项任务及0/3，没有清空数据。旧定义错误期间 EventAdapter 按既有路径每秒记录警告，恢复后停止。
- 0922-long-bullet-fixed：长中文目标的项目符号与第一行正文一起显示，减少一行空耗。

## 真实击杀、结算与重进不重复奖励

- Native0333LiveKills.dgrs 保留三项旧布局任务的原语义，另增 Native0332:live_kill_task，实体目标为 minecraft:pig。Studio 实际导出并安装，调试 start 仅用于启动此隔离任务。
- 原版 summon 生成不移动、1点生命的测试猪，随后真实左键攻击。不是向任务运行时注入计数。第一次命中后 I 菜单为1/3（0922-live-kill-count）；第三次命中出现任务完成提示（0922-kill3-hit），完成页为3/3（0922-live-completed）。
- 原有三项追踪已满，新任务未抢占。完成页未将奖励预览伪装为领取清单。
- 保存退出后只读检查 NBT：0922-task-save.json 记录 SETTLED、进度3、reward_0/1/2 全部true；0922-kill-receipt-check.json 依据正式交易键算法逐一匹配三个唯一收据（+5、-2、0各一个）。零值不显示，但原事务收据保留。
- 重进同一世界后历史“完成次数:1”，只保留结算摘要（0922-reentry-completed）。再次正常保存退出，0922-reentry-check.json 确认经验总值7及收据内容均与重进前相同。经验总值含杀敌来源，不能将7解释为纯任务净奖励。
- 本次截图中未出现“背包满/奖励阶段”误提示，但未作逐帧录像；真实满背包、物品指名、NBT名称、多玩家发奖仍未覆盖。

## 台词输入、容量边界与重开（TX 原生续验）

- 实际键盘 Enter、Shift+Enter 均未插入 CR/LF；失焦后保存分别为 NativeEnter0333 / NativeShift0333。多行剪贴板粘贴被拒绝并提示“单句不支持手动换行，请拆分为多句”，原输入保留（0922-multiline-paste）。画布上 Undo/Redo 后保存分别恢复上述两值。
- 实际微软拼音候选窗见 0922-ime-candidate；Enter 确认拼音保存为 ceshi，没有误插换行；空格选择中文保存为“测试”（0922-ime-chinese）。此结果符合该输入法 Enter 提交拼音、空格选候选的行为，不将 Enter 的拼音结果描述为中文选词。
- 文本框输入 gG 保存为正文，三个组及其成员关系未改变；关闭隔离 Studio 并重新启动，从 Story 双击进入 Session，节点与 Inspector 均显示 gG（0922-tx-reopen-confirmed）。全过程 page_id 保持 page_684b1e0fbf1846e59f08bf75664c5721。
- 容量测量字符串：基础为 (W×20 + li)×2 + W×14；追加 i / l / ii 分别显示336/337、337/337、338/337。前两者保持正常接近提示，338变红并显示超限说明；节点在提交后同步。对应 0922-capacity-336/337/338/refit。
- 338/337 的超限内容实际保存并通过 Studio 导出 Native0333Boundary338.dgrs（0922-boundary-exported），不是直接打包脚本产物。截图及包仅证明该单条边界，不替代最大字号/头像/强制 Unicode 的客户端排版矩阵。

## G 拖动、取消与修饰释放

- 第一轮 G 拖动仅改变位置，未改变成员；该次没有确认按键焦点，保留为无效尝试，不计通过。重新点击画布/节点确认焦点后，已选和未选成员均可 G 拖出 B，B保留另一成员、外层C保留A/B（0922-g-drag-selected/unselected）。
- 相同起终点执行 G 拖动后按 Esc，再松鼠标，保存的完整 sidecar SHA-256 与基线均为16378ed632446d029c179124045c99f921a98ae1c216be6100327cad0c48ca1c（0922-g-escape-confirmed）。位置及层级全部恢复。
- 拖动中先松 G 后松鼠标，成员归属完全不变，仅位置从(135.6386554621849,306.26655462184874)变为(824.0876648038252,31.49085352496604)；未补建组合（0922-g-release-before-mouse）。一次 Undo 回基线、一次 Redo 回移动结果，后者sidecar SHA-256为21089667b7c43e5482134912062b0ce1ae5521170ebf857279d6287acfd7d2df。
- 将该成员 G 拖入较小A框，A保留start并加入该成员，B保留主台词，C结构不变，A自动扩框（0922-g-transfer-small-group）。一次 Undo/Redo 分别恢复转移前后完整 sidecar，实机验证组合归属与位置同一事务。
- 未据此关闭窗口失焦/捕获丢失、KeyRepeat、拖动中再按G、全部修饰键、复制保护和大图性能等剩余矩阵。
- 续验：拖动中由临时 WinForms 验收窗实际夺取前台焦点，释放鼠标后 sidecar 与基线逐字节一致（0922-g-blur-cancel），临时窗随后关闭；未操作用户 Studio。拖到一半才按 G，松手成功移出 B（0922-g-during-drag）。因此失焦取消、途中进入/退出模式已补证；捕获丢失独立路径、KeyRepeat及完整修饰矩阵仍未单独覆盖。

## 组空白焦点缺陷与复制实机复验

- 原版点击组空白后将焦点置于 GraphRoot，而 Ctrl+C/V/Delete 绑定在 GraphCanvas；真实按键未执行。修复组 Thumb 的 PreviewMouseLeftButtonDown 改用 FocusGraphCanvas，未变组合/图语义。
- 新增 GroupBlankClickFocusesCanvasAndRoutesDeleteAsOneUndo，实际路由鼠标事件检查键盘焦点，并从聚焦画布路由 Delete；组及两个成员一起删除，一次Undo恢复。0333-group-focus.log 共6项通过。
- 新自包含EXE为1c2beee5da4ffa226ec711dbfa51f901fc3a728a412d44f0c263935bc29168de，142429398字节，ProductVersion0.3.3.3；候选与固定dist一致。
- 新EXE同一组空白Ctrl+C/V实际生成2个新台词和1个新组；总6节点/4组且ID全唯一，原2条范围外连线没有克隆（0922-fixed-group-paste、0922-group-copy-check.json）。本样例组内没有连线，不能代替组内连线重映射验证。一次Undo回原会话SHA-256 aa9a2a8bf58db09fcd10244480823e18b6d2a9e4dc81b8bffc324b01c2f4cc32；一次Redo回粘贴结果0193aa40aa0723b9786e2ae6643a39253fad2d1843576bda12194c6c944fbe70。
- 外层C包含固定start时，复制后的粘贴明确提示“起始 是固定节点，已拒绝整次粘贴”（0922-protected-group-paste）；没有部分粘贴。取消提示后Delete也不改变原会话。
- CAS分工复核返回“该项目未由CAS接管，请先应用方案”，保持MAIN集成；未修改CAS配置，未创建Worker。

## 选择数量、长选项与死亡前台

- 独立 ChoicesProject 由脚本生成1/2/3/5项连续选择 fixture（不是声称UI手工创作）；新Studio实际导出 Native0333Choices.dgrs，正式8d3f5ba3 JAR安装并通过区域进入Story→Session触发，随后传送离开触发区避免重复启动。
- 1280×800下1/2/3项整体高度不同而中心保持在对话框上方区域（0922-choice1/2/3-layout）；Tab+Enter实际从2项进入3项，方向键+Enter继续5项。
- 自动开时选择页等待4秒以上没有代选；确认后下一句自动推进至5项选择且开关保留（0922-choice-auto-waits、0922-choice5-layout）。5项中4个短选项在第一页，长选项在下一页。
- 长选项1280×800完整显示BEGIN到END；缩至640×480后限制高度并显示滚动范围1–7/20，Tab+End可达14–20/20及LONG_END（0922-choice-small-long-end），Home回首行，PageDown推进文本（对应截图）。没有静默截断末尾。
- Esc实际进入原版暂停菜单，对话保留下层。聊天中执行原版kill，关闭聊天后原版死亡界面和Respawn按钮可用（0922-choice-death-screen）；实际点击重生恢复同一CHOICE5，自动未代选（0922-choice-respawn）。本次死亡发生在选择页，不替代普通台词的自动倒计时重置验证。
- 本次未证明跨服/251或强制Unicode/头像矩阵；这几项继续开放。

## 计分板长名称共存修复

- 640×480原生使用原版scoreboard创建NativeBoard，Native0333=123时任务HUD移左、不覆盖；增加LongScoreName0333=99999后，原HUD保持200逻辑像素宽导致重叠（0922-scoreboard-wide-small）。
- 修正为排版前测量计分板含队伍格式化名称的宽度，正文折行及底栏宽度共同受剩余空间约束。极端计分板占满整屏、剩余不足30逻辑像素时不画HUD，任务菜单仍可查看，不以负坐标绘制。
- 0333-scoreboard-width.log：scoped Spotless、taskTracking0332Probe、reobfJar通过；正式JAR2ef3aa66546ba0d05252c5261c3eb63f86bc55903645ccd694a838149661d28e，1694371字节。待同一存档原生复验。
- 最新JAR同一存档640×480复验通过：0922-scoreboard-fixed-confirmed中HUD右边界收至约285屏幕像素，计分板文本从约365开始，正文按新宽度折行。I菜单仍可访问完整任务，HUD和计分板未覆盖菜单（0922-scoreboard-task-menu）。

## 普通台词自动推进的前台恢复

- 新JAR2ef3aa...普通CHOICE1前置台词显完后开启自动，150ms后Esc进入原版菜单，停留5秒以上；返回截图仍为原台词，随后才进入选择页（0922-auto-line-paused-later/return/after-wait）。证明未在后台推进、返回未立刻跳走；截图时间粒度不足以声称精确到毫秒的3秒等待。
- 设置键在此隔离客户端已自定义为K（options.txt键码37），最初按默认P未打开设置，相关auto-settings-open/actual不计通过。使用实际K进入设置停留5秒，返回仍在CHOICE5前置台词，随后自动到选择（0922-auto-settings-bound-wait/return/after-wait）。
- 历史/聊天/背包/Windows失焦的普通台词倒计时矩阵仍未单独全覆盖；选择页死亡重生已有独立证据。

## 满背包、指名NBT物品与恢复幂等

- RewardProject独立fixture，保留旧任务语义，新增full_inventory_task：真实击杀minecraft:pig一次，同一reward_0含xp+7和Native0332:named_gem×1。Studio实际导出Native0333FullInventory.dgrs（7942字节），reload日志确认5任务/1物品；初次仅替换包未reload导致unknown_item_id/Missing Task，不计产品回归。
- 原版give创建64个带display.Name=NativeNamedGem0333的钻石，debug bind_exact实际指名；作者资源显示名不同。再通过35次原版give填满石头，原生背包截图0922-full-inventory-slots与保存NBT确认36槽均64个。
- summon一血固定猪后真实左键击杀，目标1/1；满背包任务保持ACTIVE，reward_0=false，UI显示“奖励尚未发放完成，服务器将继续重试”（0922-full-pending-confirmed）。原生debug仅启动/绑定，不注入击杀事件。
- 正常保存退出（日志20:25:50卸载维度）后0922-full-reward-pending-check.json断言：宝石64、经验总值1、目标完成、奖励收据不存在。早期save-all在此集成服是Unknown command，0922-full-before-player.json/0922-full-pending-task.json只作初步读取，不作为保存成功证据。
- 重进后原版clear只移除2240个石头，自动重试结算。正常保存退出20:32:37，settled-check断言宝石65、经验8、仅新增一个准确SHA256事务键收据，reward_0=true/SETTLED。+7经验没有在满背包时先行发放。
- 再次真正进入世界（20:35:25登录日志）并正常保存退出20:36:27，reentered-check及reentry-comparison确认宝石仍65、经验及全部收据完全不变。最初几次菜单点击未进入世界，以及误打开但取消的LAN设置页，均未计为重进/联机证据。
- 调试start的full0333没有真实Story实例，结算后既有续体路径记录一次不存在Story告警，不据此宣称正常Story任务启动/续体已验证。NBT物品实际发奖已验证；奖励预览显示名与运行中改指名还需独立观察。

## 奖励预览的实际名称与绑定刷新

- 新开同资源的preview0333任务，I菜单实际显示“经验+7、NativeNamedGem0333×1”，未使用作者资源显示名（0922-preview-nbt-name）。
- 先尝试debug覆盖不同NBT定义被既有冲突保护拒绝。随后实际用游戏内物品指名器选中同一资源ID、确认“ID释放”；任务预览自动变为“无×1”（0922-preview-unbound）。
- 将UpdatedGem0333实际放入指名器目标槽，点击“物品指名”成功；关闭工具重开任务菜单，同一任务仍0/1，预览变为UpdatedGem0333×1（0922-rebind-place/confirmed、0922-preview-updated-name）。无reload/重进，不修改持久化资源ID或任务定义。
- 早期鼠标批处理部分点击未命中；增加移到目标后120ms等待并逐步截图确认后完成真实放入/绑定。失败点击不计验收。
- 此后为预览测试清除了之前的65个宝石，故此前重进幂等结论仅对应20:36保存的对照文件，不将现在的背包数量当成原奖励重复/缺失。

## 最小窗口、头像、150%与正常Story任务接续

- Studio实际导出Native0333Profile.dgrs（11486字节）。首次失败为测试夹具把头像放在项目media而非resources/media；修正夹具后成功，未修改产品导出逻辑。
- 21:15 reload后通过进入既有区域启动profile_task，任务列表可见0/1；未使用debug task start。真实左键击杀1HP牛后，任务完成提示与头像会话同时出现，日志未出现本次Story续体不存在告警（0922-profile-task-started/task-kill）。召唤与传送仅准备隔离验收场景。
- 客户端640×480、逻辑320×240，通过设置UI切换150%。真实默认头像（复用项目PNG）显示，容量338样例三行完整显示（0922-profile-338-font150），与Profile的wrap125/rows3及SAFE337相符。SAFE是作者余量，338仍可导出运行。
- 61个连续W：第一页60个、第二页1个，未裁掉尾字；记录显示完整61个原句，不将本地尾页单列（0922-profile-61w-page1/page2、profile-history）。
- 当前中英混排原句显示时，通过原版Language实际将Force Unicode Font从OFF切到ON；options.txt确认true。返回仍同原句、头像存在、三行显示，并逐页读到UNICODE_END（0922-profile-forceunicode-on/page、profile-unicode-page2/3/4）。此处不将字体切换扩展为所有代理对/控制符边界或全部窗口缩放组合已通过。

## 留声机外观与范围身份补验

- 独立新设备位于负坐标(-120,91,200)。从南/西/北/东四个方位实际观察模型（0922-gram-south-clean/west/north/east），原木底座、唱片、唱臂、号角可见；背包与玩家预览持有同一3D物品（0922-gram-inventory）。第一人称右下只见模型局部，不把此截图单独当作完整手持外观证明。
- 通过GUI保存半径0、1、128，均实际重开确认。0显示设备一格，1显示3格边长；128在当前位置显示大范围边线（0922-gram-radius0/1/128、radius128-reopen），未据此宣称六面进出实际音频判定全部通过。
- 恢复半径1并显示范围后，实际删除测试设备：框随之消失（0922-gram-deleted）；同坐标重新放置新设备，默认半径16/“显示范围”，无旧框继承（0922-gram-replaced/open）。保留原音乐设备。
- 原版1.7.10传送附加yaw/pitch命令无效，之后使用三坐标传送与实际鼠标转向，四面证据来自后者。

## 扬声器回环输出：留声机分类与Master

- 使用本机WASAPI默认扬声器Loopback（PyAudioWPatch0.2.12.8，工具仅安装.tooling/0333/audio-tools；官方接口示例https://github.com/s0d3s/PyAudioWPatch/blob/master/examples/pawp_another_record_wasapi_loopback.py）。measure-loopback.py每次约4秒，仅保存RMS/peak/440Hz/880Hz数值JSON，无麦克风采样、不保存整段音频。
- ffmpeg生成自有440Hz MP3，实际GUI导入、点击确认保存并上传至本机集成服。0922-audio-initial为确认保存前的无效零值，不计声音结论。实际上传成功后有稳定440Hz输出。
- BGM0：RMS/peak/440Hz均0。BGM100时Voice0：440Hz与前一稳定值基本一致（0.930415/0.930647）；语音分类不影响留声机。Music与Players均0，仍测到440Hz输出；Master0时全部0。对应0922-audio-bgm0、voice0-bgm100、music-players0、master0 JSON与设置截图。
- 首个音源发生输出削顶，不能据其声压比宣称线性音量通过。换为volume=0.001生成的低幅MP3，并移至(-159.5,91,200.5)避开旧音乐设备范围，新设备半径128仍覆盖。
- 低幅源BGM50/Master约100（原版保存值0.9966887）：RMS0.0001700165、440Hz0.0002189825；BGM100/Master50：RMS0.0001700474、440Hz0.0002190386，两条衰减路径差约0.03%，支持留声机Master一次乘入。BGM100参考440Hz0.0006252787，不将设备输出绝对比例解释为严格线性（PCM低幅量化/输出链尚未完全隔离）。
- AU-01/02目前仅覆盖留声机及上述分类操作；Session语音/音乐、试听多源及所有音量组合仍需验证。不把数值检测写成用户亲耳验收。

## 半径1六面播放边界：实际回环确认

- 设备(-120,91,200)、半径1、低幅440Hz。六面每侧分别取边界内外0.1处，等待2秒超过既有0.6秒渐变，再采样约4秒。
- 0922-range-confirmed-{xminus,xplus,yminus,yplus,zminus,zplus}-{inside,outside}.json：全部inside的440Hz幅度约0.00062843；全部outside的RMS/peak/440Hz均0。
- 1.7.10客户端既有player.posY是视点高度，F3列feet与eyes；此处按视点Y取样，不改PLAN要求保持的播放公式。X测试feetY90.5/eyes92.12；Y下界feet88.48/88.28对应eyes90.10/89.90，上界feet91.28/91.48对应eyes92.90/93.10。截图保留F3与真实传送回显。
- 开始按feet取Y92.5与91整数（原版传送会补0.5）的初步零值不计六面结论；重新按既有坐标完成全部对照。一次半径数字输入未落入框导致空值，保存正确拒绝；0922-range-one-saved-confirmed才是本组有效配置。


## 范围0/16补验与场景干扰记录

- 半径0三轴六面全部通过；每轴4个样本与机器判定见0922-range-r0-{x,y,z}-results.json，包含明确传送feet、客户端视点坐标和回环数据。此前移除了新测试设备下方支撑石块，避免零半径取点发生碰撞推挤；设备仍是同一实例。
- 半径16首次y下界/z上界框外存在声音，不能计通过。原版天气/生物声及另一QQ设备(-105,69,208)与测试范围交叠。关闭所有非Master原版分类后，y下界仍有QQ音源。移除(-105,68,208)方块未使旧设备完全停播，不据此宣称红石验收通过。
- 首次从东侧右键时仍持留声机，命中邻近方块放置了无曲目的新设备；随后切空手从南侧打开正确QQ设备，截图来源为001ES52Q3qN0u7。实际点击播放关闭并保存（0922-old-qq-device-disabled），保留来源/半径/身份。之后onlytone-y四项均通过。受干扰原文件保留，isolated前缀不代表隔离已成功，仅onlytone-y为已排除QQ结果。
- 原版Master滑块右端首次操作实际持久值0.9966887（界面99%），早先“100”近满音量样本应按该值理解；精确0端点与分类隔离结论不受影响。后续精确满端点需再次确认。

## GR-03 四种半径六面最终对照

- 明确白名单汇总：0922-range-boundary-summary.json，半径0/1/16/128 × X/Y/Z × 两侧 × 内外 = 48/48有效样本。每个框内都有440Hz测试音，每个框外RMS/peak均0。只作该隔离场景的实际范围证明。
- 半径16采用isolated-x与onlytone-y/z；其余有干扰样本不计通过。半径128首次替换粘贴被截成1，onlytone-x文件无效；清空后重新粘贴128，0922-range128-confirmed-saved明确保存128，最终仅采用confirmed-x/y/z。
- 半径1角点在y89放红/蓝羊毛，顶面y90与框底一致，x两端[-121,-118]、z两端[199,202]对应3格区间（0922-range1-marked-corners-day）。为采样保持既有视点坐标及floor公式，未修改产品播放语义。
- 下一音频阶段前，测试音设备实际关闭并保存，避免干扰Session音源；旧QQ设备也暂时关闭。全部操作仅在隔离验收存档。

## Session语音跨本地页及分类输出

- 新AudioProject复制隔离ProfileProject；Studio实际导出并验证Native0333Audio.dgrs（227903字节）。正常区域启动Session，440Hz循环BGM、180秒700+4t Hz扫频语音，下一作者页880Hz语音；全部音源为ffmpeg本地生成，无用户录音。
- 原版Master通过UI精确设置并在options.txt验证1.0，其余原版八分类均0；DGR两分类均1.0。首次包进入即测到BGM和语音，未以预加载状态替代实际输出。
- 0922-session-chirp-page1..4：扫频主频716.66→870.57→896.64→922.46 Hz，随真实时间持续增加；原句分四个本地显示页，音频未重启回700或停止。page4为CHIRP_END所在最后本地页；下一次点击page5画面明确VOICE_880_NEXT_AUTHOR_PAGE且输出879.85Hz，只有真正作者页推进才切音。
- DGR Voice0/BGM1：440Hz0.0197297、880Hz约1.66e-7；Voice1/BGM0：880Hz0.00636075、440Hz约3.56e-7。两路实际分类隔离成立，Music/Players等原版分类均0仍能播放Session两类。
- 0922-session-voice1-master50在第一轮180秒语音自然结束附近采到0，无效，不计Master静音证明。已正常结束并重新区域触发，fresh前缀使用重新开始的880Hz语音。
- 新鲜扫频原句两路同时播放的baseline含440Hz与715.66Hz；Master0采样所有值为0（0922-session-both-master0）；恢复1后440Hz与1410.76Hz重新可测（both-restored），扫频继续而非从700重启。此前before-master0/active仅对应已结束语音后的BGM，保留其较窄证明范围。
- 新鲜880Hz下Master50与Voice50输出同量级（幅度0.00244175/0.00263658，约8%差异），不作为高精度乘法拟合证明；源码/探针验证单次Master，原生回环验证有声、静音及分类隔离。

## 23:34 指名器残余重影修复及正式产物复验

- 实体指名器的当前绑定、操作反馈仍调用原版带阴影 drawString；改为 DgrUiText.left，同步替换复制器滚动提示的同类调用，仅影响 DGR UI 文本。scoped Spotless、playerPreferences0332Probe、nominator0323Probe、reobfJar 全部通过，日志 .tmp/0333-nominator-shadow.log。
- 新正式 JAR：1694369 字节，SHA-256 0e531da04212158c2b65213e72953192d080a91c675fbaa2b2e697419439c806。dist 与隔离客户端同哈希；之前 2ef3aa66 的音频/范围记录保留其原构建身份。
- 640×480、GUI Scale2、ForceUnicodeFont、白主题，同一 NPC 实际右键打开并点击绑定。0922-nominator-white-status-before/after 成对图确认当前实体及“实体指名已保存”两处重影消失。复制器提示只完成源码/构建验证，没有伪报新实机截图。
- NativeLabel0333 由 /noppes npc NativeLabel0333 create 正常创建，UUID dbd90573-900f-4a11-89b9-a674db7768b1，绑定 Native0332:profile_actor。三主题有效世界图为 0922-npc-{dark/white/blue}-world-final；0922-shadow-world 增补夜间，白字加深底清晰。此前空 NPC 场景不计通过。

## NPC正常链、会话中重进及奖励幂等

- NpcProject 基于隔离 ProfileProject，首个 Start trigger 改为 interact_actor(profile_actor)，新任务 Native0332:npc_task 避免复用已完成任务。Studio真实导出 Native0333Npc.dgrs，12306 字节，SHA-256 8b8b789f64c295cee178177146199bb30abfe38c7d0125a69b62d1353311b215。安装的文件与导出文件一致。
- 首次只替换安装包但未 reload，仍为旧代际，点击只出现 CNPC Hello，不计通过。正常 dgr reload 接受 3989c9c9480b 代际后，空手右键现有 NPC，启动“NPC正式链：击败一头牛”，未用调试命令启动任务/Story。
- 实际左键击杀测试牛后自动进入头像 Session；任务详情显示 0/1 及直接经验+1，存档最终 SETTLED、唯一奖励收据。第一次长 summon 命令超原版输入长度导致NBT不完整，没有生成牛；缩短后成功，不计产品缺陷。
- 原句运行期间打开历史，两条作者原句；23:52:47 正常保存并卸载世界，重进恢复在第二作者页，历史仍为原两条，截图 0922-npc-history-before/after-reentry、0922-npc-reentry-world。
- 第二次正常保存退出后，只读 NBT 比较脚本 check-npc-reentry.py：任务完整快照、经验、全部收据与基线严格相等；0922-npc-reentry-baseline/compared.json。此处证明同一集成服重进，不冒充跨服务端或跨维度。
- 人工实际听音仍待用户可听设备时确认；WASAPI数值不代替主观听感。CONSTRUCTION_COMPLETE=NO，USER_ACCEPTED=NO，RELEASE_READY=NO。

## 2026-09-23 红石实际输出补测（尚待位置续播核对）

- 新JAR 0e531da0；独立本地440Hz设备(-120,91,200)，GUI启用播放/红石，半径16。Master、Voice、BGM全部1，其他原版八分类0；旧QQ设备关闭。
- 初次仅底部(-120,90,200)供电测量均零，不计暂停通过；GUI旁路红石后恢复440Hz，排除音源/音量无声。此组无效基线保留为0922-redstone-*，不宣称底部供电已验证。
- 使用侧面(-119,91,200)红石块：0923-redstone-side-powered 440Hz幅度0.00062648；移除侧面和底部两块后 isolated-off RMS/peak=0；只恢复侧面后 isolated-restored幅度0.00062492。实际静音/恢复成立，尚不能仅凭恒定音判断位置续播。
- 00:05:21正常保存卸载。后续启用现有darkgrey.gramophone.diagnostics启动参数核对PAUSE/RESUME位置，不修改产品实现，不把状态日志单独充当听音。

## 2026-09-23 排版重复测量与红石/试听续播

- 新增现有 Dialogue0333Probe 计数验收，5000字符长句首次5000次Metrics；1000次不变布局/逐字读取额外0次；真实改宽再5000次。格式化后完整dialogue0333Probe通过（.tmp/0333-layout-count-formatted.log）。本次first 3.3804ms、1000帧9.9523ms为该模型探针单次观测，不是游戏帧率、修改前后基准或磁盘IO证据。
- 现有诊断开关启用后的正式JAR中，00:07:44开始音源55141951-ca3c-44a5-b90c-b19b03e5424f。早期diag-baseline/off/on采在音源真正START之前，零值无效，不计暂停证明。
- ready-baseline/on有440Hz（0.00063293/0.00062928），ready-off RMS/peak均0。同一正式音源00:08:27 PAUSE seconds=43.22133333253448，00:08:36 RESUME为完全相同位置，没有第二次START。由输出与位置共同证明红石暂停后续播；不冒充主观听感。
- 原生点击试听生成独立96849961-c8a6-4586-a3d8-5133548f80ea源；00:09:38关GUI只STOP该源（45.664秒），正式5514源没有STOP，关窗后440Hz0.00063094持续。双同频源相位会叠加/抵消，不用合成幅度推断音量倍数。
- 冷启动首次准备约47秒才START，记录为实际等待现象；尚未隔离下载/全曲分析分别耗时，不能把初次零输出归咎为红石实现故障或据此宣称缓存命中。

## GR-09 原生100份未保存草稿压力验收

- 100份本地ffmpeg生成的0.2秒MP3，以不同标题元数据产生100个不同内容哈希，共215692字节。通过真实AWT文件对话框逐一导入；001手工步骤，002–100由run-draft-stress.ps1逐次UI操作并校验缓存文件。没有调用产品私有load入口，没有点保存或上传确认。
- 0923-draft-stress.jsonl逐项记录；每10次实际就绪截图0923-stress-NNN-ready。最终100个源SHA与100个缓存SHA集合严格相等，无重复副本；峰值100份/215692字节。0923-draft-stress-imported.json记录通过。
- 导入前/中途/完成/退出后，服务端Media/Gramophone两份原媒体与Devices.properties大小、SHA完全不变，证明本场景没有自动上传或修改设备配置。关GUI重开恢复“已保存本地音乐”，未把draft100写回设备。
- 正式设备音源55141951持续，压力结束回环仍为原440Hz，幅度0.00062749；导入过程没有新的START/STOP。00:27:48正常保存卸载世界，00:27:49正式源STOP；之后程序自动删除本上下文100份草稿，closed.json记录数量0。没有手动删除缓存制造通过。
- 只证明100份小文件导入、设备保护、无自动上传和上下文关闭回收；不冒充128MiB压力、30分钟TTL等待、大文件速度或网络/解码次数测量。其他旧上下文残留按其独立租用/回收规则保留。

## GR-12 在线来源及关闭取消（1024×768）

- 正式JAR0e531da0，窗口客户区1024×768、GUI Scale2。原本地正式设备通过红石暂停在56.7467秒，其他原版声音分类为0，为在线试听隔离输出。
- 网易云无效ID99999999999999实际报“无法确认网易云曲目免费播放状态”；QQ无效mid00000000000000实际报“平台媒体标识无效”。两个失败未保存，服务器两份媒体/Devices.properties与100草稿前基线大小和SHA完全一致（0923-online-failures-server-after.json）。不据此限制明确保存规范化来源的既有契约。
- 网易云416892104官方详情返回fee0、347428ms；实际产品下载完整音轨，界面5:47，缓存5559319字节。0923-netease-actual-output实际RMS0.3392；重开该未保存来源命中当前上下文缓存并从0重新试听。没有将单次缓存命中扩大为HTTP/解码计数测量。
- 第一次网易云“准备后关窗”操作实际发生在START21秒后，所以仅计正常关窗停止，不能计准备中取消。
- QQ有效001ES52Q3qN0u7：点击试听后450ms内Esc关闭，再打开仍为旧本地设备来源；00:38:38回环全0，期间没有新的试听START，不复活旧结果。之后明确重新输入并点击，00:38:49独立新源开始，实际输出RMS0.3232；00:39:54关窗后STOP，00:39:59回环全0。对应0923-qq-cancel-*、qq-valid-*。
- 在线音乐两次满音量回环peak达到1，仅证明实际有声与停止，不声称音质/失真或主观听感通过。没有保存在线草稿替换设备源。
- 生存模式切换会隐藏客户端范围框，恢复创造重新出现；集成服主机仍拥有服务端命令权，且首次右键未准确命中设备，不能把这次操作报告为真实OP权限撤销验收。0923-range-permission-no-edit文件名不是通过结论。
- 00:45:17正常保存卸载世界，所有音源STOP。人的主观听音仍待确认。

## 最新正式 JAR 独立专服双客户端（2026-09-23 00:48–01:31）

- 独立 ServerRelease 使用生产版 1.7.10 server.jar + Forge FMLServerTweaker，非 Gradle 开发服；仅监听 localhost:25594。主端、见证端、专服与 dist 的 DGR JAR 均为 0e531da04212158c2b65213e72953192d080a91c675fbaa2b2e697419439c806。服务端日志 server-release-0923.log 留存握手、玩家 UUID、保存及重连时间。
- 使用 Studio 正常导出的 Native0333Npc.dgrs（12306 字节，SHA 8b8b789f64c295cee178177146199bb30abfe38c7d0125a69b62d1353311b215）。两个真实客户端分别右键绑定 NPC → 各自 cow 0/1 任务 → 实际挥剑击杀 → Session。Main 完成后 Witness 仍 0/1（0923-witness-zero-after-main-confirmed），随后 Witness 自己击杀才完成（0923-witness-isolated-kill）。早先命名含 kill/completed 但实际未击杀成功的截图不计通过。
- 两端客户区 640×480、GUI Scale 2、Force Unicode Font 开启。Main 字号150%，Witness 100%，自动播放关闭。同一作者长句 UNICODE_BEGIN 至 UNICODE_END，Main 实际4个本地分页，Witness实际2页。主端单独翻到第2页时 Witness 仍第1页；0923-dual-main-page2-full / 0923-witness-still-page1。末页分别 main-page4-full / witness-page2-full。
- 服务端 save-all 后直接只读解析 NBT，page1、main-page2、local-last 三份快照的完整 canonical_sessions、canonical_tasks、两玩家 XP 和 transaction image 全部严格相等。两端 line_page_index=2、line_epoch=3。比较结果见 0923-release-state-comparison.json；没有依赖客户端画面推断服务端推进。
- Witness 01:27:16 正常断线，01:27:54 重连同服，恢复同一作者页的第一个本地分页；完整句子仍在历史中（0923-reconnected-history）。reconnected 快照的上述四组数据与 page1 完全一致，无新增经验、收据或任务结算。
- Main 在自己的第4个本地分页读完后再点击，正常结束该三作者页故事；main-next-authored 快照新增的终止路由只属于 Main，Witness 会话仍 ACTIVE、作者索引2/epoch3。不把文件名 next-authored 解读为存在第4作者页。
- 本轮关闭最终构建的双人任务隔离、不同字号本地分页不推进权威状态、同服重连无重复结算的具体场景。未覆盖跨服/跨维度、251条历史、多人音源时间轴或一个综合故事全矩阵；仍不等于用户验收。

## 自动播放上层窗口与失焦（同一最终专服）

- Witness 重连后同一 Unicode 长句第一页开启自动，按钮显示“自动:开”；依次进入历史、聊天、创造背包、Esc菜单，各停留至少12秒（超过设置允许最长10秒）。每次画面仍是原本地第一页；历史窗口含完整原句。0923-auto-history/chat/inventory/esc-waited 留图。
- 返回会话后将真实Windows前台切到Main客户端，Witness pauseOnLostFocus=false，保持可见但失焦超过12秒；使用KeepFocus截图未重新激活它，0923-auto-focus-waited仍第一页。
- auto-history与auto-overlays服务端完整NBT快照均严格等于main-next-authored基线，包含Witness作者页/epoch、任务、两人XP及收据。没有仅以画面推断权威状态。
- 恢复Windows前台后自动正常进入第二个本地分页，随后正常结束自己的故事；完成后canonical_tasks及两玩家XP/收据仍严格相等，0923-auto-completion-comparison.json。UIA/截图调用存在额外耗时，因此2s/14s文件名只是脚本等待标签，不声称毫秒精度或完整等待时间下界的计时证明。
- 此轮未单独测试设置窗口、死亡、延迟回执，也不取代模型计时边界探针。各窗口的暂停不推进和恢复可继续已有实机证据。

## DP-16 客户端正常退出再进（最终专服）

- Witness先关闭历史、正常断线，再从Minecraft主菜单点击Quit Game，旧PID82172确认退出；启动相同参数/正式JAR的新进程74696。独立服务器保持运行。
- 启动显示处理曾错误显示该进程IME辅助窗口，导致首次点击停在主菜单；这些restart-history、client-restart未带valid的文件不计重连证明。修正仅显示游戏窗口后，服务端日志01:42:50确认同UUID真实重新登录。
- 0923-restart-history-valid实际显示“暂无对话记录”，临时历史已清空。只读NBT的client-restart-valid与auto-completed完整快照严格相等，已结束故事、任务、两玩家XP和事务收据全部保持。此包无Choice节点，不扩张为Choice专门分支证明。

## 双客户端 Gramophone 个人时间轴与试听隔离

- 同一个专服设备(-120,91,200)、16格、本地120秒440Hz、红石控制。Main于01:44:27进入范围，首次准备后01:45:13独立源8fc8b6b7 START；Witness稍后进入，01:47:00独立源4a13b67b START。两端不是加入既有同步播放进度。
- Main真实打开设备并于01:47:12开始试听，独立源ae0a5129；01:47:47关GUI仅停止该试听，双方正式源没有STOP/START。红石关闭后，Main暂停36.79458333341709秒（120秒曲目已循环），Witness暂停49.397333331686305秒。01:48:20恢复时各自RESUME完全相同位置，没有重新START。
- Main于01:48:23离开范围，只停止8fc8b6b7；Witness源继续。Witness其余8个原版声音分类随后通过真实设置界面归0，Master保持1；0923-dual-witness-only-isolated实际440Hz幅度0.00062411。01:50:51红石关闭后同一Witness源暂停80.58125秒，0923-dual-witness-only-paused回环所有幅度0。
- 0923-dual-preview-combined采样含Witness未静音的原版环境音，peak1且高噪声，只保留为无效混合采样，不用于音量或质量结论。隔离后的两份回环与精确源生命周期作为有效证据。
- 仅关闭多人错时进入的个人时间轴、单方试听不打断他方以及离开范围的隔离场景；人的主观听感、多源完整音量矩阵仍待验。

## GR-05 专服权限失效的实机边界

- Witness在真实专服有OP且创造模式时右键设备打开GUI、开启范围，关GUI后实际蓝色范围框可见。
- 先deop仍保持创造模式时框保留，这是既有GramophoneServer权限规则（创造或命令权限任一满足），不是权限撤销失败。再由Main切Witness到生存，实际范围框消失，原位置瞄准同一设备右键无法打开GUI；0923-permission-revoked-no-range/edit-denied。
- 服务端日志保留deop及gamemode动作，检查后恢复隔离测试玩家OP/创造。此次才是同时失去两条授权路径，不沿用此前集成服主机仍有OP的无效证明。未测试跨维度/跨服。

## DP-15 同服下界往返（已完成故事）

- 在复制测试存档构建原版下界门，Main实际经门从维度0进入-1，再经同一门户返回0。只读保存的playerdata分别记录Dimension=-1/0（0923-nether-position、nether-return-position）。门内会关闭GUI，早先门内history截图无效；实际按W走出门后打开历史，0923-dimension-real-history及0923-return-overworld-history均保留完整原句。
- nether与nether-return的完整会话/任务/两玩家XP/事务收据快照严格等于auto-completed；没有重复结算。只证明已完成故事后的同服维度历史保留，不冒充原句运行中跨维度、跨服或251条淘汰。

## 正式综合同包端到端（进行中，02:36起）

- 新建隔离IntegratedProject作者输入，沿用已验证的角色/物品绑定；新故事Native0332:integrated、任务integrated_task、会话integrated_session。牛与猪目标通过AND同时满足后结算，奖励7XP+指名物品1，再进入6句（短句、60/61字符边界、Unicode长句、扫频长句、880Hz下一作者句）、背景440Hz、5选项（含超长选项）。
- Studio真实界面折叠6句、创建2组再组合为父组；持久化frame父组ae7ae190包含ad34e271/c0e01abc两个子组。真实拖动改变位置，Ctrl+Z恢复原位置；G拖动Esc取消后位置/组结构严格相等。保留before-cancel/moved布局JSON。保存退出重开后组结构仍在。
- 首轮中文输入与低缩放坐标提交未落盘，不能计通过；后一次坐标过期未真正聚焦节点也排除。切英文输入状态、重新定位真实文本框后，Inspector提交INTEGRATED_UI_COMMIT落盘；再从节点本体改为“同包验收：保存重开验证。”，Enter+离开文本框+Ctrl+S落盘。前两次失败尚未隔离具体输入法/焦点因果，不据此宣布产品缺陷或已修复。
- 从Studio“项目→导出故事包”真实导出并验证Native0333Integrated.dgrs，229518字节，SHA f9263346d0984bfd8c1f837c5a045bce81ad249d4e4e43d18593c1280bda2151。第一次Windows保存框使用正斜杠绝对文件名无效；改当前目录下普通文件名成功。并未手写替代运行ZIP。
- 专服首次因旧Native0333.dgrs仍在包目录，产生共享Actor冲突而拒绝加载；旧包已移动到ServerRelease/FixtureArchive/Native0333Npc.dgrs（SHA仍8b8b789f），新包加载成功generation3563919a96d9。旧故事退役3实例与3任务是显式卸载结果，不能跟前面的同包重连快照混比。
- Main真实右键NPC启动综合任务；第一轮站点缺地板和数次猪移动未命中均排除。实际牛击杀后仅牛目标完成，猪0/1且没有Session；稳定目标后真实猪击杀才SETTLED并进入首句。integrated-before-kills/cow-only/actual-reward保存NBT。经验+7，事务inventory含id264、Count1、display.Name=UpdatedGem0333；只新增收据957795ec8bfc25fc3f7ff3977c6fbec5bb1b416a5d4af303f829d7af468b39fb。
- 游戏首句实际显示Studio保存文字。当前继续验证综合包的分页、音频和选择；不要把“已导出/已进入Session”写成全流程完成。

### 综合包双端、运行中跨维度、音频及选择追加

- Witness亦真实NPC启动自己的双目标任务，先猪后牛；原拥挤平台若干未命中排除。独立小平台真实击杀最后一头牛才进入Session（0923-integrated-witness-isolated-kill）。两端随后都在作者索引3/epoch4，Main150%四本地页、Witness100%两页；integrated-dual-page1与integrated-local-last完整会话/任务/两人XP/收据严格相等。
- Main仍运行该Unicode原句时经原版门0→-1→0；Witness只用原版tp帮助进出隔离传送门，不使用故事调试开始/推进。active-nether-position、active-return-position确认维度；nether与nether-return两份完整权威快照均等于local-last。Main安全重排到本句第一页，历史仍保留完整原句。工具脚本已修正已有Chat输入时双斜杠拼接；只有服务端日志确认执行的命令作为证据。
- Witness Master经真实原版设置界面降至0，主端独立声音：扫频长句四个本地页，前三页实测主频1040.52→1091.91→1296.96Hz持续向前。第四页停留到180秒音轨已自然结束，不能将voice-next命名文件当下一句；此时截图仍CHIRP_END且仅背景音。随后真实作者句VOICE_880_NEXT_AUTHOR_PAGE实测879.85Hz（幅度0.01725），背景440并存。没有把自然结束当暂停或重播。
- Main在880作者句开启自动，自动进入5选项后等待超过12秒不代选。实际翻到长选项页面，Tab焦点+End访问26–29/29行及LONG_END；Enter选择后Main结束，服务端新增Main integrated终止路由。见证端仍作者索引3。integrated-main-finished-comparison.json证明任务与两玩家XP/全部事务image保持一致。
- 主端正式同包NPC→两目标→直接奖励→短/临界/超容量/头像/语音→自动→长选择→结束链已完成；双端同包不同分页及运行中跨维度已有本轮证据。尚未把此前独立包的听感或所有GUI/主题矩阵自动并入本包；人的主观听感仍待反馈。

### 跨服务器上下文隔离（03:29–03:31）

- 新建仅监听127.0.0.1:25595的空白生产Forge B服（ServerContextB），使用同一正式JAR；不复制A服故事/世界。Witness实际从A断开，03:29:23加入B，H界面显示“暂无对话记录”（0923-crossserver-b-history）。
- 03:30:28返回A（25594），会话恢复Unicode作者句索引3并安全重排到第一页；H仍含完整UNICODE_BEGIN至UNICODE_END（crossserver-a-session/history）。
- Main实际save-all后只读捕获crossserver-return。与integrated-main-finished相比，会话、任务、两人XP及全部事务image四组严格相等（0923-crossserver-comparison.json），无重复奖励。本例不涵盖251条FIFO或B服另产生历史后的双向切换。

### UI-03 透明度端点（灰黑、100%、640×480、GUI2、Unicode）

- 实际文本设置点击0%与100%，对应zero-setting/hundred-setting及同一句Session截图。底板在0%透出场景、100%实色；文字仍完整可见，头像颜色/清晰度未随底板消失。
- 中间full-setting/full-session不是100%样本：重新打开设置默认回外观页，旧坐标误选蔚蓝。已重新观察并切回灰黑、文本页后设置100%；无效命名样本不计端点通过。此例仅覆盖上述明确配置，不扩展为三主题完整矩阵。

### 三尺寸与GUI Scale追加（未关闭完整视觉矩阵）

- Win32 MoveWindow改变隔离Witness真实客户区并GetClientRect校验：640×480、960×640、1280×800。GUI2下960灰黑100%、960浅白125%、1280浅白125%长Unicode句/头像/按钮均在框内，后两者完整原句在单页展示。首张1280窗口越出桌面右侧无效；已移动到(200,40)，使用white125-visible图。
- 原版Video Settings实际切至GUI Scale Small，options.txt只读确认guiScale:1；真实点击DGR主题/文本/150%控件仍可操作。
- 发现GUI1+forceUnicodeFont=true时，原版Video Settings和DGR文本均呈严重像素破碎/欠清晰（scale-small、scale1-settings、scale1-size640-blue150）。不将这些截图记为视觉通过，尚需隔离Unicode开关和原版基线。GUI2样本不代替GUI1结论。

- Unicode开关追加对照：原版Language内真实Force Unicode Font ON→OFF，options.txt确认false。同一GUI1下原版英文立即恢复清晰，DGR英文亦恢复；中文仍使用Unicode字形，需保留低缩放可读性缺口，不能以关掉强制Unicode宣布全问题解决。此为原版界面同客户端对照，尚非不加载DGR的纯净客户端基线。

- 浅白/蔚蓝GUI2、640×480、150%、强制Unicode OFF追加0/100端点（blue-zero-session/normal-blue150与white-zero/full-session）。文字与头像未跟随底板消失；0%深色字叠夜景对比差，不能声称该极端设置仍具高对比。只读像素对照：浅白两个端点正文同坐标1218个RGB(32,42,48)字像素全部相等，证明正文颜色未随底板alpha变淡。头像873个金色像素最大通道差仅3；不是逐像素完全相同，不作该声明。初次像素脚本用了错误文字色，已纠正并覆盖输出，最终证据0923-opacity-pixel-comparison.json。


### 实际客户端运行时计数（临时验收Agent，不修改正式JAR）

- 在.tooling/0333/Native/counters编译可移除Java8 instrumentation计数器，仅白名单DGR类方法入口/TaskTrackerHud内部快照重建调用计数；AtomicLong驻内存，无逐帧磁盘输出。输出仅主动snapshot时写验收目录。正式包哈希不变；此诊断场景不作为无插桩帧率。
- Witness真实Session：steady-before draw1394，随后960×640真实resize后draw3519、measure1；47.122秒后draw6347、measure仍1，即2828次绘制额外排版0。实际本地翻页start截图仅前缀，10秒后end完整至UNICODE_END，下一计数draw8853、measure仍1。Witness已remove恢复原类方法。
- Main初始无活动任务，实际字号变化触发snapshotRebuild1；draw1670→3199而rebuild仍1。真实NPC repeatable再次接取同包牛猪任务（没有完成目标/发放新奖励），两个0/1目标HUD实际显示；active-before draw19449、rebuild2、accept1、save1。之前牛遮挡/未对准NPC操作无效，只有npc-start-valid实际出现任务才作为接取证据。

- 撤销复核修正：remove后计数仍增长，Forge/Mixin重转换未完全恢复原方法。Main第二次install后的绘制计数可能含重复注入，active-steady那组不作为精确调用次数证据。第一次install至第一次remove之前的Witness布局计数、Main无任务/接取/历史组范围明确保留。关于“Witness remove恢复原类”的前述描述以本项为准：仅操作请求成功，实际未恢复。
- 两客户端均通过Disconnect→Quit Game正常退出，原PID31764/74696均不存在；保存counters-client-exit。完全进程退出确保临时Agent与bootstrap诊断类退出，随后使用原生产启动参数（无javaagent）重启。正式EXE/JAR重查哈希不变。此为测试工具撤销问题，不据此修改产品代码或宣布产品回归。

- Main第一次接取后active-before→active-after：draw19449→19618，snapshotRebuild仍2，accept/save仍各1；该区间主要打开历史，只证明历史往返没有额外接收/保存/重建，不当持续HUD长时样本。
- 正常完整重启后Main86636、Witness49120，生产JAR无Agent参数，04:10回连A。主端活动牛猪任务0/1恢复；H旧历史清空。Witness会话恢复Unicode作者句；Home显示当前UNICODE_BEGIN，末尾UNICODE_END，原先短句/边界句历史不再存在，当前恢复显示原句自然重新记录。
- clean-restart-comparison.json四组完整权威快照均true（对照counters-client-exit）。本轮完整退出覆盖活动Task与活动Session并行恢复，未包含Choice中途退出分支。两进程重启后不再加载临时Agent；不得再用带插桩旧PID的后续计数证明普通生产性能。

### 251条历史专用B服场景（开始，未完成）

- HistoryProject仅是隔离作者输入，复制综合项目后将故事改为区域→251句Session→结束；B服区域中心(1011,4,-156)、半径3。HIST_001..HIST_251为251个独立page_id，单句短文本120字/秒。
- 第一轮输入的Start trigger残留order1被正式导出校验拒绝；修正order0并重新打开作者目录。第二轮UIAutomation默认窗口选择暂时落到模态框导致ValuePattern不支持，明确主窗口名后完成真实Studio导出验证。
- Native0333History251.dgrs为13972字节，SHA f3266b02a10c72ce0b539e629c684cc11355b4c2612d1e3bd2e22dc1141dd271；复制至B服StoryPackages，04:27:31 dgr reload正式加载generation14f23f4b8d47。没有手写运行ZIP。
- 04:22 Witness从A连接B时原版NetworkManager队列NPE导致连接屏崩溃（报告0923-crossserver-connecting-crash.txt）；B服随后有CustomNPC+断线回调NPE。未能据此确定DGR因果，也未忽略失败样本。新Witness PID84112无Agent启动，04:26:21成功进入B；错误dgr story reload不计加载，只有后续dgr reload日志有效。
- 通过原版tp先到区域外，再到区域内，正常触发HIST_001。正在使用有焦点/进程/窗口尺寸守卫的有限批次原生鼠标点击逐句推进，尚未宣布FIFO实机通过。

### DP-15 251条FIFO实机完成（04:43）

- 按实际画面逐批核对句号001→045→091→140→141→190→239→250。早期B服tick滞后导致部分点击未推进，批次文件名after50/100等只标点击批次，不等于作者索引。失焦守卫曾中止批次；重新观察141后才继续。B服原版difficulty0、doMobSpawning=false减少自然刷怪干扰。
- 第250句：0923-history250-valid-start显示HIST_001/002/003，valid-end显示248/249/250。关闭历史后仅真实点击一次，第251句：history251-valid-start显示002/003/004，valid-end显示249/250/251，证实按250条上限淘汰最早原句。
- 第一次history250-start/end及history251命名截图仍停Chat输入，均无效；后续valid标记四张才是历史窗口。未用错误文件名替代观察结果。
- 只读B服保存NBT：line_page_index249→250，line_epoch250→251，transport_id1及node_id相同；任务、玩家XP/Inventory、事务数据均严格相等，见0923-history-fifo-comparison.json。B服原无奖励任务，不能将该空事务比较替代A服直接奖励收据证明；A服既有比较另存。

### 有内容的B→A→B追加

- 251句最后点击结束后断开B；04:46:55到A，历史只有恢复的Unicode原句，没有B的HIST记录。04:49:11返回B，历史仍含旧HIST250/251，没有A的Unicode句。这轮往返未再出现连接屏崩溃，但不据此关闭此前单次NPE归因。
- B故事repeatable，退出时仍在enter_region区域。重新进入B正常启动新实例，新增HIST001；历史头因此003，尾250/251/001。此为新实例记录和再次FIFO淘汰，不是复用旧作者句重复写入。populated-context-comparison.json中B transport_id2、作者索引0/epoch1确认新实例；任务/玩家XP和背包/事务无变化。A完整会话/任务/两人XP与收据四组均与clean-restarted一致。

### GUI1 Unicode无DGR基线

- 新隔离WitnessFontBaseline目录，仅复制本地偏好，空mods；Forge日志确认仅3个内建模组（MCP/FML/Forge），未加载DGR、CNPC+、UniMixins。640×480、GUI1、forceUnicodeFont=true，主菜单和Language列表同样出现Unicode字形欠清晰（font-baseline-unicode1/language）。原版界面切OFF后英文恢复（font-baseline-unicode-off），中文/非ASCII字形仍受低像素缩放限制。
- 因此不能把该现象归为本次DGR独有回归；不修改全局FontRenderer。仍不将GUI1所有字形写成清晰可读的全通过。
- B服已通过原版stop正常保存并退出PID56772；见证端最后两次菜单点击落到Direct Connect未退出，明确重观后通过窗口关闭退出84112。无DGR基线亦关闭87144。Main86636/A服86648与隔离Studio55628继续用于后续验收。

续作补充请见 ../../RESUME_0923.md；新固定EXE的分组/台词原生证据及605项回归独立记录，不覆盖上文历史产物身份。
