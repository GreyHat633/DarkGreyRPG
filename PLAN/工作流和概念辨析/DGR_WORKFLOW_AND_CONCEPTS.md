# DarkGrey RPG — 工作流与核心概念

> **文档定位：长期项目认知文档**  
> 本文用于解释 DarkGrey RPG（DGR）这个产品**如何被作者使用**、Studio 与 Minecraft Runtime **如何分工**，以及开发中最容易混淆的概念。  
> 本文不是某个版本的施工 PLAN，也不是测试清单；版本级实现与验收要求应放在对应的 `PLAN/**` 文档中。

---

# 1. DGR 是什么

DarkGrey RPG 不是“给 Minecraft 原版实体直接套任务条件”的系统。

DGR 的核心工作方式是：

```text
Studio
负责定义 RPG 语义与流程
        ↓
Story / Actor / Actor Group / Item / Item Group / Session / Task / Graph
        ↓
导出 Story Package（.dgrs）
        ↓
Minecraft Runtime
加载并注册这些作者定义
        ↓
Nominator
把 Minecraft 世界中的真实对象绑定到 DGR 已定义的身份
        ↓
玩家正常游戏
        ↓
击杀 / 拾取 / 交互 / 进入区域等 Minecraft 事件
        ↓
DGR Identity Resolution
        ↓
Story / Session / Task Runtime 根据 Studio 定义推进
```

可以把 DGR 理解成两层：

```text
作者定义层（Studio）
    定义“这个 RPG 世界里有什么、它们之间如何运作”

运行世界层（Minecraft）
    决定“当前世界中的哪个真实对象承载这些定义，以及玩家当前进行到了哪里”
```

**Studio 定义语义，Minecraft 承载语义。**

---

# 2. 标准使用工作流

## 2.1 在 Studio 中创作

作者首先在 Studio 中创建项目与 Story，然后定义这个 Story 需要的资源。

常见资源包括：

```text
Actor
Actor Group
Item
Item Group
Session
Task
```

然后通过 Graph 组织它们的关系和执行顺序。

一个典型创作过程是：

```text
创建 Story
    ↓
定义角色 / 角色组
    ↓
定义物品 / 物品组
    ↓
制作 Session
    ↓
制作 Task
    ↓
在 Story Flow 中放置 Session / Task 等资源
    ↓
连接 Story Flow
    ↓
配置 Session Graph / Task Graph
    ↓
验证
    ↓
导出 .dgrs
```

这里最重要的一点是：

> **RPG 身份和逻辑先在 Studio 中定义。**

Minecraft Runtime 不应该为了让某个测试更容易通过，反过来要求 Studio 临时创建新的 Actor、Item 或 Task。

---

# 3. `.dgrs` 在整个系统中的位置

`.dgrs` 是 Studio 向 Minecraft Runtime 交付 Story 定义的 Story Package。

它携带的是：

```text
Story 定义
Actor / Actor Group 定义
Item / Item Group 定义
Session 定义
Task 定义
Graph / Membership 等运行所需定义
```

它不是：

```text
Minecraft 世界存档
玩家任务进度
已经被指名的实体
已经被指名的物品
某个玩家当前的 StoryInstance
```

因此应明确区分：

```text
.dgrs
= Definition Package / 交付包

Minecraft SavedData / World State
= Runtime State / 运行状态
```

`.dgrs` 决定“有哪些定义可以运行”；世界数据决定“这些定义当前在这个世界里运行到了哪里”。

---

# 4. Actor 与 Minecraft Entity 不是同一个概念

这是 DGR 最重要的概念边界之一。

## 4.1 Actor 是作者定义的 RPG 身份

DGR 中的 Actor 有两种主要形态。

### Individual Actor

表示一个具体、唯一的角色身份：

```text
角色
→ NPC_ID
```

例如：

```text
酒馆老板
NPC_ID = tarven_boss
```

这里的 `tarven_boss` 表示 DGR 世界中的一个具体角色身份。

