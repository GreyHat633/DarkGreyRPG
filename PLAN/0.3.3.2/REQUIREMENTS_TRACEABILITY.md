# 0.3.3.2 需求落实状态（施工中）

本表记录当前工作树的真实状态，不代表验收完成。IMPLEMENTED 指已有实现及相应自动证据，不等于实机 PASS。所有 R 的用户验收均未发生。

| 需求 | 当前实现与证据 | 剩余事项 |
|---|---|---|
| R01–R02 | LinePagesEditor 共享外层折叠；状态由 host/node/page_id 定位，标题右侧 ▲/▼；LinePagesAuthoringTests | 实机检查两处编辑器、长列表、缩放和视觉布局 |
| R03 | DgrUiPalette 三主题；设置、会话、任务、选择器、记录、Tracker 使用语义颜色 | 逐个在用界面的全覆盖审计、深浅背景实机对比尚未完成 |
| R04 | 设置含不透明度、字号、速度及独立样本文字预览；会话/选项/记录/Tracker 缩放 | 任务菜单等剩余文字区域的字号一致性、小屏布局、透明背景描边检查 |
| R05 | Session voice/music 乘 DGR 对应系数；原版滑块不改；留声机独立混音器已乘专属/Music/Master 系数，取得系统输出匹配 | 三类音量滑块完整实机组合与主观音质检查仍待验 |
| R06 | 三页设置、恢复本地默认、实际 settings KeyBinding 开关及鼠标键关闭 | 实机改键、聊天、会话前后台回归 |
| R07 | TaskTrackerHud 右侧中部；读取计分板宽度让位；与 Toast 绘制独立 | 不同分辨率及计分板实机检查 |
| R08–R09 | TaskTrackingSelection 最多 3 个；选择持久化按玩家/存档或服务器隔离；新 received 事件补空位；全部目标在可滚动 viewport | 目标多/小屏/重连实机覆盖；任务 I 菜单被跟踪 HUD 遮挡的视觉检查 |
| R10 | 延续服务端状态通知；持有数量采样只更新投影；TaskNotificationsPlanProbe | 实机计数、合并卡片回归 |
| R11 | 服务端 held_count 与 runtime current 分离；TaskObjectiveText 共享格式、交互/区域不加 0/1 | 不同目标组合实机核对 |
| R12 | 未增加任何 Toast 配置入口 | 设置页面视觉复核 |
| R13 | SETTLED 独立最小摘要归档、重置后保留、重复计数、玩家隔离及 NBT 恢复探针通过；不改奖励状态 | 非空完成历史的真实 UI 流程待覆盖 |
| R14–R16 | DialogueBacklog 250 条/上下文、内存隔离、只追加已显示文本；Choice 由服务端成功回执确认；上下文不含维度；独立 revision 排版缓存；DialogueBacklog0332Probe | 真实 A→B→A、同服重连及权威 NBT 前后对照；会话异常中断/回执丢失组合 |
| R17 | 会话“记录”按钮与默认未绑定可改键入口；记录窗口不发会话动作 | 实机前后台输入、鼠标改键的全部组合 |
| R18 | 长选项按相同测量结果绘制与命中；自动高度、分页、超长内部滚动、Tab/箭头焦点、Enter 选择、PageUp/PageDown 阅读 | 实机极长文本、最大字号、小屏与改键冲突检查 |
| R19 | 编辑 sidecar GraphCommentFrame；显式成员；实时拖动预览及一次 Undo；缩放框不缩放节点；删除保留节点；完整成员复制新 ID 映射；Project atlas 可建框 | 空框复制、Project atlas 整组复制体验、Frame 标题精确搜索定位；真实拖拽/缩放/视觉验收 |
| R20 | 项目搜索覆盖未打开资源和当前未保存编辑；未打开资源异步读取/索引，已打开按变更失效缓存；稳定 page_id 定位；结果显示引用来源；异步陈旧结果保护 | 只读引用窗口具体句定位、Choice 具体选项焦点、Frame 焦点、所有显示名称与别名覆盖、大项目性能实测 |
| R21–R25 | 已实现；原生 UI 实测红石断电约 27 秒后同实例续播、离开停止/返回重头、双客户端错时个人播放、范围线框 | 多设备重叠、权限撤销及破坏、全分辨率组合仍待补齐 |
| R26 | 两平台匿名单曲解析/完整 MP3 解码/运行播放已实现；两首免费样例在旧及新后端均有系统输出匹配 | 实际短链、受限曲目失败样本扩展；不是全曲库保证，也不是主观听音通过 |
| R27–R29 | 本地草稿/原生导入/试听跳转/显式确认分块上传/原子清单/共享持久引用/pin/延迟 GC/独立缓存已实现；本地导入保存实机及存储探针通过 | 大量导入、断传/磁盘失败实机、世界移动备份 UX 仍待覆盖；Story 10 包/3 并发保持 |
| R30 | **PARTIAL**：隔离独立服务端及两个真实客户端已运行，错时进入和单方离开已验证 | 权威 Studio 同包导出→正式 dist JAR 安装交互完整链路、主观听音仍待验 |

## 不能忽略的证据限制

2026-09-17 已通过 Windows 原生 UIA/Win32/GDI 实际点击、拖动、撤销、保存重开及游戏设置/改键操作，详见 NATIVE_UI_ACCEPTANCE.md。上表剩余项以该报告的明确子用例补充：分组拖动/一次撤销、分句折叠、未保存第 2 句搜索与定位已通过；搜索结果显示有截断缺陷。没有同包导出/游戏安装证据。

原截图接口不兼容已通过用户指定的原生 UI 路径绕过；E/F 未实施及未覆盖用例仍属本版范围。部分实机通过不能替代完整验收。

ENGINEERING_IMPLEMENTED=NO

USER_ACCEPTED=NO


## 2026-09-17 UI-0332-01 修复复验

搜索列表禁止横向滚动，结果内容横向 Stretch，路径和片段明确换行。原生 UI 使用同一项目实际搜索并双击结果：316 像素容器中结果行宽 316；拖动分栏缩至 256 后行宽 256，路径及正文完整换行；第二句定位仍成功。证据：evidence/native-ui/113-fixed-results.png、114-fixed-navigate.png、115-fixed-narrow.png 及对应 UIA 树。UI-0332-01=FIXED_VERIFIED。此前 FAIL 是修复前记录，予以保留。

AuthoringSearch0332Tests：3 PASS / 0 FAIL（evidence/search-fix.trx）。self-contained win-x64 Release 已更新权威 dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe；ProductVersion 0.3.3.2，133909999 字节，SHA-256 7965B4980FA1A1CF7E2421A7B59F6C0D718EDC643E8A5F0D9D53DE35CF4613AA。原生复验运行的正是这个 EXE。

本次只关闭有明确复现证据的搜索布局缺陷。E/F 未实施和同包、非空任务/历史、双客户端、听音等未覆盖项保持开放；整体验收仍不完整，USER_ACCEPTED=NO，RELEASE_READY=NO。
