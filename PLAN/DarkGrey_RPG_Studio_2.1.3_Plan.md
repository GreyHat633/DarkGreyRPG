# DarkGrey RPG Studio 2.1.3 开发计划

> **版本定位**：Project Home、Story 资源工作区与 Dialogue / Quest 创建流程的完整性修复。  
> **核心目标**：在 2.1.2 节点图系统稳定的基础上，解决项目首页选择逻辑、资源库空间利用、引用资源标识、列表滚动与选中样式，以及 Dialogue / Quest 创建路径崩溃和资源生命周期不完整的问题。  
> **基线版本**：DarkGrey RPG Studio 2.1.2。  
> **版本性质**：Studio 体验与资源创建闭环更新；默认不修改 Java Runtime 0.5.0 的节点执行语义。  
> **计划状态**：待实施。  

---

## 1. 版本目标

2.1.3 不继续扩张 Story Flow 节点系统，也不新增大型 Runtime 功能。该版本集中解决 2.1.2 中已经暴露的 Story-first 工作流问题：

1. Project Home 的列表/图谱导航位置不协调；
2. 有剧情时未默认选中第一条，错误显示“没有选中的剧情”；
3. Actor 资源库被“本剧情 / 引用”两块区域割裂，空间利用率极低；
4. Actor、Dialogue、Quest 资源卡过大，选中样式过重，滚动体验不可靠；
5. 引用资源没有统一、简洁、可复用的视觉标识；
6. Actor、Dialogue、Quest、Flow 页面顶部操作区大量留白，有效信息却挤在左侧；
7. Dialogue 和 Quest 的真实“创建”路径会卡死、闪退；
8. 创建流程会过早写入文件和 Story Membership，可能留下半成品；
9. 新 Quest 使用假的 `actor_id = "actor"` 占位目标，不是真正空白草稿；
10. 真实 WPF 窗口路径缺少异常边界和崩溃日志，ViewModel Fake 测试无法覆盖实际闪退。

版本完成后，应达到以下用户体验：

- 打开项目后，只要存在 Story，就默认选中一个 Story 并立即显示剧情概览；
- Project Home 的“剧情列表”与“剧情图谱”按钮布局清楚、对称；
- Actor、Dialogue、Quest 均使用一个统一资源库，不再拆成“本剧情”和“引用”两块；
- 引用资源在同一列表中通过统一图标标识；
- 资源卡紧凑、可快速浏览，滚动条真实可拖动；
- 页面顶部形成统一的 Command Bar，充分利用横向空间；
- “创建 Dialogue / Quest”不再闪退；
- 创建操作先生成未保存草稿，第一次显式保存时才写入资源文件和 Story Owned Membership；
- 放弃草稿不会留下文件或脏 Membership；
- Dialogue 有合理最小骨架；
- Quest 从真正空白状态开始，由用户添加第一个 Objective；
- 创建、复制、引用三种语义明确且一致。

---

## 2. 不可违反的产品原则

### 2.1 Project 级资源身份保持不变

Actor、Dialogue、Quest 继续是项目级资源：

```text
actors/<id>.json
dialogues/<id>.json
quests/<id>.json
```

Story 只记录：

```text
owned_resources
referenced_resources
```

不得重新把 Dialogue 或 Quest 物理嵌套进 Story。

### 2.2 Create、Duplicate、Reference 三种语义必须明确区分

#### 创建

```text
新 ID
新资源
当前 Story 未来拥有
第一次保存后正式落盘
```

#### 从现有资源创建副本

```text
复制内容
新 ID
新资源
当前 Story 未来拥有
与原资源独立
第一次保存后正式落盘
```

#### 引用

```text
同一个项目级资源
不复制
不改变原 Home Story
当前 Story 增加 Referenced Membership
```

### 2.3 创建草稿不等于正式资源

点击“创建”后生成的是 Studio 内存中的 Draft，不应立即：

- 写入 JSON；
- 修改 Story Owned Membership；
- 出现在 Runtime 可加载目录中；
- 留下半成品文件。

只有第一次有效保存，才正式创建资源。

### 2.4 Dialogue、Quest 不承担 Story Flow 的职责

Dialogue 只负责：

- Line；
- Choice；
- Jump 兼容；
- End；
- Named Result。

Quest 只负责：

- Objective；
- Objective Group；
- 完成规则；
- 描述和元数据。

以下逻辑仍属于 Story Flow：

- Start Quest；
- Complete Quest；
- Give Item；
- Give XP；
- Send Message；
- Enter Story；
- Ending 分支。

### 2.5 引用资源与本剧情资源在同一库中显示

不再使用：

```text
本剧情
引用
```

两个独立列表。

统一列表中：

- Owned Resource：不加特殊标识；
- Referenced Resource：右侧显示统一的引用图标；
- Missing Resource：显示警告图标；
- Home Story 和引用来源通过 Tooltip 或详情显示。

### 2.6 创建失败不能导致应用闪退

