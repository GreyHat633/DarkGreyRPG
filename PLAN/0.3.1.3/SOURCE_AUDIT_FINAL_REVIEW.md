# 0.3.1.3 补充源码审计 #20–#25

## Audit #20 — Inline VM 广播刷新

- Root Cause: 每个 Inspector 订阅任意 GraphChanged 并重建所有资源选项。
- Implementation: 节点级 change signature、目标节点刷新、资源版本缓存。
- Release EXE Steps: 在大图切换 Objective 类型并观察非目标节点。
- Actual: 实窗切换无明显全图重建；精确 100 节点/20 Inspector 自动 fixture 只刷新一个 Inspector。
- Evidence: `live/27-objective-collect-item-switch.png`, `live/28-objective-actor-parameter-switch.png`。
- Automated Support: `LargeGraphRefreshesOnlyInspectorForChangedNode`; WPF 380/380。
- Status: **AGENT_VERIFIED**

## Audit #21 — LostFocus 未提交风险

- Root Cause: 多个关键 TextBox 仅 LostFocus 更新源。
- Implementation: 关键绑定即时写回；Ctrl+S/切换/关闭前提交焦点编辑。
- Release EXE Steps: 焦点仍在中文字段或非法数字字段时直接 Ctrl+S。
- Actual: 中文末尾字符落盘；非法数字保持 staged error 且文件 SHA-256 不变。
- Evidence: `live/13-invalid-radius-near-field-visible.png`, `live/14-problems-detailed-stable-code.png`；有效/非法保存哈希记录于 `MANUAL_ACCEPTANCE.md`。
- Automated Support: WPF final；focused TextBox source update tests。
- Status: **AGENT_VERIFIED**

## Audit #22 — Rename round-trip 完整性

- Root Cause: rename 构造新对象并逐字段复制，未来字段易丢。
- Implementation: copy/serialize round-trip 只改 DisplayName，深比较其余字段。
- Release EXE Steps: 重命名并保存全部四类资源，再关闭重开。
- Actual: 自动化覆盖四类 deep comparison；本轮未在 Release EXE 逐一执行四类关闭重开。
- Evidence: 无完整四类实窗序列。
- Automated Support: Core/WPF final；all resource rename round-trip tests。
- Status: **BLOCKED — NEED_USER_VERIFICATION**

## Audit #23 — 普通 UI 技术术语泄漏

- Root Cause: Inspector 与 Problems 共用含 code/detail 的 formatter。
- Implementation: 分层 formatter。
- Release EXE Steps: 制造非法半径并比较两处展示。
- Actual: 近场只有可操作中文，Problems 保留稳定 code/技术详情。
- Evidence: `live/13-invalid-radius-near-field-visible.png`, `live/14-problems-detailed-stable-code.png`。
- Automated Support: WPF 380/380；Problems/ValidationIssuePresentation tests。
- Status: **AGENT_VERIFIED**

## Audit #24 — Start 默认条件名

- Root Cause: 默认值固定为“新触发”。
- Implementation: 分配第一个未占用的“启动条件 N”。
- Release EXE Steps: 连续创建四条条件。
- Actual: 实窗显示启动条件 1–4，名称互异。
- Evidence: `live/02-start-four-conditions-light.png`, `live/03-start-inspector-scroll.png`。
- Automated Support: Core/WPF final；gap-aware naming tests。
- Status: **AGENT_VERIFIED**

## Audit #25 — 普通触发节点正式 authoring 边界

- Root Cause: `interact_actor`/`enter_region` 同时出现在普通 palette 和 Start trigger；旧设计又允许 Actor 拖空白创建节点。
- Implementation: 按用户 2026-09-01 明确决策，二者只作为 Start 启动条件；角色资源只能拖入现有节点的属性参数槽，绝不创建节点。旧普通节点只读兼容并给出告警。
- Release EXE Steps: 把 actor_alpha 拖入 Objective 角色参数，比较拖放前后节点数与 JSON。
- Actual: 节点数保持 2，Objective 变为 `actor_id=actor_alpha`；没有产生角色节点。
- Evidence: `live/28-objective-actor-parameter-switch.png`, `live/29-actor-dropped-into-objective-parameter-node-count-unchanged.png`。
- Automated Support: Core/WPF final；palette exclusion and parameter-drop tests。
- Status: **AGENT_VERIFIED**

## 汇总

- AGENT_VERIFIED：5（#20 #21 #23 #24 #25）
- BLOCKED / NEED_USER_VERIFICATION：1（#22）
- USER_ACCEPTED：0

