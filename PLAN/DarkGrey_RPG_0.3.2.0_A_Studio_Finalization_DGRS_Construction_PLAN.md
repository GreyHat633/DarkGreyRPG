# DarkGrey_RPG 0.3.2.0_A — Studio Finalization / Export & DGRS Construction PLAN

**Project:** DarkGrey_RPG / DGR  
**Target Version:** `0.3.2.0_A`  
**Target Branch:** `codex/0.3.2.0_A`  
**Construction Baseline:** latest verified `codex/0.3.1.5`  
**PLAN-time verified baseline HEAD:** `becc28c97ae62dbdd314050ef15348f4c05d9125`  
**Primary Input:** `0.3.1.5审计.docx`  
**Purpose:** 完成 Studio 冻结前最后一轮修缮，修复故事包导出 P0 阻塞，正式建立 `.dgrs` 单文件故事包契约，并在用户人工验收后决定是否冻结 Studio、进入 `0.3.2.0_B`。

---

# 0. Executive Rule — 版本总原则

`0.3.2.0_A` 是 **Studio Finalization / Freeze Candidate**。

它不是新的大架构版本，也不是 Minecraft 游戏内表现修复版本。

本版本只完成两件事：

1. 严格修复 `0.3.1.5审计.docx` 中列出的 **A 版本 6 项问题**；
2. 证明这些修改没有破坏 `0.3.1.x` 已经稳定下来的 Studio / Canonical / Runtime 高风险契约。

本版本的核心 Release Gate 是：

```text
一个合法故事
→ Studio 成功编译
→ 成功导出
→ 最终得到且只得到一个有效 .dgrs 文件
→ .dgrs 可被重新打开并通过包级校验
```

如果这一条不能完成：

```text
0.3.2.0_A = FAIL / NO-GO
```

即使其他 5 项 UI / Graph 修复全部完成，也不能称为 Release Candidate。

Codex 可以报告：

```text
IMPLEMENTED
AGENT_VERIFIED
0.3.2.0_A Release Candidate
```

Codex **不得自行报告**：

```text
USER_ACCEPTED
Studio FROZEN
Studio 已冻结
0.3.2.0_A 已正式完成
0.3.2.0_B 已获授权
```

只有用户完成真实 Studio 人工验收，并明确表示可以冻结 Studio 后，才允许进入：

```text
USER_ACCEPTED
Studio FROZEN
0.3.2.0_B authorized
```

---

# 1. Authority / Requirement Precedence

## 1.1 最高优先级：当前人工审计

施工前必须完整阅读：

```text
0.3.1.5审计.docx
```

A 版本当前明确包含 6 项：

```text
A1  会话图【结束】显示名同步
A2  【台词】Inspector 参数顺序统一
A3  【流程判断】菜单顺序 + Story Flow / Session scope
A4  故事包改为单文件 .dgrs
A5  【目标】增加“前置条件”
A6  修复合法项目故事包导出失败
```

这些是本 PLAN 的直接施工范围。

## 1.2 当前审计覆盖旧版本设计冲突

历史 `0.3.1.5` PLAN 曾把 `【流程判断】` 的主要 authoring scope 限定为会话图。

当前人工审计已经明确扩大为：

```text
Story 流程图：允许
会话图：允许
任务图：不允许
```

因此，以当前审计为准。

同理，旧路线中“进入 0.3.2.0 前先冻结 Studio”的编号边界，已被当前用户审计重新细化为：

```text
0.3.2.0_A = Studio 最后修缮
用户验收 / 冻结
0.3.2.0_B = Minecraft 游戏内审计与修复
```

冻结权限本身没有变化：仍然只属于用户。

## 1.3 历史 PLAN 的用途

以下历史材料必须作为 regression contract 重新阅读：

```text
0.3.1.1
0.3.1.2A
0.3.1.2B
0.3.1.3
0.3.1.4
0.3.1.5
```

但不得把历史文档中的 `PASS / AGENT_VERIFIED` 直接继承为本版本 PASS。

---

# 2. Source Baseline Rule

PLAN 编制时验证到：

```text
branch: codex/0.3.1.5
HEAD: becc28c97ae62dbdd314050ef15348f4c05d9125
message: docs(plan): normalize 0.3.1.5 audit report
```

但 Codex 在真正开始施工时仍必须重新解析实际最新 HEAD，不允许机械使用 PLAN 中记录的 SHA。

推荐施工前步骤：

```bash
git fetch origin
git status --short

git checkout codex/0.3.1.5
git pull --ff-only origin codex/0.3.1.5

git rev-parse HEAD
git status --short
```

然后：

```bash
git checkout -b codex/0.3.2.0_A
```

如果目标分支已存在，则使用现有分支，不得破坏性重建。

## 2.1 Dirty worktree rule

如果工作树已有用户修改：

- 不得 `git reset --hard`；
- 不得擅自 `git clean`；
- 不得擅自删除未跟踪文件；
- 不得把与本 PLAN 无关的 dirty 内容顺手纳入提交；
- 必须在施工报告记录 baseline dirty state。

## 2.2 Release authority

除非用户另行授权，本 PLAN 不授权 merge、tag、GitHub Release、上传二进制 Release asset 或删除历史分支。

---

# 3. Mandatory Source Reconnaissance Gate

本版本包含一个真正的 P0 导出问题和一个包格式边界，因此禁止 Codex 直接开始“改 UI / 改后缀”。施工前必须先对当前 `0.3.1.5` 源码做一次最小、定向的 source reconnaissance，并在施工报告记录结论。

至少确认以下 6 个链路：

## 3.1 Session End 显示名链路

确认：

```text
【结束】DisplayName
→ Story Flow 聚合/出口显示
→ 会话图内部【结束】节点
→ 其 Flow Input 可见标签
```

找出为什么外层已经更新、内层端口没有更新。

## 3.2 Dialogue Inspector 字段排序

确认：

```text
【台词】canonical property definition
inline parameter order
Inspector row generation order
```

不得在不知道顺序来源的情况下只在 XAML 上硬调视觉位置。

## 3.3 Node Registry / Scope / Menu Ordering

确认：

```text
flow_judgment stable node type
Story Flow authoring scope
Session authoring scope
Task authoring scope
Logic category canonical ordering
menu 是否存在本地二次 sort
```

