# DarkGrey_RPG 0.3.3.0 Construction PLAN

> 状态：施工中；P0 已执行，基线修复与证据见 `0.3.3.0/BASELINE.md`；P1 已完成独立旧节点清理与自动验证；交付见 `0.3.3.0/P1.md`
> 目标版本：`0.3.3.0`
> 基线分支：`codex/0.3.2.4`
> 基线提交：`82c6443238e41ce820f076e09d65d3cb26bb2236`
> 建议施工分支：`codex/0.3.3.0`
> 生成日期：2026-09-12
> 原则：0.3.2.4 Foundation Freeze 继续成立；0.3.3.0 只做本文明确列出的“旧架构清理 + 增量功能”，不得借机重构已经冻结的基础框架。

> 2026-09-12 用户澄清（优先于本文原稿）：只删除旧的独立触发器节点；现用【开始】节点的配置及启动逻辑不得改动。Start.triggers、repeat_policy、NPC/区域/Logic 启动、开始配置的既有兼容读取均保留。原稿中要求删除这些内容的段落作废；P1 不再存在启动契约阻塞。

---

## 0. 版本目标

0.3.3.0 完成四件事：

1. **清除旧独立 Trigger 节点及其专属执行路径，保留现用 Start 启动配置。**
2. **保持 Task Graph = 纯 Logic Graph，同时补齐任务描述、Objective、阶段奖励和复用能力。**
3. **将 Story Flow【动作】收敛为语义严格的【执行】，补齐 RPG 常用原子执行。**
4. **建立第一代 Presentation / Media：Actor 头像、台词配音、Session【音乐】、Session【画面】、Story【标题】以及统一媒体存储、打包、分发与缓存基础。**

目标结构：

```text
Story Flow
├─ Flow 驱动
├─ 【执行】= 瞬时原子命令
├─ 【任务】= Task Resource 聚合
├─ 【会话】= Session Resource 聚合
└─ 【标题】= Story 层一次性演出效果

Task
├─ 纯 Logic
├─ Objective
├─ Logic
├─ 【奖励】= 一次性报酬规则 / Logic Sink
└─ 【结算】= Task 最终完成边界

Session
├─ Flow 驱动
├─ 【台词】
├─ 【选择】
├─ 【画面】
├─ 【音乐】
└─ 【结束】
```

---

# 1. 不可破坏的硬约束

## 1.1 0.3.2.4 Foundation Freeze

不得借新增功能顺手改写：

- Canonical identity 语义；
- NPCID / ItemID / GroupID；
- Entity / Item Nominator；
- Canonical Task server-authoritative + client read-only cache；
- Session ACTIVE / death / reconnect 基础生命周期；
- aggregate/boundary 基础模型；
- 已稳定网络 route/discriminator，只能增量增加必要消息；
- 0.3.2.4 Utility Window 行为。

如必须触及，只允许最窄增量扩展。

## 1.2 Task Graph 永远保持纯 Logic

```text
Task internal graph:
✓ Logic
✗ Flow
✗ Story【执行】
✗ Flow Event
✗ Flow In / Flow Out
```

不得为了阶段奖励给 Objective 增加 Flow 输出；不得让【奖励】变 Flow 节点。

## 1.3 不保留废弃架构兼容 Runtime

项目尚未公开发布，因此：

```text
旧数据需要转换 → 一次性开发迁移
旧 Runtime 架构 → 删除
```

禁止新旧双轨、compatibility-only Runtime、永久 fallback。

重点适用于：

- 旧独立 Trigger 节点（不包括 Start 配置）；
- 独立 `enter_story` 节点（不包括 Start 中的同名配置）；
- Session【旁白】；
- 当前测试数据。

## 1.4 不新增模糊用户可见 ID

禁止：

- AssetID
- ImageID
- MusicID
- BuffID
- RewardID

媒体只使用：

- **媒体指纹 / 资源指纹**：内部内容寻址、去重、完整性校验；
- **媒体引用**：资源到媒体的内部引用。

## 1.5 本版本不做 Region / Environment

【区域到达】只是 Task Objective。不得创建 Region Resource、Environment Resource、区域优先级或全局 BGM 区域仲裁。

## 1.6 【环境留声机】延期

0.3.3.0 不做 Gramophone Block/TileEntity、红石播放、世界音频范围仲裁。媒体基础设施只需保证未来可扩展。

## 1.7 术语

继续严格使用：

- `ID释放`
- `实体解绑`
- `物品解绑`
- `实体指名`
- `物品指名`

0.3.3.0：

- Story 原【动作】 → **【执行】**
- Task → **【奖励】**
- **BUFF给予**
- **MOD扩展**
- **媒体指纹 / 资源指纹**
- **媒体引用**

---

# 2. 已确认源码锚点

基于 `codex/0.3.2.4 @ 82c644...`。

## 2.1 GraphNodeDefinitionRegistry

`studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/GraphNodeDefinitionRegistry.cs`

当前：

- Story `action` 显示名【动作】，Flow-only，`flow_in/flow_out`；
- Story 仍残留 `interact_actor`、`enter_region`；
- compatibility-only `enter_story` 仍存在；
- `ForAuthoringScope()` 只是隐藏部分旧节点，并未删除；
- Session 仍有 `narration`；
- Task `objective / and / or / not / logic_input / logic_output / settle` 为 Logic-only；
- Task 仍有 compatibility-only `activate`。

