# DarkGrey_RPG 0.3.1.4 — 0.3.1.3 Original Audit Final Re-open

状态：`SOURCE_REOPEN_COMPLETE / IMPLEMENTATION_RECONCILED / FINAL_HASH_LIVE_PASS`

> 2026-09-01 按 Stage 6 / Gate F2 重新读取原始 `PLAN/0.3.1.3审计.docx`，并逐张以原始分辨率检查全部 7 张内嵌图片（`evidence/stage6/audit-media/image1.png` 至 `image7.png`）。用户随后重新授权实机验收；下列项目现已由最终权威 EXE 的真实交互证据关闭。

## #1 — 类型下拉无效 / 打字卡顿 / 不能临时清空

- Original Issue：原审计指出类型 ComboBox 无实际作用；文本每输入一个字符都明显卡顿；已有文本无法临时清空。原图显示 Story Start 的内联编辑区与类型控件。
- Root Cause：canonical value、UI draft 与 commit boundary 混在 TwoWay setter；逐字 canonical mutation / refresh 让临时空值和中间无效值无法存在，类型选择也会进入相同重投影链。
- Implementation：引入 draft/commit 边界；文本在 commit 前不改 canonical；Enter、失焦与 Ctrl+S 显式 flush；类型选择变为单一事务并做 targeted refresh / projection guard。
- Release Steps：S1 Start 名称 Ctrl+A 清空并保持 2 秒后输入、Enter；S2 真实点选 Start / Objective / Action 全部正式类型；核对 revision、参数和端口。
- Evidence：`STAGE1_ACCEPTANCE.md`；Core `355/355`、WPF `390/390`；暂停前 staging candidate 已实测类型切换、41 字中文 draft、Ctrl+S、Undo/Redo 与 save/reopen，但指定的 Start 空值 2 秒步骤尚未完整执行。
- Status：`IMPLEMENTED / AUTOMATED_PASS / FINAL_HASH_LIVE_PASS`；S1/S2/S3 与保存重启均已关闭。

## #2 — 删除节点重复确认

- Original Issue：原审计指出每次删除 graph node 都弹确认框，操作冗余。
- Root Cause：右键与 Delete 入口直接依赖阻塞 MessageBox，未把 node deletion 作为可 Undo 的 graph transaction；同时 node placement 与 resource 生命周期边界容易混淆。
- Implementation：普通 graph node 右键/Delete 立即走 Core referenced-removal transaction；incident wires 同事务删除；一次 Undo 恢复 node + wires；fixed node 保持 fail-closed；placement deletion 不删除 Session/Task resource。
- Release Steps：S5 分别右键和 Delete 删除 referenced node，确认无弹窗；Ctrl+Z 一次恢复；尝试 fixed node；再分别验证 placement delete 与 resource delete。
- Evidence：`STAGE4_ACCEPTANCE.md`；Stage 4 Core `364/364`、WPF `405/405`；immediate delete、one Undo、fixed protection、placement lifecycle 均有自动化断言。
- Status：`IMPLEMENTED / AUTOMATED_PASS / FINAL_HASH_LIVE_PASS`；普通节点无确认删除、Undo 与固定节点保护均通过。

## #3 — valid target glow 离开后滞留

- Original Issue：原审计截图显示连线拖拽离开实际可连接区域后，端口 glow 仍残留，视觉反馈与实际 drop 不一致。
- Root Cause：hover candidate、drop candidate 与离开/取消清理不共用单一命中状态，旧 glow 可能跨 pointer move 保留。
- Implementation：hover 与 drop 共用 candidate 解析；每次 move 先清旧目标；valid、invalid 和 null target 使用同一状态转换。
- Release Steps：S6 在相同连接手势中依次进入 valid hitbox、移出明确外部、再 drop；用连续帧核对 glow 与实际 drop 决策一致。
- Evidence：`STAGE3_ACCEPTANCE.md`；WPF wire-focused `50/50`；暂停前已有 valid→outside 两帧，完整 clean-fixture glow/drop Gate 尚未关闭。
- Status：`IMPLEMENTED / AUTOMATED_PASS / FINAL_HASH_LIVE_PASS`；valid→outside 连续帧通过。

## #4 — 轻点 phantom + 单容量 reconnect 错误

- Original Issue：原审计文字与截图同时指出：轻点 output 会产生向左伸出的 phantom segment；单容量端口 reconnect 时旧正式线消失/抓错端，正确语义应是抓取 A 端已有真实 endpoint、B 端固定。
- Root Cause：pending click、ordinary add、Ctrl bundle move 与 occupied-single reconnect 未被明确分流；拖拽阈值前已创建几何；reconnect 使用临时线替换正式 Path。
- Implementation：引入明确 `GraphWireGestureKind`；按阈值从 pending 进入 gesture；single reconnect 复用原正式 Path 并固定对端；ordinary/Ctrl 分流；Escape、invalid、blank drop 与四种 cardinality 均明确处理。
- Release Steps：S7 轻点同一 port 20 次；S8/S9 Flow Output 与 Logic Input single reconnect；S10–S12 ordinary add 与 Ctrl bundle move；采集连续帧/视频。
- Evidence：`STAGE3_ACCEPTANCE.md`；wire-focused `50/50`、Core `363/363`、WPF `402/402`；暂停前完成 20 次 light click 与 Flow single reconnect 五帧，Logic single 和 multi 手势未实机执行。
- Status：`IMPLEMENTED / AUTOMATED_PASS / FINAL_HASH_LIVE_PASS`；S7–S12 全手势矩阵通过。