## 3.4 Objective schema / runtime

确认：

```text
Objective canonical model
Objective graph node definition
dynamic port mechanism
Objective lifecycle
Task runtime activation/progress/completion
serialization
snapshot / runtime persistence
```

## 3.5 Export pipeline

必须画出或在报告列出真实调用链，例如：

```text
Export command
→ dirty/draft flush
→ canonical compile
→ semantic/schema validation
→ resource/reference resolution
→ runtime payload materialization
→ current folder writer
→ output commit
```

实际名称以源码为准。

## 3.6 Story package contract

确认当前文件夹式故事包到底包含什么：manifest / metadata 是否已经存在、JSON / resource / payload 路径、Runtime 当前期待的目录结构、Java 端是否已有 story package reader、Studio 和 Runtime 是否已有共享 package schema、当前导出失败发生在“编译”还是“落盘/封包”。

**完成这一步之前，不允许把“导出失败”和“.dgrs”混成一个 catch-all 修复。**

---

# 4. Scope Lock

## 4.1 IN SCOPE

只有以下 6 个 Work Packages：

```text
WP-A  Session【结束】显示名在会话图内同步
WP-B  【台词】Inspector 改为角色在上、文本在下
WP-C  【流程判断】菜单顺序 + Story Flow / Session scope
WP-D  单文件 .dgrs 故事包格式
WP-E  【目标】“前置条件”开关 + Logic Input + Runtime 语义
WP-F  合法故事包导出失败 P0 根因修复
```

以及 mandatory historical regression gate、final Release EXE verification、DGRS package validation、final development report。

## 4.2 EXPLICITLY OUT OF SCOPE — 0.3.2.0_B

本版本严禁提前施工以下 Minecraft 游戏内问题：

```text
B1  “铜币”却使用金粒贴图的问题
B2  收纳箱左下角冗余状态提示
B3  游戏内 Task / Objective UI 呈现问题
```

尤其：`WP-E` 只负责 Studio / Canonical / Runtime 中“Objective 何时激活”的数据与语义，**不负责** Minecraft HUD / 任务面板如何显示 Objective。

## 4.3 Architecture Non-Goals

本版本不得扩张成 Graph Editor rewrite、新 persistence database、generic variable system、generic event bus、第三种通用接口类型、generic state-machine engine、plugin API、generic package framework、archive browser、encryption/DRM/signature/differential package、auto-update、`.dgrs` authoring importer、Minecraft UI framework、generic Objective condition framework，或为一个菜单顺序新增通用排序框架。

原则：**修现有架构，补最小必要契约，不另造平台。**

---

# 5. Severity / Release Priority

| Priority | Work Package | Release Meaning |
|---|---|---|
| **P0** | WP-F 合法项目导出失败 | 不修复绝对 NO-GO |
| **P0** | WP-D `.dgrs` 单文件故事包 | A 版本核心交付，不完成 NO-GO |
| **P1** | WP-E Objective 前置条件 | Canonical / Runtime 语义新增，必须真实工作 |
| **P1** | WP-C Flow Judgment scope / menu | Graph authoring contract，必须正确 |
| **P2** | WP-A Session End 显示同步 | Studio 最终一致性修缮 |
| **P2** | WP-B Dialogue Inspector 顺序 | Studio 最终一致性修缮 |
| **Release Blocker** | 任一高风险历史 regression | NO-GO |

这里的 P2 不表示“可不做”。六项全部属于当前 A 版本 mandatory scope。

---

# 6. Global Invariants

## 6.1 Stable identity ≠ display text

重命名 DisplayName 不得无必要改变 node type、node id、resource id、port id 或 serialized stable identity。

## 6.2 Graph interface cardinality

继续保持：

```text
● Flow Output: max 1 target
● Flow Input: 0..N sources
◆ Logic Output: 0..N targets
◆ Logic Input: max 1 source
```

即：流程去向唯一，来源可多；逻辑来源唯一，去向可多。

## 6.3 No silent data loss

不得静默删除有效连线、静默丢掉未知但合法的数据、通过忽略错误让导出假成功，或把失败资源从故事包里偷偷省略。

## 6.4 Error behavior

正常作者界面使用简短中文、可操作诊断，不暴露 raw exception/internal path；Problems / development report 可以包含 error code 与技术详情，并必须保留 root cause 证据。

## 6.5 Minimal architecture

优先复用现有 registry、validator、dynamic port、serialization、Runtime graph semantics、export staging/layout；不要因为 `.dgrs` 再造一套 canonical model。

---

# 7. WP-A — Session【结束】显示名在会话图内同步

## 7.1 Observed Problem

当前已存在修改【结束】显示名的能力，Story 流程图可以看见修改后的结束显示名，但会话图内部【结束】节点仍看不到修改后的名称。

当前审计给出的最低明确要求是：**会话图内部【结束】节点的“流程输入”端口应该显示修改后的结束显示名。**

## 7.2 Final User Contract

假设：

```text
默认结束显示名 = 结束
用户改名 = 接受委托
```

那么会话图内部最少必须呈现：

```text
【结束】
● 接受委托
```

而不是：

```text
【结束】
● 流程输入
```

Exact rule：

```text
End Flow Input visible label = current End DisplayName
```

## 7.3 Node title boundary

本 PLAN **不要求**强行把固定节点标题 `【结束】` 改成自定义名字，除非当前源码已有明确统一绑定。最低、明确且必须实现的 UI contract 是固定节点类型仍为【结束】，其 Flow Input 可见名同步 End DisplayName。

## 7.4 One Source of Truth

不得新增第二个独立持久字段去复制 End DisplayName。正确方向：

```text
canonical End DisplayName
→ projection / node definition
→ visible Flow Input label
```

## 7.5 Dynamic update / persistence

```text
修改显示名
→ 不重启 Studio
→ 会话图端口名更新
```

并且 Save → Close → Reopen 后名称仍正确。

## 7.6 Stable port

重命名只能改变 visible label，不得改变 port id / wire identity / connection。已连接到【结束】的 Flow wire 不得因改名断掉。

## 7.7 Acceptance