## 2.2 CanonicalStoryActionSchema

`studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/CanonicalStoryActionSchema.cs`

当前：

```text
node type = action
give_item
give_xp
send_message
```

现有 `give_item/give_xp` 校验为正数。

0.3.3.0 优先保留 persisted node type `action`，只改作者显示名为【执行】，再窄增量扩展类型与 signed delta。

## 2.3 CanonicalTaskObjectiveSchema

`studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/CanonicalTaskObjectiveSchema.cs`

当前：

```text
kill_entity
collect_item
interact_actor
```

并已有：

- `description`
- `required`
- `entity`
- `item`
- `metadata`
- `actor_id`
- prerequisite Logic input
- completion Logic output `完成`

0.3.3.0 增加：

```text
submit_item
reach_region
```

## 2.4 StoryStartSchema

`studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/StoryStartSchema.cs`

当前仍带完整旧 Trigger Contract：

- `triggers`
- `interact_actor`
- `enter_region`
- `logic`
- legacy `enter_story`
- trigger properties/ports
- repeat policy
- region dimension/x/y/z/radius
- trigger validation/read

上述 StoryStartSchema 为现用【开始】配置，本版本保持不变。仅删除独立节点的定义及其专属路径。

## 2.5 ActorResource

`studio/src/DarkGreyRPG.Studio.Core/Actors/ActorResource.cs`

当前 `CurrentSchemaVersion = 3`，无 portrait/media 字段。本版本做窄范围 Actor schema extension。

## 2.6 StoryPackageExporter

`studio/src/DarkGreyRPG.Studio.Core/Packaging/StoryPackageExporter.cs`

已有 selected Story → Session/Task/Actor/Item 依赖收集和 staging。媒体应扩展这套“可达依赖”，禁止打包整个项目 media store。

## 2.7 Task aggregate boundary

- `studio/src/DarkGreyRPG.Studio.Core/Graphs/Resources/CanonicalAggregateNodeFactory.cs`
- `studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/AggregatePortProjection.cs`

当前：

- Task 必须恰有一个 `settle`；
- settle 的 Logic input 被父级 Story aggregate 投影为 Flow boundary；
- Task `logic_output` 投影为 Logic output。

这套基础不因【奖励】重写。

---

# 3. 开工前设计 Gate

施工人员不得自行替用户决定以下问题。

## DESIGN-GATE-A：物品提交如何触发

已冻结：

```text
持有足够
→ 原子扣除 required
→ Objective COMPLETE

不足
→ 不扣任何物品
→ 保持未完成
```

未冻结：交互 Actor、Task UI 提交、自动提交或其他机制。

**Phase 2 `submit_item` Runtime 前必须关闭。**

## DESIGN-GATE-B：collect_item 是否锁存

推荐但未单独最终确认：

```text
当前持有 >= required
→ COMPLETE
→ 后续丢弃/消耗不回退
```

Phase 2 前用明确 test vector 冻结。

## DESIGN-GATE-C：reach_region 坐标采样

产品语义已定为同维度 + XYZ 轴对齐立方体：

```text
abs(X-centerX) <= Radius
abs(Y-centerY) <= Radius
abs(Z-centerZ) <= Radius
```

未冻结使用连续 `posX/Y/Z` 还是 floor block coordinate。Studio/Runtime 必须统一。

## DESIGN-GATE-D：【标题】blocking/non-blocking

当前倾向非阻塞、新标题覆盖旧标题，但 Phase 8 Runtime 前必须正式冻结。

## DESIGN-GATE-E：Session 音乐死亡/重连

既有 Session 已冻结：

- death UI 覆盖、respawn 恢复当前 node；
- disconnect 持久化 cursor、reconnect 恢复 node。

Phase 7 前必须定义新增音乐/voice presentation 如何与此一致。

## DESIGN-GATE-F：音频转码技术栈

方向：

```text
authoring source
→ DGR runtime playback derivative
→ OGG Vorbis 优先
```

未冻结：

- transcoder 依赖；
- 源格式；
- bitrate；
- duration/size 限制。

禁止把 50MB、192kbps 等讨论值硬写成规格。

---

# 4. 总施工顺序

```text
Phase 0  基线 / Scope Guard / source inventory
Phase 1  Legacy Trigger Purge
Phase 2  Task Description + Objective
Phase 3  Task【奖励】
Phase 4  Story【执行】+ signed delta + BUFF + advanced command
Phase 5  删除【旁白】+ Dialogue + Actor Portrait + Voice authoring
Phase 6  Media Store / Fingerprint / DGRS / Client transfer-cache
Phase 7  Session【音乐】+【画面】+ Voice runtime
Phase 8  Story【标题】
Phase 9  集成 / 恢复 / 性能 / 负面测试
Phase 10 VISUAL_QUALITY_GATE + 用户验收
```

每个 Phase 达到 Exit Criteria 后再进入下一阶段。

---

# 5. Phase 0 — 基线、Scope Guard、Inventory

## 5.1 分支与基线

从精确 SHA：

