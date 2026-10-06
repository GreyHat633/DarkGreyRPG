# DarkGreyRPG 0.3.3.6 施工 PLAN（最终讨论修订版）

**文档日期：2026-10-04**  
**状态：待施工。本文不代表代码已修改、测试已通过或版本已交付。**  
**仓库：`GreyHat633/DarkGreyRPG`**  
**开发基线：`codex/0.3.3.5`**  
**本次已重新核对的基线提交：`99a3253d7186127bdbe37898824ecbd32219f136`**  
**提交说明：`fix(studio): handle empty resource selections`**  
**目标分支：`codex/0.3.3.6`，已有工作必须先读取、比较并衔接。**

> 本文取代本对话此前生成的 `DarkGreyRPG_0.3.3.6_Construction_PLAN.md`。旧稿和原 HANDOFF 仅作讨论历史，不能与本文并列执行。
>
> **本版主线：删除 Namespace → 建立不可变 Story UID 与隐藏资源身份 → 支持 Story 内容追加迁移 → 自动 Story Group 容器 → O 键 OP 故事包管理器。**
>
> 最重要的修正：**不兼容旧 Namespace 格式；不提供 UID 重生成；迁移可写入非空 Story，且是追加而非覆盖；Group 不是新的 Runtime；冲突双方全部阻止运行，不自动替换任何包。**

**阅读定位：**第 0—3 节为版本边界与身份；第 4 节为内容迁移；第 5—7 节为 Group、文件与引用；第 8—12 节为冲突、启停、UI、代次与媒体；第 16—19 节为施工、测试和交付。

---

## 0. 阅读顺序与依据

### 0.1 信息优先级

1. 本轮用户最后明确的决定，尤其是 Namespace 直接删除、迁移允许非空目标、Group 容器定位、冲突双方禁用、O 键 OP 管理器。
2. 用户接受的配套建议：手动禁用只阻止新 Story 启动；所有已安装包参加 UID 检查；Group 命名规则；Error 与 Conflict 分开；唯一资源所有权；扁平容器。
3. 本文标记为“工程约束／工程默认”的实施细节。它们是为落地上述要求补充的方案，不冒充用户逐项指定。
4. 固定提交的源码事实，用于确定改动落点与限制，不能反过来推翻产品决定。
5. 原 HANDOFF、旧 PLAN、旧口头结论。冲突内容作废，不得恢复。

**范围判断：**用户已同意测试版本破坏旧格式。这里的“故事迁移”是当前版本 A→B 的内容复制功能，绝不是旧 Namespace 项目升级工具。

### 0.2 已废弃的旧方案

| 旧方案／旧判断 | 本次最终要求 |
|---|---|
| Namespace 留作 Legacy／迁移／fallback | 删除 DGR Namespace 业务体系及其兼容入口，只维护当前身份协议。 |
| Story 右键“重新生成 UID” | 删除。UID 仅在新 Story 创建时生成，之后只读、可复制。 |
| A→B 要求 B 为空／强制自动新建 B | 删除限制。允许选择已有非空且可写的 B。 |
| 用 A 覆盖 B／清空 B | 禁止。追加 A 的资源和内部图，B 原内容保留。 |
| 迁移询问是否复制跨 Story 连线 | 删除选项。固定不复制源 Story 的项目级外部 Flow／Logic。 |
| 一个多 Story `.dgrs` 重定义现有单 Story 模型 | `.dgrs` 仍为单 Story；`.dgrs.g` 为多 Story 容器。当前身份 schema 可断代，不承诺旧字节兼容。 |
| Group 拥有 Runtime／进度／永久 UID | 禁止。仅有容器管理信息与自动派生成员。 |
| 部署替换向导／自动选择新旧包 | 删除。重复 Story UID 就报警并阻止所有涉事容器运行。 |
| 手动禁用的包不参加 UID 检查 | 不采用。受管理的已安装包全部参加检查。 |
| 因 Group 新增而禁止既有跨 Project 引用／强制先 Import | 不采用。Reference 保持既有能力，分组来源以整个 Group 为最小只读输入。 |
| Flow 与 Logic 必须另造一套统一图 | 纠正：基线已有统一连接记录，由 `interface_kind` 区分，直接衔接，不能再造双轨图。 |

### 0.3 七个工作包

| 工作包 | 交付范围 |
|---|---|
| WP-A 身份断代 | 删除 Namespace；不可变 Story UID；隐藏 Story-scoped 资源身份；同步所有引用、路径、绑定、存储与协议。 |
| WP-B 故事内容迁移 | A 的自有资源与内部节点图追加到 B；支持非空目标；类型化身份映射；源与目标原内容不被破坏。 |
| WP-C 自动 Story Group | Flow／Logic 连通分量、自动合并拆分、名称与树／画布呈现；不加入手工成员管理。 |
| WP-D 文件容器与引用 | 单 Story `.dgrs`、Group `.dgrs.g`；完整校验；扁平容器；整体 Reference、资源依赖闭包与可编辑导入。 |
| WP-E 扫描、冲突与启停 | 全部已安装包清单；全量 UID 判重；涉事双方阻断；用户禁用与系统阻断分离；不自动替换。 |
| WP-F 故事包管理器 | O 键、OP-only；绿色启用／灰色禁用／红色冲突／独立 Error；树、详情、只读组图与整包操作。 |
| WP-G 运行与回归 | Story 级代次、媒体和运行状态；安全加载／异步失效；保留 0.3.3.5 正常功能；自动测试和真实双端验收。 |

### 0.4 不在本版范围

不新增玩家变量系统、任务节点体系、Group Runtime、Project 部署包 `.dgrp`、跨机器身份检测、云端协作、安装商店、更新器、依赖版本求解器、自动部署替换、旧格式迁移向导。不得为新 UI 重建一套编辑器／GUI 框架。

本文件只授权作为施工输入；本次生成文档没有执行开发。实际开发仍遵守用户当次授权。没有明确 Release 请求，不创建 `artifacts/DGR<version>` 成品发布；不替用户推送、合并、发布或删除正式存档。

---

## 1. 固定基线：必须继承什么、纠正什么

### 1.1 项目实际技术形态

DGR 的当前主线是 **Windows WPF Studio + Minecraft Java Runtime**，不是旧 Godot 原型。Studio 制作 Story／Session／Task 等内容，Runtime 在服务端管理执行与玩家状态，客户端显示和提交交互。基线 README 给出的构建环境是 .NET 10 与 Java 8／Minecraft 1.7.10；实际施工以仓库构建文件为准，不擅自升级引擎、JDK 或 SDK。[SRC-02]

### 1.2 已核对的代码事实

- `CanonicalStoryLogicConnection` 已包含源／目标 Story、源／目标端口以及 `InterfaceKind`；仓库校验接受 `Flow` 和 `Logic`。`story_logic_graph.json` 的旧文件名不等于只存 Logic。[SRC-03]
- `CanonicalStoryLogicGraphRepository.Validate()` 会读取 `OfflineProviderCatalog` 中的 Story 并验证边界。因此，“资源 membership 不能 AddReference 一个 Story”不等于“引用包 Story 不能作为项目图端点”。不能混用两个 API 的限制。[SRC-03]
- `GraphNodeDefinitionRegistry` 把 Story 的 `start` 声明为 Required＋Unique，`GraphScopePolicy` 会检查重复；固定端口也有定义级约束。非空目标的批量复制必须考虑这两个现状，但不得因此恢复“只允许空目标”。[SRC-04]
- 当前单 Story 打包、加载、代次和媒体中存在 `package == story` 的有效假设。加 Group Reader 后应保持 Story 描述对象，并明确外层容器来源；不能让容器总 hash 接管成员的身份或代次。[SRC-07]

本次是计划编写和局部源码核对，**不是全仓库审计或编译验收**。第 16 节要求施工者补齐调用点清单，不能把本文列出的文件当成全部影响范围。

### 1.3 开工约束

读取根 `AGENTS.md`、`.agents/skills/studio-node-ui/SKILL.md`、`.agents/skills/story-media-lifecycle/SKILL.md`；按仓库规则检查委派能力与任务边界。若本环境没有所需能力，记录事实，不伪造 Worker／实机结果。[SRC-01][SRC-05][SRC-06]

检查分支、HEAD、未提交和未跟踪内容；不做 `reset --hard`、`clean` 或覆盖用户已有修改。若远端／本地已有 0336，比较后继续，而不是另造平行实现。

工作记录集中放在 `PLAN/0.3.3.6/`：`Technical_Contracts.md`、`Implementation.md`、`Acceptance.md`、`EvidenceIndex.md`、`Delivery.json`。记录基线、决定、代码落点、证据和未覆盖项即可，不为每个小动作制造一堆文档。

---

## 2. 术语与核心不变量

| 概念 | 唯一职责 | 不得承担的职责 |
|---|---|---|
| Project | 作者在 Studio 中的工作空间。 | 服务器／玩家的 Story 身份，整项目部署身份。 |
| Story UID | 一个 Story 的稳定、可见、不可修改身份。 | 内容版本、文件名、用户账号或机器标识。 |
| Local Resource ID | Story 内部资源的隐藏身份。 | 全项目／全服务器唯一键、作者手填名称。 |
| ResourceAddress | `Story UID + Resource Kind + Local ID`。 | 用名称、来源文件名或旧 Namespace 猜测资源。 |
| Story Graph | Story 之间的正式 Flow／Logic 关系。 | 普通资源引用关系、Group 可手工维护的成员表。 |
| Story Group | 相互联动 Story 的自动容器／捆绑集合。 | 新的 Story 类型、Group 玩家进度、Group Start／End。 |
| `.dgrs` | 一个 Story 的当前格式部署文件。 | Project 包或多 Story 容器。 |
| `.dgrs.g` | 多个关联 Story 及完整组内图的外层容器。 | 新执行引擎、永久 Group 身份。 |
| Reference | 从完整外部来源只读使用定义。 | 自动复制为 Owned、自动改外部 UID。 |
| Import | 把外部内容复制为本地可编辑内容。 | 修改原包，替换别人部署的身份。 |
| 故事内容迁移 | A 的内容批量追加到 B。 | 文件格式升级、UID 重生成、覆盖／清空 B。 |

**三个边界必须独立：**

```text
身份和执行：Story
文件完整性和管理动作：.dgrs / .dgrs.g 容器
资源查找：Story UID + Kind + Local ID
```

一个 Group 可以影响“是否一起可用”，但不得合并成员 Story 的玩家实例、运行历史、重复规则、奖励收据或媒体缓存槽位。

---

## 3. WP-A：删除 Namespace，建立单一新身份模型

### 3.1 删除范围

从正式主链移除 DGR 的 Namespace policy、per-Story override、Full ID 构造与解析、命名对话框、Namespace migration／fallback、以 Namespace 和 Project Origin 判同源或冲突的代码，以及仅为这些行为存在的测试。

重点检查现有 `NamespacePolicy*`、`NamespaceMigrationPlanner`、`NamespaceProjectMigrationService`、`NamespaceProjectValidator`、`DgrResourceId` 中 `Qualify/Namespace/IsFullId`、旧 Full ID 路径编码及其所有调用者。[SRC-07]

**不要机械删坏其他概念：**C# 的 `namespace` 语法、Minecraft／其他 Mod 的注册名（如 `minecraft:stone`）、实体 UUID、媒体内容 hash、协议类型名不是本次删除对象。`ProjectOrigin` 若只有旧身份用途则删；纯 UI 本地路径记录不必与它绑定。