| ID | Test | Expected |
|---|---|---|
| A1 | 新 Session 默认 End | 默认标签正确 |
| A2 | 改 End DisplayName | 会话图 Flow Input 立即同步 |
| A3 | 已连接状态改名 | wire 不断 |
| A4 | 保存 / 重开 | 名称保持 |
| A5 | 中文 / 英文 / 较长名称 | 正常显示，不改 stable id |
| A6 | 外层 Story Flow | 与当前已工作的显示逻辑一致，无 regression |

---

# 8. WP-B — 【台词】Inspector 顺序统一

## 8.1 Observed Problem

当前【台词】节点在参数/节点编辑面是：

```text
角色
文本
```

但 Inspector 是：

```text
文本
角色
```

## 8.2 Final Contract

【台词】Inspector 必须统一为：

```text
角色：<Actor selector>
文本：<Dialogue text>
```

## 8.3 Scope

只修 author-facing property ordering。不要借此修改 canonical field name、JSON key、Runtime dialogue semantics、Actor identity semantics、text serialization 或节点类型。

## 8.4 Implementation rule

先确认 Inspector 顺序来自 property definition order、explicit metadata、ViewModel row construction 还是 local UI sort，优先修真正的顺序来源。不要只针对 `dialogue` 做 XAML 视觉 hack，也不要为了两个字段新造 generic property-order framework。

## 8.5 Acceptance

| ID | Test | Expected |
|---|---|---|
| B1 | 新建【台词】 | Inspector 角色在上、文本在下 |
| B2 | 打开旧【台词】 | 同样顺序 |
| B3 | 切换多个台词节点 | 顺序稳定 |
| B4 | 多行文本 | 文本控件正常，不影响角色行 |
| B5 | 保存 / 重开 | 数据无变化 |
| B6 | Inline / Inspector | 两边语义顺序一致 |

---

# 9. WP-C — 【流程判断】菜单顺序与 Authoring Scope

## 9.1 Current Contract Change

`0.3.1.5` 已经建立【流程判断】：

```text
1 × ● Flow Input
1 × ● Flow Output
1 × ◆ Logic Output：执行状态
```

并建立 sticky runtime semantics：initial false；Flow executes through node → executed=true；在 containing runtime instance 生命周期内保持 true。

`0.3.2.0_A` 不重新设计这个节点，只修 authoring menu 顺序、authoring scope，以及 Story Flow scope 下的 canonical/runtime/package compatibility。

## 9.2 Exact Menu Order — HARD UI CONTRACT

关键尾部顺序必须精确是：

```text
逻辑输出
条件判断
流程判断
```

三者之间不得插入其他节点。

结合既有 ordering，若当前节点集合未变化，目标可表示为：

```text
逻辑输入
与
或
非
逻辑输出
条件判断
流程判断
```

但真正锁死的是 `逻辑输出 → 条件判断 → 流程判断`。

## 9.3 Allowed scope

```text
Story 流程图：authorable
会话图：authorable
任务图：NOT authorable
```

任务图保持 pure Logic-oriented，不应出现带 Flow In / Out 的【流程判断】。

## 9.4 Scope must be model-driven

不得只做 WPF menu hide，而底层 registry 仍把它定义成 Task 可创建。优先复用现有 scope metadata 表达 Story/Session allowed、Task disallowed。

## 9.5 Runtime semantics unchanged

扩大 Story Flow authoring scope 后，必须确认同一个稳定 node type 在 Story runtime 中仍满足：

```text
executed=false
→ Flow arrives
→ executed=true
→ Flow continues
```

不得新建 `story_flow_judgment` 第二类型、第二套状态字段或不同 stable port IDs，也不得把 Story 版本做成 UI-only fake node。

## 9.6 Task compatibility

正常新 authoring 中 Task 必须完全看不到【流程判断】。如果历史/异常 fixture 已存在 Task `flow_judgment`，不得在 load 时静默删除；使用现有 compatibility/validation 规则并在报告说明。

## 9.7 Acceptance

| ID | Test | Expected |
|---|---|---|
| C1 | Story Flow 添加节点 > 逻辑 | 存在【流程判断】 |
| C2 | Session 添加节点 > 逻辑 | 存在【流程判断】 |
| C3 | Task 添加节点 > 逻辑 | 不存在【流程判断】 |
| C4 | Story menu | 逻辑输出→条件判断→流程判断连续 |
| C5 | Session menu | 同上 |
| C6 | Story place/save/reload | node/ports 稳定 |
| C7 | Session place/save/reload | node/ports 稳定 |
| C8 | Story runtime probe | false→true，Flow 继续 |
| C9 | Session runtime probe | 0315 sticky semantics 无 regression |
| C10 | Choice compatibility | 0315 Choice legacy handling 无 regression |

---

# 10. WP-D — `.dgrs` Single-File Story Package

## 10.1 Audit Requirement

当前故事包最终导出物不再以暴露文件夹作为产品形态。目标：

```text
一个故事包 = 一个 .dgrs 文件
```

`.dgrs` 是 DarkGrey RPG 的故事包扩展名。

## 10.2 Important Boundary

`.dgrs` 是 compiled/exported story package，不是 Studio source project format。本版本不得把 Studio 工程本身迁移为 `.dgrs`。

## 10.3 DGRS v1 — Construction Profile

以下是本 PLAN 对“自定义 `.dgrs` 压缩故事包”的施工化落地规范。它不是要求重新发明压缩算法。

为了 C# Studio 与 Java Minecraft Runtime 之间保持简单、成熟、可维护：

```text
DGRS v1 physical container
=
standard ZIP-compatible compressed container
+
custom .dgrs extension
+
DGRS-owned manifest/version contract
```

即 `foo.dgrs` 内部使用标准 ZIP/Deflate 机制。自定义格式来自 `.dgrs` 产品扩展名、固定 package contract、明确 format/version、DGRS validator 和 DGR 自己定义的 entry 语义，而不是重新实现压缩算法。

## 10.4 Not just “rename .zip”

禁止 existing folder → zip → rename `.dgrs` → done，而没有格式身份与版本契约。

DGRS v1 必须能回答：

```text
这是 DGRS 吗？
这是哪个 DGRS format version？
这个包结构完整吗？
这个版本是否受支持？
```

## 10.5 Manifest

