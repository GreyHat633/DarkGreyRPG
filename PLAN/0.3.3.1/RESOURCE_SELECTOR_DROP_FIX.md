# 节点角色、物品选择框拖放修复

## 原因及范围

原实现按整个节点处理资源拖放，未接入具体选择框：奖励没有旧节点级处理分支；开始只选择第一个角色交互条件；物品提交目标也未覆盖。Inspector 选择框没有独立拖放入口。

新增共享 `ResourceSelectorDrop.Kind` 行为，启用选择框原生 AllowDrop，使用资源库的现有数据格式，按稳定 ID 匹配当前下拉候选。沿用字段绑定及图编辑历史，精确修改命中的字段或模块。错误类型和不可用资源拒绝并阻止冒泡，避免画布回退误改另一字段。

当前 canonical 节点资源字段已逐一核查并接入，节点内与 Inspector 同步：

| 节点 | 字段 |
| --- | --- |
| 开始 | 每条角色交互启动条件的目标角色 |
| 台词 | 说话角色 |
| 执行：物品给予 | 物品 |
| 目标：实体击杀 | 目标角色 |
| 目标：角色交互 | 目标角色 |
| 目标：收集物品 | 目标物品 |
| 目标：物品提交 | 目标物品、提交对象 |
| 奖励 | 每条物品奖励的物品 |

其他当前节点没有角色、物品选择框。沿用各字段原有个体/资源组候选限制，不修改 schema 或运行时。

## 验证

- WPF Release：536 通过，0 失败，0 跳过。
- 新回归测试对上述 8 类节点/子类型的节点内与完整工作区控件触发真实 WPF Drop 路由事件，验证绑定保留、稳定 ID、图数据变化、单次撤销/重做及序列化重读。
- 开始两条条件、奖励两条条目：每次仅一个模块变化；拒绝错误类型、未解析/不在候选范围的资源，并验证不冒泡；禁用选择框拒绝赋值。
- 实机使用隔离副本 `.tooling/0.3.3.1/resource-drop-project`。从资源库拖“测试物品3”至奖励节点物品框，节点内与 Inspector 同步；再从资源库拖“铜币”至 Inspector，双向同步。撤销第二次拖放后保存，磁盘中该条 `item` 确认为 `GreyHat_:TEST`，数量仍为 7。
- 实机证据：[节点内拖入](evidence/live/resource-drop-inline-item.png)、[Inspector 拖入](evidence/live/resource-drop-inspector-item.png)。其他字段由上述 WPF 路由事件测试覆盖，不声称所有字段都逐一执行了实机鼠标拖动。
- 项目 skill 已加入角色、物品资源拖放规范；quick_validate 通过。

## 正式客户端

- 路径：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`
- ProductVersion：`0.3.3.1`
- 大小：`141935830` 字节
- SHA-256：`36C54DEE92AF2D21B53812DCDA8C0B9194FBC8F15F71EA2EDD523855170C079D`
- 已提升 self-contained Windows x64 Release，候选与正式文件哈希一致。

USER_ACCEPTED=NO