若 `NamespaceFileTransaction` 内含可复用的通用文件原子写入逻辑，可以剥离为普通 IO 事务后复用；**不得为了复用 IO 保留 Namespace 业务类、规则或适配层**。同步更新调用和测试。

### 3.2 旧数据政策

**不保留旧 Namespace 格式兼容，不提供自动升级，不编写本版旧数据转换功能。**

当前 project／resource／package／network／SavedData 需要有明确的当前协议标记。遇到旧格式，给出一次清楚的“不支持此测试版旧格式”诊断，停止解析；这不等于保留一套旧解析器。不能失败后当空项目保存、自动重置 UID、猜旧映射、清空世界。

更新仓库自有测试 fixture、示例、schema 和生成器到新格式。开发测试用新隔离工程／世界，**破坏格式兼容不等于授权删除用户 Data、媒体、存档或其他 Mod 数据**。不要求保住旧进度，也不以此为由增加永久迁移代码。

### 3.3 Story UID

创建 Story 时自动生成；Inspector 只读显示并提供复制。无编辑、重新生成、恢复旧 UID、隐形 UUID＋可见 Code 双身份。

工程默认：采用 `ST-XXXX-XXXX-XXXX-XXXX`，16 个数据字符，字符集 `23456789ABCDEFGHJKLMNPQRSTUVWXYZ`，排除 `0/O/1/I`；该字符集为 32 个字符，数据空间为 80 bit。使用加密随机源并检查当前可见身份冲突，碰撞重抽。具体前缀／长度若在 P0 调整，必须同步两端、schema、示例和测试，不能保留两套权威 UID。

| 操作 | UID 行为 |
|---|---|
| 改名称、改内容、移动 Project、改 Group 名、打包／解包 | 不变。 |
| 复制整个 Project／Studio 目录，换电脑 | 保留，不猜是否独立作品。 |
| 新建 Story／作为新本地内容 Import | 为新 Story 生成 UID。不是修改原 Story UID。 |
| A→已有 B 内容迁移 | A、B 的 UID 都不变。 |
| Reference | 原 UID 保留，只读。 |
| 同名不同 UID | 可并存；用 Story UID 辨识，不能把名称当冲突键。 |

### 3.4 资源身份、所有权与范围

所有仍在正式可编辑模型中的 Story-owned 类型一并覆盖：Actor／角色组、Item、Item Group、Session、Task，以及仍保留的 Dialogue／Quest 等。不要为了表面覆盖去恢复已经退场的旧编辑器；P0 逐种记录“继续存在且已改造”或“已无正式入口且移除残留”。

每个资源恰有一个 Owner Story。其他 Story 使用它就是 Reference，即使双方同处一个 Group。Actor 个体／群体在当前模型中若同属一个 Kind，须在该共同 Kind 范围内判重，不凭显示类别建立第二套身份。

```text
Story A / Actor / r17
Story B / Actor / r17    → 合法，不是同一资源
Story A / Item  / r17    → Kind 不同，可合法区分
```

工程默认：用一个强类型 ResourceAddress 贯通内存、持久化与网络。结构化字段至少能无歧义表达 `story_uid/kind/local_id`；固定类型字段可以隐含 kind，但不能丢 owner。只保留一种新序列化合同，不支持旧 `namespace:local` 回退。

Local ID 自动分配、名称修改和排序不变。跨 Story 复制后改 owner；在目标资源作用域发生碰撞时给**新副本**分配 ID。节点、动态端口、页、选项等技术 ID 也由 Studio 按各自真实作用域管理。

### 3.5 必须贯通的调用链

不能只隐藏输入框。至少检查：

- 资源创建、保存、目录路径、列表、选择器、拖放、搜索、使用位置、剪贴板、Undo／Redo、显示顺序和布局。
- speaker／portrait／voice、目标／奖励、聚合资源、公开边界、动态文本中的类型化资源引用；普通文本不得字符串替换。
- Export／Import／Reference catalog、依赖闭包、Manifest、两端校验器和 codec。
- Runtime Story／Session／Task resolver、Actor／Item 实体绑定、Nominator／Copier／Storage Box、任务呈现和管理诊断。
- 玩家状态、完成历史、奖励收据与网络请求中出现资源地址的位置。身份类型改变不允许串玩家、重复奖励或清掉无关记录。

资源名继续是“资源名称”；不改 MC 原始物品／实体显示名。普通 UI 不暴露 Local ID。技术日志可按需包含完整地址排错，但不得恢复面向作者的 ID 编辑工作流。

---

## 4. WP-B：故事内容迁移（追加批量复制）

### 4.1 固定产品语义

```text
输入：源 Story A；可写目标 Story B（允许非空，允许已参与 Group）。
结果：B 原有内容 + A 的自有资源和内部图副本。
保持：A 不变；B 原有内容不变；A/B 的 UID 不变。
不做：覆盖 B、清空 B、复制 A 的项目级外部连线、修改其他 Story 来指向 B。
```

不要求先新建空 B，不强制用户改用“迁移为新 Story”，不提供“是否复制外部连线”的选择。UI 使用既有目标 Story 选择器和一次执行动作即可。目标只读、源目标相同、源数据无法读取等基本错误可以拒绝；“B 有剧情／节点”不是拒绝理由。

### 4.2 复制与保留表

| 对象 | 行为 |
|---|---|
| A 的 Story 内部普通节点、节点间 Flow／Logic、属性 | 复制并追加，内部引用重映射。 |
| A 拥有的 Actor／Item／Item Group／Session／Task 等定义 | 全量复制，包括暂未使用的自有资源。不是只复制可达部分。 |
| 被复制 Session／Task 内部图、page／option、动态端口、子图／组合元数据 | 作为对应定义内容复制，保持内部关系。 |
| A 引用的外部资源 | 保持其原 owner 地址，补齐 B 所需引用声明／provider；不把它偷偷变成 Owned。 |
| A 的自有资源之间的引用 | 指向 B 下本次生成的相应副本。 |
| 媒体内容与引用 | 保留内容寻址引用并确保目标可用；允许底层去重，不依赖 A 永久存在。 |
| A 的编辑布局、注释框、列表顺序 | 复制相关部分；B 原布局／排序保留，新内容追加或统一偏移以便看见。 |
| B 的 UID、名称及现有根级元数据 | 保留；不把源 Story 外壳身份覆盖进 B。 |
| B 原有的跨 Story 公开端口与外部连线 | 保留原身份，不因迁移换号或重连。 |
| A 在项目 Story Graph 上的跨 Story 线 | 一律不复制，包括源的 Story 级自循环；不询问。 |
| 真实 MC 实体／物品绑定、玩家实例、任务历史与奖励收据 | 不复制。这些不是作者定义。 |

复制的角色／物品获得目标作用域身份后，不会自动拥有源世界的 NPC／Item 绑定；继续通过既有指名器处理。不得复制唯一实体身份来“还原运行效果”。

### 4.3 ID 重映射：只处理副本，不碰目标原数据

先为全部复制对象建立映射，再重写所有副本，最后一起提交。映射键必须含作用域，不能只用一个字符串 ID 做全局字典。

```text
(A UID, resource kind, source local id) → (B UID, same kind, target local id)
(source graph, source node id)         → (target graph, copied node id)
(source node, dynamic port id)         → (copied node, copied dynamic port id)
```

同一次复制中，十个节点引用同一个源资源，应共同指向同一个新副本，不能复制十份。重复执行迁移是新的批量复制，不将第二次操作合并到第一次副本。

**固定端口不是任意随机 ID。**例如 `flow_in`、`flow_out` 在不同节点内重复通常合法，且可能由节点 schema 规定。保留固定角色名，映射所属节点；动态公开边界／选项条件端口等按各自作用域 remap。不要全局替换 `flow_in` 导致节点 shape 失效。[SRC-04]

选项／页、奖励条目、图层连续性标记等按真实语义处理：独立副本必要时使用新身份，但同一复制域内的对应关系保留；媒体内容 hash 不是待随机的身份。自动去重只适用于内容存储，不按同名合并作者资源。

### 4.4 结构合法性与复制不是一回事

**已知源码限制：**当前 Story Start 唯一，直接把两个非空 Story 的所有节点追加后可能出现多 Start。这不是普通 ID 冲突，改 ID 不会消除结构约束。[SRC-04]

工程默认处理：不据此拒绝非空目标，不静默丢弃源 Start，不覆盖目标 Start，不自动拼接两条剧情。先完整保留为可编辑草稿，在普通 Problems 中报告结构问题；导出／Runtime 仍执行正常合法性检查，不自动运行多个 Start。若现有保存链禁止此类草稿，需拆开“可保存的编辑数据”与“可运行结构校验”；允许用户清理多余副本，并保证最后一个必需节点受保护。

这一段是无损复制和现有节点约束之间的工程边界，**不是要求新增多 Start 运行机制**。P0 必须实测该路径，不能只做资源 ID 测试就宣称迁移全通过。源内合法 Session／Task 作为新资源复制时不与 B 原资源图揉成一个图，避免人为制造它们内部的唯一节点冲突。

### 4.5 原子性、撤销和可见反馈

采用当前源编辑快照，包括已打开文档的未保存内容；未打开资源从项目快照加载。记录源／目标编辑代次，提交前变化则重算／受控拒绝，不覆盖后来编辑。

一次迁移是一个整体编辑动作，Undo／Redo 整体还原本次新增内容和引用；不删除 B 先前资源、不移动 A。失败不得留下半套文件、孤儿 membership 或已分配但缺失的副本。目标已有普通结构警告不应被当作事务 I/O 失败；缺定义、地址无法解析等数据损坏仍需精确诊断。

完成后给出复制数量并定位新增内容；不为每个自动改 ID 弹提示，不要求作者批准技术映射。

---

## 5. WP-C：自动 Story Group

### 5.1 生成规则

Story Group 是 Story-level Flow＋Logic 图的**弱连通分量**：计算成员时忽略方向，运行和预热时保留方向。只算实际声明的结构边，不能根据某个玩家此刻 Logic=True／False 动态分组。

```text
A ─Flow→ B ←Logic─ C     D
→ Group {A,B,C}          独立 Story D
```

只读外部 Group 的成员和内部边来自其固定快照。本地新增跨 Story 边与已装载来源的内部边合并为同一关系视图；参见第 7 节的完整来源约束。普通资源 Reference、显示分组、注释框、媒体共用不构成 Story Group。

至少两个 Story 才显示为 Group。单 Story 自循环仍是单 Story；分组算法必须处理循环、并行边和重复访问，但不能据此放松既有运行图合法性规则。使用现有连接结构，不因类名包含 Logic 而漏掉 Flow。

### 5.2 自动合并与拆分

不提供手工“创建组／加入组／移出组”及成员勾选表。加线、删线、删 Story、撤销、重做后统一由当前关系快照重新计算；若仍有其他连通路径，删除一条线不能错误拆组。

分组、导出预检、只读 Graph 和媒体遍历使用同一份 edge revision；成员集合和导出内容须来自同一快照，避免边在后台变化后把旧成员和新连线打进同一容器。

工程约束：O(V+E) 的分量计算足够，按图 revision 失效；不要每帧、hover 或树展开时重扫磁盘。

### 5.3 Group 名称

默认 `故事组合（1）`、`故事组合（2）`……，允许重命名。它只用于 Studio／包管理器显示和默认导出文件名，不是 UID。