```text
82c6443238e41ce820f076e09d65d3cb26bb2236
```

创建 `codex/0.3.3.0`。

保留历史 `scripts/verify-0324-freeze.ps1`，**不得为了 0.3.3.0 改弱它**。

新增建议：

```text
scripts/verify-0330-scope.ps1
```

其基准固定为 0.3.2.4 exact SHA，并对 identity/Nominator/utility foundation 等非目标区域产生意外 diff 时 FAIL。

## 5.2 Legacy Trigger inventory

全仓清点：

```text
StoryStartSchema
triggers
enter_region
interact_actor
enter_story
trigger_properties
repeat_policy
trigger evaluator/listener
trigger serialization
trigger diagnostics
trigger tests/probes
docs/examples
TestProject data
```

覆盖 Studio / Runtime / DGRS / tests / docs / fixtures。

## 5.3 基线命令

Runtime：

```powershell
$env:JAVA_HOME='E:\Java\jdk1.8.0_471'
$env:GRADLE_USER_HOME='E:\Java\gradle'
.\gradlew.bat build --offline --no-daemon --no-configuration-cache --max-workers=1
```

Studio：

```powershell
$env:windir=$env:SystemRoot
& 'E:\Java\dotnet-sdk-10\dotnet.exe' test `
  'studio/src/DarkGreyRPG.Studio.Tests/DarkGreyRPG.Studio.Tests.csproj' `
  --configuration Release

& 'E:\Java\dotnet-sdk-10\dotnet.exe' test `
  'studio/src/DarkGreyRPG.Studio.Wpf.Tests/DarkGreyRPG.Studio.Wpf.Tests.csproj' `
  --configuration Release

& '.\studio\package-studio.ps1'
```

同时保留 0.3.2.4 现有 verification/JavaExec probes 基线输出。

## 5.4 Exit

- exact base 可重复 build/test；
- 0330 scope guard 可运行；
- Legacy Trigger inventory 完整；
- 无无关 diff。

---

# 6. Phase 1 — Legacy Trigger Purge

## 6.1 目标（用户澄清后的权威范围）

只删除旧独立 Story 节点 `interact_actor`、`enter_region` 和 compatibility-only
`enter_story`。不得把这些节点与 Start.triggers 的同名 trigger_type 混淆。

## 6.2 必须保留

- StoryStartSchema 的全部现用属性、ports、validation、编辑配置；
- Start.triggers、repeat_policy 及现有读取；
- NPC、区域、Logic、命令启动入口及其配置语义；
- CanonicalStoryStartConfiguration、CanonicalStoryTriggerIndex、区域进入检测；
- Task Objective `interact_actor`；
- 开始节点相关测试和 probes。

## 6.3 删除范围

- 独立节点 registry、author/load/compatibility 接受路径；
- 专属的中途 Actor/Region wait、resume 和跨 Story enter_story 执行；
- 专属的节点投影、迁移生成、引用处理和旧节点测试；
- 当前作者 fixture 中若存在独立旧节点，做一次性开发迁移，不改 Start 配置。

## 6.4 验证

- 三种独立节点不能创建、加载或通过 compatibility mode 接受；
- DGRS/Runtime 拒绝这些独立节点；
- 开始配置保存/重开不变，NPC/区域/Logic/命令启动回归通过；
- Task interact_actor 与 Task pure Logic 回归通过；
- Scope guard 精确区分节点 type 与 Start trigger_type，不能对同名单词全局禁用。

## 6.5 Exit

```text
LEGACY_STANDALONE_NODE_DEFINITIONS=0
LEGACY_STANDALONE_NODE_EXECUTION_PATHS=0
START_CONFIGURATION_UNEXPECTED_DIFF=0
```

---

# 7. Phase 2 — Task Description + Objective

## 7.1 Task Description

明确分层：

```text
DisplayName = 任务名称
Task Description = 整体背景/说明
Objective Description = 单个目标文字
```

优先做 Task-specific metadata/schema extension，不随手污染所有 `GraphResourceEnvelope`。

Player UI：

```text
任务名称

任务整体说明

任务目标
□ ...
□ ...
```

## 7.2 Objective 最终集合

```text
kill_entity
collect_item
submit_item
interact_actor
reach_region
```

### collect_item

- 检查**当前 inventory**；
- 非 lifetime historical；
- 不消耗；
- 锁存行为按 DESIGN-GATE-B。

跨多个 slot 正确求和，并遵循既有 Item identity/matching。

### submit_item

等 DESIGN-GATE-A 后实现。

原子语义：

```text
check
↓
不足 → remove nothing / incomplete
足够 → remove exact required + COMPLETE + persist
```

必须防止“扣了没完成”与“完成没扣”。

### interact_actor

保持当前 Task Objective 语义。与已删除 Story Trigger 同名代码必须按 domain/scope 精确拆分。

### reach_region

内部类型必须：

```text
reach_region
```

禁止再用旧 `enter_region`。

字段：

```text
dimension_id
dimension_note?   // 仅作者备注
center_x
center_y
center_z
radius
```

判定：

```text
same dimension
AND |X-centerX| <= radius
AND |Y-centerY| <= radius
AND |Z-centerZ| <= radius
```