Codex 必须先检查当前故事包是否已有 manifest/package metadata。

若已有权威 manifest，优先扩展/复用，至少补足等价：

```text
format = dgrs
format_version = 1
producer/tool version
```

若没有 package-level manifest，则新增最小根级 `manifest.json`，最低信息：

```json
{
  "format": "dgrs",
  "format_version": 1,
  "producer": "DarkGreyRPGStudio",
  "producer_version": "<actual version>"
}
```

如果 package/story identity 已经由现有 canonical payload 权威保存，不要为了 manifest 再复制一套可漂移的 story metadata。

## 10.6 Internal Layout Rule

不要趁这个版本重写故事包内部目录结构。优先把现有成功 folder export 的 logical layout 原样作为 DGRS archive entries，只增加 DGRS 所必需的 format identity、version、validation、container boundary。

最终施工报告必须列出**实际 DGRS v1 entry layout**。

## 10.7 Archive path safety

DGRS writer/reader/validator 至少拒绝明显无效 entry：absolute path、`../` traversal、empty normalized path、duplicate normalized path。不要扩张成通用 sandbox framework。

## 10.8 Writer transaction

推荐：

```text
1. compile/validate canonical data
2. materialize package staging
3. write <target>.tmp.dgrs
4. close archive
5. reopen .tmp.dgrs
6. run DGRS validator
7. success → atomic/replace final <target>.dgrs
8. failure → final artifact 不被提交
```

不得一边写 final.dgrs，中途失败后留下看似成功的半成品。

## 10.9 Existing output protection

如果目标位置已有上次成功的 `story.dgrs`，本次重新导出失败，不能先删掉旧成功包。必须先在临时路径成功构建、验证，再替换。

## 10.10 Final user-facing artifact

一次正常导出最终只应暴露：

```text
<story-package>.dgrs
```

不得在目标目录再留下 manifest.json、data/resources/temp/staging 或同名导出文件夹。内部 staging 可以在临时工作目录存在。

## 10.11 DGRS Validator

至少检查：archive 可打开、manifest 存在、`format == dgrs`、format_version 受支持、required package entries 存在、entry path 合法、内部 canonical/runtime payload 通过现有 package validation。

对 corrupt archive、missing manifest、wrong format、unsupported version、missing required entry 必须明确失败。

## 10.12 DGRS Reader boundary

A 版本不实现 Minecraft 游戏内 UI，但应把 open/list/validate DGRS 放在未来 Java Runtime 可以稳定复用或对照实现的明确 package contract 上。已有 shared/core abstraction 就复用；无法共享代码时共享格式契约 + 双端 fixture，而不是新造 IPC/framework。

## 10.13 Format documentation

DGRS v1 不能只存在于代码里。必须在仓库留下明确格式说明，至少包含用途、physical container、format identity/version、manifest、entry layout、encoding/path rule、required/optional entries、validation behavior、compatibility policy。

## 10.14 Explicit Non-Goals

不做数字签名、加密、DRM、远程包仓库、增量补丁、多卷压缩、自定义压缩算法、从 `.dgrs` 反向恢复 Studio authoring project。

## 10.15 DGRS Acceptance

| ID | Test | Expected |
|---|---|---|
| D1 | 正常导出 | 最终只得到一个 `.dgrs` |
| D2 | extension | 精确 `.dgrs` |
| D3 | reopen | archive 可重新打开 |
| D4 | manifest | format/version 正确 |
| D5 | validator | 合法包 PASS |
| D6 | corrupt archive | FAIL，明确诊断 |
| D7 | missing manifest | FAIL |
| D8 | unsupported format version | FAIL |
| D9 | Chinese names/resources | round-trip 正确 |
| D10 | path traversal entry fixture | validator 拒绝 |
| D11 | duplicate normalized entry | validator 拒绝 |
| D12 | failed overwrite | 旧成功 `.dgrs` 不被破坏 |
| D13 | temp cleanup | 成功/失败后无用户可见 staging |
| D14 | current payload semantics | 封包前后 canonical/runtime payload 等价 |

---

# 11. WP-E — 【目标】“前置条件” Activation Gate

## 11.1 User-facing Authoring Contract

默认：

```text
☐ 前置条件
```

此时不显示新的 prerequisite Logic Input，Objective 使用当前既有默认激活行为，旧项目行为完全不变。

启用：

```text
☑ 前置条件
前置条件为 True 时激活
◆ 前置条件
```

其中 `◆ 前置条件` 是一个新的 Logic Input。

## 11.2 Port Contract

```text
Kind: Logic Input
Cardinality: max 1 source
Visible label: 前置条件
```

必须使用稳定 port id；显示名不是 port identity。

## 11.3 Activation semantics — HARD CONTRACT

这个开关是 Objective activation gate，不是持续可见条件、完成条件、每 tick 开关或 reset 条件。

启用后：

```text
Objective initial state = inactive
prerequisite != true → remains inactive
prerequisite becomes true → Objective activates
once activated → 进入既有 Objective lifecycle
```

激活后，前置条件后来变回 False：**不得让 Objective 重新失活**。

即：

```text
False → True = activation gate
activation 后 = sticky within existing Task/Objective lifecycle
```

## 11.4 Progress boundary

Objective 尚未激活时，不应把它当作已激活 Objective 正常推进，也不应提前结算完成。一旦激活，继续复用当前对应 Objective 类型既有 tracking/completion 逻辑。本 PLAN 不重新定义实体击杀、物品收集、角色交互各自算法。

## 11.5 Legacy compatibility

对 `0.3.1.5` 及更早项目，如果没有新字段，则默认 false，表现为无新 port、无行为变化。实际字段名以当前 schema 命名风格为准。

## 11.6 Toggle ON/OFF

OFF → ON：checkbox true → helper text 出现 → exactly 1 Logic Input 出现，port id 稳定，保存/重开后仍保持。

ON → OFF：helper text 与 Logic Input 消失。如果 prerequisite port 已连线，不得留下 invisible/orphan wire；checkbox、port removal、incident connection removal 必须是一个一致 graph mutation。若当前 Graph Editor 已有 Undo transaction，则 Ctrl+Z 应能恢复 toggle + port + wire；不额外发明全局确认框系统。

## 11.7 Unconnected ON state

