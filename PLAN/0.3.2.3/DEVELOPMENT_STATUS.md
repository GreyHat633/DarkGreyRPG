# DGR 0.3.2.3 开发与验收记录

状态：`AGENT_REAL_MACHINE_VERIFIED`；`0.3.2.3_USER_ACCEPTED=NO`。

基线：`c5ee16069ee938fd684af621de56597993c48c4e`；工作分支：`codex/0.3.2.3`。
本次交付本地 Java Mod，未提交、推送或创建 GitHub Release。仓库原有历史计划删除及其他无关修改保留。

## 交付

- 文件：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\darkgrey_rpg-0.3.2.3.jar`
- JAR 内 `mcmod.info` 版本：`0.3.2.3`
- 大小：`958840` 字节
- SHA-256：`B6A129AB9B272B2CC8BBD076EA4C9C4F14EF120400397EF8BD8023F76A7981DB`
- 构建输出与 dist 文件 SHA-256 一致；不含验收辅助 Mod 或 Probe 类。
- 机器可读清单：`ARTIFACT.json`；产品修改清单：`CHANGE_WHITELIST.txt`。

本版按计划冻结 Studio，未修改、升版或重新发布 Studio EXE。未改变编排资源、.dgrs 格式、Namespace/Origin、包归属规则、Story/Session/Task 核心运行时、任务推送缓存及对话生命周期。

## 实现

| 范围 | 最终行为 |
| --- | --- |
| M1–M3 配色与资源列表 | 共享 `DgrUiPalette`；按钮、Choice、Task、Copier、Nominator、检查标签统一灰色。资源名与类型/ID 同行；普通浏览不重复包来源，全局搜索保留包名和正确绑定范围。资源操作位于上方，宿主操作位于下方。 |
| M4 实体身份 | `实体指名` 遇占用 ID 弹出左转移/右取消；服务端原子迁移唯一映射、清理旧宿主直接身份并保留独立组。`ID释放` 不依赖旧实体存在；Group 释放同时处理直接绑定与类型组；`实体解绑` 只处理当前宿主。 |
| M5–M6 物品批量 | 两个命名特殊槽 `NOMINATE_SLOT` / `UNBIND_SLOT`，无关闭按钮，库存与快捷栏组成左侧整块。成功返还对应物品、刷新权威 revision、保持窗口/搜索/包/资源/滚动；失败保留槽内物品。Esc 正常关闭并返还两槽。 |
| M7–M10 物品操作与弹窗 | ItemID 唯一精确定义可原子转移；仅 Group 路径弹出左精准/右模糊。精确比较注册名、损伤和 NBT，忽略数量；模糊仅比较注册名。资源释放保留编排资源；解绑移除匹配 ItemID、精确组成员和模糊注册名规则。弹窗阻止下层点击并支持 Esc 返回。 |
| M11 结果与版本 | 新增操作/结果消息（19 SERVER、20 CLIENT），核对权限、包范围、目录 revision、物品/指名器 revision 及 NPC 身份 revision。返回 accepted/noop/stale_revision/transfer_required/invalid_resource/permission_denied 等明确结果。GUI 用 token/sequence 隔离旧窗口和旧响应，不依赖聊天推断成功。 |
| M12 自动检查 | 新增身份生命周期、原子拒绝、反向映射、孤儿释放、组清理、精确/模糊、NBT/数量、物品返还、持久化与配色/行布局守卫。更新既有双槽索引和网络注册数量检查。 |

NPC 的新增 revision 是当前服务器生命周期内的失效标记，不改变存档 schema。物品解绑作为一次 registry 变更只增加一次 revision。没有自动扫描/回收未加载实体 ID。

## 自动验证

- 生产修改前：54 项既有 JavaExec Probe 全部通过，`evidence/baseline.log`。
- 全量集成：`build`、54 项既有 Probe、新增 `nominator0323Probe` 全部通过，`evidence/full-regression-3.log`，1m41s。
- 之后只有界面绘制修正：物品弹窗关闭库存残留光照；Story chooser 的最后一个暖色文字常量改用共享灰色。前者重新 build + Nominator/容器/0.3.2.2 presentation Probe 通过（`final-render-build.log`），并重新实机跑完整物品流程。后者最终 build + 配色/身份 Probe + Story chooser codec Probe 通过（`final-build.log`）。
- 最终 Checkstyle、编译、测试、打包通过；Spotless 使用 `scripts/0323-client-format.gradle`，仅覆盖本版修改文件，保留冻结基线既有的全仓格式债务。不能将其表述为未限定范围的全仓 Spotless 通过。
- `scripts/verify-0323-freeze.ps1` 扩大检查到整个 Story/Session/Task、对话客户端生命周期及任务推送缓存相关冻结路径。最终输出保存在 `evidence/freeze-final.txt`。
- 产品代码 `git diff --check` 通过。

## 实机验证

环境：当前 Windows 10 主机，Minecraft 1.7.10、Forge 10.13.4.1614、Java 8、CustomNPC-Plus 1.11.1-fixed-v1。
隔离目录：`.tooling/0.3.2.3/live-client`，主要最终世界 `DGR0323Final`。测试包为此前验收包的独立副本，没有写入用户原世界或原始故事包。

辅助 Mod 只监听 `127.0.0.1:32361`，在实际游戏线程执行 GUI 点击/按键回调，经真实网络消息进入服务端，并记录状态；截图来自游戏帧缓冲。原生 Computer Use 截图两次报 `SetIsBorderRequired failed ... 0x80004002`，未将这些失败计为通过。这里不宣称物理鼠标全流程。

| Gate | 操作与结果 | 证据（evidence/） |
| --- | --- | --- |
| A / B | 对话 Choice、Task、Copier、两类指名器和弹窗为灰色；实体列表单行、类型/ID 为灰字。 | 01-final-entity-A、06、14、20、25、26、32、33 PNG |
| C 必须先过 | A 绑定 TarvenBoss 后实际移除；Adead=true、Aloaded=false，但 ID 仍占用。点击资源释放后唯一映射/旧个体元数据清除，资源仍在，再绑定 B 成功。 | orphan-host-proof.json、02–05 PNG、live-verification.jsonl |
| D / E | 占用 NPCID 弹窗先取消不变更，再确认迁移到 B。给 A/B 及类型加同一组后，实体解绑仅清 B；资源释放再清其他直接/类型绑定。 | 06–09 PNG、JSONL |
| F / G | 1100×740 与 640×480 下两槽/背包布局可用；多次物品绑定保持同一 windowId、搜索、包、资源与滚动，revision 连续刷新，成功后对应槽空。 | 10、11、13、15、16、20 PNG、JSONL |
| H | ItemID 从木棍定义转移到铁剑定义；取消时旧映射与目标槽保持，确认后映射改变且物品返还。 | 12–13 PNG、JSONL |
| I | keys 全局搜索同时出现 Consumer/Provider 来源；正确包范围绑定。Esc 取消模式弹窗保持槽与浏览状态；EXACT 不匹配另一损伤值，FUZZY 匹配另一损伤值。NBT 与数量边界由新增 Probe 覆盖。 | 14–16 PNG、JSONL |
| J / K | 解绑槽移除匹配 ItemID、精确组和模糊组规则，保留物品；ItemID/ItemGroup 释放均经过确认且资源仍可选择；NPCID/EntityGroup 释放已在 C/E 验证。 | 17–19 PNG、JSONL |
| 拒绝与物品安全 | 构造跨包资源请求不变更；过期 revision 拒绝并保留槽内物品，刷新后立即重试成功。弹窗覆盖槽位点击不穿透；Esc 关闭两槽返还；首次独立夹具中两把铁剑各一把、木棍16个守恒。 | 21–24 PNG、JSONL、容器 Probe |
| 冻结回归 | 活跃 Choice 上 Esc 打开原版暂停；背包覆盖不推进对话；真实死亡、重生与断线重连保留原节点。正常推进故事后 Task UI 从现有推送缓存显示 ACTIVE 击杀目标。 | 26–32 PNG、live-final.log、JSONL |

全量 GUI 请求/结果保存在 `live-verification.jsonl`；测试脚本与辅助 Mod 源码一并留作复验材料，但不进入交付 JAR。

实际运行的是同源 Forge 开发 JAR，交付为构建生成的正式重混淆 JAR。最后一处 Story chooser 灰色文字常量修正发生在末轮实机之后，已通过最终构建和新增配色/既有协议检查；没有把这一单色文字修正描述为额外一次实机操作。未测试外部独立多人服务器或穷举第三方 GUI。

## 失败尝试与修正

保留早期日志，不计入 PASS：测试夹具直接调用 Forge 注册器导致普通 Java 类加载器异常（改用受控测试注册）；两处新增源代码检查/导入规则与新接口不一致（修正后全量通过）；CustomNPC setDead 未实际移除（改用世界移除并验证 Aloaded=false）；一次并行重建运行中的开发 JAR 导致延迟类加载失败（改为构建与实机运行串行）；物品弹窗残留光照导致偏暗（修正并重跑物品实机）；部分测试脚本的状态时序/路径错误已保留在 JSONL/记录中。

## 完成状态

```text
JAVA_BUILD=PASS_WITH_EXPLICIT_SCOPED_SPOTLESS_INIT
NEW_0.3.2.3_PROBES=PASS
0.3.2.2_REGRESSION=PASS
B4_RUNTIME_REGRESSION=PASS
UI_SINGLE_GRAY_PALETTE=PASS
ENTITY_RESOURCE_ROW_LAYOUT=PASS
NPCID_ORPHAN_RELEASE=PASS
NPCID_TRANSFER=PASS
ENTITY_UNBIND=PASS
ITEM_BATCH_KEEP_OPEN=PASS
ITEMID_TRANSFER=PASS
ITEM_GROUP_EXACT_FUZZY=PASS
ITEM_UNBIND=PASS
ITEM_ID_RELEASE=PASS
REAL_MACHINE_NOMINATOR_GATES=PASS
STUDIO_DIFF=0
FROZEN_SCHEMA_DIFF=0
FROZEN_CANONICAL_RUNTIME_DIFF=0
0.3.2.3_USER_ACCEPTED=NO
```

用户个人验收仍待明确确认，不将代理实机验证、编译通过或本地交付等同于用户接受。
