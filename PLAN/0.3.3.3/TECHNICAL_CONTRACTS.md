> 本次用户临时审计新增17项问题已修复并验证；最新修复、产物及证据边界见[AUDIT1_FIXES.md](AUDIT1_FIXES.md)。以下为原计划记录，旧哈希／结论不替代本次审计。人工验收仍未通过。

# 0.3.3.3 技术校准记录

## 音量链：源码已核对，实际听音人工待验

- Minecraft 后端直接向既有 Paulscode 引擎创建 DGR streaming source。`build/rfg/minecraft-src/java/net/minecraft/client/audio/SoundManager.java` 第119、150行在初始化/设置变更时调用 setMasterVolume(MASTER)。单源只乘作者值 × DGR voice/music；不重复乘 MASTER，不读取 PLAYERS/MUSIC。
- Java Sound 后端不经 Minecraft 引擎；混音 gain = 设备/试听/包络值 × DGR music × Minecraft MASTER，乘一次。
- 保留旧 audio.voice/audio.sessionMusic；停止读取 audio.gramophone，正常保存时移除此键，保留其他设置。
- 自动等待采用1–10秒、0.5秒步长、默认3秒；自动开关只在当前客户端会话内，默认关闭。

## 对话

客户端测量采用实际 FontRenderer advance，显示段留源文字符范围；作者 page_id/服务端 epoch 不变。布局、字体、字号变化以当前段起始绝对偏移重排。

2026-09-21 原生普通字体核对：基准逻辑320×240、150%字号、有头像；正文横向80..296、纵向175..221；两侧引号各ceil(9×1.5)=14；wrap=floor((216−28)/1.5)=125，行高14，可用3行。RAW=375，SAFE=floor(RAW×0.9)=337，余量只扣一次。Java/Core共用 `schema/dialogue-capacity-profile.json` 的数值和样本。Unicode字宽及原生模式已核对；GUI Scale 1的基线可读性限制保留披露，见PLAN_RECONCILIATION_0923.md。

先前378/340遗漏了引号独立取整产生的差异，已纠正并加入几何回归。产品显示容量不是字数；超出只警告，允许编辑、保存、复制和导出。

## 留声机缓存

设备和已保存曲目试听按规范化source共享下载Future和GramophoneMediaInfo；本地未保存导入按内容hash单独保留。每个设备及试听持独立引用、播放器和时间轴；关试听不取消其他消费者，最后引用释放时取消未完成下载，已完成媒体留缓存。闲置预算128MiB、最多128条，预览元数据闲置30分钟；播放中文件由既有文件租用保护。IO队列有界、工作线程检查、回调受上下文与generation围栏约束。没有更改Story包媒体生命周期策略。

## 校准与验收边界

截至2026-09-23，计划内HUD视觉覆盖、复杂分组手势/性能、共享媒体多设备试听及运行时音量矩阵已补证，详见PLAN_RECONCILIATION_0923.md。用户主观听感及最终界面验收仍待完成；系统DPI测试由用户豁免。运行时数值、录音和截图均不代替人工接受。
