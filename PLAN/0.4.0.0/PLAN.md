# DarkGreyRPG 0.4.0.0 施工 PLAN

**主题：现行 Canonical 单栈收口、历史业务退役与代码精简**  
**文档日期：2026-10-07**  
**状态：清扫范围已获用户确认；待施工。本文不是实现、测试或交付完成报告。**  
**仓库：`GreyHat633/DarkGreyRPG`**  
**开发基线：`codex/0.3.3.7`**  
**本轮重新核对的基线 HEAD：`51582ae8fe8a71214f2306ecd08492129171cff5`**  
**目标版本：`0.4.0.0`；建议施工分支：`codex/0.4.0.0`。已有工作先读取、比较并衔接。**

> **先证明职责已经退役，再将现役调用者脱钩，最后删除旧实现。**
>
> 不保留旧 Dialogue／Quest／legacy Story 业务兼容，不保留原生注册名作为 Task 目标身份的旁路；不借清扫删除现行功能，也不提前建设多版本 Runtime。

**阅读路线：**第 0—3 节确定范围、保护合同和开工条件；第 4—10 节规定各项清扫及审计；第 11—15 节规定施工、验收和交付；第 16—17 节提供固定来源与 Codex 开工指令。

---

## 0. 执行摘要、依据与授权边界

### 0.1 本版交付什么

将 DGR 已经使用的 Story／Session／Task／Actor／Item／Item Group 体系收口为唯一正式作者与运行模型。重点不是删文件数量，而是让旧业务不再通过编辑器、资源仓库、包格式、网络请求、任务匹配或隐式 fallback 进入正式路径。

本版需要完成以下七组工作：

| 工作包 | 交付结果 |
|---|---|
| WP-A 任务身份收口 | Kill／Collect／Submit 的业务目标只接受 DGR 资源身份；Studio、打包、Runtime 解码、事件与库存匹配同步关闭原生注册名旁路。 |
| WP-B 指名器退役收口 | 删除类型级自动归组规则及已被当前统一操作替代的旧写入通道；保留具体实体指名、集体角色、物品精准／模糊匹配。 |
| WP-C Runtime 旧业务退出 | 退出旧 Dialogue／Quest／Story 运行实现，简化中央模型、旧命令和旧 Journal 运输链。 |
| WP-D Studio 旧作者体系退出 | 删除旧 Dialogue／Quest 编辑与 legacy Story 工作区分支；清理中央服务、仓库、草稿、枚举和目录创建残留。 |
| WP-E 旧局部合同退出 | 删除 Choice 旧“已选择”Logic 输出及旧 `give_item` 注册名输入，保护当前条件输入与物品执行语义。 |
| WP-F 格式、命名与资料收口 | 同步清理 Manifest 旧空字段、受影响 schema／codec、旧业务命名和现行说明；不保留第二套读取器。 |
| WP-G 验证与开发交付 | 建立删除证据与测试映射，用同一份实际导出包完成双端验证，更新权威开发产物并如实报告边界。 |

**Task 内部纯 Logic；Task 在父级 Story 上的公开 Flow 结果不因此删除。**“单栈”也不意味着合并 Studio 与 Runtime、服务端与客户端、节点内编辑与 Inspector。

### 0.2 已确认项不再重复请示

用户已对上一轮重新审定的范围表示“没有意见，就按这个来”，并要求生成本 PLAN。因此：

- 类型级自动归组、旧指名写通道的退役方向，以及 Choice 旧 Logic 输出退役，已纳入本轮批准范围。
- 已批准职责所需的调用追踪、脱钩和测试是施工任务，不重新提交“删还是保留”的同一问题。
- Start 历史条目、部分 Normalize／默认值、ProjectOrigin 等**尚未判定项只获准审计**。总确认不等于允许把它们一律删除。
- 若发现批准项实际上承担未被讨论、仍然有效的产品职责，先保留该职责，说明具体用途、调用证据与最小处理方案；只暂停有冲突的那部分，不阻塞无关已批准工作。
- 任何新增功能删除、新平台重构或正式数据破坏，均不由本文件自动授权。

### 0.3 来源优先级

```text
本轮用户确认的清扫方向及此后的明确修订
    > 本 PLAN 的退役范围与现行功能保护合同
    > 0.3.3.6／0.3.3.7 后续修正与当前架构说明
    > 未被后续推翻的历史 PLAN、审计和更新总览
    > 当前代码中的具体实现
    > 助手此前已纠正的推测与中间建议
```

代码说明“现在如何工作”，不能凭现有旧分支反驳已经批准的退役；历史 PLAN 也不能覆盖后续用户修正。本文将源码事实、产品决定和工程约定分别标明。固定源码来源见第 16 节，历史附件索引见同节。

**重要沿革：**0.3.3.0 清的是旧独立触发器，不是整个 Start；0.3.3.1 后续修正取代最初的部分文案和标题规则；0.3.3.6 后续合同允许多结算与零结算；0.3.3.7 后续修正包含任务 40%／60% 布局和新版 AND／OR 编辑。不得倒退。[H01][H02][H03][S02][S03][S04][S05]

### 0.4 这份文档没有执行的动作

编写本文时重新核对了分支 HEAD、AGENTS、当前架构和相关后续修正；沿用本对话上一轮对同一固定提交的代码审计。没有修改仓库、创建分支、运行构建、启动游戏或发布产物。列出的源码入口不是全量调用清单，P0 仍须在实际工作区查证。

本次请求是**生成施工文档**。执行者在用户要求按本文开工后，才进行本地施工；推送、合并、标签、GitHub Release 和成品发布遵守当次明确授权，不从旧轮次继承。

### 0.5 明确不做

不在本版开发多 Minecraft 版本 Runtime、通用跨平台适配层、Dimension 新抽象、跨版本字体系统、命令翻译、通用变量／脚本、Preview／Play Test、新的节点体系、Group Runtime、自动更新器、云协作、监控平台或治理守护。

不顺手升级 Java、Gradle、.NET、WPF、Minecraft、Forge 或第三方依赖。保留扫尾阶段已经完成的构建可移植性与资料成果，不重新施工一次扫尾。

不重写 Git 历史、不做全仓二进制／历史证据减肥、不引入 Git LFS／CI，不以删除比例、代码行数或“零 Legacy 单词”作为目标。不处理 JourneyMap 默认 O 键冲突，不恢复已删除的 Studio Minecraft 占位页。

---

## 1. 本版必须保护的当前合同

### 1.1 项目定位与结构

DGR 当前形态是 **Windows WPF Studio + Minecraft／Forge Java Runtime**。Studio 是高度节点化的故事作者工具；服务端负责实际执行、准入、玩家状态、任务、奖励和权限，客户端负责展示与提交交互。Project 是创作工作区，不是新的玩家运行身份。[S02]

| 对象 | 必须保留的职责 |
|---|---|
| Story | 组合会话、任务、执行与公开边界；管理故事运行与重复资格。 |
| Session／会话 | 台词、选择、音乐、画面及会话结果；不是旧独立 Dialogue 资源。 |
| Task／任务 | 目标、Logic、奖励与结算；内部不得出现 Flow 连线。 |
| Actor／个体角色、集体角色 | 故事中的角色身份；真实实体通过具体对象指名获得身份。 |
| Item／Item Group | 故事物品身份；世界端绑定、精准／模糊匹配负责与真实 ItemStack 对接。 |
| Story Group | 由跨 Story Flow／Logic 关系自动派生的容器；没有独立玩家状态或执行器。 |
| 编辑器组合／注释框 | 画布组织、嵌套、布局与整体操作；不是 Story Group。 |
| `.dgrs`／`.dgrs.g` | 分别为单 Story 与完整关联组容器，复用成员载荷与媒体机制。 |

Story UID 创建后不可变；资源身份为 `Story UID + Kind + Local ID`，普通作者操作使用名称，内部 ID 由 Studio 管理。不恢复 Namespace、Full ID 手填、UID 重生成或名称／文件名回退。

### 1.2 图、运行与任务

| 领域 | 不回退规则 |
|---|---|
| Start | 保留角色交互、进入区域、Logic、流程驱动及当前命令启动；每条流程驱动的稳定入口保持。 |
| Story 活动实例 | 单玩家同一 Story 的活动实例保护不变；所有正式启动入口遵守统一准入，不排队补发 ACTIVE 期间的重入。 |
| Logic 启动 | 保留 False→True 边沿规则，不因重连、重载或完成后清观察值制造新边沿。 |
| 重复资格 | 保留无条件、冷却、定时重复及当前 UI／时区／时间规则；新一轮准入使用当前安装定义，历史摘要保留历史事实。 |
| 连线 | Flow 输出最多一个目标、Flow 输入允许多来源；Logic 输入最多一个来源、输出允许多目标；保留现有环路校验。 |
| 公开端口 | 稳定 port ID、改名、排序、复制与边界投影保持；不能用显示名称或当前索引替代身份。 |
| Task 数量与结果 | 目标／结算无额外最低数量；默认新任务仍按当前工厂创建。允许多结算与零结算；零结算不自动完成。 |
| 结算优先级 | 同时成立时仅 Task Flow 公开输出的显式顺序决定唯一结算结果；Logic 排序不创建执行优先级。 |
| 收集 | 检查当前背包；满足后本 run 内完成不回退，不扣物；不得改为累计拾取或实时撤销完成。 |
| 提交 | DGR 物品／组 + 提交角色 + 数量；真实实体交互、资格复核、足量原子扣除；不足一件不扣。 |
| 奖励与执行 | 正负增量和零值语义不变；任务奖励与结算职责分开；奖励防重、提交事务及恢复不得弱化。 |
| 实体交互 | 保留当前 Story 候选仲裁和限定派发；不能因有多个 DGR 身份而把一次点击无条件广播给全部故事。 |

依据：[H02][H07][H09][S02][S04][S05]。历史中的“唯一结算”“标题一律阻塞”以及旧重复摘要优先规则，不能复活。

### 1.3 作者体验、阅读与表现

节点内属性与 Inspector 共用模型、命令和验证；不能精简成只剩 Inspector。保留资源选择和精确拖放、只读保护、草稿、保存重开、Undo／Redo、组合与 G 手势、搜索及使用位置。

台词保留多句 `pages`、稳定页身份、Enter 在当前句后新建、空句 Backspace、IME 保护与最后剩余数量保护。不是固定“第一句永远不能删”。保留表情差分、头像、语音／音效、自定义显示速度、动态内容块及真实正文冻结。

保留 `DialogueCapacityProfile`／同职责容量工具、作者预警、服务端展开后字节限制、客户端本地分屏和完整原句历史。这四者不是重复实现。显示容量超限不是一律禁止导出；格式错误与真实传输上限另按当前合同处理。[H05][H07][H09]

Choice 保留每选项 Flow 输出、条件 Logic 输入、条件开关、隐藏／置灰及说明、服务端点击复核；只删除旧“已选择”Logic 输出。选择可用性刷新不重播语音、画面、历史或改算同页冻结正文。

【画面】仍表示完整目标画面，转场／平滑保留，不能改成时间轴或通用等待；【标题】保留“播放完再继续流程”选项及当前默认行为。

### 1.4 当前 UI 后续修正

以最新修正而非初版草图为准：[S03][S04]

- 任务列表／详情上下或左右布局为可分配区域 40%／60%，独立滚动和统一绘制命中。
- 任务追踪最多三项、只对未来真正新接任务自动加入；不补旧、不顶替、不复活手动取消；任务通知与追踪各自布局。
- AND／OR 保留当前双入口卡片、标题旁 +／−、直接名称框、稳定端口排序和最少两个输入；不退回初版逐行 −／数量标题。
- 游戏包管理器保留启用、禁用、刷新及当前关闭／改键／视角保存行为。**不恢复后续已移除的页面删除、删除确认或关闭按钮。**
- 游戏三主题、Studio 自身主题、文本字号、音量、历史、平滑滚动与窗口工具能力保留；两端主题不是同一套设置。

