# DarkGrey_RPG 0.3.1.4 — Stage 4 Acceptance

状态：`IMPLEMENTED / AUTOMATED_PASS / LIVE_GATE_DEFERRED_BY_USER`

> 2026-09-01 用户明确要求将需要实机验收的环节暂时搁置，等其授权后再进行。因此本文只关闭 Work Package D/E/F 的源码、自动化与静态证据；任何真实窗口、鼠标键盘、主题、DPI、关闭重开证据均为 `DEFERRED_BY_USER / NEED_USER_REAUTHORIZATION`，不得写成 `AGENT_VERIFIED` 或 `USER_ACCEPTED`。

## Work Package D — Actor / Item Inspector

- 资源属性改为稳定 field block：11px secondary label、14px primary value、12px field spacing。
- 保持正式作者术语：`角色 / NPC_ID`、`角色组 / Group_ID`、`物品 / Item_ID`、`物品组 / Group_ID`。
- IDs 与 tags 保持可见；Inspector 继续使用 `CanonicalInspectorScrollViewer` 的纵向自动滚动。
- 普通资源 Inspector 的主要“资源属性”区已移除“拥有/引用状态”；内部 membership 状态与资源生命周期未改。
- 自动化：`CanonicalResourceInspectorHierarchyTests` 覆盖四类资源、层级、术语、滚动与 ownership 行缺席。

## Work Package E — Choice option ↔ Flow Output

- Choice 使用专用 ordered row projection；每行从正式 `options[]` 读取稳定 `option_id`、`display_text`、`flow_port_id`。
- Flow 与 Logic output 均由稳定 ID 唯一解析；不依赖 display text 或固定序号。
- 1/2/5/10 options 均断言 option order、Flow order 与 rendered row order 一致。
- 长文本下所有 Flow anchor X 保持一致并贴右边缘。
- rename/reorder 后保留同一个 node visual、selection、node position 与原 stable-port connections；只走当前 Choice node 的 ports refresh。
- 非 Choice node 继续使用原 generic input/output projection。

## Work Package F — Graph Node 删除 UX

- 右键【删除】与 Delete 键均立即调用同一节点删除路径，不再创建或调用 `MessageBox` / confirmation seam。
- UI 对 referenced node 显式执行 Core 的 incident-wire cleanup；节点与连线在同一个 Undo transaction 内。
- 一次 Undo 恢复节点与全部 incident wires。
- `Required / NonDeletable` 节点继续由 Core 拒绝，选择与 Problems evidence 保留。
- Aggregate placement 删除只改变 Story graph；对应 Session/Task 文件与 ownership membership 保留。
- 资源本体删除服务及其 destructive confirmation 边界未改。
- 历史“node deletion always asks for confirmation” assertion 已在 `HISTORICAL_REGRESSION_BASELINE.md` 标记为 superseded；Core 的保守低层 confirmation 参数仍保留给非交互调用者。

## 自动化证据

- Stage 4 integrated targeted WPF：`6/6` PASS。
  - `evidence/stage4/targeted/Stage4-integrated-targeted-fixed.trx`
- Aggregate placement lifecycle targeted Core：`1/1` PASS。
  - `evidence/stage4/targeted/Stage4-Delete-lifecycle-targeted-fixed.trx`
- Stage 4 full Core：`364/364` PASS。
  - `evidence/stage4/full/Stage4-Core-full.trx`
- Stage 4 full WPF：`405/405` PASS。
  - `evidence/stage4/full/Stage4-Wpf-full.trx`
- `git diff --check`：PASS（仅 Git 的 LF→CRLF working-copy warnings，无 whitespace error）。

不计为产品失败的施工期测试问题：

- 首次 Delete targeted WPF run 未设置 `windir`，在 WPF 字体缓存静态初始化阶段报 `UriFormatException`，未进入测试逻辑；按仓库约定设置 `$env:windir=$env:SystemRoot` 后 `3/3` PASS。
- 新增 placement lifecycle test 的第一版遗漏 `GraphResourceEnvelope.Graph` detached snapshot 的回写，导致测试自身读取旧快照；补回 `story.Graph = session.Document` 后 `1/1` PASS，生产代码未因此修改。

## 暂停的真实 Gate

以下项目全部为 `DEFERRED_BY_USER / NEED_USER_REAUTHORIZATION`：

- Choice：真实创建/增加/重命名/重排/删除 1/2/5/10 options；分别接线；保存、关闭、重开。
- Choice：长文本与紧凑 Logic diamond 的真实可读性、端口命中和不误接线。
- Inspector：Actor/Item 四种类型在 Light/Dark 下的层级、滚动、selected resource 一致性。
- Delete：真实右键与 Delete 均无弹窗；一次 Ctrl+Z 恢复节点与线；固定节点无法删除。
- Lifecycle：真实 placement delete 不删除资源本体；资源本体 delete 仍保留 destructive confirmation。
- DPI：100% / 125% / 150%。

Stage 4 暂不启动或控制 Studio 进程，不生成真实 UI 截图，不将自动化代替实机 Gate。
