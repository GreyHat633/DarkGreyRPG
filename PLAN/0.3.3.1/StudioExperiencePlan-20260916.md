# Studio 编辑体验与游戏演出修复交付记录

日期：2026-09-16。版本：0.3.3.1。USER_ACCEPTED=NO；本记录不是全部功能无缺陷的保证。

## 实现

- 添加菜单将音乐、画面归入“演出”，保留原图类型限制；画面按钮为“添加/移除”，Inspector 不再重复显示节点类型。
- 图片按像素比例在 16:9 预览的 75% 范围居中。异常导入显示具体原因，保留原图层；异步完成校验节点、项目、加载状态和数量上限。完整画面替换与空列表清屏语义不变。
- 标题新增“等待播放结束”，旧文件默认等待；非等待标题提交后继续故事，完成回执不重复推进。Java/Studio/消息支持兼容字段。
- 台词显示速度 0–120 字/秒，默认 30，0 为立即显示；按时间逐字显示，点击先补全后继续，选项保留完整台词和头像。
- 任务变化由服务端产生带实例、目标、事件标识的通知；客户端合并同任务、最多三张、4 秒后移除，不抢输入。计数更新和初始同步不报新任务。
- 两套项目内剪贴板分别处理节点与参数。节点深复制配置、内部连线、相对布局及动态端口；会话/任务深复制资源、布局及图片名称，ID 使用 _copy/_copy1 等，固定节点拒绝整次粘贴。参数粘贴保留目标身份和资源元数据，端口仅按确定身份或唯一名称/方向/类型匹配；共享引用和无法保留的连线须确认。文件与图变更组合撤销，已打开的共享引用同步更新。
- 运行目录位于 DarkGreyRPG/Cache/StoryPackagesRuntime/<安装路径哈希>。可识别旧缓存迁移不覆盖冲突数据，无法识别的内容保留并诊断。不改变 10 包缓存、3 包并行预加载及流程/逻辑关联规则。

## 自动验证

- WPF 全套：577/577 通过（plan-wpf-final2.log）。后续剪贴板保存后撤销修正：针对性 10/10 通过（plan-clipboard-save.log）。
- 核心测试：444 通过、3 跳过（plan-core-tests.log）。
- 图片异步切换节点/项目、画面编辑与剪贴板针对性：23/23 通过（plan-import-guard.log）。
- Java：Title0330、CanonicalSessionClientModel、SessionPresentation0330、TaskNotificationsPlan、PackageRuntimeMigrationPlan、DgrsMediaLifecycle0331 均通过。覆盖标题新旧消息、Unicode/时间速度、服务端事件与客户端三卡/去重/到期、旧缓存迁移冲突和安装目录隔离。
- scoped Spotless 检查及 assemble/reobfJar 通过（plan-java-assemble.log）。媒体生命周期探针的 reparse guard 项为 SKIP_UNAVAILABLE，不能将它算作已实测。

## Windows 原生 UI 实测

使用 UI Automation、Win32 输入和目标窗口截图，未使用 Computer Use。

- 正式 dist EXE 的 Ctrl+C/Ctrl+V：10 → 11 节点，Ctrl+Z 恢复 10。早期候选还实测了重做。
- Inspector 右键参数复制成功后，参数粘贴由灰色变为可用。
- 原生“演出 → 画面”菜单创建节点；横图、竖图、方图实际导入。
- 扩展名伪装、损坏文件、8193×4096 超限文件分别给出原因；失败前后均保持 3 层，窗口未崩溃、按钮恢复。
- 串联的画面 B 在正式 EXE 内实际再次导入图片，1 → 2 层，无崩溃，随后撤销。
- 独立项目保存重开保留所导入图片和几何配置。共享引用取消/确认、动态端口匹配、深复制去重、重复 ID、跨图限制和原子撤销主要由自动测试覆盖，并非每一项都做了手工鼠标重演。

## Minecraft 实机

两轮均由 IDEA 的“1. Run Client”启动；以独立测试模组的本地动作/观察接口操作真实客户端与服务端，并保存游戏截图及状态，不是把单元测试当实机。

- 独立世界 ExperiencePlan0916 与 ExperiencePlan0916B；独立运行目录 .tooling/0.3.3.1/ExperienceClient。
- NPC 交互 → 30 字/秒台词（观察到 5/16 字）→ 点击补全仍停当前句 → 选项保留头像/完整上一句。
- 等待标题：播放期间任务列表为空，结束后任务 ACTIVE 并显示接取卡。
- 不等待标题：标题尚在淡入时任务已 ACTIVE，右侧同时出现接取卡。
- 0 字/秒完整显示；120 字/秒观察到 9/327 字。A 横图 → B 竖图仅一层且媒体引用改变；空画面后无残留图层。
- 通过服务端 debug emit_kill 产生击杀事件推进目标，再实际 NPC 交互完成下一目标；第二轮 XP 奖励夹具任务达到 SETTLED，出现完成卡。这里没有声称手工击杀了三只实体。
- 第一轮沿用原测试故事的未绑定物品奖励，目标完成但任务未结算；第二轮仅在隔离夹具改为 XP 奖励验证了结算，不修改用户故事。
- 热重载与退出重进后保留任务状态，没有把已有任务当新任务提示。失败通知、多卡上限、重复消息及到期主要由探针验证。没有进行音频设备听感验收。
- StoryPackages 仅有 Experience.dgrs；运行缓存生成在 Cache/StoryPackagesRuntime/<hash>。旧数据迁移/冲突场景由独立文件系统探针验证。

## 隔离与交付

用户项目 E:/Java/MinecraftMod/RPGProject/TestProject 和已有世界未作为写入测试对象。IDEA 临时注入脚本参数已恢复为测试前值；测试夹具与证据保留在 .tooling/0.3.3.1 和本目录 evidence。

- Studio：E:/Java/MinecraftMod/DarkGreyRPG/dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe
  - ProductVersion 0.3.3.1
  - 133847023 字节
  - SHA-256 49F388961EA13A9AFDBC1C8C4562B5B0DD622A857DDD55354FF11CAC5FC5D736
- 模组：E:/Java/MinecraftMod/DarkGreyRPG/dist/darkgrey_rpg-0.3.3.1.jar
  - 1236070 字节
  - SHA-256 75AD229F2D6580CCBECC9C21C87FE323251B2819535F27A6586694A28BAAC265

完整哈希另见 evidence/ExperienceArtifacts.json；日志见 evidence/ExperienceLogs；原生 UI 与游戏截图/状态见 evidence/live/Experience-*。
未提交或推送 GitHub，未将本轮有限验证认定为用户验收或 Release。