### 1.5 引用、包生命周期与数据

Reference 保持来源身份且只读；Import 创建新的本地内容身份；A→已有非空 B 的故事内容迁移是追加复制，不是旧格式迁移。保留源和目标原内容、完整资源／节点映射、单次 Undo，固定不复制 A 的项目级外部线。

Group 仍由 Flow＋Logic 弱连通关系派生。普通资源引用不扩组；通过公开边界联动外部 Group 时保留完整来源闭包，不强制 Import。

所有已安装容器，包括 Disabled，参加实际成员 UID 冲突检查；对称阻断，不先来先得。普通 Disabled 只挡新启动，已有获准实例继续；Conflict／Error 撤销相关执行资格，不能借旧快照继续跑。无关故事、完成历史和防重收据不被顺带清空。[S02][S05]

媒体维持 Story 粒度：真正 Start 后才预热；沿有向 Flow＋Logic 可达关系遍历；最多 3 并发、10 Story 槽位，下载占槽，活动项保护。底层共享媒体、旧 archive 租约、延迟回收、解码和纹理生命周期不是旧业务栈，不删除。

环境留声机保持独立设备与媒体域；物品候选分页不占 Story 媒体槽位。清扫不改变音乐来源、音频后端或两类玩家音量。

---

## 2. 已批准退役清单与判定方法

### 2.1 本轮清单

| ID | 退役职责 | 当前线索／入口 | 处理类型 |
|---|---|---|---|
| CL-01 | 旧 Dialogue 业务定义、运行及专属客户端路径 | `dialogue/*`、旧 Dialogue manager／controller／screen／messages | 现役依赖脱钩后删除 |
| CL-02 | 旧 Quest 业务与旧 Story 执行模型 | `quest/*`、`story/runtime/*`、旧 Story 定义／Loader | 排除现役共享职责后删除 |
| CL-03 | 中央模型中的旧集合、参数、旧命令 | `ProjectSnapshot`、`ProjectRepository`、`CommandDarkGreyRpg`、启动构造 | 收口为当前业务模型 |
| CL-04 | Canonical Task → 旧 Quest Journal 运输链 | `CanonicalJournalService`、`CanonicalTaskLegacyJournalAdapter`、旧 Journal DTO／消息／GUI | 当前调用者改走现行路径，再删除 |
| CL-05 | Task 原生目标身份旁路 | 两端 `GraphResourceAddressCodec`、`nativeTarget`、事件 Normalizer、库存匹配 | 从生产、消费和校验两端同时关闭 |
| CL-06 | 实体种类 → 集体角色的自动归组 | `bindEntityTypeGroup`、`typeGroups`、Resolver 类型规则 | 删除写入、持久读取、消费和专属协议 |
| CL-07 | 已被统一指名操作取代的旧写通道 | `C2SNominatorEntityBind`／`InventoryBind` 等、注册与旧调用者 | 保留必要现役能力后删除旧 mutator |
| CL-08 | Studio 旧作者业务体系 | `Core/Dialogues`、`Core/Quests`、旧 Story workspace／CRUD／draft／fallback | 整条职责清除，不按目录盲删 |
| CL-09 | Choice 旧“已选择”Logic 输出 | `SessionChoiceSchema`、`ValidateLegacyLogicPorts`、Runtime 专属支持 | 只退役该输出，保留新输入与结果 |
| CL-10 | `give_item` 旧 `item + metadata` 形态 | `CanonicalStoryActionConfiguration`、`legacyRegistryItem` 与执行分支 | 删除旧解析与执行 |
| CL-11 | Manifest 的旧 Dialogue／Quest 空列表 | 两端 Manifest、路径遍历、包验证与 fixture | 更新单一当前合同并拒绝退休形态 |

CL-06 与 CL-07 不得只删普通 UI：当前旧后端仍可能被调用或消费数据。CL-08 不得只删 XAML：旧中央服务仍会维持旧概念。CL-05 不得只删 Kill Normalizer：库存匹配与新身份编码同样参与。

### 2.2 不用名称判断新旧

每个拟删除文件或符号先在 `Implementation.md` 的清扫台账登记：

```text
CL 编号／准确文件与符号
旧职责：以前替用户做什么
现行替代：现在由谁承担
源码证据：构造、注册、调用、读写、界面和数据入口
现役依赖：直接／间接／字符串／反射／XAML／测试
处理：删除、先提取复用、改接调用、保留
验证：保留行为的正向测试 + 退役入口的负向测试
结果：已删／已脱钩／保留原因／未关闭问题
```

这是开发记录，不是新增产品管理平台。已经批准的职责不逐文件向用户请示；遇到新职责冲突才集中说明。

### 2.3 五种处理状态

| 状态 | 含义 |
|---|---|
| `REMOVE` | 只服务已批准退休职责，现役依赖已为零。 |
| `DETACH_THEN_REMOVE` | 旧职责确认退休，但当前主链尚借用数据壳或工具，先搬走调用。 |
| `KEEP` | 当前功能；即使名称老、包含 native API 或 compatibility 字样也保留。 |
| `AUDIT_ONLY` | 尚不能判定职责是否退休；本版给出证据和结论，不自动删除。 |
| `BLOCKED_SCOPE_DECISION` | 清理需要改变未批准的有效产品行为；说明影响并等待针对性决定。 |

拒绝旧输入的一段边界校验、负向测试中的旧样本、历史文档不是兼容 Runtime。相反，新类中接受旧规则的分支也属于清扫对象。

## 3. P0：建立可施工基线与单一合同

### 3.1 基线与文件保护

实际开工先读取 `AGENTS.md` 和受影响目录规范。涉及 Studio UI、媒体生命周期、开发产物推广时，分别读取仓库内：

```text
.agents/skills/studio-node-ui/SKILL.md
.agents/skills/story-media-lifecycle/SKILL.md
.agents/skills/dgr-release-packaging/SKILL.md
```

上述是仓库规范路径，不假定执行环境已有同名外部插件。缺文件或缺实机工具时如实记录，不伪造读取或 Worker。

记录 HEAD、分支、未提交／未跟踪改动、已有 dist 与测试环境。基线前移时对比本 PLAN 的固定 SHA，登记变化后衔接；不回退用户现有修改，不重置同名施工分支。没有取得远端写权限时不推送。

禁止 `reset --hard`、无差别 `git clean`、按目录整体覆盖、批量终止无关进程、清空 Data／正式世界／故事包输出目录。隔离测试副本不能冒充用户原目录。

### 3.2 基线测试与真实调用清单

先核对 `docs/BUILDING.md`、构建脚本和实际工具链，记录 Gradle 启动 Java、编译目标、游戏 Java 与 Studio SDK；不复刻历史 E 盘 SDK 路径，不以 `--offline` 作为首次准备环境的唯一入口。

至少建立：

| 清单 | 内容 |
|---|---|
| 生产路径 | 当前入口、注册、对象构造、服务端／客户端类、文件和网络读取路径。 |
| 作者路径 | 新建、打开、草稿、保存、导出、引用、复制、删除、Problems 与工作区恢复。 |
| 测试路径 | 当前有效 Core／WPF 测试、Java 测试／独立 Probe、fixture 和生成器。 |
| 不适用项 | 只验证退休协议的历史测试，以及已有环境限制；逐项说明，不笼统忽略。 |

`build` 成功不等于所有 JavaExec Probe 已执行。基线已有失败、环境跳过、本轮回归新增失败分别记录。历史 503／692 或其他 PASS 数字不直接作为本版结果或固定目标。

在实际项目结构确认后可使用既有入口：

```powershell
git status --short
git rev-parse HEAD
.\gradlew.bat tasks --all

dotnet test studio/src/DarkGreyRPG.Studio.Tests/DarkGreyRPG.Studio.Tests.csproj --configuration Release
dotnet test studio/src/DarkGreyRPG.Studio.Wpf.Tests/DarkGreyRPG.Studio.Wpf.Tests.csproj --configuration Release

.\gradlew.bat build --no-daemon
```

命令是定位后的示例，不绕过仓库要求的 SDK 发现、依赖准备和测试参数。Core／WPF 若共享输出文件，应串行执行。独立 Probe 使用本地 `tasks --all` 和构建定义查到的真实名称，不能凭空添加“已通过”的命令。

### 3.3 跨语言合同先冻结，再批量改代码

在 `Technical_Contracts.md` 只登记本轮需要改变的合同：

| 边界 | P0 必须明确 |
|---|---|
| Task 目标 | 每种目标允许的 Kind、内部索引表示、JSON 结构、空草稿规则、外部引用声明与验证层级。 |
| 指名器 | 当前统一操作与旧 mutator 的调用者、待删 discriminator、具体实体身份与 typeGroups 的精确边界。 |
| Choice | 旧 Logic 输出识别标准；当前条件输入／开关／结果的完整字段与行为。 |
| `give_item` | 当前 DGR Item 形态、旧 registry 形态、正负／零执行及拒绝位置。 |
| 包与项目 | 受影响的 Manifest／schema 字段、未知字段规则、single／group 共享合同和实际目录路径。 |
| 世界保存数据 | typeGroups 等退休结构的标记、拒绝边界、无写回保护；不建立转换器。 |
| 网络 | 退役消息编号、当前消息布局与版本拒绝策略；不让旧字节被误解为别的写操作。 |
| Generation／fingerprint | 受影响语义是否应进入既有成员指纹；两端一致，不能用内容代次改变来清空所有状态。 |

**版本号约定：**不提前拍定所有 schema 都升到同一个数字。只改需要断代的合同；P0 必须登记准确的新值、理由和对应读写端，并在开始相关修改前冻结。不同 `schema_version`、`format_version`、应用版本与成员内容代次不是一个概念。

删除 Manifest 字段或不再接受某种目标形态时，不能仍宣称完整支持原合同；同时不为无关 Actor、媒体和当前物品绑定结构机械断代。不建立版本协商平台，也不保留旧 reader。

### 3.4 草稿与可运行数据必须区分

允许当前正常创建、未选资源、未完成输入、非空内容迁移产生的可编辑结构草稿。保留现有 dormant／未连接目标的合法例外，不借本轮把“未选目标就不能保存任何项目”变成新规则。

但是**非空的错误身份不是空草稿**：原生注册名、错误 Kind、错误 Story 所属、损坏地址不能转为空值、第一项资源或同名对象以通过验证。

可保存草稿、可导出内容、可运行定义分别沿现有边界验证。当前未解析的只读来源可以有作者诊断；真正导出需要完整可验证的引用闭包。不得用全局 `compatibilityMode=true` 绕过新合同，也不得在清理该参数时丢掉现行草稿能力。

### 3.5 不兼容与不破坏的边界

只维护完成后的单一当前合同。不写旧项目／包／世界升级向导，不把旧解析器搬进 `legacy/`、`compat/` 或隐藏开关，不按 producerVersion 选择旧执行器。

拒绝退休输入时，给出有限明确的诊断，尽可能定位容器、Story、资源、节点和字段；原始文件保持不变。不能先读取失败返回空集合，再标 dirty 写回，造成“拒绝旧格式”实际变成清空旧数据。

对于世界保存数据：无法按当前合同安全解释时，阻止受影响 DGR 上下文继续执行或写回，保留原数据与错误；不将旧 typeGroups 自动展开为所有实体逐个绑定，不转移旧进度，不清空世界。具体隔离粒度必须按真实 SavedData 结构说明，不承诺不存在的逐字段事务能力。

