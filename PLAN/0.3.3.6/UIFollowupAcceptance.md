# 故事图谱连线与拖动反馈修正

2026-10-05；版本保持 0.3.3.6。本轮依据用户最新反馈修正交互，取代上一轮“Project 禁止连线编辑”和“公开输出保留上下按钮”的行为。上一轮按禁用连线验收，没有核对故事组织的实际使用需求，不能作为本轮牵线功能的验证证据。

## 实现

- 删除共用公开输出组件里的上下排序按钮及空列。Story Inspector、Session/Task 聚合节点内与 Inspector 都只保留拖动手柄；名称、端口身份和两类独立卡片不变。
- 移除 Project 画布拒绝拖线和 Host 把全部 Project 来源视为只读的两处限制。恢复本地来源的新增、改接、剪刀按钮与 Ctrl 临时剪线。引用来源输出仍由原包维护，本地故事接入引用故事输入仍可编辑。普通手工组合入口不恢复。
- 修正连接提交后顶部数量滞后一拍的问题。
- 左栏显示拖动手柄，拖动时整个来源故事/故事组淡化，显示跟随鼠标的名称卡片和蓝色插入线。顶部、底部和组间插入使用同一判定；支持边缘滚动，Esc/拖出列表取消。组成员不因排序变化。
- 拖动预览适配两种主题，不透出原列表文字；组卡片完整显示，长 UID 使用省略及悬停全文，避免横向溢出。

## 验证

本轮证据目录：[evidence/UIFollowup](evidence/UIFollowup)。

- 定向 WPF 回归：41 通过、0 失败。覆盖 Project Flow/Logic 实际手势入口、连线创建、改接、断开、保存重开、撤销重做、连接数量通知、引用来源只读、本地接入引用输入，以及派生故事组名称恢复。见 `targeted-final.log`、`followup-targeted-final.trx`。
- 最终完整 WPF 回归：661 通过、0 失败、1 项原有性能基准跳过；见 `wpf-full-clean.log`、`followup-full-clean.trx`。此前轮次中旧“本地不可断线/连线”断言和一次图片异步解码超时均保留在开发日志中；已将两处旧连线断言改为真实连接及自动持久化验证，相关边界测试 2 项通过，图片测试单独复测通过。磁盘满时的中断轮次不计为通过。
- Core/Runtime 源码及二进制未改变，沿用前一轮 Core 495 通过与 Java 构建/实机结果，没有把历史结果记作本轮重跑。
- 原生实机：从端口新增本地 Story Flow 连线，文件由 0 变为 1；拖动连线改接到另一 Story，读回稳定端口及新目标；Ctrl 剪断后 1→0，撤销 0→1，重做 1→0，最终恢复。保存重启后仍连接改接后的目标。见 `flow-connected.png`、`reconnected4.png`、`native-wire-cut.json`。
- 原生实机：拖动公开输出后端口顺序对换，两个 port ID 保持不变；见 `native-port-reorder.json`。共用编辑器的节点内与 Inspector 没有上下按钮，见 `aggregate-no-arrows.png`。
- 原生实机：独立故事和故事组排序，拖动中检查预览和插入线，落下后文件变化；撤销/重做与修改前后字节完全一致。深色、浅色预览均清楚，Esc 取消后文件哈希不变。见 `native-navigation.json`、`navigation-cancel.json`、`navigation-preview-verified.png`、`navigation-preview-light.png`。

## 交付与数据

正式运行 `studio/package-studio.ps1`，已更新权威自包含 Windows x64 Release：`dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`。实际启动正常，加载 `Program/coreclr.dll`；没有创建成品包、标签、Release、提交或推送。

部署前后 Data 2,634 个文件，变化数为 0。启动后核对原有 279 个项目文件，变化/缺失数为 0。见 `dist-data-audit.json`、`dist-projects-after-launch.json`、`dist-startup.json`。

| 文件 | 版本 | 字节数 | SHA-256 |
| --- | --- | ---: | --- |
| `DarkGreyRPGStudio.exe` | 0.3.3.6 | 204288 | `8e8bf253b16b1516616d890b8bd604a907cf9d607469e3785d4ab113a5b3b20c` |
| `Program/DarkGreyRPGStudio.dll` | 0.3.3.6 | 1725440 | `b7300e1a6b54aaba5f62cac77918d872f1b8ed11b19fdccdb44004fc0027c2e9` |
| `Program/DarkGreyRPG.Studio.Core.dll` | 未变 | 1303040 | `a5c7af122ccbc29cbb6bc7c21bac25113248ef8df97b040b3b5652149b807653` |

Runtime JAR 保持前次已验证的 `b273b422808eb43f015ad9b574702c07e9e52eba5e2be09151d6311d0e85458e`，无需替换 Minecraft 中的相同 JAR。

E 盘空间不足时，仅处理已确认的 `.tooling/wpf-build` 发布暂存。因 E 盘回收站不能释放 E 盘空间，暂存文件经 D 盘中转逐文件哈希验证后送入 D 盘回收站；没有清空回收站，也未处理项目、Data 或正式程序。各次记录见 `recycled-stage.json`、`recycled-stages.json`、`recycled-historical-stages.json`。当时复核只找到 10 个仍有负载的回收站条目，早先 9 个暂存目录已不在回收站。后续用户已明确确认是其手工清空回收站所致，并要求继续清理；本轮新增清理见 [StorageCleanup.md](StorageCleanup.md)。当时的复核记录保留于 `RecycleBinVerification.json`。

代理实机结果与自动测试分开记录；`USER_ACCEPTED=NO`，未将本轮反馈视作用户验收通过。
# 后续记录

最新的标题整行折叠、节点内排序、公开输出标题及零结算修正见 [TaskUIFixAcceptance.md](TaskUIFixAcceptance.md)。本页保留先前轮次的测试、文件大小及校验值；当前权威记录为 [Delivery.json](Delivery.json)。
