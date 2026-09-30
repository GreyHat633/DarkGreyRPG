# 0.3.3.4 逐项验收状态

PASS 为开发侧所列证据通过，不能替代 USER_ACCEPTED。PARTIAL 表示已实现但仍有未验证子场景。USER_ACCEPTED=NO，RELEASE_READY=NO。最新 Wpf-same-name-final.trx：619 通过、0 失败、1 项性能专项显式跳过；此前文件锁失败保留记录，本轮未复现。

| ID | 场景 | 状态 | 证据 / 边界 |
|---|---|---|---|
| A01 | 中间/末尾/空句 Enter | PASS | ContinuousLine0334 / AtomicNativeUndo0334 / PostNativeAuthoring0334；IME_Input.json、原生保存与撤销操作。通过范围按对应测试与 Native_Verification 记录。 |
| A02 | 有文本时 Backspace | PASS | ContinuousLine0334 / AtomicNativeUndo0334 / PostNativeAuthoring0334；IME_Input.json、原生保存与撤销操作。通过范围按对应测试与 Native_Verification 记录。 |
| A03 | 空句删除中间/首位 | PASS | ContinuousLine0334 / AtomicNativeUndo0334 / PostNativeAuthoring0334；IME_Input.json、原生保存与撤销操作。通过范围按对应测试与 Native_Verification 记录。 |
| A04 | 最早 Page 移位后删除 | PASS | ContinuousLine0334 / AtomicNativeUndo0334 / PostNativeAuthoring0334；IME_Input.json、原生保存与撤销操作。通过范围按对应测试与 Native_Verification 记录。 |
| A05 | 仅剩一项后再新增 | PASS | ContinuousLine0334 / AtomicNativeUndo0334 / PostNativeAuthoring0334；IME_Input.json、原生保存与撤销操作。通过范围按对应测试与 Native_Verification 记录。 |
| A06 | 空格、动态块、长按键 | PASS | ContinuousLine0334 / AtomicNativeUndo0334 / PostNativeAuthoring0334；IME_Input.json、原生保存与撤销操作。通过范围按对应测试与 Native_Verification 记录。 |
| A07 | 连续输入与 Undo/Redo | PASS | ContinuousLine0334 / AtomicNativeUndo0334 / PostNativeAuthoring0334；IME_Input.json、原生保存与撤销操作。通过范围按对应测试与 Native_Verification 记录。 |
| A08 | 中文 IME | PASS | 原有 IME_Input.json 加候选态退格实机：nihao→niha→清空，测试空页不被删除；结束组词后 Backspace 才删除空页。CloseoutNativeChecks.json；恢复后 Session 字节一致。 |
| A09 | 双编辑入口 | PASS | 共享控件专项及原生双入口：Inspector IME/Enter/退格、节点内 Enter→空句退格；两入口原子选择/剪切/恢复，节点内 Undo/Redo。InlineFixedChecks.json；保存后的 Session 与原件字节一致。 |
| A10 | 旧操作回归 | PASS | OldOperations0334.json：实际节点复制/粘贴保留 Page 内容与设置并生成新 ID，拖序反转且 ID 不变，Ctrl 多选并显式 − 删除、+ 重新新增、连续 Undo 恢复；保存原文件字节一致。重开由新交付 EXE 启动后读取验证。显式多删保持既有可删至 0 的语义（PLAN 3.4）。 |
| B01 | 玩家名输入查询 | PASS | inspection0334Probe / inspection-scale.log；离线只读哈希、权限/异常/分页/缩放截图。通过范围按对应测试与 Native_Verification 记录。 |
| B02 | 在线→下线→重启后离线查询 | PASS | inspection0334Probe / inspection-scale.log；离线只读哈希、权限/异常/分页/缩放截图。通过范围按对应测试与 Native_Verification 记录。 |
| B03 | 长期历史玩家 | PASS | HistoricalIdentity.json / historical-result.png：从隔离索引移除 Native0334，仅保留实际历史 usercache；只有 Witness 在线，查询成功且未补写索引，停服后恢复原文件并校验哈希。 |
| B04 | 不存在/无 DGR 记录/损坏记录 | PASS | no-record-fixed.png / historical-result.png 与 InspectionBoundary0334Probe：真实临时磁盘文件损坏、截断、错误根结构、超限均拒绝且不写回；缺失 data 原误判无记录已修复。 |
| B05 | 只读性 | PASS | inspection0334Probe / inspection-scale.log；离线只读哈希、权限/异常/分页/缩放截图。通过范围按对应测试与 Native_Verification 记录。 |
| B06 | 同资源不同 placement | PASS | inspection0334Probe / inspection-scale.log；离线只读哈希、权限/异常/分页/缩放截图。通过范围按对应测试与 Native_Verification 记录。 |
| B07 | 缺失资源/旧节点/未绑定原始记录 | PASS | inspection0334Probe / inspection-scale.log；离线只读哈希、权限/异常/分页/缩放截图。通过范围按对应测试与 Native_Verification 记录。 |
| B08 | 权限与报文 | PASS | 实机拒绝无权限查询；InspectionBoundary0334Probe 验证非法/超大压缩及解压报文；InspectionClient0334Probe 调用生产 GUI 接收器，过期/未来/重复/超时后/旧窗口响应不覆盖当前状态。未以网络注入冒充测试。 |
| B09 | 大量结果/快速切换查询 | PASS | 22 条实际分页/滚动；10000 条磁盘记录→1000 条玩家投影→16 条分页报文，P95 21.425ms；生产 GUI 接收器 10000 次交错响应验证当前结果不被旧响应覆盖（48.807ms，总接收耗时，不含 socket/渲染）。 |
| B10 | 重复资格诊断 | PASS | inspection0334Probe / inspection-scale.log；离线只读哈希、权限/异常/分页/缩放截图。通过范围按对应测试与 Native_Verification 记录。 |
| C01 | 第一次使用 | PARTIAL | 共享文本向量、DynamicReferences0334-fixed、FinalNative0334；双玩家/Choice/Title/Message/Task 截图；插入器及说明实际可用；首次用户可理解性须用户审阅。 |
| C02 | 三种动态内容 | PASS | 共享文本向量、DynamicReferences0334-fixed、FinalNative0334；双玩家/Choice/Title/Message/Task 截图。通过范围按对应测试与 Native_Verification 记录。 |
| C03 | 对象选择与拖放 | PASS | SameNameChecks.json：下拉列表/已选条目显示名称和完整 ID；两种同名物品实际拖入后 ID 正确切换，错误类型任务拖入不改选择；插入并保存确认稳定 ID，Undo 后 Story 及测试 membership 字节恢复。SameNamePicker0334-fixed.trx 9/9。 |
| C04 | 原子编辑 | PASS | 动态编辑专项、真实三块混排剪切/撤销、正向及反向跨块选择、剪切后粘贴恢复；InlineFixedChecks.json / CloseoutNativeChecks.json，保存后 canonical payload 字节一致。 |
| C05 | 与台词快捷键合用 | PASS | 共享文本向量、DynamicReferences0334-fixed、FinalNative0334；双玩家/Choice/Title/Message/Task 截图。通过范围按对应测试与 Native_Verification 记录。 |
| C06 | Undo/Redo 与双入口 | PASS | 共享文本向量、DynamicReferences0334-fixed、FinalNative0334；双玩家/Choice/Title/Message/Task 截图。通过范围按对应测试与 Native_Verification 记录。 |
| C07 | 保存/复制/粘贴/搜索 | PASS | 共享文本向量、DynamicReferences0334-fixed、FinalNative0334；双玩家/Choice/Title/Message/Task 截图。通过范围按对应测试与 Native_Verification 记录。 |
| C08 | 导出依赖 | PASS | 共享文本向量、DynamicReferences0334-fixed、FinalNative0334；双玩家/Choice/Title/Message/Task 截图。通过范围按对应测试与 Native_Verification 记录。 |
| C09 | 两个玩家同时展示 | PASS | 共享文本向量、DynamicReferences0334-fixed、FinalNative0334；双玩家/Choice/Title/Message/Task 截图。通过范围按对应测试与 Native_Verification 记录。 |
| C10 | 长动态值/分段/历史 | PARTIAL | 共享文本向量、DynamicReferences0334-fixed、FinalNative0334；双玩家/Choice/Title/Message/Task 截图；当前页快照/重连、原有分段探针通过；最长动态值加语音/History 整套实机尚未覆盖。 |
| C11 | 空值/未绑定/坏 token | PASS | 严格 token/缺失引用/无副作用自动化；errors-problems-fast.png 实机明确显示缺失物品与非法动态内容错误并阻止进入故事；RuntimeCloseout.json 实测有效绑定数量 8→0，以及背包有 8 个物品但未绑定时显示“数据不可用”，不混作 0。Session/绑定文件恢复哈希一致。 |
| C12 | 所有声明支持字段 | PARTIAL | 正文/Choice/Title/Message/Task toast/journal 与完成历史已有实机，完成历史见 RuntimeCloseout.json；2026-09-29 本轮补充候选窗口、候选组展开、个体/组实际提交及奖励数量实机；多玩家、真实超时重放等剩余矩阵继续保留，见 ResourceNames_ItemGui_2026-09-29.md。 |
| D01 | 勾选与禁用 | PASS | Repeat0334Probe、RepeatAuthoring；周期卡片、冷却、scheduled-eligibility 实机证据。通过范围按对应测试与 Native_Verification 记录。 |
| D02 | Inline/Inspector 完整编辑 | PASS | RepeatCloseout.json：原生双向操作冷却数值/单位、定时周期、星期、月日、HH:mm；2/29 与 12/31、每日/每周/每年、无条件切换，双入口同步，恢复原文件哈希一致。 |
| D03 | 折叠与摘要 | PASS | 原有 220ms 动画/连线专项加 repeat-reversal 25 帧实机录制；实际 62/132/256/581ms Toggle，动画中反向、完全收起、恢复展开；抽查正文不缩放、连线稳定。 |
| D04 | 无条件回归 | PASS | Repeat0334Probe、RepeatAuthoring；周期卡片、冷却、scheduled-eligibility 实机证据。通过范围按对应测试与 Native_Verification 记录。 |
| D05 | 冷却基准 | PASS | Repeat0334Probe、RepeatAuthoring；周期卡片、冷却、scheduled-eligibility 实机证据。通过范围按对应测试与 Native_Verification 记录。 |
| D06 | 冷却单位与输入 | PASS | Repeat0334Probe、RepeatAuthoring；周期卡片、冷却、scheduled-eligibility 实机证据。通过范围按对应测试与 Native_Verification 记录。 |
| D07 | 完成时间点边界 | PASS | Repeat0334Probe、RepeatAuthoring；周期卡片、冷却、scheduled-eligibility 实机证据。通过范围按对应测试与 Native_Verification 记录。 |
| D08 | 每周/每年 | PASS | Repeat0334Probe、RepeatAuthoring；周期卡片、冷却、scheduled-eligibility 实机证据。通过范围按对应测试与 Native_Verification 记录。 |
| D09 | ACTIVE 跨计划时刻 | PASS | Repeat0334Probe、RepeatAuthoring；周期卡片、冷却、scheduled-eligibility 实机证据。通过范围按对应测试与 Native_Verification 记录。 |
| D10 | 下线、重启、相同包 reload | PASS | Repeat0334Probe、RepeatAuthoring；周期卡片、冷却、scheduled-eligibility 实机证据；重连/重启快照已测；scheduled-after-restart-reload.json 核对完成时刻仍为 13:54:37.501，与原 GUI 一致；相同包 reload 日志已记录。 |
| D11 | ERROR | PASS | Repeat0334Probe、RepeatAuthoring；周期卡片、冷却、scheduled-eligibility 实机证据。通过范围按对应测试与 Native_Verification 记录。 |
| D12 | 启动入口与重复请求 | PASS | repeat-all-entries-final.log：8 个生产服务入口 × 冷却/每日定时，逐个首次启动、ACTIVE 重入、尚未到期、NBT 恢复后到期重启、ERROR；所有拒绝和重复请求前后完整 NBT 相同，接受后激活时刻只更新一轮。另有 Forge coordinator/Actor 仲裁专项与区域实机。发现并修复 NPC 候选把跨故事 Flow 当作 Logic 输入的缺陷。 |
| D13 | 切换/撤销/重开 | PASS | Repeat0334Probe、RepeatAuthoring；周期卡片、冷却、scheduled-eligibility 实机证据。通过范围按对应测试与 Native_Verification 记录。 |
| D14 | 老内容加载 | PASS | Repeat0334Probe、RepeatAuthoring；周期卡片、冷却、scheduled-eligibility 实机证据。通过范围按对应测试与 Native_Verification 记录。 |