如果已开启但 prerequisite port 无来源，则 value 不是 True，Objective 保持 inactive。是否对“不连接”发 warning/error，复用当前 required-input/incomplete-authoring validation 规则，不另造 severity policy。

## 11.8 Runtime persistence

如果当前 Objective active state 已参与 snapshot/NBT/runtime save/restore，则 prerequisite 触发后的激活状态必须进入相同生命周期，不得保存世界前已激活、重载后又 inactive。若当前 Objective 本身不持久化 active state，则报告现有边界，不借本版本扩张成通用状态系统。

## 11.9 DGRS integration

新字段与必要 runtime semantics 必须正确进入：

```text
Studio save
→ canonical compile
→ .dgrs
→ package validator
```

不能出现 Studio UI 有 checkbox 但 export 丢字段。

## 11.10 This is NOT Task UI work

本 WP 不规定 Minecraft 里 Objective 如何排版、展开、折叠、显示 completed/inactive 或 HUD 提示，这些全部留给 `0.3.2.0_B`。

## 11.11 Acceptance

| ID | Test | Expected |
|---|---|---|
| E1 | legacy Objective | 默认 OFF，无 port，行为不变 |
| E2 | toggle ON | helper + ◆ 前置条件出现 |
| E3 | toggle OFF | helper + port 消失 |
| E4 | stable port id | save/reload 不变 |
| E5 | cardinality | Logic Input max 1 source |
| E6 | false prerequisite | inactive |
| E7 | true prerequisite | activates |
| E8 | true→false after activation | remains active |
| E9 | inactive progress | 不提前按 active Objective 推进 |
| E10 | save/reload Studio | authoring 状态稳定 |
| E11 | Runtime snapshot if applicable | activated state 不倒退 |
| E12 | DGRS round-trip | 新字段/port semantics 不丢 |
| E13 | toggle OFF with wire | 无 orphan/dormant connection |
| E14 | Task UI | 本版本无 Minecraft UI 修改 |

---

# 12. WP-F — Story Package Export P0 Root-Cause Repair

## 12.1 Audit Fact

当前人工审计确认项目故事包导出失败，并明确指出用户确认项目/任务包本身没有问题。因此本版本不能在没有具体 validator/schema/reference 证据时简单归咎为“用户项目非法”。

## 12.2 P0 Rule

Codex 必须：

```text
先复现
→ 捕获 first failure
→ 确认 root cause
→ 再改代码
```

禁止先写 catch-all 或吞错误。

## 12.3 Required reproduction evidence

如果审计中的实际项目/fixture 在仓库或工作树可取得，必须直接用它复现，并记录 project/story identity、export command、output target、first exception/validation failure、stack/error code、pipeline stage。

如果 exact project 不在 Codex 环境：不得假装已经复现 exact project；必须构造最小 representative fixture；报告：

```text
USER_PROJECT_EXACT_REPRO = NEED_USER_VERIFICATION
```

但这不免除源码 root-cause 调查。

## 12.4 Pipeline stage classification

把失败精确归到真实阶段：

```text
1. active draft flush / save consistency
2. canonical compile
3. schema validation
4. semantic validation
5. resource/reference resolution
6. runtime/package model generation
7. staging materialization
8. DGRS archive write
9. DGRS reopen validation
10. final output commit
```

## 12.5 Root cause, not symptom patch

最终报告必须包含：Observed failure、First failing stage、Root cause、Why valid project triggered it、Files changed、Why fix is general、Regression fixture。

## 12.6 Forbidden “fixes”

严禁 disable validator、skip invalid-looking node/resource、silently omit Task/Session/Objective、fallback back to folder export、hard-code user's package ID/name、catch exception and still show success、create empty `.dgrs`、only change extension、只测试空 Story、用与最终 Release EXE 不同的代码路径。

## 12.7 Dirty/draft consistency

Export 是允许要求 disk-consistent authoring state 的边界。导出前必须确保 active UI draft → canonical committed state → export snapshot。具体采用 flush+save 还是 flush+in-memory snapshot，复用当前 Studio 已有设计。关键是导出不能偷偷使用比 UI 落后一版的 stale disk data，同时不能重新引入 dirty 时无法正常编辑的全局锁。

## 12.8 Success transaction

成功定义必须是：

```text
compile PASS
validation PASS
package materialized
DGRS written
DGRS reopened
DGRS validator PASS
final file committed
```

在这之前不得向用户显示“导出成功”。

## 12.9 Failure transaction

失败时明确告诉用户失败，保留可操作中文摘要，详细诊断进入 Problems/log，不留下伪成功 final `.dgrs`，不破坏旧成功包，临时文件尽力清理，不修改用户 authoring 数据去迎合 exporter。

## 12.10 Export tests

Valid fixtures 至少：minimal valid Story、Story+Session、Story+Task、Story+Session+Task、Story+Flow Judgment、Task+Objective prerequisite OFF、Task+Objective prerequisite ON/connected、中文 display/resource names、representative root-cause fixture。

Invalid fixtures 使用当前 validator 已有主要非法类别，确认 invalid → fail clearly → no final DGRS。不要为了本版本再发明几十类 synthetic validation。

## 12.11 Release Gate

最终 Release EXE 中至少完成一次真实：

```text
Open/create valid project
→ author Story
→ Save
→ Export
→ choose target
→ export
→ obtain exactly one .dgrs
→ validate package
```

如果环境无法操作用户 exact package，最终状态只能是 representative project `AGENT_VERIFIED` + exact user package `NEED_USER_VERIFICATION`。

---

# 13. DGRS / Canonical Compatibility Rules

1. `0.3.1.5` 项目必须继续打开，不得要求 source project 转为 `.dgrs`。
2. 旧 Objective 缺失 prerequisite field → default false。
3. 0315 Session `flow_judgment` node type、stable ports、sticky semantics 不改；本版本只增加 Story Flow authoring scope。
4. Task 不增加 Flow Judgment authoring。
5. 0315 Choice Flow-only authoring 和 legacy Logic compatibility 保留。
6. 旧 folder materialization 若仍是合理 compiler staging，可以保留为内部 implementation detail；最终 user-facing export 不得再把 folder 当故事包交付物。

推荐：

