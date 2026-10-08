# 0.4.0.0 技术合同

开工日期：2026-10-07。基线 `51582ae8fe8a71214f2306ecd08492129171cff5`，施工分支 `codex/0.4.0.0`。

## P1 冻结合同

| 边界 | 基线 | 本轮合同 |
|---|---|---|
| GraphResourceEnvelope / CanonicalGraphResource | schema 2 | schema 3，两端只读取 3。Task 目标与 Story give_item 的输入语义断代；Session 在 P4 同步退出旧 Choice 输出。 |
| 身份表示 | story-uid-v1 | 不变。持久字段为 `{story_uid, kind, local_id}`；内存为无损 typed key。 |
| kill_entity.entity | Actor 或 registry_name | 仅 Actor。 |
| collect_item.item / submit_item.item | Item / Item Group 或 registry_name | 仅 Item / Item Group。 |
| interact_actor.actor_id / submit_item.actor_id | Actor | 仅 Actor，内存构造同样检查 Kind。 |
| give_item | item_id 或 item + metadata | 仅 action_type + DGR 个体 Item 的 item_id + 整数 amount。正、负、零语义保持。 |
| 空目标 | 作者草稿及 dormant 目标 | 空串保留；非空损坏地址、错误 Kind、原生 ID 明确拒绝。 |

资源必须出现在所属 Story 的 owned/referenced 声明中；引用保留来源 owner，不将跨 Story 引用误判为本地资源。导出仍要求现行闭包。只转换 schema 声明的引用，不转换正文、描述或高级指令。

击杀事件仅从具体实体的 DGR Actor 身份生成，按地址去重；伤害归因、FakePlayer 排除、服务端线程与异常隔离保持。收集仍同步背包快照；提交及候选共享 Item/Group 匹配，保留 damage 附加条件和原子扣除。

P2 的 SavedData/网络和 P5 的包合同在各批源码修改前分别冻结；未修改合同不得预填为已完成。Actor、物品世界绑定、membership、媒体、Project 与布局不随 graph 机械递增。

## 拒绝与数据保护

只维护当前 reader。旧输入拒绝不自动迁移，不删线、不转换为空草稿、不回写空项目；旧世界数据的无写回隔离将在 P2 单独验证。旧 schema 的自动 fixture 应更新为当前格式，退休输入作为负向样本保留。

## P2 冻结合同

| 边界 | 基线 | 当前目标 |
|---|---|---|
| NominatorSavedData NBT | schema 3，含 type_groups | schema 4；根字段只含 schema_version、identity_format、revision、entities。旧版本、任何 type_groups（包括空列表）及未知字段拒绝。 |
| 实体身份 | 具体 UUID 绑定 + 类型规则追加 | 仅具体实体个体/多组；CNPC 唯一宿主检查保留。原生实体类型仅作为宿主诊断。 |
| 旧实体/物品写请求 | discriminator 8 / 9 | 删除类/handler/注册，编号留空。当前统一操作 19/20 保持；Open 10—13 保持。 |
| S2CNominatorEntityOpen | marker 0x44475236，含 typeGroups 列表 | marker 0x44475240，删除该列表；其余字段顺序保持，旧 marker 和尾随字节拒绝。其他消息布局不机械断代。 |
| 世界数据拒绝粒度 | MapStorage 可缓存读取失败的半空对象 | 隔离整份实体指名 SavedData；不改 NPC/Item 独立存储，不清玩家状态或世界。失败或未完成读取的实例不能查询、修改、标 dirty 或序列化。 |

MapStorage 专用 public String 构造先进入待读取状态；显式新数据使用无参构造。成功的 schema 4 事务读取解除该状态。读取失败保留原内存内容并隔离实例；get(MapStorage) 不把失败对象当成空成功，不自动新建覆盖。writeToNBT 在修改输出 NBT 之前检查状态，保证 MapStorage 的 FileOutputStream 打开之前拒绝。压缩读取失败也必须被待读取状态挡住。

实体操作必须在改 NPC 独立身份之前确认实体指名数据可用。真实实体解析遇到保存数据错误不返回已取得的半份身份；异常隔离由现役事件路径保持。物品统一操作不依赖实体指名 SavedData，失败响应仍携带关联 token/sequence，保留槽位及当前物品匹配。

不迁移旧 type_groups，不展开为具体实体，不保留第二 reader。仅对本机隔离测试文件执行 SavedData/MapStorage 保存验证，正式世界保持原样。

## P3 Journal 与命令合同

