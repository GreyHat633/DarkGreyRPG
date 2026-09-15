# 奖励节点右键创建修复

2026-09-15。用户报告：任务图右键“添加节点 → 任务 → 奖励”没有创建节点。

原因：工厂生成一条默认物品奖励（尚未选择物品），创建服务调用严格节点校验，因空物品 ID 拒绝候选。之前奖励 Inspector 测试直接构造图，绕过了菜单和创建服务，未覆盖此失败。

修复：创建服务允许明确的空物品选择草稿；仅忽略该条目的 `.item` 空字符串错误。非法数量、错误类型、缺失字段等继续报错；严格奖励校验与导出校验保持不变。

验证：

- WPF 534/534 通过。新增测试实际触发“添加节点 → 任务 → 奖励”菜单 Click，验证已有一个奖励时能创建第二个、创建坐标、撤销/重做、编辑经验数量、保存重开；另测非法数量不会被草稿规则放过。
- Core 446 通过、1 个既有迁移样例跳过、0 失败。
- 实机使用权威 dist EXE 与隔离 live-project；实际右键展开三级菜单并点击“奖励”，创建新节点 `node_8ff48d4f53c145aa8b65b75de92d0835`。
- [创建后](evidence/live/reward-create-created.png)：旧奖励保留，新奖励显示并被选中。
- [编辑保存](evidence/live/reward-create-edited-saved.png)：选择铜币，数量 7，Ctrl+S。直接读取任务 JSON 确認物品为 `GreyHat_:CopperCoin`、数量为 7。
- [重开后](evidence/live/reward-create-reopened.png)：关闭并重开应用和任务，节点、物品及数量保留。已查看截图。USER_ACCEPTED=NO。

交付：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`，Windows x64 self-contained Release。

- ProductVersion：`0.3.3.1`
- 大小：`141931734` 字节
- SHA-256：`DCE3517D0B5B45B7FDF112F5F83081CF85A2F048A87123E3326A9550CC74FF63`