同样结构与语义本来就落入当前合同的数据，可以由同一个 reader 正常读取；这不需要额外兼容层。但不能保证所有 0.3.3.7 项目／包都能直接进入 0.4。重新导出也以作者项目仍可被当前 Studio 解释为前提。

---

## 4. WP-A：Task 业务只认 DGR 身份

### 4.1 已核实的问题

当前两端地址 codec 对 Kill／Collect／Submit 保留 `registry_name` 目标形态；Studio 的 `CurrentProjectValidator` 对对应目标使用 `nativeTarget` 例外。[S10][S11][S12]

当前 Kill 事件适配器调用 `killEvents()`，会包含原生实体类型与 DGR 角色身份；库存匹配器允许目标直接等于 ItemStack 注册名。[S13][S14][S15]

因此不只是删除一张实体名映射表。本工作包覆盖**生产 → 保存 → 包验证 → 读取 → 事件／库存匹配 → 执行**整条链。

### 4.2 最终目标合同

| 业务字段 | 唯一合法的已配置身份 | 不再接受 |
|---|---|---|
| `kill_entity.entity` | DGR Actor 地址，允许现行个体／集体角色 | `minecraft:zombie`、Mod 实体注册名、`registry_name` 对象 |
| `interact_actor.actor_id` | DGR Actor 地址 | 裸实体类型、显示名称猜测 |
| `collect_item.item` | DGR Item 或 Item Group 地址 | 原生 Item 注册名目标 |
| `submit_item.item` | DGR Item 或 Item Group 地址 | 原生 Item 注册名目标 |
| `submit_item.actor_id` | DGR Actor 地址 | 类型自动映射或缺失提交对象 |
| `give_item.item_id` | DGR 个体 Item 地址 | 旧 `item + metadata`、物品组奖励替代 |

`entity`、`item` 等内部字段名无需仅为文案改名。内部可继续使用 ResourceAddress 的无损 key 表示，但必须来自受验证的地址，不允许任意非空字符串冒充身份。

### 4.3 Studio 与两端读取

去掉 Task `Target()` 中的原生身份分支及 `nativeTarget` 豁免，复用现有类型化地址处理。验证 Kind、所属 Story、资源声明及可运行闭包；不能只判断有没有 `~` 或是否非空。

覆盖 GraphResourceEnvelope 保存／读取、文件项目加载、单包／组包验证、Reference／Import、剪贴板、内容复制、目标参数修改、资源使用位置与测试生成器。不让某个导入器或内存构造路径重新绕过合同。

所有已配置目标的原生形态在当前入口明确拒绝；拒绝不静默改写图。普通正文、描述或高级指令中出现 `minecraft:zombie` 不构成目标身份，必须仍能正常保存。

### 4.4 Kill 事件

服务端从死亡实体解析 DGR 身份，向 Task 交付有效 Actor 目标；不再生成原生 registry Kill target，也不为无 DGR 身份的实体凭空分配角色。

同一实体可能有个体角色及多个集体角色，这是当前能力。解析结果按相同 DGR 地址去重，不能因为删除 native target 就只取第一个角色，也不能同一地址重复计数。多个不同目标按现有任务规则分别匹配，不擅自改成“全局只允许一个目标加一”。

保留真实玩家直接致死／玩家所属投射物的归因、FakePlayer 与自动化排除、环境最终致死排除、服务端线程和异常隔离。不要为清扫重写伤害归因。

`kill()`、`killEvent()`、`killAll()` 等旧辅助别名逐一查调用者：仅服务旧原生事件的退出；仍有现役调用的改接统一 DGR 事件路径，不能留下另一套降级语义。

### 4.5 Collect 与 Submit：先改实际库存路径

当前收集是背包快照同步，不是 lifetime 拾取累计。修改 `CanonicalTaskInventory.matches()`，移除：

```text
目标字符串 == Minecraft ItemStack 注册名
```

保留：

```text
ItemStack
→ 服务端已配置的 DGR Item／Item Group 匹配
→ 当前目标允许的 DGR 地址
→ 既有数量与附加条件判断
```

`ItemStackDefinition` 内部仍可使用注册名、damage、NBT 还原／匹配真实物品。物品组 FUZZY 仍按既有绑定规则识别同种物品；删除的是**任务直接以注册名为目标**，不是删除 FUZZY 的实现依据。

当前 metadata／damage 目标附加条件保持，不在本版改成新物品谓词系统。候选图标、持有数和真正提交必须引用同一匹配语义，避免界面展示可提交而服务端不接受。

`collectEvents()`、旧拾取辅助分支和相关订阅只有完成实际调用核验后才删除／改接。不得为了保住旧 Probe 把收集改回拾取计数。

### 4.6 验收重点

未指名同种实体不计数；不同种实体显式加入同一 DGR 集体角色可计数。物品仅因原生注册名相同而没有任何匹配的 DGR 绑定时不满足目标；通过合法 FUZZY 组绑定匹配则正常满足。

空草稿可编辑，错误非空目标不能冒充草稿。单包／组包中的原生目标被拒绝，内存直接构造的错误目标也不能触发执行。任务领取、隐藏目标、完成锁存、实际提交、奖励及只读展示保持。

---

## 5. WP-B：指名器退出类型级归组与旧写入通道

### 5.1 两条链的区别

当前普通实体指名界面经 `NominatorControls` 发 `C2SNominatorAction`，由 `NominatorActions` 对具体实体操作；旧 `C2SNominatorEntityBind` 仍注册且带 `typeScope` 分支，Resolver 仍消费 typeGroups。[S16][S17][S18][S19][S20][S22]

本轮批准退役的是：

```text
Minecraft 实体种类
→ 自动给该种类全部实体追加 DGR 集体角色
```

保留的是：

```text
具体实体
→ 主动指名
→ 个体角色／一个或多个集体角色
```

### 5.2 具体清扫范围

| 部分 | 操作 |
|---|---|
| `NominatorService.bindEntityTypeGroup` | 移除类型级归组服务及专属调用。 |
| `NominatorSavedData.typeGroups` | 删除当前合同中的类型归组存储／读写；原数据按 P0 的拒绝保护处理。 |
| `EntityDgrIdentityResolver` 类型级追加 | 移除按实体类型查组的分支；保留具体绑定解析与合法身份去重。 |
| 旧请求的 `typeScope/typeGroupId/addTypeGroup` | 随旧 mutator 清理，不保留隐藏操作。 |
| 仅为 typeGroups 存在的响应字段、构造参数、界面残留、fixture | 确认无现役用途后删除。 |
| `safeType`、实体种类查询和安全辅助 | 按调用职责拆分；仅为类型归组存在的部分可删，不能连实际宿主诊断或 CNPC 安全一起删除。 |

不能把旧类型规则改存进另一张表再继续运行，也不能通过扫描世界自动补发具体实体绑定来“无损兼容”。

### 5.3 当前绑定能力保护

具体实体仍由 UUID 等宿主信息定位，网络可使用当前实体编号配合 UUID、距离和维度核验。Minecraft 种类信息若用于宿主诊断可以保留，但不决定其 RPG 身份。

保留个体身份唯一宿主、明确转移、实体解绑、资源绑定释放、集体角色多实例与多组、资源所有权、只读引用来源及包闭包验证。

保留 CNPC 特定复制／同 UUID 情况下的唯一个体保护、合法组身份行为，以及 DGR Copier／Storage Box 的现行复制规则。不能把 `compat/customnpcs` 因名字含 compat 归为旧版本兼容栈。

### 5.4 旧写请求退役

对 `C2SNominatorEntityBind`、`C2SNominatorInventoryBind` 及其他疑似旧 mutator 建立调用表：普通 GUI、工具右键、快捷入口、命令、测试与网络注册分别核对。

所有需要保留的正式写操作统一复用当前操作服务与核验逻辑，不另造第二个功能相同的 mutator。删除旧写 handler、注册、编码器和只为其存在的调用／测试。

**Open／同步／结果消息不因同处旧目录就自动删除。**当前工具仍需要的读取和打开入口保留或最小改接；不把“旧写通道清理”扩成整套指名网络重写。

### 5.5 权限、协议与物品保护

当前统一操作继续核验权限、持有工具／有效容器、目标实体、距离、维度、UUID、资源目录 revision、绑定 revision、具体资源归属及操作关联。旧通道不再作为绕过这些检查的后门。

移除的网络编号默认留空，不为连续编号重排其余消息；不得复用为不同写操作。编号空洞不是兼容实现。当前消息布局发生变化时，使用项目现有的明确协议标记／拒绝机制；确有缺口只补最窄当前版本保护，不建设多版本协商。

物品精准／模糊选择、库存槽位返还、明确转移、取消、解绑和 ID 释放保持。失败不能吞掉指名槽物品；迟到响应不能在旧窗口继续执行或覆盖新选择。

### 5.6 验收重点

A 僵尸被指名“山贼”，B 僵尸未指名，C 骷髅也被指名“山贼”：A/C 具有组身份，B 不具有；B 不因类型相同自动获得故事交互或任务资格。

旧类型规则输入受控拒绝且原文件不被清空；新世界绑定经过保存／正常重启仍成立。旧写请求不再能改变绑定，当前实体与物品全部正式操作仍正常。测试是否发出了请求与真正服务端变更分别记录。

## 6. WP-C：Runtime 旧业务与 Journal 脱钩

### 6.1 旧运行模型退出

当前启动创建 Canonical Session／Task／Story 管理器，而命令构造中旧三套 manager 参数为 null；中央 `ProjectSnapshot` 仍带旧集合。[S06][S07] 这支持退役业务方向，但不能代替逐类依赖检查。

清理旧 Dialogue 定义／节点／会话引擎、旧 Quest 定义／Objective／进度引擎、旧 Story 定义／Loader／执行器和专属持久化。准确删除文件由台账确认，**不是直接删除三个目录后再修编译**。

重点覆盖：

```text
业务模型、解析／序列化、执行器、监听器、服务与管理器
客户端 Controller、旧 Screen、旧帧／动作／关闭消息
工厂、构造重载、导入、旧命令与帮助、补全
反射／字符串注册、资源文件、测试 task／fixture／示例生成器
```

专门为旧业务存在的测试随批准职责退役；包含当前仍需验证的权限、原子性、错误隔离等断言的测试先搬到当前入口。不得用全库排除测试来掩盖生产引用未清完。

### 6.2 中央快照与 Repository

最终 ProjectSnapshot／Repository 只承载当前项目元数据、Actor／Item／Item Group、Canonical Story／Session／Task 及必要图关系。

移除旧 `dialogues/quests/stories` 集合、旧类型构造参数和旧 getter；检查 package merger、loader、debug、nominator、live 工具、测试构造是否借它们传数据。`containsStory()` 等保留有意义的接口，但只回答当前 Story 是否存在。

不得因删除旧 `getStory()` 而把调用者改成“查不到就取第一个”。新 Story 列表、成员关系、导出闭包和错误信息必须保持准确。

`ProjectRepository` 的当前文件项目路径、包安装路径、配置、原子快照提交与 revision 不因瘦身删除。两个输入来源若都提供当前 Canonical 内容，不属于旧业务双栈。

### 6.3 命令

删除已退休的 `/dgr dialogue`、`/dgr quest` 子命令、别名、补全、帮助与旧参数，不保留专门的退休命令适配层。普通未知命令错误仍可存在。

保留 `/dgr story`、`/dgr session`、`/dgr task`、`/dgr reload`、当前诊断与工具命令的有效能力；不能把整个命令类视为旧系统删除。诊断仍只读；保留必要管理员执行权限，不给普通玩家增加启动、重置或发奖能力。