Radius 是 XYZ 三轴同时扩张的立方体半径。

新增：

```text
/dgr dimension id
```

runtime identity = Minecraft/Forge 1.7.10 integer Dimension ID。

### kill_entity

计数：

- real player direct melee：YES；
- real player-owned projectile：YES；
- automation：NO；
- Forge FakePlayer：NO；
- 玩家先打、环境最终致死：NO。

实现前审计真实 1.7.10 mapped damage source API，不凭现代版本方法名猜。

## 7.3 prerequisite visibility

prerequisite False：

```text
Objective 对玩家完全隐藏
```

不是 disabled/greyed out。

不增加“隐藏直到激活”开关。

## 7.4 Studio

新增/调整：

- Task Description；
- submit_item；
- reach_region；
- dimension note；
- Radius tooltip；
- 中文 validation。

## 7.5 Tests

- 五种 schema round-trip；
- illegal property combinations；
- hidden prerequisite；
- current-inventory collect；
- submit atomicity；
- region dimension mismatch；
- cube 6 faces + 8 corners + radius 0；
- kill melee/projectile/FakePlayer/environment；
- reconnect/persistence；
- client stale revision rejection 回归。

## 7.6 Exit

- Task 仍 100% Logic-only；
- 五种 Objective author/export/load/runtime；
- Task Description 正常；
- prerequisite 无剧透；
- DESIGN-GATE-A/B/C 已关闭。

---

# 8. Phase 3 — Task【奖励】

## 8.1 节点

```text
【奖励】
Logic In only
Flow = 0
```

定义：

> 当 Task 内部条件满足时，对该 Task 实例一次性结算一份 Reward Package。

## 8.2 Reward Package

单节点允许多条：

```text
奖励内容
[物品] 铁剑 +1   [删除]
[物品] 苹果 +16  [删除]
[经验]      +250 [删除]

[ + 添加奖励 ]
```

0.3.3.0 只支持：

- item
- XP

未来货币/声望/技能点等不在本版本。

## 8.3 Signed delta

Item / XP：

```text
positive = give
negative = remove
zero = no-op
```

最终不得低于 0。

多个 Reward Entry 是“一份奖励包”，不是要求一物品一节点。

## 8.4 One-shot

每 Task instance、每 Reward node：

```text
NOT_GRANTED
→ eligible
→ settlement
→ GRANTED
```

持续 True、重登、reload、True→False→True 都不得重复发。

不创建用户可见 RewardID；使用 graph node internal identity/receipt key。

## 8.5 Crash safety

先审计现有 Task “idempotent reward receipts”基础，能复用则复用。

必须避免：

```text
给了物品
→ crash
→ receipt 未保存
→ restart 后整包再给
```

明确 persistence ordering / idempotent recovery，并用 probe 模拟。

## 8.6 与【结算】

```text
【奖励】= 任意 Task Logic 条件下的一次性报酬
【结算】= 整个 Task 的最终完成边界
```

例：

```text
Objective1 完成 → Reward A
Objective2 完成 → Reward B
Objective1 AND Objective2 → Settle
```

奖励不塞进 Objective properties，也不塞进 Settle。

## 8.7 Task 复用验收

同一 Task Resource 在两个 Story placement 中使用时，目标/逻辑/阶段奖励/结算完整随资源复用，不需要在 Story 外另复制 Reward。

---

# 9. Phase 4 — Story【执行】

## 9.1 Rename

作者显示：

```text
【动作】 → 【执行】
```

内部优先保留：

```text
node type = action
```

不要只为文案 churn persisted type。

## 9.2 严格语义

> Story Flow 到达后立即执行、立即结束的一次性原子命令。

必须“瞬时 + 原子 + 无独立长期生命周期”。

不得吞并：

- Session；
- Task；
- Title；
- Music；
- Screen；
- 未来独立演出系统。

## 9.3 0.3.3.0 类型

```text
物品给予
经验给予
BUFF给予
生命给予
玩家传送
消息发送
命令执行（高级）
```

## 9.4 Signed delta 共用规则

```text
delta > 0 = 增加
delta < 0 = 减少
delta = 0 = 不改变
```

### 物品给予

正数增加，负数移除，库存最低 0。遵循既有 DGR Item identity/matching。

### 经验给予

正数增加，负数扣除，总经验最低 0。施工前明确“经验”是 XP points 还是 level，禁止混用。

### 生命给予

```text
newHealth = clamp(currentHealth + delta, 0, maxHealth)
```

不另造“造成伤害”。

若负数生命要经过 Minecraft damage event/armor 等机制，必须在实现前明确；不能把“直接 delta”偷偷变成别的产品语义。

### 玩家传送

至少：

```text
dimension_id
x y z
```

yaw/pitch 是否纳入由实现审计决定。必须 server-authoritative。

### 消息发送

保留现有 `send_message`。

---

# 10. BUFF给予

## 10.1 Vanilla 模式

默认：

```text
MOD扩展 = false
BUFF = Studio 内置 Vanilla 1.7.10 下拉
duration_delta
level_delta
```

作者不需要 Potion ID 或内部名称。

中文 Studio 使用中文可读名称。

## 10.2 MOD扩展

勾选：

```text
☑ MOD扩展
```

