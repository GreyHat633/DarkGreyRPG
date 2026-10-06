# DarkGreyRPG

Minecraft 1.7.10 的 RPG 创作与运行框架。Windows WPF **Studio** 制作故事；Minecraft **Runtime** 执行 Story（故事）、Session（会话）与 Task（任务）。服务端掌管状态、权限、任务和奖励，玩家通过游戏内界面参与。

当前功能版本为 **0.3.3.7**，处于正式内测准备的 0.3.x.x 扫尾阶段。当前源码与既有发布资产可能有差异；本轮验证状态见 [扫尾验收](PLAN/0.3.x.x-Closeout/Acceptance.md)。开发候选不能只凭相同版本号认定为已发布文件。

- [已发布成品](https://github.com/GreyHat633/DarkGreyRPG/releases)：Runtime JAR 与完整 Windows x64 Studio ZIP。
- [安装与入门](docs/GETTING_STARTED.md)：作者、服主、玩家各自的操作。
- [当前架构](docs/CURRENT_ARCHITECTURE.md)：身份、关联组、引用、结算与更新边界。
- [源码构建](docs/BUILDING.md)：工具准备、联网恢复、测试及便携输出。
- [第三方材料](THIRD_PARTY_NOTICES.md)：实际随附组件和仍待确认的来源。

Studio 支持 Story／Session／Task 图、角色与物品资源、跨故事 Flow／Logic 连接、只读 Reference、Import、追加复制故事内容、单故事 `.dgrs` 和关联组 `.dgrs.g` 导出。Task 支持多个结算及显式优先顺序；也可保存零结算草稿。AND／OR 逻辑输入支持改名、带线排序及 Undo／Redo。

游戏端提供 OP 故事包管理器、会话、任务列表与最多三个任务追踪。默认 **O** 打开／关闭故事包管理器，**I** 打开任务，**P** 打开 DGR 设置；可在 Minecraft 按键设置改绑。“任务追踪默认开启”只影响之后新接的任务。

运行环境为 Minecraft **1.7.10**、Forge **10.13.4.1614**、Java **8**。基础 Story／Session／Task 和原版实体绑定实测无需 CustomNPC+；CNPC 实体集成另需匹配组件。本轮客户端／专服实测 CustomNPC+ **1.11.1-fixed-v1** 与 UniMixins **0.3.1**，不推导其他同名版本可用。源码构建所需 Gradle 启动 JDK 与游戏 Java 不同，详见构建说明。

完整解压 Studio ZIP，从根目录 `DarkGreyRPGStudio.exe` 启动，无需另装 .NET。项目默认保存在该副本 `Data/Projects`，也可明确选择外部位置。服主把导出的故事包放入游戏目录 `DarkGreyRPG/StoryPackages`，启动服务器或执行 `/dgr reload`。

DGR 自有源码的项目级授权目前为 **PROJECT_LICENSE_PENDING_OWNER**，未自动赋予 MIT、GPL 或商业授权。第三方原有许可保持独立；公开再分发材料中的待确认项请阅读第三方说明。历史 PLAN、旧协议及旧架构文档保留为开发记录，当前使用入口以上述三份手册为准。