### Collective Actor

表示一类角色或一个角色集合：

```text
角色组
→ Group_ID
```

例如当前项目中的：

```text
史莱姆
Group_ID = slimes
```

`slimes` 表示 DGR 作者定义的一组角色身份。

它并不等于 Minecraft 的 `EntitySlime` 类，也不等于 Forge/Minecraft 的实体注册名。

---

# 5. Minecraft Entity 是 Actor 的“宿主”

Minecraft Entity 是世界里实际存在的对象，例如：

```text
牛
猪
僵尸
原版史莱姆
CustomNPC+ NPC
其他 Mod Entity
```

这些对象本身并不会自动成为某个 DGR Actor。

DGR 需要通过身份映射知道：

> “这个 Minecraft 实体，在当前 RPG 世界中应该被当成谁？”

这就是 Nominator 的职责。

例如：

```text
Minecraft 牛
        ↓ Nominator
Group_ID = slimes
```

从 DGR 的角度看，这只牛现在属于 `slimes` 角色组。

所以：

```text
Minecraft Entity Type
≠
DGR Actor Identity
```

实体长什么样、原版是什么类型，不决定它在 DGR 中是谁。

---

# 6. NPC_ID 与 Group_ID 的区别

二者都属于 Actor identity，但语义不同。

```text
NPC_ID
= 一个具体的 Individual Actor

Group_ID
= 一个 Collective Actor Group
```

例如：

```text
NPC_ID = tarven_boss
```

表示“酒馆老板”这个具体角色。

而：

```text
Group_ID = slimes
```

表示“属于史莱姆这一组的角色”。

因此一个要求“消灭 3 个史莱姆”的 Task Objective，如果作者在 Studio 中选择的是：

```text
Group_ID = slimes
```

它表达的是：

> 击杀 3 个拥有 DGR identity `slimes` 的实体。

它不是在表达：

```text
击杀 3 个 Minecraft EntitySlime
```

更不需要为了这个任务再创建一个：

```text
NPC_ID = slime
```

---

# 7. Nominator 是 Binding Tool，不是 Resource Editor

Nominator 负责的是：

```text
Minecraft Host Object
        ↕
DGR Authored Identity
```

对于实体：

```text
Minecraft Entity
→ NPC_ID / Group_ID
```

对于物品：

```text
Minecraft ItemStack / Item definition
→ Item_ID / Item Group_ID
```

Nominator 不负责：

```text
创建 Actor
创建 Actor Group
创建 Item
创建 Item Group
创建 Story
修改 Task
修改 Story Flow
```

所以正确方向永远是：

```text
Studio 已经定义好 Resource
        ↓
Minecraft 中选择真实 Host Object
        ↓
Nominator 建立绑定
```

而不是：

```text
Minecraft 测试时缺一个方便的对象
        ↓
回到 Studio 新建 Resource
```

如果 Runtime 无法消费已经正确导出的 Resource，应优先检查 Runtime / Package / Binding 链路，而不是改作者数据迎合 Runtime。

---

# 8. Item 与 Minecraft Item 也不是同一个概念

DGR Item 同样是作者定义的 RPG 身份。

## 8.1 Individual Item

```text
物品
→ Item_ID
```

例如：

```text
任务信物
Item_ID = quest_token
```

## 8.2 Item Group

```text
物品组
→ Group_ID
```

表示一组作者定义的物品身份。

Minecraft 中实际的 ItemStack 只是这些身份的宿主或匹配对象。

例如完全可以：

```text
minecraft:stick
        ↓ Nominator
Item_ID = quest_token
```

之后 Runtime 可以把符合该绑定定义的 ItemStack 识别为 `quest_token`。

因此：

```text
Minecraft Item Registry ID
≠
DGR Item_ID
```

不要因为 Minecraft 中某个物品注册名和 DGR Item_ID 恰好相似，就把两者当成同一个身份系统。

---

# 9. Task Objective 匹配的是 DGR 语义

