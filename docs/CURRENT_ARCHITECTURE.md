# 当前架构

本文描述 0.3.3.7 当前代码；历史草案不作为实现合同。Studio 是 Windows WPF 作者工具，Runtime 是 Forge 游戏端；服务端负责准入、实例、任务、奖励和权限。Project 是创作工作区，没有新的玩家运行身份。

## 身份与资源

Story 创建时分配固定 UID，之后不重生成。资源由所属 Story UID、类型和隐藏 Local ID 定位；作者以名称操作，不需要管理 Namespace。Session、Task、Actor、Item 与 Item Group 使用当前结构化地址。当前格式拒绝旧身份输入，不承诺自动迁移旧 Dialogue／Quest 项目。

“迁移故事内容”执行**追加复制**：目标允许已有内容，保留原内容，为副本分配技术身份并映射内部引用；不修改原 Story UID，不复制源 Story 在项目图中的外部连线。草稿保存和可运行导出是不同检查层级，导出会严格验证图与资源。

依据：[身份值对象](../studio/src/DarkGreyRPG.Studio.Core/Identity/)、[当前合同](../PLAN/0.3.3.6/Technical_Contracts.md)、`StoryContentCopyService` 与 `StoryContentCopyHistory`。

## 图、组和外部内容

Story 图组合会话、任务与逻辑。Session 图描述台词、选择和结果。Task 图描述目标、逻辑及结算；多个结算都成立时，仅 **Task Flow 公开输出的显式顺序**决定结算优先级。零结算不会自动生成完成出口；未到结算时任务仍可保持活动。其他输出排序只改变展示，不能宣称创造执行优先级。

项目图允许编辑跨 Story 的公共 Flow／Logic 连接。由这些实际连线的无向连通分量派生 Group；方向仍用于执行与可达遍历。Group 没有独立 UID、玩家进度或 Runtime。游戏包管理器展示的组图只读，不等同 Studio 的可编辑项目图。

`.dgrs` 包含一个 Story；`.dgrs.g` 是完整关联组容器，平铺成员载荷、共享媒体与组连接，不嵌套成员 ZIP。成员运行状态、内容代次和媒体所有权仍按 Story 管理。

Reference 保留来源身份并只读。整组引用生成只读成员视图；单资源引用不会自动扩组。接入来源公共边界时，需要该来源的完整关联闭包，不能省略所依赖成员；也不能理解成“所有外部 Story 禁止连接”。Import 创建新的本地身份并重映射内部关系。详见 `DgrsStoryPackageExporter`、组容器读取器与 [当前合同](../PLAN/0.3.3.6/Technical_Contracts.md)。

## 安装、准入与更新

物理容器包含单包和组包。管理器区分 Enabled、Disabled、Conflict、Error，保留手动启用意图；诊断状态优先级为 Error > Conflict > Disabled > Enabled。

- Disabled 挡新的启动／重开；现有实例可继续恢复或完成。
- Conflict 表示磁盘容器的身份冲突；同一 UID 涉及的容器对称受阻，不自动选新包替旧包。手动禁用不消除磁盘上的冲突。
- Error 表示载荷或资格错误；不能写成普通手动禁用。冲突、错误、删除或实际内容变化可能使受影响实例退役，保留相应完成历史与奖励防重。
- 恢复 Enabled 不自动重开错误实例。内容代次按成员运行语义判断，文件名／编辑布局变化不等同运行内容变化；不能保证任意 reload 无条件保留全部进度。

管理器请求在服务端验证 OP、修订和来源。客户端按键不能绕过权限。包读取先验证再发布；错误报告与加载行为以当次日志为准。

## 数据边界

Studio 的根 EXE 加载 `Program/DarkGreyRPGStudio.dll`，运行库在 `Program`，媒体工具在 `Tools/FFmpeg`。副本专属设置、日志、缓存、临时文件归 `Data`；默认项目归 `Data/Projects`。用户明确创建的外部项目留在所选位置并记录绝对路径；其他外部项目打开前导入本地。移动整套目录可恢复内部相对路径，新副本不读取共享 Windows 用户设置。详见 [便携存储](../studio/PORTABLE_STORAGE.md)。

Minecraft 管理自己的 `DarkGreyRPG/Project`、`StoryPackages`、`Cache`、`Config`，世界中另有玩家与运行保存数据。更新／回退必须备份匹配的世界、配置、包和 Runtime，不能只替换一个配置文件。

任务弹窗与追踪位置独立。自动追踪仅用于未来新接任务；最多三项追踪。任务窗口的上下／左右布局分别给列表分配 40% 可用区域，列表和详情独立滚动。这些是既有行为，本次扫尾不重做。