任何真实 UI 创建路径都必须：

- 捕获异常；
- 写入崩溃日志；
- 在 Output / Problems / Toast 显示可读错误；
- 保持应用继续运行；
- 不留下半成品文件。

---

## 3. 2.1.3 范围概览

本版本分为五个主要工作包：

```text
A. Project Home 导航与默认选择
B. 统一资源库与卡片视觉
C. Story 页面 Command Bar 与空间利用
D. Dialogue / Quest 草稿式创建系统
E. 异常边界、真实窗口测试与发布
```

---

# A. Project Home 导航与默认选择

## A-01：剧情列表 / 剧情图谱按钮左右对齐

当前：

```text
项目首页                                  +
[剧情列表] [剧情图谱]
```

修改为：

```text
项目首页                                  +
[剧情列表]                      [剧情图谱]
```

实现要求：

- 使用两列 `Grid`，不再使用平均分配的 `UniformGrid`；
- “剧情列表”左对齐；
- “剧情图谱”右对齐；
- 两个按钮视觉层级一致；
- 当前模式有清楚但克制的选中状态；
- `+` 新建剧情按钮继续位于标题最右侧；
- 100%、125%、150% DPI 下布局不漂移。

## A-02：有 Story 时默认选中第一条

打开或恢复项目时：

```text
Stories.Count > 0
→ SelectedStory = 第一个可见 Story
→ 右侧立即显示 Story Overview
```

只有：

```text
Stories.Count == 0
```

时，才显示：

```text
当前项目还没有剧情
[新建剧情]
```

不得再用：

```text
SelectedStory == null
```

作为“没有剧情”的判断。

## A-03：刷新列表时保留选择

刷新 Story 列表时遵循：

1. 记录旧 `SelectedStory.Id`；
2. 如果旧 ID 仍存在，继续选择它；
3. 如果旧 Story 被删除：
   - 优先选择相邻项；
   - 否则选择第一项；
4. 新建 Story 后：
   - 自动选中新 Story；
5. 切换列表/图谱后：
   - 保留当前 Story 选择；
6. 从 Story Workspace 返回 Project Home：
   - 选中刚刚打开的 Story。

## A-04：搜索时保持合理选择

搜索规则：

- 当前 Story 仍在结果中：保持选中；
- 当前 Story 被筛除：选择第一条可见结果；
- 无搜索结果：右侧显示“没有匹配结果”，不是“项目没有剧情”；
- 清空搜索：优先恢复搜索前的 Story；
- 搜索不改变 Story 文件和项目数据。

## A-05：Project Home 空状态分类

明确区分三种状态：

### 项目没有 Story

```text
当前项目还没有剧情
[新建剧情]
```

### 有 Story，但搜索无结果

```text
没有匹配当前搜索条件的剧情
[清空搜索]
```

### 有 Story 且已选择

显示完整 Story Overview：

- 显示名称；
- ID；
- 描述；
- 标签；
- Owned / Referenced 资源统计；
- Flow 节点数量；
- 进入剧情按钮。

---

# B. 统一资源库与卡片视觉

## B-01：Actor 统一资源库

删除 Actor 页中的：

```text
本剧情 ListBox
引用 ListBox
```

改为一个：

```text
FilteredMemberships
```

统一资源库。

排序默认建议：

1. Owned；
2. Referenced；
3. 显示名称；
4. ID。

也可允许用户切换：

```text
按名称
按归属
最近编辑
```

排序切换不是首发 Gate 的硬要求。

## B-02：Dialogue / Quest 使用相同规则

Dialogue 和 Quest 资源库统一采用：

```text
一个列表
Owned 与 Referenced 混合显示
引用图标标识
```

不得在不同资源类型中使用不同的引用表现。

## B-03：统一引用图标

不直接使用容易误解的 Unicode `↩` 作为最终图标。

建议使用与现有 Fluent 导航一致的矢量图标：

- 链环；
- 外部链接；
- 共享引用；
- 回链。

按钮显示：

```text
[+ 创建]
[引用图标  引用]
```

资源卡右侧：

```text
[引用图标]
```

Tooltip：

```text
引用资源
来源剧情：<Home Story Display Name>
```

要求：

- Actor、Dialogue、Quest 使用同一图标；
- 图标风格与现有导航栏、菜单一致；
- 不使用 Emoji；
- 不使用不同字体中的特殊字符；
- 缺失资源使用另一种警告图标，不能混淆。

## B-04：资源卡紧凑化

建议资源卡高度：

```text
52—60 DIPs
```

卡片内容：

```text
显示名称                         [引用图标]
resource_id
```

可选第三信息仅在需要时显示为 Tooltip，不长期占行。

Owned Resource 不显示“本剧情”文字。

Referenced Resource 不长期显示“来自：XXX”全文，只显示图标；来源放 Tooltip。

## B-05：选中样式收口

删除大面积高饱和蓝色框。

新样式：