`/dgr task journal` 等若仍有正常查看用途，接到现行任务视图／只读查询，不继续走旧 Quest DTO。当前命令具体语法在 P0 核对并更新说明；不新增平行任务菜单。

### 6.4 Journal 清扫顺序

当前链是 Canonical Task → Legacy Adapter → Quest DTO → 旧消息；名称带 Canonical 并不代表没有旧依赖。[S23][S24]

按以下顺序处理：

1. 记录旧 Journal 所有生产调用者，包括命令、客户端请求、菜单和自动推送。
2. 将有用查看能力接到当前 Task 展示／只读投影，复用现有缓存和请求预算。
3. 移除 `CanonicalTaskLegacyJournalAdapter`、旧 `QuestJournalEntry/QuestStatus` 等仅为该运输链存在的数据结构。
4. 移除 `S2CQuestJournal/C2SQuestJournalRequest` 的注册与 handler，以及失去所有现役用途的旧 GUI／Controller。
5. `CanonicalJournalService` 若仅剩空壳则删除；若仍承担当前有效编排职责则收敛，不能留下转译旧模型的工作。

保留当前 `CanonicalTaskJournalEntry`／projector 等实际被任务菜单、HUD、历史、提交候选使用的模型。不要按目录 `task/journal` 整体删除。

### 6.5 客户端与网络基础设施

`DialogueNetwork` 是当前多个系统共用网络；`ClientQuestKeyHandler` 承担当前工具快捷键。允许定点改成 `DgrNetwork`、`ClientDgrKeyHandler` 或同等准确名称，但保持职责。[S24]

类名改动与协议改动分开：不得为改名同时更换 mod ID、注册名、持久配置键或网络通道字符串。保留有效键位与输入拦截；反射、代理、客户端专属类和服务端类加载都要验证。

`GuiDialogueHistory`、`GuiDialogueSettings`、`DialogueFontDrawing`、`CanonicalDialogueRenderer`、阅读上下文与容量工具不属于旧 Dialogue Runtime。名字不影响正确性时可保留，不强求全部去掉 Dialogue 字样。

---

## 7. WP-D：Studio 只保留当前作者模型

### 7.1 当前矛盾与清理目标

当前 `ProjectService` 拒绝包含旧资源数据的项目，却仍持有旧 Dialogue／Quest 文档集合并创建旧目录；`ShellViewModel` 在 Canonical Story 不存在时仍可能进入旧 StoryWorkspace 分支。[S08][S09]

目标是让**项目创建、打开、选择、编辑、保存、引用和删除**只认识现行支持的作者模型。不保留隐藏菜单、不保留只能通过测试 API 创建的旧资源体系。

### 7.2 建议实施顺序

| 步骤 | 要做的事 | 必须验证 |
|---|---|---|
| 1 | 固定当前新 Project／Story／Session／Task 创建、保存和打开的正向测试。 | 当前源数据、工作区与权威模型明确。 |
| 2 | 从 Shell、主页、导航、菜单与 XAML 中移除旧编辑入口和 fallback。 | 缺 Canonical 定义时准确报错，不进入旧编辑器。 |
| 3 | 清理旧 draft、selection、dirty、保存／关闭／退出询问分支。 | 新资源未保存保护与恢复不受影响。 |
| 4 | 让当前 Story 生命周期、引用和所有权服务脱离旧 StoryRepository。 | Actor／Item／引用资源删除与拥有关系检查仍完整。 |
| 5 | 删除旧 Dialogue／Quest／Story 专属 Repository、Document、Serializer、Validator、ViewModel 与 View。 | 代码、XAML、反射及构建资源均无生产引用。 |
| 6 | 精简中央枚举、资源描述与目录创建，更新生成器、帮助和相关测试。 | 新建／保存重开／导出链完整。 |

### 7.3 中央服务的脱钩原则

当前 `CanonicalStoryLifecycleService` 等如果仍为“可能存在旧 Story 引用”查询旧仓库，先改为现行 membership／资源地址／引用索引；不能删除整个依赖检查。

共享的 Actor 编辑、Item 编辑、原子文件写入、异常类型、资源选择、搜索结果、问题定位或复制工具可以提取为当前独立职责，随后删除旧业务容器。不允许把整个旧仓库重命名成新仓库来保住全部旧规则。

`ProjectService`、`ProjectSession`、Shell、ProjectHome、ProjectResourceRegistry 中旧资源种类应退出；当前资源类型与 GraphResourceKind 不混为同一个枚举。Story、Session、Task、Actor、Item、ItemGroup 的合法分类保持。

### 7.4 目录与文件：按完整路径判定

当前 Canonical 数据也可能在路径中出现 `stories`。因此：

- 删除的是“新项目仍创建、但只用于旧独立资源模型”的目录创建代码，不是扫描所有名为 stories 的文件夹后删除。
- 保留真正承载当前 Story、membership、角色、物品、引用包、媒体、布局和恢复数据的路径。
- 不删除现有用户空目录来追求外观整齐；也不在 OpenProject 内偷偷清除不支持的数据。
- 根 actors 目录及其要求若有现役作用必须保留；路径收敛以实际 Repository 为准，不能把上一轮的示意目录当成真实磁盘合同。
- 拒绝旧项目的检测可以精简，但不能误判当前格式或失败后写空项目。

目录变化若触及当前持久合同，纳入 P0 版本与拒绝策略，不另建自动搬家工具。

### 7.5 防止视觉上“没删干净”

清理 XAML DataTemplate、导航切换、资源类型标签、旧右键菜单、隐藏编辑页、空 Tab、legacy 提示与断开的 binding。WPF 编译通过不能证明绑定字符串都正确。

保留当前顶部菜单、资源库、画布、Inspector、输出／问题／调试器，保留保存／退出和异常反馈。不能以“删旧编辑器”为由让主页无法创建新 Story 或让引用资源失去查看入口。

当前工作区的保留草稿、切换回原编辑器、编辑历史、异步资源导入与关闭释放均需要实际操作验证。不要为精简将所有切图改成销毁再重建并丢掉未保存内容。

---

## 8. WP-E：Choice 与 `give_item` 的精确退役

### 8.1 Choice：退出旧输出，不碰新输入

历史 0.3.3.5 规定旧 option_id 对应的 Logic 输出仅为兼容保留；当前 `SessionChoiceSchema` 仍实现该规则。[H09][S25] 本轮明确取代该保留要求：旧“已选择”Logic 输出退出当前合同。

清理范围是专属端口接受、创建辅助、复制／重排映射、投影、求值与相关兼容测试。当前创建 helper 若仍间接调用名为 `InitializeLegacy` 的方法，先迁移实际调用，不根据方法名字直接删除。

新的 Choice 必须保留：

```text
一个 Flow 输入
每个选项的稳定 option_id
每个选项的 Flow 输出
每个选项的条件 Logic 输入及开关／False 行为
当前服务端选择结果、回执与去重
```

“玩家做过什么选择”的真实运行记录不是旧输出端口，不能删除。需要选择后的 Logic 时继续使用现有流程判断／公共 Logic 能力，不新增另一种“已选择变量”。

### 8.2 旧结构处理

带旧 Logic 输出的输入不得作为可运行当前图接受；不能静默丢端口、删线或改写作者剧情后保存成功。使用明确错误定位旧节点／端口并保留原数据。

`SessionChoiceSchema` 目前存在多种字段数量、条件开关默认和回退规则。这些**不自动全部归入 CL-09**。只有确认为旧输出专属、且当前工厂与合法数据不需要的部分随之删除；其余按第 10 节审计，不把缺少新字段都当成旧输出。

对普通未连接条件维持默认可选；有连接且 False 时按隐藏／置灰处理；非法连接不是未连接。保留当前条件开关的作用，不借收口重新定义端口可用性。

### 8.3 Choice 回归边界

停留 Choice 时 Logic 变化仅更新可用性，不能重进节点、增加正文 epoch、重播演出或重复记历史。点击时服务端重新复核；过期／隐藏／禁用选项请求拒绝并刷新，不能错走其他索引。

全部不可用时保持等待，不自动结束、不选第一个；即使只有一个可用选项，自动播放也不能代选。排序、删除、Undo／Redo、复制、参数粘贴和保存重开保持稳定身份映射。

### 8.4 `give_item` 旧形态

当前配置解析明确标记 `item + metadata + amount` 为 0.3.0.0 兼容形态。[S26] 删除该分支、`legacyRegistryItem` 标志、旧执行函数和只为它存在的字段／构造参数。

唯一当前方式由 DGR 个体 Item 地址解析世界绑定，按原正负／零数量规则执行。未绑定或资源不存在按现有明确错误路径处理，不自动查注册表、不选择第一物品、不生成替代物品。

保留底层从当前绑定还原 ItemStack 所需的注册名、damage 和 NBT；这与允许作者传裸注册名是两个不同边界。保留库存容量处理、负数下限、事务、防重和通知。

---

## 9. WP-F：格式、协议、命名与当前文档

### 9.1 删除 Manifest 旧空字段

当前 Manifest 明确要求 `dialogues`／`quests` 为空，但仍声明、序列化和遍历它们。[S27][S28]

删除对应 DTO 属性、写入、读取、必需路径枚举、旧统计与验证分支；同步处理 C# 与 Java、单包与组成员清单、目录载荷、Reference／Import、merger、测试 fixture 与示例。

新合同不把退休字段“忽略掉以兼容”：对仍带这些字段的输入按冻结合同拒绝，即使列表为空。所需版本标记在 P0 确定并两端一致。

不顺势重写 ZIP 布局、媒体路径、全部 Canonical 前缀或 single／group 容器。只删除已批准无业务用途的字段；`story`、`canonical_stories` 等其他字段是否冗余不能未经证明一起删。

### 9.2 包校验必须保留

保留路径安全、重复 entry、严格字段／类型、资源闭包、成员完整性、媒体指纹与大小预算、压缩展开预算、共享引用一致性和原子发布。单成员成功不等于整个组成功。

相同 ResourceAddress、相同定义可共享；不同定义不能按读取顺序选一份。引用资源的 Owner UID 不自动成为可执行 Story 成员。UID 冲突与依赖错误按当前规则分别诊断。

包名、文件位置、容器总 hash 不替代 Story 身份或成员代次。仅格式变化造成的代次影响应按现有机制准确记录，不自动清空所有玩家状态或奖励收据。

### 9.3 网络与持久化收口

所有受影响 decoder、serializer、网络注册和 SavedData 都只实现冻结后的当前合同。不得留下旧命令适配、历史 payload 读取或“兼容模式下仍可执行”的隐藏入口。

格式标记、消息边界和字段校验继续有界；对损坏／旧请求拒绝且无副作用。删除消息留空编号，不要求重排。若重新使用编号存在误解码风险，禁止重用。

正常的新版本内部恢复、同一运行的续接、内容快照、缓存、撤销与事务恢复保留；它们不是“旧版本兼容”。

### 9.4 命名精简的上限

允许在已改动模块内精确命名，使当前网络／快捷键等不再误导维护者；清掉无调用别名与旧注释。一次重命名应能单独检查，并覆盖字符串／XAML／反射引用。

不全仓移除 `Canonical` 前缀，不为了统一名称移动全部包目录，不修改现有资源、模组与文件标识来让字符串变漂亮。名称调整服务于已经完成的职责收口，不能取代它。

### 9.5 更新现行说明，不篡改历史

优先更新已有 `README.md`、`docs/CURRENT_ARCHITECTURE.md`、`docs/GETTING_STARTED.md`、受影响 schema 和测试说明，明确：

