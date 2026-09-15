# 奖励条目分组与数量

2026-09-15。用户反馈多条奖励字段连续堆叠，节点与 Inspector 都无法辨认条目边界。

两处共用 RewardEntriesEditor：每条奖励独立带边框、背景和间距；标题为“奖励 1”“奖励 2”…，删除按钮放到各自标题栏；总标题显示“奖励内容（共 N 条）”。条目删除后顺序编号重排，撤销后恢复。原字段编辑、数值拖动与错误提示保留；空错误提示不占行。没有改动奖励数据格式。

验证：WPF Release 534/534 通过。既有奖励编辑回归增加了首次编号、添加、删除后重排及撤销恢复断言。

实机使用权威 EXE 与隔离 live-project，选中含铜币的奖励，点击添加奖励形成物品、经验两条卡片。节点内和 Inspector 总数同步为 2。点击 Inspector“删除奖励 1”，两处总数同步为 1，剩余经验卡片编号为 1；Ctrl+Z 恢复两条。浅色切换时当前视图已转到故事，未完成奖励卡片浅色实机验证。

- [深色两条](evidence/live/reward-layout-two-dark.png)
- [删除后](evidence/live/reward-layout-removed.png)

截图已查看。节点为可平移的画布，较长节点可超出当前视口；Inspector 使用原有滚动区域。USER_ACCEPTED=NO。

交付路径：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`。

- Windows x64 self-contained Release；ProductVersion `0.3.3.1`
- 大小 `141931734` 字节
- SHA-256 `E0F230FF18FBF80D97215BD18C0FA778F7A7129DD182C1376F5D7DDC9078CB55`