```text
Compile
→ internal staging directory
→ DGRS pack
→ validate
→ final .dgrs
```

不要为了“没有文件夹”把现有 compiler 全部重写成直接流式写 ZIP。

---

# 14. Automated Verification

本 PLAN 不预先写死测试数量，最终报告必须给出 fresh result。

## 14.1 Studio Core

运行完整相关 Core suite，并新增/更新：End display label projection、Flow Judgment allowed scopes、Objective prerequisite schema/default/stable port/serialization、DGRS manifest/version/round-trip、export root-cause regression fixture。

## 14.2 WPF

运行完整相关 WPF suite，并新增/更新：Session End visible Flow Input label、Dialogue Inspector row order、Story/Session Logic menu order、Task Flow Judgment absence、Objective checkbox/helper/port visibility、toggle mutation、export command result/error behavior。

不得只搜索 XAML string 就声称完整 UI PASS。

## 14.3 Java / Runtime

运行既有 formatting、compile、runtime probes，并补充必要 probe：Flow Judgment in Story scope、Objective prerequisite false/true/sticky activation、DGRS fixture compatibility/parser contract（若当前架构支持）。不要为了测试 DGRS 新建大型运行平台。

## 14.4 Package tests

DGRS 至少覆盖 valid round-trip、single-file final output、manifest/version、unicode、corrupt archive、missing manifest、unsupported version、path traversal、duplicate normalized path、writer failure cleanup、failed overwrite preservation、payload semantic equivalence。

## 14.5 Export regression

必须有一个测试锁死本次 WP-F root cause，让后续开发者能看出为什么 0315 合法项目会导出失败，而不是只有泛化 `ExportWorks()`。

---

# 15. Final Release EXE Live Gate

自动化不是 Studio freeze 的充分证据。本版本必须生成最终 Release EXE，并对同一最终哈希执行 live gate。

## 15.1 Artifact record

最终报告至少记录 Release EXE path、ProductVersion、FileVersion、size、SHA-256、build configuration。若沿用当前约定，权威路径继续是：

```text
dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe
```

除非当前构建脚本已正式改变。

## 15.2 Same-hash evidence rule

所有 `FINAL_HASH_LIVE_PASS` UI/export 证据必须来自最终报告记录的同一 EXE SHA-256。

## 15.3 Live gate — WP-A

Session → End display name 改名 → Story Flow 检查 → 会话图 End Flow Input 检查 → Save → Reopen。

## 15.4 Live gate — WP-B

真实选择【台词】，Inspector 必须看到角色在上、文本在下。

## 15.5 Live gate — WP-C

分别在 Story Flow / Session / Task 打开 `添加节点 > 逻辑`。Story/Session 证明 `逻辑输出 → 条件判断 → 流程判断`；Task 证明无流程判断。然后 Story/Session 各放置一个，保存重开。

## 15.6 Live gate — WP-E

Objective OFF：`☐ 前置条件`、无 helper、无 prerequisite port。ON：`☑ 前置条件`、`前置条件为 True 时激活`、`◆ 前置条件`。连线、保存、重开；若 UI 支持 Undo，验证 toggle OFF 后 undo 能恢复结构。

## 15.7 Live gate — WP-D / WP-F

必须：

```text
1. 打开/创建一个非空合法项目
2. 至少包含 Story + 实际内容
3. 保存
4. 执行故事包导出
5. 导出成功
6. 目标位置只出现一个 .dgrs
7. 不出现最终同名 folder
8. 用 DGRS validator 重新打开
9. 输出 manifest / entry listing 证据
10. package validation PASS
```

如果能取得用户审计 exact project，再执行 exact project 导出。

## 15.8 Suggested evidence path

```text
.tooling/0.3.2.0_A/
  baseline/
  tests/
  export-rootcause/
  dgrs/
  live/
  screenshots/
  final/
```

不要把大量二进制视频/缓存无条件提交进 Git。

---

# 16. Historical Regression Gate

`0.3.2.0_A` 是 Studio freeze candidate，因此至少重新验证：

## 16.1 Unsaved close

继续保持 `保存 / 不保存 / 取消`，多 dirty resource 仍只弹一次 workspace-level decision。

## 16.2 Actor / Item Inspector

继续保持无“资源属性”、无冗余“显示名称”、正确 NPC_ID/Item_ID/Group_ID、标签 hanging-indent。

## 16.3 Wire geometry / gestures

继续保持 wire 从 visible ●/◆ center 出发、visual port/hitbox 规则、single reconnect、multi ordinary drag、Ctrl multi reconnect、glow leaving clears、blank/invalid drop transaction。

## 16.4 Action terminology

继续保持 `物品给予 / 经验给予 / 消息发送` 及正确 field labels。

## 16.5 Choice

继续保持 Choice option normal UI = Flow-only，旧 Choice Logic connection compatibility 不被破坏。

## 16.6 Flow Judgment

Session 0315 semantics 不能因 Story scope 扩展回归。

## 16.7 Draft / Commit

文本编辑继续 `UI draft ≠ canonical value`，Ctrl+S/save/export 前正确 flush，不得重新出现每 keystroke 全图 mutation。

## 16.8 Node layout / viewport

Story Flow / Session / Task 节点位置继续跨 Studio restart 保存；各 graph viewport 继续相互独立。

## 16.9 Resource lifecycle

继续：删除 Session/Task resource → placements 删除；只删除 placement → resource 保留。

## 16.10 Story Start / Session Start / Task Settlement

继续验证 Story Start fixed/startup conditions、Session required 起始、Task pure Logic、unique 结算、first true wins、no legacy 激活 authoring。

## 16.11 Objective existing types

继续：实体击杀有 quantity、物品收集有 quantity、角色交互 NO quantity。WP-E 不能把三个 Objective 重构回模板混乱状态。

## 16.12 Dark / Light

本次新增/变化的 Dialogue Inspector、Flow Judgment、Objective prerequisite、export UI/error 在 Dark/Light 都必须可读。

## 16.13 Export errors

合法项目成功，非法项目仍应失败。修 WP-F 不能让 validator 宽松到什么都能导出。

---

# 17. 0.3.2.0_A Release Acceptance Matrix

最终 DEVELOPMENT REPORT 必须逐行给状态。允许状态：