```text
只有当前 Story／Session／Task 作者和执行体系
任务目标使用 DGR 角色／物品身份
具体实体指名，不提供种类自动归组
旧 Choice 输出及旧给物品输入不受支持
0.4 当前格式、手动导出／reload 与旧格式拒绝方式
```

只更新与清扫有关的构建／命令说明，不复写完整历史手册。历史 PLAN、审计、失败记录、发布哈希和旧标签保持原文；在当前索引中明确其历史地位即可。

扫尾后已有的主题修复、依赖材料和便携部署成果保留。没有新实证，不把它们重新列为 0.4 待修问题。

---

## 10. AUDIT_ONLY：允许查清，不自动删除

### 10.1 Start 中的历史 `enter_story`

严格区分三个东西：

| 位置 | 本轮处理 |
|---|---|
| 已退休的独立 Story 节点 `enter_story/interact_actor/enter_region` | 不恢复；无现役职责的残留分支可按已批准旧体系清理。保留明确拒绝样本。 |
| 当前 Start 的角色交互、进入区域、Logic、流程驱动 | 保留。 |
| Start 配置中的历史 `enter_story` 条目或辅助读取 | 审计其创建、保存、端口、导出与运行调用；不凭名字判退役。 |

若该历史条目仍是一条作者可用的独立启动语义，不在本版未经说明删除。若已无任何现行职责、只处理退休节点数据，可以登记证据后作为死分支收口。不能把“当前新建菜单不出现”单独当充分依据。

### 10.2 Normalize、默认值和兼容参数

逐项问：当前工厂能否生成这类数据？当前格式允许省略吗？是否为了合法草稿、撤销、复制或恢复？是否在解释另一种旧业务模型？

完全无效果、只传递而无读取的参数可在证据充分后移除；服务现行草稿的校验模式必要时改成准确内部名称，但不创建一套新验证平台。

`wait_for_completion`、`custom_text_speed`、`pages`、Choice 条件开关、公开端口与画面连续性等字段不能统一设为“缺字段一律失败”。是否必需由各自当前合同决定。

### 10.3 ProjectOrigin／旧 ID 参数名

追踪 `ProjectOriginCode` 的真实读写和用途。仅用于已退休身份／同源判定且无现役使用的可删；若影响当前本地设置、路径定位或保护机制，先分离职责，不能机械抹掉数据保护。

内部名为 `fullId` 的参数可能已经传当前 typed key，不能据此恢复或删除旧 Full ID 系统。检查数据含义，不做词语禁用。

### 10.4 其他大类、Bridge 和平台调用

`creator`、Live Bridge、编辑工具、媒体类或诊断类不因诞生早就自动退休。只处理与 CL-01—CL-11 明确相关的旧依赖；其他有效能力保留，另有问题登记，不作为本版隐形扩展。

Dimension 整数、执行原生指令、BUFF 的 MOD 扩展、字体数值表、Minecraft 注册名、实体 UUID、ItemStack 元数据有各自现行用途。本次只禁止明确的任务身份／类型自动归组旁路，不能进行全仓 native API 清除。

### 10.5 审计项的关闭标准

每项给出 `KEEP / REMOVE_DEAD_BRANCH / BLOCKED_SCOPE_DECISION`，附调用证据与理由。未证明的保留并登记；不得以“未清空所有可疑字符串”宣布整个版本必须无限延期，也不得把保留项藏起来称全仓历史问题归零。

## 11. 施工阶段与阶段门槛

### 11.1 阶段顺序

| 阶段 | 施工内容 | 退出门槛 |
|---|---|---|
| P0 基线与合同 | 第 3 节；建立台账、当前测试映射、版本／拒旧／网络合同，查清现役依赖。 | 基线可追踪，已批准项与审计项分开，没有再次讨论保留旧系统。 |
| P1 业务目标身份 | WP-A + `give_item`；两端 codec、校验、Kill 与实际库存匹配一起收口。 | DGR-only 正向／负向用例通过；收集、提交、奖励语义不回退。 |
| P2 指名器 | WP-B；当前操作闭环，类型级规则及旧写请求退出。 | 具体实体／物品指名正常；旧类型规则与旧 mutator 均不能生效。 |
| P3 Runtime 脱旧 | WP-C；旧引擎、中央模型、旧命令、Journal 运输链退出。 | 正式启动、任务菜单／诊断无旧业务依赖；当前执行与持久化回归通过。 |
| P4 Studio 与 Choice | WP-D + Choice；清旧工作区、资源服务与旧输出，保留当前图编辑。 | 当前完整作者链可用；无 legacy fallback；双入口、草稿及 Undo 正常。 |
| P5 合同与资料收口 | WP-F；完成受影响 Manifest／schema、命名、fixture、文档、清扫检查和 AUDIT_ONLY 结论。 | 只有一个当前合同；台账闭合，生产无批准退休入口，保留项有理由。 |
| P6 联调与开发交付 | WP-G；最终自动套件、同包 E2E、实机 UI 与 Data 安全、权威 dist。 | 证据对应准确源码／产物；必需测试通过或明确未关闭；未做未授权发布。 |

WP-F 不是等到 P5 才首次接入：P0 冻结合同，P1—P4 每批同步两端与相关版本／测试；P5 是最终一致性检查。任何中间不兼容构建只在隔离开发环境使用，不推广到用户正在运行的程序。

### 11.2 每批操作纪律

一批内完成“证据 → 脱钩 → 删除 → 受影响编译／测试 → 台账更新”，禁止先删除全仓后集中修复。相关跨语言改动作为同一批可审查变更，不能让 C# 与 Java 各自决定不同合同。

利用正常本地版本控制保存可回退节点，保留用户未提交改动；是否提交遵守当次开发授权。代码回退不代表世界／Data 自动回退，测试数据始终隔离。

共享的 codec、中央快照、网络注册和 Shell 由一个集成责任人协调；能够真实分工时再分工，不虚构并行代理。不得为“分工”创造长期管理系统。

### 11.3 出现失败时

新增功能回归、编译断口、数据破坏或越权必须定位处理；不能恢复旧兼容路径来使测试变绿。

本轮批准删除的旧格式测试可以退役，但当前功能测试失败不能标成“历史测试”直接删。未涉及业务的历史样本失败可说明不适用；混合测试先提取现行断言。

遇到新产品决策冲突，只暂停对应条目并提交用途、证据、方案；不反复询问已经批准的清扫范围。

---

## 12. 验证矩阵

### 12.1 证据类型与结果

- **A：自动验证。**单元测试、当前 Probe、序列化向量、集成测试、编译与构建，各自记录。
- **W：真实 Windows Studio。**实际 WPF 窗口、键鼠／原生 UI Automation；直接调用 ViewModel 不等于 W。
- **M：真实 Minecraft。**配套客户端／服务端或集成服务器；纯模型 Probe 不等于 M。

所有用例起始为 `NOT_RUN`；执行后记 `PASS / FAIL / BLOCKED / NOT_RUN`。`N/A` 仅用于有明确证据确实不适用的分支，不能因没环境而写 N/A。截图证明静态布局，不单独证明动画、持续交互和状态恢复。

测试台账以“行为断言”为单位，允许合并在少数测试文件中，不需要为每行建立一个新工程或脚本。

### 12.2 身份与原生目标退役

| ID | 场景 | 必须结果 | 层级 |
|---|---|---|---|
| ID-01 | 同 local_id 的跨 Story 角色／物品目标 | 按 Story＋Kind 隔离，不串资源。 | A |
| ID-02 | 合法 Actor／Item／ItemGroup 目标保存、导出、读取 | 两端一致，字段类型正确，UI 仍显示资源名称。 | A/W/M |
| ID-03 | Kill 原生字符串和 `registry_name` 对象 | 受控拒绝；不能运行、不能自动换成空目标。 | A |
| ID-04 | Collect／Submit 原生目标，经单包和组包输入 | 全部拒绝；组不能部分注册。 | A/M |
| ID-05 | 旧 `give_item item+metadata` | 解析／执行入口拒绝，不发物品、不推进副作用。 | A |
| ID-06 | 当前 `give_item` 正／负／零与未绑定 | 现行增减、下限、错误及防重保持。 | A/M |
| ID-07 | 同种实体：一个具体指名、另一个未指名 | 只有有 DGR 身份者匹配对应任务。 | A/M |
| ID-08 | 不同种实体：都具体指名为同一集体角色 | 均能按当前 Kill／交互规则参与。 | A/M |
| ID-09 | 个体＋多个组、重复解析同一地址 | 合法多身份保留；相同目标不因重复列表多计。 | A |
| ID-10 | 近战、玩家投射物、FakePlayer、环境最终致死 | 沿用当前归因，不因移除 registry 路径改变。 | A/M |
| ID-11 | 物品精准／模糊、NBT／damage、多个库存格 | 匹配、数量和候选展示一致；无裸 registry 目标。 | A/M |
| ID-12 | 空草稿、dormant 目标、错误 Kind／缺声明 | 合法草稿保留；错误非空身份不被视为空白。 | A/W |
| ID-13 | 内存直接构造错误目标，不经 Studio／archive | 不能绕过有效业务边界执行。 | A |
| ID-14 | 正文／描述／高级指令包含 `minecraft:zombie` | 文本不被误删、替换或目标校验拦截。 | A/W |

### 12.3 指名器

| ID | 场景 | 必须结果 | 层级 |
|---|---|---|---|
| NOM-01 | 新建具体个体／集体角色绑定 | 只影响指定实体，资源身份不按种类推导。 | A/M |
| NOM-02 | 个体占用、明确转移、取消 | 唯一宿主与明确确认不变；取消无副作用。 | A/M |
| NOM-03 | 实体解绑／资源绑定释放 | 保留资源，不误删实体／其他无关绑定。 | A/M |
| NOM-04 | 旧 typeGroups 数据进入当前读取 | 明确拒绝相关退休结构；原记录不被空写回。 | A |
| NOM-05 | 旧实体／物品写消息及类型级请求 | 不注册为有效写操作，不能通过其他编号误执行。 | A |
| NOM-06 | 非授权、旧 revision、距离／UUID／容器不符 | 当前统一服务拒绝且无写入。 | A/M |
| NOM-07 | 指名槽物品、EXACT／FUZZY、转移／解绑失败 | 槽物品不丢；匹配规则与返还生命周期不变。 | A/M |
| NOM-08 | 旧窗口结果、重开、切服 | 关联失效，不覆盖新选择，不重放变更。 | A/M |
| NOM-09 | 新当前绑定保存、正常重启、重连 | 当前数据恢复一致，无类型归组复活。 | A/M |
| NOM-10 | CNPC 复制／同 UUID、Copier／Storage Box | 不复制唯一个体身份；现行合法组行为保持。 | A/M |

### 12.4 旧运行体系与 Journal

| ID | 场景 | 必须结果 | 层级 |
|---|---|---|---|
| RUN-01 | 生产构造、网络／事件注册、反射入口检查 | CL-01—CL-04 批准旧业务无有效执行入口。 | A |
| RUN-02 | 当前 Story→Session→Task→结算 | 正常运行，无旧 manager／旧模型依赖。 | A/M |
| RUN-03 | 旧 Dialogue／Quest／Story 数据输入 | 当前边界明确拒绝；不恢复旧 Loader。 | A |
| RUN-04 | `/dgr dialogue`、`/dgr quest` 与补全 | 无旧入口／兼容别名，当前命令仍可用。 | A/M |
| RUN-05 | 当前任务菜单、有效 task journal／诊断能力 | 不经旧 Quest DTO，内容完整且只读。 | A/M |
| RUN-06 | 历史、HUD、候选分页、隐藏目标 | 当前投影及权限保持，不变成全量旧 Journal。 | A/M |
| RUN-07 | 专服类加载与客户端显示 | 无旧客户端类混入服务端；共用网络／快捷键正常。 | A/M |
| RUN-08 | 旧消息编号与当前报文 | 已退役编号不复用为别的写操作；当前消息 roundtrip 正常。 | A |

