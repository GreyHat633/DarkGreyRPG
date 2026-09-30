> 本次用户临时审计新增17项问题已修复并验证；最新修复、产物及证据边界见[AUDIT1_FIXES.md](AUDIT1_FIXES.md)。以下为原计划记录，旧哈希／结论不替代本次审计。人工验收仍未通过。

# 0.3.3.3 原PLAN复核与收尾

2026-09-23。按用户要求以原 `DarkGreyRPG_0.3.3.3_Construction_PLAN.md` 为范围重新核对。系统DPI沿用用户豁免；三类音轨人工听感按用户答复待验。本文件是当前结论，历史记录保留其原日期/产物身份，不修改成最终版本的测试。

## 纠正此前扩大或过期的待办

| 之前的说法 | 原PLAN依据与本次处理 |
|---|---|
| 所有主题×字号×尺寸×GUI Scale必须穷举 | §12.4要求覆盖三主题、三字号、至少低中高三个窗口与不同GUI Scale，并记录配置；没有要求笛卡尔积。已有640×480、960×640、1280×800及不同GUI Scale样本满足覆盖。删除无边界的“其余布局组合”待办。 |
| 延迟回执必须再做完整人工网络故障场景 | DP-07属于§12.2自动/可重复矩阵。Dialogue0333Probe与CanonicalSessionClientModelProbe覆盖连续点击、awaiting围栏、重复帧、旧epoch、迟到前页；本轮重新运行通过。正常正式专服双端/重连另有实机证据。 |
| 资源深复制缺原生界面就不能关闭 | GP-18要求结构、ID和资源映射唯一，不强制每个分支都原生操作。ClipboardPlanTests实际落盘验证双引用只复制一次资源、新节点ID、Undo删除资源/成员关系、Redo/重开恢复；WPF605全量中已通过。组/成员重叠复制的原生301/299→Undo300/298另已补。 |
| 奖励异常仍需泛化追加 | TK-05已有真实满背包→恢复→重进幂等；本轮TaskReward0330Probe重新验证apply/checkpoint故障、journal恢复及累计收据，PASS。任务面板说明未发放并继续重试，不再把所有pending判成背包满；不扩展奖励Runtime。 |
| WPR权限拒绝意味着性能验收必须一直挂起 | §11.4/12.5要求实际测量，不指定WPR。保留WPR失败记录，但用可见WPF Rendering回调间隔、实际Win32拖动期间进程IO、既有排版/下载/缓存计数完成范围内测量。不再把GPU呈现/系统级ETW当新增交付门槛。 |
| 必须彻底确定旧NetworkManager NPE现场线程因果 | 现有隔离复现仅支持上游竞争窗口推断，不能写成现场根因已证明。本版不重写原版网络；保留风险，未将无限线程取证作为本版新增功能。此次最终JAR A→B→A成功。 |

## 37项及冻结边界核对

`REQUIREMENTS_TRACEABILITY.md`重新列出A01–A16、T01–T08、G01–G10、X01–X03，恰好37项。每项列实现落点、证据和人工边界，替换旧表中已经完成却仍写“待验证”的状态。

- 核对§10.4决策表：唯一显式组为目标、唯一已有成员组便捷加入、多个显式组保留为子组、祖先覆盖后代去重、空组清理及共同父级；对应GraphGroupOperations与Core/WPF组合回归。
- 核对§11.3正式调用：正文不再滚动、历史不再用shownText逐帧覆盖、音量不再乘Music/Players或独立gramophone偏好。历史/任务/选项滚动仍为合法保留路径。
- Story/Session/Task Runtime及CanonicalTaskPlayerTransactions相对当前基线无diff；新分屏仍属客户端展示，分组仍是编辑sidecar。没有新增网络Runtime、登录音乐服务或改变Story包媒体政策。
- 产物是未提交工作树构建，不能冒充新Git提交。大量历史目录删除及其他脏文件未纳入本轮修改、未恢复或发布。

## 补齐计划内缺口

### AU-04 多设备/试听/会话

隔离生产客户端同时两正式网易云设备、一独立试听、Session背景音乐。临时PlanHarnessAgent仅调用现有GUI入口和PlayerUiPreferences.setMusicVolume，未修改产品类或打包进JAR；试听按钮为原生点击。正常设置入口会关闭试听，因此明确将“四源同时调量”标成运行中集成夹具证据，而非虚构正常UI入口。

