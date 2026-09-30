# 留声机实际传输与缓存计数（2026-09-23）

系统 DPI 按用户要求不变。CAS preflight 仍 PROJECT_NOT_RESOLVED，MAIN 负责。所有运行均使用隔离 Client / ServerRelease，未操作用户存档。

## 修复前证据

旧正式 JAR `0e531da0...`，客户端 PID31632，正式生产 Forge 测试服务器 PID86648。一次性挂载临时方法入口计数；每次快照才写文件，无逐帧磁盘记录。结束后完整退出客户端，没有使用已知不可靠的诊断 remove/reinstall。

- 留声机位置 (-120,91,200)，半径16，开启播放和红石控制，当前无红石供电，正式设备未起播。
- 实际点击已保存本地音轨的试听：服务器下载计数1、GramophoneMediaInfo.inspect计数2、音源start1。
- 关窗重开再次试听：下载仍1、inspect仍2、start2、stop1，缓存复用正常；重复分析发生于第一次加载。
- 证据 `0923-cache-local-complete-counts.txt` / `0923-cache-local-reopen-counts.txt` 及对应原生截图。
- 早先带 yaw/pitch 的传送命令无效果：已查本地1.7.10原版源码，该命令只接受3/4坐标参数；不能误写为UI输入失败或产品故障。改用支持的坐标参数后，日志确认真实传送，再用鼠标转动视角。

## 修复

- GramophoneLocalClient 在完整下载、实际分析、大小/哈希校验后交付 GramophoneMediaInfo，而非仅交付 Path；消费端复用同一分析结果。
- 去掉消费端与传输进展无关的65秒总等待上限。传输层原有60秒无进展超时、上下文检查、取消与后台清理仍保留。
- 未改变网络包、DGRS、故事包媒体调度或身份。
- 新增实际尾块处理回归：有效MP3的最终块通过IO、分析和主线程调度后返回元数据；错误哈希拒绝；无进展超时仍释放登记。gramophone0332Probe 和 reobfJar 通过，见 `0923-transfer-fix-build.log`。

旧服务器通过游戏 stop 正常保存退出，旧客户端正常关闭；新正式 JAR 已复制到 dist、Client/mods 和 ServerRelease/mods，三者哈希相同。新服务器 PID40144、客户端 PID37832；服务器日志确认原综合故事包 generation 3563919a96d9 未改变。修复后的实机计数与大文件传输继续记录如下。

## 续作实测

- 新正式 JAR 原生首次试听旧本地音轨：download=1、inspect=1、start=1，修复前 inspect=2。证据 `0923-cache-fixed-local-complete-counts.txt`。
- 原生导入 4802394 字节 MP3，实际保存确认后上传完成。服务器保存文件与导入文件 SHA256 均为 c94ef1650398eed82bb2a8d59d9818cdd510c56a91b53f98c19af9ba849f496b。
- Studio 固定 EXE：Inspector 中编辑首句为“续作排序草稿 0923”，真实拖动至第二句，节点与 Inspector 同步，保存成功。Ctrl+Z 后保存：integrated_short 回到首句，草稿文本保留；profile_page_0 回第二句。证据 `0923-sort-after.json`、`0923-sort-undo-saved.json` 和截图。
- SOURCE_SNAPSHOT 已刷新当前记录源码哈希并纳入 GramophoneDownloadCompletionProbe；未提交或推送。
- 4.8MB 音轨断线重连后真实冷下载：06:31:31.732 点击，缓存文件06:31:32.903创建、06:32:46.709写完（实际传输约73.8秒）。06:32:59截图已就绪并播放13秒。文件哈希与服务器/输入一致；原65秒上限已被实际超越。
- 此次冷加载计数增量 download=1、inspect=1、start=1。关窗重开再次点击试听，download仍2、inspect仍3，仅start从2增至3，缓存没有重复下载或分析。计数基线已含前一旧音轨与导入分析，使用增量而非绝对数。证据 `0923-large-cold-before.txt`、`0923-large-during.txt`、`0923-large-cached-counts.txt`、`0923-large-file-evidence.json`。

## 在线来源实测计数

| 场景 | Online.download 累计 | Online.open 累计 | inspect 累计 | start/stop 累计 |
|---|---:|---:|---:|---:|
| 本地缓存重开后 | 0 | 0 | 3 | 3/2 |
| QQ 001ES52Q3qN0u7 首次试听 | 1 | 3 | 4 | 4/3 |
| 网易云 416892104 首次试听 | 2 | 5 | 5 | 5/4 |
| 换回 QQ 再试听 | 2 | 5 | 5 | 6/5 |
| 再换网易云试听 | 2 | 5 | 5 | 7/6 |
| 关闭 GUI | 2 | 5 | 5 | 7/7 |

每个平台首次新增一次下载和一次完整分析，返回已加载来源没有新增下载、open或分析。Online.open 为解析器方法入口计数，不等同于抓包统计的所有重定向请求。正式设备无红石供电，未播放；start/stop差归零只证明本场景试听生命周期配对，不替代多设备活跃源混音验收或人的听感。

QQ截图时长3:34、网易云5:47，均实际起播。首次尝试 Ctrl+A 没有清空旧游戏文本框，导致两个链接拼接，界面正确拒绝；该次没有新增download/inspect，不作为网易云失败样本。改用End与Backspace实际清空后，重新输入并成功播放。

当前上下文缓存为3份媒体共13787932字节（4802394+3426219+5559319）；另两个旧上下文各3426219字节单列在文件清单，没有掩盖或算入当前上下文。旧文件未达到reclaimCache的24小时异常退出回收门限；本轮未删历史文件。

本轮不声称完成真实30分钟空闲长测、多声源完整混音、跨服所有缓存边界或主观听音。整体剩余项仍以CURRENT_ACCEPTANCE_GAPS为准。

06:40:46 原生断开服务器，当前上下文3份缓存共13787932字节及Owner.lock自动回收，目录消失；未手动删除。06:41:14正常关闭客户端，声音系统正常退出，随后核对PID37832消失，临时计数器随JVM退出。证据0923-final-cleanup.json。服务器40144仍保留供后续隔离验收，Studio65084为本轮测试实例，未操作用户Studio87708。

产物复核：固定Studio ProductVersion 0.3.3.3、142429910字节、SHA256 bbe9d7b46ab6a1f35631bb427e5838e6ee1e87c75286c2054f1ebc0ddf93aa22；正式JAR1694413字节、SHA256 a9518ff07701a89a152649b416f93542260a0e19bb5243fe2f829cdfe3eceef6。身份见0923-final-artifact-verify.json。
