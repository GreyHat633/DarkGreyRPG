# DarkGreyRPG 0.3.3.5 施工 PLAN

**版本：** 0.3.3.5  
**整理日期：** 2026-09-30  
**适用对象：** Codex／实际施工代理  
**仓库：** `GreyHat633/DarkGreyRPG`  
**固定基线：** `codex/0.3.3.4 @ 515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf`  
**建议施工分支：** `codex/0.3.3.5`  
**文档状态：** 施工规格；不是已经实现、测试通过或用户验收的证明。

> 本版不是单纯补测试。必须完成四项审查问题的修复，并交付三项产品能力：Inspector「使用位置」、【选择】选项逻辑条件、【画面】转场及 PPT 式平滑。不得只完成基础修复便宣布 0.3.3.5 完工；也不得借这些功能重做 Story／Task／Session 架构。

---

## 0. 阅读顺序与约束来源

### 0.1 优先级

1. 本对话中用户最后明确同意的产品规则。
2. 本 PLAN 对这些规则的整理，以及明确标注的施工默认值。
3. 固定基线中的现有正式行为和后续修正记录。
4. 更早版本 PLAN／handoff，仅作背景，不得用旧文档回退用户后来修改的行为。

本文中“源码现状”是读取固定提交得到的事实；“施工要求”是本版需要实现的目标；“施工默认值”用于补齐尚未逐项指定的交互细节，不冒充用户曾逐字确认的原话。内部类名和新增字段名可在定位后调整，但不得改变产品语义。

本轮只编写 PLAN，未修改仓库，未执行 0.3.3.5 产品构建或实机验收。此前审查报告的测试边界继续有效。

### 0.2 施工前必读

- `AGENTS.md`。
- `.agents/skills/studio-node-ui/SKILL.md`。
- `.agents/skills/story-media-lifecycle/SKILL.md`。
- `PLAN/0.3.3.4/GITHUB_PUBLICATION_2026-09-30.md`。
- 本文末尾的固定提交源码索引。
- 本对话提供的 `DarkGreyRPG_0.3.3.4_Code_Review_515b374.md`；其 R1—R4 是修复起点，具体处理方式以用户后续修正及本文为准。

施工前检查工作树。保留用户未提交的修改、正式项目、故事包和世界；不得 `reset --hard`、`clean`、覆盖正式测试数据，或为了生成整洁分支丢失本地工作。

### 0.3 本版明确不做

- 批量属性编辑。
- 【玩家等级】【持有物品】【任务状态】等第二套玩家状态检测节点。
- 新的 Flow 触发器、背包轮询触发系统、通用条件表达式／脚本语言。
- 通用【等待】节点、摄影机节点、时间轴、完整动画编辑器。
- 同一句台词的局部速度／停顿编辑。
- 快速阅读、已读跳过；不顺手加入临时隐藏对话界面的新入口。
- NPC 环境气泡、素体和素体编辑器。本次只保留后续待办归属：气泡能力以后在素体／编辑器阶段讨论，不放进 Story Studio。
- 新的 ItemID、NPCID、BlockID、MusicID、用户可见 ImageID。
- 用客户端物品候选查看代替服务器的任务提交、扣物、奖励事务。
- 重做留声机生命周期、Story 媒体缓存策略或 0.3.3.4 已完成的字体清晰度修正。

---

## 1. 本版范围总览

| 工作包 | 交付内容 | 核心边界 |
|---|---|---|
| A · 文本及头像修复 | 动态文本预警、展开后的真实上限保护、容量标尺重新标定、旧头像残留修复 | 不篡改作者正文，不回退字体修正，不取消本地分屏 |
| B · 任务物品组展示 | 单格轮播、悬停候选菜单、按页获取、有界缓存、整包大小保护 | 单体物品仍为固定图标；不改变物品匹配和提交语义 |
| C · 使用位置 | 右侧 Inspector 默认折叠的反向引用列表，点击精确定位 | 不放右键菜单，不另造大型资源关系图 |
| D · 条件选项 | 每个选项的 Logic 条件输入，False 时隐藏／置灰，服务器复核 | 只消费既有 Logic，不新建状态检测节点 |
| E · 画面演出 | 淡入淡出、滑入滑出、擦除、随机线条、平滑 | 画面仍为完整状态；无用户图像身份管理、无时间轴 |
| F · 集成及交付 | 配套数据升级、说明、受影响回归和正式产物 | 不把编译／截图／代理自评替代用户验收 |

任务完成历史的按需读取与整份任务快照预算，是 B 中“大内容无法同步”的必要配套，不是另一套任务玩法或历史系统。

---

## 2. 源码现状：不得遗漏的实施事实

### 2.1 Choice 不是“新建就有已选择 Logic 输出”

`SessionChoiceSchema.cs` 明确规定：当前新选项只创建稳定 Flow 输出；ID 等于 `option_id` 的 Logic 输出仅为旧数据的保留兼容端口，新建作者内容不再创建。[S04]

因此，前面讨论中“每个新选项已经自带已选择 Logic 输出”的说法不能当作实施依据。本版新增的是**条件 Logic 输入**；不顺手复活所有新选项的旧 Logic 输出。现存合法连线不得破坏，需要选择后的 Logic 时继续用现有【流程判断】及公共逻辑接口。

### 2.2 当前 Screen 严格校验字段，没有转场或图层对应关系

C# `CanonicalSessionPresentationSchema` 目前让 `screen` 只接受 `layers`，单图层接受 `media_ref/x/y/width/height/anchor_x/anchor_y/z`，最多 32 层。Java 同样严格解析。[S05][S06]

所以平滑不能只在客户端加动画函数：C# 编辑模型、保存／复制、导出验证、Java 解析、Presentation、网络投影和渲染都要接通。新增字段不能被旧 setter 重建字典时丢掉。

### 2.3 自动推进会合并连续的画面变化

`CanonicalSessionRuntime.resolveAutomatic()` 连续执行 `music/screen`，直到台词、选择等驻留点；当前帧主要承载最终 Presentation。给 Screen 增加转场参数，不会自动变成逐张等待的幻灯片播放。[S06]

本版继续保持非阻塞画面语义，具体连续切换规则见第 9 节；不得未经确认添加新的流程等待点。

### 2.4 Logic 输入变化与“流程已经推进”是两回事

当前 `setLogicInput()` 会重新计算 Logic，但返回 true 的主要条件是等待中的【条件判断】继续走了 Flow。停在 Choice 时，不能假定只增加端口便会自动刷新菜单。[S07]

需要单独接通“选项可用性改变”的展示更新，不能为了刷新菜单伪造一次流程推进、增加 line epoch 或重播语音。

### 2.5 物品组的“四格预览”只是 UI 截取

`ItemSlotStrip` 当前仍从传入的全部 `items` 建立 ItemStack 列表，折叠时只画前四个；服务端候选投影也仍可能产生完整列表。[S09][S10]

本版必须同时改变数据取得方式和绘制方式，不能只把四格画成一格，而后台仍每次发送整个物品组。

### 2.6 现有搜索可以复用定位，却不是准确的反向引用

`AuthoringSearchHit` 已包含 Story／Resource／Node／Page／Option 等定位信息，适合复用；当前文字搜索不能证明某段文字就是资源引用。`NavigateSearch()` 对只读项的提前返回也要注意，不能让“能列出但点不进去”的情况进入新功能。[S08]

### 2.7 0.3.3.4 后来的行为优先保留

- 连续台词编辑沿用 0.3.3.4 的 Enter 新句、空句 Backspace 和中文输入法保护；不要改回 0.3.3.3 的单纯 Enter 提交。
- 资源名称动态块沿用 typed ID 引用，不把它们转换成游戏物品实际名称或裸字符串。
- 150% 是玩家选择档位，不一定是最终绘制倍率；实际倍率由字体像素对齐规则决定。
- 任务回顾中真实目标快照与当前定义参考继续分开；不得填造历史。
- Story Start、重复资格、跨 Story 的 Flow／Logic 接口和运行中禁止重入等规则保持。

---

## 3. 需求追踪表

施工报告必须对以下每项给出：实现文件、自动检查、实机证据或未覆盖原因。不得只填一个总 PASS。

| 编号 | 必须交付的要求 |
|---|---|
| A01 | Choice 每个选项就地显示动态内容长度风险，不替换作者文案 |
| A02 | 静态内容、名称引用、运行时动态值分开精确计算／保守估算 |
| A03 | 服务端展开后、冻结展示值前执行字段级真实字节验证 |
| A04 | 异常处理不截字、不伪造选项、不写入超限展示快照、不误推进 |
| A05 | 按最终 Runtime 的有效字体倍率与正文几何重新标定容量 |
| A06 | 保留一次 10% 安全余量、容量预警和正常本地分屏 |
| A07 | 当前头像引用不同且未就绪时，禁止借用上一张头像 |
| B01 | 单体物品保持固定单格；物品组也只占单格 |
| B02 | 物品组所有有限候选依序参与轮播，不能永久只轮第一页 |
| B03 | 悬停展开可交互菜单，按实际数量收缩，每页上限默认 20 |
| B04 | 菜单翻页、原生物品提示、屏幕边缘摆放和鼠标过渡完整 |
| B05 | 候选按页请求，常规任务更新不携带全组 ItemStack |
| B06 | 分页授权、上下文版本、旧响应拒绝、限流和字节预算完整 |
| B07 | 轮播和页缓存有界，不可见时停止主动预取，离服清理 |
| B08 | 任务快照及完成历史按需／有界传输，不删除真实数据或省略目标 |
| B09 | 发送状态不在编码失败前提前记成已发送；失败可受控重试 |
| C01 | 使用位置只在右侧 Inspector 提供默认折叠入口 |
| C02 | 覆盖角色／角色组、物品／物品组、会话、任务和已有媒体引用 |
| C03 | 通过 typed references 建立准确索引，不以显示名全文搜索代替 |
| C04 | 当前项目内未打开内容与已加载只读引用也可查、可定位 |
| C05 | 点击定位到具体节点／台词页／选项／属性，不改变数据或权限 |
| C06 | 修改、改名、复制、撤销、导入和关闭项目时正确更新及释放 |
| D01 | 每选项一个稳定 Logic 条件输入，未连接默认可选 |
| D02 | True 可选；False 按作者设置隐藏或显示但不可选择 |
| D03 | 不可选择说明只在相应模式需要时显示，双编辑入口一致 |
| D04 | 不新增状态判断／触发器，不改变 Objective 现有含义 |
| D05 | 服务器生成菜单并在接受选择时复核，不信任客户端可选状态 |
| D06 | Logic 变化只刷新必要菜单，保留阅读、冻结文字和媒体状态 |
| D07 | 全部不可用、旧点击、键盘焦点、自动播放和历史均有确定行为 |
| D08 | 增删排序／复制／剪线／撤销同步维护条件端口和连线 |
| E01 | Screen 支持无、淡入淡出、滑入滑出、擦除、随机线条、平滑 |
| E02 | 转场属于目标 Screen；最终画面始终严格等于目标完整状态 |
| E03 | 图层内部对应标记不暴露给用户，不等同资源 ID 或媒体指纹 |
| E04 | 复制 Screen 保留对应关系，同图多实例不得串配 |
| E05 | 平滑支持位置／大小插值、旧层淡出、新层淡入 |
| E06 | 转场与台词并行，不生成通用等待、时间轴或自动剧情事件 |
| E07 | 重发、续接、分支及中途中断遵守唯一演出身份，不重复动画 |
| E08 | 旧／新纹理使用受保护且有界；会话结束与断线完整释放 |
| E09 | 原预览区可预览转场，保留对话框／选项框参考及图像编辑 |
| F01 | 修改 C#／Java／网络／存储边界时配套校验，不运行两套 Runtime |
| F02 | 更新「介绍说明」、视觉检查、真实同包 E2E 和正式产物 |