才显示：

```text
ModID
BUFF内部名称
duration_delta
level_delta
```

身份：

```text
(ModID, BuffInternalName)
```

不创建 BuffID。

数字 Potion ID 仅 Runtime 诊断/解析，不进入作者持久身份。

## 10.3 delta 规则

DGR 自己读取当前效果并计算：

```text
newDuration = currentDuration + durationDelta
newLevel = currentLevel + levelDelta
```

规则：

```text
duration + / - / 0 = 增 / 减 / 不变
level + / - / 0 = 升 / 降 / 不变

newDuration <= 0 OR newLevel <= 0
→ remove BUFF
```

Studio 等级为用户理解的 1-based；Minecraft amplifier 不暴露。

不得依赖 Vanilla Potion merge 行为代替 DGR 的确定计算。

## 10.4 MOD lookup

按 `(ModID, BuffInternalName)` 解析。

若同一 Mod 内出现重复：

```text
fail loudly
do not guess first
```

## 10.5 查询命令

新增：

```text
/dgr buff list
/dgr buff export
```

`export`：

- 固定 DGR-owned export directory；
- UTF-8 TXT；
- 不允许任意 filesystem path；
- 每行包含：
  - 当前语言本地化名称；
  - BUFF 内部名称；
  - ModID / 来源模组；
  - 当前 Potion ID（诊断 only）。

当前语言无翻译时 fallback 到内部名称/translation key，不 AI 翻译。

Runtime 启动/查询时建立 lookup/cache，禁止每次 BUFF 执行都完整扫描 Potion array。

---

# 11. 高级命令执行

Inspector 默认：

```text
☐ 高级选项
```

关闭时：

- 不出现“命令执行”；
- 不出现 command textbox。

开启：

```text
☑ 高级选项
执行类型：命令执行
命令：[ ... ]
```

第一版：

- 一个节点一条命令；
- 无多行脚本；
- 无宏；
- 无循环；
- 无 DGR 自造脚本语言。

安全约束：

- server-side authoritative；
- 明确 execution identity/permission context；
- client 不得直接发任意命令让 server 执行；
- command 来自 server-installed Story Package 的受信 authoring 内容。

UI 提示：

> 高级命令可能绕过 DGR 标准资源语义；已有原生【执行】能力时优先使用标准类型。

---

# 12. Phase 5 — 删除【旁白】+ Dialogue + Portrait + Voice

## 12.1 删除【旁白】

当前 fixture 一次性：

```text
narration
→ line
speaker_actor_id = null
```

然后删除 registry/menu/schema/runtime/tests 中 narration 独立路径。

不保留 compatibility runtime。

## 12.2 Dialogue 渲染

有 speaker：

```text
角色名
[略增垂直间距]
1px 轻分隔线
「正文」
```

`「」` rendering-only，不写入 authored text。

无 speaker：

- 无名字；
- 无头像；
- 无分隔线；
- 无 `「」`；
- 纯旁白正文。

## 12.3 Actor Portrait Set

Actor schema 增加：

```text
default_portrait_ref
portrait_variants[]
  - name
  - media_ref
```

variant 任意用户命名。

规则：

- default 0..1；
- variants 0..N；
- 无 PortraitID；
- exact binary 用媒体指纹 dedupe。

## 12.4 Line portrait

Line 增加 variant selection：

- 未指定 → Actor default；
- 指定 → 对应 variant；
- **不继承上一句**；
- speakerless → 无 portrait；
- 不做 per-line 任意 XY/scale。

## 12.5 Voice authoring

Voice 是 line property，不是独立节点：

```text
voice_ref?
```

speaker line 和 narration line 都可有 voice。

## 12.6 Actor schema migration

对当前开发数据做一次性 schema migration；最终 canonical Actor model 只保留新版本，不维持双轨作者模型。

## 12.7 Exit

- Session 无 narration node/runtime；
- line 完整表达 dialogue/narration；
- Actor portrait/variant round-trip；
- line variant validation；
- voice ref 可保存；
- Session 基础 Flow 不回归。

---

# 13. Phase 6 — Media Foundation

## 13.1 Project-owned store

导入后：

```text
外部 source
→ copy/import
→ project-owned media store
→ resources use media_ref
```

外部原文件移动/删除不得破坏项目。

UI 可显示 filename/type/dimensions或duration/size/thumbnail，隐藏 fingerprint。

## 13.2 媒体指纹

内部 `MediaFingerprint`（例如 SHA-256）用于：

- exact binary dedupe；
- integrity；
- package reachability；
- client cache key；
- future server dedupe。

不得成为用户 ID。

## 13.3 Undo-safe GC

禁止 last ref 删除后立即删文件。

策略：

- editor session 允许 temporary orphan；
- clean save/close/next-open 等安全边界 mark-and-sweep；
- authoritative saved resources 为 roots；
- unreachable 才物理删除；
- startup recovery 清理 crash orphan；
- temporary orphan 不进 DGRS。

## 13.4 Source 与 Runtime derivative

若 DESIGN-GATE-F 决定转码：

```text
Authoring Source
→ Runtime Playback Derivative
```

DGRS 只包含 runtime derivative，不包含巨大 source master。

