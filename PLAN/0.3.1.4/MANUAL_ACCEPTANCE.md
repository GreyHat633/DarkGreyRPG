# DarkGrey_RPG 0.3.1.4 — Manual Acceptance Matrix

状态：`AGENT_VERIFIED_LIVE_MATRIX_PASS / USER_ACCEPTED=NO / SOURCE_COMMIT_PENDING`

> 用户已重新授权实机验证。本矩阵记录 2026-09-02 对最终权威 EXE 的真实窗口、鼠标、键盘、关闭重启与磁盘结果；此前 staging/deferred 记录保留在各 Stage 报告中，只作为施工历史。本矩阵的 PASS 不自动等同于用户授予 `USER_ACCEPTED`，也不授权 commit、push、tag、Release 或 merge。

## Authoritative candidate

- EXE：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`
- ProductVersion：`0.3.1.4-rc`
- FileVersion：`0.3.1.4`
- Size：`141430998` bytes
- SHA-256：`B122AAA2E670147C3CC82908C2EE85626D84CD7D3C4D13C19EEB3BEDD019119C`
- Package：Windows x64 / Release / self-contained / single-file
- Git：branch `codex/0.3.1.4`；baseline HEAD `b855abae465edb05b35655b8658c73a402ae912f`；0.3.1.4 仍为未提交 working tree。

## S1–S20

| Scenario | Result | Actual live result | Primary evidence |
|---|---|---|---|
| S1 Text Draft | `PASS` | Start 名称经物理 `Ctrl+A`/Delete 清空，保持 2100 ms 仍为空；物理键盘输入 `s1finaldraft`，Enter 后 UI 与磁盘一致。 | `evidence/final-live-post-choice-layout/s1-s3-s5/s1-s3-s5-final-gate.json` |
| S2 ComboBox | `PASS` | Start、Action、Objective 节点内 ComboBox 均以真实弹项点击逐项切换；保存、清洁关闭、进程重启后保持。用户另已人工确认 Objective 节点内切换正常。 | `evidence/final-live-post-choice-layout/stage1-inline/stage1-inline-combobox-gate.json` |
| S3 Objective target 100x | `PASS` | 角色“酒馆老板”与角色组“史莱姆”交替 100 次，0 crash，进程始终 Responding；最终 inline/right 与磁盘 `slimes` 一致。 | `evidence/final-live-post-choice-layout/s1-s3-s5/s1-s3-s5-final-gate.json` |
| S4 Node layout restart | `PASS` | Story、2 Session、2 Task 共五图节点坐标保存后在 Dark 重启及 Light 重启保持；layout sidecar 与 graph key 隔离有效。 | `evidence/final-live-post-choice-layout/stage2-layout-gate.json` |
| S5 Node Delete | `PASS` | 普通 Objective Delete 无确认且立即消失；Ctrl+Z 恢复节点、端口与连接；固定 Settlement 拒绝删除；保存后磁盘语义恢复。 | `evidence/final-live-post-choice-layout/s1-s3-s5/s1-s3-s5-final-gate.json` |
| S6 Glow | `PASS` | valid hitbox 进入后 glow，移动到明确外部后同次移动清除；连续帧已留证。 | `evidence/final-live-post-choice-layout/stage3-wire/stage3-wire-gate.json` |
| S7 Light-click phantom | `PASS` | Flow/Logic 输出端口轻点 20 次不生成 phantom segment，资源 hash 不变。 | 同上 |
| S8 Single Flow reconnect | `PASS` | occupied Flow Output 抓取正式 endpoint、拖动、hover、commit 全序列通过，连接落到替代目标。 | 同上 |
| S9 Single Logic reconnect | `PASS` | occupied Logic Output 正式 endpoint reconnect 全序列通过。 | 同上 |
| S10 Multi Flow ordinary | `PASS` | ordinary drag 只增加/移动一条，最终连接数与目标正确。 | 同上 |
| S11 Multi Flow Ctrl | `PASS` | Ctrl+drag bundle 以连续移动/hover/commit 帧完成，4 条连接保持。 | 同上 |
| S12 Multi Logic ordinary/Ctrl | `PASS` | Logic ordinary 与 Ctrl bundle 两种手势均通过，4 条连接保持。 | 同上 |
| S13 Inspector | `PASS` | Actor、Actor Group、Item、Item Group 四类真实截图齐全；显示名称、NPC_ID/Item_ID/Group_ID、tags、ScrollPattern 正确；无“拥有/引用状态”主字段。 | `evidence/final-live-post-choice-layout/s13-s16/s13-s16-inspector-theme-gate.json` |
| S14 Choice 1/2/5/10 | `PASS` | 最终 10-option 几何逐行核验；每组 Flow/Logic 输出靠近、组间距明确、输入左对齐、输出右对齐；Dark/Light 均通过。用户已人工批准最终排版。 | `evidence/final-live-post-choice-layout/choice-final2/choice-port-layout-gate.json` |
| S15 Choice persistence | `PASS` | 5 条独立连接，reconnect 前进与还原均通过；保存、进程重启后 option/flow-port mapping、连接数和几何保持。 | 同上 |
| S16 Light/Dark | `PASS` | Dark/Light 均真实启动；Graph、Choice、Inspector、展开的 Problems 表头与代表性问题可读；画面平均亮度 59.1/246.6。 | `evidence/final-live-post-choice-layout/s13-s16/s13-s16-inspector-theme-gate.json` |
| S17 Resource lifecycle | `PASS` | 删除 placement 保留资源；删除 resource 清理所有 placement/connection；保存后磁盘断言通过。 | `evidence/final-live-post-choice-layout/s17-s18-lifecycle-reorder/s17-s18-lifecycle-reorder-gate.json` |
| S18 Resource reorder | `PASS` | 插入预览、`session_c` 移到 `session_a` 前、保存与重启后的可见顺序均为 C→A。 | 同上 |
| S19 Viewport | `PASS` | Story 与 Session 视口隔离，平移/缩放状态保存并在重启后恢复。 | `evidence/final-live-post-choice-layout/stage2-layout-gate.json` |
| S20 Canonical authoring smoke | `PASS` | 18/18 UI 步骤、18/18 磁盘断言通过：故事、角色组、角色、物品、Session、三选项、Task、击杀 10 个史莱姆 Objective、Settlement、资源拖入、连线、移动、保存、关闭重启与布局恢复。 | `evidence/s20-canonical-authoring-smoke/s20-canonical-authoring-smoke.json` |

## Final automated companion evidence

- Release build：`0 warnings / 0 errors`。
- Core：`364/364`，`evidence/final-live-post-choice-layout/full/FinalB122-Core.trx`。
- WPF：`407/407`，`evidence/final-live-post-choice-layout/full/FinalB122-Wpf.trx`。
- 所有最终 live JSON 都绑定同一权威 SHA-256 `B122AAA2...119C`。
- 验收专用进程均已关闭；用户原有 PID `13092` 未被关闭或接管。

## Decision boundary

- `AGENT_VERIFIED=YES`
- `KNOWN_P0_P1_LIVE_FAIL=NONE`
- `USER_ACCEPTED=NO`：等待用户对完整 RC 作明确验收结论。
- `COMMIT_PUSH_RELEASE=NOT_AUTHORIZED`