范围说明：PASS 行没有自动扩大到所有分辨率/DPI/网络故障组合。性能对比见 Performance_Regression.md；UI 证据见 UI_Evidence.md。不得把本表未验证项改写为整版完成或 Release ready。


## 2026-09-29 资源名称 / 物品 GUI 增量

详见 [实施与证据](ResourceNames_ItemGui_2026-09-29.md)。Core 466通过/12跳过；WPF普通621通过/1跳过，性能2通过；Java对应探针通过。实际验证名称拖入/改名撤销重做、最终EXE的中文IME及保存重开、四模式显示、奖励物品格/组展开/原生提示、真实提交与奖励数量。仅补充所列覆盖，不关闭 C10 或 C12 其余实机矩阵。USER_ACCEPTED=NO，RELEASE_READY=NO。

## 2026-09-29 四项 UI 回归修正
本轮见 [Four_UI_Fixes_2026-09-29.md](Four_UI_Fixes_2026-09-29.md)。623 项 WPF 回归通过、1 跳过；Java 构建与任务探针通过。原生实机证据已记录。C10/C12 剩余项不关闭，USER_ACCEPTED=NO。


## 重复卡片与高度过渡补正
见 [Repeat_Card_Height_2026-09-29.md](Repeat_Card_Height_2026-09-29.md)：9 项相关回归通过，原生双入口操作完成。USER_ACCEPTED=NO，既有待验项保留。