即使暂时仅支持 OGG，也不得让资源持久引用回外部 source path。

## 13.5 Reachable media packaging

在现有 StoryPackageExporter 依赖遍历上扩展：

```text
Selected Story
├─ Actor → portraits
├─ Session
│  ├─ line voice
│  ├─ screen images
│  └─ music
└─ reachable media only
```

禁止 `copy project/media/*`。

## 13.6 Package validation

至少验证：

- media refs；
- fingerprint == bytes；
- missing media；
- path traversal；
- corrupt payload；
- duplicate fingerprint physical storage strategy。

## 13.7 Client transfer/cache

原则：

- server-installed Story Package authoritative；
- client 按 fingerprint 获取必要 media；
- client 校验 fingerprint；
- network/disk/decode 不阻塞 render/server tick；
- missing media 不 freeze；
- active playing/preloading media 不被 cleanup。

分类策略：

- Music：最近 unique tracks count-based retention；
- Voice：独立策略，不使用“最近 5 首”；
- Image/Portrait：独立策略。

具体 Music retain count 在实现前冻结，PLAN 不硬编码 5/10。

## 13.8 Transcode gate

关闭 DESIGN-GATE-F 后确定：

- import formats；
- OGG derivative；
- transcoder packaging；
- async conversion；
- error UX；
- duration/size limits。

## 13.9 Tests

- duplicate import dedupe；
- external source 删除后项目仍可用；
- Undo restore；
- safe GC；
- startup orphan recovery；
- only reachable media exported；
- unrelated media excluded；
- corrupt fingerprint reject；
- client verification；
- active cache protected；
- large media I/O 不阻塞主 tick。

---

# 14. Phase 7 — Session【音乐】、【画面】、Voice Runtime

## 14.1 【音乐】

Session-only。

最小属性：

```text
操作：播放 / 停止
音乐
循环
淡入
淡出
```

语义：

- 新音乐替换当前 Session music；
- Stop 清理；
- Session END 清理；
- 不变成 Region/global music resolver。

死亡/重连必须先关闭 DESIGN-GATE-E。

## 14.2 Voice Runtime

```text
line active + voice_ref → play
advance → stop previous
voice end → silence
Session END → stop
```

不自动按文件名匹配。

## 14.3 【画面】

Session-only。

每个节点声明执行后的**完整画面快照**：

```text
A = Alice + Bob
B = Bob
→ Alice 自动消失

C = []
→ clear all
```

每项：

```text
media_ref
normalized position
size
anchor
z/layer
```

禁止：

- ImageID；
- user-facing instance identity；
- HideImage node；
- left/center/right identity slots。

Presets 只预填 transform。

## 14.4 Inspector

推荐：

```text
左：图片/层级列表
右：标准化预览
侧/下：选中项 transform
```

支持 add/remove/drag/resize/reorder。

只读 overlay：

```text
☑ 显示对话框参考
☑ 显示选项框参考
```

overlay：

- 不可编辑；
- 不改 Dialogue 配置；
- 不进入 DGRS；
- 尽量使用真实游戏 UI 比例。

## 14.5 Session END

统一清：

```text
screen composition
music
voice
```

不影响 Vanilla/第三方全局音频。

## 14.6 Tests

- A→B snapshot replacement；
- empty clear；
- layering；
- resize normalization；
- END cleanup；
- music replace/stop；
- voice advance stop；
- narration voice；
- death/reconnect 根据最终 Gate；
- missing/late media 不 crash、不 freeze。

---

# 15. Phase 8 — Story Flow【标题】

## 15.1 Scope

只属于 Story Flow，不属于 Session。

## 15.2 属性

```text
主标题
副标题（可选）
fade_in
stay
fade_out
```

## 15.3 Runtime

Minecraft 1.7.10 无现代 `/title`，DGR 自实现 client HUD overlay。

DESIGN-GATE-D 后冻结：

- blocking/non-blocking；
- replacement；
- death/disconnect clear 行为。

当前推荐但未冻结：

```text
start animation
→ Story Flow immediately continues
next title
→ override current title
```

## 15.4 Tests

- main only；
- title+subtitle；
- timing validation；
- replacement；
- scaling；
- Chinese；
- Story cursor correctness；
- no Session ownership leakage。

---

# 16. Phase 9 — 集成 / 恢复 / 性能 / 负面测试

## 16.1 Server authority

以下全部由 server authoritative state 决定：

- Task progress；
- submit item；
- Reward；
- Item/XP/Health delta；
- BUFF delta；
- teleport；
- advanced command；
- Task completion；
- reward receipts。

Client 只做展示、受控请求、presentation/media cache。

## 16.2 Network

新增 packet 前：

- 审计 discriminator；
- 避免冲突；
- 大媒体不能粗暴塞普通 tiny packet；
- 明确 chunking/size bounds/recovery；
- corrupt/out-of-order transfer 可恢复；
- stale presentation revision 规则不回归。

## 16.3 Persistence / reconnect

覆盖：

- Reward receipt；
- Task progress；
- hidden prerequisite activation；
- Session cursor；
- music/screen/voice 最终 lifecycle；
- title 不污染长期 state，除非最终设计明确要求。

## 16.4 性能禁区

禁止：

