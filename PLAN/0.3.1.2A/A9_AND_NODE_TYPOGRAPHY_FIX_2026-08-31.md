# DRG Studio 0.3.1.2A A9 与节点参数字体修复复验

日期：2026-08-31  
修复候选：`.tooling/0312a-a9-fix/publish-typography/DarkGreyRPGStudio.exe`  
版本：`0.3.1.2A`  
SHA-256：`F288DBD8A186616932FC80C43FD76DB648913F1130282E0F11C1BAC388D31C82`  
文件大小：`141338326` bytes  
分支 / HEAD：`codex/0.3.1.2A` / `8d5259f0a64119e3d3813f2c90bea1615462eaf7`

本轮修复第二轮验收重新发现的 A9 Flow 单线重接缺陷，并追加修复真实窗口中暴露的深色节点参数黑字问题。旧 RC `dist/DarkGreyRPGStudio-0.3.1.2A/DarkGreyRPGStudio.exe` 未覆盖、未提升；没有提交、推送或发布。

## 修复内容

### A9：占用中的单基数端口必须重用正式线

根因位于 `CanonicalGraphEditorView.BeginWire`：旧逻辑只在 incident 数量大于 1 时进入已有连线移动；Flow Output / Logic Input 只有一条 incident 时仍按“新建连线”处理，因此保留原线并创建第二条拖动线，最终触发 `graph.connection.flow.output.multiple_targets`。

修复后：

- Flow Output / Logic Input 为单基数端口；恰有一条 incident 时直接把该正式 connection visual 作为重接对象；
- Flow Input / Logic Output 保持多 incident 语义；单条时仍允许新增，多条时整束移动；
- cancel 恢复原 endpoint 与原正式线；commit 原子替换 endpoint，不生成第二条连接。

新增两条 WPF 回归：

- 直接拖动占用中的 Flow Output 时只保留同一条正式 Path，并成功重接；
- 直接拖动后取消时恢复原 JSON 与原正式 Path。

### 深色节点参数字体：显式跨过 WPF 前景色截断点

真实 100% 窗口发现固定深色节点卡片内的“参数 / 目标类型 / 目标对象 / 数量”为黑色。根因不是主题资源缺失，而是 `Expander` 与嵌套 `UserControl` 各自的本地默认黑色 `Foreground` 截断了节点根控件的继承。

修复在固定深色节点表面、参数 Expander 和内联参数编辑器三个层级显式使用 `#F7FAFC`。新增 WPF 回归逐项断言上述四类标签的实际渲染前景色，不再只检查根控件属性。

## 自动化回归

- `DarkGreyRPG.Studio.Tests`：347/347 PASS，0 failed，0 skipped；
- `DarkGreyRPG.Studio.Wpf.Tests`：358/358 PASS，0 failed，0 skipped；
- 合计：705/705 PASS；
- `dotnet format ... whitespace --verify-no-changes`：PASS；
- `git diff --check`：PASS（仅现有 LF/CRLF 提示，无 whitespace error）。

TRX：

- `evidence/a9-typography-fix-2026-08-31/core-a9-typography-fix.trx`
- `evidence/a9-typography-fix-2026-08-31/wpf-a9-typography-fix.trx`

## 真实候选 EXE 复验

复验使用隔离项目 `.tooling/0312a-a9-fix/live-project`，没有操作原项目。

### Flow 单线取消

从已有连接的 `interact_detective.flow_out` 直接拖向空白：

- 拖动中原目标线不再保留，只移动一条正式蓝线；
- Esc 取消后正式线恢复；
- Story JSON SHA-256 前后均为 `191F603E8C17391E94E18884CD848282A24B0B489A9D3B5C288512AC0452D930`。

证据：`A9-final-flow-cancel-drag-cancel-before-escape.png`、`A9-final-flow-cancel-after-cancel.png`。

### Flow 单线成功重接

最终候选从 `interact_detective.flow_out -> start_evidence.flow_in` 直接重接到 `play_final.flow_in`：

- 拖动中没有“原线 + 第二条 draft line”；
- 保存后该输出只有一个 endpoint：`interact_detective.flow_out -> play_final.flow_in`；
- JSON SHA-256 从 `191F603E...452D930` 变为 `0E18DD4F...0BEA991`；
- 真实窗口没有 `graph.connection.flow.output.multiple_targets`。

证据：`A9-final-flow-reconnect-drag-before-commit.png`、`A9-final-flow-reconnect-after-commit.png`。

### Logic 多 incident 取消

`collect_evidence.logic_status` 的两条 Logic incident 仍按整束语义移动，Esc 后两条黄色正式线恢复；Task JSON SHA-256 前后均为 `ACAF5DA6C34266DAC0999F37D6B4FE706FFAEC71B9145A6AA38BB12B94E513BD`。

证据：`A9-logic-bundle-live-drag-cancel-before-escape.png`、`A9-logic-bundle-live-after-cancel.png`。

### 节点参数字体

在真实候选 EXE 中进入“王城迷案 -> 搜集证物”，切换到 100% 缩放。三个 Objective 节点的“参数 / 目标类型 / 目标对象 / 数量”均为浅色并在固定深色卡片上可读；输入控件与端口文字未被改坏。

证据：`A9-typography-100-percent-fixed.png`。

## 判定边界

```text
A9 RE-DISCOVERED DEFECT = REPAIRED_IN_CANDIDATE
DARK NODE PARAMETER TYPOGRAPHY = REPAIRED_IN_CANDIDATE
OLD DIST RC = UNCHANGED
USER_ACCEPTED = NO
```

第二轮报告仍作为原 RC 的历史 NO-GO 记录保留。A1、A2、A12、A13、A15 的严格 Gate 证据缺口没有因本轮修复自动消失，因此不能把整个 0.3.1.2A 宣布为最终 PASS，也不能未经授权覆盖旧 RC 或发布候选。
