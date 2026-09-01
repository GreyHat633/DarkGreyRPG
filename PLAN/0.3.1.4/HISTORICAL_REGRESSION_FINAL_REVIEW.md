# DarkGrey_RPG 0.3.1.4 — Historical Regression Final Re-open

状态：`BASELINE_REOPEN_COMPLETE / AUTOMATED_PASS / FINAL_HASH_LIVE_PASS`

## Re-open boundary

- 已重新打开 `HISTORICAL_REGRESSION_BASELINE.md`，不是仅从 0.3.1.4 Work Package 反推。
- minimum master contracts 与 broader baseline 均按 G1–G14 重新对照。
- 详细 live 对照表见 `STAGE5_HISTORICAL_REGRESSION.md`；S1–S20 逐项见 `MANUAL_ACCEPTANCE.md`。

## Final evidence

| Evidence class | Result |
|---|---|
| Fresh Release build | `PASS / 0 warnings / 0 errors` |
| Fresh Core/WPF | `364/364 + 407/407 PASS` |
| Final authoritative EXE | `B122AAA2E670147C3CC82908C2EE85626D84CD7D3C4D13C19EEB3BEDD019119C` |
| Live acceptance | `S1–S20 = 20/20 PASS` |
| Full authoring smoke | `18/18 UI + 18/18 disk PASS` |
| Superseded behavior restored | `NONE` |

## Broader baseline reconciliation

- Story/Session/Task resource ownership、stable identity、aggregate sync、save/restart：PASS。
- Actor/Actor Group/Item/Item Group terminology、selector 与 Inspector hierarchy：PASS。
- Flow/Logic cardinality、single/multi reconnect、ordinary/Ctrl、glow/drop、phantom absence：PASS。
- Choice stable option/flow port mapping、rename/reorder/reconnect、Dark/Light/restart：PASS。
- Story Start draft/commit、Objective type/target/quantity、Settlement fixed lifecycle：PASS。
- layout/viewport per-graph isolation 与 cross-process restart：PASS。
- Problems 普通中文提示与技术详情分层：真实面板可读；automated validation routing 全绿。
- canonical package 仍排除 editor layout sidecar；Java Runtime 未被本轮 Studio-only layout 改动污染。

## Gate F3 conclusion

- 当前没有发现 still-valid 历史合同的自动化或真实窗口 FAIL。
- 任何未来针对同一权威 hash 的反例仍应立即转为 `0314 NO-GO`，不能用本报告覆盖新事实。
- `GATE_F3=PASS`
- 该结论不自动授予 `USER_ACCEPTED` 或 Git publication authority。
