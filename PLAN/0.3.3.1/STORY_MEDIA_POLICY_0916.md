# 0.3.3.1 故事包媒体生命周期施工记录（2026-09-16）

## 已实现规则

- 实际触发故事 Start 时才预加载整包声明的图片、音频。附近 NPC 不触发预热；候选触发查询不预加载。
- 图谱中的流程输出→流程输入、逻辑输出→逻辑输入均计入连接。按输出到输入方向广度优先遍历，当前包优先，混合连线统一去重、防环；预加载不启动相连故事。
- 同时最多 3 个包下载；客户端全局最多 10 个媒体缓存包名额（包括下载中），服务器/本地存档命名空间隔离。没有玩家设置入口，没有媒体总容量或分类淘汰配额。
- 非运行包按 LRU 整包淘汰；运行包保护。名额全受保护时排队；资源全部独立校验后才标记整包就绪。
- 共享引用复用，下载/播放/缓存所有者持有 pin；最后所有者释放后清理。包更新回收旧引用；断线暂停旧请求；持久化记录不直接恢复 ready 标记，重新校验文件。
- 对话按需渲染/解码，整包磁盘缓存不等于全部解码或上传 GPU。独立会话调试仍可按需取得媒体，不会额外遍历相连包。

规范：`.agents/skills/story-media-lifecycle/SKILL.md`；`AGENTS.md` 已添加使用入口，Skill 校验通过。

## 自动检查

- `storyMediaCacheIndexProbe` PASS：20 包 FIFO 排队、3 包并发、10 包名额、10 个运行包保护及释放、完整性、共享引用、LRU、版本替换、持久化恢复。
- `storyMediaServerProbe` PASS：流程/逻辑混合环路、只遍历输出方向、近邻优先、500 包混合环路去重、带版本的网络清单编解码。
- `mediaTransfer0330Probe`、`sessionPresentation0330Probe` PASS。
- `canonicalStoryForgeCoordinatorProbe`、`storyBoundary0331Probe`、`dgrsMediaLifecycle0331Probe` PASS。
- 编译、限定文件 Spotless、`reobfJar`、相关 tracked 文件 `git diff --check` 通过。

## 游戏验证

使用 `.tooling/0.3.3.1/live-client` 的独立 Minecraft 客户端和测试存档；未修改用户原故事包或原存档。

1. 冷缓存进入世界、生成附近 NPC：缓存包 0，下载 0，预热 0。
2. 通过客户端 NPC 互动实际触发 `GreyHat_:test_story`：3 张图片及 1 段音频全部就绪，包受运行保护。GPU 只上传当前头像；语音播放引擎报告正在播放。
3. 实际点击进入选择：头像仍显示（有截图）。选择拒绝后故事结束，缓存保留、运行保护解除。
4. 再次触发故事：复用 4 个媒体文件，未新增导入。断线重连后恢复运行保护。
5. 实际传送进入区域：`QA0330:presentation` 的区域 Start 触发，其 2 个媒体资源就绪；相连 `LiveHarness:FlowTarget` 被预加载但保持未运行。
6. 关闭并重新启动 JVM：先恢复 3 个缓存包记录，预加载为 0；进入原存档后命中 6 个磁盘媒体文件，导入数 0、网络下载数 0。
7. 独立触发 `LiveHarness:FlowTarget`，命令执行成功。验证结束后关闭测试客户端。

证据目录：`PLAN/0.3.3.1/evidence/story-media-policy-0916/`。运行中的截图、JSON 状态与构建日志分别记录，不将代码检查当作实机操作。

## 交付

- JAR：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\darkgrey_rpg-0.3.3.1.jar`
- 大小：1,217,129 字节
- SHA-256：`62570B6797A49A0084D35AD84A941EF77C51D685E0E94629E161296DDF1D0362`
- 已与 `build/libs` 产物核对哈希一致。
- 本次没有 Studio UI/客户端变更，没有重新发布 Studio EXE，没有推送 GitHub。

边界：首次 Start 紧接的第一句仍可能赶不上资源加载；没有声称零延迟。实际游戏检查为集成服务端；远程独立服务器和几百个大包的真实带宽压力尚未实测。语音状态由播放引擎确认，未把它描述成人工听音验收。

USER_ACCEPTED=NO；本次验证不代表整个 0.3.3.1 已获用户验收。