### 12.5 Studio 与 Choice

| ID | 场景 | 必须结果 | 层级 |
|---|---|---|---|
| STU-01 | 新建项目及 Story／Actor／Item／Session／Task | 无旧资源入口，当前作者模型完整。 | A/W |
| STU-02 | Canonical Story 文件或 membership 缺失 | 明确诊断，不回退旧 Workspace，不写空资源。 | A/W |
| STU-03 | 切 Story、返回首页、再进入含草稿工作区 | 未保存数据、选择、历史保持。 | A/W |
| STU-04 | 节点内／Inspector 修改、拖放、保存重开 | 同模型、同验证、同 Undo，不出现 XAML 断绑定。 | A/W |
| STU-05 | 删除资源／Story，含外部引用和共享媒体 | 现行所有权／使用位置检查有效，不误删外部定义。 | A/W |
| STU-06 | Reference、Import、非空 B 内容追加 | 来源只读、复制映射、原内容保护、外部线边界正确。 | A/W |
| STU-07 | 组合、G 手势、台词页、搜索／使用位置 | 当前操作完整；稳定身份定位，不重新引入旧模型。 | A/W |
| STU-08 | 新建目录与打开旧目录数据 | 不创建退休专属目录；不误删当前 stories 路径或用户文件。 | A |
| CHO-01 | 新 Choice 创建与旧输出样本读取 | 新输入／Flow 完整；旧“已选择”Logic 输出拒绝。 | A/W |
| CHO-02 | 未连接、条件开关、True、False 隐藏／置灰 | 当前真值和开关语义保持。 | A/M |
| CHO-03 | 停在 Choice 时条件改变 | 刷新可用性，不重播正文、语音、画面或历史。 | A/M |
| CHO-04 | 隐藏／禁用／迟到点击 | 服务端拒绝，无选择记录与 Flow 推进。 | A/M |
| CHO-05 | 全部不可用、只有一个可用项、自动播放 | 等待规则正确，不代选、不自动结束。 | A/M |
| CHO-06 | 条件端口／选项排序、复制、删除、撤销重做 | 映射稳定，不复活旧输出，不损坏合法线。 | A/W |
| CHO-07 | 真实选择结果、流程判断、公共 Logic | 结果和下游 Logic 可用，不因退旧输出被误删。 | A/M |

### 12.6 包、运行语义与保护能力

| ID | 场景 | 必须结果 | 层级 |
|---|---|---|---|
| PKG-01 | 当前 `.dgrs`／`.dgrs.g` 导出及两端校验 | 单／组语义与完整资源闭包保持。 | A/W/M |
| PKG-02 | 已退役 Manifest 字段含空或非空列表 | 按新单一合同拒绝，无隐式忽略兼容。 | A |
| PKG-03 | 路径穿越、重复 entry、坏成员、坏媒体、超预算 | 受控拒绝，原产物保留，不部分运行。 | A |
| PKG-04 | 所有安装来源含 Disabled 的重复 UID | 对称 Conflict，无先到 winner、无旧快照绕过。 | A/M |
| PKG-05 | Disabled 与 Conflict／Error 的活动实例 | 前者仅挡新开，后者按现行规则退役；无关故事不清空。 | A/M |
| PKG-06 | 无内容变化 reload、重连、正常重启 | 当前 0.4 测试链继续／历史保持，无重复奖励。 | A/M |
| PKG-07 | 只更新组内一个成员／只改容器名 | 按成员真实语义判代次，不按容器整体清状态。 | A |
| PKG-08 | 真实导出包与运行包 SHA-256 | 同一字节，不以手写正向包代替作者链。 | W/M |
| REG-01 | 角色／区域／Logic／流程驱动 Start | 当前入口、ACTIVE 保护与边沿保持。 | A/M |
| REG-02 | 无条件、冷却、定时；历史 ONCE＋当前可重复 | 准入采用当前定义，不以旧摘要误拦。 | A |
| REG-03 | 零目标／零结算、多结算同时成立 | 保留允许范围、ACTIVE 行为、显式优先级和唯一结果。 | A/M |
| REG-04 | 当前库存收集、完成后丢弃；实际角色提交不足／足量 | 收集不回退；提交不足不扣，足量原子扣除一次。 | A/M |
| REG-05 | 台词 Enter／Backspace／IME、动态内容、容量及本地分屏 | 当前编辑／阅读语义不退回旧版本。 | A/W/M |
| REG-06 | 标题等待／不等待、画面平滑、头像差分、音乐／语音 | 演出及回执不重复推进，表现能力保留。 | A/M |
| REG-07 | 三追踪、仅新任务自动、独立 HUD、任务 40%／60% | 最新 UI 与选择规则保持。 | A/M |
| REG-08 | AND／OR 三图双入口、命名排序、至少二输入 | 最新卡片与稳定端口交互保持。 | A/W |
| REG-09 | 在线／离线玩家诊断 | 只读，不创建玩家、不恢复执行、不发奖励或清坏记录。 | A/M |
| REG-10 | 媒体 3／10、租约、共享、旧异步结果 | 当前政策自动回归；实机基础头像／音频正常。 | A/M |
| REG-11 | 留声机、设置／历史、实际键位及 GUI 层级 | 共享网络／按键改名后仍可使用，无无关功能退役。 | A/M |

### 12.7 数据与交付

| ID | 场景 | 必须结果 | 层级 |
|---|---|---|---|
| SAFE-01 | 旧／坏输入读取失败前后比较文件 | 不覆盖、空写、改 UID 或清世界。 | A |
| SAFE-02 | 保存／导出中途 IO 失败、取消、异步晚到 | 沿现有事务保护，不半提交假成功。 | A |
| SAFE-03 | 权威 dist 推广前后 Data 清单／哈希 | 部署未覆盖个人 Data、项目、媒体及恢复文件。 | A/W |
| DEL-01 | 清扫台账、当前测试映射、AUDIT_ONLY 结论 | 批准项闭合；保留及未完成原因清楚。 | A |
| DEL-02 | 程序源码／patch、EXE、DLL、JAR、测试包 | 来源对应，版本／大小／SHA-256 实际读回。 | A/W/M |
| DEL-03 | 基线失败、当前失败、跳过与实机缺口 | 分层报告，不用旧 PASS 或测试总数造完成结论。 | A |
| DEL-04 | 分支、正式包、标签、用户验收 | 不做未授权远端／成品操作，不替用户填接受。 | A |

---

## 13. 必须完成的同包端到端样例

以下是有限验收内容，不建设大型示例 RPG、不增加产品内模拟事件按钮。正向样例必须由实际 Studio 创建／保存／导出；恶意输入可另用手写 fixture。

### E2E-A：身份不看物种

创建集体角色“山贼”和对应 Kill Task。在隔离世界中，将僵尸 A 与骷髅 C 具体指名为“山贼”，同种僵尸 B 保持未指名。先击杀 B，进度不变；再对 A/C 执行当前有效玩家击杀，两者都按目标计数。

同时验证一个角色交互 Start：未指名的 B 不能因类型获得身份。另用小型自动向量验证个体＋多组、重复地址、FakePlayer 和环境归因。记录实际绑定与任务状态，不以模型结果冒充实机击杀。

### E2E-B：物品匹配和原子提交

创建一个个体物品、一个含 EXACT／FUZZY 成员的组、收集目标、指定角色提交目标与一次奖励。通过真实指名器配置世界物品，覆盖跨槽求和、附加条件、原生 Tooltip 和候选页。

足量完成收集后移除物品，收集完成不回退；提交不足时不扣物，足量与正确角色交互只扣所需数量并完成。任务菜单／候选查看不能远程提交。当前 `give_item` 正负／零用自动事务测试补齐。

### E2E-C：条件选择与多结算

当前 Story／Task 公共 Logic 经正常边界进入 Session Choice 条件输入。实际确认未连接选项可选，False 隐藏／置灰，状态更新不重播演出，过期点击被拒绝。

同项目准备多结算 Task 并验证显式顺序；另准备零结算 Task，说明保持 ACTIVE 是设计而不是卡死。验证真实选择结果仍能沿 Flow／流程判断与公共 Logic 继续。

### E2E-D：作者全链与外部来源

在真实 Studio 中创建本地 A、非空 B、Session／Task／Actor／Item；验证双入口编辑、含草稿切换、台词快捷键、组合、搜索及使用位置。将 A 内容追加到 B，一次 Undo／Redo 保持原数据和映射。

另加载一个完整只读来源 Group，先只引用资源确认不扩组，再按现有公开边界联动并导出完整闭包。Reference 原字节与 UID 不变；Import 产生新本地身份。不能打开旧 Workspace 来完成任何一步。

### E2E-E：安装、禁用、冲突与恢复

部署一份独立包和一份小型组包；两个玩家分别运行相关当前内容，验证实例隔离、普通 Disabled 的已有运行继续与新启动阻断。

在隔离安装目录加入同 UID 副本，手动 reload 后相关容器对称受阻；未冲突包保持。移除测试副本并 reload 恢复资格，但不自动重播副作用。只操作测试文件，不恢复已删除的管理器删除 UI。

在当前 0.4 格式内测试一次无内容变更 reload、断线重连和正常保存／停服／重启，确认完成历史、活动状态及奖励收据。此项不要求迁移 0.3 旧存档。

### E2E-F：原版与 CNPC、显示与交付

基础组不安装 CNPC，按已核实的当前必需依赖运行原版实体故事链；集成组增加当前使用的准确 CNPC 构建，验证具体绑定、实际交互与复制身份保护。专服与集成服务器均有最小启动检查。

检查任务窗口、设置／历史、包管理器改键关闭、头像／表情差分、语音／背景音乐、标题与画面基础演出。共享网络／快捷键调整影响留声机时做一次正常打开／播放控制回归，不做新在线音乐平台研究。

使用明确隔离的测试目录和进程，不停止用户正在运行的客户端。声音只有实际听到才能记为听音通过；日志只证明播放源状态。

## 14. 清扫检查、开发产物与报告

### 14.1 轻量检查，不建设治理系统

复用已有测试／脚本机制，必要时增加一份本版范围检查脚本；名字由实际仓库结构决定，不假称已存在。检查结果必须能定位具体文件／符号，并明确“文字命中”与“实际可执行依赖”的区别。

本版最终目标：

```text
已批准旧 Runtime 生产入口 = 0
已批准旧作者入口／fallback = 0
Task 原生注册名目标接受路径 = 0
实体类型自动归组写入／消费路径 = 0
旧指名 mutator 有效注册 = 0
Choice 旧“已选择”Logic 输出接受路径 = 0
give_item 旧注册名形态接受路径 = 0
Manifest 退休字段的当前读写路径 = 0
```

这些是**职责及接受行为**的零，不是历史文件、负向样本或字符串出现次数的零。允许最小拒绝诊断保留退休名称，允许反例测试中存在旧数据，不保留完整旧解析／执行引擎来产生错误消息。

扫描包括本轮新增未跟踪源码、XAML、资源声明、构建文件和生成器；不能只扫描已跟踪 `.java`。文件数／行数变化可附带报告，但无预设比例，不计入替换成空文件、搬去 legacy 目录或删除文档的虚假收益。

### 14.2 测试退役映射

在 Acceptance 中维护一张紧凑映射表：