- 每 tick 全量扫描 Potion；
- 每 tick 全量扫描 Task media；
- render tick 解码大音频；
- server tick 同步读大文件；
- UI gesture 每帧写盘；
- resize 时重建业务状态。

## 16.5 Legacy final sweep

最终全仓审计：

```text
legacy trigger runtime
enter_story compatibility
Session narration runtime
old authoring menu
old tests claiming compatibility
```

只能在明确 historical docs/PLAN/migration evidence 中残留。

---

# 17. VISUAL_QUALITY_GATE — 硬验收

禁止：

```text
Build PASS + Tests PASS = UI PASS
```

必须：

```text
Functional Pass
↓
Screenshot Pass
↓
Visual Polish Pass
↓
Screenshot Again
↓
User Acceptance
```

## 17.1 必拍 Studio 状态

- Actor 默认头像/表情变体；
- 【台词】speaker/portrait/voice/text；
- 【画面】Inspector；
- layer list；
- preview canvas；
- Dialogue overlay ON/OFF；
- Choice overlay ON/OFF；
- 【音乐】；
- 【标题】；
- 【区域到达】；
- Task Description；
- 【奖励】空 / 多物品+XP；
- 【执行】普通模式；
- negative signed delta；
- 高级选项 closed/open；
- 命令执行；
- 【BUFF给予】Vanilla；
- 【BUFF给予】MOD扩展；
- narrow/large window；
- DPI/scaling。

## 17.2 必拍 Game 状态

- speaker dialogue；
- narration line；
- portrait + dialogue；
- screen + portrait + dialogue；
- choice；
- title；
- Task Description；
- hidden prerequisite；
- phase reward；
- music/voice/session presentation。

## 17.3 视觉标准

任何一项明显失败即 NOT PASS：

- margin 一致；
- label baseline；
- control width；
- grouping/hierarchy；
- 无拥挤；
- 无无意义大空白；
- 中文不裁切；
- tooltip 不挡关键操作；
- checkbox/dropdown/textbox 比例合理；
- resize 合理；
- 不出现明显拼装感；
- 与现有 Studio 视觉语言一致；
- Dialogue 层级清楚；
- divider 不抢正文；
- `「」` 不导致错误换行或存储污染。

每个关键 UI 至少保留一次 Before/After polish 证据。

最终用户视觉审核是 release gate。

---

# 18. 测试矩阵

## 18.1 Runtime build

```powershell
$env:JAVA_HOME='E:\Java\jdk1.8.0_471'
$env:GRADLE_USER_HOME='E:\Java\gradle'
.\gradlew.bat build --offline --no-daemon --no-configuration-cache --max-workers=1
```

运行全部现有 verification JavaExec probes + 0.3.3.0 新增 probes。

重点覆盖现有 canonical：

- graph resource；
- Story runtime；
- Story action configuration；
- Task runtime；
- Task Forge；
- Task instance/persistence；
- Story server service；
- network discriminator；
- package loading。

## 18.2 Studio

```powershell
$env:windir=$env:SystemRoot
& 'E:\Java\dotnet-sdk-10\dotnet.exe' test `
  'studio/src/DarkGreyRPG.Studio.Tests/DarkGreyRPG.Studio.Tests.csproj' `
  --configuration Release

& 'E:\Java\dotnet-sdk-10\dotnet.exe' test `
  'studio/src/DarkGreyRPG.Studio.Wpf.Tests/DarkGreyRPG.Studio.Wpf.Tests.csproj' `
  --configuration Release

& '.\studio\package-studio.ps1'
```

## 18.3 新增测试主题

### Trigger purge
- 旧独立节点 schema/runtime/compat path = 0；Start 配置保持不变。

### Task
- description；
- 五 Objective；
- hidden prerequisite；
- collect current inventory；
- submit atomic；
- cube region；
- player/projectile attribution；
- FakePlayer exclusion。

### Reward
- multi-entry；
- signed item/xp；
- clamp；
- one-shot；
- reconnect/reload；
- crash recovery；
- Task reuse。

### Execute
- item/xp/health signed delta；
- teleport；
- message；
- advanced command；
- unauthorized client negative path。

### BUFF
- Vanilla serialization；
- MOD lookup；
- duration/level +/-/0；
- <=0 remove；
- duplicate fail；
- localized UTF-8 export。

### Dialogue/Actor
- narration migration；
- no narration runtime；
- portrait default/variant；
- no inheritance；
- render-only quotes。

### Media
- fingerprint；
- dedupe；
- reachability；
- corruption；
- source path independence；
- GC/Undo；
- async transfer/cache。

### Presentation
- voice；
- music；
- screen snapshot；
- title；
- Session END clear。

---

# 19. 0.3.3.0 Scope Guard 建议

旧 `verify-0324-freeze.ps1` 继续作为历史冻结证据。

新增 `verify-0330-scope.ps1` 至少输出：

```text
UNEXPECTED_IDENTITY_DIFF=0
UNEXPECTED_NOMINATOR_DIFF=0
UNEXPECTED_UTILITY_WINDOW_FOUNDATION_DIFF=0
UNEXPECTED_LEGACY_COMPAT_ADDITION=0
TASK_FLOW_PORT_COUNT=0
FORBIDDEN_MEDIA_ID_TERMS=0
LEGACY_STANDALONE_NODE_EXECUTION_PATHS=0
NARRATION_RUNTIME_PATHS=0
```