| 变化 | 名称行为 |
|---|---|
| 成员不变，仅内容／边结构变化 | 保留名称。 |
| 一个既有 Group 吸收独立 Story | 保留原名。 |
| 两个或更多既有 Group 合并 | 使用新的默认名，不选择谁继承。 |
| 一个 Group 拆分 | 每个新多 Story 分量使用新的默认名；单 Story 恢复自己的名称。 |
| 用户 Undo／Redo 此次编辑 | 恢复对应编辑快照的组名，不重新猜测。 |

内部可用 `Hash(sorted(Story UID set))` 作为派生 GroupKey，供显示元数据查找。不能成为运行身份、冲突裁决键或长期随机 Group UID。仅保留当前组的正常 metadata；Undo 历史可保留旧快照，但不能因旧 key 还在磁盘就随意复活历史名字。

### 5.4 Studio 界面

故事库按独立 Story 和可展开 Group 展示；成员还是原 Story，点击仍进入该 Story 工作区。组标题可重命名、导出，不能改变所有权或手工移动成员。

Project Graph 可用轻量背景框和组名提示关系，不能遮挡节点、抢拖线手势、把 Group 变成有新端口的流程节点。Reference Group 明确只读来源，引用资源仍使用现有选择器。

现有主题、字段字号、紧凑列表和折叠规范继续有效；不要把每个单行条目做成巨大卡片。[SRC-05]

---

## 6. WP-D：`.dgrs` 与 `.dgrs.g` 文件合同

### 6.1 文件语义

- `.dgrs`：恰好一个 Story。
- `.dgrs.g`：一个完整联动 Group，含多个 Story 及完整组内 Story Graph。
- 本版两者都只认当前 Story UID／ResourceAddress 格式；保留 `.dgrs` 扩展名不等于支持 0335 的旧内部格式。

加载器先识别最长后缀 `.dgrs.g`，再识别 `.dgrs`；不能仅用最后一个扩展名 `.g` 或漏扫双后缀文件。真实格式同时由 Manifest 验证，不只相信文件名。

### 6.2 扁平容器，复用 Story 载荷

工程建议结构如下，目录和字段名在 P0 写入两端共用合同；它不是已经存在的文件布局。

```text
王国主线.dgrs.g
├ manifest.json
├ story_graph.json
├ stories/
│  ├ <StoryUID-A>/
│  │  ├ story.json
│  │  ├ membership.json
│  │  └ resources/<kind>/<local_id>.json
│  └ <StoryUID-B>/...
├ referenced_resources/<owner_uid>/<kind>/<local_id>.json
└ media/<content-hash>.<ext>
```

单 Story `.dgrs` 使用同一种 Story 载荷及地址编码，仅外层合同限制一个成员。Group 不递归嵌套其他 Group，也不把若干压缩 `.dgrs` 再压一遍；相同媒体内容可共享一份字节。

一个小的外层 Container Reader 读取并验证后，交出多个与独立包相同的 Story 描述，以及 `Story UID → 来源容器` 管理索引；不另造 Group 执行器。原子加载、来源标记、租约和管理状态属于必要 IO／管理层职责，并非“零代码改动”。

### 6.3 Manifest 至少表达的内容

| 信息 | 用途与限制 |
|---|---|
| format／当前 schema version／producer version | 严格辨识当前协议；拒绝旧格式与未知字段。 |
| container kind：single／group | 与扩展名及成员数量交叉校验。 |
| display name | 单 Story 名或 Group 名，只展示。 |
| member Story UID 列表 | 明确哪些 Story 实际作为可执行成员安装；禁止重复。 |
| 每 Story 的定义、membership、资源索引位置 | 必须完整对应，不能夹带未声明可执行 Story。 |
| 每 Story 的内容 fingerprint | Reader 重新计算／验证，不能盲信作者填写值。 |
| 完整组内 Graph 位置 | 所有 source／target 指向成员与有效公开端口。 |
| 引用资源声明及来源地址／内容校验信息 | 和可执行成员严格分开。 |
| 媒体 entry、类型、hash、长度 | 原始数据完整性与按需读取。 |
| 可选导出时间 | 仅展示；不进入成员内容 fingerprint。 |

不新增用户管理的 Group UID／Package UID。文件清单的内部 handle、扫描 revision、容器字节 hash 可以存在，不能越权成为 Story 身份。文件改名不使 Story 换身份。

### 6.4 导出规则

在 A→B→C 的任一成员点击导出，均导出完整 `.dgrs.g`。导出界面说明“本次包含 3 个 Story”，不提供“仅导出当前成员”的捷径。独立 Story 输出 `.dgrs`。Group 的默认文件名取 Group 名，做安全化；同名文件遵守正常目标路径选择，不按名字决定故事身份。

导出从一致编辑快照出发：计算成员／来源闭包 → 收集每成员自有资源和实际需要的引用资源闭包 → 校验地址和公开端口 → 计算成员指纹 → 写临时文件 → 完整校验 → 原子提交到用户选定目标。失败不得破坏上次导出的完整文件，也不得先清空用户输出目录。

分组是“联动发布必须一起”，不是跨版本自动更新协议。增删线后旧文件和新文件同在安装目录，按冲突规则处理；不自动删除、替换旧容器。

### 6.5 All-or-Nothing 校验

任何成员缺失、成员 UID 重复、资源索引不匹配、无法解析的定义、媒体完整性错误、组图指向不存在的成员／端口，都使当前容器不可发布为可运行定义。不能先注册 A，再读 B 失败后只撤一部分。

Reader 可以返回诊断用的可信 Manifest／部分目录信息供 UI 展示；**部分可显示不等于部分可运行**。非法包列为 Error，不从管理器中消失。

安全边界复用现有 archive 工具：拒绝路径穿越、绝对路径、重复规范化 entry、链接逃逸及声明／实际大小不符；在 P0 审计现有 entry、数量、解压大小、协议字节上限，保留有界处理。超过上限明确报错，不静默少加载几个 Story。不要用 Group 功能顺便解除旧安全上限。

---

## 7. Reference／Import：保留既有能力，完整来源不可拆

### 7.1 必须分开的三个概念

```text
加入来源库：为了读取资源，需要完整取得哪个 .dgrs / .dgrs.g？
引用一个资源：当前 Story 实际使用哪个 ResourceAddress？
参与 Story Graph：哪些 Story 之间存在真正 Flow / Logic 边？
```

三者不是一回事。把 Group 加入 Reference 库，不等于本地 Story 启动了所有成员；引用一个 NPC，也不等于那个 NPC 的 Owner Story 成为本地 Group 成员。

### 7.2 只读 Reference

独立 Story 以整个 `.dgrs` 加入；Group 以整个 `.dgrs.g` 加入，不允许从 Group 中单独截出一个成员作为独立 provider。来源包、原 UID、自有资源归属和来源内部 Graph 保持只读。

在资源库中展开 `来源 Group → Story → Resource`，继续选择具体 Actor／Item／Session／Task 等。使用位置、搜索、预览和类型校验照常工作；没有新的 Namespace、没有强制 Import 提示、没有为了引用 NPC 自动纳入它的整个剧情。

Reference 更新按完整输入文件重新校验；本地改图不能改写外部来源字节。同 UID 集合的来源新快照需经过正常明确刷新／校验，不能悄悄按文件名切换到另一个身份；资源消失变为缺失引用，不用同名资源顶替。

### 7.3 既有跨 Story 引用／连线能力

基线已经允许引用包 Story 参与公开边界验证。[SRC-03] **不得因本版引入 Group 就关闭该能力，也不得统一要求先 Import 为 Owned。**来源 Story 的定义不可编辑，但本地项目持有的连线可以按照原有边界规则连接这些只读 Story。

工程默认的完整性处理：

- 只引用 `G/A/NPC`：G 完整存在于 Reference 库；NPC 是资源依赖，G 不因此加入本地可执行 Group。
- 本地 Story X 与 `G/A` 建立 Story-level Flow／Logic：导出联动集合时包含 X 和 G 的完整成员／内部图。来源 G 仍只读、UID 不变，外层可以生成一个新的扁平 Group 容器；不能仅摘 A。
- 连接两个外部 Group：按同样规则收齐两边完整来源与本地边，去重相同 Story 身份；若同 UID 的来源定义本身歧义，报告问题，不能挑一个覆盖。
- 新容器与原 G 一起安装会出现成员 UID 重叠，按第 8 节双方 Conflict；不增加服务器自动替换机制来掩盖这一点。

这只是“完整 Reference 来源＋联动整体打包”的实施闭包，**不是增加可编辑外部内容或跨 Project 同步服务**。不得为了满足闭包而复制修改来源 UID，亦不得把普通资源依赖加入 Story 连通算法。

### 7.4 导出引用资源与运行时身份

保留当前资源引用的可用性目标：导出收集真正使用的引用定义及必要的传递依赖／媒体，保持原 ResourceAddress，不把 Owner Story 的可执行根顺手注册进来。引用未解析应明确报错，不能导出一份暗中依赖本地 Reference 目录路径的包。

Manifest 的 `member Story UID` 与 `referenced_resources` 必须分开：多个内容包引用同一个 NPC，不是“重复安装了 NPC 所属 Story”。UID 冲突检查只统计实际成员声明，而非每个资源地址中出现过的 Story UID。

相同 ResourceAddress 的相同定义可以共享；如果可用候选中同一地址有不同定义版本，不能按读取顺序覆盖。工程默认沿用保守的引用内容一致性验证，报告依赖内容不一致，对受影响内容阻断；这类 Error 与重复可执行 Story 的 Conflict 分开。不为此增加版本求解器或自动升级 provider。

### 7.5 可编辑 Import

独立 `.dgrs` Import：创建新本地 Story，再复制定义；原文件和 UID 不变，新对象在创建时有新 UID。

Group `.dgrs.g` Import：整体读取，先创建所有目标 Story 与 UID 映射，再复制各自资源和**导入集合内部的 Story Graph**；保留对应关系、可保留组名。外部资源依赖不自动改 owner。不得拆出一半成功、一半失败。

与 A→已有 B 的“故事内容迁移”严格区分：后者不复制源的项目级 Story 外部连线；前者为了完整导入一个 Group，保留其内部 Story 图。两者可以复用类型化复制引擎，但调用边界和根对象策略不同，不给用户增加“要不要复制连线”的迁移选项。

---

## 8. WP-E：清单、UID 冲突与错误隔离

### 8.1 先有完整清单，再决定哪些能运行

维护轻量的已发现文件清单，每个物理来源均有自己的管理行：有效启用、用户禁用、Conflict、Error 都要保留。不再只用 `Map<StoryUID, LoadedStory>` 代表安装目录，否则第二份重复 UID 会在扫描阶段被吞掉，UI 永远看不见双方。

至少分开：

```text
Inventory：目录内全部受管理来源与诊断
Validated candidates：验证通过的 Story 定义及其容器来源
Effective runtime view：允许新启动的定义／图
Existing runs：已获准实例及必要的原定义租约
```

这是加载和管理层的数据职责，不是新增一套 Group 状态机。临时导出文件、缓存文件不在受管理安装输入内。

### 8.2 UID 冲突范围

**全部已安装包参加检查，包括用户手动禁用的包。**按实际成员 Story UID 建反向索引，任一 UID 被多个物理容器声明，就把所有相关容器标为 Conflict。一个 Group 中任一成员冲突，整 Group 被阻断。

