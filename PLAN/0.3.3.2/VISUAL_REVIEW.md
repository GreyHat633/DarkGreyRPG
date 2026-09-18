# 视觉验收记录

VISUAL_REVIEW=PARTIAL_FAIL

2026-09-17 根据用户要求使用 Windows 原生 UI Automation / Win32 / GDI 实机操作。此前 sky 的 SetIsBorderRequired / 0x80004002 已通过另一条原生控制路径绕过；不再把“仅枚举窗口”作为本轮结论。

完整操作矩阵、证据边界及缺陷见 [NATIVE_UI_ACCEPTANCE.md](NATIVE_UI_ACCEPTANCE.md)。截图、UIA 树、偏好文件与项目夹具位于 evidence/native-ui/。

已观察：Studio 分句折叠同步、第二句未保存搜索/跳转、分组框实际拖动/单次撤销、保存重开；游戏设置三主题/透明度/最大字号预览/速度边界/分项音量控件/恢复默认/改键，以及任务与历史空态窗口。

**未通过：搜索长路径和片段被截断。** 31-search-result.png、32-search-navigate.png：316 像素结果容器内生成 504 像素结果行，内容需要横向滚动，未完整换行。本轮未修复。

仍缺：多分辨率全窗口主题字号矩阵、非空 Tracker/历史、长选项、分组缩放复制/连线层级/精确搜索、只读搜索、同包游戏剧情。设置预览不能证明真实剧情渲染、音量配置不能证明真实听音。E/F 未实施，USER_ACCEPTED=NO，RELEASE_READY=NO。

## 2026-09-17 UI-0332-01 修复复验

搜索列表禁止横向滚动，结果内容横向 Stretch，路径和片段明确换行。原生 UI 使用同一项目实际搜索并双击结果：316 像素容器中结果行宽 316；拖动分栏缩至 256 后行宽 256，路径及正文完整换行；第二句定位仍成功。证据：evidence/native-ui/113-fixed-results.png、114-fixed-navigate.png、115-fixed-narrow.png 及对应 UIA 树。UI-0332-01=FIXED_VERIFIED。此前 FAIL 是修复前记录，予以保留。

AuthoringSearch0332Tests：3 PASS / 0 FAIL（evidence/search-fix.trx）。self-contained win-x64 Release 已更新权威 dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe；ProductVersion 0.3.3.2，133909999 字节，SHA-256 7965B4980FA1A1CF7E2421A7B59F6C0D718EDC643E8A5F0D9D53DE35CF4613AA。原生复验运行的正是这个 EXE。

本次只关闭有明确复现证据的搜索布局缺陷。E/F 未实施和同包、非空任务/历史、双客户端、听音等未覆盖项保持开放；整体验收仍不完整，USER_ACCEPTED=NO，RELEASE_READY=NO。
