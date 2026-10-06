# 0.3.3.6 开发验收

> 后续修正：Project 连线编辑已恢复，公开输出上下按钮已移除，左栏拖动反馈已改进。最新交付和验收见 [UIFollowupAcceptance.md](UIFollowupAcceptance.md)；本页保留此前记录。

> 2026-10-05 后续整改：最新测试、实机结果和交付哈希见 [UIReformAcceptance.md](UIReformAcceptance.md) 与 [Delivery.json](Delivery.json)。本页保留整改前的历史验收记录，原测试数量及二进制哈希不再代表当前 dist。

2026-10-05：开发施工与代理实机验收完成。DELIVERED=YES；USER_ACCEPTED=NO；RELEASE_READY=NO（未授权成品发布）。工作区未提交或推送。

## 自动验证

- Core 全量：491 PASS / 0 FAIL / 0 SKIP，`full-core-current-final-identity.trx`。
- WPF 全量：652 PASS / 0 FAIL / 1 SKIP，`full-wpf-current-contracts.trx`。既有 FixedThreeHundredNodeWorkload 跳过；未以增加 Ignore 隐藏失败。
- Java 完整构建、限定变更范围 Spotless、Checkstyle、测试和 CNPC 外部身份探针通过，`current-media-budget-trace-build.log`。
- 当前身份跨语言、成员语义指纹、Dynamic Content、SavedData/网络拒绝旧格式、媒体队列/LRU/共享租约与传输窗口探针见 Delivery.json。历史独立探针不冒充全量 JavaExec 执行。

## 真实操作与计划门槛

| 门槛 | 实际结果与证据（evidence/ 下） |
|---|---|
| E2E-A 身份与非空迁移 | 原生向非空目标 Copy；隐藏资源和节点 ID 冲突重映射；未使用资源、外部引用、动态文字和媒体保留；源/目标原资源不变；Undo/Redo；原生删除重复 Start 修复；保存重开、正式导出、实际游玩并完成。current-rich-copy-result.json / rich-copy-runtime-completed.json |
| E2E-B 自动组和来源闭包 | Flow/Logic 派生 Group；原生整体 Reference 三成员包；仅 NPC 引用导出仍为单 Story；拖动 X→只读来源公开端口后导出完整四成员闭包；整体 Import 新 UID 另有当前组导入证据。current-reference-closure-result.json / group-import-current.trx |
| E2E-C 管理器和冲突 | 正常、禁用、错误、双向冲突同时显示；实际 OP 撤权/非 OP 拒绝；双 OP 过期删除确认被拒并刷新；下载中制造重复12成员 Group，退休12 Story记录和11 Session，保留11 ERROR历史；无关 Task 继续；回收副本恢复资格但不复活实例。current-two-op-result.json / media-conflict-inflight-result.json |
| E2E-D 普通禁用 | 两玩家同组不同 Story 活动期间禁用，已有会话重连/正常完成，新区域入口拒绝；另做 Flow 正向对照，Enabled 可启动 B，Disabled 时 A 完成而 B 不新建。current-two-active-disable-result.json / current-flow-disable-result.json |
| E2E-E 代次和媒体 | 同组 A/C 活跃，只更新 B，A/C 会话、台词代次和媒体保持；部署单一确定文件。current-three-media-runtime-result.json |
| MED-01/02 | 定向到达与混合环有界；current-three-media-runtime-result.json / current-media-policy.log / media-budget-fixture.json |
| MED-03/04 | 真实12 Story、3张共享大 PNG：最多3下载/10槽；10项保护时额外排队；完成一项后队列3→2，保护实例不淘汰。media-budget-observation.json |
| MED-05/06 | 共享图像保持；独占锁模拟单媒体读取失败，其他单项可用而未全体 Ready；4次重试后恢复、三文件hash正确。media-budget-retry-result.json；最后租约释放/LRU另由政策探针覆盖 |
| MED-07/08 | 管理器/只读图不触发剧情预热；成员更新与真实下载中断线/冲突后无异步复活。media-disconnect-inflight-result.json / media-conflict-inflight-result.json |
| Task/存档回归 | 专用服务器 XP 0→7，重启仍7；单人集成服务器新 UID XP 7→14，重开仍14，不重放奖励。current-task-runtime-result.json / current-integrated-runtime-result.json |

问题导航原生双击定位节点和 Inspector；不宣称字段键盘焦点。音频具有实际下载/校验/播放请求证据，不宣称人耳听音验收。上述为代理实际操作，不代替用户本人验收。

## 权威交付读回

`dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`：self-contained win-x64 Release，ProductVersion **0.3.3.6**，**204288 字节**。

SHA-256：`8e8bf253b16b1516616d890b8bd604a907cf9d607469e3785d4ab113a5b3b20c`。

Studio DLL：`8169be9193c428d1073b2ed6f631b7dc190b0323b72a59fe2955dc8a708f4ba9`。Core DLL：`77e3673a48dde35b5c3b7eb80d278ac8746d0a238690e5b8907e3f4846449acd`。

部署前后 Data 2454 个文件路径/大小/hash一致。后续实机测试工程正常新增，未覆盖个人 Data。原生测试使用权威副本。Runtime JAR：1949038 字节，SHA-256 `be3c49d863ed381784bebcd6ba9e1aa09b7bce1c9edc6b8a44b1d751b4801cd9`。最终读回见 `final-artifact-readback.json`；测试包路径、成员和校验值见 Delivery.json。

磁盘不足时仅将已确认的历史/暂存副本和隔离测试缓存送入回收站，保留项目/证据；未清空回收站。未创建成品 artifacts/DGR0.3.3.6、GitHub Release 或标签。