---

## 4. 工作包 A：动态文本、容量与头像

### 4.1 先分清两类限制

**文本容量**回答“这一屏大约放得下多少”，用字形宽度与排版计算，超出后可由客户端分屏。

**传输长度**回答“服务器能否把这段内容完整送给客户端”，按最终文本的 UTF-8 字节数检查。客户端还没收到文本前，分屏不能解决传输失败。

固定基线中的主要边界：[S11]

| 字段 | 当前硬边界 |
|---|---:|
| 单个选择选项正文 | 2048 UTF-8 字节 |
| 台词／选择提示正文 | 32767 UTF-8 字节 |
| 说话人名称 | 256 UTF-8 字节 |
| Choice 选项数量 | 32 |

施工前核对实际调用链；新增不可选择说明也要有明确的字段级及整帧上限。不得把内部动态结构 131072 字符上限当作所有展示字段都能发送的证明。

### 4.2 Studio 的预警方式

用户要求是提前在 Studio 看出动态内容过多的风险，而不是游戏中把选项改成「动态内容过长」。

实施规则：

1. 对【选择】逐个选项计算，不将整个节点的数量作为每个选项上限。
2. 普通文本按实际 UTF-8 字节精确计算。
3. `actor_name/item_name` 用当前项目中的真实资源名称展开计算；不是运行时物品 Tooltip 名称。
4. `player_name/player_level/item_count` 用既有取值边界推导保守预算。普通原版名称边界与第三方模组可能修改的值必须区分，不宣称无限 Mod 值可预知。
5. 重复出现同一动态块也逐次计入，不按去重后的资源数计算。
6. 编辑、插入、粘贴、资源改名和撤销后更新；名称改变导致其他选项超限时，这些使用位置也必须重新校验。
7. 保持紧凑。选项中有动态内容时可显示小号的“动态内容 N 项”；正常状态不铺满技术数字。接近／超过预算时显示明确警告，技术字节细节放悬停和问题面板。

建议文案：

> 选项展开后可能超过可传输长度，请缩短文本或减少动态内容。

“动态内容数量”可以帮助理解，但不能武断设成“所有选项最多 10 块”：短等级数字和长资源名称占用不同。上限判断依据展开后的长度预算。

风险估算属于预警：不吞输入、不自动删块、不替换文案，不把普通显示容量或动态块数量警告改成硬禁保存／导出。格式错误、缺失引用仍遵守现有合法性校验。明确已经超过协议边界的情况要以最高风险展示给作者，不能标成安全；它即使进入外部包，也由下一节的最后保护受控拒绝。

### 4.3 服务端最后保护：拒绝非法展示，不改写内容

在 `DynamicContentResolver → displayText/presentationText → frame` 链路中，按最终槽位执行：

```text
取得原模板
→ 解析真实动态值
→ 用目标字段的 UTF-8 上限检查完整结果
→ 合法才冻结该作者页的展示值、构造并提交报文
```

必须保留当前“同一作者页的动态结果冻结，重发／重连不随意重新求值”的行为。条件菜单的可用性刷新不能顺便让同一句动态正文变化。

异常约束：

- 不把正文或某个选项替换成“动态内容过长”；不静默截断。
- 不只删除超长选项而发送剩余菜单，这也会改变作者给玩家的选择。
- 不把非法结果先存入 `presentationTexts`，随后每次重发再次失败。
- 不因展示失败自动选择、完成会话、重置 Story 或重发奖励。
- 通过现有日志／独立诊断反馈标记 Story、Session、节点、作者页／option、槽位和实际／允许字节数；不要把完整隐私文本灌进日志。
- 客户端等待状态必须可解除并得到受控的非剧情错误反馈，不能一直灰着所有按钮。错误反馈与作者台词是两个通道。
- 保留已经合法发生的运行状态，不做跨任务回滚；修正内容后按既有重载／续接机制恢复，而不是制造第二套执行器。

回归向量包括审查中的 40 个 `item_name` 块：短模板展开为 20 汉字 × 40，得到 2400 UTF-8 字节。必须在写入非法展示快照之前识别选项 2048 字节越界。

### 4.4 容量标尺重新标定

保留用户接受的 0.3.3.4 字体、头像、名字和按钮布局。**不为继续使用旧容量数字而缩回字体或挤回标题栏。**

依据最终代码重新取得：

- 最低支持的合法 GUI 逻辑尺寸。
- 所有实际可达的 GUI 因子及玩家 100%／125%／150% 档位；容量保证以玩家最大档及其实际有效倍率为基准。
- normal／Unicode 标准字宽。
- 有角色、有头像、名字栏、引号和自动／记录按钮时的正文区域。
- 实际换行宽度、行高、完整行数。

不能继续固定“150% 一定是三行”；当前有效倍率在部分因子下是 200%，正文也会因名字栏增高而变窄／变低。[S12]

实施原则：

1. 由 Runtime 的真实几何计算生成可重现的标定数据，Studio 使用同一数值来源或经过交叉验证的对应模型。
2. 在合法支持域中取保守容量，并保留一次 `SAFE = floor(RAW × 0.90)` 的约 10% 余量。
3. 不用重复扣减 10%、额外砍掉一整行等方式代替精确测量。
4. 不强行维持旧示例 160／144，亦不沿用旧 profile 的 375／337；正式数值在实测后记录。
5. 不保证第三方字体包。客户端仍按实际字体排版、溢出本地分屏。
6. 不统计纯字符数；保留不同字形宽度及自动折行造成的剩余空间消耗。
7. 不只取几个版面的总面积最小值；换行宽度与行数的形状同样影响容纳结果。必要时对同一句在各支持规格下分别测量，再采用最不利结果，不能用面积相同证明排版相同。
8. 验证“推荐值内的代表内容在最坏支持规格下可完整显示”，而不只验证生成器与消费器自洽。

显示容量超限仍可保存／导出并在游戏本地分屏。分屏不增加作者页，不重播同一句语音；最后一个本地分屏之后才发送正常继续。历史仍立即记录完整作者页，不做分段防泄露。

### 4.5 头像引用严格对应

对话显示必须使用 `getVisiblePortraitRef()` 等当前有效的台词上下文，而不是 Choice 帧本身可能为空的头像字段。

```text
当前期望头像引用 == 已显示头像引用，且纹理有效
→ 可以复用

当前期望头像引用已经变化，新纹理未就绪
→ 显示现有加载占位；不得借上一张图

当前没有头像
→ 按现有无头像布局，不残留旧图
```

同角色换差分也是不同引用，不能继续拿旧差分冒充新差分。此版按引用一致性解决，不需要另加角色识别系统。Choice 保留前一条台词头像的现有行为必须继续正常。

保留 0.3.3.4 头像留白、长名字居中及 Tooltip、字体清晰度。断线、会话结束、资源重载时清理显示引用，但不要删除仍可复用的磁盘媒体。


---

## 5. 工作包 B：任务物品组单格轮播与候选菜单

### 5.1 显示对象和适用范围

主要作用于任务菜单中【物品收集】【物品提交】等目标的物品展示、提交候选相关界面，以及已有任务 HUD 中的对应图标。

- 单体物品：一个固定物品格，显示实际绑定物品的图标与原生提示。
- 物品组：也只占一个物品格，依次轮换组内满足该目标条件的候选示例。
- 奖励条目继续依照现有单体物品／经验规则。一项奖励一个相应展示；不得借此把奖励改成物品组奖励或随机发奖。
- 多项不同奖励并不是一个物品组，不得把整个奖励列表压成一个轮播格。
- HUD 不接管鼠标，不新增鼠标解锁模式；完整候选菜单在可交互 GUI 中使用。

数量语义必须明确：物品组目标要求 10 个，表示按既有匹配规则累计的总需求，不是组内每一种都要 10 个。轮播不能让数量标记看起来随候选切换而改变。

### 5.2 候选必须与真正匹配规则一致

复用 `ItemIdentitySavedData`、物品组成员定义和 `CanonicalTaskInventory.matches` 的语义。客户端不得自行扫描整个注册表猜候选。

精准／模糊匹配保持：

- 精准：按现有类型、耐久及附加数据规则。
- 模糊：按既有 registryName 规则，并尊重目标额外条件。
- 模糊项的一个图标是示例，不承诺穷举所有可能 NBT、耐久组合。

“轮播所有物品”指当前服务端绑定列表经过目标条件过滤后的有限候选集，不是为一个模糊成员枚举无穷物品变体。

类型取自项目资源身份，不能以“成员数是否大于零”推断是不是物品组；空物品组也仍然是组。保留未绑定、定义缺失、单项展示过大等原因，不把加载失败显示成“没有候选”。

