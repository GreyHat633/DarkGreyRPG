# 2026-09-23 续作：排除系统 DPI 测试

- 用户明确要求跳过需要修改电脑 DPI 的测试；标记 USER_WAIVED，不修改系统 DPI。其余验收继续。
- CAS capability preflight：PROJECT_NOT_RESOLVED；分配记录接口返回项目未接管。由 MAIN 执行，未修改 CAS 配置。

## 已确认

- 上次中断前 `gramophone0332Probe` 已成功完成，27s。128 MiB 精确边界、超出 1 字节、30 分钟 TTL 两侧及 128 条目上限通过。受控元数据调用实际 tick 淘汰路径，不是实际下载或实机 30 分钟等待。
- `NetworkQueueRaceProbe` 在未启动游戏、未加载 DGR 事件处理器的独立 JVM 中调用旧版 Minecraft 的实际 `NetworkManager.flushOutboundQueue`，让两个消费者同时通过非空检查，稳定复现 `InboundHandlerTuplePacketListener.access$100` 空指针。与现场栈的位置一致（映射源码行号不同）。证明底层存在此竞争窗口；现场线程交错未采集，因此现场归因仍是推断，未声称已修复。没有修改底层网络代码。
- 初次网络探针的 EmbeddedChannel 缺少 handler 构造失败，修正后通过；该失败不是产品故障。

## 300 节点 / 300 分组布局

真实 WPF Window + UpdateLayout，预热 5 次后记录 30 次移动与布局调用。窗口在屏幕外；测量不含原生鼠标输入及合成器帧呈现，不能当作实机 FPS。

| 构建 | 分组数 | 中位 ms | P95 ms | 最大 ms |
|---|---:|---:|---:|---:|
| 修复前 | 0 | 11.177 | 30.462 | 41.092 |
| 修复前 | 300 | 63.600 | 70.893 | 71.811 |
| 首次优化后 | 0 | 11.354 | 26.516 | 30.879 |
| 首次优化后 | 300 | 18.191 | 35.053 | 42.486 |

小范围优化：复用组边框画刷、按 ID 查找已有组视图、预先索引画布子元素、直接使用遍历索引计算层级、FrameSnapshot 使用节点 ID 集合过滤成员。样式颜色和分组语义不变。

首次规模测试漏掉拖动结束的 CommitGroupMove，撤销断言失败；补齐实际事务步骤后通过。7 项分组测试通过。随后 77 项相关回归出现 1 项失败：重复节点 ID 导致组边界字典构造异常；已改为只接受可唯一识别的非空节点 ID，正在全量复验。

## 原生操作边界

Computer Use 初始化两次报 `failed to write kernel assets: 系统找不到指定的路径`，按用户已授权改用原生 UIA/Win32。测试仅操作 PID 55628 的隔离 Studio；PID 87708 不操作。IntegratedProject 复制为 ResumeProject 后用实际文件夹对话框打开；正斜杠路径被对话框拒绝，改用反斜杠后成功。测试期间 UIA 两次暂时没有取得窗口，重观后恢复；不能把这些尝试算成功操作。

## 尚未关闭

最终复验：WPF 全量 **605 PASS / 0 FAIL / 0 SKIP**（1m44s）。最终同场景无组中位 11.634ms、300 组中位 18.047ms（P95 51.395ms）；不以此承诺稳定 60FPS。格式化后的缓存与网络诊断探针均再次通过，见 `evidence/native-ui/0923-probes-final.log`。

新自包含 win-x64 Release 已复制到固定路径 `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe` 并核对源/目标哈希一致：ProductVersion 0.3.3.3，142429910 字节，SHA256 `bbe9d7b46ab6a1f35631bb427e5838e6ee1e87c75286c2054f1ebc0ddf93aa22`。JAR 未修改。

固定路径 EXE 以独立 `perf-settings.json` 启动 PID83756，实际打开 ResumeProject，进入 Session，搜索“组合”得到 3 项并定位子组。原生鼠标在组空白处拖动后保存，start/native_end 的 y 从 80 同时变为 -253.48745817917916，其他 3 个节点坐标不变；Ctrl+Z 后保存，5 个节点坐标完全恢复。证据 `0923-perf-group-move-comparison.json` 及 `0923-perf-*` 截图。未操作另一个用户 Studio PID87708。单击资源只选中 Inspector，双击才进入 Session，这两者未混同。

实机缓存长测、完整混音及主观听感、剩余编辑与游戏状态组合、合成器帧时间仍未齐备。USER_ACCEPTED=NO、CONSTRUCTION_COMPLETE=NO、RELEASE_READY=NO。没有提交或 push。

## 后续补齐的实际动作

- 新固定 EXE 的 ResumeProject：节点首句改为“续作节点编辑 0923”，提交后 UIA 同时读到节点和 Inspector 的相同值；再从 Inspector 改为“续作 Inspector 编辑 0923”，保存 JSON 确认。接着输入“续作折叠草稿 0923”，直接折叠再展开，两个入口保留相同文本；保存、Ctrl+Z、保存确认退回 Inspector 文本，Ctrl+Y、保存确认回到折叠草稿。没有修改正式综合包或用户项目。
- 正常退出 PID83756 后，从固定路径重新启动 PID65084；实际打开故事、搜索“续作折叠草稿”得到唯一 integrated_short 结果，进入后节点和 Inspector 均显示保存的文本。见 `0923-perf-reopen-line.png`；搜索定位与完全退出重开不是只查看 JSON。
- 缓存追加实际磁盘文件：4×32MiB闲置文件与64MiB占用文件。超预算后最旧文件经后台清理真实消失；将另一个闲置文件设为31分钟年龄后真实删除，60分钟年龄的占用文件保留。探针最后仅清理自己创建的确切文件，未递归删除。20s PASS，见 `0923-cache-real-files.log`。测试文件不是有效音频，不包含下载或解码性能结论；30分钟仍使用受控时间戳。