| 安装情况 | 结果 |
|---|---|
| `A.dgrs` 与 `A-copy.dgrs` 同 UID，即使字节完全相同 | 双方 Conflict。 |
| Group `{A,B}` 与独立 `{B}` | 两个容器均 Conflict，A 也不可作为该组成员运行。 |
| Group `{A,B}` 与 Group `{B,C}` | 双方 Conflict，不保留各自“不重复”的部分。 |
| `{A,B}`、`{B,C}`、`{C,D}` | 三个容器都标记，不因前一容器先被禁用而让后一容器漏检。 |
| 活跃 A 与手动禁用旧 A | 仍是双方 Conflict；灰色禁用不是解决冲突。 |
| 两个 Story 同名、UID 不同 | 不是 UID 冲突；名称仅展示。 |
| 两个包仅引用同一个外部资源 | 不是重复 Story 安装，按依赖一致性单独验证。 |

必须先对同一份完整清单判定，不能边枚举边把第一个当 winner；结果不依赖文件排序、包名、时间、指纹大小或谁先加载。

### 8.3 错误包与可知身份

能严格读取 Manifest 成员列表但内容有错的包仍保留其可知身份，避免坏内容故意绕过重复身份诊断。若整个 Manifest 无法信任／解析，列 Error 并说明无法确定成员；不得由文件名猜 UID。异常容器本身永不运行。

同一行可同时有 Error 与 UID overlap 的具体诊断；核心展示按严重错误优先，但所有已知冲突来源仍可追踪。全扫描失败（目录不可读）不能误当“目录为空”然后注销所有 Story，须报告本次扫描失败。

### 8.4 系统不作保留决策

Conflict 详情列出重复 Story 名称、UID、全部来源容器和被阻止范围。没有自动替换、保留新版、覆盖、智能合并、自动改 UID、删除冲突方等按钮。

用户可以在普通管理器执行明确的删除操作，或自己调整文件、回 Studio 做内容迁移；删除针对用户选中的文件，绝不是软件从冲突中自动选择一方。解决后通过现有手动 `/dgr reload` 或面板“重新扫描／加载”刷新。不另加文件监视器和到处自动 reload。

### 8.5 冲突不能回退到旧运行快照继续执行

基线 loader 的“坏候选保留旧快照”不能把新检测到的 UID Conflict 屏蔽。冲突出现后，即使旧 Story 定义仍保留在内存／缓存，也不得作为获准运行的 fallback。

冲突容器的全部 Story 取消新启动并停止活动执行；未冲突的其他容器继续正常。引用依赖因失去合法来源而无法解析的内容显示对应依赖 Error，不伪称它也有 UID 重复。不把一次局部冲突做成全服务器所有故事失效。

---

## 9. 启用、禁用、Conflict、Error 与删除的生命周期

### 9.1 用户意图与有效状态分开

保存用户的 Enabled／Disabled 设置；Conflict／Error 为扫描和校验派生状态，不把用户设置偷偷改成永久 Disabled。

```text
允许新 Story 启动 = 用户启用 && 容器有效 && 无身份冲突 && 所需依赖可解析
```

清除冲突后恢复原用户意图：原先 Enabled 的恢复启动资格，原先 Disabled 的仍禁用。**恢复资格不等于自动启动剧情或重放已取消的步骤。**

管理设置只属于服务器本地配置，不写进发布包，也不由客户端宣称。工程默认：内部按准确来源记录设置，校验成员集合；文件只改名且唯一对应原成员集合时可保留设置，若无法唯一匹配不得拿一个旧设置随机覆盖多个来源。这个管理映射不是新的公开 Group UID。

### 9.2 普通手动禁用：不再新启动，已有实例可以完成

用户已接受该语义。对于成员 A／B／C 的 Group，关闭整包所有成员的新启动资格，但已获准的 Story 实例继续走原执行链；不新增暂停／恢复系统，不强制终止当前对话和任务。

工程约束：

- 手动入口、NPC 交互、Flow 驱动、Logic 驱动、冷却／重复和其他正式入口，都必须走统一 Story 新启动检查；不能某个入口绕过 Disabled。
- 已有 Story 中继续 Session、Task、目标和奖励是同一已获准 Story 运行，不等于新开一个 Story，不被误拦。
- 跨 Story A→B 若需要创建新的 B，而 B 所属容器已禁用，则不允许新开 B；A 已运行不意味着整个 Group 都获得新启动许可。
- 既有 B 实例恢复／继续与 NEW／RESTART 区分；断线重连、服务端重启从可验证的同一已有运行状态恢复，不能通过恢复接口偷偷新建实例。
- 未完成实例可以长期存在，不加自动超时／自动成功。实例引用的定义和媒体需保留到实际结束，再释放租约。

UI 主状态仍灰色“已禁用”，有活动实例时加小字“禁止新启动，仍有 N 个实例运行”；不要用绿色暗示仍允许新启动。

### 9.3 Conflict／Error：不是温和禁用

重复 UID 要求双方不运行，优先于普通禁用的“已有实例可完成”。当运行中检测到冲突或当前容器确定无效时，统一撤销相关 Story 的执行资格，并在服务端安全边界终止／退役活动实例，关闭相应会话、标题、追踪和播放引用。不能让旧任务监听器继续计数、旧请求继续发奖、旧 continuation 后续又启动成员。

通过既有取消／retire 链路处理，不新造 Group 进度。保留已经完成的历史与防重复奖励收据；不以“停止执行”为理由删除整个玩家世界数据。迟到网络包／异步媒体结果均失效。冲突消失后不自动重放已执行副作用。

当前来源无法完整验证时不允许只运行它的一部分；也不得在 UI 显示 Error 而后台继续把它当正常启用。内存中留着文件字节用于回收／诊断，不代表能继续执行。

### 9.4 启用动作

普通 Enable 只切换用户意图并重新检查资格，不绕过 Error／Conflict，也不直接执行 Start。冲突行不能提供“强制启用”。群组动作覆盖完整容器，不提供成员级的启用开关。

### 9.5 删除动作

管理器可删除用户明确选择的独立 `.dgrs` 或整个 `.dgrs.g`，不能删除组内单个成员。删除是破坏性文件动作，应有一次常规确认，列出文件和成员数量；这与迁移不询问连线、冲突不提供替换向导无关。

工程默认：删除使整个容器停止运行；有活动实例时确认文案说明影响。服务端核验权限、来源 handle、当前 revision 和允许的文件范围；先安全关闭执行／读租约，再按当前文件系统要求完成删除。失败报告实际结果，不能先显示已删除成功。不得删除包内资源所对应的 MC 实体、用户 Project、其他容器媒体或持久完成历史。

目录扫描结果、定义视图、运行阻断和客户端显示应在同一服务端提交边界切换；耗时 IO 可在旁路完成，不能在渲染线程展开／验证 archive。

---

## 10. WP-F：Minecraft 故事包管理器 UI

### 10.1 入口与权限

默认 **O 键**呼出，只给 OP 使用。使用现有按键注册／网络／窗口基础；键位可通过标准设置更改，不重新做快捷键系统。

按键只在正常游戏上下文处理；聊天、输入法组合、其他文本框输入 O 时不能抢焦点。按住按键不反复创建窗口。单人集成服务器与专用服务器均按服务端权限校验。

客户端可以缓存权限以隐藏无效入口，但服务器必须验证打开、列表、详情、Graph、启停、删除和重新扫描的每一类请求。权限撤销、换服、断线后关闭／清理管理上下文；伪造 packet 不得访问管理数据或写操作。

O 键是主要 UI 入口，不能交付成只有 `/dgr packages` 或聊天报错。无 GUI 的服务器控制台可以复用同一服务提供等价查询／管理入口，命令命名属于工程细节，不替代 O 键交付。

### 10.2 布局

延续现有 Minecraft DGR 界面风格，做一个可调整窗口内的管理面板，不照搬 WPF Inspector，也不新建桌面式控制平台。

```text
故事包管理                              [重新扫描／加载] [关闭]
已启用 4    已禁用 1    冲突 2    错误 1

┌ 已安装故事包 ──────────────┬ 详情 ───────────────────────────┐
│ ✓ 酒馆风波      已启用     │ 当前 Story／Group 的对应信息     │
│ ✓ ▾ 王国主线    已启用     │                                  │
│       王国密令            │ Group 才有只读 Story Graph        │
│       王城事件            │                                  │
│ ○ 北境故事      已禁用     │ Conflict／Error 有明确原因        │
│ ⚠ 旧版密令      冲突       │                                  │
│ ✕ 损坏故事组    错误       │ [启用／禁用] [删除…]              │
└──────────────────────────┴──────────────────────────────────┘
```

显示名字优先，文件名放次级行／详情，避免树上同时堆满 GroupKey、PackageID、UID 和路径。独立 Story 一行即可；Group 展开的是 Story，不是多个可分别控制的 Story Package。

### 10.3 状态视觉合同

| 主要状态 | 主色 | 图形／文字 | 行表现 |
|---|---|---|---|
| 已启用 | 绿色 | 勾／实心状态标记＋“已启用” | 正常名称字重，可辨识为可用。 |
| 已禁用 | 灰色 | 空心标记＋“已禁用” | 减弱亮度但保证可读；活动实例用小字说明。 |
| 冲突 | 红色 | **带感叹号的警告三角形**＋“UID 冲突” | 状态和重点名称加粗，可用淡红底／边线强调。 |
| 错误 | 红橙／橙色 | 错误叉／独立错误标记＋“错误” | 明确是文件／结构／依赖错误，不写成 UID 冲突。 |

必须同时依赖颜色、图形、文字，而不只靠色差。列表行高与字号稳定；不能因为冲突就把每行放大到列表跳动、截字。详情的错误标题可以更大、更粗。主题和 GUI Scale 下保证对比度。

Minecraft 字体不一定包含 `⚠`／`✓`，实际实现可绘制原生几何／纹理图标，不能交付缺字方框。保持点击区域和缩放坐标一致。

### 10.4 Story 详情

点击独立 Story 或 Group 成员，显示 Story 名称、可复制 UID、所属容器、继承的有效状态、启动规则、资源／外部引用概况和必要版本信息。

组内成员不得出现独立启停／删除按钮。可以提供“查看所属故事组”导航，但不要再开一个独立关系页面。Story 详情不展示整个服务器 Story Graph。

### 10.5 Group 详情与只读 Graph

显示组名、文件名、成员数、有效状态、来源文件／版本信息；内容 hash 短码为诊断信息而非 Group 身份。显示**这个容器内部**的 Story Graph：Flow 实线箭头，Logic 用不同线型／标记并给图例。

支持拖动画布、缩放和自动适配；选择成员可高亮。对于能解析但损坏的图，可用虚拟红节点呈现缺失目标用于诊断，不据此允许运行。大图采用现有布局或有界自动布局，不把全服务器故事塞入一张图。

完全只读：不能拖线、剪线、修改端口、保存节点位置到故事定义、编辑来源内容。pan／zoom 只是客户端视图状态。

### 10.6 冲突／错误详情

警告在管理器中集中呈现，不需要每次打开 O 都弹一个阻挡整个界面的模态框。顶部显示冲突／错误汇总，行内标记醒目，点行看明细。

例如：