### 5.3 轮播行为

施工默认值：正常轮播间隔 **1.2 秒**，时间由单调时钟计算，不受刷新帧率影响。此值是本 PLAN 的交互默认值，不是用户之前指定的固定数字。

- 依次轮播，候选稳定排序／去重后保持一致顺序。
- 一个成员时固定显示；零个成员显示现有缺失／空组占位及原因。
- 快轮完当前已缓存页时预取下一页；最终能轮到全组，而不是永远只播首 20 个。
- 下一页尚未到达时保留已有图标或继续已有有限序列，不闪空格，不阻塞界面。
- 悬停图标／候选菜单期间暂停外层轮播，避免正在看的物品被换掉。
- 离开悬停后从当前位置继续，不必每次重置第一个候选。
- 修改绑定版本后旧候选页作废，不能新旧两版本混播。

### 5.4 悬停次级菜单

**入口是悬停物品组格，不是额外“查看全部”按钮，也不是双击打开大窗口。**

施工默认：悬停约 250ms 展开；鼠标离开图标和菜单的联合区域约 200ms 后关闭。允许为防误关增加很小的过渡区域，不能把整个任务窗口都算作保持区域。

菜单要求：

1. 默认每页最多 **20 个**，正常布局 5 列 × 最多 4 行。
2. 很少的候选只占实际需要的列和行，不保留四行空白。
3. 小窗口可减少列数；面板达到可用最大高度后继续翻页，不越出屏幕。
4. 翻页控件是菜单的一部分，鼠标从原图标移动进去时不能消失。
5. 菜单靠右则向左展开，靠下则向上展开，并避开当前主要目标内容。
6. 有下一页才显示相应控制；稳定版本下翻页顺序一致，返回上一页不跳号／遗漏。
7. 悬停菜单内某个候选显示该物品自身的名称、附魔、Lore 和 Mod 提示。原生提示不得挡住关键翻页命中区。
8. Esc 先关闭本次展开菜单，不顺手关闭任务窗口；菜单内滚轮／翻页／点击被本菜单消费，不穿透成任务提交或底层按钮点击。
9. 切换任务、提交上下文失效、窗口关闭时关闭菜单；旧请求不能让它重新弹出。
10. 菜单只是查看。点某个物品不领取、不移动、不扣除，不改变“向哪个 NPC 提交”的现有规则。

外层格应有轻量的组提示，避免玩家误以为只接受当前轮到的物品。可以使用角标／短 Tooltip；不额外扩展一个物品格的布局宽度。

### 5.5 数据取得方式必须一起改变

普通任务快照只携带：

```text
展示类型：单体／物品组
目标及任务实例上下文
稳定的物品组身份或服务端候选上下文标记
候选版本
总候选数（或明确的统计中状态）
少量首屏／首个图标数据
```

不要在每次进度更新时附上完整候选集。轮播预取和悬停菜单复用同一个分页取得服务，不各建一套缓存。

请求至少绑定：玩家连接、任务实例／placement、objective、包与绑定版本、分页位置、请求序号。仅以 `GroupID + page` 请求不足以表达同一个组在不同目标下的过滤条件。

服务端重新核对：

- 请求玩家是否确实可查看对应任务／当前提交候选上下文。
- 未激活隐藏目标不得通过猜 ID 获取详情。
- 任务定义、绑定及当前候选版本是否匹配。
- 只读查看请求不得执行提交、发奖、进度推进或库存修改。
- 同名不同身份、同一资源不同 placement、两个玩家不能互相串缓存。

### 5.6 每页数量与字节预算同时约束

“20 个／页”是图标数量上限，不等于任意 20 个复杂 NBT 都必然安全。

- 保留单物品展示大小上限，并对每次响应执行真实编码／NBT 计量检查。
- 页面最多 20 项，但允许因总预算只返回较少项；用明确的下一游标续接，不能静默丢掉没装下的候选。
- 此时可显示“第 N 页 · 共 M 个候选”，不要用固定 `ceil(M/20)` 伪造实际页数。
- 单个非法展示项返回对应占位及原因，服务端真实匹配仍按原规则；不要修改物品。
- 拒绝非法页号、过大长度、旧版本／重复／旧窗口响应；同一页并发请求合并。
- 设置按玩家和全局的有界请求、待处理数量与内存预算；不能仅把压力从基础快照搬到无限分页队列。

不提高 `CreatorSnapshot` 既有 1 MiB 压缩／2 MiB NBT 计量上限冒充解决。新增候选报文同样须记录并验证独立上限。[S10]

### 5.7 缓存生命周期

这是物品展示缓存，不属于 Story 图片／音频缓存；不要拿“3 包／10 包”名额来管理图标页。

施工默认策略：

- 仅当前可见物品格参与主动轮播预取。
- 图标轮播保留当前／下一页，菜单保留当前页及少量返回页；全局使用有界 LRU，建议起点为最多 16 页且展示数据预算 4 MiB，取先达到的边界。
- 页数与字节参数是可调工程常量，不向玩家增加设置；实际内存评估需包含 ItemStack／NBT 对象开销，而不只计算压缩包。
- 当前正在展示的页受短期使用保护；全被使用时停止额外预取，不能无限增加缓存。
- GUI 关闭停止它的轮播／预取和请求所有权；有效已完成缓存可以短暂保留供重开，但仍受总预算。
- 离服／换存档清理连接相关缓存、请求和绑定；同服换维度也必须重新验证具体任务上下文，不能借旧菜单越权。
- 旧异步响应仅可进入同版本仍有效的缓存，不能复活已关闭菜单或覆盖新任务。

### 5.8 基础任务快照与完成历史的总预算

只分页物品组还不足以关闭 R2：大量任务描述／完成历史仍可能使基础快照超限。

必须完成以下配套，不新增任务玩法：

1. 常规同步优先提供当前任务与必要进度；完成历史内容在打开相应页面后按需取得。
2. 持久化完成次数、真实目标快照、当前定义参考分开保留；按需读取不等于删除历史。
3. 大量 ACTIVE 任务也不能无限塞一个报文；必要时按同一版本分块组装完整快照，或按稳定任务摘要＋按需详情取得。
4. 客户端不能把半份新快照当完整状态，不能把尚未到达的目标误删；全部激活目标仍可访问，已追踪目标不静默缺失。
5. 同版本重复候选描述尽量复用，不为每一个进度变化重建全部复杂 ItemStack。
6. 只有成功编码并提交现有发送通道后，才更新“已发送内容”的本地记录；编码／提交失败继续保留待发送状态。不要把进入发送队列夸大成玩家已经收到，更不要无必要另造通用 ACK 系统。

---

## 6. 工作包 C：Inspector「使用位置」

### 6.1 入口和布局

入口固定在**右侧 Inspector**，默认折叠；不增加右键菜单入口，不要求新快捷键。

```text
资源名称：酒馆老板
……现有属性……

使用位置（7）  ▼／▲
```

展开后按当前项目的 Story／资源组织结果，示例：

```text
酒馆故事 ＞ 初次见面 ＞ 台词 · 第 3 句 · 说话角色
酒馆故事 ＞ 寻找货物 ＞ 目标 · 提交对象
王城故事 ＞ 传闻 ＞ 台词 · 第 2 句 · 角色名称引用
```

复用现有主题、说明文字和 220ms 折叠动画。默认只占一行标题，结果多时在区域内有界滚动，不把所有资源配置挤出 Inspector，也不在画布节点里展开一份长使用列表。[S02]

如果当前资源类型主要通过现有弹窗编辑，最小新增一个右侧只读资源检查上下文承载本卡片即可；不要顺手迁移所有资源编辑器或重做整个 Inspector。选中节点时仍以节点编辑为主；上下文标题要清楚标明当前查的是哪一个资源／媒体。

### 6.2 查询范围

范围是**当前项目中可读取的内容**：本地原生资源、未打开的 Story／Session／Task、已加载的只读引用包。不能声称知道外部所有项目的使用情况。

支持：

- Actor、Actor Group。
- Item、Item Group。
- Session、Task。
- 默认头像／差分图片、Screen 图片、台词语音和音乐等已有媒体引用。

本版不需要独立媒体资源库。选中现有图层／头像／语音字段时，可以在同一 Inspector 以“当前媒体”上下文查看使用位置。

结果应区分实际字段引用、资源声明／拥有关系和可推导的间接使用。不能把 membership 里的一条声明当作第二处真实台词引用重复计数。

### 6.3 建立 typed 反向引用索引

键必须包含资源种类与稳定身份；媒体用内容指纹引用。不能使用显示名称、文件名相似度、字符串包含或图层序号来认定引用。

每个使用位置至少携带：

```text
被引用对象：ResourceKind + StableID（或媒体引用）
来源：Project/Provider + Story + ResourceKind + ResourceID
定位：NodeID / PageID / OptionID / 图层或字段路径
用途：说话角色、提交对象、资源名称动态块、奖励物品、画面图层等
权限：可编辑／只读
```

例如台词里普通打字写“酒馆老板”不构成引用；真正的 `actor_name` 块构成引用。同名不同 ID、不同 ResourceKind 的相同字符串不得串联。[S08][S14]

新增 Choice 的相关字段和 Screen 对应标记要进入正确的 schema 访问器：条件端口连线不是物品引用，图层对应标记也不是媒体资源。

### 6.4 点击定位

复用现有 Story／资源导航、稳定 Page／Option 定位和必要的展开行为。

- 点击后打开正确图，选中对应节点，定位具体句子／选项／属性。
- 不为了定位展开整个项目或全部台词卡片，只展开必要路径。
- 只读引用可以定位查看，但不可编辑；不能导入成副本后假装完成定位。
- 目标在点击前已经删除或改变时，刷新结果并给出简短提示，不能跳到同名对象。
- 查询和导航不改变文档 dirty 状态，不重新生成身份，不丢失当前编辑草稿。

### 6.5 更新与生命周期

以项目上下文持有可重建索引，不把“反向引用表”变成第二份权威项目数据。

