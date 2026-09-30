# 0.3.3.4 原生实机验证（进行中）

所有桌面操作使用 Windows UIAutomationClient / Win32 / CopyFromScreen，没有使用 Computer Use、cua 或 sky。仅操作 `.tooling/0334` 隔离工程、客户端与服务器。USER_ACCEPTED=NO，RELEASE_READY=NO。

## 已确认

- 权威 Studio 原生搜索 1→0→1，规则已写个人 windows-native-ui skill 和项目 studio-node-ui skill。
- 实际 IME 候选态的第一次 Enter 提交输入，下一次 Enter 新增 Page。空句 Backspace、Ctrl+Z/Ctrl+Y 保存后 Page 数和 ID 正确。见 `evidence/native-ui/IME_Input.json` 与相关截图。
- 原生动态块替换、保存、图历史撤销/重做；普通文字草稿 Ctrl+Z 保留动态块。深浅主题可读。
- 实机发现并修复：嵌入按钮禁用、弹窗背景透出、FlowDocument 默认字体/两端对齐；WPF 原生 Undo 将嵌入按钮变成空 Grid，改用 canonical 文本草稿历史；Popup 捕获外部点击阻断资源拖放，改为可拖放且有取消/Escape/失焦/卸载关闭的弹窗。
- `drop-result.png`：从左侧资源库实际拖入物品使“插入”启用；`drop-saved` 保存含 item_count，撤销恢复原文。
- 重复卡片非法 0 提示错误；5 秒配置节点与 Inspector 同步。开始节点原有 2 个区域启动条件保留。
- 同一个正式导出包在独立服务器安装，`SamePackage.json` 核对导出与安装 SHA-256。扩展样例由脚本准备初始结构，再在 Studio 编辑并正式导出；没有脚本生成替代 ZIP。
- 游戏两玩家实际显示 `Hello Witness0334 level 7`、`Hello Native0334 level 12`；Native 升到 17 后当前作者页仍为 12，重连仍为 12；存档 `two-players-nbt.json` 包含分离快照。
- 在线玩家名查询成功；重启后 Witness 管理员按 Native0334 名称查询离线记录成功。`offline-readonly.json` 状态存档 SHA-256 前后一致。
- 30 秒冷却完成后立即离开/返回区域，状态仍 TERMINATED；到期后真实区域驱动再次 ACTIVE。截图 `cooldown-rejected` 与后续台词。5 秒资格时间也见 `debug-online`。
- 扩展包真实 Choice 提示/选项、Title/Subtitle 显示 Native0334 / 17 / count 3；见 `coverage-choice.png`、`coverage-title.png`。
- 实机发现 Task toast 与旧 `/dgr task journal` 漏接动态求值，已修复通知、旧日志、完成历史和提交候选说明。新 JAR 正在隔离实例重测，不把旧失败截图算通过。

## 最近自动化

- `AtomicNativeUndo0334.trx` 36 通过；`PostNativeAuthoring0334.trx` 9 通过；`PickerNative0334.trx` 8 通过。
- `task-all-text-build.log`：jar/reobfJar、通知动态求值及不重复通知、旧日志兼容、Task Journal 投影通过。
- 历史 WPF 610/5 结果保留；已按上一版确认的菜单规则修正测试，新完整结果为 617 通过、0 失败、1 显式跳过（Wpf-closeout-full.trx）。

## 未关闭

新版 Task 提示/任务窗口实机复验，第二玩家 count 8，权限/异常关联/长列表/窄窗口，定时每日/每周/每年双入口完整 UI、实际运行接线，最终构建哈希更新。性能对比与整版人工验收仍需按证据区分。

## 2026-09-24 后续实机复验

