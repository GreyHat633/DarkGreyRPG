# DGR 0.3.2.2 开发与验证记录

状态：`AGENT_REAL_MACHINE_VERIFIED`；`0.3.2.2_USER_ACCEPTED=NO`。

基线为 `0f1f002aba10a7e1ae12564d6fdda093d5389ef4`，工作分支为 `codex/0.3.2.2`。本次完成 Java 客户端体验修正，交付本地候选文件；没有创建 Release、提交或推送 Git。工作区原有的历史计划删除、配置等无关改动予以保留。

## 交付

- Mod：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\darkgrey_rpg-0.3.2.2.jar`，939437 字节。
- SHA-256：`4AD7A245BE0AB5218F214A80DEB0F878AE0E1E124B935EBF304AE00F312115AF`。
- 故事包文案修正版：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\0.3.2.2\story-packages\kill_slimes.dgrs`，6262 字节。
- 故事包 SHA-256：`3A81CD6E4CA9C8805E9A88D81DD629A9A9D015098277A214162B149A96B76B25`。
- 机器可读交付清单：`ARTIFACT.json`。JAR 内 `mcmod.info` 版本已验证为 `0.3.2.2`，未包含实机辅助 Mod 或 Probe 类。

Studio、编排格式、资源加载器、Canonical Session/Task/Story runtime 保持冻结；本次不发布或更新 Studio EXE。

## 完成的行为

| 模块 | 最终行为 |
| --- | --- |
| M1 对话 | Choice 使用深色暖色调按钮；LINE/NARRATION 左键任意位置继续；Choice 外部点击无效；等待期间不显示“等待服务器”，禁止重复提交。 |
| M2 界面层级 | 保留同一对话模型与界面状态，在其他 GUI 下方绘制；上层 GUI 独占输入；聊天按实际改键打开，关闭上层后恢复。 |
| M3 生命周期 | Esc 打开原版暂停菜单；原版死亡/重生界面可用；断线清理客户端呈现，重连由服务器重投影原 Session 节点。 |
| M4 任务同步 | 持久任务变更增加内存 generation，服务端 tick 合并变更并向对应玩家推送完整快照；客户端只读、深拷贝、revision 去旧，开界面只读缓存，无定时请求。旧连接的延迟回调丢弃。 |
| M5 目标切换 | 原包下一阶段类型/边正确，但描述沿用了击杀目标文案；独立修正故事包的一个 description，真实击杀后显示“与酒馆老板对话”。 |
| M6 任务界面 | 深色双栏、按内容调整面板高度；低分辨率上下布局；任务列表与详情独立滚动；同时展示多个 ACTIVE 目标，按稳定任务 ID 保留选择。 |
| M7 复制器 | 模板列表滚动，删除入口逐行对应，二次确认，滚动取消待确认；沿用服务器选择/删除校验。 |
| M8 物品指名器 | 保留上半区资源浏览；下半区背包/快捷栏组成连续区，指名槽和按钮组成右侧区；槽索引及服务端指名协议不变。 |
| M9 名称/检查器 | 编辑器、指名器、复制器、收纳箱、素体、检查器；ID 与 GroupID 按条目分行，实体使用 NPCID 标签。注册 ID 和绑定含义不变。 |

## 自动验证

53 项既有 JavaExec 回归在基线上通过，最终再执行这 53 项与新增 `presentation0322Probe`，共 54 项通过；`build` 同轮成功，用时 1m46s。之后仅扩充新增 Probe，补测 NARRATION 传输/动作/清理及错误列表元素类型，单独运行通过。最后一次补测 `compileJava UP-TO-DATE`，生产代码和已构建 JAR 未发生变化。

新增 Probe 覆盖：快照乱序/重复、输入输出深拷贝、目标替换、结算移除、世界切换清空、坏数据原子拒绝、持久变更 generation、常规/紧凑布局与点击区域、NARRATION 编解码和继续动作。

构建明确使用 `scripts/0322-client-format.gradle` 限定 Spotless 检查范围。基线的未限定 Spotless 在冻结的 `CanonicalGraphResourceLoader` 及相关旧测试上已有格式失败；本轮未修复这些冻结文件，不能把本次结果解释为“不加 init 参数的全仓格式检查通过”。编译、既有运行时回归、测试和打包仍实际执行。

执行方式（PowerShell，仓库根目录）：