- 当前打开编辑器的已提交内存状态优先于磁盘；不因扫描磁盘覆盖正在编辑的正文。
- 索引随新增／删除／复制／改名／替换媒体／导入引用／撤销重做增量失效或重建。
- 项目切换后取消旧查询；旧结果不得填入新项目 Inspector。
- 列表折叠时不重复做无必要的全量解析；展开时可以显示“正在查找”，不能暂时显示一个误导的 0。
- 不为查使用位置解码所有 PNG／OGG，也不预热游戏媒体。
- 项目关闭释放索引、文件读取句柄、事件订阅和结果视图；不长期保留一套隐藏完整编辑器。

---

## 7. 工作包 D：每个选项的 Logic 条件输入

### 7.1 最终职责边界

> 【目标】及既有运行机制负责产生／保存相关状态；Logic 连线负责传递和组合；【选择】只依据输入真假决定某个选项是否可用。

不增加【持有物品】【玩家等级】【玩家状态】节点，不增加后台背包监听器，也不在 Choice 配置里另造一套条件表达式。

【物品收集】完成后的逻辑可能保持 True；这表示目标已经完成，**不保证玩家此刻仍持有物品**。不得为了“出示通行证”的例子偷偷将完成状态改为实时库存状态。

跨 Task／Story／Session 的信号通过现有公共 Logic 输入／输出接口传递，不能跨越图边界直接把内部 Objective 节点 ID 填进 Choice。

### 7.2 端口及参数

Choice 保留现有一个 Flow 输入、每选项一个 Flow 输出。对每个选项新增一个稳定的 Logic 输入，例如内部字段：

```text
option_id                 原有稳定选项身份
flow_port_id              原有流程出口
condition_port_id         新增，稳定 Logic 输入身份
unavailable_behavior      hide / disable
unavailable_hint          作者填写的不可选择说明，可为空
```

这些字段名是建议实现名。最终 schema 应有唯一映射、方向和种类校验；不能按“第几个选项”生成会随拖序变化的身份。

- 新建选项即拥有相应条件输入，作者不接线即可正常使用。
- 端口显示简短的“条件 · 选项文字”，悬停可看完整对应选项；与 Flow 出口在位置和 Logic 图形上明确区分。
- 只保留基线里已有的合法旧 Logic 输出；不得给所有新选项重新生成“已选择”Logic 输出。[S04]

### 7.3 真值表

| 输入情况 | 可用性 | 游戏显示 |
|---|---|---|
| 条件端口未连接 | 可选 | 正常显示 |
| 有合法连接且值为 True | 可选 | 正常显示 |
| 有合法连接且值为 False，模式 hide | 不可选 | 不显示此选项 |
| 有合法连接且值为 False，模式 disable | 不可选 | 显示原选项但禁用；可显示作者说明 |
| 连线结构缺失／无效 | 不是“未连接” | 走图合法性错误，不能默认解锁 |

特别注意：既有通用 `logicInputValue()` 对未连接返回 false，Choice 这里必须先判断是否存在连接，不能直接套用而让新菜单全部消失。

False 模式的施工默认值为“隐藏”；作者可选“显示但不可选择”。说明字段只在后一种模式有意义，平时不占额外高度。它不是脚本字段，不需要作者输入技术表达式。

### 7.4 Studio 双入口

仍是紧凑选项列表，不把每一项自动变成庞大的多层卡片。节点和 Inspector 使用同一数据模型／组件。

每项可有一个紧凑的“条件设置”展开区，提供 False 行为与提示文本；逻辑条件本身通过画布端口接线，不能再配一个重复的条件下拉表达式。

增删、拖序、复制节点／参数、撤销和重做要求：

- 选项文字改动不改变端口身份；同步标签。
- 拖序只改变显示顺序，已有连线跟随同一个 option。
- 删除选项同步删除该输入与输出上的相关连线，一次 Undo 完整恢复。
- 跨图复制按既有规则重建新节点／端口身份并重映射内部连线，不丢条件线。
- 新增的静态／动态提示字段如采用现有动态编辑能力，必须同时接入长度验证、typed 引用索引与服务器展开；不能只在 UI 上接受却在运行时原样显示编码。

### 7.5 Runtime 选择与刷新

服务端产生 `visible/enabled/hint` 等展示结果；客户端不自行求值 Logic，也不能凭按钮是否变灰决定服务器必须接受。

接受选择时按下列顺序：

```text
核对玩家、会话、Story、当前 Choice、有效选择轮次
→ 按稳定 option_id 找到选项
→ 读取当前既有 Logic 状态，重新判断该条件
→ 仍可选才记录选择并推进对应 Flow
```

失效点击或恶意提交隐藏选项时，拒绝并重投影当前 Choice；不要抛出会把正常菜单变成 FAILED 的异常，也不要重置故事。

可用性更新与文字冻结分开：

- 在进入 Choice、既有外部 Logic 更新、恢复会话和点击复核时计算可用性。
- 外部 Logic 更新需要经过现有传播链路；不得另加轮询玩家等级／背包的检测器。
- 当前 Choice 的展示发生变化时重发必要状态，不伪造 Flow 继续。[S07]
- 可用性版本独立于作者页正文、声音和画面执行身份；不得为了刷新按钮增加 line epoch、重新播放声音、重启转场或重复记录历史。
- 同一 Choice 的动态文字继续使用当时冻结值。不能某按钮一解锁，其他选项中的玩家等级数字也突然重算。
- 被拒绝后客户端解除 awaiting，并以稳定 option_id 恢复可用焦点；不能按旧索引点击到另一项。

### 7.6 全部不可用和边界

施工默认行为：全部隐藏／禁用时保持当前 Choice 等待，不自动结束、不选第一个、不制造“离开”出口。可以显示与剧情正文分离的弱提示“当前没有可选项”。

Studio 对“全部选项都依赖条件且没有无条件出口”给出设计风险提醒，但不硬性禁止，因为作者可能有意等待既有外部 Logic。

必须同时遵守：

- 最终渲染列表允许 0 个可选项，但 Canonical 定义本身仍有作者配置的选项。
- 不为了接收空展示列表放松整个图结构校验。
- 键盘导航跳过隐藏／禁用项；鼠标点击禁用项不发送有效选择。
- 自动播放到 Choice 仍停止，只有一个可选项也不能自动选。
- 被拒绝或不可用的点击不进入已选历史；真正接受的选项只记录一次。
- 不依赖旧 Logic 输出来绕过条件；新输入引入的回路按现有 Logic 合法性规则拒绝，不私自松开环路约束。


---

## 8. 工作包 E：画面转场与 PPT 式平滑

### 8.1 产品目标

作者应能够完成：

```text
制作【画面 A】
→ 复制得到【画面 B】
→ 在 B 的预览中移动／缩放某张图片，或添加／移除图片
→ B 的进入转场选择「平滑」并设置时长
→ 游戏从实际当前画面自然变化到 B
```

不要求作者创建每一帧、不需要时间轴、不输入图层身份。PPT 仅为交互和视觉目标的参考；本版不依赖 Office，不导入 PPTX，也不假设可以直接复用 PowerPoint 内部代码。

### 8.2 Screen 仍代表完整目标画面

转场放在**后一个 Screen**，命名为“进入转场”。进入 B 的含义是：从当前实际显示状态过渡到 B 的完整状态。

- B 的 `layers` 为空：最终完全清空 DGR 画面层。
- B 不包含 A 的某层：该层可以在过渡中淡出，但结束后必须不存在。
- B 包含新增图层：按转场出现。
- 不把 B 改为“在 A 基础上执行增删命令”。
- 只作用于 Screen 图像层；不要把 Minecraft 世界、对话框、选项、头像或其他 Mod 的 HUD 一起滑走／擦掉。
- 音乐继续自己的生命周期；画面转场不能重新启动音乐。

### 8.3 第一版必须提供的转场

| 显示名称 | 行为 | 额外参数 |
|---|---|---|
| 无 | 直接切换到目标画面 | 无 |
| 淡入淡出 | 旧画面淡出、新画面淡入 | 时长 |
| 滑入滑出 | 旧画面沿指定运动方向移出，新画面从对应另一侧移入 | 方向、时长 |
| 擦除 | 沿指定方向逐步揭示目标画面 | 方向、时长 |
| 随机线条 | 将区域分成有限横条／竖条，固定次序逐步替换为目标 | 横向／纵向、时长 |
| 平滑 | 匹配对应图层，平滑改变位置／大小；新增／消失图层淡入淡出 | 时长 |

滑入滑出采用一项配方向，不拆一长串效果按钮。若代码内部区分 slide-in/out，也必须维持上表完整的双向替换效果，而不是新图滑进来后旧图永久留底。

施工默认值：新建／旧内容未指定时为“无”；选择非“无”后初始时长 0.5 秒，合法范围 0—60 秒；0 秒直接切换。默认缓动为固定的平滑加减速，不提供一大组数学曲线菜单。

随机线条的顺序在一次转场开始时确定，同一转场绘制中不每帧重新随机；断线重放也不能通过随机效果改变游戏状态。

### 8.4 平滑的匹配依据：内部“图层对应标记”

不能只比较媒体文件是否相同。例如同一张 `star.png` 放了十次，十个实例必须各自对应，而不是随机串位。

建议每层增加不可见的 `morph_key`：

- 它表示这个画面对象与复制画面中的对应对象。
- 不是玩家可见资源 ID，不可在资源库创建、指名或重命名。
- 不替代 `media_ref`；内容指纹仍然负责媒体文件。
- 在**同一个 Screen 内必须唯一**；跨复制出来的 Screen 相同是有意的对应关系。
- 匹配限制在相应会话／演出上下文，不能把不同玩家或不同会话中碰巧相同的标记串起来。

#### 复制与编辑规则

| 操作 | 对应标记行为 |
|---|---|
| 从 A 复制一个 Screen 为 B | 保留原层标记，A/B 可对应 |
| 修改图层位置、尺寸、锚点、层级 | 保留标记 |
| 替换该图层使用的图片资源 | 保留对象标记；不同图片按下一节交叉淡化 |
| 在同一 Screen 内复制某张图层，得到另一个对象 | 新图层生成新标记 |
| 手动重新导入相同媒体为一个新图层 | 新标记；不能因文件相同自动合并对象 |
| 复制一整组含多个 Screen 的图／会话 | 若重映射，必须统一映射整组的对应关系，不能每个 Screen 各自随机重建 |
| Undo／Redo | 恢复同一对应关系，不能每次重做生成新值 |
| 旧内容没有对应标记 | 读取时不改原文件；进入新编辑模型时可生成并在正式保存时持久化。不得假装知道历史复制关系 |