`plan-au04-before.txt`与`plan-au04-0/0.5/1.txt`：ACTIVE=3且Session pins=1；三条channel完全相同，音乐gain随偏好变化。独立JavaSound正确乘当时Master0.9966887，Session单源不重复乘Master。`plan-au04-closed.txt`关闭试听后ACTIVE=2，设备channel不变，Session继续。补测`plan-master-exact100.txt`得到Master=1.0、两设备gain=1.0、Sessiongain=1.0。早前0.9966887没有被重新命名为1.0。

### GR-05 范围上下文

真实服务器A主世界设备(-120,91,200)原生“显示范围”开启。原版portal从0进入-1后当前设备列表为空；返回0并回原设备，range=true恢复，见`plan-gr05-nether.txt`、`plan-gr05-restored-range.txt`及截图。门中会关闭聊天窗口，最初返回指令没有执行；使用临时服务端夹具执行普通tp命令离开门，冷却后再经portal返回，不将失败按键算成功。

最终JAR在A→B→A原生连接操作中，B上下文为25595且设备列表为空，A返回25594恢复同一实例range=true。B快照原.txt被同名截图助手窗口元数据覆盖；原工具stdout已观察的字段单独存入`plan-gr05-server-b-observed.json`，并有B实际截图/服务器连接日志佐证。没有假称被覆盖的.txt仍是原始诊断。

### GR GUI显示修复

四源场景发现范围模式把窗口底板降到0x70透明度，下层Session正文与状态/底栏重叠。初次尝试深度状态无效，已全部撤销该尝试；最终只让窗口使用原主题WINDOW_PANEL，范围线框仍显示在窗口外，未改变播放器、缓存、权限或服务器数据。

最终JAR原生复验`plan-final-editor-range.png`：范围线框仍在，窗口状态/底栏可读；退出/跨服/返回正常。限定Spotless与留声机探针PASS；最终颜色修改重混淆构建PASS（8s）。中间3b0064…构建不是最终产物。

### §12.5 性能

- 可见真实WPF窗口、300节点，0组与300组比较；每配置200次Rendering回调，移动100次，原台词折叠按钮5次，组成员保持。`plan-frame-0333.trx` 1 PASS。
- 0组移动：中位16.614ms、P95 33.076ms；300组移动：16.626ms、23.959ms。
- 0组折叠：中位15.803ms、P95 38.727ms；300组折叠：20.782ms、46.251ms。保留折叠长尾，未承诺恒定60FPS；这些是UI Rendering回调间隔，不是GPU present测量。
- 固定EXE的300节点项目，真实按住组标题，160次鼠标位置更新、10.153秒；全部进程Read/Write/Other字节及操作计数为0（`plan-native-drag-io.json`）。采样排除鼠标释放后正常提交；最后移动30像素，实际截图显示组移动，Undo回原位，证明不是只发输入而无状态变化。
- 既有逐字2828次draw额外measure0、5000字首次测量后1000帧额外0、主题/历史无任务accept/save、缓存下载/分析计数、192MiB回收、真实1844.7秒TTL继续有效。不是把单次模型时间冒充游戏帧率。

## 最终状态

ENGINEERING_IMPLEMENTED=YES
DEVELOPER_PLAN_CHECKS=COMPLETE
AUTOMATED_REGRESSION=PASS_WITH_RECORDED_SKIPS
E2E_SAME_DGRS=PASS
DEDICATED_SERVER_TWO_CLIENTS=PASS
DEVELOPER_VISUAL_REVIEW=PASS_WITH_DOCUMENTED_LIMITATIONS
AUDIO_LISTENING_CHECK=MANUAL_PENDING
USER_ACCEPTED=NO
CONSTRUCTION_COMPLETE=NO_UNTIL_MANUAL_ACCEPTANCE
RELEASE_READY=NO

Core原有12 SKIP继续保留，不算PASS。Studio WPF最近全量605 PASS，之后只新增可见帧测试并单项PASS，不写成又跑了606全量。最终JAR最后仅更改窗口底板；其他未变路径沿用明确列出的历史证据，不冒充所有旧测试都在最终哈希重跑。

保留的已知限制：GUI Scale1 Unicode可读性在无DGR基线亦复现；0%对话底板叠夜景可能低对比；300组折叠存在上述帧间隔长尾；旧跨服NetworkManager异常现场因果未证明。用户无需改系统DPI。剩余人为事项为QQ/网易云/本地的实际听感，以及最终界面/交互是否符合个人审计预期。