- Normal：透明或低层级背景；
- Hover：轻微背景变化；
- Selected：
  - 浅色选中背景；
  - 左侧 3px Accent 指示条；
  - 或细描边；
- Keyboard Focus：
  - 不覆盖整个卡片；
  - 保持可访问性；
- Disabled / Missing：
  - 降低透明度或显示警告图标。

## B-06：滚动条必须真实可拖动

要求：

- 一个资源库只对应一个 ScrollViewer；
- 资源条目超过可视区域时显示滚动条；
- 鼠标可按住 Thumb 拖动；
- 鼠标滚轮有效；
- Page Up / Page Down 有效；
- Home / End 有效；
- 列表虚拟化开启；
- 不允许卡片或透明层遮挡滚动条命中区域。

WPF 建议：

```text
VirtualizingPanel.IsVirtualizing = true
VirtualizingPanel.VirtualizationMode = Recycling
ScrollViewer.CanContentScroll = true
```

真实自动化必须验证“拖动滚动条”，不能只验证滚轮。

## B-07：资源库宽度与分隔线

资源库建议：

```text
MinWidth = 220
DefaultWidth = 280
MaxWidth = 380
```

允许拖动 GridSplitter 调整。

保存：

```text
Actor Library Width
Dialogue Library Width
Quest Library Width
```

可以共用一个 Story Resource Library Width 设置，避免过度复杂。

---

# C. Story 页面 Command Bar 与空间利用

## C-01：统一 Story 页面 Header

Actor、Dialogue、Quest、Flow 页面统一使用：

```text
StoryName / PageName      Search / Summary                Commands
```

示例：

```text
史莱姆 / 角色      [搜索角色……………………]      1 个角色    [+ 创建] [引用图标 引用]
```

Dialogue：

```text
史莱姆 / 对话      [搜索对话……………………]      3 个对话    [+ 创建 ▼] [引用图标 引用]
```

Quest：

```text
史莱姆 / 任务      [搜索任务……………………]      2 个任务    [+ 创建 ▼] [引用图标 引用]
```

Flow：

```text
史莱姆 / 流程      [常用节点……………………]      20 节点    [实际大小] [适应] [−] 100% [+]
```

## C-02：减少重复标题层级

当前：

```text
史莱姆
角色
创建 / 引用
```

占三层。

改为一层 Command Bar。

Story ID 可作为次要文本：

```text
slime
```

放在 Header 下方或 Tooltip 中，不单独占一整行。

## C-03：搜索框使用横向空间

搜索框不再固定在左侧 270px 小区域。

要求：

- Actor、Dialogue、Quest 的搜索框放入 Header；
- 宽度随窗口扩展；
- 1100×700 最小窗口仍可使用；
- 1700×980 时不会留下大片无意义空白；
- 小窗口下命令可以进入 Overflow Menu。

## C-04：统一页面内容区

Actor / Dialogue / Quest：

```text
┌────────────────────────┬──────────────────────────────────────┐
│ 资源库                 │ 当前资源编辑器                       │
│                        │                                      │
└────────────────────────┴──────────────────────────────────────┘
```

Flow：

```text
┌───────────────────────────────────────────────────────────────┐
│ Flow Canvas                                                   │
└───────────────────────────────────────────────────────────────┘
```

## C-05：保存、引用、删除命令位置

资源级命令：

- 保存；
- 查看引用；
- 解除引用；
- 删除资源；

应放在当前资源编辑器 Header 或底部统一操作栏，不与页面级“创建/引用”混在一起。

页面级：

```text
创建新资源
引用现有资源
搜索和排序
```

资源级：

```text
保存当前资源
删除当前资源
解除当前 Story 引用
查看其它引用
```

---

# D. Dialogue / Quest 草稿式创建系统

## D-01：移除多余的“创建模式”大型模态窗口

当前创建可能经历：

```text
选择空白 / 导入
→ 输入 ID / 名称
```

改为：

```text
[+ 创建 ▼]
```

### 主按钮

直接创建空白草稿。

### 下拉菜单

```text
从现有资源创建独立副本
```

引用继续使用独立按钮：

```text
[引用图标  引用]
```

普通新建只弹出一个紧凑身份窗口。

## D-02：统一资源身份窗口

### Dialogue

```text
┌────────────────────────────────────┐
│ 新建对话                           │
│                                    │
│ 所属剧情     史莱姆                │
│ 资源 ID      [ tavern_offer      ] │
│ 显示名称     [ 酒馆老板的委托    ] │
│                                    │
│                     [取消] [创建草稿]│
└────────────────────────────────────┘
```

### Quest

```text
┌────────────────────────────────────┐
│ 新建任务                           │
│                                    │
│ 所属剧情     史莱姆                │
│ 资源 ID      [ collect_slime_gel ] │
│ 显示名称     [ 收集史莱姆凝胶    ] │
│                                    │
│                     [取消] [创建草稿]│
└────────────────────────────────────┘
```