```text
[红色警告三角] Story UID 冲突
故事：王国密令
UID：ST-K7M4-Q2PX-9D6R-W3TF
来源：王国主线.dgrs.g、旧版王国密令.dgrs
影响：两个容器内所有 Story 均被阻止运行。
请自行处理重复文件或在 Studio 中迁移内容，然后重新加载。
```

没有“保留新版／替换这些包／一键解决冲突”。普通文件删除仍可由用户主动选择，界面不得替他预选。

### 10.7 数据请求与刷新

列表、详情、Graph 均由服务端返回可信元数据。大清单分页／有界加载；点 Group 才获取必要图信息，不为打开管理器发送资源全文、媒体、全玩家任务状态或完整历史。

请求带会话标记和 inventory revision；过期响应不覆盖新状态，过期删除／启停请求返回刷新提示，不盲目执行。多 OP 同时操作以服务器当前版本串行决定，按钮等待态在成功、失败、超时／断连时均可退出。

手动扫描期间标注正在检查，不闪成“0 个包”。面板重开不重复注册监听器；关闭、换服后释放列表、图和回调。

---

## 11. WP-G：逐 Story Generation，不做 Group Generation

### 11.1 三种不同的键

```text
Story UID                     → 稳定身份
Story UID + ContentFingerprint → 当前 Story 定义代次
Container hash / file lease    → 当前外层文件与读取寿命
```

容器总 hash 不能充当成员 Story 的 fingerprint。Group 改名、压缩顺序、导出时间或文件名变化不应退役所有成员。新身份不读取旧 Namespace／旧 generation key 的适配映射。

### 11.2 成员 fingerprint 工程合同

P0 固定一份 C#／Java 一致的规范化算法和测试向量。建议输入：当前 Story 定义、自有资源、影响自身的引用定义内容、媒体内容 hash、与本 Story 相关的正式 Story-level 边及运行解释 schema。

排序只用于语义无序集合（成员、资源地址、无序边），不能随意排序台词、选项、奖励优先顺序等有序内容。剔除 UI 布局、导出时间、文件名、Group 名等非运行信息。不得递归把整组其他 Story 的全部 fingerprint 拼进每个成员，造成连锁换代。

工程默认：变更 A→B 的边时，端点 A／B 的相关边摘要变化；无关 C 不变化。这里只是保守判定受影响范围，不承诺任意图结构修改都可无缝保留活动游标。

### 11.3 更新、禁用、冲突不是同一事件

- Group `{A,B,C}` 只修改 B 的独立内容：A/C 定义代次不动；按既有策略处理 B 的活动状态。
- 只改 Group 名／文件名／压缩结构：不改变成员运行代次。
- 合并／拆分容器：逐 Story 比较前后有效定义；仅外层归属变化不清状态，真实边变更按端点处理。
- 手动 Disabled：不改 Story fingerprint，不因设置变化退役已获准实例。
- Conflict／Error／明确删除：资格阻断优先，不能用 fingerprint 没变为理由继续运行。

运行实例、完成历史、重复资格和奖励防重收据不是一锅数据。任何退役动作要精确记录范围；不把“定义换代”实现成清空该玩家全部任务／已领奖记录。手动内容迁移不迁移 A 的玩家进度到 B。

### 11.4 文件租约与旧异步结果

只改 B 却替换了整个外层 `.dgrs.g`，A/C 可能仍有读媒体请求。保留适当的 archive／entry 租约或经验证的内容缓存，直到真实使用者释放；不能因外层文件替换就让无关成员丢头像、断音频。

完成回调核对 server/session、Story UID、成员代次、请求版本及当前阻断状态。相同内容可复用缓存，不可因旧回调完成而复活已取消实例。该机制只维护必要读寿命，不是多版本 Group Runtime。

---

## 12. 媒体：保留 3 并发／10 Story 槽位

### 12.1 保留的行为

真正 Story Start 后才加入当前 Story 的媒体预热；沿 Flow＋Logic 的输出方向按距离加入后续 Story，去重和处理环。预热不启动剧情，不根据附近 NPC 或猜玩家选择先触发。[SRC-06]

```text
Group 按无向连通判断成员。
媒体按有向可达判断预热顺序。
二者使用同一份图，但算法目标不同。
```

同一 Group 的 A、B、C 同包，不代表 Start A 就把三个成员全部视为一个大媒体 bundle。

### 12.2 逻辑槽位

最多同时 3 个 Story 媒体 bundle 下载执行；最多 10 个 Story 媒体槽位，正在下载也占槽位。运行 Story 受保护，全部保护时后续排队，不突破上限、不阻塞剧情、不删除保护资源。不新增面向用户的缓存配额设置。[SRC-06]

“3 包／10 包”的旧术语在外层容器出现后应明确指**逻辑 Story bundle**；同步更新当前 skill／注释／测试，避免后续实现按 `.dgrs.g` 文件数计数。这是既有政策的作用域澄清，不是新的媒体政策。

### 12.3 完整就绪与共享资源

每个 Story 所需媒体全部验证后才是 bundle Ready；单项可以独立就绪并显示。不能把“Group 文件读完”当所有成员媒体已就绪。

底层媒体内容 hash 可以跨 Story 共享，但拥有者、活动下载和实际播放引用要分别计数。关闭一个成员／淘汰一个 bundle 不误删另一个需要的内容。IO、图片解码、GPU 纹理、音频句柄仍有独立有界生命周期；不把容器展开等同于全媒体常驻。

管理器列表／Graph／冲突详情和 Reference 浏览不触发剧情媒体预热。禁用后已有实例所需媒体仍受保护；Conflict 终止实例后正常释放，不一把清空所有缓存。

---

## 13. 导出、加载、操作的事务边界

### 13.1 三类事务，不引入统一大平台

| 操作 | 必须一起提交的内容 |
|---|---|
| A→B 迁移 | 新副本、资源映射、membership、相关布局与本次编辑历史。 |
| 容器导出／读取 | 全成员、Graph、索引与校验结果；不能发布部分成员。 |
| 管理器状态变化／扫描 | 当前清单、冲突结果、启动资格、受影响运行处理和发给客户端的 revision。 |

可复用现有原子文件写入、快照和服务端主线程提交，不先造通用事务引擎再找调用者。纯元数据不需要把全项目媒体复制到内存。

### 13.2 失败与竞态最低要求

导出／复制磁盘满、文件占用、源文件被改、重复操作、关闭窗口、切项目、异步晚到时，都有受控失败结果。不能半提交后显示成功，也不能拿旧“compatibility”兜底让错误内容继续跑。

完整扫描无法完成时报告失败、不把不可读目录当成空目录；一旦某个确定冲突／无效结果成立，则按规则阻止该容器，不能借“保留旧快照”绕过。未相关包不跟着全部重置。

---

## 14. 保留 0.3.3.5 功能，不借断代重做游戏逻辑

允许的是身份格式断代，不是把已经工作的功能退回早期版本。重点回归：

- 节点内／Inspector 双入口、资源拖放、Undo／Redo、组合／注释、搜索和使用位置。
- 连续台词 Enter／空句 Backspace／IME 保护、页与选项顺序、动态文本引用、原句历史、本地分屏与自动播放不代选。
- Choice 条件输入、服务端资格复核、隐藏／置灰；不得把条件修改变成重复进入节点。
- Task 目标、结算／奖励、实际提交、物品组轮播与分页、完成历史按需读取和网络预算。
- 头像／差分、voice／music、画面转场与图层连续性；媒体域和留声机独立。
- Nominator、实体／物品唯一绑定、Copier／Storage Box 的已有复制规则。
- 多玩家隔离、断线恢复、服务端权威和奖励防重。

旧测试若测试的是仍然需要的功能，改用新身份 fixture 后继续跑；只有专门验证旧 Namespace／旧格式兼容的测试才删除。不能通过删掉失败的业务测试制造“新体系全绿”。

---

## 15. 工程默认登记（不冒充额外产品要求）

| 项目 | 本文实施默认 | 不得偷换成 |
|---|---|---|
| UID 文本 | `ST-`＋16 个无易混字符；CSPRNG；两端一致。 | 隐形 UUID＋显示码双身份。 |
| 资源地址 | 强类型三元地址，类型明确时可隐含 kind。 | 旧 Full ID／名称／文件名回退。 |
| 目标非空 | 追加；副本自动 remap；保留目标外壳、原节点与原线。 | 覆盖、清空、必须新建目标。 |
| 唯一节点结构冲突 | 无损复制为草稿，普通校验与人工编辑；不新增多 Start Runtime。 | 以 ID 无冲突为由允许非法执行，或偷偷删掉节点。 |
| 迁移撤销 | 一次整体 Undo／Redo，基于原快照。 | 对资源逐个撤销导致半个迁移。 |
| GroupKey | 当前成员集合派生，仅元数据／管理辅助。 | 长期随机 Group UID、代次或冲突身份。 |
| 本地连只读 Group | 允许既有公开边界连接，导出时收齐整个来源 Group。 | 禁止引用／强制 Import／偷偷改原包。 |
| 外部资源重复定义 | 相同地址且相同定义可共享；不同定义报告依赖不一致。 | 先来先得、按最新版猜测、版本求解平台。 |
| 普通 Disabled 的已有实例 | 继续；新 Story 创建统一被拦；同实例恢复不等于新启动。 | 暂停系统、强制终止所有当前对话。 |
| Conflict／Error | 停止涉事 Story 的活动执行，完整阻断容器。 | 用旧快照继续运行，或当温和禁用允许完成。 |
| 明确删除 | 一次普通删除确认；全容器停止；安全释放读取后删除目标文件。 | 根据冲突自动选择／自动删除一方。 |
| UI 告警 | 管理器内红色三角、状态文字、详情、顶部汇总。 | 每次开窗弹模态替换向导。 |
| 设置来源定位 | 服务器本地来源记录＋成员核验；歧义不随机继承。 | 新增公开管理 UID／Project Origin。 |
| 网络和图上限 | 审计并复用现有有界预算；明确拒绝超限。 | 无限制全文推送／静默截断成员。 |

若工程默认遇到源码约束，施工者在 `Technical_Contracts.md` 写明具体矛盾与最小处理，不扩大产品范围。用户已明确的“非空追加、UID 不改、Namespace 删除”等不可因实现方便而变更。

---

## 16. 源码改动地图与 P0 必查清单

以下是本对话已定位的主要入口，不代表每行都经过本次完整审计。路径中的旧命名可能在本版重命名，重命名前记录调用关系。

### 16.1 Studio

根目录：`studio/src/`。

