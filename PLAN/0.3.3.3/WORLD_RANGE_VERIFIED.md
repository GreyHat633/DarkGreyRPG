# 留声机世界线框修正与连续实机验证

## 实现
- 保留留声机方块实体渲染入口，直接平移引擎传入的方块渲染位置一次；十二条边使用局部坐标[-r,r+1]。明确选择、保存、恢复MODELVIEW，不重建世界投影，不使用朝向玩家的旋转。
- 开启深度测试、关闭深度写入，线框接受场景遮挡；临时关闭普通纹理和光照贴图，使蓝色不受上一物体纹理/亮度污染，绘制后恢复状态。
- 方块实体S35同步仅携带instance/radius/revision；范围显示不再读取DEVICES音频列表。区块加载、配置更新、移除及世界切换走方块生命周期，不强制加载区块，不扩大音频同步范围，不预取音频。
- 保留原有显示开关、创作者权限、实际方块范围边界及BGM文字。编辑预览转换为同一局部坐标后绘制。

## 定位证据与限制
旧诊断版4775个样本、修正版1168个样本均使用MODELVIEW(5888)和透视投影；两者矩阵随观察变化。旧版存在关闭深度测试、范围显示依赖音频近距列表两个确认的问题。本隔离环境未稳定复现用户描述的旧版完全锁定屏幕，不能把矩阵模式猜测说成已证实根因。修正版以下实际画面用于证明最终行为，取代此前两张传送截图的不足。

## 最终JAR验收
最终实机客户端与专服均加载SHA256 `67a5b7423c87ec594568ff63e1759af4bd1287181b13a697e4f595c6c5b5d9b2`，源码清理后重建哈希相同。临时测试agent控制隔离玩家的连续观察轨迹并调用游戏原生截图；不修改产品渲染结果。源码及JAR不含诊断类。

- fixture设备(-160,90,200)，半径2，八个红色标记方块在各轴边界组合(-162/-157,88/93,198/203)。
- `evidence/native-ui/range-final-flight.mp4`：173帧，连续环绕、原地转身、从8格退到20格并停留多轮同步、升降。可见线框与标记固定对应；转身时离开视野。视频为连续采样帧编码，播放时长近似，不作为帧率基准。
- `evidence/native-ui/range-final-crossing-render.mp4`：155帧，穿入立方体、穿出到另一侧、再返回，边界保持世界位置。
- `range-final-outside.txt`：audioDevices=0，同时tile元数据有效、radius=2、shown=true；对应远处画面仍可见线框。
- `range-final-radius-applied.txt` / `range-final-radius2-applied.txt`：通过生产SAVE网络入口修改2→3→2，收到实际服务端更新。
- `range-final-off-applied.txt`：shown=false；恢复后shown=true。
- `range-final-remove.txt` / `range-final-removed-state.txt`：正常setblock命令移除设备后tile=absent、audioDevices=0。
- `range-final-nether-stable.txt` / `range-final-nether-verified.txt`：正常服务器维度转移到-1并移离传送门后无设备及范围残留。
- 自动化：GRAMOPHONE_RANGE_CHUNK_METADATA、gramophone0332Probe、playerPreferences0332Probe及reobfJar通过；新增测试检查仅视觉字段同步、半径更新、非法半径拒绝。

失败/无效尝试保留：键鼠录制失焦只完成42帧；首个临时测试监听器因非public访问失败；tick阶段穿越录像朝向不足，未计验收，改为RenderTick连续轨迹；首次维度转移后立即又穿门返回，改为移离传送门再确认-1。测试失败均未作为通过证据。

## 交付
`dist/darkgrey_rpg-0.3.3.3.jar`，1700204字节，SHA256 `67a5b7423c87ec594568ff63e1759af4bd1287181b13a697e4f595c6c5b5d9b2`。Studio未更新；用户工作目录、系统DPI未改动。测试进程正常退出。未提交/push/Release。

USER_ACCEPTED=NO；RELEASE_READY=NO。QQ/网易云/本地听感人工验收及此前对话扫描式闪动的验证边界保持原状。