要求：

- ID 实时验证；
- ID 冲突立即提示；
- 显示名称不能为空；
- Enter 提交；
- Esc 取消；
- 窗口创建异常被捕获；
- 不允许窗口异常导致应用退出。

## D-03：Draft 数据模型

新增统一草稿状态：

```text
IsNewDraft
DraftOwnerStoryId
SourceTemplateId
HasEverBeenSaved
```

Draft：

- 存在于内存；
- 出现在当前资源库顶部；
- 显示“未保存”徽标；
- 可编辑；
- 可放弃；
- 不属于正式 Registry；
- 不写入 Runtime 目录。

## D-04：第一次保存原子落盘

第一次保存必须作为一个事务完成：

```text
验证 Draft
→ 写入资源临时文件
→ 写入更新后的 Story 临时文件
→ 验证两份临时文件
→ 原子替换资源文件与 Story 文件
→ 注册 Owned Membership
→ Draft 转为正式 Document
```

任何一步失败：

- 正式文件保持原样；
- Draft 继续存在；
- 用户可修复后重试；
- 不产生孤儿文件；
- 不产生 Membership 指向不存在资源的状态。

若当前 AtomicFileWriter 无法跨两个文件实现真正事务，需要：

1. 先捕获两份原始快照；
2. 执行两次原子写；
3. 第二次失败时恢复第一份；
4. 写入结构化错误日志；
5. 测试回滚。

## D-05：放弃 Draft

用户：

- 切换资源；
- 切换 Story；
- 关闭项目；
- 退出程序；

若 Draft 未保存，询问：

```text
保存
放弃草稿
取消
```

放弃后：

- 从资源库移除；
- 不删除任何正式文件；
- 不修改 Story；
- 不留下 Recovery，除非用户选择保留草稿。

## D-06：Dialogue 新建骨架

新 Dialogue Draft 默认：

```text
entry = end
nodes:
  - id: end
    type: end
    result: complete
```

UI 不直接把用户丢进一个只有技术节点的空白编辑器。

显示 Empty State：

```text
这个对话目前还没有台词。

当前命名出口：
complete

[添加第一句台词]
[添加玩家选择]
[编辑命名出口]
```

### 添加第一句台词

自动生成：

```text
line_1
→ end
entry = line_1
```

用户只需填写：

- Speaker；
- Text。

## D-07：Dialogue Editor 目标 UI

建议结构：

```text
史莱姆 / 对话    [搜索……]              [+ 创建 ▼] [引用]

┌────────────────────────┬─────────────────────────────────────┐
│ 对话结构               │ 当前节点                            │
│                        │                                     │
│ ● 酒馆老板：欢迎光临   │ 类型       台词                     │
│ ◆ 是否接受委托？       │ Speaker    [酒馆老板 ▼]             │
│ ■ accept               │ 文本       [......................] │
│ ■ refuse               │ 下一节点   [decision ▼]             │
└────────────────────────┴─────────────────────────────────────┘
```

### Line 编辑

- Speaker：项目 Actor 选择器；
- Text：多行文本；
- Next：现有 Dialogue Node 选择器；
- Missing Speaker：可修复警告。

### Choice 编辑

- Prompt；
- Choice 列表；
- 每个 Choice：
  - Text；
  - Target Node；
  - 删除按钮；
- 添加选项；
- 支持排序。

### End 编辑

- Named Result；
- Result ID 验证；
- 重复出口警告。

### Jump 兼容

- 可加载；
- 可编辑；
- 不一定提供为默认新建动作；
- 保存时不丢失。

## D-08：Dialogue 保存条件

至少验证：

- ID 合法；
- Entry 存在；
- 节点 ID 唯一；
- 所有 Next / Choice Target 存在；
- Line Speaker 存在；
- 至少有一个 End；
- Named Result 非空；
- Named Result 合法；
- 可达路径能到达 End；
- 未知兼容字段不丢失。

## D-09：Quest 新建必须真正空白

新 Quest Draft 不再生成：

```text
InteractActor
actor_id = "actor"
```

默认：

```text
title
description = ""
objectives = []
objective_groups = []
```

UI Empty State：

```text
这个任务还没有目标。

选择第一个任务目标：

[击杀实体]
[收集物品]
[与角色交互]
```

旧 `ReachLocation`：

- 继续兼容加载；
- 可编辑；
- 默认新建菜单可暂不显示；
- 不得丢失。

## D-10：Quest 第一个 Objective 流程

### Kill Entity

```text
目标名称    [消灭史莱姆]
实体 ID     [minecraft:slime]
数量        [10]
```

### Collect Item

```text
目标名称    [收集史莱姆凝胶]
物品 ID     [modid:slime_gel]
Metadata    [0]
数量        [5]
```

### Interact Actor

```text
目标名称    [向酒馆老板复命]
角色        [酒馆老板 / boss ▼]
次数        [1]
```

要求：

