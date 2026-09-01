# DarkGrey_RPG 0.3.1.4 — Stage 1 Acceptance

> Work Package A：Editor Draft / Commit 与 Objective 稳定性
> 结论：`GATE_STAGE1=PASS` / `AGENT_VERIFIED_STAGE1`
> 边界：本 Gate 使用 staging Release 候选，不是最终 `dist` 权威交付物，不代表 `USER_ACCEPTED`。

## 1. 候选包

- 路径：`.tooling/0314-stage1-final-publish/DarkGreyRPGStudio.exe`
- ProductVersion：`0.3.1.3-rc`
- FileVersion：`0.3.1.3`
- EXE 大小：`204288` bytes
- EXE SHA-256：`B0331653A0A98905A7E341C3D64756E9D04C5ED23BACCAFA38436BE387959720`
- 说明：版本号与最终单文件发布将在 Stage 6 统一完成；本候选仅用于 Stage 1 的当前源码真实窗口验证。

## 2. 实现结果

- 文本字段从逐字符 canonical mutation 改为 draft，在 Enter、LostFocus 或 Ctrl+S flush 时一次提交。
- invalid draft 保留在编辑器内并显示字段错误，不覆盖最近合法 canonical 值。
- Objective type 与 target 使用单一 Core/Host 事务；target 修复允许旧数据保留无关 validation issue。
- ComboBox 使用稳定 ID 投影；projection guard 阻止 canonical 回投时再次进入 setter。
- Graph 仅对 changed node、当前 Inspector、inline editor 和必要 port projection 做 targeted refresh。
- Ctrl+S 在保存前显式 flush 当前 focus 内的 draft。
- inline/right 双 Inspector 的删除确认与刷新均落到实际 node visual 实例。

## 3. 自动化证据

| 范围 | 结果 | 证据 |
|---|---:|---|
| Stage 1 targeted | `11/11` PASS | `evidence/stage1/targeted/Stage1-A9-targeted.trx` |
| Core full | `355/355` PASS | `evidence/stage1/full/Stage1-Core-full.trx` |
| WPF full（最终 clean 重跑） | `390/390` PASS | `evidence/stage1/full/Stage1-Wpf-full-final-clean.trx` |

最低要求覆盖：20 次文本 draft 不逐字增加 GraphRevision、一次 commit/Undo、临时空值、invalid draft、Ctrl+S active draft flush、Objective type/target 单事务、projection 不重入、100 次切换、unrelated node VM identity 稳定，以及实际 node visual 的 inline/right 同步。

## 4. 最终候选真实窗口验收

测试项目：`E:\Java\MinecraftMod\RPGProject\project_test`

### Objective

| 操作 | 次数 | median | p95 | max | 结果 |
|---|---:|---:|---:|---:|---|
| target 切换 | 100 | `67.97 ms` | `91.33 ms` | `108.91 ms` | 0 crash；最终 `test_npc`；inline/right 一致 |
| type 切换 | 100 | `67.86 ms` | `88.78 ms` | `103.11 ms` | 0 crash；最终 `interact_actor`；inline/right 一致 |

资源选择补充：实际选择 `collect_item + test_item_group`，再切回 `interact_actor + test_group_npc`，进程持续响应。

### Story Start

- 实际连续切换 `50` 次；最终 inline/right 均为 `interact_actor`。
- median `516.809 ms`，p95 `576.286 ms`，max `604.485 ms`，`>1 s` 次数为 `0`。
- p95 略高于 PLAN 的“约 500 ms”建议值；没有多秒冻结，硬要求 `0 crash / 0 unhandled exception / 0 多秒冻结 / 最终选择正确` 全部满足，原始观测不做修饰。
- A7 参数/端口：`enter_region` 显示维度/XYZ/半径且输入端口 `0`；`logic` 隐藏角色/区域参数且逻辑输入端口 `1`（总端口 `5`）；`interact_actor` 显示 actor selector 且输入端口 `0`（总端口 `4`）。
- 实际 Ctrl+Z 恢复 `logic + 1` 个输入端口，Ctrl+Y 恢复 `interact_actor + 0` 个输入端口。
- 实际选择 `test_group_npc` 后 inline/right 一致；保存、关闭、重启后仍为角色交互、角色组、0 个逻辑输入端口。

### Action 与文本 draft

- 实际逐项切换 `give_item`、`give_xp`、`send_message`，每项 inline/right 一致。
- 通过 Win32 Unicode keyboard input 连续输入 `41` 个中文字符，发送耗时 `12.96 ms`；输入后焦点仍在 `StoryActionMessage`。
- 焦点内实际 Ctrl+S 后，磁盘精确包含最后一个字。
- 一次实际 Ctrl+Z 整段恢复为 `任务完成`，一次 Ctrl+Y 整段恢复完整 41 字符文本。
- 再次保存、关闭并重启候选 EXE 后，`send_message` 与完整文本均从磁盘恢复。

## 5. 视觉证据

| 文件 | SHA-256 | 用途 |
|---|---|---|
| `evidence/stage1/ui/final-candidate-action-chinese.png` | `1FFD364C49E7794A01A0200808CAE60D60B3D64429297773B2089C8DA23E3FE8` | 当前候选的 Action 类型、完整中文消息与已保存状态 |
| `evidence/stage1/ui/final-candidate-reopen-action.png` | `F819E099F2EEA062EBF41147765EC95FBC45A3F1453A9A71CFF97B89D549D7FA` | 重启后实际 Story/Action 选中状态；精确值由同轮 UI Automation 读取 |
| `evidence/stage1/ui/final-candidate-reopen-start.png` | `D60CBA29DE09607116A20F09F7DED002518461316F9409E5545FA1ECB952792B` | 重启后实际 Story/Start 选中状态；精确值由同轮 UI Automation 读取 |

## 6. 证据边界与异常记录

- 0.3.1.3 pre-fix 的 `0313-07` 已有 WER full dump 与 `clrstack`，确认 UI thread `STACK_OVERFLOW`。
- 最终 Stage 1 候选的上述压力与交互路径未产生新 crash dump。
- 一次早期 50 轮驱动因缓存了已被 WPF 重建的 AutomationElement 而失败；改为每轮重新定位后通过。另一次首版 Unicode `SendInput` 驱动因 x64 `INPUT` 结构尺寸错误返回失败；修正为 40-byte 结构后通过。两者均是验收驱动器失败，不计作产品 PASS，也未隐藏。
- Stage 1 不触碰节点布局持久化、graph gestures、删除 Undo、视觉层级或 Choice 端口对齐；它们按 PLAN 留给 Stage 2–4。

## 7. Gate

`GATE_STAGE1=PASS`

理由：Work Package A 的实现、最低自动化、真实 Release 候选压力、真实 ComboBox 参数/端口联动、Ctrl+S、Undo/Redo 与 save/close/reopen 均已形成可复核证据；最终权威 `dist` 交付与用户验收仍明确延后到 Stage 6。