保留新增层的唯一性校验，拒绝同一 Screen 中重复标记。不能用数组序号、Z 层级或坐标当永久身份，因为这些都会被作者调整。

### 8.5 平滑规则

| 实际源画面与目标画面的关系 | 呈现 |
|---|---|
| 同一对应标记、同一图片 | 位置／尺寸／锚点换算后的显示矩形连续变化 |
| 同一对应标记、更换了图片 | 在移动／缩放过程中旧纹理淡出、新纹理淡入；不做像素形变 |
| 只有源画面存在 | 从当前位置淡出 |
| 只有目标画面存在 | 在目标位置淡入 |
| 没有任何可匹配对象 | 合理退化为淡入淡出，不猜相同文件实例之间的对应 |

以当前渲染坐标体系计算显示矩形，再插值，保证锚点改变时不突然跳位。当前支持的比例和拉伸规则保持；不新增旋转编辑、骨骼变形或透明度曲线编辑器。

相同对象在层级顺序变化时使用明确、稳定的处理，不能对整数 Z 逐帧取整造成闪烁。施工默认：目标公共层按目标 Z 顺序绘制，退出层保持确定的源顺序；为交叉重叠案例录制检查。该规则属于表现层默认，不修改节点存储顺序。

### 8.6 实现边界：真实平滑，不是整张画面的伪缩放

合格结果必须能证明：A 中两个不同对象，在 B 中分别移动到不同位置／尺寸，且能同时有一个对象消失、另一个出现。

以下不能视作完成 Morph：

- 只对整个屏幕做缩放。
- 所有同文件图层作为一个对象一起移动。
- 只做交叉淡化却不改变对象位置。
- 只能编辑同一个节点的静态预览，正式导出后没有对应标记。
- 每切一次画面都重新复制一份媒体文件以模拟对象身份。

---

## 9. 转场调度、网络与资源生命周期

本节为保持既有非阻塞语义而明确的施工默认规则。没有另行授权“转场播放结束才继续流程”，不得将其擅自实现为新的 Story／Session 等待点。

### 9.1 不阻塞流程，不建立长动画队列

转场与后续台词／选择可以同时呈现。服务器仍在既有自动推进链路中应用目标 Screen，然后推进到后面的实际驻留节点。

同一次自动推进中，如果直接经过 `Screen A → Screen B → Screen C → 台词`，没有任何中间驻留，本版沿用最终目标 C 的含义，不额外排队播放三张幻灯片。要看见 A 再看 B，作者用现有台词／选择等驻留点分开呈现。

此行为应写入【画面】介绍说明，避免“有时长所以流程会自动等待”的误解。它是保持现有语义的实施边界，不是漏做中间动画后隐藏问题。

### 9.2 动画来源是实际已显示画面

运行时的来源是当前客户端演出层，不是 Studio 节点列表中的上一项，也不是按复制关系去强制启动另一个 Screen。

例如分支 `A → B` 或 `C → B`，B 都从真正走到这里之前的画面开始；对应标记只能帮助匹配，不能让未执行的 C 内容突然出现在 A 分支。

### 9.3 独立且稳定的画面执行身份

现有 Presentation 的总 revision／music revision 不能直接当作每一帧都要重新播放 Screen 动画的理由。

应为画面变化提供最小的独立执行身份／修订，并区分：

- 新执行的 Screen：按目标设置开始一次转场。
- 同一目标的重发、台词本地分屏、菜单可用性刷新：不重新开始。
- 音乐修改而图层不变：不触发画面转场。
- 断线重连／存档恢复：直接恢复当前目标的最终画面，不从旧世界重放整段过渡。
- 首次真正进入会话且执行有转场的 Screen：允许从空 DGR 图像层进入。

必要时最小扩展现有 Presentation／帧的演出字段，不另建通用事件总线、第二份剧情历史或动画 Runtime。

### 9.4 连续切换和中断

如果 A→B 还未结束，玩家已经进入 C：

- 从当前已计算的可见状态开始过渡到 C，不先跳回 A 或强行等完 B。
- 只保留当前有界源状态与最新目标，不保留所有过去的动画对象和纹理。
- 去掉不再使用的过渡临时对象；不能因连续点击积累第三、第四个完整场景缓存。
- 极端连续中断导致合成工作集超过预算时，受控收敛到最新目标并清理，不放开内存限制；诊断中记录降级原因，不影响剧情推进。
- 会话结束、Story 卸载、断线时终止转场、清空 DGR 图像层，并释放该演出持有的资源。

### 9.5 冷媒体与失败

转场不能绕过既有包准入，也不能把缺图当 Ready。

- 新目标媒体已就绪：开始转场。
- 未就绪：先按既有媒体需求请求，不无限卡住台词。
- 为避免“新图全空而旧图已经被擦掉”，允许表现层做一次有限准备等待；施工默认上限 3 秒，期间不阻塞 Flow。
- 等待期间有更新目标则放弃旧目标的等待。
- 超时／失败后按已经就绪的目标内容及明确占位收敛；不能永久显示过期旧画面，也不能用不相关图片顶替。
- 后到达的图片显示在当前目标的最终位置，不重新启动已经结束的整场转场。

有限等待时长是工程默认值，应在实际加载场景检查；不能用它承诺冷缓存零延迟，不能因此把“附近 NPC 预加载”加回来。

### 9.6 同时使用旧／新资源时的保护

现有 `CanonicalMediaClient.ready()` 与可见需求、纹理回收相关，动画期间必须让**确实参与本次演出的源／目标资源**拥有使用引用，不能只声明目标便把源纹理回收。[S13]

- 使用既有 pin／lease、纹理需求和有界解码机制扩展，不制造影子媒体池。
- 保留源的 GPU 纹理只为本次过渡，不因动画保留全部旧 Story 包。
- 目标仍按 Story 3 包预加载／10 包名额准入；动画不能绕过准入另下载任意资源。
- 只让当前演出需要的资源解码；不能为了预览所有转场一次上传全项目图片。
- 源＋目标的活动工作集、解码中间结果、离屏合成纹理必须分别纳入预算。原有“32 层上限”不意味着 32+32 张最大图都可以无限量常驻。
- 不以每帧 IO、同步解码、每帧重建大纹理实现效果。GL 资源创建与释放保持正确线程。
- 裁切、混合、深度、纹理过滤和绑定状态在绘制结束后恢复，不能污染对话框、原版 HUD 或其他 Mod。
- 若采用离屏合成，目标数量和分辨率有界；在实际支持环境验证，不为了某个特效接管整个 Minecraft 渲染器。

动画结束释放过渡使用权，磁盘缓存仍由原有所有者和 LRU 决定是否保留。不能把“动画结束”解释成“删除 .dgrs 或删除共享媒体”。

### 9.7 Studio 预览

在现有【画面】预览区域接入一次转场预览，不另造完整游戏模拟器。

- 进入方式和时长在节点与 Inspector 都可编辑，同步保存／撤销。
- 可以从明确的前置 Screen 作为预览来源；多分支时让作者选择这次预览的来源，并明确它只影响预览，不是 Runtime 的强制来源。
- 没有来源时从空画面预览。不能任意拿最近编辑过的另一个 Screen 作为真来源。
- 保留可隐藏／显示的对话框、选项框参考；参考不能被当作图像图层拖动。
- 编辑静态图层与播放预览分离，预览不把每一帧坐标写入正式节点，不制造几百条 Undo。
- 预览停止／切换节点／关闭项目后释放临时图像和动画对象。
- 各效果要在 Studio 和游戏使用一致的方向定义、插值和最终状态；不能一个向左、另一个向右。

---

## 10. 数据演进与全链路接线

### 10.1 数据归属

| 数据 | 权威位置 | 生命周期 |
|---|---|---|
| 资源使用位置索引 | Studio 可重建缓存，来源是项目真实定义 | 项目上下文 |
| Choice 条件端口、False 行为、提示 | 作者资源／DGRS | 随资源保存 |
| Choice 当前可用性 | 服务端既有 Logic 的展示投影 | 当前玩家、会话、选择轮次 |
| 冻结的动态正文 | 既有 Session 展示快照 | 既有作者页规则 |
| 物品组成员及匹配规则 | 既有服务端绑定数据 | 世界／既有绑定生命周期 |
| 物品候选页面 | 可重建的展示数据 | 有界客户端／服务端缓存 |
| Screen 最终图层与转场配置 | 作者资源／DGRS | 随资源保存 |
| 图层对应标记 | 图层的内部演出元数据 | 保留复制对应关系，不是资源身份 |
| 正在播放的动画、t 值、旧纹理使用权 | 客户端临时演出状态 | 一次转场；结束／断线释放 |
| Story 进度、任务收据、玩家选择 | 既有 Canonical 运行／持久化系统 | 不由新展示缓存管理 |

### 10.2 配套升级原则

- C# 和 Java 新字段、枚举、默认值、字段数量校验必须一起修改。
- 旧 0.3.3.4 内容未提供 Choice 条件时默认不设限制；未提供 Screen 转场时默认“无”。不要在读盘时自动改写用户原文件。
- 旧资源需要新增内部标记时只在可写编辑模型／显式保存路径产生，不改用户可见 Resource ID。
- 新功能要求配套 Studio／客户端／服务端版本。确实需要报文或包能力版本时最小显式升级并给出不匹配说明；不建立两套运行时来掩盖版本不匹配。
- 不能为了方便解析允许任意未知字段；也不能忘记旧字段严格数量检查导致新包被自己拒绝。
- 不保证旧版本程序能读取 0.3.3.5 新增能力。保留原文件并提供明确错误，比兼容路径叠加更重要。

### 10.3 复制、导入、保存、重命名

重点检查三个不同身份：

1. 资源稳定 ID：现有身份规则，不因动画、候选查询改变。
2. 选项及端口稳定 ID：跟选项走；复制时按已有语义重映射。
3. 图层对应标记：用于跨复制 Screen 的对象匹配，不按普通端口 ID 一律重建。