- Actor 使用项目资源选择器；
- 不生成假的 Actor ID；
- 数值字段限制合法范围；
- 字段旁显示即时验证。

## D-11：Quest Editor 目标 UI

```text
史莱姆 / 任务    [搜索……]              [+ 创建 ▼] [引用]

任务名称    [收集史莱姆凝胶]
任务描述    [帮助酒馆老板收集材料……]

完成规则    [全部目标完成 ▼]

┌────────────────────────┬─────────────────────────────────────┐
│ 任务目标               │ 当前目标                            │
│                        │                                     │
│ 收集史莱姆凝胶 ×5      │ 类型       收集物品                 │
│ 向老板复命 ×1          │ 物品       [modid:slime_gel]        │
│                        │ Metadata   [0]                      │
│                        │ 数量       [5]                      │
└────────────────────────┴─────────────────────────────────────┘
```

## D-12：Objective Group 的普通用户表现

默认只显示：

```text
完成规则
├─ 全部目标完成
├─ 任意一个目标完成
└─ 按顺序完成
```

内部映射：

```text
ALL
ANY
SEQUENCE
```

规则：

- 添加第一个目标时自动创建 `main` group；
- 新目标默认加入 `main`；
- 改完成规则只修改 `main.mode`；
- 普通用户不需要填写 Group ID；
- 多 Group 结构放进“高级任务结构”。

## D-13：Quest 保存条件

至少验证：

- ID 合法；
- Title / Display Name 合法；
- 至少一个 Objective；
- Objective ID 唯一；
- Objective 配置完整；
- 数量为正；
- Actor 引用存在；
- Group 引用的 Objective 存在；
- 至少一个有效 Group；
- 未知兼容字段不丢失。

未满足时：

- Draft 可继续编辑；
- Save 禁用或保存时明确提示；
- 不闪退；
- 不生成假数据。

## D-14：从现有资源创建独立副本

流程：

```text
+ 创建 ▼
→ 从现有资源创建独立副本
→ 选择模板
→ 输入新 ID / 新显示名称
→ 创建未保存 Draft
→ 第一次保存后正式落盘
```

要求：

- 原资源不变；
- 新 Draft 复制内容；
- Home Story 变为当前 Story；
- 引用关系不复制为 Owned；
- 外部 ID 引用保留；
- 可独立修改。

## D-15：引用现有资源

点击：

```text
[引用图标  引用]
```

弹出资源选择器：

- 只显示尚未在当前 Story 中出现的资源；
- 显示 Home Story；
- 显示资源类型；
- 支持搜索；
- 支持预览；
- 确认后只修改 Referenced Membership；
- 不创建新文件；
- 不改变原 Home Story；
- 立即在统一资源库中显示引用图标。

---

# E. 稳定性、异常边界与真实窗口测试

## E-01：将真实 Dialog 调用放入异常边界

所有以下调用必须处于 `try/catch`：

- `RequestCreate`
- `RequestImportIdentity`
- `PickResource`
- `ShowDialog`
- Dialog 构造
- Owner 绑定
- 资源加载

异常处理：

```text
Output.Error
Problems.operation.failure
Toast.Error
Crash Log
```

不得逃出 ICommand 导致进程退出。

## E-02：全局 UI 异常日志

增加：

```text
Application.DispatcherUnhandledException
AppDomain.CurrentDomain.UnhandledException
TaskScheduler.UnobservedTaskException
```

日志路径建议：

```text
%AppData%/DarkGreyRPG/Studio/logs/crash-<timestamp>.log
```

日志包括：

- Studio Version；
- OS；
- DPI；
- Theme；
- Current Project；
- Current Story；
- Current Route；
- Exception；
- Stack Trace；
- Inner Exception；
- Last UI Command。

UI 线程异常原则：

- 已知可恢复异常：记录并继续；
- 状态可能损坏：显示错误并安全关闭当前操作；
- 不应无条件吞掉所有异常。

## E-03：Fake Dialog 测试之外增加真实窗口测试

必须用真实 Release WPF 进程验证：

### Dialogue 创建

1. 打开有 Story 的项目；
2. 进入 Dialogue；
3. 点击“创建”；
4. 填写 ID、名称；
5. 创建 Draft；
6. 添加 Line；
7. 保存；
8. 重启；
9. 文件存在；
10. Story Owned Membership 正确。

### Quest 创建

1. 进入 Quest；
2. 点击“创建”；
3. 填写 ID、名称；
4. 创建空 Draft；
5. 添加 Collect Item；
6. 保存；
7. 重启；
8. 文件存在；
9. Story Owned Membership 正确；
10. 不存在 `actor_id = "actor"` 假数据。

### 取消

- 身份窗口取消：不产生 Draft；
- Draft 放弃：不产生文件；
- Import 取消：不产生文件；
- Reference 取消：不修改 Story。

### 异常注入

- 文件不可写；
- Story 文件被占用；
- 无效 ID；
- 冲突 ID；
- 第二阶段写入失败；
- 回滚后无孤儿资源。

