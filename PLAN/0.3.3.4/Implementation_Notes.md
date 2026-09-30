# 0.3.3.4 施工记录

状态：实现候选已放入权威 dist 路径；整版尚未完成实机验收。USER_ACCEPTED=NO；RELEASE_READY=NO。不得将构建、探针或本记录视为用户验收。

## 基线

- 2026-09-24，原分支 codex/0.3.3.3，HEAD 641c0f3316356fb57dd9f80d3afa00b0bc003f54，与计划一致。
- 已创建 codex/0.3.3.4。施工前工作区已有大量修改/删除/未跟踪文件，保留原状。
- BASELINE_STATUS.txt 保存原始清单；BASELINE_TRACKED.patch 保存原有 tracked diff。未跟踪文件未删除或覆盖。
- Agent Switch delegation_preflight 和 initial record_repartition 均返回 Scheduler 未运行。MAIN 保持所有权；未派发 Worker。

## 重复条件契约

保留 repeat_policy = once / repeatable。可选 repeat_condition：

- none: {"type":"none"}；旧文档缺失字段等价于 none。
- cooldown: {"type":"cooldown","value":24,"unit":"hours"}；正整数，单位 seconds/minutes/hours，转换为毫秒检查 int64 溢出。
- scheduled: {"type":"scheduled","period":"daily","time":"12:00"}。
- weekly 增加 weekday（1=周一，7=周日）；yearly 增加 month/day，不允许年份/月度/Cron。
- 拒绝未知/多余字段，无效草稿不写入 canonical。
- 正常完成时间仍使用既有 terminalTime，必须为 TERMINATED；ERROR 不重启。
- 计划取严格晚于完成时间的首个时刻，读取不写状态。时区在既有配置 story.repeat_time_zone 保存。
- DST gap 取该缺口后的首个有效时刻；overlap 取首次；年度 2/29 跳过非闰年。

## 实现候选范围

- A：共享 LinePagesEditor Enter 后插、预按键为空时 Backspace 删除、数量与重复按键保护、稳定 Page ID 焦点；富文本草稿 Undo 与 Graph 结构历史协调。
- B：真实登录名称索引、现有本地名称缓存回退；只读复制已加载存储或直接读取离线存档，不绑定执行器；每条异常记录保留可读信息，分页与请求关联、管理员鉴权、超时与读取大小限制。`/dgr debug` 打开玩家名输入 GUI，显示来源、查询时间、Story/Session/Task 与待续接信息。
- C：可视原子动态块支持玩家名称、Minecraft 经验等级、DGR 物品持有数；应用于台词、选择提示/选项、任务/目标说明、标题/副标题、消息。新增可见插入器、物品拖放、内部原子剪贴板与外部可读文本；搜索、重命名、声明依赖/导出验证同步。Runtime 服务器求值；当前 Session 作者页按实例保存已求值内容，重发/重启不重算，客户端既有记录接收实际正文。
- D：C#/Java 重复条件解析、双入口折叠卡片、草稿校验和摘要；Store 与诊断共享纯资格计算，ERROR 不自动重启。服务器时区持久化；冷却/严格下一计划点、DST 和闰年处理。
- 隐藏动态字段延迟创建 RichTextBox；300 节点渲染测试通过。未增加 Preview、Event Trace 或自动 reload。

## 动态内容与持久化契约

- 字符串前缀 `U+001E DGR1 U+001F` 后为 JSON 数组，元素只允许普通字符串或白名单对象：`player_name`、`player_level`、带 `item_id` 的 `item_count`。
- 未标记历史文本一律按字面处理；不递归展开替换结果。C# / Java 共用 `src/test/resources/dynamic-content-0334.json` 验证字面文本、转义、未知/重复字段等。
- 世界状态 schema 6 增加可选 `presentation_texts` 字段；旧存档可缺省。记录 transport 对应的玩家/节点/line epoch/作者页 stamp 以及显示槽值，严格检查 NBT 类型、槽名称和长度。它是显示快照，不是事件历史。旧版本程序不保证可读取新版已写存档。
- 缺失物品引用和无法求值分别显示不可用提示，不当作合法零值；异常求值日志每分钟最多一次。

## 验证证据