对每一个相关字段执行一遍“创建 → 保存 → 关闭 → 重开 → 复制 → 撤销／重做 → 导出 → 导入”。不能让 UI 正常而正式包丢失配置。

### 10.4 说明同步

更新既有默认折叠的「介绍说明」：

- Choice：未接线默认可选；False 隐藏／禁用；不检测玩家状态；没有可选项时等待。
- Screen：完整目标状态、进入转场、非阻塞、平滑复制关系、同一自动推进批次最终目标、媒体未就绪边界。
- 相关 Logic 输入／输出说明：不同图层级通过公共端口传递；Objective 完成不等于实时持有。
- 物品组 Tooltip：当前图标为候选轮播，悬停可分页查看，模糊匹配图标为示例。
- 使用位置：查询范围仅为当前可读取项目，不声称全局引用搜索。

不要把实现细节、UTF-8 公式、内部对应标记直接铺进普通玩家 UI。


---

## 11. 源码施工落点

以下是基线中已经存在的入口；不是要求将所有改动挤进这些文件。可抽取小型共享组件，但不得创建平行执行器。表中目录通配表示需要继续定位的区域，不冒充已经存在某个新类。

| 工作包 | 既有源码入口 | 必须贯通的关系 |
|---|---|---|
| A 文本风险 | `Studio.Core/Graphs/Definitions/DynamicContentText.cs`、`SessionChoiceSchema.cs`；`Views/Graph/DynamicContentEditor.cs`、`ReorderEntriesEditor.*` | 输入／粘贴／名称改动 → 风险提示 → 导出检查 |
| A 服务端保护 | `session/runtime/DynamicContentText.java`、`session/forge/DynamicContentResolver.java`、`session/server/CanonicalSessionServerService.java`、`session/persistence/CanonicalSessionSavedData.java` | 真实展开 → 槽位验证 → 合法快照 → 网络投影 |
| A 容量 | `scripts/generate-dialogue-capacity-profile.py`、`schema/dialogue-capacity-profile.json`、`DialogueCapacityProfile.cs`、`client/session/DialogueFontScale.java`、`CanonicalDialogueLayout.java` | 新几何／有效倍率 → profile → Studio 实时容量 |
| A 头像 | `client/gui/CanonicalDialogueRenderer.java`、`client/session/CanonicalSessionClientModel.java`、`media/CanonicalMediaTextures.java` | 有效台词上下文 → 当前引用纹理，不能旧图顶替 |
| B 候选生产 | `creator/TaskItemPreview.java`、`TaskRewardPreview.java`、`CanonicalTaskUiProjection.java`、`CanonicalTaskPresentationServer.java` | 常规摘要 → 经授权的分页候选，不执行任务 |
| B 传输与历史 | `creator/CreatorSnapshot.java`、`network/message/canonical/CanonicalTaskSubmitChoiceFrame.java`、`task/persistence/CanonicalTaskCompletionHistory.java`、`creator/CanonicalTaskHistoryProjection.java` | 总预算／分页／修订；历史不丢失、不填造 |
| B GUI | `client/gui/ItemSlotStrip.java`、`GuiItemCandidates.java`、`GuiCanonicalTaskScreen.java`、`GuiCanonicalTaskSubmitChooser.java`、`client/TaskTrackerHud.java` | 单格轮播 → 悬停菜单 → 原生 Tooltip；HUD 无鼠标交互 |
| C 使用位置 | `ViewModels/Graph/CanonicalStoryWorkspaceViewModel.Search.cs`、`Views/Graph/CanonicalStoryWorkspaceView.Search.cs`、`CanonicalStoryWorkspaceView.xaml`；Core `Identity/ResourceRenameMap.cs`、`Graphs/Resources/CanonicalStoryWorkspaceLoader.cs` | typed 引用扫描 → 可重建索引 → Inspector → 精确导航 |
| D 作者端 | `SessionChoiceSchema.cs`、`GraphNodeAuthoringService.cs`、`GraphNodeDefinitionRegistry.cs`、`GraphNodeShapeValidator.cs`、`AggregatePortProjection.cs`、Inspector／选项编辑模型、`CanonicalGraphClipboard.cs` | 条件端口／属性／标签／连线／历史一次同步 |
| D 服务端 | `session/runtime/CanonicalSessionRuntime.java`、`CanonicalSessionStep.java`、运行时 ChoiceOption、`session/server/CanonicalSessionServerService.java`、既有 Forge Logic 传播入口 | 既有 Logic → 可用性 → 帧更新／选择复核 |
| D 客户端 | 网络 ChoiceOption／SessionFrame／Action、`CanonicalSessionClientModel.java`、`GuiCanonicalSessionScreen.java`、`GuiWrappedChoiceButton.java` | 隐藏／禁用／旧点击处理，不触发重播 |
| E 作者端 | `CanonicalSessionPresentationSchema.cs`、Inspector 与画面图层编辑相关文件、`CanonicalGraphClipboard.cs`、`Packaging/StoryPackageMedia.cs`、`StoryPackageExporter.cs` | 内部对应标记 → 复制保留 → 参数与预览 → 正式导出 |
| E 游戏端 | `session/runtime/CanonicalSessionPresentation.java`、`CanonicalSessionRuntime.java`、`media/CanonicalSessionScene.java`、`CanonicalMediaClient.java`、`CanonicalMediaTextures.java`、Session 投影及持久化 | 目标状态／唯一画面身份 → 转场 → 最终状态／资源释放 |

`Studio.Core/...` 相对 `studio/src/DarkGreyRPG.Studio.Core/`；WPF 相对 `studio/src/DarkGreyRPG.Studio/`；Java 相对 `src/main/java/darkgrey/rpg/`。新增分页服务、引用索引或转场控制器应有独立职责和测试，不给现有巨大视图文件继续堆所有逻辑。

### 11.1 需要记录的最小实现契约

在施工记录中固定并验证：

- Choice 新字段和条件端口映射、展示可用性修订方式。
- 动态字段预算表与实际校验位置。
- 候选分页的上下文键、版本、游标、请求上限及内存预算。
- 引用索引如何提取直接／间接引用、如何定位只读内容。
- Screen 转场字段、图层对应标记、画面执行身份与中断策略。
- 源＋目标图层的活动资源预算和释放路径。

这一步只解决代码接线，不重新征集已经决定的产品方向；只有确实无法兼容现有权威语义的冲突才需要上报，不把每个像素和内部类名都退回给用户确认。

---

## 12. 施工顺序

### 阶段 0：基线核对与最小契约

确认 HEAD、工作树、本地最新用户改动、版本及正式产物路径。按仓库规则完成必要的分工能力检查；调度器不可用时在日志中如实记录，不伪称已经委派。

先完成第 11.1 节的接线说明，尤其解决“新 Choice 原来没有旧 Logic 输出”“自动推进并非每个 Screen 都驻留”“仅将图标画成一格不能解决大包”三个常见误判。

### 阶段 1：A 基础修复 + C 使用位置

A 先处理不改语义的头像修复、风险计算和容量标定；C 基于现有 typed 引用和定位实现。

C 和 A 可以作为边界清楚的工作并行，但动态字段／资源引用访问器由同一主线协调，避免复制两套解析器。

完成条件：作者能在 Inspector 查到真实使用位置；Choice 风险在编辑时可见；容量依据已更新；冷头像不再串角色。

### 阶段 2：D 条件选项

从一个公共 Session Logic 输入 → 某个选项条件端口开始，验证未连接、True、False、隐藏、禁用和逻辑变化更新。再覆盖多个选项、AND／OR／NOT、跨图公共接口和不合法点击。

先完成最小可运行的服务端／客户端链路，再做选项 UI 排版。不能只改 Studio 端口和截图。

### 阶段 3：B 物品组与大内容同步

先接通有界分页与权限，再改成单格轮播／悬停菜单；不要先把全量候选照旧收进客户端，再把分页留作“以后优化”。

完成候选缓存、整包预算及历史按需读取配套。仅在版本变化时构建必要的候选索引，可分批并可取消；保持轻量成员描述，不每 tick 构造全部 ItemStack／全部 NBT。

### 阶段 4：E 普通转场与平滑

顺序：

```text
完整目标状态／独立画面身份／生命周期
→ 淡入淡出
→ 滑入滑出／擦除／随机线条
→ 图层对应标记、复制关系、Morph
→ 多图／分支／中断／冷媒体
→ 原预览区与正式游戏一致性
```

平滑是本版重点，不是可以悄悄延期的可选项。普通效果完成不能替代 Morph 交付。

### 阶段 5：集成、说明、验收与正式交付

整合动态条件选项＋多媒体转场＋物品组任务的一条真实故事。修复交叉问题后重新构建权威产物，记录版本、文件大小和 SHA-256。

不得把各个工作包曾经通过的结果简单拼接，冒充最终合并版本已全部跑过。

---

## 13. 测试与验收矩阵

测试是交付保障，不是本版主要新功能。以下用例的结果必须写明“自动化”“独立几何验证”“真实 Studio”“真实 Minecraft”“未覆盖”，不能混写。

### 13.1 A：文本、容量、头像

| 用例 | 预期 |
|---|---|
| A-T01 选项 2048 字节等于上限／超过 1 字节 | 按真实字节判定，不按字符串长度误判 |
| A-T02 40 个名称引用、每个 20 汉字 | Studio 明确预警；Runtime 不冻结非法 2400 字节选项，不改写作者文字 |
| A-T03 相同块数量，分别为等级和长名称 | 得到不同预算，不能全部套固定数量 |
| A-T04 改名／Undo／Redo／粘贴后长度变化 | 所有受影响选项的风险更新，不丢原子块 |
| A-T05 中文、ASCII、补充平面字符、格式标记 | 与实际 UTF-8／渲染处理一致；不能截断代理对或格式序列 |
| A-T06 超限后重发、断线重连、修正再恢复 | 没有坏快照永久复用，无重复奖励／选项记录 |
| A-T07 所有支持 GUI 因子与最大字号、有／无头像 | Studio 标尺与实际 Runtime 排版一致；安全区内代表文本不意外超屏 |
| A-T08 故意超过显示容量 | 仍可按已定规则导出并本地分屏，历史完整、语音一次 |
| A-T09 A 头像就绪→B 冷头像 | B 名字不会搭 A 图；B 就绪后正常显示 |
| A-T10 同角色差分／旁白／无图／进入 Choice | 差分不借错图，旁白无残留，Choice 正常保留前句上下文 |

