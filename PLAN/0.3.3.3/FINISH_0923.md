> 当前状态（2026-09-23最终复核）：计划内开发者检查已闭环，人工验收待完成。以[PLAN_RECONCILIATION_0923.md](PLAN_RECONCILIATION_0923.md)、[DELIVERY_REPORT.md](DELIVERY_REPORT.md)和[CURRENT_ACCEPTANCE_GAPS.md](CURRENT_ACCEPTANCE_GAPS.md)为准。以下历史日志保留各自时间及产物边界，其中旧待办与旧哈希不代表当前状态。最近WPF全量605 PASS，新增帧采样单项PASS；Core原有12 SKIP不算通过。

# 0.3.3.3 持续收尾验收（2026-09-23）

用户要求继续直到结束；系统DPI豁免保持。用户回复QQ/网易云/本地主观听感尚未全部听过，保留人工待验。
CAS预检PROJECT_NOT_RESOLVED，record_repartition拒绝未接管项目；MAIN负责，没有创建或更改CAS方案。computer-use初始化再次因kernel assets路径不存在失败，沿用用户授权的Windows原生控制。

## 已完成的新证据

- 解析中换源：启动4.8MB本地下载后在完成前切到网易云，最终仅网易云起播；download(local)=1、online=1、inspect=1、start/stop=1/1；旧部分文件回收。0923-late-source-*。
- 真实空闲TTL：06:53:18.844关试听后客户端保持在线，07:22:38仍有5559319字节曲目，07:24:03（1844.7秒）仅剩Owner.lock；没有改时间戳、手动回收或离线。0923-ttl-idle-start.json / 0923-ttl-after30.json。
- 窄Inspector：固定EXE真实拖动右侧分隔条，长句换行、容量提示及滚动区域保留。0923-inspector-narrow.png。
- 压力项目为独立合成夹具，不替代正式同包E2E。300台词节点、298组、两个未分组节点；真实搜索Perf000/Perf299及定位成功。初次夹具错误保留了旧connections，被载入验证拒绝，修正为无连接后正常；不是产品故障。
- 对两个未分组节点发25次原生G keydown与一次keyup，只产生一个双成员组，298→299；一次Undo回298。Ctrl/Shift/Alt+G均不组合。Windows WM_CANCELMODE打断G拖动后保存，节点坐标及组关系恢复。0923-perf-repeat-* / modifiers / capture-cancel。
- 八次真实组拖动及Undo，窗口保持响应。进程I/O统计24.1秒内Read11184/Write6848字节；包含管道，不能当作磁盘读写证明。WPR FileIO+DesktopComposition启动被Windows拒绝（0x80070005），没有提权/改权限；文件级IO及合成器帧时间保留平台受限。
- 全UIA树遍历产生约896KB输出并显著增加采样时间；后续压力操作使用仅截图模式，未把自动化树遍历耗时算成产品帧时间。
- 640×480 GUI2，实际操作100/125/150%字号；150%HUD明确显示溢出行与任务菜单入口，历史窗口完整可访问；小设置窗口文字预览可滚动。960×640、1280×800有新三主题设置/HUD样本。不是所有主题字号分辨率的笛卡尔积。
- 主题/字号/历史窗口操作前后无TaskTrackerClient.accept/save计数；HUD缓存随字号重建。主线程只读快照notifications.cards/pending/seen均0，audio.active=0。未改变任务奖励权威逻辑。

## 本轮修复

第一人称手持静止后只露出喇叭角，底座与唱片落出视口。增加仅EQUIPPED_FIRST_PERSON的物品渲染入口，沿用同一模型，仅调整局部平移与0.72缩放。世界、第三人称、掉落物和物品栏走原路径。
限定Spotless、gramophone0332Probe、reobfJar通过（24s），最终微调再构建通过（7s）；640×480实际手持已完整显示喇叭、唱片及底座，见0923-hand-final-small.png。旧服务器正常stop保存各维度，旧诊断客户端完整退出。

## 后续实机与当前产物

- 最终JAR 1695931字节，SHA256 890fba817a5dd511693e0d09b6e7a6157a938ed12c7a003376deace094cec196；安装至隔离客户端/正式专服。通过真实牛猪击杀再次触发同包Session。
- 两个网易云正式设备共享一份5559319字节缓存，新增试听后ACTIVE=3，关试听仅移除试听源。start方法入口包含准备未完成时返回false的调用，不等于成功起播次数；实际音源数量按ACTIVE及channel标识核验。
- 两正式设备与Session背景音并存：背景音乐0/0.5/1均对应正确gain且channel不变；0档回环RMS/peak均0。两首相同曲目叠加的非零档回环peak达到1，不用RMS比值声称线性输出或主观音质。
- Master实测0.5/0/0.9966887，留声机gain同步、Session源gain保持1而引擎Master变化；0档回环为静音。最后点击位置并非严格1端点，保留真实数值，不写100%恢复。
- 英语US切换UK触发Minecraft资源重载；notifications.cards/pending=0、seen=3不变，Session epoch=1不变。资源重载中音源释放，返回后重新建立2正式设备与1Session背景源，不冒充无重启。0923-language-uk/return-state.txt。
- 自动播放设置暂停：1790121823037至1790121862285毫秒（39.248秒）设置保持打开，epoch=1、automatic=true、autoStarted=0；关闭后1790121865035仍epoch=1并开始新计时，1790121868235到epoch=2。设置autoWaitSeconds=3；这是采样边界约束，不声称逐帧精确3000ms。
- 组及成员重叠选择复制：独立300节点/298组夹具，清洁单次Ctrl+C/Ctrl+V后保存为301节点/299组，新ID唯一，仅一份成员。0923-overlap-clean-saved.json。首次组合SendKeys报错的尝试不计；保存夹具首次弹出的接口变更确认已处理。深复制资源另有自动回归，不以台词节点的共享actor引用声称深复制通过。

人工听音：用户明确回复尚未全部听过，保持MANUAL_PENDING、USER_ACCEPTED=NO。系统DPI=USER_WAIVED。WPR文件IO及合成器帧时间=PLATFORM_BLOCKED(0x80070005)。这些边界不合并为PASS。
- 依次移除两个临时测试设备，ACTIVE 2→1→0，剩余设备channel不变，Session背景音playing=true且epoch=4不变。首次坐标Y=92无块、命令失败不计；从服务端实际NBT确认Y=93后重试成功。
- 重叠复制一次Undo并保存恢复300节点/298组，见0923-overlap-clean-undo.json。
- 08:12正式专服正常stop保存三个维度；诊断客户端与GroupLiveProject测试Studio均正常退出，后续进程检查已不存在。未关闭用户其他Studio实例。
