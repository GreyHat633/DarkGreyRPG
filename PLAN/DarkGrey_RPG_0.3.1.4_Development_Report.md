# DarkGrey_RPG 0.3.1.4 Studio Regression Repair — Development Report

日期：2026-09-02
分支：`codex/0.3.1.4`
基线 HEAD：`b855abae465edb05b35655b8658c73a402ae912f`
状态：`TECHNICAL_COMPLETE / AGENT_VERIFIED=YES / USER_ACCEPTED=NO / SOURCE_COMMIT_PENDING`

## 1. Scope and authority

本轮严格按 `DarkGrey_RPG_0.3.1.4_Studio_Regression_Repair_Construction_Plan.md` 的 Stage/Gate 顺序，结合 `0.3.1.3审计.docx` 的 8 条原始问题继续施工和复验。PLAN 与审计文档是技术范围和证据来源，不被解释为 commit、push、tag、GitHub Release、merge 或 freeze 授权。

用户已授权真实电脑验收，并已人工确认 Objective 节点内下拉切换正常、批准最终 Choice 排版。完整 RC 的 `USER_ACCEPTED` 尚未由用户明确授予。

## 2. Authoritative delivered artifact

- 路径：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`
- ProductVersion：`0.3.1.4-rc`
- FileVersion：`0.3.1.4`
- Size：`141430998` bytes
- SHA-256：`B122AAA2E670147C3CC82908C2EE85626D84CD7D3C4D13C19EEB3BEDD019119C`
- 形态：Windows x64 / Release / self-contained / single-file

所有最终 live Gate 均绑定该 SHA-256；未用 `.tooling`、普通 `bin\Release` 或旧候选替代权威交付物。

## 3. Original 0.3.1.3 audit reconciliation

| # | Original regression | Root cause / implementation | Automated | Final live |
|---:|---|---|---:|---:|
| 1 | 类型下拉无效、逐字卡顿、不能临时清空 | canonical mutation 与 UI draft/commit 混合；拆分 draft/commit，选择事务化并定向刷新 | `PASS` | `PASS`，S1/S2 |
| 2 | 删除节点重复确认 | 删除入口依赖阻塞确认且缺少统一 Undo transaction；改为即时删除、一次 Undo 恢复、fixed fail-closed | `PASS` | `PASS`，S5 |
| 3 | valid target glow 离开后滞留 | hover/drop/clear 使用不同状态；统一命中解析并在每次 move 清旧目标 | `PASS` | `PASS`，S6 |
| 4 | 轻点 phantom、单容量 reconnect 抓错端 | pending/add/reconnect/bundle 未分流；引入明确 gesture kind 与阈值，复用正式 Path | `PASS` | `PASS`，S7–S12 |
| 5 | Actor/Item Inspector 层级不清 | 通用 Inspector 缺少主次层级与资源术语；按四类资源重排主值、ID、tags、滚动 | `PASS` | `PASS`，S13 |
| 6 | 节点排版关闭重开丢失 | 坐标仅在进程内；新增独立 canonical JSON 的 graph layout sidecar | `PASS` | `PASS`，S4/S19 |
| 7 | Objective target 切换闪退 | mutation、options rebuild 与 TwoWay 回写无限重入导致 stack overflow；改为原子事务、projection guard、stable ID | `PASS` | `PASS`，S3 100 次、0 crash |
| 8 | Choice option/output 错位 | option 与端口由不同投影/布局链生成；改为 stable-ID row projection 与固定端口几何 | `PASS` | `PASS`，S14/S15；用户批准排版 |

状态语义：#1–#8 均为 `ROOT_CAUSE_CONFIRMED / IMPLEMENTED / AUTOMATED_PASS / FINAL_HASH_LIVE_PASS`。详细逐条重开记录见 `0.3.1.4/AUDIT_0313_FINAL_REVIEW.md`。

## 4. PLAN acceptance results

- S1–S20：`20/20 AGENT_VERIFIED_PASS`。
- S20 canonical authoring smoke：`18/18 UI steps + 18/18 disk assertions PASS`。
- Release solution build：`0 warnings / 0 errors`。
- Core：`364/364 PASS`。
- WPF：`407/407 PASS`。
- Known P0/P1 live failure：`NONE`。

覆盖范围包括：文本 draft/commit、节点内 ComboBox、Objective target 100 次、五图 layout、删除/Undo/fixed protection、Flow/Logic 全手势、Inspector 四类资源、Choice 1/2/5/10 与持久化、Dark/Light、资源 lifecycle/reorder、viewport 以及完整 authoring smoke。

## 5. Evidence map

- 总矩阵：`0.3.1.4/MANUAL_ACCEPTANCE.md`
- Stage 6 总审计：`0.3.1.4/STAGE6_FINAL_AUDIT.md`
- 证据索引：`0.3.1.4/EVIDENCE_INDEX.md`
- 审计 #1–#8：`0.3.1.4/AUDIT_0313_FINAL_REVIEW.md`
- 历史回归：`0.3.1.4/HISTORICAL_REGRESSION_FINAL_REVIEW.md`
- S1/S3/S5：`0.3.1.4/evidence/final-live-post-choice-layout/s1-s3-s5/s1-s3-s5-final-gate.json`
- S2：`0.3.1.4/evidence/final-live-post-choice-layout/stage1-inline/stage1-inline-combobox-gate.json`
- S4/S19：`0.3.1.4/evidence/final-live-post-choice-layout/stage2-layout-gate.json`
- S6–S12：`0.3.1.4/evidence/final-live-post-choice-layout/stage3-wire/stage3-wire-gate.json`
- S13/S16：`0.3.1.4/evidence/final-live-post-choice-layout/s13-s16/s13-s16-inspector-theme-gate.json`
- S14/S15：`0.3.1.4/evidence/final-live-post-choice-layout/choice-final2/choice-port-layout-gate.json`
- S17/S18：`0.3.1.4/evidence/final-live-post-choice-layout/s17-s18-lifecycle-reorder/s17-s18-lifecycle-reorder-gate.json`
- S20：`0.3.1.4/evidence/s20-canonical-authoring-smoke/s20-canonical-authoring-smoke.json`

## 6. Residual boundary and next decision

- 技术施工与 Agent 实机 Gate 已完成；当前没有已知 P0/P1 live fail。
- `USER_ACCEPTED=NO`：需用户明确决定是否接受完整 RC。
- 0.3.1.4 仍是未提交 working tree；现有 dirty work 已保留。
- `COMMIT_PUSH_TAG_RELEASE_MERGE_FREEZE=NOT_AUTHORIZED`。
- 原审计 DOCX 的结构化文本和 7 张内嵌原图已检查；由于环境缺少 `soffice`，整页 DOCX render 仍为 `BLOCKED_TOOLING`，不影响已完成的原文/原图审计，但不将其冒充为页面级 render PASS。

最终裁决：`RC_TECHNICALLY_READY_FOR_USER_ACCEPTANCE`。