## E-04：滚动和资源库真实 QA

准备：

```text
30 Actors
30 Dialogues
30 Quests
```

验证：

- 统一列表；
- 引用图标；
- 拖动滚动条；
- 滚轮；
- 搜索；
- 选择；
- 编辑；
- 切换后选择保持；
- 1100×700；
- 1700×980；
- 100%、125%、150% DPI。

---

## 4. 技术设计建议

## 4.1 Unified Resource Membership Item

建议统一：

```csharp
StoryResourceMembershipItemViewModel
```

字段：

```text
Type
Id
DisplayName
MembershipKind
IsOwned
IsReferenced
IsMissing
HomeStoryId
HomeStoryDisplayName
SourcePath
IconKind
Tooltip
```

Actor 可扩展 Tags。

Dialogue / Quest 复用同一 Item Template。

## 4.2 Shared Resource Library Control

新增：

```text
Views/StoryResourceLibraryView.xaml
Views/StoryResourceLibraryView.xaml.cs
```

参数：

```text
Items
SelectedItem
SearchText
CreateCommand
CreateFromExistingCommand
ReferenceCommand
EmptyText
AutomationPrefix
```

Actor、Dialogue、Quest 复用。

## 4.3 Shared Story Page Command Bar

新增：

```text
Views/StoryPageCommandBar.xaml
```

插槽：

- Breadcrumb；
- Search；
- Summary；
- Primary Command；
- Secondary Command；
- Overflow。

## 4.4 Draft Document Boundary

建议：

```text
DialogueDraftSession
QuestDraftSession
```

或扩展现有 Document：

```text
IsNewDraft
PendingHomeStoryId
SourceTemplateId
```

要求不把 Draft 注册到正式 Repository，直到第一次保存。

## 4.5 Multi-file Save Transaction

新增：

```text
ProjectResourceCreationTransaction
```

负责：

```text
Resource JSON
Story Membership JSON
Rollback Snapshot
Recovery Log
```

Actor 后续也可以迁移到同一机制，但 2.1.3 Gate 重点是 Dialogue / Quest。

---

## 5. 预计修改文件

### Project Home / Shell

- `studio/src/DarkGreyRPG.Studio/MainWindow.xaml`
- `studio/src/DarkGreyRPG.Studio/MainWindow.xaml.cs`
- `studio/src/DarkGreyRPG.Studio/ViewModels/ProjectHomeViewModel.cs`
- `studio/src/DarkGreyRPG.Studio/ViewModels/ShellViewModel.cs`

### Story Resource Workspace

- `studio/src/DarkGreyRPG.Studio/ViewModels/StoryWorkspaceViewModels.cs`
- `studio/src/DarkGreyRPG.Studio/Views/DialogueEditorView.xaml`
- `studio/src/DarkGreyRPG.Studio/Views/QuestEditorView.xaml`
- Actor editor模板或独立 View
- 新增 Shared Library / Command Bar 控件

### Core / Repository

- `ProjectService.cs`
- `ProjectResourceRegistry.cs`
- `DialogueRepository.cs`
- `QuestRepository.cs`
- `DialogueDocument.cs`
- `QuestDocument.cs`
- 新增 Resource Creation Transaction
- 新增 Draft Session

### Dialogs / Services

- `IResourceWorkspaceDialogs.cs`
- `ResourceWorkspaceDialogs.cs`
- `ResourceDialogViewModels.cs`
- 资源身份窗口
- 资源选择器
- 创建下拉菜单

### App / Logging

- `App.xaml`
- `App.xaml.cs`
- 新增 CrashLogService

### Tests / QA

- Core Tests
- WPF Tests
- 新建 `studio/qa/2.1.3-resource-creation-ui-acceptance.ps1`

具体文件可调整，但不得把所有逻辑继续堆入 `ShellViewModel`。

---

## 6. 实施阶段与 Gate

# 阶段 0：基线确认

1. 确认 2.1.2 `main`；
2. 运行 Core / WPF / Gradle baseline；
3. 复现 Dialogue 创建闪退；
4. 复现 Quest 创建闪退；
5. 获取真实异常日志；
6. 保存 2.1.2 安全 checkpoint。

**Gate 0：**

- 闪退可稳定复现；
- 已获得异常类型和 Stack Trace；
- 不能仅凭猜测修改；
- 2.1.2 基线测试结果记录完成。

# 阶段 A：Project Home 修复

- 左右对齐列表/图谱按钮；
- 默认选择第一条 Story；
- 刷新保留选择；
- 空项目 / 搜索无结果状态分离。

**Gate A：**

- 有 Story 时启动即有选择；
- 无 Story 才显示新建空状态；
- 新建 Story 自动选中；
- 删除 Story 后选择合理回落。

# 阶段 B：统一资源库