允许变化仅限本文明确需要的：

- Graph definitions/schema；
- Task feature runtime；
- Story execute runtime；
- Session presentation；
- Actor portrait schema；
- media/package/network；
- related UI/tests/docs。

---

# 20. DGRS Contract 原则

新增 persisted 内容预计：

- Task Description；
- new Objective fields；
- Reward package；
- Execute expanded fields；
- Actor portrait/variants；
- line portrait/voice refs；
- Session music；
- Session screen composition；
- Story title；
- media manifest/reachable payload。

硬规则：

1. authoring model 与 runtime model 分离；
2. UI-only state 不进 DGRS；
3. preview overlay toggle 不进 DGRS；
4. source path 不进 DGRS；
5. fingerprint/ref 仅内部；
6. only reachable runtime media；
7. no standalone Legacy Trigger nodes；
8. no Narration node；
9. no compatibility-only duplicates。

---

# 21. 明确 Non-Goals

0.3.3.0 不做：

- Region Resource；
- Environment system；
- Gramophone；
- world audio overlap；
- faction reputation；
- currency；
- skill points；
- reward title；
- optional objectives；
- Task failure；
- abandon/cancel；
- timers；
- escort；
- repeatable/daily；
- party-shared Task；
- waypoint；
- NPC 感叹号；
- arbitrary scripting language；
- multi-line command；
- generalized Actor choreography；
- voice filename auto-match；
- user-facing media IDs；
- full backward compatibility layer。

---

# 22. 推荐 Commit / Phase 边界

```text
P0  baseline + scope guard
P1  legacy trigger purge
P2  task description/objectives
P3  task reward
P4  execute/buff/command
P5  narration/dialogue/actor/voice authoring
P6  media foundation/package/client transport
P7  session music/screen/voice runtime
P8  title
P9  integration/performance/recovery
P10 visual polish + acceptance
```

每一段都要求：

```text
build
tests
relevant probes
scope guard
```

PASS 后再继续。

---

# 23. Definition of Done

只有同时满足以下条件才能标记：

```text
0.3.3.0_CONSTRUCTION=COMPLETE
```

## Architecture

- 旧独立 Trigger 节点完全清除；Start 配置和启动逻辑保留；
- no dual runtime；
- Task pure Logic；
- Reward 属于 Task；
- Execute 属于 Story Flow；
- no Region；
- no user-facing media IDs。

## Functional

- Task Description；
- 5 Objective；
- phased Reward；
- signed resource delta；
- BUFF Vanilla + MOD扩展；
- advanced command；
- Actor portrait variants；
- line voice；
- Session music；
- Session screen；
- Story title；
- media package/distribution。

## Persistence / authority

- server authoritative；
- Reward one-shot + recovery safe；
- Task/reconnect stable；
- no client arbitrary command execution；
- media fingerprint verified。

## Packaging

- only reachable media；
- no authoring source master leakage；
- no external source path dependency；
- no standalone Legacy Trigger node payload；
- no Narration payload。

## Quality

- Runtime build PASS；
- Studio tests PASS；
- WPF tests PASS；
- existing probes PASS；
- new probes PASS；
- `verify-0330-scope.ps1` PASS；
- performance sanity PASS；
- `VISUAL_QUALITY_GATE` PASS；
- final user audit PASS。

用户最终确认后记录：

```text
0.3.3.0_USER_ACCEPTED=YES
```

---

# 24. Codex / Agent 执行纪律

仓库 `AGENTS.md` 要求非平凡开发在 minimal localization 后执行 Delegation Capability Preflight，并在实质实现前完成 Initial Delegation Check；MAIN 负责未解决架构、跨模块决策、review 和 final integration，稳定、边界明确、可验证且不重叠的工作包才适合 WORKER。

0.3.3.0 中：

**MAIN 必须持有：**

- DESIGN-GATE A–F；
- Legacy Trigger inventory/purge ownership；
- Task Reward one-shot/persistence；
- Media contract；
- Session death/reconnect presentation lifecycle；
- package/network integration；
- final review。

**可 bounded delegation：**

- 独立 Studio Inspector；
- 独立 schema tests；
- TXT export formatting；
- media thumbnail/preview helper；
- isolated visual polish；
- isolated probe additions。

不得重复修改 RUNNING Worker 的同一文件/语义区域。Worker 返回后必须 review 并明确 adopt/reject。

---

# 25. 最终施工原则

```text
先冻结语义
↓
数据契约
↓
Runtime
↓
Studio UX
↓
Package / Network
↓
功能测试
↓
截图
↓
视觉打磨
↓
再次截图
↓
用户验收
```

禁止：

```text
先堆 UI
→ 再猜 schema
→ Runtime 被迫适配
→ 最后加兼容补丁
```

0.3.3.0 的成功标准不是“功能多”，而是：

> **在不破坏 0.3.2.4 已冻结基础的情况下，让 Task 成为完整可复用的任务资源，让 Story【执行】拥有清楚且受控的原子命令语义，并建立足够干净的第一代演出/媒体基础。**
