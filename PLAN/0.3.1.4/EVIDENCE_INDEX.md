# DarkGrey_RPG 0.3.1.4 — Evidence Index

状态：`FINAL_HASH_EVIDENCE_INDEXED / AGENT_VERIFIED=YES / USER_ACCEPTED=NO`

> 自动化、真实窗口证据与用户授权分层记录。下列最终 live Gate 均绑定权威 `dist` EXE；Agent 的技术 PASS 不自动授予 `USER_ACCEPTED`，也不授权 commit、push、tag、Release、merge 或 freeze。

## Source and audit records

| Record | Purpose |
|---|---|
| `BASELINE.md` | branch、HEAD、dirty-worktree、环境与 0.3.1.3 baseline。 |
| `AUDIT_0313_INGEST.md` | 原审计初次摄取与 8 条编号。 |
| `CRASH_0313_07.md` | Objective pre-fix stack overflow 复现与 root cause。 |
| `ROOT_CAUSE_RECORD.md` | 8 条 root cause / implementation / automated / live evidence ledger。 |
| `AUDIT_0313_FINAL_REVIEW.md` | Gate F2 按原 DOCX 和图片重新打开后的 #1–#8 独立 review。 |
| `HISTORICAL_REGRESSION_BASELINE.md` | 0.3.1.0→0.3.1.3 still-valid 主合同。 |
| `HISTORICAL_REGRESSION_FINAL_REVIEW.md` | Gate F3 历史合同最终重开。 |
| `MANUAL_ACCEPTANCE.md` | S1–S20 最终权威 Release EXE 矩阵。 |
| `STAGE6_FINAL_AUDIT.md` | Gate F1–F5 与 DoD 最终技术状态。 |
| `../DarkGrey_RPG_0.3.1.4_Development_Report.md` | 0.3.1.4 开发、验证和授权边界总报告。 |

## Final build and automated evidence

| Evidence | Result |
|---|---:|
| Release solution build | `0 warnings / 0 errors` |
| `evidence/final-live-post-choice-layout/full/FinalB122-Core.trx` | Core `364/364` |
| `evidence/final-live-post-choice-layout/full/FinalB122-Wpf.trx` | WPF `407/407` |

早期 Stage TRX 与失败诊断继续保留作为施工历史；最终裁决以上述 FinalB122 full suite 为准。

## Final live gates

| Scenarios | Evidence | Result |
|---|---|---:|
| S1 / S3 / S5 | `evidence/final-live-post-choice-layout/s1-s3-s5/s1-s3-s5-final-gate.json` | `PASS` |
| S2 | `evidence/final-live-post-choice-layout/stage1-inline/stage1-inline-combobox-gate.json` | `PASS` |
| S4 / S19 | `evidence/final-live-post-choice-layout/stage2-layout-gate.json` | `PASS` |
| S6–S12 | `evidence/final-live-post-choice-layout/stage3-wire/stage3-wire-gate.json` | `PASS` |
| S13 / S16 | `evidence/final-live-post-choice-layout/s13-s16/s13-s16-inspector-theme-gate.json` | `PASS` |
| S14 / S15 | `evidence/final-live-post-choice-layout/choice-final2/choice-port-layout-gate.json` | `PASS / USER_LAYOUT_APPROVED` |
| S17 / S18 | `evidence/final-live-post-choice-layout/s17-s18-lifecycle-reorder/s17-s18-lifecycle-reorder-gate.json` | `PASS` |
| S20 | `evidence/s20-canonical-authoring-smoke/s20-canonical-authoring-smoke.json` | `PASS / 18 UI + 18 disk` |

调试、重试和旧候选目录仍保留，但不属于最终裁决路径；例如 `choice-final2` supersede 其他 Choice 调试目录。

## Pre-fix and original-audit evidence

| Evidence | Purpose |
|---|---|
| `evidence/pre-fix/0313-07-clrstack.txt` | Objective crash stack overflow 调用链。 |
| `evidence/pre-fix/dumps/DarkGreyRPGStudio.exe_260901_194227.dmp` | pre-fix WER full dump；`714.69 MB`，因 GitHub 单文件限制仅在本地保留，不纳入源码提交。 |
| `evidence/pre-fix/00-0313-launch.png` 至 `03-after-resource-doubleclick.png` | pre-fix real-window reproduction sequence。 |
| `evidence/stage6/audit-media/image1.png` 至 `image7.png` | 从 `PLAN/0.3.1.3审计.docx` 提取并逐张原尺寸检查的原始审计图片。 |

DOCX 整页 render 因当前环境缺少 `soffice` 仍记作 `BLOCKED_TOOLING`；结构化文本和全部内嵌原图已完成检查，不把未做的页面级 render 冒充为 PASS。

## Authoritative delivered artifact

- EXE：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`
- ProductVersion：`0.3.1.4-rc`
- FileVersion：`0.3.1.4`
- Size：`141430998` bytes
- SHA-256：`B122AAA2E670147C3CC82908C2EE85626D84CD7D3C4D13C19EEB3BEDD019119C`
- Package：Release / Windows x64 / self-contained / single-file。
- Runtime：S1–S20 全部由该 hash 的真实窗口与磁盘证据关闭；用户原有 Studio 进程未被验收驱动关闭。

## Decision boundary

- `AGENT_VERIFIED=YES`
- `KNOWN_P0_P1_LIVE_FAIL=NONE`
- `USER_ACCEPTED=NO`：等待用户对完整 RC 明确给出验收结论。
- `COMMIT_PUSH_RELEASE=NOT_AUTHORIZED`