Canonical Task Runtime 处理的是作者在 Studio 中配置的 Objective 语义。

## 9.1 `kill_entity`

正常 DGR authoring 下，目标应来自 Actor identity：

```text
NPC_ID
或
Actor Group_ID
```

例如：

```text
objective_type = kill_entity
entity = slimes
required = 3
```

正确理解：

> 击杀 3 个解析结果中包含 `slimes` 的实体。

不是：

> 击杀 Minecraft 原版史莱姆类。

## 9.2 `interact_actor`

目标同样是 Actor identity。

例如：

```text
actor_id = tarven_boss
```

表示：

> 与当前解析为 `tarven_boss` 的 Minecraft Entity 交互。

## 9.3 `collect_item`

目标来自 Item identity：

```text
Item_ID
或
Item Group_ID
```

Runtime 应根据 Item identity mapping 判断真实 ItemStack 是否满足该 DGR 目标。

---

# 10. Minecraft 原生注册身份只是底层兼容信息

Runtime 可能为了兼容或底层事件归一化而认识：

```text
minecraft:slime
minecraft:cow
minecraft:stick
```

这些是 Minecraft / Forge 层的 registry identity。

它们与 DGR authoring identity 属于不同层：

```text
minecraft:*
= Host / compatibility / low-level event identity

NPC_ID / Actor Group_ID
Item_ID / Item Group_ID
= DGR authored semantic identity
```

不能因为 Runtime 能看见 `minecraft:slime`，就反推出 Studio 中的“史莱姆角色组”应当等于 `minecraft:slime`。

正常 DGR 工作流始终以 Studio 定义的身份为语义中心。

---

# 11. 一个用于理解身份体系的例子

下面只是概念示例，用于说明“Host Type 与 DGR Identity 分离”，不是规定所有项目必须这样测试。

Studio 中已有：

```text
史莱姆
Group_ID = slimes
```

Task：

```text
消灭 3 只史莱姆
Target = slimes
```

Minecraft 世界中：

```text
A：未指名的原版史莱姆
B：通过 Nominator 加入 slimes 的牛
C：通过 Nominator 加入 slimes 的猪
```

DGR 语义应当是：

```text
杀 A
→ A 没有 DGR identity slimes
→ 不推进 target=slimes

杀 B
→ B 解析出 Group_ID=slimes
→ 推进

杀 C
→ C 解析出 Group_ID=slimes
→ 推进
```

这个例子说明：

> DGR 判断的是作者定义的身份，不是 Minecraft 对象原本的外观或类型。

---

# 12. Story 与 StoryInstance 不是同一个概念

Studio 中的 Story 是一个定义：

```text
Story Resource / Story Definition
```

它描述：

```text
Story Flow
使用哪些 Session / Task
入口条件
逻辑关系
```

当某个玩家在 Minecraft 中真正进入这个 Story 时，Runtime 才创建或恢复：

```text
StoryInstance
```

所以：

```text
Story 已加载
≠
某个玩家正在运行这个 Story
```

“已加载”表示 Runtime 当前拥有这个 Story Definition。

“正在运行”表示某个玩家存在对应 StoryInstance，并且 Instance 有当前状态。

这两个状态不得混用。

---

# 13. Session / Task Resource 与 Story Flow Placement 不是同一个概念

Studio 中：

```text
Session
Task
```

首先都是独立 Resource。

当作者把它们放到 Story Flow 中时，出现的是：

```text
Session Placement
Task Placement
```

Placement 表示：

> “这个 Story Flow 在这里使用这个 Resource。”

它不是重新创建一份新的 Resource。

因此：

```text
删除 Placement
≠
删除 Resource
```

而删除 Resource 时，所有引用它的 Placement 才需要被处理。

这也是为什么 Runtime 中 TaskInstance 的身份通常不仅需要 Task Resource ID，还需要知道它属于哪个 StoryInstance、哪个 Placement。

---

# 14. Definition、Placement、Instance 三层要分开