## 2026-09-30 物品数量覆盖层修正
见 [Item_Count_Overlay_2026-09-30.md](Item_Count_Overlay_2026-09-30.md)：去除黑底，修复深度遮挡，隔离信标实机检查完成。USER_ACCEPTED=NO，旧待验项保留。


## 2026-09-30 媒体延迟定位与修复
见 [Media_Latency_2026-09-30.md](Media_Latency_2026-09-30.md)。7 项媒体/生命周期探针及构建通过；原生单机/联机记录了失败退避、暖缓存、30 秒纹理回收、逐 tick 上传、过期丢弃。冷缓存联机停等传输仍慢，严格前后帧分布及部分实机矩阵未覆盖。C10/C12 不关闭，USER_ACCEPTED=NO，RELEASE_READY=NO。

## 2026-09-30 联机媒体窗口增量
见 [Media_Window_2026-09-30.md](Media_Window_2026-09-30.md)：相同隔离冷缓存 A/B，整包从 99.1214 秒降至 14.6925 秒；8 项探针和构建通过，最终构建验证失败不阻塞、重连只补缺失媒体、JVM 重启重新校验且不重新下载。仅更新上一轮停等瓶颈结论；公网/多人矩阵及原 C10/C12 未覆盖项不关闭。USER_ACCEPTED=NO，RELEASE_READY=NO。