- `evidence/Core-full.trx`：462 通过、12 跳过；这是早于最后依赖校验补充的完整 Core 回归。
- `evidence/DynamicReferences0334-fixed.trx`：8/8，覆盖后续动态引用、任务 metadata 重命名、仅动态引用的物品导出、拒绝缺失声明且保留旧导出文件、workspace loader。
- `evidence/Authoring0334-delivery.trx`：最终相关 WPF 44/44；覆盖连续编辑模型、动态内容共用样例、原子编辑器、重复条件、草稿/Ctrl+S、搜索与编辑区域命中。
- `evidence/Rendering0334.trx`：7/7；含 0/300 组合下 300 节点移动/折叠。Rendering 回调中位约 16–17 ms；这不是 GPU FPS 或实机视觉验收。
- `evidence/Wpf-complete.trx`：完整 WPF 610 通过、5 失败，均为 `ProjectAtlasMenuTests` 旧菜单结构预期。该文件在 BASELINE_STATUS 中已是修改状态；后续查明旧断言与 0.3.3.3/DELIVERY_REPORT.md 确认规则冲突，修正断言；菜单产品行为未改。新完整结果见 Wpf-closeout-full.trx。
- `evidence/runtime-final-build.log`：jar/reobfJar、重复规则、只读投影/报文/共享文本样例、Session 显示快照、Story 服务探针通过。
- `evidence/presentation-persistence-fixed.log`：Session 重发/重启/不同玩家快照隔离、SavedData 与 Forge 路由通过。
- `evidence/release-publish.log`：自包含 Windows x64 Release 发布成功；`evidence/Delivery.json`：权威路径版本、字节数、SHA-256 与旧版备份位置。
- 菜单颜色测试原本随机选中相同初始颜色会撤销创建组合；测试现固定选择不同颜色，未改变组合产品行为。

## 当前交付与验证状态（更新）

- Studio：`E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`；ProductVersion `0.3.3.4`；142485206 bytes；SHA-256 `EA53EC45DB51E95B9CAA1FDD0F8DB3E283FE3EE867ECAAD7878B06F536723894`。
- Runtime：`E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.4.jar`；1750712 bytes；SHA-256 `88413C691FC36AAFB389139D136C23F9258ECBC3BA635388636784583DFB666C`。
- 新增实机修复：原子块点击/草稿 Undo、资源拖放弹窗、可读 Choice 端口标签；任务通知/日志/完成记录/提交说明动态解析；权限即时拒绝、丢失关联标记、诊断连续缩放。
- 实际 UI、同包 E2E、只读哈希、双玩家及重复资格见 `Native_Verification.md`、`UI_Evidence.md`、`E2E_Artifacts.md`。
- 完整 WPF 菜单期望修正后：617 通过、0 失败、1 显式跳过，见 Wpf-closeout-full.trx。其后 Run 点击修复另见专项及实机复测。
- 无 Git 提交/推送，无 Release。USER_ACCEPTED=NO，RELEASE_READY=NO。逐项未覆盖场景见 `Acceptance.md`。

## 原生 UI 规则

用户明确禁止 Computer Use/cua/sky；之前据插件边界停止原生 UI 的选择已纠正。此后使用 Windows UIAutomationClient / Win32 / CopyFromScreen。成功操作权威 Studio 搜索 1→0→1 后，已将规则写入个人 `E:\AI\CODEX\.codex\skills\windows-native-ui\SKILL.md` 和项目 `.agents/skills/studio-node-ui/SKILL.md`，并校验 skill。

早期 Computer Use 的截图/点击失败不是当前阻塞理由，也不是 UI 通过证据。全部后续真实结果以原生操作证据为准。

## 规模测试发现的局部修复

对施工前与候选 EXE 原始程序集进行同机对比，发现候选图中离屏正文也创建 RichTextBox，拖动阶段出现可复现的秒级长帧。仅将 DynamicContentEditor 的离屏正文保留为轻量文本，进入图可见范围再创建富文本编辑器；不丢弃节点、正文、校验或引用。已创建的编辑器保持状态，Inspector 正常即时创建。

`ViewportDynamic0334.trx`：相关编辑及 300 节点/300 Group Rendering 7/7，修复后 300 Group 移动 P95 28.410 ms / max 35.175 ms；`ViewportEntry0334.trx` 7/7，新增离屏节点进入视口才创建正文编辑器的回归。`viewport-publish.log` 发布后已替换权威 EXE。实际原子按钮打开选择器、正文输入 Z、撤销恢复 Hello 并保存见 `viewport-picker-second.png`、`viewport-real-edit.png`、`viewport-real-undo.png`。


## 继续施工：磁盘边界与节点富文本点击