| 原测试／Probe | 原本保护的行为 | 本轮处理 | 当前对应证据 |
|---|---|---|---|
| 仅接受旧 Dialogue／Quest／旧目标形态 | 已批准退休合同 | 删除其兼容断言，增加当前拒绝测试 | 新拒绝测试／fixture |
| 旧文件内混有当前权限、事务、恢复断言 | 有效行为仍在 | 提取／改接当前入口，不减少保护 | 当前测试名称 |
| 当前正常业务用旧身份 fixture | 行为有效、样本过时 | 将样本改为当前格式，不恢复兼容代码 | 更新后自动结果 |
| 与本轮无关且确实受环境限制 | 未执行 | 如实 BLOCKED／NOT_RUN，不计 PASS | 环境说明 |

既有失败不能一概归为“历史问题”，旧套件中当前业务断言必须查清。最终技术通过要求本轮必需的当前合同测试没有未解释失败；若有阻断，如实交付源码和缺口，不能把待验证候选称为完成成品。

### 14.3 工作记录控制在一个目录

```text
PLAN/0.4.0.0/
├─ PLAN.md                   本施工规格的唯一仓库副本
├─ Technical_Contracts.md    本轮合同、版本、拒绝边界与向量
├─ Implementation.md         清扫台账、脱钩、改名与审计结论
├─ Acceptance.md             基线、测试映射、矩阵、实机与剩余项
├─ Delivery.json             准确源码／产物／授权状态
└─ evidence/                 必要日志、截图、样本清单和哈希
```

不为每个函数另写一份 PLAN；同一决定只在一个权威位置维护。必要轻量附件放 evidence，不引入项目管理数据库或常驻扫描服务。

### 14.4 开发交付与成品发布分开

目标应用版本为 `0.4.0.0`，Studio／Runtime 的源版本、显示版本与实际产物应一致；内部 schema 编号按 P0 独立管理。

按 AGENTS，Studio 用户可运行的权威开发位置为：[S01]

```text
E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe
```

该路径是用户开发环境的交付要求，不是要求公共构建脚本只能从 E 盘运行。执行环境不能访问该路径时，记录未推广，不编造已更新。

保留根 apphost、`Program/Tools/Docs/Data` 布局；程序更新使用既有白名单与 Data 保护。至少实际读回 EXE、`Program/DarkGreyRPGStudio.dll`、Core DLL 与程序清单的版本／大小／SHA-256；根 EXE 的 hash 没变不能证明功能 DLL 没变，也不能证明已更新。[S01][S03]

Runtime 记录实际 JAR 输出、版本、大小、SHA-256，以及专服／客户端测试副本。只有用同一份最终源码构建的配套产物才能计为本轮交付；旧 JAR、旧测试包和旧截图不能冒充新结果。

没有额外明确发布请求，不创建／覆盖 `artifacts/DGR0.4.0.0`、正式 Studio ZIP、GitHub Release 或标签；不覆盖 0.3.3.7 已有发布。普通开发构建和更新 dist 不等于成品发布授权。

### 14.5 Data、世界与回退

部署前后比较 Data 文件清单、大小与 hash。正常启动产生的新日志／用户偏好写入，与部署过程的改写分开记录。只检查隔离测试项目的行为，不通过编辑用户原项目来证明更新成功。

代码回退需要对应源码／二进制；世界回退需要匹配的世界、DGR 配置、故事文件及 JAR 备份。不能只恢复一个 JAR 就保证已经写过的新存档可被旧版本读取。

不要求兼容 0.3 进度，也不因此授权删除它。读取不支持数据时不写回，迁移与重建测试内容只在明确的测试目录内进行。

### 14.6 Delivery.json 起始模板

以下仅是执行者需要填写的模板；`null`、`NOT_RUN` 和未开始状态必须由真实结果替换，不能预填 PASS。

```json
{
  "version": "0.4.0.0",
  "theme": "canonical-only-cleanup",
  "scope_status": "APPROVED",
  "plan_date": "2026-10-07",
  "baseline_branch": "codex/0.3.3.7",
  "baseline_commit": "51582ae8fe8a71214f2306ecd08492129171cff5",
  "implementation_branch": null,
  "implementation_commit": null,
  "implementation_patch_sha256": null,
  "implementation_status": "NOT_STARTED",
  "technical_status": "NOT_RUN",
  "delivery_status": "NOT_DELIVERED",
  "legacy_business_compatibility": false,
  "multi_minecraft_version_work": false,
  "contracts": {
    "project_schema": null,
    "graph_schema": null,
    "single_container_format": null,
    "group_container_format": null,
    "nominator_saved_data_schema": null,
    "changed_wire_contracts": null
  },
  "tests": {
    "baseline": "NOT_RUN",
    "studio_core": "NOT_RUN",
    "studio_wpf": "NOT_RUN",
    "java_build": "NOT_RUN",
    "java_current_contract_probes": "NOT_RUN",
    "studio_native_ui": "NOT_RUN",
    "minecraft_without_cnpc": "NOT_RUN",
    "minecraft_with_cnpc": "NOT_RUN",
    "multiplayer_permissions": "NOT_RUN",
    "data_preservation": "NOT_RUN"
  },
  "retirement_inventory_status": "NOT_STARTED",
  "audit_only_findings": [],
  "studio": {
    "authoritative_path": "dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe",
    "promoted": false,
    "exe": null,
    "main_dll": null,
    "core_dll": null,
    "program_manifest": null
  },
  "runtime_jar": null,
  "test_packages": [],
  "baseline_failures": null,
  "remaining_items": [],
  "finished_product_created": false,
  "remote_release_modified": false,
  "user_accepted": false
}
```

`scope_status=APPROVED` 只表示清扫方向获批，不是用户接受成品。代码完成、测试完成、dist 推广和用户验收四件事独立报告。

---

## 15. Definition of Done

本版完成时，应能用证据回答下面这些问题：

- [ ] CL-01—CL-11 的批准退休职责已关闭，台账列明所有现役依赖的处理，没有搬到隐藏兼容目录继续运行。
- [ ] Studio 只剩当前作者业务模型；旧工作区、旧资源 CRUD 和隐式 fallback 不再可达。
- [ ] Runtime 只以当前 Story／Session／Task 业务执行；中央模型及旧命令不再维持旧业务。
- [ ] Task 目标在创建、编码、校验、加载、事件与库存匹配中只认 DGR 身份。
- [ ] 类型级自动归组与旧指名写通道退出；具体实体／物品指名和当前安全规则完整。
- [ ] Choice 旧“已选择”Logic 输出与旧 `give_item` 输入退出；现行条件输入、结果、Flow 和奖励不受损。
- [ ] Manifest 退休字段及关联 schema／两端代码一致收口，无自动迁移或错误内容静默降级。
- [ ] Start、重复、Task 多／零结算、引用／内容复制、媒体与诊断等受保护功能无未解释回归。
- [ ] AUDIT_ONLY 每项有明确保留、死分支清理或待决策结论；未证明项没有擅自删除。
- [ ] 当前自动套件、必要实机矩阵和同包 E2E 有可复查结果；失败、跳过及环境缺口不计 PASS。
- [ ] 用户 Data、世界、原故事包与历史证据未被破坏；测试和回退范围准确。
- [ ] 权威开发产物与准确源码对应，EXE／DLL／JAR／测试包校验值真实；未做未授权成品发布。

**不能以编译通过、删了多少行或旧字符串搜不到代替以上条件。**若部分必需验证未完成，应报告“待验证／未交付的候选”及具体缺口，而不是声称 0.4.0.0 全部完成。

---

## 16. 来源索引与使用边界

### 16.1 用户提供的 14 份历史材料

这些材料用于还原决定的先后，不表示其中的旧状态、旧路径、旧默认值仍然有效。本轮用户确认及最新修正优先。附件 SHA-256 短码仅用于识别本次收到的副本，不是产品版本或验收结果。

| 索引 | 本次附件文件名 | 主要用途 | SHA-256 前 12 位 |
|---|---|---|---|
| <a id="h01"></a>H01 | `DarkGrey_RPG_0.3.3.0_Construction_PLAN(1).md` | 只删独立旧触发器；Start 保护；Task 纯 Logic 与执行／奖励基础。 | `a64e5dbb17d0` |
| <a id="h02"></a>H02 | `DarkGrey_RPG_0.3.3.1_Construction_PLAN(1).md` | 双编辑入口、跨 Story 边界、当前库存收集、角色交互提交、媒体磁盘路径。 | `2abccdf03b1e` |
| <a id="h03"></a>H03 | `DarkGreyRPG_0.3.3.1_Update_Summary(1).md` | 覆盖初稿的名称、表情差分、标题可选等待、复制与媒体后续修正。 | `282aec9e30c4` |
| <a id="h04"></a>H04 | `DarkGreyRPG_0.3.3.2_Construction_PLAN(1).md` | 玩家设置、任务追踪／历史、对话记录、留声机与媒体域。 | `661468dd67bb` |
| <a id="h05"></a>H05 | `DarkGreyRPG_0.3.3.3_Construction_PLAN(1).md` | 容量、客户端本地分屏、完整原句历史、组合／G 手势和两类音量。 | `4d031b72b682` |
| <a id="h06"></a>H06 | `0.3.3.2审计(1).docx` | 玩家 UI、任务奖励名称、自动播放、留声机与可读性原始反馈。 | `aed8a8cdc440` |
| <a id="h07"></a>H07 | `DarkGreyRPG_0.3.3.4_Construction_PLAN(1).md` | Enter／Backspace／IME、离线只读诊断、动态内容和重复条件。 | `d0802e80cab9` |
| <a id="h08"></a>H08 | `临时审计1.docx` | 组合交互、设置、引号／历史、指名器显示等后续人工反馈。 | `4f0198722b08` |
| <a id="h09"></a>H09 | `DarkGreyRPG_0.3.3.5_Construction_PLAN(1).md` | 旧 Choice 输出与新条件输入的区分、容量、物品匹配／分页、使用位置和转场。 | `daf3fadb7739` |
| <a id="h10"></a>H10 | `DarkGreyRPG_0.3.3.6_Construction_PLAN_FINAL(1).md` | 身份断代、非空内容追加、Group／Reference／容器、冲突与数据安全。 | `c510c4a6dc2c` |
| <a id="h11"></a>H11 | `DarkGreyRPG_0.3.3.7_Construction_PLAN(1).md` | 未来新任务自动追踪、独立 HUD、多输入 Logic、滚动和占位退出。 | `acf767f1e3b2` |
| <a id="h12"></a>H12 | `DarkGreyRPG_0.3.x.x_Closeout_Construction_PLAN(1).md` | 扫尾没有全面清旧栈；当前资料、可移植构建、Data 与发布边界。 | `762477216fdc` |
| <a id="h13"></a>H13 | `0.3.3.0审计(2).docx` | 25 项作者体验／跨 Story／媒体原始反馈；以之后确认覆盖中间意见。 | `5857c16b9565` |
| <a id="h14"></a>H14 | `0.3.3.1审计.docx` | 本次副本只有编号与一个“啊”，不作为实质需求证据。 | `edbeb90e71d6` |

### 16.2 固定源码定位

除 S05 特别标注外，以下均固定到本 PLAN 的 `51582ae8fe8a71214f2306ecd08492129171cff5`。S05 使用前轮已读取的 `d0d3f0e9f057793d1399568cc113f947fa1d86cc` 原始合同，当前架构与后续修正用于避免把旧计划恢复成新规则。

“本轮重读”只说明本次文档准备重新读取；“前轮同 SHA 已读”说明承接上一轮审计，不谎称本次再次全文读取每个文件。所有符号删除前仍须在实际工作区追踪完整调用者。

