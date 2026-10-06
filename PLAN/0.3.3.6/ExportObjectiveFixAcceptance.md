# 0.3.3.6 导出失败修正验收（2026-10-06）

本次处理 `.dgrs` 和 `.dgrs.g` 导出均失败的问题，沿用 0.3.3.6。Studio 与 Runtime 同步修正；已有 Task、稳定端口、显式输出顺序和旧结果槽拒绝规则不变。未自动填写目标说明、删除资源、迁移用户文件或创建成品包。

## 原因与修正

权威 Studio 的六次导出日志均报 `graph.objective.description.invalid`。单故事“测试故事”中任务“啊”的默认目标说明为空；故事组中“故事组B”的任务“继续下一段对话”也有空说明。导出包含故事拥有的全部资源，所以即使默认任务尚未放进故事画布也会触发错误。上一轮仅用有效的另建故事验证导出，没有覆盖这个实际项目，遗漏了此问题。

目标说明是显示文字，现在允许空字符串及空白字符串，仍拒绝缺失或非字符串的数据。Runtime 与任务日志使用节点名称作为显示兜底，不写回资源。需要执行的目标对象仍须有效；未选择目标且未接入完成逻辑的默认目标沿用已有规则，保持 INACTIVE。空 Task 和零结算 Task 保持 ACTIVE，不生成最终结果。

导出校验保留资源闭包和原有拒绝规则，收集同一次导出的图错误。错误界面列出故事、任务／会话、节点和需修正内容；双击问题条目可打开对应资源、目标 Inspector。失败不替换已有包，不留下部分导出。诊断记录携带内部身份，未新增持久化字段。

## 自动测试

- 相关 Core：28 通过，0 失败。
- 相关 WPF：47 通过，0 失败，包含单故事／故事组的多个错误定位、原导出文件保留和导航回归。
- 完整 Core：503 通过，0 失败。
- 完整 WPF：685 通过，0 失败，1 跳过；跳过项为默认未启用的 `FixedThreeHundredNodeWorkload` 性能基准。
- Java：`build`、Spotless、Checkstyle、test 和 `publicOutputPriority0336Probe` 通过。新增空说明／空白说明的运行、日志显示、终态恢复和重复事件检查，以及非字符串说明拒绝检查。
- Java 正式 `StoryPackageLoader` 直接加载原生 UI 导出的两个包，确认 3 个 Story、4 个 Task 全部可启动及恢复。多结算优先级、节点／JSON 顺序变化、唯一最终结果和奖励收据回归通过。

自动证据：`evidence/ExportObjectiveFix/Tests/*.trx`、`TestCounts.json`、`java-build.log`、`java-native-packages.log`。

## 代理原生实机

使用 UI Automation 和 Win32 控制本次候选副本，将权威 `Data/Projects/Project_2` 的 31 个文件逐字节复制至隔离项目。通过“项目 → 导出故事包”和文件夹选择窗口实际导出：

| 包 | 字节 | SHA-256 |
| --- | ---: | --- |
| 测试故事.dgrs | 3946025 | `4b03cf0eb7acd0f867b9bd542fbf19b462303d30b9d606720695fcb40c894e3a` |
| 故事组（1）.dgrs.g | 386546 | `8351d3e841d7ae870d287db41d5067a1f0a055bc864300187cffcd9fe8e307df` |

两次均显示“已导出并验证”，导出前后原项目和隔离副本均无文件内容变化。随后只在隔离副本中清空已连接目标的对象，验证导出阻止、友好错误行、双击定位至目标 Inspector，以及原 `测试故事.dgrs` SHA-256 不变。诊断用修改已恢复。未向用户的 Minecraft 故事包目录投放测试包。

证据：`NativeChecks.json`、`02-single-export-success.png`、`03-group-export-success.png`、`05-diagnostic-row.png`、`06-diagnostic-focus.png`。包留在 `.tooling/0336-ui-followup/ExportObjectiveFix/Exports`，没有建立新的发布包目录。

## 交付与数据保留

候选与权威目录使用 `studio/package-studio.ps1` 完成自包含 Windows x64 Release 更新。权威 EXE 实机启动并正常退出，版本 0.3.3.6，无错误窗口。原有 Data 2661 个文件、64211962 字节，在更新及启动前后按路径、大小、SHA-256 比对均无差异。程序白名单 405 个文件全部存在，根目录仅有 apphost EXE。

| 文件 | 字节 | SHA-256 |
| --- | ---: | --- |
| dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe | 204288 | `8e8bf253b16b1516616d890b8bd604a907cf9d607469e3785d4ab113a5b3b20c` |
| dist/DarkGreyRPGStudio/Program/DarkGreyRPGStudio.dll | 1751040 | `c1b9518e36101871b0906378903dcabdc18f17cb1c7f929d20bf497e369bf370` |
| dist/DarkGreyRPGStudio/Program/DarkGreyRPG.Studio.Core.dll | 1308160 | `13fde463fbf91877e36fa3c10111f8ea4bbc812328a9322e41200c22eefd5e6e` |
| dist/darkgrey_rpg-0.3.3.6.jar | 1953544 | `8cea55b7b577df8686e2a965caa895e229774b12bcc99c8d1492245e6b400031` |

apphost 字节不变；此次实现变化位于 Program 中的 Studio／Core DLL 及 Runtime JAR。只将本轮确认无用的 4 个发布暂存目录（2374304102 字节）送入回收站，核对 4 个可恢复载荷；未清空回收站，也未处理原先未知历史暂存。

交付证据：`DeliveryProof.json`、`DistSmoke.json`、`RecycleProof.json`。工作区改动保留，未提交、发布、创建 ZIP 或更改版本。以上为自动测试与代理实机验收；本次修复尚未记为用户验收通过。
