# 执行类型默认选中修复

新建执行节点默认使用列表第一项“物品给予”；开启高级选项选择“指令执行”；关闭选择“物品给予”，不再恢复先前原生类型。类型参数缓存及撤销机制沿用现有实现。

根因：WPF 更换 ItemsSource 时清除选择，而绑定的已缓存选中值未重新应用。新增 ActionTypeSelector，以独立的 ActionType 绑定保存身份，在列表更换后重新同步选中项，忽略列表重建引起的空选择。节点内与 Inspector 共用此控件。

- WPF：537 通过，0 失败；新增测试覆盖新建、两处控件、6 轮开关、撤销和重做，并核对选中项与图数据一致。
- 核心：444 通过，0 失败，3 跳过。旧消息发送场景测试改为显式初始化消息类型；工厂默认值测试改为第一项物品给予。
- 实机隔离测试项目：从经验给予开启高级选项，两处显示指令执行；从 Inspector 关闭，两处显示物品给予。截图：`evidence/live/action-default-advanced.png`、`evidence/live/action-default-native.png`。新建默认值由 WPF 工厂/节点添加测试覆盖。
- self-contained Windows x64 Release 已更新到 `E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`，与候选哈希一致。
- ProductVersion：0.3.3.1；大小：141939926 字节。
- SHA-256：`7A508E519F2466910A92379DA51C9E7E3E1AEDC3685E519131CBB0BE289517AC`

USER_ACCEPTED=NO
