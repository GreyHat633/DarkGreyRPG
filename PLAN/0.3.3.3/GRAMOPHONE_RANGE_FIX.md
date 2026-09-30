> 此前两张传送截图验证不足，后续修正与连续实机证据以 WORLD_RANGE_VERIFIED.md 为准。

# 留声机范围与BGM名称修正

2026-09-23：范围绘制从 RenderWorldLast 的玩家坐标推算迁移到留声机 TileEntitySpecialRenderer，使用引擎传入的方块相对相机坐标。已保存范围与编辑预览共用同一绘制路径，边界仍为实际播放使用的方块坐标±半径（正向上界+1）。扩展渲染包围盒及距离覆盖最大半径，避免只按小模型裁剪。权限、维度和显示开关保持有效。设置名称改为 BGM。

验证：gramophone0332Probe、playerPreferences0332Probe、reobfJar PASS。隔离客户端和专服加载同一新版JAR；设备位于(-120,91,200)，范围开启；以普通服务端tp命令从(-113.5,94,203.5)移动到(-110.5,94,203.5)，保留同一朝向拍摄范围与世界参照物。证据：evidence/native-ui/range-tile-fixed-a.png、range-tile-fixed-b.png、range-move-3blocks.txt；设置页点击声音后截图 range-bgm-label.png 确认 BGM。

测试早期带yaw/pitch的tp命令不兼容1.7.10（result=0），未计通过；后续只使用xyz命令（result=1），临时客户端agent仅设置测试朝向。未改动用户项目或游戏目录。Windows截图接口仍报0x80004002，使用已授权原生截图。临时agent未打包产品。

当前JAR 1699461 bytes，SHA256 `0b371e95873edca3c4217b00744cf129b7a59bb830cf8b34e1734abbe6e27398`。Studio未更新。隔离测试进程已正常停止。USER_ACCEPTED=NO；RELEASE_READY=NO。