- `ReadOnlyStateSource` 对可解压但缺少 compound `data` 的文件明确报错，避免误显示无记录；真实文件损坏/截断/超限均验证未写回。`inspection-boundaries-fixed.log`。
- 真实节点内文字点击发现 `Run` 被传给 `VisualTreeHelper.GetParent` 的异常；画布祖先和组合命中改用已有内容元素感知的父级遍历。`InlineClick0334-fixed.trx` 71/71；权威 EXE 的点击/选择/剪切/撤销/重做、Enter 新句和空句退格通过，`InlineFixedChecks.json` 记录恢复后的字节一致校验。
- `Wpf-closeout-full.trx` 为修复菜单期望后的完整 617/0/1。后续 `Wpf-inline-closeout-full.trx` 为 617 通过、1 失败、1 跳过；唯一失败是 `M4BlankAndImportCreationPersistSelectedStoryAndIndependentData` 文件替换 IOException，`ShellFileLockRecheck0334.trx` 单独复测 1/1 通过。保留偶发文件锁风险，未声称最新完整运行零失败。
- `inspection-disk-wire-scale.log`：10000 条磁盘记录、1000 条目标玩家记录、16 条/页，30 次采样，读取/解压/投影/压缩/解码总耗时中位 15.140 ms、P95 21.425 ms、最大 21.522 ms；原文件未变化。仍不包含 socket 和 GUI。
- 所有成功 UI 操作均使用 Windows 原生工具；失败的输入尝试不计入通过。未提交或推送，USER_ACCEPTED=NO，RELEASE_READY=NO。


诊断接收关联补验：`inspection-client-correlation-fixed.log` 调用生产 `GuiPlayerStateInspection.accept`，覆盖旧/未来/重复/超时后/重开窗口响应及 detached copy；10000 次交错响应 48.807ms。测试通过反射准备待查询状态，不模拟真实网络传输；LWJGL 仅加入该探针运行依赖，未更改产品依赖。


同名物品补验：动态物品选择器增加完整稳定 ID 的辅助文字，动态块提示同步显示 ID；避免同名项无法辨认。`SameNamePicker0334-fixed.trx` 9/9。权威 EXE 原生选择、正确/错误类型拖放、插入保存及撤销恢复见 `native-ui/SameNameChecks.json`。新增测试物品已移除，membership/Story 与测试前字节一致。当前 EXE SHA-256 `EA53EC45DB51E95B9CAA1FDD0F8DB3E283FE3EE867ECAAD7878B06F536723894`。


启动入口补验：`CanonicalStoryServerServiceProbe.repeatEntryGates` 实际调用 8 个生产服务入口（Entry、Actor、Region、Logic、指定 Logic trigger、Flow、terminal Flow、NPC candidate），分别对冷却/每日定时验证首次启动、ACTIVE 重入、未到期、落盘恢复后到期重启和 ERROR。拒绝/重复调用前后整个 NBT 相同；首次接受产生正确新激活时刻。测试发现 actorStartInputs 混入 Flow 边界端口，已仅接纳 LOGIC 连接。`repeat-all-entries-final.log` 及 `repeat-entry-regression-build.log` 通过。新 JAR 已同步 dist 与三个隔离实例，哈希以 Delivery.json 为准；不把服务测试称为全部入口实机操作。

最新版 WPF 完整回归 `Wpf-same-name-final.trx`：619 通过、0 失败、1 跳过（固定 300 节点性能专项单独运行）。此前偶发文件替换异常本轮未复现，历史失败日志保留。

原生错误提示补验：`native-ui/errors-problems-fast.png` 明确显示未声明物品引用和未知动态内容两项错误；进入故事被阻止。隔离 Session 已恢复到 SHA-256 A05745055ECDBB977817A7B7B77014C272894EC363F516A5972427940CB173CF，没有把无效内容保存成数量 0。


运行时错误/完成历史补验：`native-ui/RuntimeCloseout.json` 记录当前 JAR 的真实客户端入服、任务日志 8→0 数量、任务完成后历史动态文本、同样持有 8 个石头但无绑定时显示“数据不可用”。任务完成通过既有 debug kill event 驱动，不冒充物理击杀或 NPC 提交。物品恢复 8 个、绑定文件字节恢复、服务器正常保存停服。提交候选与长文本/语音/会话历史组合仍未完成实机覆盖。

## 2026-09-29 UI 与 OP 调试体验修正

已实施用户确认的五项修正。节点与 Inspector 的台词卡片、共享动态内容工具栏、菜单和重复设置同步更新；OP Tab 补全、可读摘要和折叠技术详情完成。具体实现、实机证据、分组测试及历史混合运行中断见 [UI_OP_Corrections_2026-09-29.md](UI_OP_Corrections_2026-09-29.md)。

最终分组 WPF 回归共 620 通过、0 失败，另 1 项 opt-in 基准跳过；动态引用核心测试 2/2；三个 Java 诊断探针通过。最终 EXE 与 JAR 已更新到 dist，准确版本、大小与 SHA-256 见 evidence/Delivery.json。没有提交/推送；USER_ACCEPTED=NO、RELEASE_READY=NO。C10 和 C12 的剩余整版实机验证继续保留。