| 领域 | 现有入口／待定位调用者 | 必须处理 |
|---|---|---|
| 身份 | `DarkGreyRPG.Studio.Core/Identity/DgrResourceId.cs`、`Namespace*.cs` | 业务删除，通用 IO 提炼，不留旧协议读链。 |
| Story／资源生命周期 | `Graphs/Resources/CanonicalStoryLifecycleService.cs`、`CanonicalStoryResourceLifecycleService.cs`、Actor／Item lifecycle | 创建 UID／地址；批量复制与删除的所有权正确。 |
| 资源库与持久化 | `GraphResourceRepository.cs`、`CanonicalProjectGraphStore.cs`、`CanonicalStoryMembershipManifest.cs`、Actor／Item repositories | 所有 key／路径包含正确 Story scope。 |
| Story Graph | `CanonicalStoryLogicGraphRepository.cs`、`CanonicalProjectStoryGraphService.cs`、`CanonicalStoryBoundaryProjection.cs` | 沿用统一 Flow／Logic；加入分量派生；保留 Reference 端点。 |
| 节点规则 | `GraphNodeDefinitionRegistry.cs`、`GraphScopePolicy.cs`、`GraphNodeShapeValidator.cs`、`GraphDynamicPortPolicy.cs` | 区分固定／动态身份；草稿与可运行校验；不改变正式节点语义。 |
| 复制引擎 | `DarkGreyRPG.Studio/ViewModels/Graph/CanonicalGraphClipboard.cs`、`CanonicalStoryWorkspaceViewModel.Clipboard.cs` | 复用／扩展类型化 mapping，不用字符串全局替换。 |
| 打包 | `Packaging/StoryPackageManifest.cs`、`StoryPackageExporter.cs`、`DgrsPackage.cs`、`StoryPackageMedia.cs` | 当前 single 格式与外层 group container；共享 Story payload；完整校验。 |
| 离线来源 | `OfflineStoryPackageImportService.cs`、`OfflineReferencePackageService`、`OfflineProviderCatalog`、`CanonicalExternalReferenceService.cs` | 原子输入完整来源；类型化地址；区分资源引用与图端点。 |
| Studio UI | `ProjectGraphViewModel.Canonical.cs`、`ProjectGraphView.*`、`CanonicalStoryWorkspaceViewModel*`、`CanonicalStoryWorkspaceView*`、身份对话框 | UID 只读、迁移目标选择、Group 树／背景、移除 Namespace UI。 |
| 引用与演出 | `DynamicContentText`、使用位置索引、媒体引用／画面连续性相关模型 | 副本内部引用一致；同名不混；不改媒体 hash。 |

### 16.2 Java Runtime

根目录：`src/main/java/darkgrey/rpg/`。

| 领域 | 现有入口 | 必须处理 |
|---|---|---|
| 包读取 | `project/packages/StoryPackageManifest.java`、`DgrsArchiveReader.java`、`StoryPackageSnapshotReader.java`、`LoadedStoryPackage.java` | 外层容器与成员描述分离，只读当前协议。 |
| 扫描／合并 | `StoryPackageLoader.java`、`StoryPackageSnapshotMerger.java`、`StoryPackageRuntimeReloader.java` | 完整 Inventory、全量冲突对称阻断、无关包隔离。 |
| 图校验 | `graph/canonical/CanonicalStoryLogicGraphLoader.java`、`CanonicalStoryLogicConnection.java` | Flow／Logic 同一真相，成员闭包、端口合同。 |
| 身份与绑定 | `identity/*`、`item/identity/*`、Nominator／creator 相关 resolver | ResourceAddress、MC 实体身份不复制、业务 ID 与 vanilla registry ID 分开。 |
| 代次 | `PackageGenerationKey.java`、`StoryPackageGeneration*.java`、`StoryPackageContentFingerprint.java`、`DgrsGenerationStore.java` | 逐 Story 比较；外层文件租约不变成成员代次。 |
| 启动与活动实例 | `story/canonical/forge/CanonicalStoryForgeManager.java`、Story／Session／Task 的 instance／persistence／event 层 | 统一 admission gate；普通禁用继续，Conflict 停止；副作用防重。 |
| 媒体 | `media/StoryMediaServer.java`、`StoryMediaCacheIndex.java`、`CanonicalMediaServer.java`、`CanonicalMediaClient.java` | 单 Story descriptors、3/10、租约、跨容器共享。 |
| 管理 UI | `client` 按键、`client/gui`、`network` 现有协议基础；`command/CommandDarkGreyRpg.java` | 新 O 面板＋服务端管理服务，不用聊天输出来替代。 |

### 16.3 P0 输出必须回答的工程问题

这些是代码盘点任务，不是重新把产品问题抛回给用户：

1. 当前所有 DGR Namespace／Full ID／Project Origin 读写点在哪里；哪些通用 IO 要保留，如何删除业务规则？
2. 每类身份真实作用域是什么；固定端口、动态端口、Option／Page／Layer continuity 如何映射？
3. Story 原子批量复制使用哪个编辑历史入口；当前 Start 唯一与草稿持久化如何分离而不丢节点？
4. `.dgrs` 与 `.dgrs.g` 的精确 Manifest／entry 布局、ResourceAddress 字段、当前 schema／network 标记及拒旧格式规则是什么？
5. 统一 Story Graph 如何纳入完整 Reference Group 内部边和本地边；导出闭包如何保证不漏成员也不把普通资源引用算成成员？
6. 管理清单和当前运行 registry 如何分开，避免重复包被字典覆盖？扫描失败、无效包和冲突如何不同处理？
7. 所有 NEW／RESTART 入口、已有实例继续入口、Conflict 的取消／退役入口在哪里？
8. 成员 fingerprint 的字节规范和 C#／Java 共同测试向量是什么；旧外层文件租约由谁持有？
9. O 键、OP 读写校验、网络分页／字节预算、文件 handle 校验和实际发布路径在哪里？

---

## 17. 分阶段施工与完成门槛

每阶段先执行相关回归，再进入下一阶段。允许内部按模块并行，但身份／协议合同与最后集成由同一负责人维护，避免 C#／Java 两套定义各自漂移。

### P0：定位与协议合同

记录基线与本地修改；读仓库规范；完成第 16.3 节盘点。将当前格式、地址、复制映射、容器闭包、启停矩阵、错误优先级和测试向量写入 `Technical_Contracts.md`。

**门槛：**没有再讨论 UID 重生成／空目标限制／旧 Namespace 兼容／自动替换；已确认 Flow＋Logic 不另开数据源。协议明确后再修改持久化主链。

### P1：身份断代与当前格式贯通

移除 Namespace 业务，建立 StoryUid／ResourceAddress；改创建、存储、路径、引用、绑定、协议与基础单 Story 导出／加载。更新 fixture，拒绝旧格式，不支持 silent fallback。

**门槛：**新工程无需 Namespace；两个 Story 的相同 local_id 不串；新 `.dgrs` 能走正常 Runtime；仍保留功能的测试已适配而非删掉。

### P2：Story 内容追加迁移

基于 P1 完成 A→已有 B，包括非空 B、所有自有资源、内部引用、自动 ID 映射、布局、失败回滚与单次 Undo／Redo。实现第 4.4 节草稿边界，不自动改剧情。

**门槛：**源和目标原内容保持；迁移不携带源的项目级外部边；B 既有边不失效；重复迁移生成独立副本；保存重开不丢。

### P3：自动 Group 与双文件容器

实现分量与命名、Studio 呈现、`.dgrs.g` 导出／Reader、完整性校验、Reference 整包输入、Import 整组映射与成员 fingerprint。继续复用单 Story 载荷。

**门槛：**从任一联动成员导出都覆盖完整集合；Group 不是可运行新对象；资源引用不扩组；损坏一成员无法部分发布；现有 Reference 图能力保留。

### P4：服务端清单、冲突与生命周期

实现 Inventory／有效运行视图区分，全已安装集判重，包括手动 Disabled；对称 Conflict、Error、启停／删除以及已有实例的精确行为。先测服务，再接 UI。

**门槛：**文件顺序不影响结果；Conflict 不回退运行旧包；无关包不被清空；普通禁用后活动 Story 能继续但所有新启动入口都被挡。

### P5：O 键 OP 故事包管理器

实现四种视觉状态、树、Story／Group 详情、只读 Graph、整体操作、服务器权限、分页与旧响应失效。实机验证颜色、图标、字体、缩放和交互。

**门槛：**非 OP 所有入口均被服务器拒绝；面板能看到灰、红、Error 包而非只列成功加载的包；无自动替换入口；组内无独立开关。

### P6：跨层回归与开发交付

完成下方测试矩阵，使用同一份 Studio 正式导出的当前格式文件在配套客户端／服务器验证；测试禁用、冲突、引用、迁移、组更新和媒体回收。

按仓库规范更新权威 `dist`，记录产物版本、大小、SHA-256、测试范围及未覆盖项。源码完成、构建成功、实机验证、用户验收分别记录，不能相互冒充。

---

## 18. 需求追踪与测试矩阵

初始状态统一为 **NOT_RUN**。单元测试、模型探针、真实 GUI、真实 Minecraft、多玩家各自记录，不以源码断言替代实机证据。

### 18.1 身份与旧体系删除

| 编号 | 场景 | 必须结果 |
|---|---|---|
| ID-01 | 新 Project／Story／Actor／Item／Session／Task | 不询问 Namespace 或技术 ID；Story UID 自动创建。 |
| ID-02 | Story 改名、改内容、文件／Project 移动 | UID 不变；可复制但不可编辑。 |
| ID-03 | 查菜单、Inspector、命令和内部公开服务 | 没有 UID 重生成／换 UID API 或 UI。 |
| ID-04 | 整个 Project 复制到另一目录 | UID 保留；不依赖机器、账户或路径改号。 |
| ID-05 | 两 Story 含同 local_id、同名资源 | 正确隔离；角色／物品／任务不串。 |
| ID-06 | 同 Story 中跨 Kind 同 local_id | 类型化地址正确；Kind 内冲突自动避让。 |
| ID-07 | 同一资源试图拥有两个 Owner | 验证拒绝；其他 Story 必须引用原地址。 |
| ID-08 | 旧 Namespace project／package 输入 | 受控拒绝；不迁移、不开兼容解析、不当空文件写回。 |
| ID-09 | 命中 Namespace／Full ID／Origin 残留调用 | 正式路径无业务依赖；通用 IO 可独立工作。 |
| ID-10 | Minecraft registry ID、实体 UUID、媒体 hash | 仍合法，不因业务 Namespace 删除被破坏。 |
| ID-11 | 改名／重排／保存重开／选择器拖放／使用位置 | 身份稳定、位置精确、只读不变可写。 |
| ID-12 | 新 ID 的绑定、任务、奖励、网络请求和存储 roundtrip | 同地址全链一致，跨玩家不串，不丢收据。 |

### 18.2 故事内容迁移

| 编号 | 场景 | 必须结果 |
|---|---|---|
| COPY-01 | A→空 B | 全自有资源和内部图复制；A/B UID 不变。 |
| COPY-02 | A→非空 B，资源／节点有碰撞 | B 原内容完整保留；新副本自动 remap；不弹 ID 问卷。 |
| COPY-03 | B 已有外部 Flow／Logic | B 原公开端口身份、连线保持；A 外部边不复制。 |
| COPY-04 | A 多处引用同一自有资源 | 本次只建一个对应副本，全部指向它。 |
| COPY-05 | A 有外部角色／物品／Session 依赖 | 保留外部地址和只读来源，不误改为 B Owned。 |
| COPY-06 | 台词动态块、头像差分、奖励、组成员引用 | 按 typed 字段重写，不碰普通文本中相同字串。 |
| COPY-07 | 固定 `flow_in` 等重复于不同节点 | 固定角色保留；节点映射正确，不随机改坏端口。 |
| COPY-08 | 选项／页／公开端口／画面连续性 ID | 各自作用域正确，副本内部对应保持。 |
| COPY-09 | 两次迁移同一 A 到 B | 得到两批独立副本，不覆盖第一次或目标旧资源。 |
| COPY-10 | B 非空并产生多个 Start | 完整保留可编辑草稿、正常诊断；不自动运行多 Start、不丢节点。 |
| COPY-11 | 源有未保存编辑、目标操作中被修改 | 使用正确编辑快照；变化后不覆盖新编辑。 |
| COPY-12 | 中途写入失败、关闭、取消 | 原子回滚；源和 B 原内容不损坏。 |
| COPY-13 | Undo／Redo＋保存重开 | 整批副本／映射／布局一致；目标原内容不被撤掉。 |
| COPY-14 | 迁移后删除源 A，在隔离测试中运行 B | B 自有媒体／定义不依赖 A 文件；外部依赖仍明示。 |
| COPY-15 | Source readonly；目标 readonly／同一个 Story | 可读源不被写；目标只读和自复制受控拒绝。 |
| COPY-16 | MC 实体绑定／玩家记录存在 | 不复制真实实体 UID／进度／奖励收据到 B。 |

