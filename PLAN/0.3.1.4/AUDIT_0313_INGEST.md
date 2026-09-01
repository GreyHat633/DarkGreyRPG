# DarkGrey_RPG 0.3.1.4 — 0.3.1.3 原始审计摄取记录（Gate 0A / 0B）

## 原始证据

- 原始文件：`PLAN/0.3.1.3审计.docx`
- SHA-256：`85B786F6437B0A58AD807B90860D8ECD3FD5CE3C0E332F91434802CDE4997798`
- 文件大小：761,408 bytes
- 读取时间：2026-09-01 19:47:16 +08:00
- 阅读范围：全文 5 页、8 条原始文字、7 张内嵌截图；逐页检查截图中的鼠标/端口/连线、节点位置、Inspector 层级和 Choice 行列关系。
- 证据边界：原始 DOCX 与截图可以确认用户观察和视觉现象，但不自动等同于本机动态复现。下表只使用 PLAN 允许的初始状态，不写 `PASS`。

## 8 条审计摄取

| 编号 | 原文行为核心 | 截图 / 页码 | 严重级别 | 初始复现状态 | 初始源码状态 |
|---|---|---|---|---|---|
| 0313-01 | Type 下拉选项切换无效；逐字输入很慢；当前 canonical 回写使字段不能临时清空后再输入。 | 第 1 页；第 1 页下半截图（Task Objective Inspector） | P0 | `NEEDS_LIVE_REPRO` | `SOURCE_CONFIRMED`：Objective Type/Target 的 TwoWay 绑定直接触发 canonical mutation，setter 后又刷新投影。 |
| 0313-02 | 删除节点每次弹确认框，要求移除确认或提供“不再询问”。 | 第 1 页文字 | P2 | `NEEDS_LIVE_REPRO` | `SOURCE_CONFIRMED`：节点删除命令当前走 `MessageBox` 确认分支。 |
| 0313-03 | 鼠标离开可连接目标端口后，端口仍持续发光。 | 第 1 页文字；第 2 页上半截图 | P1 | `NEEDS_LIVE_REPRO` | `SOURCE_CONFIRMED`：候选端口高亮状态与命中结果的退出/取消清理不是同一生命周期。 |
| 0313-04 | 轻点输出端口会出现左侧幽灵线段；占用的 single Flow Output / Logic Input 应拖动真实已连接端点并固定另一端，而不是先删再造。 | 第 1 页文字；第 2 页中部截图 | P0 | `NEEDS_LIVE_REPRO` | `SOURCE_RISK_IDENTIFIED`：现有 pending drag / reconnect 几何和 single-wire mutation 路径不足以证明四种手势契约。 |
| 0313-05 | Actor / Item Inspector 的标签和值层级不清楚；“拥有 / 引用状态”语义无法判断。 | 第 2 页底部文字；第 3 页上半截图 | P2 | `NEEDS_LIVE_REPRO` | `SOURCE_CONFIRMED`：当前资源 Inspector 复用通用字段排列，未提供 PLAN 要求的主值/辅助值信息架构。 |
| 0313-06 | 调整节点位置后，重启 Studio 布局被重置。 | 第 3 页文字；第 4 页上半截图 | P0 | `NEEDS_LIVE_REPRO` | `SOURCE_CONFIRMED`：节点坐标只有进程内 host 状态，没有独立、可恢复且不污染 Runtime 数据的 Studio sidecar。 |
| 0313-07 | Objective 目标选择切换非常慢，随后 Studio 进程闪退。 | 第 4 页中部文字与截图 | P0 | `REPRODUCED` | `SOURCE_CONFIRMED`：真实 Release 崩溃栈确认 `SelectedObjectiveActor` → canonical mutation → `RefreshFromHost()` → WPF selection 回写重入，最终 `STACK_OVERFLOW`。详见 `CRASH_0313_07.md`。 |
| 0313-08 | Session Choice 的 option 行与输出端口行上下错位，无法可靠判断选项和出口映射。 | 第 4 页底部文字；第 5 页截图 | P1 | `NEEDS_LIVE_REPRO` | `SOURCE_RISK_IDENTIFIED`：当前 Choice 内容行与动态端口布局由不同投影/几何链计算，结构上允许错位。 |

## Gate 结论

- Gate 0A：`COMPLETE` — 原始 DOCX 已逐页读取，未以 PLAN 摘要或 Handoff 替代。
- Gate 0B：`COMPLETE` — 8 项均已绑定原文行为、页码/截图、严重级别、初始复现与源码状态。
- 本记录不宣称任何问题已修复或验收。