```text
NOT_STARTED
ROOT_CAUSE_CONFIRMED
IMPLEMENTED
AGENT_VERIFIED
BLOCKED
NEED_USER_VERIFICATION
```

Codex 不得填写 `USER_ACCEPTED`。

| ID | Gate | Evidence | Status |
|---|---|---|---|
| A1 | End Flow Input 同步 DisplayName | Release EXE | |
| A2 | End rename 不断 wire | Release EXE + model | |
| A3 | Save/reopen End name | Release EXE | |
| B1 | Dialogue Inspector 角色在上 | screenshot | |
| B2 | Dialogue Inspector 文本在下 | screenshot | |
| C1 | Story 可添加流程判断 | screenshot + model | |
| C2 | Session 可添加流程判断 | screenshot + model | |
| C3 | Task 不可添加流程判断 | screenshot + model | |
| C4 | 逻辑输出→条件判断→流程判断 | screenshot | |
| C5 | Story Flow Judgment runtime | probe | |
| C6 | Session Flow Judgment regression | probe | |
| D1 | final output = one `.dgrs` | filesystem evidence | |
| D2 | DGRS manifest/version | entry listing | |
| D3 | DGRS reopen/validate | validator output | |
| D4 | corrupt DGRS rejected | automated | |
| D5 | failed overwrite preserves old package | automated | |
| E1 | Objective legacy default OFF | Core/WPF | |
| E2 | ON → helper + Logic Input | Release EXE | |
| E3 | false→inactive | Runtime probe | |
| E4 | true→activate | Runtime probe | |
| E5 | true→false remains active | Runtime probe | |
| E6 | DGRS round-trip | package probe | |
| F1 | 0315 export failure reproduced | root-cause evidence | |
| F2 | root cause documented | report | |
| F3 | representative valid project export PASS | final EXE | |
| F4 | exact user project export | final EXE / NEED_USER_VERIFICATION | |
| R1 | unsaved close | Release EXE | |
| R2 | Actor/Item Inspector | Release EXE | |
| R3 | wire center | frames/measurement | |
| R4 | single reconnect | frames/video | |
| R5 | multi/Ctrl reconnect | frames/video | |
| R6 | Choice Flow-only | Release EXE | |
| R7 | Draft/Commit | interaction | |
| R8 | node layout restart | before/after | |
| R9 | resource lifecycle | interaction | |
| R10 | Light/Dark changed surfaces | screenshots | |

---

# 18. NO-GO Conditions

任何一条成立，都不能发布 `0.3.2.0_A Release Candidate`：

1. 合法 Story 仍无法导出。
2. 导出“成功”但没有有效 `.dgrs`。
3. 最终仍以 folder 作为用户故事包交付物。
4. `.dgrs` 只是改后缀，没有 format/version/validation contract。
5. 导出失败会留下看似成功的损坏 final `.dgrs`。
6. 失败重导会先破坏旧成功 `.dgrs`。
7. exporter 通过跳过 Task/Session/Objective 来“修复”合法项目。
8. user-facing success 在 DGRS validator 之前出现。
9. Objective checkbox 只是 UI，canonical/export/runtime 不认识。
10. Objective prerequisite False 也会激活。
11. Objective prerequisite 激活后 False 会把 Objective 重新关闭。
12. 旧 Objective 因新字段默认行为改变。
13. Flow Judgment 仍然只在 Session 可用。
14. Task 新 authoring 中出现 Flow Judgment。
15. 菜单没有形成 `逻辑输出 → 条件判断 → 流程判断`。
16. End rename 改变 stable port id 或断 wire。
17. Dialogue Inspector 顺序仍和节点参数相反。
18. 任何 0.3.1.x 高风险 Studio 行为回归。
19. Codex 只靠 unit test 声称动态 UI 已通过。
20. Codex 自行宣布 Studio frozen / USER_ACCEPTED。
21. B 版本 Minecraft UI / 铜币 / 收纳箱问题被混入 A 施工。

---

# 19. Recommended Work Order

## Phase 0 — Read / Baseline

读 `0.3.1.5审计.docx`、最新 0315 PLAN/report；记录实际 HEAD/dirty state；build current 0315 baseline。

## Phase 1 — Export Failure Reproduction

第一优先：

```text
reproduce WP-F
capture first failure
confirm root cause
```

此阶段不急着改 `.dgrs`。必须先知道当前 folder export 为什么失败。

## Phase 2 — DGRS contract against actual exporter

看清当前 package layout 后，锁定 DGRS v1 layout、manifest/version，实现 writer/reader/validator，先让 package codec tests 通过。

## Phase 3 — Fix WP-F + integrate DGRS

把修复后的正常 export pipeline 接到：

```text
compile
→ validate
→ staging
→ DGRS
→ reopen validation
→ final commit
```

先打通 P0 end-to-end。

## Phase 4 — WP-E Objective prerequisite

顺序建议：schema/default → dynamic port → serialization → Runtime activation → persistence → DGRS → Studio UI，避免先做假的 checkbox。

## Phase 5 — WP-C Flow Judgment scope/order

复用 0315 stable node，只扩 scope、修 order、验证 Story runtime。

## Phase 6 — WP-A / WP-B

完成两项 contained Studio consistency fix。

## Phase 7 — Full automated regression

运行 Core、WPF、Java formatting/compile、existing probes、new DGRS/export/objective probes。

## Phase 8 — Final Release EXE

生成最终权威 EXE，记录 final hash。

## Phase 9 — Same-hash live acceptance

执行 Section 15 + 17 matrix。

## Phase 10 — Re-read original audit

重新打开 `0.3.1.5审计.docx`，逐条对照 1–6，不能只按 Work Package 自己总结。

## Phase 11 — Historical regression gate

最后再跑高风险 0.3.1.x regression，确认收官修改没有破坏旧功能。

---

# 20. Expected Source Hotspots — Reconnaissance Only

Codex 必须以当前实际源码为准。可能涉及但不限于：Studio Shell/Export command、Canonical graph node definition registry、CanonicalNodeInspectorViewModel 或 current equivalent、Story/Session/Task authoring scope definitions、Dialogue node property definitions、Session End projection、Objective canonical model/runtime、Story package compiler/validator/filesystem writer、Studio Core/WPF tests、Java Runtime/package probes。

