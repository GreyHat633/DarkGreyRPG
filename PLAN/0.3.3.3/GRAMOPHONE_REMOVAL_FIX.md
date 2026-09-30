# 留声机拆除生命周期与金色范围修正（2026-09-24）

交付：dist/darkgrey_rpg-0.3.3.3.jar，1,701,765 bytes。
SHA-256：b5e90b0ae26ea360919e22eba160e61119c7274e6958ff565337601c320849d2。
客户端、隔离服务端和交付 JAR 字节一致；Studio 未更改。USER_ACCEPTED=NO，RELEASE_READY=NO。

## 修正
- 新增携带设备完整身份的 REMOVED 通知；拆除/失效从服务端索引删除后通知该维度玩家，立即停止该设备及其旧音源，而不是等待范围快照和淡出。
- 服务端周期检查索引中的设备是否仍对应已加载世界方块/方块实体；客户端播放与绘制均核对实际方块、有效性和实例身份，不加载远处区块。
- 设备释放最后一个未完成下载引用时取消下载；共享试听/其他设备持有的引用继续保留。
- 世界线框改为金黄色，每条边一粒缓慢移动的亮金色光点，共 12 粒；直接依附有效设备绘制，没有独立粒子实体、延迟发射队列或拆除残留。保持世界坐标、创作者权限、深度遮挡和 BGM 名称。

## 验证
- 离线构建、gramophone0332Probe、playerPreferences0332Probe 通过；新增 GramophoneRemovalProbe 验证旧身份移除不影响同位置的新身份、重复移除、待退场设备清理及最后引用的下载取消。
- 最终 JAR 在隔离服务器/客户端运行；代码来源由 removal-final-gold.txt 记录。
- removal-final-playing.txt：实际混音音源 active=1、playing=true、gain=1，播放位置持续推进。
- removal-final-harvest.txt：调用原版玩家 ItemInWorldManager.tryHarvestBlock，harvest=true。首次持创造模式剑的尝试 harvest=false，不计为成功；换为非剑物品后正常拆除。
- removal-final-stopped.txt：拆除后 active=0；removal-final-settled.txt：等待多轮同步后仍为 0。
- removal-final-absent.txt：tile=absent，audioDevices 从 4 降至 3。实际帧截图确认金色线框、光点与设备一起消失。
- 证据位于 evidence/native-ui/，截图位于其 screenshots/ 子目录；gold/absent 前后截图为本次拆除检查，不冒充连续移动验证。此前世界坐标连续验证保留在 WORLD_RANGE_VERIFIED.md。
- 临时附加代理只操作隔离测试世界和读取状态，未装入交付 JAR。实机音源状态验证不代替用户主观听感验收。

## 结论边界
原实现缺少即时移除协议和世界实体校验已确认；没有将“此前用户环境持续残留”的完整触发条件宣称为已复现。此次最终版本的正常挖掘、音源停止、线框消失有实机证据。测试客户端/服务器正常退出，未操作用户存档或 DPI。