### 18.3 自动 Group、命名与文件容器

| 编号 | 场景 | 必须结果 |
|---|---|---|
| GROUP-01 | 两个独立 Story，分别添加 Flow／Logic | 两种边均形成 Group；未连接时独立。 |
| GROUP-02 | A→B←C、反向边、混合边 | 成员按无向连通；方向仍在图数据中保留。 |
| GROUP-03 | 删除桥接线／非桥接线 | 前者拆组，后者不误拆。 |
| GROUP-04 | 自循环、环、重复遍历 | 算法终止；单 Story 不变为多 Story Group；图语义校验仍有效。 |
| GROUP-05 | 扩张、两组合并、多分量拆分 | 名称遵守第 5.3 节；单 Story 不残留 Group 名。 |
| GROUP-06 | Undo／Redo 和保存重开 | 关系与对应组名快照一致，无幽灵旧名复活。 |
| GROUP-07 | 引用外部 NPC／Item／共享媒体 | 不改变可执行 Group 成员。 |
| GROUP-08 | 导出过程中图 revision 变化 | 用一致快照或重试，不混旧成员／新边。 |
| PKG-01 | 单 Story 导出 | `.dgrs`，当前格式，恰好一个执行成员。 |
| PKG-02 | 从 A/B/C 任一联动成员导出 | `.dgrs.g`，完整成员和图，无单成员导出捷径。 |
| PKG-03 | `.dgrs.g` 扫描和文件改名 | 正确识别双后缀；身份不因文件名变化。 |
| PKG-04 | 缺成员／未声明成员／无效公开端口 | 整容器 Error，无部分 Story 注册。 |
| PKG-05 | 重复成员 UID、重复规范化路径、路径穿越 | 受控拒绝；不覆盖目录外文件。 |
| PKG-06 | 媒体错 hash、超预算、巨大 entry 数 | 有界验证，明确错误，不截成员假成功。 |
| PKG-07 | 同一快照重复导出 | 规范化成员内容指纹一致；有序剧情内容不被排序。 |
| PKG-08 | 导出失败／磁盘满／输出文件占用 | 上次完整产物保留，不清用户输出目录。 |

### 18.4 Reference 与 Import

| 编号 | 场景 | 必须结果 |
|---|---|---|
| REF-01 | Reference 独立包／Group 包 | 分别完整读入单包／整个组；UID 保留、只读。 |
| REF-02 | 从 Group 选择一个成员的 NPC | 来源完整，实际引用精确到该资源，不启动整组。 |
| REF-03 | 两 Group 中相同 local_id | 按 Owner Story 区分，不按 local_id／名称串资源。 |
| REF-04 | 本地 X 通过已有公开端口连接只读 G/A | 允许既有功能；原包未改；导出闭包含整个 G。 |
| REF-05 | 联接两个 Reference Group | 完整来源与内部图保留；无双轨图或外部写入。 |
| REF-06 | 本地只引用外部资源再导出 | 仅所需定义闭包；Owner Story 不冒充可执行成员。 |
| REF-07 | 两包共享相同引用定义／不同定义 | 前者合法；后者明确依赖一致性错误，不先来先得。 |
| REF-08 | Provider 刷新、资源消失、换成不同 UID | 明确缺失／身份变化，不同名顶替，不自动改本地引用。 |
| IMP-01 | Import 单 Story | 新本地 Story 获新 UID；原包不变。 |
| IMP-02 | Import 整 Group | 所有新 UID 先映射，内部跨 Story 图完整重写。 |
| IMP-03 | Group Import 失败 | 无半个组落地；资源与布局原子。 |
| IMP-04 | 对比 Import 与 A→已有 B 内容迁移 | 前者保留导入集合内 Story 图；后者固定不带源外部边。 |

### 18.5 全量冲突、Error 与启停

| 编号 | 场景 | 必须结果 |
|---|---|---|
| CON-01 | 两独立包同 UID，交换文件名／枚举顺序 | 双方 Conflict；没有先加载 winner。 |
| CON-02 | Group↔独立 Story 重复 | 两容器所有 Story 阻断，包括组内不重复成员。 |
| CON-03 | Group↔Group 共享一个／多个 UID | 双方阻断；详情列出全部重复身份与来源。 |
| CON-04 | 三容器链式重叠 | 三方同时标记，不能因先排除一方而漏检。 |
| CON-05 | 一个重复包原本手动 Disabled | 仍参与冲突；禁用不是消冲突方法。 |
| CON-06 | 完全相同包复制第二份 | 仍 Conflict，不按字节相同自动合并安装身份。 |
| CON-07 | 同名不同 UID | 可正常存在；管理器有 UID／来源可辨认。 |
| CON-08 | 运行中放入重复包再 reload | 涉事活动执行停止，旧缓存不能绕过阻断。 |
| CON-09 | 移除一方并 reload | 原 Enabled 恢复启动资格；原 Disabled 不被自动启用。 |
| CON-10 | 无法解析 Manifest 的坏文件 | Error 行可见；不猜 UID，不注册任何 Story。 |
| CON-11 | 可读 Manifest＋坏成员，且 UID 与别包重复 | 诊断保留可知身份，不因坏内容丢失重叠信息。 |
| CON-12 | 存在冲突／坏组，另有正常独立包 | 正常包仍可用，不全局回滚成空 registry。 |
| LIFE-01 | 活动 Story 内有 Session／Task，再手动禁用 | 已有实例继续；新 Story 创建全部被挡。 |
| LIFE-02 | 禁用后 NPC、Flow、Logic、重复入口 | 统一拦 NEW／RESTART，不留一个绕过入口。 |
| LIFE-03 | 活动 A→尚未运行的禁用 B | B 不被启动；A 的旧许可不扩大为 Group 许可。 |
| LIFE-04 | 禁用后断线重连／重启恢复已有 run | 同实例可恢复；不借恢复新开 run；定义与代次可验证。 |
| LIFE-05 | Conflict 后旧点击／任务事件／奖励请求到达 | 无推进、无领奖、无重新起 Story；对应 UI 正常关闭。 |
| LIFE-06 | 删除 Group，活动读取／实例存在 | 全容器安全停止后处理文件；无成员级删除、无无关数据删除。 |
| LIFE-07 | 目录不可读、文件删除失败 | 真实错误可见；不可读不当空目录，删除失败不报成功。 |
| LIFE-08 | 手动再次 Enable | 只恢复资格，不直接启动／重播剧情。 |

### 18.6 O 键管理器与 OP 权限

| 编号 | 场景 | 必须结果 |
|---|---|---|
| UI-01 | OP 正常按 O | 打开真正故事包管理器，而非聊天输出。 |
| UI-02 | 非 OP 按 O／伪造列表和写操作 packet | 服务端全部拒绝；无管理信息或写副作用。 |
| UI-03 | 聊天／输入框／IME 中键入 O，长按 O | 不抢输入、不重复开窗。 |
| UI-04 | 同时安装启用、禁用、Conflict、Error 包 | 清单全显示；统计正确，不只列有效 registry。 |
| UI-05 | 绿色／灰色／红三角／错误叉 | 颜色＋图形＋文字一目了然，无字体缺字符方框。 |
| UI-06 | 最大／最小 GUI Scale、窄窗口、长中文名 | 排版、裁切、按钮和命中区域正常，无状态跳行。 |
| UI-07 | Group 展开、点击成员／Group 标题 | 正确详情切换；成员不能独立启停／删除。 |
| UI-08 | Group 图 pan／zoom／fit、Flow／Logic | 图例清楚，范围仅该容器；不能编辑连线或源数据。 |
| UI-09 | Conflict／Error 详情 | 原因和全部来源清楚；无自动保留／替换按钮。 |
| UI-10 | 禁用但仍有活动实例 | 灰色禁用＋活动数量解释，不误显示全已停止。 |
| UI-11 | 两 OP 并发，第二人点旧 revision 删除 | 服务器拒绝过期目标，刷新后再操作，不误删新文件。 |
| UI-12 | 打开后取消 OP、断线、换服／重开 | 清理权限与旧数据；迟到响应不覆盖新服务器。 |
| UI-13 | 大清单／大图／慢网／失败响应 | 分页有界、等待可退出，不阻塞主界面或渲染线程。 |
| UI-14 | 点普通删除并取消／确认／失败 | 一次常规确认；无自动选另一冲突方；结果真实。 |

### 18.7 代次与媒体

| 编号 | 场景 | 必须结果 |
|---|---|---|
| GEN-01 | 组 A/B/C 仅改 B 内容 | A/C 成员指纹不变，活动状态不因外层 hash 被全退。 |
| GEN-02 | 只改 Group 名／文件名／压缩顺序／导出时间 | 所有成员代次不变。 |
| GEN-03 | 改 B→C 边 | 端点相关摘要变化，未受影响 A 不变。 |
| GEN-04 | 无身份歧义的容器归属变化 | 按 Story 对比，不按新 GroupKey 全清。 |
| GEN-05 | 更新 B 时 A/C 正在读旧 archive 媒体 | 合法读取／缓存租约有效，A/C 不被无故中断。 |
| GEN-06 | 某 Story 退役或 Conflict | 不清无关历史／奖励收据，不复活迟到动作。 |
| MED-01 | 多 Story Group 仅 Start 中间一个成员 | 按有向可达预热，不按全 Group 加载。 |
| MED-02 | Flow／Logic／混合环 | 两类边都遍历，方向正确，去重有界。 |
| MED-03 | 超过 3 下载、超过 10 Story 槽位 | 严守 3/10，下载也占槽；不是按容器数计数。 |
| MED-04 | 10 个槽位全运行保护 | 新媒体排队，剧情仍可继续，不删除保护项。 |
| MED-05 | 多 Story 共用媒体，淘汰／禁用其中一方 | 其他使用者无丢图断音；最后释放才可回收。 |
| MED-06 | 单项就绪／整 Story 未就绪 | 单项可用但不冒充 bundle Ready。 |
| MED-07 | 管理器打开、只读 Graph、Reference 浏览 | 不触发剧情媒体预热。 |
| MED-08 | 更新／断线／冲突后旧异步结果回到 | 正确丢弃或仅复用验证内容，不复活旧实例。 |

### 18.8 必交付的端到端样例

**E2E-A：身份与非空迁移。**制作两份非空 Story，故意让隐藏资源 ID／节点 ID 有交集，同时包含未使用自有资源、外部引用、动态文字和媒体。A→B 后比较源与 B 原内容，检查全部副本引用、草稿结构提示、撤销重做和保存重开。按普通编辑器处理结构后导出 B 并真实游玩，不能只给 JSON diff。