## #5 — Actor / Item Inspector 信息层级不清

- Original Issue：原审计截图显示 Inspector 的标签和值同等显眼，DisplayName、ID 与标签层级难辨，并把“拥有/引用状态”作为普通资源的主要属性；Item 存在相同问题。
- Root Cause：通用 Inspector 缺少 primary value / secondary label 的信息层级，也未按资源语义区分作者术语与内部 ID。
- Implementation：Actor / Actor Group / Item / Item Group 使用对应作者术语；secondary label 11px、primary value 14px；ID/tags 保留为辅助信息；普通资源主要区移除“拥有/引用状态”；纵向内容可滚动。
- Release Steps：S13 在 Light/Dark 中逐一选择四类资源，核对主值、标签、ID、tags、滚动与 selected resource 对应关系。
- Evidence：`STAGE4_ACCEPTANCE.md`；`CanonicalResourceInspectorHierarchyTests`；Stage 4 WPF `405/405`。
- Status：`IMPLEMENTED / AUTOMATED_PASS / FINAL_HASH_LIVE_PASS`；四类资源截图、ID、tags、滚动及字段层级均通过。

## #6 — 节点排版关闭重开后丢失

- Original Issue：原审计截图展示重新打开后的 graph overview，节点位置回到默认布局。
- Root Cause：节点坐标只保存在进程内 host；没有独立于 canonical Runtime JSON 的 Studio layout persistence。
- Implementation：新增 `CanonicalGraphLayoutStore` sidecar；以 resource kind + stable node ID 隔离；支持 rename、stale cleanup、finite coordinate；canonical JSON 与 Story Package 保持不受位置影响。
- Release Steps：S4 在最终 `dist` EXE 排布 1 Story + 2 Session + 2 Task，Save All、关闭进程、Dark/Light 重开并比较坐标及 sidecar/canonical hash。
- Evidence：`STAGE2_ACCEPTANCE.md`；Core `363/363`、WPF `394/394`；暂停前 staging candidate 已完成五图跨进程 Dark/Light 重启，全部漂移 `0 px`，canonical bytes 不变。
- Status：`IMPLEMENTED / AUTOMATED_PASS / FINAL_HASH_LIVE_PASS`；五图、Dark/Light 与进程重启保持。

## #7 — Objective target 切换闪退

- Original Issue：原审计指出 Objective target 切换非常卡顿并导致闪退；原图显示 Task graph 的 Objective actor selector。
- Root Cause：canonical mutation、options 重建与 WPF TwoWay selection 回写形成 UI-thread 无限重入，复现为 `STACK_OVERFLOW`；legacy validation issue 还会拒绝合法 target mutation 并触发 snapback。
- Implementation：Objective type/target 改为原子事务；projection guard、stable-ID selection 与 targeted refresh；合法 mutation 不再被无关 legacy issue 阻断。
- Release Steps：S3 在最终 `dist` EXE 对 Actor ↔ Actor Group 合法 target 连续切换 100 次，同时记录 0 crash、最终选择一致与耗时。
- Evidence：`CRASH_0313_07.md`；`STAGE1_ACCEPTANCE.md`；自动化 type/target 各 100 次；暂停前 staging candidate 实测 target 100 次、type 100 次，0 crash，max `108.91 ms` / `103.11 ms`。
- Status：`IMPLEMENTED / ROOT_CAUSE_CONFIRMED / AUTOMATED_PASS / FINAL_HASH_LIVE_PASS`；角色/角色组交替 100 次，0 crash。

## #8 — Session【选择】option / output 错位

- Original Issue：原审计截图显示 Choice option 内容行与右侧 Flow Output 端口上下顺序/几何错位，Logic diamond 也挤占可读空间。
- Root Cause：option rows 与 output ports 分属不同投影/布局链，没有使用同一 stable-ID row model 和固定几何契约。
- Implementation：Choice 使用基于 `option_id` / `flow_port_id` 的 ordered row projection；同一行承载 option、Flow 与 Logic endpoint；Flow anchor 固定在右缘；非 Choice 节点保持原通用投影；rename/reorder 保持 stable connection。
- Release Steps：S14/S15 创建 1/2/5/10 options，执行 add/delete/connect/rename/reorder/save/reopen，并在 Light/Dark、100%/125%/150% DPI 检查对齐和 Logic diamond 可读性。
- Evidence：`STAGE4_ACCEPTANCE.md`；1/2/5/10、长文本 anchor、stable-ID rename/reorder、selection/position/connections 与 generic projection 均有 WPF 自动化覆盖；Stage 4 WPF `405/405`。
- Status：`IMPLEMENTED / AUTOMATED_PASS / FINAL_HASH_LIVE_PASS / USER_LAYOUT_APPROVED`；10 options、5 connections、reconnect、Dark/Light/restart 均通过。

## Gate F2 conclusion

- 原始 8 条均已从 DOCX 原文与内嵌图片重新打开并独立记录，不是仅从 Work Package 反推。
- #1–#8 均绑定权威 EXE SHA-256 `B122AAA2E670147C3CC82908C2EE85626D84CD7D3C4D13C19EEB3BEDD019119C` 的最终 live evidence。
- `GATE_F2=PASS / NO_KNOWN_P0_P1_LIVE_FAIL`
- 该结论是 Agent 的 RC 技术验收；不自动授予 `USER_ACCEPTED`、commit/push/Release 或 Studio freeze。