**这不是授权重写这些区域。先找真实入口，改最小 coherent set。**

---

# 21. Commit / Branch Guidance

目标分支：

```text
codex/0.3.2.0_A
```

推荐按真实依赖分组，不要求机械照抄：

```text
fix(export): repair valid story package export
feat(package): add dgrs v1 container and validation
feat(task): add objective prerequisite activation gate
fix(graph): extend flow judgment scope and menu ordering
fix(studio): sync session end display label
fix(studio): align dialogue inspector field order
test: lock 0.3.2.0_A package and regression contracts
docs: record dgrs v1 and release candidate evidence
```

避免 giant opaque commit，也不要制造几十个无意义碎片提交。

---

# 22. Final Development Report Requirements

## 22.1 Baseline

记录 branch、baseline branch、baseline HEAD、baseline dirty state、final HEAD、build configuration。

## 22.2 Per Work Package

A–F 每项：Observed problem、Reproduction、Root cause、Files changed、Implementation、Compatibility impact、Automated verification、Release EXE verification、Status。

## 22.3 Export Root Cause — mandatory dedicated section

必须单独写 `0.3.1.5 为什么合法项目导出失败`，包含 first failing stage 与真实 root cause。

## 22.4 DGRS v1 actual contract

必须报告 physical container、format/version、manifest path、required entries、actual entry layout、writer transaction、validator behavior、compatibility policy，并附一个成功样例的 filename、size、SHA-256、entry listing、manifest summary、validator PASS。

## 22.5 Final EXE

记录 path、ProductVersion、FileVersion、size、SHA-256。

## 22.6 Regression

逐项列出 Section 16 fresh result，不得写“历史功能应该没问题”。

## 22.7 Blocked / User verification

无法在 agent 环境真实证明的内容必须写 `BLOCKED` 或 `NEED_USER_VERIFICATION`，尤其 exact user audit project 若未提供给 Codex。

## 22.8 B boundary

报告明确写：

```text
0.3.2.0_B issues were intentionally not implemented.
```

并列出铜币/金粒、收纳箱左下角提示、游戏内 Task UI，防止后续 handoff 丢失。

---

# 23. Definition of Done

`0.3.2.0_A` 只有满足以下全部条件，才可以被 Codex 称为 `Release Candidate`：

- 6 个 A Work Package 全部 IMPLEMENTED；
- WP-F root cause 已确认并修复；
- 合法 representative Story 在 final Release EXE 中真实导出成功；
- final output 是一个 `.dgrs`；
- `.dgrs` 有明确 DGRS v1 format/version contract；
- `.dgrs` 能 reopen + validate；
- Objective prerequisite 不是 UI fake，而是 canonical/runtime/export 真实语义；
- Flow Judgment 在 Story Flow + Session authorable；
- Flow Judgment 在 Task 不 authorable；
- exact menu order 为 `逻辑输出 → 条件判断 → 流程判断`；
- Session End visible Flow Input 同步 DisplayName；
- Dialogue Inspector 为角色在上、文本在下；
- Core/WPF/Java 相关 fresh suites PASS；
- final Release EXE 已真实运行；
- 所有 live evidence 使用同一 final EXE hash；
- 0.3.1.x 高风险 regression 无 P0/P1 failure；
- 原 `0.3.1.5审计.docx` 在最终阶段被重新逐条检查；
- B scope 未被施工；
- final report 没有伪造 USER_ACCEPTED。

最终 Codex 允许的最高状态：

```text
0.3.2.0_A Release Candidate
IMPLEMENTED
AGENT_VERIFIED
Studio NOT FROZEN
Awaiting USER_ACCEPTED
```

然后由用户人工验收。只有用户明确接受后，才允许：

```text
USER_ACCEPTED
Studio FROZEN
Proceed to 0.3.2.0_B
```

---

# 24. User Final Acceptance Checklist

## Check 1 — End display

```text
改 Session End 显示名
→ Story Flow 正确
→ 会话图 End 输入端口也正确
```

## Check 2 — Dialogue

```text
点【台词】
→ Inspector 角色在上
→ 文本在下
```

## Check 3 — Flow Judgment

```text
Story Flow：可添加
Session：可添加
Task：不可添加
且：逻辑输出 → 条件判断 → 流程判断
```

## Check 4 — Objective prerequisite

```text
OFF：无 Logic Input
ON：出现说明 + ◆ 前置条件
```

并可用前一 Objective/Logic True 触发后一 Objective 激活。

## Check 5 — Real export

用真实审计项目：

```text
Export
→ 成功
→ 一个 .dgrs
→ 无最终 folder
```

## Check 6 — Reopen package evidence

至少用内置 validator/tooling 验证 DGRS format/version、entries、canonical package validity。

如果这 6 项都由用户确认，再讨论“冻结 Studio”。

---

# 25. Short Codex Instruction

> Implement `0.3.2.0_A` as the final Studio freeze-candidate release from the actual latest `codex/0.3.1.5` HEAD. Read `0.3.1.5审计.docx` in full before coding and again at final acceptance. Fix exactly six A-scope items: sync Session End DisplayName into the internal End Flow Input label without changing stable port identity; order Dialogue Inspector as 角色 then 文本; make `流程判断` authorable in Story Flow and Session but not Task, with exact Logic-menu order `逻辑输出 → 条件判断 → 流程判断`; replace user-facing folder story packages with one versioned `.dgrs` v1 compressed container using the existing package payload/layout as much as possible; add Objective `前置条件` as a real optional Logic-input activation gate with legacy default OFF and sticky activation semantics; and reproduce/root-cause/fix the valid-project export failure. Export is P0: success means compile/validate → build DGRS → reopen/validate → atomically commit one final `.dgrs`. Do not suppress validation, omit data, or fall back to folder export. Re-run high-risk `0.3.1.x` regressions against the final Release EXE. Do not implement Minecraft Task UI, copper/gold-nugget cleanup, or storage-chest HUD work; those belong to `0.3.2.0_B`. Codex may deliver `IMPLEMENTED / AGENT_VERIFIED / Release Candidate`, but only the user may declare `USER_ACCEPTED`, freeze Studio, and authorize `0.3.2.0_B`.