- `task-toast-fixed.png`、`journal-fixed.png`：任务通知、旧任务日志的说明/目标已正确解析；`witness-choice-count.png` 为第二玩家等级 7、物品数 8。
- `offline-all-records.png`：重启后按玩家名查到离线 Story、Session 与两个不同 Placement Task；`restart-all-readonly.json` 六个 DGR 数据文件前后 SHA-256 一致。
- `missing-placement-fixed.png`：缺失 Placement 保留记录并标异常；`denied-fixed.png`：管理员权限撤销后即时明确拒绝并清除旧结果。
- `resize-fixed.png`：实际从 (1238,766) 拖到 (810,560)，整个缩放手势连续完成，输入名/结果保持；修复原先 drawScreen 调 initGui 重置 geometry 手势的问题。
- `page-one.png`、`page-two.png`、`page-two-scroll.png`：隔离玩家 22 条记录，第一页/第二页与末页按钮、滚动实际可用。测试通过既有 debug Task start 创建不同 Placement，未改正式玩家数据。
- `no-player.png`：不存在的本服玩家查询反馈。
- `chat-held.png` 与 witness.log：实际消息为 Message Witness0334 level 7 count 8。
- `daily/weekly/yearly` 对应截图覆盖周期切换；每年 2/30 拒绝、2/29 接受。`node-inline-daily` 等截图覆盖节点到 Inspector 同步。
- `ScheduledSamePackage.json`：Studio 正式导出 Native0334Scheduled.dgrs 与安装文件相同 SHA-256，手动 reload。
- `scheduled-eligibility.png`：真实故事正常结束后，资格为等待计划时刻，服务器 Asia/Shanghai，下次 2026-09-24 23:59。`scheduled-before-boundary.txt`：提前重入区域仍 TERMINATED。没有改系统时间；精确跨界/周历/闰年由可注入时钟探针验证。
- `resize-final-build.log`：最新 JAR/reobfJar、只读诊断和动态任务通知探针通过；`FinalNative0334.trx`：最新相关 WPF 6/6。

此节更新前文“正在重测/未关闭”状态；当前主要剩余为规模对比、逐项覆盖矩阵与整版用户人工验收。旧失败证据保留，不冒充新版通过。

## 定时持久化与无记录补充

- `no-record-fixed.png`：已定位 Native0334，但没有 DGR 记录；与 `no-player.png` 的无法定位提示不同。
- `unchanged-reload.txt`：同一正式定时包手动 reload 成功；`scheduled-after-restart-reload.json`：重启并 reload 后仍为 TERMINATED，terminal_time=1790229277501，即 2026-09-24T13:54:37.501+08:00，与先前 `scheduled-eligibility.png` 完成时刻一致。
- 此次刷新 UI 的 Witness 名输入被自动化截短，`unchanged-time.png` 不是定时状态通过证据；以上持久化结论来自实际服务器存档与 reload 日志，未冒充 UI 截图。

## 当前权威 Studio 最终复测

- `viewport-picker-second.png`：当前权威 EXE 的原子块实际点击打开选择器。
- `viewport-real-edit.png` 显示 HZello；`viewport-real-undo.png` 恢复 Hello 且三个动态块保留，随后 Ctrl+S 保存。
- `ViewportEntry0334.trx` 7/7、`ViewportDynamic0334.trx` 7/7；后一组包含 300 节点 / 300 Group 绘制检查。
- 所有最终版本、大小、SHA-256 以 `evidence/Delivery.json` 为准；前文“进行中”记录保留历史，当前未验证范围由 `Acceptance.md` 明确列出。


## 继续施工：磁盘边界与节点富文本点击

- `ReadOnlyStateSource` 对可解压但缺少 compound `data` 的文件明确报错，避免误显示无记录；真实文件损坏/截断/超限均验证未写回。`inspection-boundaries-fixed.log`。
- 真实节点内文字点击发现 `Run` 被传给 `VisualTreeHelper.GetParent` 的异常；画布祖先和组合命中改用已有内容元素感知的父级遍历。`InlineClick0334-fixed.trx` 71/71；权威 EXE 的点击/选择/剪切/撤销/重做、Enter 新句和空句退格通过，`InlineFixedChecks.json` 记录恢复后的字节一致校验。
- `Wpf-closeout-full.trx` 为修复菜单期望后的完整 617/0/1。后续 `Wpf-inline-closeout-full.trx` 为 617 通过、1 失败、1 跳过；唯一失败是 `M4BlankAndImportCreationPersistSelectedStoryAndIndependentData` 文件替换 IOException，`ShellFileLockRecheck0334.trx` 单独复测 1/1 通过。保留偶发文件锁风险，未声称最新完整运行零失败。
- `inspection-disk-wire-scale.log`：10000 条磁盘记录、1000 条目标玩家记录、16 条/页，30 次采样，读取/解压/投影/压缩/解码总耗时中位 15.140 ms、P95 21.425 ms、最大 21.522 ms；原文件未变化。仍不包含 socket 和 GUI。
- 所有成功 UI 操作均使用 Windows 原生工具；失败的输入尝试不计入通过。未提交或推送，USER_ACCEPTED=NO，RELEASE_READY=NO。


