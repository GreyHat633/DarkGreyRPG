# DarkGrey_RPG 0.3.1.4 — Stage 5 Historical Regression Pass

状态：`AUTOMATED_PASS / FINAL_HASH_LIVE_PASS`

> 用户重新授权后，Stage 5 不再停留于 static/deferred。下表把 `HISTORICAL_REGRESSION_BASELINE.md` 的 G1–G14 与最终权威 EXE、S20 端到端 smoke 和 2026-09-02 fresh full suites 对齐。

## Fresh evidence

- 权威 EXE SHA-256：`B122AAA2E670147C3CC82908C2EE85626D84CD7D3C4D13C19EEB3BEDD019119C`。
- Release build：`0 warnings / 0 errors`。
- Core：`364/364`；WPF：`407/407`。
- S1–S20：`20/20 AGENT_VERIFIED_PASS`。
- S20：`18/18 UI + 18/18 disk PASS`。

## G1–G14 reconciliation

| Gate | Final result | Evidence |
|---|---|---|
| G1 Workspace / Navigation | `PASS` | S20 从 Project Home 进入故事，逐层打开 Story/Session/Task；资源树、breadcrumb 与三栏工作区真实运行。 |
| G2 Resource Library | `PASS` | S13 四类资源选择；S18 插入预览、排序、保存与重启；S20 创建并加载真实资源。 |
| G3 Lifecycle / Identity | `PASS` | S17 placement/resource 不对称删除；Actor/Item/Session/Task stable ID 与 membership 磁盘断言。 |
| G4 Palette | `PASS` | S20 真实空白画布菜单添加 Line、Choice、End、Objective；aggregate 由资源拖入 Story Flow。 |
| G5 Story Start | `PASS` | S1 draft/Enter；S2 Start type；S20 与 S4 保存重启。 |
| G6 Session | `PASS` | S20 Line/Choice/3 options/End 真实创作、连线与重启；S14/S15 Choice 专项。 |
| G7 Task | `PASS` | S20 Objective→Settlement；S5 固定 Settlement 保护、Delete/Undo；S3 target 100x。 |
| G8 Objective | `PASS` | S2 三正式类型弹项；S3 actor/group 100x；S20 kill_entity/slimes/10 持久化。 |
| G9 Actor / Item identity | `PASS` | S13 Actor/Actor Group/Item/Item Group 的作者术语、ID、tags；S20 真实创建与 selector。 |
| G10 Graph UX | `PASS` | S6–S12 pointer/hitbox/glow/phantom/single/multi/ordinary/Ctrl；S4/S19 layout/viewport；Choice 端口专项。 |
| G11 Theme | `PASS` | S16 Dark/Light 下 Graph、Choice、Inspector、Problems；Choice 与 layout 另有双主题重启截图。 |
| G12 Errors | `PASS` | S16 Problems 展开后显示严重性/资源/问题/字段与代表性技术详情；full tests 覆盖 validation routing。 |
| G13 Save / Restart | `PASS` | S2、S4、S15、S17/S18、S19、S20 均含 clean close/restart 与磁盘断言。 |
| G14 Canonical Authoring Smoke | `PASS` | `evidence/s20-canonical-authoring-smoke/s20-canonical-authoring-smoke.json`。 |

## Superseded-contract audit

- “每次删除节点都确认”保持 superseded；普通节点立即删除 + Undo，resource delete 仍可确认。
- “9px 即完整 hit target”保持 superseded；视觉 anchor 与交互 hitbox 分离。
- `【条件】`作者文案不恢复；正式 UI 保持`【条件判断】`。
- Session Start legacy Logic Output 不恢复；新 authoring 为 Flow-only，旧数据仅迁移兼容。
- “局部图”不恢复为正式术语；保持`图谱 → 流程图 → 会话图/任务图`。

## Anti-overarchitecture

没有新增 Graph Editor v2、Generic Inspector/Form DSL、AutoSave framework、数据库、通用 Event Bus、Plugin System、scenario platform、新 Objective/Action 类型或 Runtime 大重构。新增结构限于 PLAN 允许的 draft boundary、四手势状态、layout sidecar、Choice 专用 row projection 与局部 guard/refresh。

`GATE_STAGE5=PASS`
