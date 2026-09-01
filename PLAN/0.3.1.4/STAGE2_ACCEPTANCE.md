# DarkGrey_RPG 0.3.1.4 — Stage 2 Acceptance

> Work Package B：P0 Node Layout Persistence
> 结论：`GATE_STAGE2=PASS` / `AGENT_VERIFIED_STAGE2`
> 边界：本 Gate 使用 staging self-contained Release 候选，不是最终 `dist` 权威交付物，不代表 `USER_ACCEPTED`。

## 1. 候选包

- 路径：`.tooling/0314-stage2-publish/DarkGreyRPGStudio.exe`
- ProductVersion：`0.3.1.3-rc`
- FileVersion：`0.3.1.3`
- EXE 大小：`204288` bytes
- EXE SHA-256：`B0331653A0A98905A7E341C3D64756E9D04C5ED23BACCAFA38436BE387959720`
- 说明：最终版本号和 `dist` 权威交付留到 Stage 6；此包仅用于当前源码的 Stage 2 实窗验收。

## 2. 实现结果

- canonical Runtime 图数据、Studio node layout、per-graph viewport 三者保持独立。
- 项目级单一 sidecar：`resources/editor/studio_layout.json`，`schema_version = 1`。
- sidecar key 为 `resource_kind + stable resource id`；DisplayName 重命名不改 key。
- sidecar 使用现有 `AtomicFileWriter` 原子写入；读取时 fail closed，并过滤空 ID、非有限坐标和不存在的 node ID。
- node move 只触发 `LayoutChanged`，不增加 GraphRevision、不修改 canonical snapshot。
- editor 分别跟踪 `IsGraphDirty` / `IsLayoutDirty`，总 dirty 为两者 OR；只改 layout 也可 Ctrl+S、Save All，并阻止关闭时静默丢失。
- 保存时 canonical 与 layout 各自只在对应 dirty 时写入；Session/Task 的 layout-only 保存不会触发 Story aggregate synchronization。
- Save All 先保存 dirty Session/Task，最后保存可能因 aggregate 同步变脏的 Story，避免漏写。
- 删除 node 后 stale layout entry 在下一次保存清理；本会话 Undo 恢复相同 stable node ID 时仍可复用 host 内位置。
- 每个 editor 继续拥有独立 `GraphViewportState`；viewport 不写入 sidecar。

## 3. 自动化证据

| 范围 | 结果 | 证据 |
|---|---:|---|
| Core full | `363/363` PASS | `evidence/stage2/full/Stage2-Core-full.trx` |
| WPF full | `394/394` PASS | `evidence/stage2/full/Stage2-Wpf-full.trx` |

覆盖：sidecar roundtrip、Story/Session/Task key 隔离、finite filtering、deleted cleanup、stable resource ID、原子写入、canonical serialization before/after node move byte-equivalent、layout-only save 后 canonical 文件 byte-identical、Story Package 排除 layout、重新创建 workspace/editor 后加载布局、每 editor viewport object 独立。

## 4. 真实 Release EXE Gate

隔离项目：`.tooling/0314-stage2-layout-project`

- 实际打开 `1 Story + 2 Session + 2 Task`，每图包含独立节点与 canonical connections。
- 使用真实 pointer drag 分别移动五张图中的节点；sidecar 最终包含五个 key 和全部 live node ID。
- 使用真实 `Ctrl+Shift+S` Save All；状态栏明确显示“所有未保存图资源与布局已写入磁盘”。
- 在同一进程分别平移 Story 与 Session A，来回切图后两者节点屏幕坐标各自恢复，误差 `0 px`，证明 viewport 不串图。
- 实际把 Story Start trigger 从原名改为 `布局触发器（已重命名）`，保存后关闭并重新启动；stable port connection 与重命名文本均恢复。
- 关闭前与 Dark 重启后，五张图在 `100% + 重置 viewport` 下的节点屏幕坐标完全相同；DPI `119`，每图 X/Y 漂移均 `0 px`，严格小于 `1 DIP`。
- 再关闭并用 Light theme 重启；Story 节点屏幕坐标仍为 `(948, 407)`，与 Dark 完全相同。
- theme 切换前后 sidecar SHA-256 均为 `EF2885618E1E959A3B16D8536190BE1437123B36CE664BB79F051AB4D35737E0`，没有被主题或重启改写。

逐图坐标、viewport 观测、DPI、graph keys、截图路径与 sidecar hash：`evidence/stage2/stage2-layout-gate.json`。

## 5. 视觉证据

| 文件 | SHA-256 | 用途 |
|---|---|---|
| `evidence/stage2/ui/stage2-dark-before-restart.png` | `12068722A218506A8E1D1A8EF111D63C4294124A61F3C119FD684A672C5D782B` | Dark，真实 Save All 后、关闭前；节点、连接、重命名 trigger、已保存状态 |
| `evidence/stage2/ui/stage2-dark-after-restart.png` | `2380989124E59B57B2FE44B78D1E44928418A6EB61569C42979B0EB7ECCE8487` | Dark 重启后相同布局与连接 |
| `evidence/stage2/ui/stage2-light-after-restart.png` | `16C5EA818CFD1D2E094948B3E1D31D9B96E98A351FA319ED34A28054CD2F410F` | Light 重启后相同布局数据与连接 |

截图已人工检查：三张均为真实 Studio 窗口；Story Start 与 Terminate 的 Flow connection 正确连接 stable ports，重命名文本可见，Dark/Light 下节点位置一致。

## 6. 证据边界与验收驱动记录

- sidecar 不进入 Story Package 由 exporter 自动化断言；Java Runtime 未被修改且不会读取 `resources/editor`。
- 首次实窗驱动失败是 fixture 缺少 ProjectService 强制要求的 `actors` 目录；补齐隔离 fixture 后继续。第二次、第三次失败分别是 UIA node peer 与 viewport peer 的 AutomationId 定位不正确；根据真实 UIA tree 改为 `CanonicalGraphNode_<stable-id>` 与 `CanonicalStoryWorkspaceGraph` 后，完整流程从重新生成 fixture 开始一次通过。这些均是验收驱动问题，不计为产品 PASS，也未隐藏。
- Stage 2 不处理 wire gesture、Inspector hierarchy、Choice row、node delete UX；它们严格留给 Stage 3–4。

## 7. Gate

`GATE_STAGE2=PASS`

理由：Work Package B 的数据边界、dirty/save/close 语义、原子 sidecar、stable identity、删除清理、package exclusion、restart fixture 与真实 self-contained Release EXE 的五图 save/close/restart、viewport、rename、Dark/Light Gate 均形成可复核证据；最终权威 `dist` 交付与用户验收仍延后到 Stage 6。