| 索引 | 固定文件 | 证据用途 | 读取边界 |
|---|---|---|---|
| S01 | [AGENTS.md][S01] | 交付、Data、目录与仓库 skills。 | 本轮重读 |
| S02 | [docs/CURRENT_ARCHITECTURE.md][S02] | 现行对象、身份、容器、准入、40%／60% 任务布局。 | 本轮重读 |
| S03 | [PLAN/0.3.3.7/Corrections.md][S03] | 最新 AND／OR 卡片、任务布局、改键与交付证据边界。 | 本轮重读 |
| S04 | [PLAN/0.3.3.6/RuntimeDirectoryGraphRepeatAcceptance.md][S04] | 管理器去掉删除／关闭按钮；新准入用当前重复规则。 | 本轮重读 |
| S05 | [PLAN/0.3.3.6/Technical_Contracts.md][S05] | 当前身份、保存合同、多／零结算及显式输出顺序。 | 前轮已读，固定于 0336 提交 |
| S06 | [src/main/java/darkgrey/rpg/DarkGreyRpg.java][S06] | Canonical 启动；旧 manager 参数与注册链。 | 前轮同 SHA 已读 |
| S07 | [src/main/java/darkgrey/rpg/project/ProjectSnapshot.java][S07] | 中央旧业务集合与当前 Canonical 内容并存。 | 前轮同 SHA 已读 |
| S08 | [studio/src/DarkGreyRPG.Studio.Core/Projects/ProjectService.cs][S08] | 旧文档集合、目录创建及旧项目拒绝。 | 前轮同 SHA 已读 |
| S09 | [studio/src/DarkGreyRPG.Studio/ViewModels/ShellViewModel.cs][S09] | 当前 Story 打开与 legacy workspace fallback。 | 前轮同 SHA 已读 |
| S10 | [studio/src/DarkGreyRPG.Studio.Core/Graphs/Resources/GraphResourceAddressCodec.cs][S10] | Studio Task registry_name 编码分支。 | 前轮同 SHA 已读 |
| S11 | [studio/src/DarkGreyRPG.Studio.Core/Identity/CurrentProjectValidator.cs][S11] | 目标 nativeTarget 例外与现有资源校验。 | 前轮同 SHA 已读 |
| S12 | [src/main/java/darkgrey/rpg/graph/canonical/GraphResourceAddressCodec.java][S12] | Java 读取原生目标形态的对应分支。 | 前轮同 SHA 已读 |
| S13 | [src/main/java/darkgrey/rpg/task/forge/CanonicalTaskEventAdapter.java][S13] | 实际 Kill 派发与目标同步入口。 | 前轮同 SHA 已读 |
| S14 | [src/main/java/darkgrey/rpg/task/forge/CanonicalTaskForgeEventNormalizer.java][S14] | 原生 Kill／Collect 与 DGR 身份事件辅助。 | 前轮同 SHA 已读 |
| S15 | [src/main/java/darkgrey/rpg/task/forge/CanonicalTaskInventory.java][S15] | 库存直接 registry 比较与当前 DGR 匹配。 | 前轮同 SHA 已读 |
| S16 | [src/main/java/darkgrey/rpg/client/gui/GuiNominatorEntity.java][S16] | 当前具体实体 UI 和统一操作发送路径。 | 前轮同 SHA 已读 |
| S17 | [src/main/java/darkgrey/rpg/client/gui/NominatorControls.java][S17] | 当前统一请求、revision、确认与关联。 | 前轮同 SHA 已读 |
| S18 | [src/main/java/darkgrey/rpg/nominator/NominatorActions.java][S18] | 当前实体／物品写操作与服务端核验。 | 前轮同 SHA 已读 |
| S19 | [src/main/java/darkgrey/rpg/network/message/nominator/C2SNominatorEntityBind.java][S19] | 旧 mutator 与 typeScope 分支。 | 前轮同 SHA 已读 |
| S20 | [src/main/java/darkgrey/rpg/network/NominatorNetwork.java][S20] | 旧写消息与当前统一消息同时注册。 | 前轮同 SHA 已读 |
| S21 | [src/main/java/darkgrey/rpg/nominator/NominatorService.java][S21] | 类型级归组与具体实体／物品服务边界。 | 前轮同 SHA 已读 |
| S22 | [src/main/java/darkgrey/rpg/identity/EntityDgrIdentityResolver.java][S22] | 具体宿主、CNPC 唯一性与 typeGroups 消费。 | 前轮同 SHA 已读 |
| S23 | [src/main/java/darkgrey/rpg/task/journal/CanonicalJournalService.java][S23] | Canonical Task 借用旧 Quest 运输壳。 | 前轮同 SHA 已读 |
| S24 | [src/main/java/darkgrey/rpg/network/DialogueNetwork.java][S24] | 现役共用网络与旧 Journal 注册。 | 前轮同 SHA 已读 |
| S25 | [studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/SessionChoiceSchema.cs][S25] | 旧 Logic 输出兼容、新条件输入与字段形态。 | 前轮同 SHA 已读 |
| S26 | [src/main/java/darkgrey/rpg/story/canonical/runtime/CanonicalStoryActionConfiguration.java][S26] | 旧 0.3.0.0 give_item 兼容 payload。 | 前轮同 SHA 已读 |
| S27 | [studio/src/DarkGreyRPG.Studio.Core/Packaging/StoryPackageManifest.cs][S27] | 必须为空的旧 dialogues／quests 字段。 | 前轮同 SHA 已读 |
| S28 | [studio/src/DarkGreyRPG.Studio.Core/Packaging/DgrsPackage.cs][S28] | 旧路径列表、当前包校验与 dormant 目标边界。 | 前轮同 SHA 已读 |
| S29 | [studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/GraphNodeFactory.cs][S29] | 当前工厂默认、pages 与 compatibility 参数。 | 前轮同 SHA 已读 |

上述是源码定位依据，不是允许跳过 Runtime 的包加载器、当前任务验证器、测试生成器或其他调用者。最终影响清单由 P0 与逐批施工补齐。

### 16.3 本轮确认取代的旧要求

本轮明确取代旧 Dialogue／Quest／legacy Story 兼容、Choice 旧 Logic 输出保留、任务原生注册名目标与实体类型自动归组的继续运行。旧文档中要求保留这些路径的段落，不再约束 0.4.0.0。

其余已确认功能与后续修正仍有效；尤其不能恢复原始方括号标题、旧表情术语、强制唯一结算、全部标题阻塞、Enter 只提交、只读项目图或管理器删除按钮。具体当前名称与展示以现行修正核验，不靠历史截图猜测。

较早的多版本／Universal DGRS handoff 仅为未来背景。本版不承诺全版本通用，也不把原生 API 隔离扩大成跨版本平台开发。

---

## 17. 可直接交给 Codex 的开工指令

> 以 `GreyHat633/DarkGreyRPG` 的 `codex/0.3.3.7 @ 51582ae8fe8a71214f2306ecd08492129171cff5` 为已核对基线，核对并保留实际工作区后，在 `codex/0.4.0.0` 新建或衔接本地工作。先读 AGENTS、相关仓库 skills、当前架构与0336／0337后续修正；不覆盖用户修改。
>
> 执行本 PLAN 的 CL-01—CL-11：让 Task 的 Kill／Collect／Submit 在 Studio 编码、验证、Runtime 解码、事件和真实库存匹配中只认 DGR 资源身份；删除类型级自动归组及旧指名写通道，保留具体实体／物品指名与精准／模糊匹配。删除旧 Dialogue／Quest／legacy Story 作者和执行体系，把现役调用者从旧 Repository／DTO／Journal／中央模型上先脱钩。退役 Choice 旧“已选择”Logic 输出、旧 give_item 注册名 payload 和 Manifest 旧空字段。
>
> 当前 Story／Session／Task 功能保持；Start 配置、多／零结算、公开输出优先级、条件 Logic 输入、结果与奖励、草稿／Undo、只读引用／非空内容追加、容量／动态文本／本地分屏、媒体租约和最新 UI 全部受保护。Dialogue 命名、Minecraft 注册名、UUID、兼容类名不是批量删除依据。
>
> P0 先冻结受影响 schema／format／SavedData／网络的准确当前合同；只维护一套新合同，不写旧格式迁移、不以旧快照或空写回掩盖拒绝。Start 历史条目、Normalize／默认值、ProjectOrigin 等仅按审计证据处理，新产品职责冲突才集中向用户说明，已批准项不反复请示。
>
> 按 P0—P6 分批完成，逐批建立现役依赖为零与正反向测试证据。使用真实 Studio 导出的同一份单包／组包，在隔离原版和 CNPC 环境验证具体指名、任务、选择、结算、奖励、禁用／冲突、重载及正常恢复；Windows／Minecraft 未执行时如实记录。
>
> 最后按仓库规范更新权威开发 dist，保护 Data，实际读回 EXE／主要 DLL／JAR／测试包的来源和哈希；没有额外发布授权，不创建成品目录、发布 ZIP、远端 Release 或标签。不做多版本 Runtime，不按文件数凑精简结果，不将测试、交付和用户验收混写。

<!-- 固定源码与附件索引链接定义 -->
[S01]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/AGENTS.md
[S02]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/docs/CURRENT_ARCHITECTURE.md
[S03]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/PLAN/0.3.3.7/Corrections.md
[S04]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/PLAN/0.3.3.6/RuntimeDirectoryGraphRepeatAcceptance.md
[S05]: https://github.com/GreyHat633/DarkGreyRPG/blob/d0d3f0e9f057793d1399568cc113f947fa1d86cc/PLAN/0.3.3.6/Technical_Contracts.md
[S06]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/DarkGreyRpg.java
[S07]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/project/ProjectSnapshot.java
[S08]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/studio/src/DarkGreyRPG.Studio.Core/Projects/ProjectService.cs
[S09]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/studio/src/DarkGreyRPG.Studio/ViewModels/ShellViewModel.cs
[S10]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/studio/src/DarkGreyRPG.Studio.Core/Graphs/Resources/GraphResourceAddressCodec.cs
[S11]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/studio/src/DarkGreyRPG.Studio.Core/Identity/CurrentProjectValidator.cs
[S12]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/graph/canonical/GraphResourceAddressCodec.java
[S13]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/task/forge/CanonicalTaskEventAdapter.java
[S14]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/task/forge/CanonicalTaskForgeEventNormalizer.java
[S15]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/task/forge/CanonicalTaskInventory.java
[S16]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/client/gui/GuiNominatorEntity.java
[S17]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/client/gui/NominatorControls.java
[S18]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/nominator/NominatorActions.java
[S19]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/network/message/nominator/C2SNominatorEntityBind.java
[S20]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/network/NominatorNetwork.java
[S21]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/nominator/NominatorService.java
[S22]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/identity/EntityDgrIdentityResolver.java
[S23]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/task/journal/CanonicalJournalService.java
[S24]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/network/DialogueNetwork.java
[S25]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/SessionChoiceSchema.cs
[S26]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/src/main/java/darkgrey/rpg/story/canonical/runtime/CanonicalStoryActionConfiguration.java
[S27]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/studio/src/DarkGreyRPG.Studio.Core/Packaging/StoryPackageManifest.cs
[S28]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/studio/src/DarkGreyRPG.Studio.Core/Packaging/DgrsPackage.cs
[S29]: https://github.com/GreyHat633/DarkGreyRPG/blob/51582ae8fe8a71214f2306ecd08492129171cff5/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/GraphNodeFactory.cs
[H01]: #h01
[H02]: #h02
[H03]: #h03
[H04]: #h04
[H05]: #h05
[H06]: #h06
[H07]: #h07
[H08]: #h08
[H09]: #h09
[H10]: #h10
[H11]: #h11
[H12]: #h12
[H13]: #h13
[H14]: #h14
