# 0.3.3.4 对话头像自适应与旧任务回顾

USER_ACCEPTED=NO，RELEASE_READY=NO。仅更新 MOD；Studio 无改动。不提交、不推送。

## 实现

- 去掉头像 64 GUI 单位的固定上限。使用左侧正文区可用高度的约 90%，宽度不超过面板内部宽度的 22%，正方形框垂直居中。横竖图片在框内等比例居中，缺图加载占位与边框使用相同坐标。前景与后台对话共用该布局。
- NPC 名字仍居中于头像列，保留主题强调色、粗体、长名字悬停全文和完整分割线。正文起点随头像宽度调整。
- 320×240 标准档头像仍为 48 GUI 单位，正文宽度、行数及容量配置不变；其他尺寸沿现有分页机制重排，不增加服务端翻页、音频或运行操作。
- 完成记录有真实目标快照时优先显示快照；原有同激活轮、结算时间和结果匹配的运行存档补回机制保留。目标为空且无可恢复运行存档时，按明确的 `task_resource_id` 或旧历史 ID 中的四段长度编码提取稳定任务 ID，不按标题关联。
- 新增可选 `content_source`、`reference_description` 和独立 `reference_objectives` 响应字段。参考只存在于脱离存档的响应副本，真实次数与 `objectives` 不变。参考目标没有 `current`、完成勾选、提交指令或虚构进度。
- 页面标明“任务内容(当前故事包)”，列出当前简介、要求与“奖励参考”。使用现有物品格、数量和原生提示，不声称奖励已发放。资源缺失时保留标题、次数及既有简介，仅追加简短缺失说明；玩家页面不显示内部结果端口。
- 1.7.10 原版 `FontRenderer.renderUnicodeChar` 对全角左括号字形宽度的 byte 无符号移位存在符号扩展问题，会把后续文字绘制位置移到屏外。因此来源标签使用半角括号；未修改全局字体或用户编写文本。
- 历史 schema 仍为 1，新结算记录兼容追加稳定资源 ID；CreatorSnapshot 原有压缩 1 MB／解压跟踪 2 MB 边界、物品显示大小限制、发放和匹配逻辑保持原值。

## 自动验证

assemble 与以下探针通过：taskHistoryReferenceProbe、canonicalTaskEventPersistenceProbe、canonicalTaskJournalProjectionProbe、canonicalTaskJournalIntegrationProbe、canonicalSessionClientModelProbe、dialogue0333Probe。

覆盖标准容量几何不变、头像范围与中心、取消固定上限、分页重排与布局缓存、历史快照优先、旧 ID 解码、异常 ID 拒绝、改名／修改后稳定 ID 查询、缺失资源、参考／历史字段分离、真实次数不变、NBT 输入不可变、报文往返、原有匹配恢复与结算去重。实际复制的旧记录探针确认次数 2、历史目标为空、运行实例为空，解析同一故事包后得到两个参考目标，读取前后记录保持字节一致。

日志位于 `evidence/portrait-legacy/`。最终 assemble 的 compileJava、jar、reobfJar 均为 UP-TO-DATE，交付与实机使用的生产构建一致。

## 原生实机证据

全程使用 windows-native-ui 技能的 Win32／UIA 输入与屏幕截图，没有使用 Computer Use／cua。测试 JVM、世界与故事包均位于 `.tooling/0334`。

| 场景 | 结果 | evidence/native-ui 截图 |
| --- | --- | --- |
| 普通窗口与长名字 | 头像框居中，名字截断并可悬停全文 | portrait-dark-centered.png、portrait-long-name-tooltip.png |
| 无头像／匿名 | 不保留空头像列，正文与分割线正常 | portrait-no-avatar.png、portrait-new-anonymous.png |
| GUI 缩放 1，竖图 | 大对话框头像随高度增大，图像居中 | portrait-scale1-tall-stable.png、portrait-white-tall.png |
| 横图与缩小窗口 | 正方形框、横图等比例居中，文字从框右侧开始 | portrait-white-wide-960.png、portrait-azure-wide-960.png |
| GUI 缩放 4，320×240 档 | 头像 48 GUI 单位，长台词实际分页 | portrait-standard-tall-clean.png、portrait-standard-wide-page1.png、portrait-standard-wide-page2.png |
| 分页中缩小窗口 | 按现有来源锚点重新分页，可能重复当前锚点所在新页的前文；不丢正文、不提前推进服务端 | portrait-wide-resize-reflow.png |
| 前景／后台共用布局 | 设置窗口后台头像位置一致 | portrait-settings-underlay.png、portrait-scale2-underlay.png |
| 真实记录的单机隔离副本，重启后 | 次数 2；“消灭史莱姆，要求 3 只”与“与酒馆老板对话”；完整来源标签与奖励参考 | legacy-final-readable.png、legacy-final-reward.png |
| 独立服务器配套客户端 | 同样显示次数 2 和两个参考目标；较短窗口可滚动到奖励 | legacy-dedicated-readable-valid.png、legacy-dedicated-reward-scroll.png、legacy-dedicated-tooltip.png |
| 隔离服务器临时移走任务包并 reload | 保留标题、次数；显示简短缺失说明；重新安装后参考恢复 | legacy-missing-resource.png、legacy-dedicated-restored.png |

单机直接复制正式世界并沿用原玩家 UUID，未改变历史或任务数据。其历史文件及空任务实例文件在多次查询和 JVM 重启后仍与原始副本逐字节相同。正式历史、任务数据及 `.dgrs` 的前后 SHA-256 均匹配，见 `evidence/portrait-legacy/read-only-proof.json`。

独立服务器使用另一个世界副本。因 offline-mode 服务器按玩家名生成 UUID，仅在该隔离副本重映射历史的 player 与 id/group 的 UUID 段，次数、目标、标题、结算等内容未修改；见 dedicated-identity-map.json。该步骤不涉及正式存档。

退出测试 JVM 后恢复八项原测试配置／MOD／包，哈希逐项匹配，见 restore-proof.json。保留隔离世界和测试输入作复查。

## 覆盖边界

加载占位与实际图片使用同一布局坐标，几何探针通过；没有人工延长 IO 来抓拍持续加载占位。三主题、不同尺寸和实际 GUI 缩放 1／2／4已有样本，未声称穷尽所有组合。新历史与匹配恢复本轮为自动回归，原有实机证据仍保留。

之前 C10 长文本／语音／会话历史组合、C12 剩余提交候选边界、媒体公网／多人压力与严格帧分布等未覆盖项继续开放，本轮不替代这些验证。不标记用户验收通过。

## 交付

- 路径：`E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.4.jar`
- mcmod.info 版本：`0.3.3.4`
- 大小：`1,793,662` 字节
- SHA-256：`BE5C57EA6BA82AF9BA7CC4EB34D156F3427EEDF607A5337E1210FD18A3461F48`
- 客户端和服务端配套使用同一 JAR。Studio 权威 EXE 未更改。
- CAS 预检／所有权记录均返回调度器已暂停，Main 完成跨模块实现、复查及交付。