- Actor 合并列表；
- Dialogue / Quest 统一卡片；
- 引用矢量图标；
- 紧凑卡片；
- 选中样式；
- 可拖滚动条；
- 虚拟化。

**Gate B：**

- 30 条资源可快速浏览；
- 一个库同时显示多条；
- 引用资源可识别；
- Scroll Thumb 可拖动；
- 大蓝框消失。

# 阶段 C：Command Bar 与布局

- 四个 Story 页面统一 Header；
- 搜索占用横向空间；
- 按钮右对齐；
- 资源库可调整宽度；
- 1100×700 响应式。

**Gate C：**

- 无大面积闲置 Header；
- 操作不挤在左侧；
- 四页视觉规则一致；
- Flow 画布不被压缩。

# 阶段 D：异常边界

- Dialog 调用进入 try/catch；
- 全局异常日志；
- Output / Problems / Toast；
- 闪退改为可恢复错误。

**Gate D：**

- 点击创建不再退出进程；
- 人为注入 Dialog 异常，应用继续运行；
- 日志包含完整 Stack Trace。

# 阶段 E：Dialogue Draft 创建

- 身份窗口；
- 未保存 Draft；
- 默认 End complete；
- Empty State；
- 第一条 Line 快捷创建；
- 第一次保存事务；
- 放弃 Draft。

**Gate E：**

- 新建 Dialogue 不立即写文件；
- 保存后文件和 Owned Membership 同时存在；
- 放弃不留文件；
- 重启可加载。

# 阶段 F：Quest Draft 创建

- 真正空白 Draft；
- 选择第一个 Objective；
- ALL / ANY / SEQUENCE 用户化；
- 不生成假 Actor；
- 第一次保存事务；
- 放弃 Draft。

**Gate F：**

- 新建 Quest objectives 初始为空；
- 添加 Collect / Kill / Interact 有效；
- 保存前验证；
- 不存在 `actor_id = "actor"`；
- 重启可加载。

# 阶段 G：Duplicate / Reference

- 从现有资源创建副本；
- 引用现有资源；
- 统一资源选择器；
- 引用图标；
- 独立性测试。

**Gate G：**

- 副本后续修改不影响源；
- 引用修改反映同一资源；
- Reference 不改变 Home Story；
- 列表标识正确。

# 阶段 H：真实窗口回归与发布

- 真实 Dialogue 创建；
- 真实 Quest 创建；
- Scroll Drag；
- DPI；
- 最小窗口；
- 打包；
- 文档；
- GitHub。

**Gate H：**

- 无 P0 / P1；
- 所有自动化通过；
- 真实 Release WPF 通过；
- EXE 版本 2.1.3.0；
- Git 工作区干净。

---

## 7. 测试矩阵

## 7.1 Project Home

- 0 Story；
- 1 Story；
- 20 Stories；
- 默认选择；
- 新建；
- 删除；
- 搜索；
- 切图谱；
- 返回项目首页；
- 最近项目恢复。

## 7.2 Resource Library

- Owned only；
- Referenced only；
- Mixed；
- Missing；
- 30+ items；
- Scroll drag；
- Keyboard navigation；
- Search；
- Selection preservation；
- Reference icon Tooltip。

## 7.3 Draft Lifecycle

- Create Draft；
- Edit；
- Save；
- Discard；
- Cancel；
- Switch Story；
- Close Project；
- Exit App；
- Crash Recovery 可选；
- Duplicate；
- Reference。

## 7.4 Transaction Failure

- Resource write failure；
- Story write failure；
- Validation failure；
- Permission denied；
- File lock；
- ID conflict；
- Rollback；
- No orphan file；
- No broken Membership。

## 7.5 Dialogue

- Default End；
- Add first Line；
- Add Choice；
- Multiple Ends；
- Named Result；
- Missing Speaker；
- Invalid target；
- Restart persistence。

## 7.6 Quest

- Empty Draft；
- Kill Entity；
- Collect Item；
- Interact Actor；
- ALL；
- ANY；
- SEQUENCE；
- Missing Actor；
- Invalid amount；
- Restart persistence。

---

## 8. 文档更新

新增：

- `docs/2.1.3_RESOURCE_CREATION_MODEL.md`
- `docs/2.1.3_STORY_RESOURCE_LIBRARY.md`
- `docs/2.1.3_RELEASE_NOTES.md`
- `PLAN/DarkGrey_RPG_Studio_2.1.3_Plan.md`

更新：

- `docs/2.1_UI_UX.md`
- `docs/DECISIONS.md`
- `docs/TESTING.md`
- `studio/README_WPF.md`
- 根 `README.md`

文档必须明确：

- Create / Duplicate / Reference；
- Draft；
- 第一次保存；
- Dialogue 默认骨架；
- Quest 空白目标；
- Objective 完成规则；
- 引用图标；
- Project Home 默认选择。

---

## 9. 版本与发布

同步版本：

```text
Version         2.1.3
AssemblyVersion 2.1.3.0
FileVersion     2.1.3.0
```

