# DarkGrey RPG Studio WPF 重建审计

审计日期：2026-08-10  
计划：`PLAN/DarkGrey_RPG_WPF_Studio_Full_Rebuild_Plan.md`  
范围：WPF Studio Phase 1 的 STEP 1（审计与冻结）

## 结论

- 现有 Godot Studio 已冻结并从 `studio/` 归档到
  `legacy/studio-godot-prototype/`，归档共 50 个文件。
- 仓库当前没有 `.git` 元数据，因此无法按计划创建 Git 安全点提交；本次没有初始化新仓库，避免替用户决定版本库边界。
- Java / Forge 1.7.10 Runtime、CNPC+ Actor Binding、Live Bridge、示例项目和现有构建产物均未修改。
- 新 Studio 必须复用现有 Actor JSON 字段和 Runtime 加载约束，但不得复用旧 Godot UI 架构。
- 当前机器的全局 `dotnet` 只有 SDK 8.0.204；STEP 2 需要先提供隔离在 E 盘的稳定 .NET 10 SDK，不能以 .NET 8 降级代替计划要求。

## 冻结基线

| 项目 | 当前基线 |
| --- | --- |
| Godot 原型归档 | `legacy/studio-godot-prototype/` |
| Java Runtime 源码 | `src/main/java/`（保持原样） |
| Runtime JAR | `build/libs/darkgrey_rpg-0.5.0.jar` |
| Runtime JAR SHA-256 | `0342C9EF9137DDE652C31E0E4EF862482C1F683360D4DD4350EE74DE374DF388` |
| 旧便携 Studio EXE | `dist/DarkGreyRPGStudio.exe`（历史产物，不再作为 2.0 输出） |
| 当前旧 EXE SHA-256 | `EA8F63617C783E584977ED82E7CCFECA6FAC6545B797EE9FC3B057701A7A226C` |

根目录的旧 `scripts/package-studio.ps1` 仍属于 Godot 原型打包链。它在 WPF 打包流程建立前仅供历史查阅，不应再作为默认 Studio 构建入口。

## 可保留的数据格式

现有 Runtime 的 `ActorDefinition` 与 `ProjectRepository` 确认 Actor 资源使用以下 JSON 字段：

```json
{
  "schema_version": 1,
  "id": "tavern_owner",
  "display_name": "酒馆老板",
  "notes": "Owns the tavern and introduces the slime quest.",
  "tags": ["town", "merchant"]
}
```

兼容要求：

- `schema_version` 当前只支持 `1`。
- Actor 文件位于 `<project>/actors/<id>.json`，文件名必须与 `id` 一致。
- `display_name` 必须为非空字符串；`notes` 为字符串；`tags` 为字符串数组。
- Runtime 当前接受的资源 ID 模式为 `[a-z0-9][a-z0-9_.-]*`。
- WPF Phase 1 对新建 ID 按计划采用更严格的 `[a-z0-9_-]+`；读取既有含 `.` ID 时必须兼容并明确提示，不能静默破坏旧项目。
- `project.json` 继续使用 `schema_version`、`id`、`display_name`。

本轮不修改 Actor schema，也不把 CNPC 的模型、皮肤、生命值、AI、攻击、移速或动画字段写入 Actor。

## 可保留的 Runtime API 与行为

以下 Java / Forge 行为属于稳定边界，新 Studio 只能通过兼容 JSON 或既有本地协议与其协作：

- `ProjectRepository.reload()` 及 `/dgrpg reload` 项目重载流程。
- `config/darkgrey_rpg.cfg` 中的项目目录配置与默认项目名。
- `EntityNPCInterface.wrappedNPC` 暴露的 `ICustomNpc` Actor Binding。
- CNPC+ Stored Data 键 `darkgrey_rpg.actor_id` 的持久化语义。
- Live Bridge 默认回环地址 `127.0.0.1:32145` 及现有 JSON Lines 协议。

Phase 1 不重写 Forge 工程、CNPC+ 集成或 Runtime Project Loader。

## Godot 版 Actor 保存问题

审计旧 `RpgProjectStore.gd` 和 `Main.gd` 后确认以下问题，WPF 实现不得复制：

1. `_atomic_write` 先把有效文件重命名为 `.bak`，再把 `.tmp` 重命名为目标。两个重命名之间目标文件不存在；进程或系统在此窗口中断时，主文件会缺失，因此不是计划要求的单步原子替换。
2. Actor Rename 先写入新 ID 文件，再删除旧 ID 文件。若旧文件删除失败，函数返回失败，但磁盘上可能同时存在新旧两个文件，形成部分提交和重复资源。
3. Rename 的目标碰撞主要由 UI 内存字典预检查，Repository 自身没有完整的磁盘碰撞保护；未载入或无效 JSON 文件仍可能成为覆盖目标。
4. 已保存 Actor 的 ID 在普通文本框中直接编辑并隐式触发 Rename，资源级操作与字段编辑没有分离。
5. 保存失败的主反馈为 `Actor save failed. See Problems.`，缺少自然语言原因；Dirty 状态也主要通过保存按钮文字追加 `*` 表达。

WPF Core 必须把 Save 与 Rename 设计成独立 Repository 操作，先验证和检查碰撞，再使用同目录临时文件与平台原子替换能力；失败后保留最后一个有效文件和 Document Dirty 状态。

## 新旧 Studio 边界

### 仅归档查阅

- Godot `.tscn`、`.gd`、Theme、Inspector、Tree 和窗口布局。
- Godot 导出模板、便携启动、翻译辅助脚本和旧 UI Probe。
- 旧 EXE 及其打包脚本。

### 允许复用

- Actor / Project JSON 的业务字段与兼容语义。
- Runtime 命令、CNPC+ Actor Binding 和 Live Bridge 协议事实。
- 示例项目作为兼容性测试夹具，但不作为新 UI 模板。

### 新建

- `studio/DarkGreyRPG.Studio.sln`。
- .NET 10 WPF App、无 WPF 依赖的 Core、自动化 Tests。
- MVVM App Shell、Actor CRUD、Project Service、反馈系统和 WPF 打包链。

Dialogue、Quest、Story 在 Phase 1 只提供真实可切换的 Placeholder，不实现正式编辑器业务。

## STEP 1 验证记录

- `git status`：失败，原因是当前目录不是 Git 仓库；已如实记录，没有伪造安全点。
- Godot 归档：通过；原 `studio/` 已不存在，归档目录存在且包含 50 个文件。
- Runtime 基线哈希：通过；JAR SHA-256 与已验证的 0.5.0 基线一致。
- Runtime 源码修改：无。
- .NET 10 前置条件：未满足，已作为 STEP 2 的环境准备项记录。
