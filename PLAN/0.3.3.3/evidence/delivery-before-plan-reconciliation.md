# 0.3.3.3 当前施工产物

2026-09-23。最新自包含 win-x64 Release Studio 和重混淆 JAR 已同步至固定路径，源/目标 SHA-256 一致。工程仍未满足全部完成条件，USER_ACCEPTED=NO、RELEASE_READY=NO、CONSTRUCTION_COMPLETE=NO。

| 产物 | 版本 | 字节 | SHA-256 |
|---|---|---:|---|
| `E:/Java/MinecraftMod/DarkGreyRPG/dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe` | ProductVersion 0.3.3.3 | 142429910 | `bbe9d7b46ab6a1f35631bb427e5838e6ee1e87c75286c2054f1ebc0ddf93aa22` |
| `E:/Java/MinecraftMod/DarkGreyRPG/dist/darkgrey_rpg-0.3.3.3.jar` | 0.3.3.3 | 1695931 | `890fba817a5dd511693e0d09b6e7a6157a938ed12c7a003376deace094cec196` |

构建基线 `702a096ddf7523867d545c02cb9d7bd915036d3b`，分支 `codex/0.3.3.3`，实现仍在未提交工作树。SOURCE_SNAPSHOT.json 记录已修改/新增源码的逐文件 SHA-256；不是一个新提交或 Release。没有 push 或上传二进制。

累计修复：嵌套组边界复用和祖先更新；G 与修饰键/框选冲突及组空白焦点；任务直连奖励/小屏计分板共存；指名器残余重影；留声机设备/试听共享媒体及失败重试。之前已修复超容量警告错误阻断编辑/导出，并完成最小布局容量375/337的普通字体原生核对。

最近全量：Core462 PASS/12 SKIP，WPF605 PASS；最新边界修改后的针对性6项PASS，最新留声机探针及重混淆构建PASS。详见TEST_RESULTS.md，不把旧全量/旧截图当作新代码实机证据。

原生同包文件 `evidence/native-ui/Native0333Long.dgrs` SHA-256：`c73417b1623ec8bee033d4d5c10d7c699f58dbae264001ec18fead17edb8a88b`。正式单客户端完成长句本地翻页/自动/历史。后续已补正常NPC任务链、头像Unicode、Session音频跨本地页、留声机红石及100草稿回收；完整剩余范围见 CURRENT_ACCEPTANCE_GAPS.md。

后续补测：最新正式JAR双客户端会话/历史/翻页/字号隔离已实际验证，见E2E_AND_VISUAL_REVIEW.md；其余多人和声音矩阵仍未关闭。


2026-09-22：新增空白台词导出前校验，空草稿仍可保存，旧输出受保护。9项导出测试、Core全量通过；最新EXE原生空白导出被拒绝且未生成文件。新增组合嵌套/拖动撤销/拆组/重启恢复、留声机范围与缓存操作。区域进入→会话正常链已通过正式JAR，包 Native0333RegionValid.dgrs（2890字节，952aa87df25e79f9879a2e6f243ce1be4804f4ff328500d3e5014781e44e8074）。详见 evidence/native-ui/0922-NATIVE-REVIEW.md。



最新任务修复：直连奖励筛选正式 objective.logic_status；任务菜单不再被 HUD 遮挡，小窗口仍可滚动访问全部目标。任务投影/追踪探针、构建以及当前正式 JAR 原生复验通过，证据见 0922-NATIVE-REVIEW.md。




2026-09-22 23:55：补齐指名器两处/复制器一处残余阴影绘制；相关偏好/指名器探针及正式重混淆构建通过，指名器浅色同位置原生对照通过。最新JAR 1694413字节 / a9518ff07701a89a152649b416f93542260a0e19bb5243fe2f829cdfe3eceef6。正式Studio导出NPC包→空手右键启动新任务→实际击杀→头像Session正常接续；会话中正常保存重进，历史保留，任务/XP/全部收据严格不变。证据与无效尝试边界见 evidence/native-ui/0922-NATIVE-REVIEW.md。人工听音及未完成矩阵仍保留待验，不宣称全计划完成。



2026-09-23 DPI豁免后的续作完成一轮：系统DPI按用户要求跳过；WPF全量605通过。修复300组移动布局开销（中位63.600→18.047ms）及重复节点ID边界计算异常；新固定Studio已发布并原生验证组搜索/拖动/保存/撤销和台词双入口编辑/草稿折叠/UndoRedo/完全退出重开。缓存受控策略及192MiB真实文件回收通过；上游NetworkManager竞争窗口隔离复现，但现场因果仍为推断。详见RESUME_0923.md。当前固定EXE 142429910字节，SHA256 bbe9d7b46ab6a1f35631bb427e5838e6ee1e87c75286c2054f1ebc0ddf93aa22；JAR不变。整个PLAN仍未全项关闭。



2026-09-23 最新收尾见 FINISH_0923.md：修复第一人称留声机裁切并在640×480复验；真实30分钟TTL、两设备/试听共享缓存、多设备与Session音量、语言无新Toast、设置暂停重新等待、300节点原生导航/重复键/修饰键/捕获取消/重叠复制均增加了有效证据。ARTIFACTS.json和SOURCE_SNAPSHOT.json已同步最终文件。主观听音按用户答复待验；系统DPI豁免；WPR细粒度性能取证受平台权限限制。

