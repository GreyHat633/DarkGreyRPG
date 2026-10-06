# 安装与入门

## 获取与安装

从 [GitHub Releases](https://github.com/GreyHat633/DarkGreyRPG/releases) 取得正式 Runtime JAR 和完整 Studio ZIP，核对该次发布校验清单。扫尾开发候选与已发布 0.3.3.7 可以版本号相同，以来源与 SHA-256 区分；不要用开发 JAR、源码 JAR 或裸 EXE 代替成品。

作者在可写位置完整解压 Studio，启动根 EXE，保留 Program／Tools／Docs。首次默认深色，视图菜单可选浅色、深色或跟随系统，每份副本独立保存。普通作者和玩家无需 SDK。

游戏端使用 Minecraft 1.7.10、Forge 10.13.4.1614、Java 8。匹配 Runtime JAR 放入客户端和服务器 `mods`，重启后生效。CNPC 适配另需相应 CustomNPC+；本仓库实测对象为 `1.11.1-fixed-v1`，不要用任意同名新版替代。集成测试所用 UniMixins 0.3.1 为该 CNPC 构建提供 Mixin 加载环境。基础组验证与待确认事项见 [本轮验收](../PLAN/0.3.x.x-Closeout/Acceptance.md)。

## 作者制作

1. 文件菜单新建项目，默认保存在本副本 `Data/Projects`；需要外部路径时明确输入或浏览选择。
2. 创建 Story 和所属角色、物品、Session、Task。UID 和资源 Local ID 由软件管理。
3. 在会话中连接开始、台词、选择与结果；在任务中配置目标与结算。零结算可保存，但不会自动完成。
4. 在 Story 图组合会话与任务，设置启动条件。项目图中连通的 Story 自动形成关联组。
5. 保存全部并验证项目；项目菜单“导出故事包…”导出单包 `.dgrs` 或完整组 `.dgrs.g`。Reference 为只读来源；Import 生成本地副本。“迁移故事内容…”用于追加复制当前内容，不能当旧格式转换器。

AND／OR 输入卡片可直接改名，Enter 或失焦提交，Esc 恢复。点选后用标题右侧减号删除；至少保留两个输入。拖动手柄调整顺序并保留连线，点击卡片外取消选中。

## 服主部署与排错

将导出文件放在**服务器拥有的** `<Minecraft>/DarkGreyRPG/StoryPackages`。单人游戏由集成服务器使用该游戏目录。运行配置在 `DarkGreyRPG/Config/darkgrey_rpg.cfg`；显式设置的外部目录按配置使用。

启动或 OP 执行 `/dgr reload` 后读取当次结果。`/dgr status` 查看状态，`/dgr story list` 列故事，`/dgr story info <StoryUID>` 查看信息，`/dgr reload errors [页码]` 查看错误分页。服务器控制台可以执行状态与重载；需要玩家的启动与状态命令应由游戏内玩家执行，例如 `/dgr story start <StoryUID>`、`/dgr story state <StoryUID>`。

OP 按 O 打开／关闭故事包管理器，Esc 或实际背包键也可关闭。管理器提供启用、禁用、刷新；不要在磁盘同时保留重复 Story UID 的多个容器。Conflict／Error 与手动 Disabled 分开处理，禁用挡新启动，不保证立即终止所有活动实例。组图只读。

角色用游戏内指名器进行实体绑定；普通原版实体与 CNPC 适配的核对范围见验收。资源的显示名称相同不代表身份相同。物品指名器绑定实际物品；奖励需使用有效的本地／引用物品定义。

## 玩家操作

默认 I 打开任务，P 打开 DGR 设置，O 管理器仅限 OP。按键在 Minecraft 控制设置中可改绑。任务页可追踪最多三个任务，满额时明确处理；设置中的“任务追踪默认开启”只作用于此后新接任务。追踪位置与任务弹窗位置分别选择左右，长列表与详情分别滚动。对话和选择按当前界面提示操作。

## 更新与回退

正常保存并停服，备份**完整世界目录**以及 `DarkGreyRPG/Config`、`StoryPackages`，保存当时 Runtime JAR 与校验值；有明确外部数据目录时一并备份。替换匹配客户端／服务端 JAR，再启动检查加载与测试故事。内容变化可能使受影响实例退役，更新前先留备份。

失败时正常停服，恢复同一备份时间点的世界、DGR 配置、故事包与匹配 JAR，再启动。不要混用不同时间点的奖励／任务数据。Studio 更新只替换程序白名单中的文件，完整保留 `Data`；更新前仍建议备份项目及明确外部项目。移动 Studio 时移动整个目录。

## 内测反馈

提供：发生了什么、预期什么、操作步骤、大致时间、相关故事／任务名称、截图和当时日志。Studio 错误日志在本副本 `Data/Logs`；Forge 本轮隔离客户端／专服使用各自 `logs/latest.log`，具体启动器可能另有控制台日志。分享前去掉令牌、私人地址和无关聊天，无需发送整个服务器目录。

历史 PLAN、旧协议和旧架构文档保留当年的背景与验收范围；当前规则以 [当前架构](CURRENT_ARCHITECTURE.md)、此文和 [构建说明](BUILDING.md) 为入口。