DGR 中经常同时存在三层概念：

```text
Definition
作者定义“它是什么”

Placement
作者定义“这个 Story 在哪里使用它”

Instance
Runtime 记录“某个玩家当前这一次运行到了哪里”
```

例如 Task：

```text
Task Resource
= “清理酒馆周边”这份任务定义

Task Placement
= Story Flow 中使用这份任务的那个节点

TaskInstance
= 某个玩家在某个 StoryInstance 中这一次任务的运行状态
```

不要把其中任意两层混成一层。

---

# 15. Task Graph 决定状态，UI 只展示 Runtime 结果

Task Graph 决定 Objective 的逻辑关系，例如：

```text
Objective A 完成
        ↓ Logic
Objective B 前置条件成立
        ↓
Objective B 激活
```

Runtime 维护 Objective 状态：

```text
INACTIVE
ACTIVE
COMPLETED
```

UI 的职责不是重新分析 Graph 来猜“现在是第几阶段”。

正确关系是：

```text
Task Graph
    ↓
Task Runtime
    ↓
Active Objectives
    ↓
UI 显示
```

也就是说：

> **Runtime 决定什么是 Active，UI 只投影当前状态。**

---

# 16. Graph 层级与职责

DGR 的主要图结构可以理解为：

```text
图谱
→ 项目 / Story 之间的高层关系

Story Flow（流程图）
→ 一个 Story 的主执行流程

Session Graph（会话图）
→ 一次会话内部的台词、选择、条件和结束

Task Graph（任务图）
→ 一个任务内部的 Objective、Logic、前置条件和结算
```

其中：

```text
Story Flow
负责“接下来执行什么”

Session Graph
负责“这段会话内部怎么走”

Task Graph
负责“这个任务内部什么时候激活、完成、结算”
```

不要用 Story Flow 去承担 Task 内部阶段逻辑，也不要让游戏 UI 自己推导 Task Graph。

---

# 17. CustomNPC+ 与 DGR Actor 的关系

CustomNPC+ 是 Minecraft 中可以承载 DGR Actor identity 的一种 Host / Compatibility Surface。

它不是 DGR Actor 本身。

正确理解：

```text
CustomNPC+ NPC
= Minecraft 世界对象

DGR Actor
= Studio 作者定义的 RPG 身份
```

DGR 可以通过兼容层把某个 CustomNPC+ NPC 解析成 Actor，但 Actor 的语义仍然来自 DGR，而不是来自 CustomNPC+ 的类或模板。

DGR 的核心 identity model 不应依赖 CustomNPC+ 才成立。

---

# 18. Authoring Authority 与 Runtime Authority

为避免架构方向颠倒，应明确两个权威边界。

## Studio 是 Definition Authority

负责：

```text
Story 定义
Actor / Group 定义
Item / Group 定义
Session 定义
Task 定义
Graph 语义
Package 输出
```

## Minecraft Runtime 是 Runtime State Authority

负责：

```text
Story Package 加载状态
Nominator binding
玩家 StoryInstance
SessionInstance / TaskInstance
Objective progress
世界中的实际 Entity / ItemStack
持久化 Runtime State
```

简单说：

```text
Studio 决定“规则与身份是什么”
Minecraft 决定“当前世界里谁承载它、现在运行到哪里”
```

---

# 19. 开发时应优先沿真实数据流排查问题

遇到 Integration 问题时，优先按真实链路查：

```text
Studio 定义是否正确
        ↓
DGRS 是否正确携带定义
        ↓
Minecraft 是否正确加载 Package
        ↓
Authoritative Snapshot 是否包含资源
        ↓
Nominator 是否把 Host 绑定到正确 DGR identity
        ↓
Identity Resolver 是否解析到这些 identity
        ↓
Gameplay Event 是否转换成正确 Canonical Event
        ↓
Story / Session / Task Runtime 是否正确消费
        ↓
UI 是否正确投影 Runtime State
```

