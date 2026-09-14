> 2026-09-14 最终集成更新：本文件保留工作包阶段记录；后续编译修复、真实交互及最终证据以 [DELIVERY_REPORT.md](DELIVERY_REPORT.md) 为准。阶段性待办不能覆盖最终记录，未实测部分也不自动计为通过。

# WP-B：公共数值控件与输入水印

状态：已实现源码与 STA 回归；Studio 整体构建仍需主线先修复现有 `ActorEditorViewModel.cs` 语法错误。本文只记录 A22/A12 的公共控件边界，不代表用户验收或 0.3.3.1 完成。

## NumericDrag API

`studio/src/DarkGreyRPG.Studio/Views/Graph/NumericDrag.cs` 提供以下附加属性：

- `NumericDrag.Step`：大于零时安装行为；鼠标向右增加、向左减少。
- `NumericDrag.Minimum` / `NumericDrag.Maximum`：拖动结果在范围内夹取。
- `NumericDrag.CommittedEvent`：拖动超过阈值并在释放时提交后冒泡；`NumericDrag.CommittedEventArgs` 提供 `BeforeText` 与 `ValueText`。

未聚焦的有效数字框在按下时进入待定手势；超过系统横向拖动阈值才进入拖动。阈值前释放会聚焦并选中全部文字。已经聚焦的 TextBox 不被行为拦截，因此选字、中文输入法组合态、空草稿、单独负号和其他未完成文本仍由 WPF 正常处理。

拖动预览期间暂时解除 `TextBox.Text` binding。释放时 PropertyChanged binding 只应用一次最终值，LostFocus/Default/Explicit binding 显式更新一次；Esc、丢失鼠标捕获和卸载均恢复原值且不更新源。无 binding 的字段也恢复取消前的文字，并通过冒泡事件交给所属宿主提交。

## SessionScreenEditor 接线要求

主线接入 `SessionScreenEditor` 时，在控件构造期间对根控件注册 `NumericDrag.CommittedEvent`（或使用 `NumericDrag.AddCommittedHandler`）：

```csharp
AddHandler(NumericDrag.CommittedEvent, OnNumericDragCommitted);
```

处理器应从 `e.OriginalSource` 取得 TextBox，使用现有 `Tag`/AutomationId 到 `_fields` 的字段键映射，按 `NumericDrag.CommittedEventArgs.ValueText` 使用不变量数字解析，写入当前 `_layers` 项，然后调用现有的单次 `Commit()`。不要在 `PreviewMouseMove` 写盘、刷新列表或重载资源；未解析的自由文本保持草稿并由原有验证显示错误。事件只在有效拖动提交时发送，取消无需额外回滚通知。

## TextInputWatermark API

`studio/src/DarkGreyRPG.Studio/Views/Graph/TextInputWatermark.cs` 提供 `TextInputWatermark.Text`（并提供等价的 `Watermark` 拼写）。行为使用不可命中的 Adorner，仅在 TextBox 为空、启用、可见且未获得键盘焦点时显示；它从不写入 `TextBox.Text`，所以不会保存、导出、清除或覆盖用户内容。Text 改变、焦点、可见性、启用状态和卸载都会刷新或移除 Adorner。

## Verification

新增 `NumericDragAndWatermark0331Tests` 覆盖空/非法草稿保持、冒泡提交事件、水印 Adorner 生命周期及不改写文本、PropertyChanged/LostFocus binding 每手势一次源 setter、绑定取消不改源。受主线现有语法错误影响，完整 WPF 项目暂不能运行；隔离 WPF 编译项目构建成功，6/6 隔离 STA 测试通过。
