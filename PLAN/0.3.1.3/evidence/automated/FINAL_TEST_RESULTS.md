# 0.3.1.3 Final Automated Support

执行日期：2026-09-01

> 本文件只记录自动化支持，不替代 Release EXE 真实 UI Gate。

## .NET

- Core：354/354 PASS
  - TRX：`Core-final.trx`
- WPF：380/380 PASS
  - TRX：`Wpf-final.trx`
- Configuration：Release
- SDK：`E:\Java\dotnet-sdk-10\dotnet.exe`

## Java 8 Runtime Compatibility

- JDK：`E:\Java\jdk1.8.0_471`
- Gradle：E 盘本地 Gradle 与缓存
- `spotlessJavaCheck`：PASS
- `testClasses`：PASS
- `canonicalTaskRuntimeProbe`：`TASK_RUNTIME_PROBE_PASS`
- `canonicalTaskJournalProjectionProbe`：`CANONICAL_TASK_JOURNAL_PROJECTION_PASS`
- 总结：`BUILD SUCCESSFUL`

## 关键回归覆盖

- 100 节点 / 20 个 Inspector：目标节点变化只刷新相关 Inspector。
- Start 条件类型、typed payload、默认名称、保存/Undo/Redo。
- 20 DIP 端口命中槽、标签排除、输出列位置。
- 普通新增、单线重连、Ctrl bundle 的 wire 状态机。
- 资源删除事务、补偿回滚、持久化快照采用与 placement 不复活。
- 资源 DisplayName rename 的全类型 round-trip 深比较。
- Story/Session/Task 独立 viewport。
- Objective 三种语义、角色交互无数量字段、Java Runtime 投影。
- 普通 Inspector 与 Problems 技术详情分层。

