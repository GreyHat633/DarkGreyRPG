# 0.3.3.4 实机产物

USER_ACCEPTED=NO；RELEASE_READY=NO。只操作 `.tooling/0334` 隔离工程/服务器/两客户端。

| 产物 | 版本 / 字节数 | SHA-256 |
|---|---|---|
| `E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe` | 0.3.3.4 / 142489302 | `B120FA91138B805487C3D95C46A7257441FF32E1BC8C17E306160E84DF22EA3F` |
| `E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.4.jar` | 0.3.3.4 / 1759480 | `5DCFF88E1506BD3723788757BB8176CD302AF1FE1A49AE1EA27872F8B9454428` |

正式导出包位于 `evidence/native-ui/Native0334Coverage.dgrs`、`Native0334Scheduled.dgrs`。分别由 `SamePackage.json`、`ScheduledSamePackage.json` 核对安装哈希。扩展测试图的初始结构由脚本准备，随后实际 Studio 编辑、校验、正式导出；没有脚本 ZIP 替换导出包。

实际链路：双玩家不同名字/等级/物品数 → 当前页数值变化及重连保持快照 → Choice → Title/Subtitle → Message；Task toast/旧任务日志显示实际解析文本。诊断在线/离线重启、多 Placement、权限撤销、异常关联、22 条分页/窄窗口均留有截图。

冷却：正常结束后 30 秒内拒绝，过期由区域驱动重启。定时：每日 23:59 正式包正常结束后等待计划时刻，提前区域触发仍 TERMINATED。跨时间边界不更改系统时钟，以可注入时钟探针补充。

离线查询只读证据：`offline-readonly.json`、`restart-all-readonly.json`。后者核对六个 DGR 数据文件重启/查询前后哈希相同。

2026-09-29 UI/OP 修正后，隔离游戏及验收 Studio 已关闭，隔离工程已恢复并保留。没有改用户正式世界、没有发布 Git 或 Release。

2026-09-29 UI/OP 修正及本轮验证范围见 [UI_OP_Corrections_2026-09-29.md](UI_OP_Corrections_2026-09-29.md)。上述哈希为本轮最终产物；此前截图及导出包保留其历史证据边界。


## 2026-09-29 资源名称与物品 GUI 最新交付

当前权威 EXE / JAR 已更新为本轮构建；之前条目为历史证据。

- Studio `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`：0.3.3.4，142497494 bytes，SHA-256 `384CE5B5CCBF514A3C816D91F9E3628259A9B03837506A76C7968EDB68E78D27`。
- MOD `dist/darkgrey_rpg-0.3.3.4.jar`：0.3.3.4，1776312 bytes，SHA-256 `5E3DE05A480B9AB13F5EAC5A1750C63E09727FAF6383F60F5493C956322E6B2C`。
- 报告：`ResourceNames_ItemGui_2026-09-29.md`；机器读数：`evidence/Delivery.json`。
- 开发验证版本，不表示 USER_ACCEPTED 或 RELEASE_READY。

## 最新四项 UI 修正产物
见 [Four_UI_Fixes_2026-09-29.md](Four_UI_Fixes_2026-09-29.md) 和 evidence/Delivery.json；权威 EXE 与 JAR 已更新。


最新 Studio 产物与哈希见 [Repeat_Card_Height_2026-09-29.md](Repeat_Card_Height_2026-09-29.md) 和 evidence/Delivery.json。


最新 JAR 与哈希见 [Item_Count_Overlay_2026-09-30.md](Item_Count_Overlay_2026-09-30.md)。


## 2026-09-30 媒体延迟增量
交付和测量边界见 [Media_Latency_2026-09-30.md](Media_Latency_2026-09-30.md)，诊断汇总见 evidence/media-latency/local-summary.json。JAR 1,786,880 字节，SHA-256 C258400D3BF7CB1232BF687C81878F56EFB4B3DE4E4AC1BC6EF359DD1B49D7C7。仅确认所列覆盖，不代表整版验收。

## 2026-09-30 联机媒体窗口交付
最新 JAR 1,788,065 字节，SHA-256 175094D5ABA5EBB2AAFF685C057966776C4F9F81450FC3FC91B2775AB1EBB478。实机 A/B、失败重连、暖缓存重启见 [Media_Window_2026-09-30.md](Media_Window_2026-09-30.md) 与 evidence/media-window。替换上一轮 JAR，旧报告保留为历史证据。

## 2026-09-30 NPC 名称与任务按钮交付

最新权威 JAR 版本 0.3.3.4，1,788,793 字节，SHA-256 `C2B2B230CFB8D9EDB133B7EEA06EAA041F4762AAA36C8AFC4DD029F375289F13`。构建及原生点击、名字布局证据见 [Speaker_Task_Buttons_2026-09-30.md](Speaker_Task_Buttons_2026-09-30.md)。替换上一轮 JAR；Studio 未变更，旧待验项继续保留，USER_ACCEPTED=NO。

## 2026-09-30 已完成任务回顾交付

最新权威 JAR 0.3.3.4，1,789,832 字节，SHA-256 `85552EC69DE674475A6AC81C53BB2C4969B7BD93CF99E67342860D31A1102F12`。实际完成目标、旧记录补回及重启证据见 [Completed_Task_Review_2026-09-30.md](Completed_Task_Review_2026-09-30.md)。替换上一轮 JAR，Studio 未变更，USER_ACCEPTED=NO。

## 2026-09-30 头像自适应与旧记录参考交付

权威 JAR 0.3.3.4，1,793,662 字节，SHA-256 `BE5C57EA6BA82AF9BA7CC4EB34D156F3427EEDF607A5337E1210FD18A3461F48`。客户端／服务端同包，实机与数据不变证据见 [Portrait_Legacy_Review_2026-09-30.md](Portrait_Legacy_Review_2026-09-30.md) 及 evidence/portrait-legacy。Studio 无改动，原待验项继续保留，USER_ACCEPTED=NO、RELEASE_READY=NO。

## 2026-09-30 对话字体缩放修正

见 [Dialogue_Font_2026-09-30.md](Dialogue_Font_2026-09-30.md)：名字跟随字号，放大文字按实际屏幕像素对齐，标题／分页／记录／预览共用实际倍率。三项探针及隔离原生 GUI 因子 1～4 × 三字号、真实 GL 最近邻与恢复检查通过。权威 JAR 0.3.3.4，1,800,595 字节，SHA-256 `A95138D18823EA2BE66F5D55D90B3A9538D6FA543AE4EC958BA6D57136C8630C`。Studio 无改动；旧待验项保留，USER_ACCEPTED=NO、RELEASE_READY=NO。


## 2026-09-30 对话头像留白

头像右移 3 GUI 单位，名字和悬停同步；完整分割线与分页宽度不变。构建及三个对话探针通过，隔离 Win32 实机覆盖标准／小窗口、100%／150%。八项隔离文件恢复，权威 JAR 已更新。详见 [记录](Portrait_Inset_2026-09-30.md)。USER_ACCEPTED=NO；此前未覆盖项不关闭。


## 2026-09-30 对话按钮留白

自动／记录整体左移 3 GUI 单位，分割线不变。构建和对话探针通过，原生隔离实机检查大小窗口及按钮实际点击。权威 JAR 已更新，隔离文件已恢复。详见 [记录](Dialogue_Button_Inset_2026-09-30.md)。USER_ACCEPTED=NO；其他未覆盖项不关闭。
