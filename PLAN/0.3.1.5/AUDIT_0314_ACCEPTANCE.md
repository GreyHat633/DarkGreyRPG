# 0.3.1.4 人工审计逐条验收记录（0.3.1.5）

验收日期：2026-09-02（Asia/Shanghai）  
审计来源：`PLAN/0.3.1.4审计.docx`  
验收对象：`dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`  
ProductVersion：`0.3.1.5-rc`  
FileVersion：`0.3.1.5`  
文件大小：`141441750` bytes  
SHA-256：`9EC1466A2D35AD8428DA43C98C3CC2BDDA3D422649A180712A1F95554641967C`

## 1. 结论与状态边界

原人工审计中的四条问题已逐条对照最终哈希的权威可运行 EXE 复验：

| 审计项 | 结论 | 验收状态 |
|---|---|---|
| 1. 未保存关闭提示 | 精确出现“保存 / 不保存 / 取消”；取消保留进程与内存修改，不保存退出且磁盘保持旧值 | `AGENT_VERIFIED` |
| 2. Actor / Item Inspector 排版 | 四类资源仅显示对应 ID；无“资源属性”和冗余“显示名称”；长标签在值列悬挂缩进换行 | `AGENT_VERIFIED` |
| 3. 连线起点位于端口中心 | Flow 与 Logic 的新鲜真实窗口拖线帧均从可见端口中心出发，不从文字中部出发 | `AGENT_VERIFIED` |
| 4. Action Inspector 字段与术语 | 三类 Action 的节点标题、Inspector 类型和字段标签均正确；旧术语不再出现 | `AGENT_VERIFIED` |

汇总：`AUDIT_0314 = 4/4 AGENT_VERIFIED`。  
这不是 `USER_ACCEPTED`，也不表示 0.3.1.5 PLAN 中与本次四条人工审计无关的门禁已经全部完成。

## 2. 逐条证据

### 2.1 未保存关闭提示

在真实窗口中把“物品给予”的数量从 `10` 改为 `11` 后关闭 Studio：

- 对话框按钮精确为 `保存`、`不保存`、`取消`；
- 点击 `取消` 后进程仍存活，Inspector 中未提交到磁盘的值仍为 `11`；
- 再次关闭并点击 `不保存` 后进程退出；
- 重新读取项目文件，持久化值仍为 `10`。

机器证据：`.tooling/0.3.1.5/audit-0314/live-four-items-r9/audit0314-four-item-gate.json`  
窗口证据：`audit-1-unsaved-prompt.png`、`audit-1-unsaved-prompt-discard.png`

### 2.2 Actor / Item Inspector 排版

真实窗口逐一选中四类资源：

- Actor “酒馆老板”：仅显示 `NPC_ID：tavern_boss`；
- Actor Group “史莱姆”：仅显示 `Group_ID：slimes`；
- Item “铜币”：仅显示 `Item_ID：copper_coin`；
- Item Group “货币组”：仅显示 `Group_ID：currencies`；
- 所有视图均不存在“资源属性”和冗余“显示名称”；
- 超长标签值从值列 `x=1239` 开始，标签名列在 `x=1187`；值区域高度由普通行的 `22` 增至 `111`，视觉复核确认后续行保持在值列下方，不退回“标签：”下方。

机器证据：`.tooling/0.3.1.5/audit-0314/live-four-items-r9/audit0314-four-item-gate.json`  
窗口证据：`audit-2-actor-long-tags.png`、`audit-2-actor-group.png`、`audit-2-item.png`、`audit-2-item-group.png`

### 2.3 连线从可见端口中心出发

用最终哈希 EXE 在全新隔离 fixture 上执行 Flow 与 Logic 单线重连：

- Flow 输出端口命中点为 `(602, 386)`，UIA 命中对象是 `CanonicalGraphPort_flow_source_flow_out`；
- Logic 输入端口命中点为 `(740, 440)`，UIA 命中对象是 `CanonicalGraphPort_logic_fixed_logic_in`；
- before / mouse-down / moving / hover / commit 连续帧经视觉复核，线段均锚定在可见圆形或菱形端口中心，未从标签文字中部出发；
- 新鲜运行的 `flow_single`、`logic_single`、`glow_leave` 均为 `PASS`。

新鲜审计相关证据：`.tooling/0.3.1.5/audit-0314/wire-live-r1/stage3-wire-gate.json`  
窗口证据目录：`.tooling/0.3.1.5/audit-0314/wire-live-r1/frames/`

边界说明：这次新鲜运行在上述审计相关步骤通过后，旧的综合 Stage 3 驱动继续执行到审计范围外的 Ctrl 多选阶段时，因 `Save All` 菜单状态断言停止，因此不能把这次运行表述为“整个 Stage 3 重新通过”。同一最终 EXE 哈希此前已有完整 27 帧 Stage 3 `PASS`：`.tooling/0.3.1.5/live/stage3-wire-gate.json`。本条审计结论只依赖已完成的新鲜端口命中与连续帧证据，并由同哈希完整运行交叉支持。

### 2.4 Action Inspector 字段与术语

真实窗口逐一点击三个 Action 节点：

- `give_item`：节点标题与 Inspector 类型均为 `物品给予`，字段标签为 `物品`、`数量`；
- `give_xp`：节点标题与 Inspector 类型均为 `经验给予`，字段标签为 `经验值`；
- `send_message`：节点标题与 Inspector 类型均为 `消息发送`，字段标签为 `消息`；
- 旧术语 `给予物品`、`给予经验`、`发送消息` 未出现。

机器证据：`.tooling/0.3.1.5/audit-0314/live-four-items-r9/audit0314-four-item-gate.json`  
窗口证据：`audit-4-item-give.png`、`audit-4-xp-give.png`、`audit-4-message-send.png`

## 3. 交付与授权边界

- 权威可运行文件位于 `E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`；
- 本次完成审计逐条验收与证据落档；
- 源码、测试、当前 PLAN 与审计原件已以 `eb163ec907da7465b0055ac83a571a7281c3208c` 推送到 `origin/codex/0.3.1.5`，本验收记录与施工报告随同该分支发布；
- 未执行 merge、tag、GitHub Release 或二进制附件上传；
- 工作树中的用户既有修改与其他未提交施工内容均保留。
