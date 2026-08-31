# DRG Studio 0.3.1.2A 第二轮逐项验收

日期：2026-08-31  
结论：**NO-GO**  
验收对象：`dist/DarkGreyRPGStudio-0.3.1.2A/DarkGreyRPGStudio.exe`  
SHA-256：`A460437194662A3E0F4E624B0F5C541EB4A0312352506CDEA05DD64D109D2971`  
分支 / HEAD：`codex/0.3.1.2A` / `8d5259f0a64119e3d3813f2c90bea1615462eaf7`

本轮直接操作上述 Release EXE 和隔离验收项目 `.tooling/0312a-reacceptance/project`。没有修改 Studio 源码、没有重打 RC、没有提交、推送或发布。证据位于 `evidence/second-pass-2026-08-31/`。

## 阻断摘要

1. **A9 FAIL**：真实 Flow 单线重接时，原正式线仍连接原目标，同时出现第二条拖动线；释放后右侧报告 `graph.connection.flow.output.multiple_targets`，原 endpoint 未提交到新目标。这与 PLAN 要求的“直接移动当前 connection visual，不叠加第二条 draft line”正面冲突。
2. **A1 BLOCKED**：125% DPI 两种尺寸已验，当前桌面未安全切到 100% DPI，不能补签完整 Gate。
3. **A2 BLOCKED**：10 次 Left Alt 循环未复现白色矩形；PLAN 明确规定“原问题无法复现”不得判 PASS。
4. **A12 BLOCKED**：多次真实资源拖放自动控制未能形成可信 ghost，不能用静态或代码证据替代 50% / 100% / 150% 与 pan/no-pan 动态 Gate。
5. **A13 BLOCKED**：DGR 资源和三个 Objective 的语义复验通过，但不是严格从空项目完成整条七步链。
6. **A15 BLOCKED**：大部分子项已重新验收；port anchor 文字长度漂移和全部基数方向组合没有形成完整独立动态证据，按“任何一个失败/缺证即 NO-GO”不补签。

## A1-A15 逐项结果