重复卡片补验：`RepeatCloseout.json` 记录双入口完整字段操作及 Story 字节恢复校验；`native-ui/repeat-reversal/timing.json` 记录 25 帧 Windows UIA 反向折叠动作与时间。检查第 2/3/6/10/13/14/17/24 帧，正文按高度裁切、连线位置稳定，最终恢复展开。


同名物品补验：动态物品选择器增加完整稳定 ID 的辅助文字，动态块提示同步显示 ID；避免同名项无法辨认。`SameNamePicker0334-fixed.trx` 9/9。权威 EXE 原生选择、正确/错误类型拖放、插入保存及撤销恢复见 `native-ui/SameNameChecks.json`。新增测试物品已移除，membership/Story 与测试前字节一致。当前 EXE SHA-256 `EA53EC45DB51E95B9CAA1FDD0F8DB3E283FE3EE867ECAAD7878B06F536723894`。


启动入口补验：`CanonicalStoryServerServiceProbe.repeatEntryGates` 实际调用 8 个生产服务入口（Entry、Actor、Region、Logic、指定 Logic trigger、Flow、terminal Flow、NPC candidate），分别对冷却/每日定时验证首次启动、ACTIVE 重入、未到期、落盘恢复后到期重启和 ERROR。拒绝/重复调用前后整个 NBT 相同；首次接受产生正确新激活时刻。测试发现 actorStartInputs 混入 Flow 边界端口，已仅接纳 LOGIC 连接。`repeat-all-entries-final.log` 及 `repeat-entry-regression-build.log` 通过。新 JAR 已同步 dist 与三个隔离实例，哈希以 Delivery.json 为准；不把服务测试称为全部入口实机操作。

最新版 WPF 完整回归 `Wpf-same-name-final.trx`：619 通过、0 失败、1 跳过（固定 300 节点性能专项单独运行）。此前偶发文件替换异常本轮未复现，历史失败日志保留。

原生错误提示补验：`native-ui/errors-problems-fast.png` 明确显示未声明物品引用和未知动态内容两项错误；进入故事被阻止。隔离 Session 已恢复到 SHA-256 A05745055ECDBB977817A7B7B77014C272894EC363F516A5972427940CB173CF，没有把无效内容保存成数量 0。


运行时错误/完成历史补验：`native-ui/RuntimeCloseout.json` 记录当前 JAR 的真实客户端入服、任务日志 8→0 数量、任务完成后历史动态文本、同样持有 8 个石头但无绑定时显示“数据不可用”。任务完成通过既有 debug kill event 驱动，不冒充物理击杀或 NPC 提交。物品恢复 8 个、绑定文件字节恢复、服务器正常保存停服。提交候选与长文本/语音/会话历史组合仍未完成实机覆盖。

## 2026-09-29 UI 与 OP 调试体验修正

已实施用户确认的五项修正。节点与 Inspector 的台词卡片、共享动态内容工具栏、菜单和重复设置同步更新；OP Tab 补全、可读摘要和折叠技术详情完成。具体实现、实机证据、分组测试及历史混合运行中断见 [UI_OP_Corrections_2026-09-29.md](UI_OP_Corrections_2026-09-29.md)。

最终分组 WPF 回归共 620 通过、0 失败，另 1 项 opt-in 基准跳过；动态引用核心测试 2/2；三个 Java 诊断探针通过。最终 EXE 与 JAR 已更新到 dist，准确版本、大小与 SHA-256 见 evidence/Delivery.json。没有提交/推送；USER_ACCEPTED=NO、RELEASE_READY=NO。C10 和 C12 的剩余整版实机验证继续保留。