**E2E-B：自动组与跨 Project 来源。**制作 A Flow→B、C Logic→B、D 独立，导出 Group 与独立文件；在另一 Project 整体 Reference Group，先仅引用 NPC 验证不扩组，再经既有公开边界连本地 X 验证导出闭包。另测整组 Import 的新 UID 与内部关系。

**E2E-C：管理器与对称冲突。**同一服务器安装正常独立包、正常 Group、手动禁用包、同 UID 副本和损坏容器。O 键 OP 面板必须全显示、四类状态分明；调整文件顺序结果一致；双方活动运行停止，无关内容照常。移除副本后原意图恢复，非 OP 伪造操作失败。

**E2E-D：普通禁用的边界。**两个玩家正在同组不同 Story 中，OP 手动禁用 Group；当前实例能继续完成，新的入口一律拒绝，A 不能用跨 Story 出口启动尚未运行的 B。验证已有实例重连，不把恢复当新建。

**E2E-E：成员更新与媒体。**同组至少三 Story，修改其中一个重新导出；确保部署目录中只有用户明确选定的那份新文件，避免把“并存冲突”误测成“内容更新”。A/C 不受 B 内容更新影响，媒体严格 3/10、有向预热、共享和租约回收正常。

所有 E2E 使用实际 Studio 导出的确定文件，记录路径、字节数、SHA-256、成员 UID；服务器与客户端必须使用同一产物。手写／合成包可用于恶意输入测试，但不能替代正式导出链验收。

---

## 19. 完成标准、交付与诚实报告

### 19.1 Definition of Done

- [ ] 正式主链无 DGR Namespace、旧 Full ID 与旧格式兼容 fallback；不是只删输入框。
- [ ] Story UID 创建后不可变；无重生成入口；普通资源身份隐藏且 Owner／Kind／Local ID 全链一致。
- [ ] 故事迁移可以追加到非空 B；源和目标原内容不变；自动 remap；固定不复制源外部 Story 线；结构约束有真实可编辑处理。
- [ ] Flow／Logic 统一派生 Group，成员自动、组名规则稳定；没有手工 membership 或 Group Runtime。
- [ ] `.dgrs` 单 Story、`.dgrs.g` 完整 Group；共享 Story 载荷、扁平媒体、全量校验且无部分发布。
- [ ] Reference 整体来源／单资源使用／Story 级连线三者分清；既有跨 Project 能力不被误删。
- [ ] 所有已安装容器判重，包括 Disabled；涉事双方完整阻断；无自动替换和旧快照绕过。
- [ ] 普通禁用只挡新启动，已有实例继续；Conflict／Error 的停止与删除语义明确实现。
- [ ] O 键 OP-only 管理器可用；四类状态、树、详情、只读图、整体操作、服务端权限全验证。
- [ ] 成员代次与媒体保持 Story 粒度；3 并发／10 Story 槽位；无泄漏和异步复活。
- [ ] 正常 0335 功能回归，自动测试与真实 GUI／Minecraft 分层记录。
- [ ] 当前 `dist` 已正确更新且产物版本、字节数、SHA-256 有实际读回证据；未测项明确列出。

### 19.2 构建与开发产物

使用仓库当前可用的 Java 8／Gradle 和 .NET 构建脚本，先核验路径和依赖，不把历史机器上的 SDK 路径写死为所有环境事实。Windows WPF 视觉／键盘验收使用仓库要求的原生 UI Automation／Win32；无 Windows／Minecraft 时记录该层 NOT_RUN，不用 Linux 静态检查冒充。[SRC-01][SRC-02][SRC-05]

按 `AGENTS.md`，用户可运行的 Studio 权威位置是：

```text
E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe
```

开发交付前更新该 self-contained Windows x64 目录并核验 ProductVersion／大小／SHA-256；`.tooling` 候选或 `bin/Release` 不等同于交付。保留 `Data`，保持根 apphost、`Program/Tools/Docs/Data` 组织；变更发布布局或更新可运行副本前读取 `dgr-release-packaging` skill。[SRC-01]

Runtime JAR 记录实际输出和部署测试路径，不假定未检查的路径已有产物。正常开发构建／更新 `dist` 不等于获得成品 Release 授权，不额外创建 `artifacts/DGR0.3.3.6`。

### 19.3 Delivery.json 起始模板

```json
{
  "version": "0.3.3.6",
  "plan_revision": "final-discussion-2026-10-04",
  "baseline_commit": "99a3253d7186127bdbe37898824ecbd32219f136",
  "implementation_commit": null,
  "status": "NOT_STARTED",
  "namespace_compatibility": false,
  "uid_regeneration": false,
  "story_transfer_mode": "append_to_existing_writable_story",
  "formats": [".dgrs", ".dgrs.g"],
  "tests": {
    "studio_core": "NOT_RUN",
    "studio_wpf": "NOT_RUN",
    "java": "NOT_RUN",
    "windows_gui": "NOT_RUN",
    "minecraft_e2e": "NOT_RUN",
    "multiplayer_permissions": "NOT_RUN"
  },
  "studio": {
    "path": "dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe",
    "product_version": null,
    "bytes": null,
    "sha256": null
  },
  "runtime_jar": {"path": null, "bytes": null, "sha256": null},
  "test_packages": [],
  "known_gaps": [],
  "user_accepted": false
}
```

完整交付报告列出实现项、实际测试层级、失败／未测项、确切产物与校验值。不能预填 PASS、声称不存在的截图／视频、写“已完成”却只有设计文件，或用代理测试取代用户本人验收。

---

## 20. 来源索引与证据边界

### 20.1 产品来源

本 PLAN 的最终产品约束来自本对话后续用户修订，不从旧文件推断授权。尤其以以下明确决定为准：

- 删除 UID 重生成，改为 A→B 内容复制；固定不复制源的外部 Story 连线。
- 非空 B 同样允许，技术 ID 冲突由 Studio 处理；采用追加而非覆盖。
- Group 只是故事整合容器，可用 `.dgrs.g`；无独立执行意义。
- Group／独立 Story 的 UID 重叠由系统报警、双方禁用，不提供替换决定。
- 整 Group 是最小只读 Reference 来源，其后引用方式保留。
- O 键 OP-only 包管理器，状态一目了然，绿色／灰色／红色及警告三角。
- Namespace 直接删除，测试版本不为旧格式背兼容代码。
- 其余配套建议获用户接受；本文将必要实现细节单独标为工程默认。

原 `DarkGreyRPG_0.3.3.6_UID_StoryGroup_HANDOFF.md` 的 Group 原意、成员完整性、Story 粒度媒体等仍可作背景；其中 UID 重生成、多 Story `.dgrs`、外部 Story 禁连等过时段落不得恢复。旧 `DarkGreyRPG_0.3.3.6_Construction_PLAN.md` 明确作废。

### 20.2 固定源码来源

以下均对应仓库 `GreyHat633/DarkGreyRPG` 的固定提交 `99a3253d7186127bdbe37898824ecbd32219f136`。括号中的读取层级用于区分本次重新核对与本对话前文已读取；不等于完成全文件／全仓库审计。

| 索引 | 文件／证据 | 支持内容 |
|---|---|---|
| SRC-01 | `AGENTS.md`（本次重读） | 便携目录、权威 Studio、发布授权与施工规范。 |
| SRC-02 | `README.md`（本对话前文已读取） | WPF Studio／Minecraft Runtime 形态、构建环境和导出工作流；README 版本文字较旧，不用它判当前版本。 |
| SRC-03 | `studio/src/DarkGreyRPG.Studio.Core/Graphs/Resources/CanonicalStoryLogicGraphRepository.cs`（本次重读） | 同一连接结构含 Flow／Logic；校验读取 OfflineProviderCatalog Story；公开端口约束。 |
| SRC-04 | `Graphs/Definitions/GraphNodeDefinitionRegistry.cs`、`GraphScopePolicy.cs`、`GraphNodeShapeValidator.cs`、`Graphs/GraphValidator.cs`（均在 Studio Core；本次读取相关部分） | Story Start 唯一、固定端口、图作用域和连线基数，不可只靠 ID 重写消除结构约束。 |
| SRC-05 | `.agents/skills/studio-node-ui/SKILL.md`（本次重读） | 原生 Windows 实机工具、主题文字、卡片、220ms 折叠和拖放规则。 |
| SRC-06 | `.agents/skills/story-media-lifecycle/SKILL.md`（本次重读） | 真正 Start、有向 Flow／Logic、3/10、保护、LRU、共享与独立解码寿命。 |
| SRC-07 | 本对话前文读取的 `StoryPackageManifest` C#／Java、`StoryPackageExporter.cs`、`OfflineStoryPackageImportService.cs`、`CanonicalExternalReferenceService.cs`、`DgrResourceId.cs`、`NamespacePolicy.cs`、`LoadedStoryPackage.java`、`StoryPackageLoader.java`、`StoryPackageSnapshotMerger.java`、`PackageGenerationKey.java`、`StoryPackageGenerationLifecycle.java`、`StoryMediaServer.java`、`StoryMediaCacheIndex.java` | 单 Story 包、旧身份依赖、读取／合并／代次／媒体改动入口；具体实现前须按 P0 继续追踪。 |

本次通过 GitHub connector 重新读取基线分支，HEAD 仍是上述提交。本文没有宣称远端之后不会更新，也没有宣称目标分支已创建。未访问用户 Windows 电脑、未运行工程测试、未修改 GitHub。

---

## 21. 可直接交给开发执行者的开工指令

> 以 `GreyHat633/DarkGreyRPG` 的 `codex/0.3.3.5`、已核对提交 `99a3253d7186127bdbe37898824ecbd32219f136` 为基线，新建或衔接 `codex/0.3.3.6`；先读本地变更和仓库规范，禁止覆盖已有工作。本文取代此前 0336 PLAN。
>
> 完全删除 DGR Namespace／旧 Full ID／旧格式兼容；新系统只保留不可变 Story UID 与隐藏的 Story-scoped 资源地址。故事内容迁移是 A 的自有资源及内部图追加进可写 B，B 可以非空，保留源与目标原内容，副本自动 remap，不复制源项目级 Story 外部连线，不添加 UID 重生成或空目标限制。
>
> 复用现有按 `interface_kind` 区分 Flow／Logic 的 Story Graph，自动派生 Group；`.dgrs` 单 Story，`.dgrs.g` 为扁平完整容器。Group 无永久 UID、无 Runtime／玩家状态／Generation。保留跨 Project Reference 与原有公开边界能力，来源 Group 整体只读输入，普通资源引用不扩大成员。
>
> 已安装文件清单必须包括 Disabled／Conflict／Error。任何来源重复实际成员 Story UID，所有涉事容器都阻止运行；不先来先得、不自动替换、不自动删一方。普通手动禁用仅禁止新 Story 启动，已有实例允许完成；Conflict／Error 则撤销活动执行资格。
>
> 实现 O 键 OP-only 故事包管理器：绿色启用、灰色禁用、红三角冲突、独立 Error；Group 可展开，Story 详情与 Group 只读图分开，操作按完整容器；每个读写请求均由服务器鉴权。
>
> 完成 P0 合同后按 P1—P6 执行，逐 Story 代次与媒体保持独立，严守 3 并发／10 Story 槽位；不要为 Group 重写执行引擎。实测非空复制唯一节点边界、来源闭包、对称冲突、已有实例禁用行为、权限与异步失效。实际证据和未测项分别记录；最后按仓库 invariant 更新开发 `dist`，不要把设计文档或探针结果冒充实机交付与用户验收。