| Gate | 结论 | 第二轮证据与说明 |
|---|---|---|
| A1 | BLOCKED | 真实 RC 在 `GetDpiForWindow=119`（约 125%）下完成逻辑 1100×700、1700×980 两种窗口；标题、说明、按钮保持居中。证据：`A1-125dpi-1100x700.png`、`A1-125dpi-1700x980.png`。100% DPI 未验。 |
| A2 | BLOCKED | 空白、节点附近、连线附近轮换做 10 次 Left Alt down/up；焦点始终为主窗口，`GUI flags=0`、`hwndCapture=0`、`hwndMenuOwner=0`，未见白矩形。证据：`A2-cycle-01.png` 至 `A2-cycle-10.png`、`A2-contact-sheet.png`。依 PLAN 不能因未复现而判 PASS。 |
| A3 | AGENT_VERIFIED | Dark/Light 下资源 normal、selected、hover 均可读，主题切换后无浅色文字丢失。证据：`A3-*`。 |
| A4 | AGENT_VERIFIED | Actor / Item / Session / Task 四类真实右键菜单分别显示对应的编辑、新建、引用和删除项；Item 未 fallback 成角色。证据：`A4-*-context-menu.png`。 |
| A5 | AGENT_VERIFIED | Story Start、Action、Task Objective、Session Line 均在节点内直接编辑；Objective 折叠/展开、header 拖动不误触控件拖动；保存 JSON 后 `reaccept_item`、数量 9、`reaccept_actor` 和新台词均持久化；重启后生成节点 stable ID 仍存在。证据：`A5-*`。 |
| A6 | AGENT_VERIFIED | Start、Action、Line、Choice、Objective、Settlement 的 Inspector 在真实 Dark UI 中逐个选择；Objective 下拉在 Dark/Light 均可读。证据：`A6-*`。 |
| A7 | AGENT_VERIFIED | Story 菜单为流程/聚合/逻辑/动作/触发且 Flow 仅可新建终止；Session 为会话/逻辑/结束且可创建 Choice、无 Start；Task 只提供目标与逻辑，无固定 Settlement/Activation。证据：`A7-*`。 |
| A8 | AGENT_VERIFIED | Graph 默认 cursor handle 为 `0x10003`；剪刀按钮与 Left Alt 均切换为同一自定义 handle `0xFFFFFFFFB7131483`；关闭模式、Alt 松开和移出 Graph 均恢复默认。截图使用当前实际 cursor handle 绘入窗口捕获：`A8-scissors-cursor.png`、`A8-alt-cursor.png`。 |
| A9 | **FAIL** | 新建 Flow 线使用正式蓝色实线，cancel 后 JSON hash 不变；Logic 多 incident 拖动时两条黄色线同步移动，cancel 后 hash 不变。但 Flow 单输出已有连接的重接中，原线仍保留且另画第二条蓝线，释放后报 `graph.connection.flow.output.multiple_targets`。决定性证据：`A9-flow-single-reconnect-drag-before-commit.png`、`A9-flow-single-reconnect-after-commit.png`；对照证据：`A9-new-wire-*`、`A9-logic-multi-existing-*`。 |
| A10 | AGENT_VERIFIED | 严格执行十步：Story 脏编辑 → 创建 Actor/Item/Session/Task → 切 Session → 切 Task → 返回 Story；原未保存 Action 仍在，期间无 save-first 阻断。证据：`A10-dirty-sequence-return-story.png`。 |
| A11 | AGENT_VERIFIED | 在真实窗口连续展开/收起 Item folder 10 次；下游 Session Y 在 343/476 间随状态更新，20 次转换最大 204.2 ms，无超时。动态操作日志由本轮探针直接采集。 |
| A12 | BLOCKED | 在真实窗口尝试 Session、Actor/Item Group 等资源拖放并捕获 ghost/drop，但本轮自动输入没有可靠触发拖放 ghost；现有 `A12-*` 只能证明尝试，不能证明 anchor 一致，故不补签。 |
| A13 | BLOCKED | 真实 UI 创建 Actor group `slimes`、Item group `reaccept_group`；同一 Task 保存 kill/collect/interact 三个 Objective，目标分别使用 `reaccept_actor`、`reaccept_item`、`reaccept_actor`，JSON 无 `minecraft:slime/stone`。但隔离项目不是严格空项目起步，未满足 Gate 前提。证据：`A13-*`。 |
| A14 | AGENT_VERIFIED | 初始 EnterRegion 后新增 InteractActor；两条时第一条删除可执行；删除第一条后只剩角色交互，最后一条 Remove `IsEnabled=False`。证据：`A14-two-start-triggers-dark.png`、`A14-one-start-trigger-remove-disabled.png`。 |
| A15 | BLOCKED | 已通过：节点右键编辑/删除；Session/Task 聚合双击进入；删除确认及 incident wire 同删；selected state；Item 菜单；individual/collective 创建；新 Session Start 仅 `流程输出`；Choice 创建；Settlement 结果 1/2/3；Start 类型无 EnterStory；GiveItem 下拉在 Item Group 存在时仍只列 Individual；中文端口；普通 UI 不展示 Node GUID；保存重启后 stable IDs/Line 不漂移；Logic output-many 与 Flow output-one 有真实约束证据。未完整覆盖：长短端口文字 anchor 漂移、Flow input-many 和 Logic input-one 的全部独立动态组合。证据：`A15-*`。 |

## A9 决定性复现

复现对象为 Story 中已有：

```text
interact_detective.flow_out -> play_final.flow_in
```

在真实窗口从 `interact_detective.flow_out` 拖向新 Action 的 `flow_in`：

1. 拖动前仅有原正式连接；
2. 拖动中原连接仍连接 `play_final`；
3. 同时出现从同一输出到鼠标/新目标的第二条蓝线；
4. 释放后出现 `graph.connection.flow.output.multiple_targets`；
5. 保存 JSON 后该输出仍只有原 endpoint，未完成重接。

这不是视觉色值差异，而是 PLAN A9 明确禁止的“原线 + 第二条 draft/新增线”交互语义。

## 自动化回归与构件

- `DarkGreyRPG.Studio.Tests`：347/347 PASS，0 failed，0 skipped。
- `DarkGreyRPG.Studio.Wpf.Tests`：355/355 PASS，0 failed，0 skipped。
- 合计：702/702 PASS。
- TRX：`core-second-pass.trx`、`wpf-second-pass.trx`。
- RC EXE 复验后 SHA-256 未变化：`A460437194662A3E0F4E624B0F5C541EB4A0312352506CDEA05DD64D109D2971`。

自动化通过不覆盖 A9 的真实窗口失败，也不能替代 A1/A2/A12/A13/A15 的缺失动态边界。

## 最终判定

按 PLAN：A9 已出现真实 FAIL，且 A1/A2/A12/A13/A15 尚未获得完整 Gate 证据。因此当前 RC 只能判定为：

```text
0.3.1.2A SECOND-PASS ACCEPTANCE = NO-GO
USER_ACCEPTED = NO
```

本轮只做审核与证据固化，没有对发现的 A9 缺陷实施修复。