```powershell
$env:JAVA_HOME='E:/Java/jdk-25.0.1'
$env:GRADLE_USER_HOME="$PWD/.gradle-user-home"
$env:TEMP="$PWD/.tmp"
$env:TMP=$env:TEMP
$probes=Get-Content .tooling/0.3.2.1/baseline-tasks.txt
./gradlew.bat spotlessApply build @probes creatorUxProbe creatorNetworkDiscriminatorProbe presentation0322Probe -I scripts/0322-client-format.gradle '-PdgrsPath=E:/Java/MinecraftMod/DarkGrey_RPG/.tooling/0.3.2.0_B2/real-minecraft-20260903/story-packages/kill_slimes.dgrs' --offline --no-daemon --no-configuration-cache --max-workers=2 --console=plain
./scripts/verify-0322-freeze.ps1
```

日志：`evidence/baseline.log`、`evidence/final-regression-1.log`、`evidence/final-probe-supplement.log`。`evidence/PROBE_TASKS.txt` 保存实际回归任务名。

冻结检查输出：`STUDIO_DIFF=0`、`FROZEN_SCHEMA_DIFF=0`、`FROZEN_CANONICAL_RUNTIME_DIFF=0`、`FREEZE_GUARD=PASS`；本次代码的 `git diff --check` 通过。

## 实机验证与证据边界

在当前 Windows 主机实际运行 Minecraft 1.7.10 / Forge 10.13.4.1614 和 CustomNPC-Plus 1.11.1-fixed-v1。测试使用隔离的 `.tooling/0.3.2.2/live-client` 世界和配置，未改动用户原世界/故事包。辅助 Mod 只绑定本机 `127.0.0.1:32261`，通过游戏线程驱动真实 GUI 回调、网络包和游戏事件；截图来自 Minecraft 帧缓冲。聊天改键还实际使用 OS 键盘注入验证。鼠标点击/滚动主要由游戏回调驱动，不宣称物理鼠标全流程。

| Gate | 实际操作与结果 | 证据 |
| --- | --- | --- |
| A 对话/Choice | LINE→LINE→CHOICE；同 tick 连点只推进一次；选项外点击不推进，选项点击及 END 正常。NARRATION 使用服务端测试传输帧后真实点击推进。 | `30-final-choice.png`、`44-final-narration-transport.png`、run-4/run-5、JSONL |
| B Esc | 活跃对话打开原版暂停菜单，返回游戏后节点/transport 保持。 | `33-final-pause.png` |
| C 改键聊天 | Chat 改为 Enter，OS 注入 Return 实际打开 GuiChat；关闭后恢复原帧。 | `31-final-remapped-chat-native-key.png`、JSONL |
| D 上层 GUI | Inventory、实际 GuiChest（测试库存）和任意测试 GuiScreen 覆盖对话；点击下层 Choice 坐标无动作；上层关闭恢复。新 Session 到达时不抢任务界面。 | `32-final-upper-choice-no-click.png`、`27-task-settled-upper-gui-preserved.png` |
| E 死亡 | 活跃 Choice 时服务器真实伤害致死，原版重生按钮可用，恢复相同节点。 | `08-death-choice.png`、`09-respawn-choice.png`、run-4 JSONL |
| F 重连 | LINE/Choice/任务结算后最终对话断开并重新进入，同一 Session 恢复；任务不回滚。 | `07-reconnected-choice.png`、28/29 截图、run-3/run-4 |
| G 即时任务 | 同步后以正常 I 绑定打开任务 UI，立刻读取现有 revision；击杀期间界面不关闭也即时刷新。 | run-3/run-4、JSONL |
| H 实际目标 | 使用 GreyHat_ 原包确认下一节点已 ACTIVE 但仍用“消灭史莱姆”；修正版由绑定 NPC 的正常交互触发 Story，真实生成/绑定并击杀 3 只 EntitySlime，自动切换到回访目标；再交互移除任务并进入最终对话。 | `M5-AUDIT-TRACE.md`、`26-real-kill-3.png`、run-2/run-3、JSONL |
| I 任务布局 | 1100×740 和 640×480 窗口检查；3 个任务、每项两个 ACTIVE 目标；紧凑列表滚动选择、详情和边界可用。 | `34-final-parallel-tasks.png`、`35-final-lowres-parallel-tasks.png` |
| J 复制器 | 18 个模板滚动，选中服务器索引 1；第一次删除不变更；滚动取消确认；再次二次确认后 18→17，原 Template 1 精确移除，Template 2 留在该索引。 | `39-final-copier-confirm.png`、`40-final-copier-deleted.png`、JSONL |
| K 指名器 | 640×480 槽位可见，真实点击背包物品到目标槽、选择资源、点击指名按钮；服务器确认 CopperCoin 绑定。 | `41-final-nominator-layout.png`、`42-final-nominator-bound.png`、run-4 |
| L 标签 | 实际物品 Tooltip 按序显示 ItemID CopperCoin、GroupID keys、GroupID tools；实体牌显示 NPCID 后接 GroupID；名称变更及注册映射通过既有 creator 回归。 | `46-final-item-multiple-labels-verified.png`、`47-final-entity-labels.png`、JSONL |