CanonicalJournalService 直接返回现役 CanonicalTaskJournalEntry；`/dgr task progress` 保持只读正文/目标输出，动态正文继续服务端展开。`/dgr task journal` 先复用当前 CanonicalTaskPresentationServer 强制推送，再打开已有 GuiCanonicalTaskScreen；不增加第二投影或菜单。

旧 Journal 的 3/4 discriminator 留空。当前仅客户端的打开消息使用新 34，marker `0x44475240` 加目标 dimension，严格拒绝 marker/尾随字节；客户端沿现行 scheduler 检查连接与维度后打开。当前缓存、候选/历史页面请求、分页预算与用户快捷键保持，其他网络编号不移动。

Command 构造只接 ProjectRepository、EditorSessionManager、三个 Canonical manager 和 StoryPackageLoader；旧 dialogue/quest 命令、帮助/补全和无生产职责的旧 manager 参数删除。现行 story/session/task/reload/debug/diagnostics/admin 权限保持。


## P4 Choice 输出合同（修改前冻结）

沿用 P1 两端 graph schema 3；不另开旧图读取器。Choice 的任意输出 Logic 端口被拒绝：Studio `graph.session.choice.output_logic.retired`，Runtime `session.choice.output_logic.retired`，定位节点/端口并保留作者输入。现行 Flow 输出、条件 Logic 输入（包括合法三字段无条件选项）、稳定 option_id、服务器可用性复核与选择历史保持。移除旧输出编辑/复制标签/呈现/求值；不改 Session 状态存档版本或抹除真实选择记录。


## P4 作者项目边界（CL-08）

Project 原有元信息/ProjectOrigin 保持；新建默认目录仅 actors 和 resources。现行 Canonical 文件布局、membership、媒体、布局/恢复及显式外部路径不移动。已有 stories/dialogues/quests 空目录保留；这些根目录中有 JSON 就拒绝，不通过旧 Serializer 判断或自动改写。ProjectSession 只持当前 Project 和共享 ActorRepository；Story/Session/Task/Item 作者服务使用 CanonicalProjectGraphStore。旧 Shell routes、旧 Dialogue/Quest draft、旧 Flow recovery 的业务 API 退出，已有恢复文件原样留存。当前 _retainedStoryWorkspaces、保存协调器、工作区历史与异步当前资源导入保持。

## P5 包合同（修改前冻结）

单 Story 的 Manifest schema_version 与 dgrs format_version 均由 2 改为 3；dgrs.g 根 format_version 同样为 3，成员仍是同一套单 Story 合同。Graph / story_schema_version 为 3，identity_format 保持 story-uid-v1。

required_resources 删除 dialogues / quests 字段、DTO、路径枚举与诊断分支；即便值为空也作为未知字段拒绝。保留 story、actors、items、item_groups、canonical_stories、canonical_memberships、sessions、tasks、media 与可选 story_logic_graph。当前单包必须只有一份 Story 和 membership，package_id 等于 story_id；只维护当前 reader，不迁移、不抹除未知数据。路径、重复字段、大小预算、闭包、媒体完整性、冲突/禁用、组包事务安装与既有 UID 进度保持。

原始 Manifest 明确提供身份、生产者、版本、资源列表根与必需 ID；各可选资源列表缺失仍按空列表处理。导出不再新建旧根目录；仅在用户指定的导出目标内清理由本 exporter 生成的旧目录，正式作者项目不受此清理影响。自动夹具更新后，由同一份 Studio 导出单包/组包同时通过 C# 与 Java 的正反验证。

## 2026-10-08 上传前六项修正（最终方案）

本地 Session Choice 正常保存和导出不再包含 `prompt`；现行 schema 3 中旧字段读取后忽略，打开不回写，外部引用不重序列化。Choice 选项正文、前置条件、不可选原因、option_id／port_id 合同保持。Runtime 步骤不再有 prompt 参数或访问器；既有 Choice 帧 text 固定空，网络字段、顺序和 discriminator 不变。

WorldSavedData 增加可选 `line_contexts` 缓存，按活动 transport 仅保留最近一份已解析台词的既有静默 LINE 帧，严格校验字节、类型和 transport；缺失时旧存档可读取。恢复按 LINE→CHOICE 顺序，LINE 保留原历史 epoch、使用当前演出状态和投影修订，不播放语音／画面，不改变真实 Choice 游标；终态缓存清理。旧 presentation_texts 的 prompt 槽兼容读取，但不再展示。

故事出口编辑改真实源节点 display_name；端口 ID、display_order、连线与运行语义不变，空名称拒绝，外部来源只读。列表只写已有 NavigationOrder／membership DisplayOrder，不新增资源文件格式。类型入口在候选生成和最终确认均校验 Kind、SourceStoryId 和来源资格；通用资源入口保持全部类型。