### 13.2 B：任务物品组

| 用例 | 预期 |
|---|---|
| B-T01 单体、0／1／3／20／21／40／100 个组成员 | 外层始终一格；菜单按实际内容收缩或翻页 |
| B-T02 大组完整轮播 | 后续页也参与；第一帧不下载全组 |
| B-T03 悬停图标→移入菜单→翻页→移出 | 暂停外层轮播，菜单可点，不意外关闭或穿透 |
| B-T04 屏幕四角、小窗口、三主题 | 面板和原生提示不出屏、不挡死翻页 |
| B-T05 同名不同物品、精准／模糊、附加条件 | 候选与服务端真实匹配一致，模糊示例有说明 |
| B-T06 大 NBT 候选和单项不可展示 | 字节预算生效；保留候选位置与原因，不修改实际物品 |
| B-T07 菜单打开时重新指名／修改组 | 旧页失效，版本一致；不混入新旧列表 |
| B-T08 快速翻页、切换目标、关闭重开、断线 | 旧响应不覆盖新上下文；缓存和队列回到有界状态 |
| B-T09 伪造未激活目标或其他玩家页请求 | 服务器拒绝，无状态、库存或奖励副作用 |
| B-T10 大量完成历史／大量 ACTIVE 任务 | 同步有界且最终完整，不靠删历史、省略目标过关 |
| B-T11 故意制造编码／发送提交失败 | 不把失败内容提前标成已同步，能受控重试 |
| B-T12 查看后真实提交／发奖 | 仍走原 NPC 交互和数量事务；查看行为本身零副作用 |

### 13.3 C：使用位置

| 用例 | 预期 |
|---|---|
| C-T01 初次选择每类支持资源 | 右侧出现默认折叠项目，不添加右键入口 |
| C-T02 普通文字提及名称 vs 真正 typed 引用 | 前者不计为引用，后者准确定位 |
| C-T03 同名不同 ID／同字符串不同 ResourceKind | 结果不串联 |
| C-T04 未打开 Story／共享 Session／只读引用包 | 结果完整且不重复，点击能查看不改权限 |
| C-T05 Page 拖序、Option 改名、替换媒体后点击 | 按稳定身份定位到正确字段，不跳到旧序号 |
| C-T06 复制、删除、撤销、导入及改名 | 索引更新，读盘不覆盖内存草稿 |
| C-T07 大结果集、快速切换资源／项目 | 列表有界，旧任务不回填，无持续内存增长 |
| C-T08 仅查询／展开／跳转后保存前后比较 | 没有无关数据写入或隐式 ID 变化 |

### 13.4 D：条件选项

| 用例 | 预期 |
|---|---|
| D-T01 新建选项不接线 | 默认可选，不因通用 Logic 默认 false 全部隐藏 |
| D-T02 True／False 两种模式及提示 | 与真值表完全一致，原选项正文未替换 |
| D-T03 AND／OR／NOT 与公共输入 | 复用现有 Logic，复杂条件不出现表达式框 |
| D-T04 Task 输出→Story 公共传递→Session 条件输入 | 实际跨图链路可用；不跨层直连内部 Objective ID |
| D-T05 已完成收集目标后丢掉物品 | 仍按既有完成 Logic，不偷偷变成库存检测 |
| D-T06 停在 Choice 时已有 Logic 改变 | 菜单刷新；台词、头像、声音、历史、画面不重启 |
| D-T07 旧可选响应后条件变 False，再点击 | 服务器拒绝并刷新，不错走分支 |
| D-T08 隐藏／禁用 option_id 的伪造请求 | 没有选择记录和 Flow 推进 |
| D-T09 全部不可用，之后某输入变 True | 保持等待后恢复可选，不自动选择／结束 |
| D-T10 键盘、分页、自动播放 | 跳过不可用项，稳定 ID，自动不代选 |
| D-T11 增删排序、剪线、节点复制、Undo／Redo | 三类身份关系正确；不复活新建旧 Logic 输出 |
| D-T12 恢复已有 0.3.3.4 Choice 和带旧兼容端口内容 | 旧内容照常可选；合法旧线不被破坏，新建不多出旧输出 |

### 13.5 E：画面演出

| 用例 | 预期 |
|---|---|
| E-T01 无、0 秒及各正常时长 | 无除零、无额外等待，末态严格等于目标 |
| E-T02 淡入淡出／四方向滑动／四方向擦除 | 方向与 Studio 预览一致，不移动对话和世界 |
| E-T03 横向／纵向随机线条 | 单次次序稳定，无逐帧随机闪烁 |
| E-T04 A 复制到 B，两图分别移动和缩放 | 各对象独立平滑，不是整幅缩放 |
| E-T05 同一个 PNG 放十次并分别移动 | 无串配；同屏标记唯一，跨复制关系保留 |
| E-T06 A 中删除一层、B 中增加一层 | 旧层淡出、新层淡入，最终不残留旧层 |
| E-T07 同一对象换图片／改锚点／改 Z | 规则明确，无误用图像或锚点跳位 |
| E-T08 多 Screen 整组复制、保存重开、导出导入 | 对应关系仍有效，参数不被旧 setter 丢掉 |
| E-T09 分支 A→B 与 C→B | 来源是实际显示状态，不取编辑顺序 |
| E-T10 同批自动经过多个 Screen | 沿非阻塞最终目标语义；说明与行为一致，不暗建队列 |
| E-T11 动画中快速进入 C、缩放窗口、重复帧 | 从有界当前状态接续，重复帧不重播，尺寸变化无泄漏 |
| E-T12 新图延迟／失败、共享图、包名额满 | 不突破准入，不无限阻塞，不旧图冒充目标 |
| E-T13 会话结束／断线／重启／资源重载 | 清理转场使用权，重连恢复最终目标，不重放历史动画 |
| E-T14 最大图层／长时间反复播放／多玩家 | 解码、纹理、缓存、请求有界；3/10 包规则不变 |
| E-T15 半透明 PNG、重叠对象、原版与其他界面 | 首末态准确，GL 状态恢复，无对其他界面的污染 |

### 13.6 集成场景

制作一个正式 Studio 工程，包含两条可分支会话、一个带公共 Logic 输出的任务、一个多候选物品组，以及三个有复制关系的 Screen。

实际链路：

```text
Studio 编辑与查使用位置
→ 给选项连已有公共 Logic
→ 设置隐藏／禁用
→ 复制画面并设置五种转场及平滑
→ 正式导出 .dgrs
→ 记录导出包 SHA-256
→ 同一文件部署到隔离 Minecraft 环境
→ 实际触发、对话、选项、转场、查看组候选、提交、查看历史
```

必须覆盖：两玩家的 Choice 可用性不同且互不串线；候选查看不会提交；条件刷新不会重播转场；本地长台词翻屏不会重复音频；关闭／重连后无旧图层、旧菜单或旧请求复活。

不能“Studio 导出 A 包，Minecraft 实际测了脚本另造的 B 包”。自动探针可补充，但不替代这条产品链路。

---

## 14. 视觉与性能验收

### 14.1 Studio

检查节点内与 Inspector 的单项／多项／折叠／错误状态。必须有真实图片或截图检查，不仅断言控件存在。

- 使用位置默认折叠，不挤掉基本属性。
- 条件输入与相应选项明确对应，Logic／Flow 图形不混淆。
- False 行为和提示设置紧凑，文本容量／动态预警不遮挡正文。
- Screen 转场参数不压缩原图片预览；方向设置只在必要效果出现。
- 沿用项目现有 220ms 高度伸缩，不把文字整体缩放做折叠。
- 快速展开反向、移动带连线节点、中文输入法、拖放资源等现有交互不退化。

### 14.2 Minecraft

- 单体和组图标占用相同格位，数量可读。
- 悬浮菜单及原生 Tooltip 在灰黑／浅白／蔚蓝可读，少量候选不留大块空白。
- 禁用选项仍能读清原文和原因，不与正常项一样可点击。
- 图像转场平滑，无无故黑屏／旧头像串位；首末帧的布局与静态目标一致。
- 动画不能改变玩家主音量设置、对话输入权限或其他 Mod 的渲染状态。

### 14.3 有界性而非虚构帧率承诺

至少记录重复打开关闭、轮播大组、反复转场后的：候选页缓存、在途请求、解码队列、GPU 纹理和演出对象数量。

比较同机同场景的实际帧耗时与基线。不要把 UI 线程回调频率当作 GPU FPS，也不要用一次 localhost 下载测试声称公网性能。

有界性属于本版实现条件；具体不同机器的性能数字必须实测。没有实测的硬件／组合如实标注，不编造“完全不卡”。

---

## 15. 正式交付要求

### 15.1 必须交付的内容

- Studio、客户端和服务端配套的 0.3.3.5 源码与构建。
- 新增字段／网络能力说明和旧内容最小默认处理说明。
- 更新后的容量 profile、生成方式和跨端标定结果。
- 使用位置、Choice 条件、物品组候选、转场／Morph 的正式 UI。
- 需求追踪表和逐项验收结果。
- 同一个 .dgrs 的 Studio→游戏证据及哈希。
- 资源生命周期检查、残留未验证项和已知限制。

### 15.2 权威产物路径

按 `AGENTS.md` 的既定交付规则：[S01]

```text
E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe
E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.5.jar
```

Studio 为现有自包含 Windows x64 Release 交付方式。仅放在 `.tooling`、`bin/Release` 或版本临时目录，不算交付到用户使用路径。

每份产物报告版本、真实大小、SHA-256，并核实最终实机使用的文件与交付文件一致。本文不会预填不存在的产物哈希。

先用仓库实际登记的构建／测试任务；新增专项可登记，不能把建议任务名当作已存在命令。WPF 原生验收按项目要求使用 Windows UI Automation／Win32，不使用用户禁止的 Computer Use/cua/sky。

### 15.3 版本状态与权限

