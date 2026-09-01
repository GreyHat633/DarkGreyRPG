# 0.3.1.3 Evidence Index

## 原始审计重读

- `audit/final-original-audit-reread.pdf`：Word 2019 从原始 DOCX 最终导出，12 页。
- `audit/final-original-audit-reread.txt`：最终文本提取。
- `audit/pages/page-01.png` … `page-12.png`：逐页渲染。
- `audit/pages/contact-sheet-12-pages.png`：12 页视觉总览。
- 原始 DOCX：`PLAN/0.3.1.2审计.docx`，1,965,091 bytes，SHA-256 `D399ECA704127358192A4B68699E1A14C7D68FF7EAF5873A0AA4DDEF9418105F`。

## Baseline / Automated

- `baseline/Core/Core-baseline.trx`：基线 349/349。
- `baseline/Wpf/Wpf-baseline.trx`：基线 368/368。
- `automated/Core-final.trx`：最终 354/354。
- `automated/Wpf-final.trx`：最终 380/380。
- `automated/FINAL_TEST_RESULTS.md`：.NET、Java 8 probes 与关键回归摘要。

## Release EXE 实窗证据

| 文件 | 证明内容 | 边界 |
|---|---|---|
| `live/01-project-home-light.png` | Light Project Home 三栏 | 静态视觉 |
| `live/02-start-four-conditions-light.png` | 四条件、字段标签、条件名称 | 静态视觉 |
| `live/03-start-inspector-scroll.png` | Inspector 滚到末尾条件 | 实际滚动后帧 |
| `live/04-trigger-type-popup.png` | Start 类型下拉可打开 | 不单独证明选择后的全链 |
| `live/08-actors-expanded-before-drag.png` | 角色资源树与拖放起点 | 静态视觉 |
| `live/10-restarted-fixed-build-project-home-dark.png` | 修复构建重启、Dark 三栏 | 静态视觉 |
| `live/11-fixed-actor-parameter-and-node-inspector.png` | 角色参数与节点 Inspector | 选择后状态 |
| `live/13-invalid-radius-near-field-visible.png` | 普通区只显示可操作中文 | 实际非法输入 |
| `live/14-problems-detailed-stable-code.png` | Problems 显示稳定 code/detail | 同一错误的详细视图 |
| `live/16-session-ghost-during-real-drag.png` | ghost 与鼠标相对锚点 | 拖动中的真实帧 |
| `live/17-session-task-aggregate-nodes.png` | 资源拖入生成 aggregate placement | 释放后状态 |
| `live/18-normal-reconnect-ctrl-bundle-result.png` | 新增/重连/Ctrl bundle 结果 | 无连续视频，C7 仍待用户全矩阵 |
| `live/19-session-resource-delete-confirmation.png` | 资源删除确认 | 操作前确认 |
| `live/22-post-delete-edit-no-resurrection.png` | 删除后继续编辑保存不复活 Session | 修复后的关键证据 |
| `live/23-placement-delete-task-resource-preserved.png` | 反向删 placement 保留 Task 资源 | 修复后状态 |
| `live/24-actor-reorder-before-preview.png` | before 插入线 | 未覆盖完整重排矩阵 |
| `live/25-story-viewport-restored-125-percent-pan.png` | Story 125%/pan 恢复 | 来回切换后 |
| `live/26-task-viewport-restored-90-percent-pan.png` | Task 90%/pan 恢复 | 来回切换后 |
| `live/27-objective-collect-item-switch.png` | Objective 物品收集参数 | 选择后状态 |
| `live/28-objective-actor-parameter-switch.png` | Objective 角色交互，无数量字段 | 选择后状态 |
| `live/29-actor-dropped-into-objective-parameter-node-count-unchanged.png` | 角色进入属性参数，节点数不变 | 用户边界的直接证据 |
| `live/30-light-project-home-1100x700-logical-dpi119.png` | Light，约 1100×700 logical，DPI119 | 实际 1364×868 physical |
| `live/31-light-project-home-1700x980-physical-dpi119.png` | Light 大窗口 | DPI119 |
| `live/32-dark-project-home-1700x980-physical-dpi119.png` | Dark 大窗口 | DPI119 |
| `live/33-dark-project-home-1100x700-logical-dpi119.png` | Dark，约 1100×700 logical | DPI119 |

`05`–`07`、`09`、`12`、`20`、`21` 保留为施工过程记录，不作为最终通过证据。错误 cursor/ghost 捕获已删除，避免误导。

