# 物品数量原版风格与深度修正

USER_ACCEPTED=NO；RELEASE_READY=NO。未提交、未推送。

删除 ItemSlotStrip 数量黑色矩形背景。短数量使用与原版堆叠数量一致的白字阴影、右下角位置；绘制时禁用光照、深度测试和混合，结束恢复调用方 GL 属性，避免三维物品写入的深度遮挡数字。大数量旁置布局、实际物品数量、NBT 和发放逻辑未改变。

验证：离线 assemble 与 scoped Spotless 成功，见 evidence/item-count-overlay-build.log。使用 Windows 原生 UIA/Win32，在隔离服务器 UiFix0334 玩家任务界面检查石头数量 10、扣除数量 2、信标数量 5；数字无黑色背景，叠加于三维图标上方，信标原生悬停正常。证据：evidence/native-ui/item-overlay-journal.png、item-overlay-beacon-hover.png。本轮未穷举所有主题、缩放和模组物品；此前 C10/C12 待验范围继续保留。

交付 JAR：`E:/Java/MinecraftMod/DarkGreyRPG/dist/darkgrey_rpg-0.3.3.4.jar`

- 版本 0.3.3.4
- 大小 1776445 字节
- SHA-256 `9DF954343435B1753CEC34165A32CFE0434E3F548C96C9A5D7FAEF8B3DE38AE0`

本次仅修改 MOD 渲染代码，Studio 沿用上一轮已交付产物。正式用户项目与游戏存档未操作。
