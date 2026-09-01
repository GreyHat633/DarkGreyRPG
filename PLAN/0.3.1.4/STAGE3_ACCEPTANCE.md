# DarkGrey_RPG 0.3.1.4 — Stage 3 Acceptance Record

> Work Package C：Wire Gesture
> 当前结论：IMPLEMENTED / AUTOMATED_PASS / LIVE_GATE_DEFERRED_BY_USER
> 边界：用户在 2026-09-01 明确要求暂时搁置所有实机验收，等再次授权后再进行。因此本文件不把 Stage 3 写成 AGENT_VERIFIED，也不关闭真实 Release Gate。

## 1. 实现结果

- 明确区分 NewConnection、ReconnectSingleEndpoint、AddOnMultiPort、ReconnectMultiBundle 四种手势。
- port MouseDown 先进入无视觉的 PortPressed；只有超过系统 drag threshold 才进入正式拉线。
- occupied Flow Output / Logic Input 复用原 connection 的正式 Path；实际按下的 endpoint 跟随 pointer，另一端固定。
- single reconnect 只接受同 interface、同 direction 的替换 endpoint；Escape 恢复，invalid target 不改图，blank drop disconnect。
- occupied Flow Input / Logic Output 普通拖拽继续 add one；只有 Ctrl+drag 才移动全部 incident bundle。
- hover 与 mouse-up 使用同一 FindWireTarget + PreviewWireTarget 路径；每次 move 先清全部旧 glow。
- 正式线拖动时暂时隐藏它的旧透明 hit Path，且 Line/Hit geometry 同步；取消后恢复原 geometry 与 hit visibility。

## 2. 自动化证据

| 范围 | 结果 | 证据 |
|---|---:|---|
| Core full | 363/363 PASS | evidence/stage3/full/Stage3-Core-full.trx |
| WPF full | 402/402 PASS | evidence/stage3/full/Stage3-Wpf-full.trx |
| Wire-focused | 50/50 PASS | CanonicalGraphEditorViewTests + GraphPointerStateTests |

覆盖：pending threshold、20 次 light click 无 transient/history、四手势分类、single output/input reconnect、Escape、invalid/no-op、blank disconnect、glow clearing、Flow Input ordinary/Ctrl、Logic Output ordinary/Ctrl、四项 cardinality matrix，以及 reconnect 后 canonical editor dirty/persistence snapshot。

## 3. 已采集但不足以关闭 Gate 的部分动态证据

在用户下达“暂时搁置实机验收”前，staging self-contained Release 候选已完成以下局部真实操作：

- 同一 Flow Output 真实轻点 20 次；磁盘 graph SHA-256 未变化，画面无 phantom wire。
- occupied Flow Output 的五帧 same-direction reconnect；原正式线在 MouseDown 不 collapse，移动端跟随，固定 input 不动，hover 发光，commit 后画面为替换 output → 原固定 input。
- pointer 从有效 output 移到 20 DIP 命中区外后，下一帧 glow 消失。

候选路径：.tooling/0314-stage3-publish/DarkGreyRPGStudio.exe；EXE SHA-256 B0331653A0A98905A7E341C3D64756E9D04C5ED23BACCAFA38436BE387959720；实际代码载荷 DarkGreyRPGStudio.dll SHA-256 9E414DD46C71412E94E4828CED4F73BCA4A5438AA8569503986873AFA34C809F。

局部 JSON：evidence/stage3/stage3-wire-gate.json。该文件是中途结果，不得解释为完整 Gate PASS。

## 4. 用户重新授权后必须从干净 fixture 重跑

- S6：valid glow 进入/离开，并确认 drop 与 glow 命中一致。
- S7：20 次 light click phantom absence。
- S8：Flow Output single reconnect 五帧。
- S9：Logic Input single reconnect 五帧。
- S10：已有三线的 Flow Input ordinary add one。
- S11：Flow Input Ctrl bundle move。
- S12：Logic Output ordinary/Ctrl 两套手势。
- 正式保存、关闭、重开后复核四张 graph 的 canonical connections。
- cardinality matrix 的真实 UI 操作补充；自动化已通过但不能代替动态 Gate。

## 5. Gate

GATE_STAGE3=DEFERRED_BY_USER / NEED_USER_REAUTHORIZATION

Stage 3 生产实现和自动化已闭合；真实 Gate 根据用户最新指令暂停。后续施工可以继续，但最终 RC、AGENT_VERIFIED、MANUAL_ACCEPTANCE 与权威 dist 交付不得据此关闭。