表内截图均在 `evidence` 目录。26/27 等 run-3 截图用于证明真实目标/结算行为，其任务面板当时尚未合入最终自适应高度；最终外观以 34/35 为准。

NARRATION 的限定：冻结的当前资源加载器拒绝独立 `narration` 编排节点，因此该项实机测试在活跃 LINE 的相同身份上，由服务端发送现有协议支持的 NARRATION 帧，然后点击并由权威 Session 处理继续动作。这证明该呈现/输入分支，不表示新增了编排节点类型。并发任务/多目标使用独立合成补充包，与 GreyHat_ 实际目标复现分开记录。

未运行外部独立多人服务器，也未穷举第三方 GUI。JSONL 保留所有尝试，包括错误命令、截图时序修正、测试脚本错误预期和失败调用；不将这些尝试计为通过。Windows 原生截图接口在恢复后仍不支持，已改用游戏帧缓冲；完整测试日志没有删去这些证据边界。测试客户端已正常退出。

## 实际故事包根因与修正范围

原始运行目录包 SHA-256 为 `82E289D8777C869631B0BA7F96985D2FE1CF0475C5396E1C11AE38C3C0928E03`，交付结束复核未改变。原包副本为 `evidence/original-kill_slimes.dgrs`。

`GreyHat_:KillSlimes` 的击杀节点 `node_cfc0a454a1ef4168890d4ce6fbb3e803` 完成后，`node_4feb6ff8c4944e078f9914cd808c9f17` 正确转成 ACTIVE；后者是 `interact_actor`、目标 `GreyHat_:TarvenBoss`、需求 1，但 description 也写成“消灭史莱姆”。因此看起来像未切换，实际 runtime 没有停留在第一目标。

修正版仅改变 ZIP 中一个文件的一个 JSON 属性：
`resources/canonical/tasks/x477265794861745f/x4b696c6c536c696d6573.json` 的 `/graph/nodes/2/properties/description`，从“消灭史莱姆”改为“与酒馆老板对话”。其他 ZIP 条目字节一致，未改边、节点 ID、目标、奖励或包结构。

本机未找到该 GreyHat_ 包的原始 Studio 编排项目，未擅自改当前打开的其他项目。审计文档截图没有包哈希，因此无法证明截图与此次原包逐字节一致；本轮结论以当前用户运行目录原包及其实机重放为证据。修正版是独立导出包；原作者项目以后重新导出时仍应同步该描述。正常包更新产生的 generation 生命周期继续沿用既有规则。

## 验收状态

```text
JAVA_BUILD=PASS_WITH_EXPLICIT_SCOPED_SPOTLESS_INIT
NEW_0.3.2.2_PROBES=PASS
0.3.2.1_REGRESSION=PASS
B4_RUNTIME_REGRESSION=PASS
DIALOGUE_LAYERING_GATES=PASS
SESSION_RECONNECT_GATE=PASS
TASK_PUSH_CACHE_GATE=PASS
TASK_REAL_OBJECTIVE_TRANSITION_GATE=PASS
REAL_MACHINE_UI_GATES=PASS_WITH_DOCUMENTED_FIXTURE_SCOPE
STUDIO_DIFF=0
FROZEN_SCHEMA_DIFF=0
FROZEN_CANONICAL_RUNTIME_DIFF=0
LOCAL_DELIVERY=VERIFIED
0.3.2.2_USER_ACCEPTED=NO
```

代理实机验证和本地交付完成不等于用户验收；待用户审阅该候选版后显式确认。