## 2026-09-30 NPC 名称与任务按钮

见 [Speaker_Task_Buttons_2026-09-30.md](Speaker_Task_Buttons_2026-09-30.md)：NPC 名称居中于头像列、主题强调色与粗体；任务底部改为实际按钮，“已完成／进行中”和“追踪／取消追踪”原生点击验证通过。覆盖三种主题、长名字提示、无头像、空任务禁用、完成记录及任务窗口缩小后的点击。构建和相关探针通过，权威 JAR 已更新。旧待验项保留，USER_ACCEPTED=NO，RELEASE_READY=NO。

## 2026-09-30 已完成任务内容补正

上一轮完成页面点击检查漏掉了原始结果端口和空目标内容问题。现按 [Completed_Task_Review_2026-09-30.md](Completed_Task_Review_2026-09-30.md) 修正：保留实际完成目标，去掉玩家界面的内部结果 ID；旧记录只从匹配结算存档补回，不重复计数。构建、相关探针及隔离原生单／多目标、滚动、重启验证通过。以前的按钮点击证据保留，其内容可读性结论以本记录为准。其他旧待验项不关闭，USER_ACCEPTED=NO。

## 2026-09-30 头像自适应与旧记录参考

见 [Portrait_Legacy_Review_2026-09-30.md](Portrait_Legacy_Review_2026-09-30.md)：头像按高度与宽度自适应并垂直居中，标准容量配置不变；旧记录无历史目标时按稳定 ID 展示当前任务参考内容，真实次数和历史快照不变。六项探针、隔离单机及独立服务器原生验证通过，正式文件及单机副本数据字节一致。原报告中无快照只显示“无法还原”的行为由本轮参考展示补足；旧证据保留。加载占位强制延迟抓拍及旧 C10/C12／媒体未覆盖项保留，USER_ACCEPTED=NO。

## 2026-09-30 对话字体缩放修正

见 [Dialogue_Font_2026-09-30.md](Dialogue_Font_2026-09-30.md)：名字跟随字号，放大文字按实际屏幕像素对齐，标题／分页／记录／预览共用实际倍率。三项探针及隔离原生 GUI 因子 1～4 × 三字号、真实 GL 最近邻与恢复检查通过。权威 JAR 0.3.3.4，1,800,595 字节，SHA-256 `A95138D18823EA2BE66F5D55D90B3A9538D6FA543AE4EC958BA6D57136C8630C`。Studio 无改动；旧待验项保留，USER_ACCEPTED=NO、RELEASE_READY=NO。


## 2026-09-30 对话头像留白

头像右移 3 GUI 单位，名字和悬停同步；完整分割线与分页宽度不变。构建及三个对话探针通过，隔离 Win32 实机覆盖标准／小窗口、100%／150%。八项隔离文件恢复，权威 JAR 已更新。详见 [记录](Portrait_Inset_2026-09-30.md)。USER_ACCEPTED=NO；此前未覆盖项不关闭。


## 2026-09-30 对话按钮留白

自动／记录整体左移 3 GUI 单位，分割线不变。构建和对话探针通过，原生隔离实机检查大小窗口及按钮实际点击。权威 JAR 已更新，隔离文件已恢复。详见 [记录](Dialogue_Button_Inset_2026-09-30.md)。USER_ACCEPTED=NO；其他未覆盖项不关闭。