更新：

- `.csproj`
- 窗口标题
- About
- package script
- Release Notes
- EXE 文件属性

Java Runtime 默认仍为：

```text
0.5.0
```

除非真实验证发现必须进行兼容修复。

---

## 10. 非目标

2.1.3 不包含：

- Dialogue 全面改为自由节点画布；
- Quest 全面改为自由节点画布；
- 新 Story Flow 节点类型；
- Story Runtime 持久化；
- Entry Presentation Runtime；
- WPF Live Bridge；
- Minecraft 客户端完整分支 E2E；
- 通用货币系统；
- Actor 创建流程全面重构；
- Project Story Graph 逻辑编辑；
- 第三方 UI 框架迁移。

---

## 11. Definition of Done

### Project Home

- [ ] 列表与图谱按钮左右对齐；
- [ ] 有 Story 时默认选中第一条；
- [ ] 空项目状态判断正确；
- [ ] 搜索无结果状态正确；
- [ ] 刷新保持选择。

### Resource Library

- [ ] Actor 不再拆成 Owned / Referenced 两块；
- [ ] Dialogue / Quest 使用相同统一规则；
- [ ] 引用资源有统一矢量图标；
- [ ] 资源卡明显压缩；
- [ ] 大蓝色选中框移除；
- [ ] 滚动条可拖动；
- [ ] 30+ 资源浏览可用。

### Layout

- [ ] Actor / Dialogue / Quest / Flow Command Bar 统一；
- [ ] 横向空间被合理利用；
- [ ] 搜索框和命令不再挤在左侧；
- [ ] 1100×700 可用；
- [ ] 1700×980 不出现大块无意义空白。

### Stability

- [ ] Dialogue 创建不闪退；
- [ ] Quest 创建不闪退；
- [ ] Dialog 异常可恢复；
- [ ] 有完整崩溃日志；
- [ ] Output / Problems / Toast 有可读错误。

### Draft Creation

- [ ] Dialogue 创建后先是 Draft；
- [ ] Quest 创建后先是 Draft；
- [ ] Draft 不立即写正式 JSON；
- [ ] Draft 不立即修改 Story Membership；
- [ ] 第一次保存原子创建；
- [ ] 放弃 Draft 不留文件；
- [ ] 写入失败可回滚。

### Dialogue

- [ ] 默认 End / complete；
- [ ] Empty State 清楚；
- [ ] 第一条 Line 可快捷创建；
- [ ] Speaker 使用 Actor 选择器；
- [ ] Choice 目标使用节点选择器；
- [ ] Named Result 校验完整。

### Quest

- [ ] 初始 objectives 为空；
- [ ] 不生成假 Actor；
- [ ] 可添加 Kill / Collect / Interact；
- [ ] ALL / ANY / SEQUENCE 使用用户化名称；
- [ ] 保存前完整验证；
- [ ] 重启后正确加载。

### Duplicate / Reference

- [ ] 从现有资源创建副本可用；
- [ ] 副本独立；
- [ ] Reference 共享；
- [ ] Reference 不改变 Home Story；
- [ ] 列表图标正确。

### Release

- [ ] Core tests 全通过；
- [ ] WPF tests 全通过；
- [ ] Runtime build/probes 全通过；
- [ ] 真实 WPF QA 全通过；
- [ ] EXE 版本 2.1.3.0；
- [ ] 文档和截图齐全；
- [ ] Git 工作区干净。

---

## 12. 最终强制要求：Codex 必须上传完整成品到 GitHub

开发、测试、打包完成后，Codex 不得只保留本地成果。

必须：

1. 将完整 2.1.3 源码、测试、文档和 QA 脚本提交到：
   - `https://github.com/GreyHat633/DarkGrey_RPG`
2. 从当前真实 2.1.2 `main` 创建远端开发分支，例如：
   - `studio/2.1.3-resource-creation-workflow`
3. 分阶段提交，不得把全部改动压成一个不可审计的巨型 commit。
4. 推送远端分支。
5. 创建面向 `main` 的 Pull Request。
6. PR 描述必须包含：
   - 2.1.3 范围；
   - Dialogue / Quest 创建模型；
   - 崩溃根因；
   - 测试结果；
   - Runtime 是否修改；
   - EXE 路径和 SHA-256；
   - QA 截图路径；
   - 已知限制。
7. 合并前确认：
   - CI / 本地 Gate；
   - 真实窗口 QA；
   - GitHub 上可以读取所有最终源码。
8. 完成后给出：
   - 仓库；
   - 分支；
   - PR 编号或链接；
   - 最终 commit SHA；
   - 测试数量；
   - EXE SHA-256；
   - Runtime JAR SHA-256；
   - 已知限制。
9. 若项目没有 GitHub Release，则创建 `Studio 2.1.3` Release，并附：
   - Release Notes；
   - EXE 或可下载构建产物；
   - SHA-256；
   - 升级说明。