不要在链路中间出现问题时直接回头“重新设计作者数据”。

例如：

```text
Task target=slimes 没推进
```

应该检查：

```text
目标实体有没有 slimes binding？
Resolver 是否解析出 slimes？
Kill Event 是否携带 slimes？
Task Runtime 是否收到并匹配？
```

而不是先创建一个新的 `NPC_ID=slime`。

---

# 20. 最容易混淆的概念速查表

| 概念 A | 不等于概念 B | 正确理解 |
|---|---|---|
| DGR Actor | Minecraft Entity Type | Actor 是 RPG 身份，Entity 是宿主 |
| NPC_ID | Minecraft Entity Registry ID | NPC_ID 是 Individual Actor identity |
| Actor Group / Group_ID | Minecraft Mob Type | Group_ID 是作者定义的集合身份 |
| DGR Item / Item_ID | Minecraft Item Registry ID | Item_ID 是 RPG 物品身份 |
| Nominator | Studio Resource Editor | Nominator 只建立 Host ↔ DGR Identity 绑定 |
| `.dgrs` | Minecraft World Save | DGRS 是 Definition Package |
| Story | StoryInstance | 前者是定义，后者是玩家运行状态 |
| Session / Task Resource | Story Flow Placement | Placement 只是 Resource 的一次使用位置 |
| Resource | Instance | 一个是定义，一个是运行状态 |
| Story 已加载 | 玩家正在运行 Story | Load State 与 Instance State 不同 |
| Task Objective target | Minecraft class/name | target 是 DGR authoring identity |
| ActiveObjectives | UI 推导的“任务阶段” | Active 状态由 Runtime 决定 |
| CustomNPC+ NPC | DGR Actor | CustomNPC+ 只是可承载身份的 Host |
| `minecraft:*` identity | DGR authored identity | 前者是底层兼容信号，后者是正常 RPG 语义 |

---

# 21. 对开发 Agent / 开发者的解释原则

在对 DGR 不确定时，应先确认现有作者定义和真实数据流，而不是根据显示名、Minecraft 类型或测试便利性自行猜测。

尤其应遵守以下解释原则：

1. **先读取现有 Resource，再决定 Runtime 应如何消费。**
2. **不要根据显示名称推导 ID 类型。** “史莱姆”可能是 Group，不代表存在 NPC_ID `slime`。
3. **不要根据 Minecraft Host 类型推导 DGR identity。** 原版史莱姆不会因为类名像“史莱姆”就自动属于 `slimes`。
4. **不要为了验收方便创造新的 Authoring Resource。** 如果现有 fixture 已经表达需求，应测试 Runtime 是否正确消费它。
5. **出现不确定概念时，优先检查 Studio schema / DGRS / Runtime model；仍无法确认时再询问。**
6. **不要用一个“恰好同名/同外观”的 Host Object 作为唯一证据。** 这很容易让错误的身份映射碰巧通过。

---

# 22. 本文不负责什么

本文只定义长期工作流和概念边界。

以下内容不应该塞进本文：

```text
某个版本具体要改哪些文件
某次 B2 的验收步骤
具体测试世界名称
某个 commit SHA
当前 bug 列表
临时 debug command
UI polish 需求
版本发布日期
```

这些应该进入对应版本的 Construction PLAN / Development Report。

这样可以避免把一次性的施工案例误认为 DGR 永久产品规则。

---

# 23. 最简心智模型

如果只记住一条，应记住：

```text
Studio
定义 RPG 身份与逻辑
        ↓
DGRS
运输这些定义
        ↓
Minecraft Runtime
加载定义
        ↓
Nominator
把真实 Minecraft 对象映射到这些身份
        ↓
Gameplay Events
        ↓
Identity Resolution
        ↓
Story / Session / Task Runtime
推进玩家的运行状态
```

以及：

> **DGR 的语义中心是 Studio 中作者定义的 Resource 与 Identity，而不是 Minecraft 对象原本的类型、名字或贴图。**
