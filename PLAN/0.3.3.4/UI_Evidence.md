# 0.3.3.4 UI 证据索引

Windows 原生 UIA / Win32 / CopyFromScreen；权威 Studio 0.3.3.4，Windows 缩放 125%，图中约 89%/常规缩放；深浅主题。Minecraft 1.7.10、DGR 0.3.3.4，1280×800 客户区，当前 GUI scale 2。截图统一在 `evidence/native-ui/`；后缀 `.txt` 保存原生窗口或 UIA 状态。

| 内容 / 入口 | 证据 |
|---|---|
| 权威 EXE 搜索 1→0→1 | `../Native_UIA_Verified.json` |
| IME 候选提交与下一次 Enter、新 Page ID/Undo | `IME_Input.json`、`ime-composition.png` |
| 原子块替换/草稿撤销、主题 | `atom2-*`、`draft-*`、深浅主题相关截图 |
| 资源库物品实际拖放、保存/撤销 | `drop-result.png`、`drop-saved.png` |
| 重复每日/每周/每年 Inspector | `schedule-daily.png`、`schedule-weekly.png`、`schedule-yearly.png` |
| 每年日期拒绝/接受，节点与 Inspector | `yearly-invalid.png`、`yearly-leap.png`、`yearly-inline-zoom.png`、`inline-daily-edited.png`、`daily-both-save.png` |
| 双玩家正文与稳定快照 | `witness-value.png`、`native-reconnected-snapshot.png`、`two-players-nbt.json` |
| Choice、Title/Subtitle、消息 | `coverage-choice.png`、`coverage-title.png`、`witness-choice-count.png`、`chat-held.png` |
| Task toast / journal 动态说明 | `task-toast-fixed.png`、`journal-fixed.png` |
| 在线/离线重启、异常与权限 | `debug-online.png`、`offline-all-records.png`、`missing-placement-fixed.png`、`denied-fixed.png` |
| 窄窗口完整缩放、长列表第二页/滚动、未知玩家 | `resize-fixed.png`、`page-two.png`、`page-two-scroll.png`、`no-player.png` |
| 冷却/每日运行接线 | `cooldown-rejected.png`、`scheduled-eligibility.png`、`scheduled-before-boundary.txt` |

操作脚本保存在 `.tooling/0334/Native/`；动作后截图/存档/哈希共同验证，单张静态截图不能证明完整交互。早期失败截图保留用于问题追踪；新版修复以带 fixed 的证据和记录为准。并未覆盖所有 Windows DPI、Minecraft GUI 字号组合。