- `IMPLEMENTED`、`AGENT_VERIFIED`、`NEED_USER_VERIFICATION` 分开报告。
- 用户是最终验收人；代理不能独立写 `USER_ACCEPTED=YES`。
- 本次用户请求是生成施工 PLAN，不是授权本助手修改或推送仓库。实际施工时 Git 提交、推送、PR、Release 依用户授权办理。
- 保留正式项目、`.dgrs` 和存档。所有破坏性边界测试使用隔离副本，不以“回归测试”为由改写用户世界。

### 15.4 不可接受的“完成”替代品

- 用更多测试文档替代未完成的新功能。
- 只画一格轮播，后台仍每次全量接收物品组。
- 使用位置只是跳到全文搜索，没有 typed 引用判定。
- Choice 在客户端置灰，服务端仍接受该 option。
- 为选项条件重新加入玩家状态检测器或 Flow 触发器。
- Morph 没有图层对应关系，或只对整张画面缩放。
- 动画看似正常，但复制／导出丢配置，或关闭后资源不断增长。
- 为保留旧容量数字回退用户已经认可的 0.3.3.4 字体布局。
- 将超限动态文本替换成“动态内容过长”作为剧情正文。

---

## 16. 给 Codex 的执行摘要

从固定的 0.3.3.4 基线开始，保留用户后续改好的 UI、字体、连续台词编辑、动态名称、冷却重复、任务回顾和媒体生命周期。

本版先补齐四个已确认缺口，再交付三项新能力：

1. 动态文本风险在 Studio 提前出现，服务端最终验证不改作者文案；容量按实际有效字号重新标定，保留一次 10% 余量；冷头像不借用旧图。
2. 单体固定一格、物品组轮播一格；悬停为可交互的最多 20 项分页菜单。数据也必须按页取得、有界缓存；任务历史／基础快照同步预算一起补齐，不丢真实进度。
3. 所有支持资源在右侧 Inspector 默认折叠显示使用位置，按 typed 引用精确定位；不放右键菜单，不造另一套项目权威索引。
4. 每选项新增稳定 Logic 条件输入。未连接可选，True 可选，False 隐藏或置灰。只接既有 Logic，服务器重检；不增加状态判断或触发器，也不把旧兼容 Logic 输出重做回来。
5. Screen 支持五类转场，重点是复制画面后的平滑。内部图层对应标记由 Studio 管理；目标依然是完整画面，转场非阻塞，生命周期有界。

按工作包施工，不逐个像素反复打断用户。实现后在正式产物上验证真实交互，并清楚列出未覆盖部分，不擅自缩减本版范围。


---

## 17. 固定提交源码与来源索引

以下链接全部固定到本 PLAN 的基线 SHA，不随着默认分支变化。源码用于说明现状；本文新增的端口、分页和转场规则是施工目标，不能反过来描述为基线已经实现。

### S01 · 项目交付与目录约束

- [`AGENTS.md`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/AGENTS.md)

### S02 · Studio UI、折叠、双入口与原生验收规则

- [`.agents/skills/studio-node-ui/SKILL.md`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/.agents/skills/studio-node-ui/SKILL.md)

### S03 · Story 媒体 3 包／10 包、部分资源使用与生命周期

- [`.agents/skills/story-media-lifecycle/SKILL.md`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/.agents/skills/story-media-lifecycle/SKILL.md)

### S04 · Choice 作者端字段、Flow 输出及旧 Logic 兼容端口

- [`studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/SessionChoiceSchema.cs`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/SessionChoiceSchema.cs)

### S05 · C# Screen 严格 schema 与 32 图层上限

- [`studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/CanonicalSessionPresentationSchema.cs`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/CanonicalSessionPresentationSchema.cs)

### S06 · Java 自动推进与完整画面状态

- [`src/main/java/darkgrey/rpg/session/runtime/CanonicalSessionRuntime.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/session/runtime/CanonicalSessionRuntime.java)
- [`src/main/java/darkgrey/rpg/session/runtime/CanonicalSessionPresentation.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/session/runtime/CanonicalSessionPresentation.java)

### S07 · Logic 输入刷新与流程推进区分

- [`src/main/java/darkgrey/rpg/session/runtime/CanonicalSessionRuntime.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/session/runtime/CanonicalSessionRuntime.java)
- [`src/main/java/darkgrey/rpg/session/server/CanonicalSessionServerService.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/session/server/CanonicalSessionServerService.java)

### S08 · 现有跨资源搜索、稳定定位字段与只读导航边界

- [`studio/src/DarkGreyRPG.Studio/ViewModels/Graph/CanonicalStoryWorkspaceViewModel.Search.cs`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/studio/src/DarkGreyRPG.Studio/ViewModels/Graph/CanonicalStoryWorkspaceViewModel.Search.cs)
- [`studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalStoryWorkspaceView.xaml`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/studio/src/DarkGreyRPG.Studio/Views/Graph/CanonicalStoryWorkspaceView.xaml)

### S09 · 当前物品格创建、前四项折叠和原生 Tooltip

- [`src/main/java/darkgrey/rpg/client/gui/ItemSlotStrip.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/client/gui/ItemSlotStrip.java)

### S10 · 物品候选及整份任务同步边界

- [`src/main/java/darkgrey/rpg/creator/TaskItemPreview.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/creator/TaskItemPreview.java)
- [`src/main/java/darkgrey/rpg/creator/CreatorSnapshot.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/creator/CreatorSnapshot.java)
- [`src/main/java/darkgrey/rpg/creator/CanonicalTaskPresentationServer.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/creator/CanonicalTaskPresentationServer.java)
- [`src/main/java/darkgrey/rpg/task/persistence/CanonicalTaskCompletionHistory.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/task/persistence/CanonicalTaskCompletionHistory.java)
- [`src/main/java/darkgrey/rpg/creator/CanonicalTaskHistoryProjection.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/creator/CanonicalTaskHistoryProjection.java)

### S11 · 文本协议长度与冻结展示路径

- [`src/main/java/darkgrey/rpg/network/message/canonical/CanonicalSessionNetworkCodec.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/network/message/canonical/CanonicalSessionNetworkCodec.java)
- [`src/main/java/darkgrey/rpg/session/runtime/DynamicContentText.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/session/runtime/DynamicContentText.java)
- [`src/main/java/darkgrey/rpg/session/persistence/CanonicalSessionSavedData.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/session/persistence/CanonicalSessionSavedData.java)
- [`src/main/java/darkgrey/rpg/session/server/CanonicalSessionServerService.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/session/server/CanonicalSessionServerService.java)

### S12 · 有效字体倍率、正文几何和旧容量标定

- [`src/main/java/darkgrey/rpg/client/session/DialogueFontScale.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/client/session/DialogueFontScale.java)
- [`src/main/java/darkgrey/rpg/client/session/CanonicalDialogueLayout.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/client/session/CanonicalDialogueLayout.java)
- [`src/main/java/darkgrey/rpg/client/gui/CanonicalDialogueRenderer.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/client/gui/CanonicalDialogueRenderer.java)
- [`studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/DialogueCapacityProfile.cs`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/DialogueCapacityProfile.cs)
- [`scripts/generate-dialogue-capacity-profile.py`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/scripts/generate-dialogue-capacity-profile.py)
- [`schema/dialogue-capacity-profile.json`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/schema/dialogue-capacity-profile.json)
- [`PLAN/0.3.3.4/Dialogue_Font_2026-09-30.md`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/PLAN/0.3.3.4/Dialogue_Font_2026-09-30.md)

### S13 · 画面绘制、可见媒体、纹理与传输生命周期

- [`src/main/java/darkgrey/rpg/media/CanonicalSessionScene.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/media/CanonicalSessionScene.java)
- [`src/main/java/darkgrey/rpg/media/CanonicalMediaClient.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/media/CanonicalMediaClient.java)
- [`src/main/java/darkgrey/rpg/media/CanonicalMediaTextures.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/media/CanonicalMediaTextures.java)
- [`PLAN/0.3.3.4/Media_Window_2026-09-30.md`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/PLAN/0.3.3.4/Media_Window_2026-09-30.md)

### S14 · typed 动态内容和资源名称引用

- [`studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/DynamicContentText.cs`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/DynamicContentText.cs)
- [`src/main/java/darkgrey/rpg/session/forge/DynamicContentResolver.java`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/session/forge/DynamicContentResolver.java)
- [`studio/src/DarkGreyRPG.Studio/Views/Graph/DynamicContentEditor.cs`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/studio/src/DarkGreyRPG.Studio/Views/Graph/DynamicContentEditor.cs)

### 发布基线

- [`GITHUB_PUBLICATION_2026-09-30.md`](https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/PLAN/0.3.3.4/GITHUB_PUBLICATION_2026-09-30.md)
- 固定 HEAD：`515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf`。
- 用户后续确认的约束已融入本文；早前“超长选项替换错误文字”“前四格＋查看全部”“新增玩家状态节点”等被否定方案，不再作为施工依据。

[S01]: https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/AGENTS.md
[S02]: https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/.agents/skills/studio-node-ui/SKILL.md
[S03]: https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/.agents/skills/story-media-lifecycle/SKILL.md
[S04]: https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/SessionChoiceSchema.cs
[S05]: https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/CanonicalSessionPresentationSchema.cs
[S06]: https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/session/runtime/CanonicalSessionRuntime.java
[S07]: https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/session/runtime/CanonicalSessionRuntime.java
[S08]: https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/studio/src/DarkGreyRPG.Studio/ViewModels/Graph/CanonicalStoryWorkspaceViewModel.Search.cs
[S09]: https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/client/gui/ItemSlotStrip.java
[S10]: https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/creator/TaskItemPreview.java
[S11]: https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/network/message/canonical/CanonicalSessionNetworkCodec.java
[S12]: https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/client/session/DialogueFontScale.java
[S13]: https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/src/main/java/darkgrey/rpg/media/CanonicalSessionScene.java
[S14]: https://github.com/GreyHat633/DarkGreyRPG/blob/515b37485a8a7d065e4f74ed058f2bf4c2c9c8cf/studio/src/DarkGreyRPG.Studio.Core/Graphs/Definitions/DynamicContentText.cs
