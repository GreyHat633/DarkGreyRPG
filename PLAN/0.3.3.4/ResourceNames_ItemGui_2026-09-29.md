# Studio 资源名称与游戏物品 GUI 调整（0.3.3.4）

状态：实施并交付开发验证版本；USER_ACCEPTED=NO，RELEASE_READY=NO。没有提交或推送。下文区分自动化、原生实机与尚未覆盖项目，不能用本记录替代个人验收。

## 实施内容

- 台词移除 Enter 说明，设置改名为“详细设置”；容量放入正文框右下角，正文留出底部空间，估算说明改为悬停提示。动态内容入口仅保留台词工具栏，既有块仍可编辑。
- 重复配置为“重复设置”及四种模式：不可重复、无条件重复、冷却重复、定时重复；保留旧配置，模式切换一次撤销，条件字段沿用 220ms 动画。
- 动态块及名称引用显示为带主题强调色、半粗体的花括号块。手工花括号仍是普通文本。修复延迟创建正文框时首次拖入不能落下的问题，预览状态也显示强调样式。
- 角色/物品名称编辑文案统一为“资源名称”。角色、物品及组拖入叙事正文后保存 typed ID 引用；节点与 Inspector 共用组件，资源改名及撤销重做刷新名称。缺失引用显示标记并进入校验。
- C#/Java 动态结构新增 actor_name / item_name，保留原编码和旧文本兼容。扩展导出依赖检查、命名空间 ID 重写、任务简介引用、复制导入和选择端口标签同步。新内容需要本轮 Studio/MOD 配套使用。
- 游戏任务详情、奖励、提交候选与 HUD 使用共享实际物品格；奖励数量、扣除、经验分开表达，大数量完整显示；任务目标保留作者文字。候选组默认四格，“查看全部”打开可滚动列表，保留匹配规则提示和缺失原因。
- 悬停读取原生 ItemStack 提示。服务端只生成隔离的展示副本，单物品 8KiB 与报文已有边界保留；候选帧的展示扩展保留原身份、令牌与选择校验。旧响应缺展示数据时显示明确占位。
- 任务详情按文字/物品格测量；提交候选按内容测量行高和面板高度，窄窗口不裁掉“查看全部”。HUD 不添加鼠标交互。

## 自动化

- Core：466 通过、12 原有环境/专项跳过，0 失败。
- WPF：621 通过、1 原有显式跳过，0 失败；包含新增延迟正文拖入、第二句选区替换、资源改名历史、重复策略单次撤销等检查。性能专项另记。
- Java：assemble、inspection0334Probe、task0330Probe、task0331RuntimeProbe、canonicalTaskJournalProjectionProbe 通过；最后候选面板布局调整后再次 assemble 与 task0331RuntimeProbe 通过。
- Java 证据覆盖动态文本向量、展示副本只读、未绑定占位、旧/新候选帧兼容、截断/尾随数据拒绝、身份和令牌保留、库存不足/回滚/重放等。
- dotnet 输出含 NU1900（无法获取 NuGet 漏洞信息）及已有分析器警告；构建/测试成功，不将该警告写成漏洞扫描通过。

日志见 evidence/name-item/。不把自动化测试当作中文输入法或玩家真实操作证明。

## Windows 原生实机

仅使用 Windows UIA/Win32、CopyFromScreen；未使用 Computer Use/cua。工程为 `.tooling/0334/Native/Project` 和 `.tooling/0334/NameItemProject`，游戏为隔离 Server/Witness。未改用户截图中的正式项目。

| 覆盖 | 证据（evidence/native-ui） |
|---|---|
| 角色名称拖入并保存 ID | names-trace-drop.png；names-trace-saved.png；保存 Session 含 actor_name/Native0332:icon_actor |
| 第二句选区替换为物品组，第一句保持 | names-second-group.png；names-rename-redo-block.png；第二句仅含 item_name/Native0332:icon_group |
| 资源改名、撤销、重做投影 | names-rename-projected.png、names-rename-redo-block.png、names-rename-undo-block.png |
| 重开、深色主题、双入口容量与原子块 | final-line-visible.png（最终延迟正文修复前的布局证据，最后 EXE 操作另见补充记录） |
| 实际物品奖励、大数量、扣除、经验、未绑定原因 | icons-final-progress.png、icons-reward-tooltip.png、icons-narrow-missing.png |
| 物品组前四格、六候选展开、原生匹配提示 | icons-group-preview.png、icons-final-all.png、icons-group-native-tooltip.png |
| 游戏资源名解析 | 任务简介显示“验收商人”与物品/组资源名称；隔离 dgrs 由本轮 exporter 导出 |
| 游戏主题、窄窗口、HUD | icons-light-journal.png、icons-blue-journal.png、icons-narrow.png、icons-final-hud.png |
| 两候选展示、物品提示、展开后返回 | submit-chooser-open.png、submit-native-tooltip.png、submit-all-candidates.png、submit-return.png |
| 最终候选布局与窄窗口 | submit-final-groups.png、submit-final-narrow.png；两次相同任务不同 placement 的候选 |
| 实际提交个体物品 | submit-stone-applied.png：20→10，服务端 objective=COMPLETED、progress=10 |
| 实际提交物品组及奖励 | submit-group-applied.png：绿宝石16→6，石头17→26；fixture 三个奖励节点各发3个；check_b SETTLED，progress=10 |

最终组提交后另一 placement 因既有故事终止清理变为 CANCELLED_BY_STORY_TERMINATION，不能把它写成“仍可继续提交”或额外修改运行规则。服务端状态和库存读回保存在 evidence/name-item/。测试中的 debug 临时 placement 不代替真实故事完整流程验收。

最初 CustomNPC 实机入口出现第三方 `NoSuchFieldError: tempInvisIds`；未修改该模组，改用隔离的原版 Pig 角色组完成基本提交操作。名称拖入早期失败截图保留；重新启动及修复轻量预览输入路径后以最后构建验证为准，不删失败记录。

## 保留验证项

- C10 长文本/语音/会话历史整套组合：本轮未覆盖，仍 PARTIAL。
- C12 仅关闭本轮实际覆盖的候选显示、展开、选择以及个体/组扣除与奖励数量检查。多玩家交叉令牌、真实超时重放、离开距离/维度等完整实机矩阵仍保留；自动化通过不等于这些实机全部通过。
- 附魔/特殊模组物品提示的完整实机矩阵、全部极端窗口和主题组合、每一种叙事字段的逐一原生拖入仍未逐项覆盖。新引用解析和公共控件已有自动化及代表场景实机证据。
- 没有标记用户验收通过，没有把本轮 GUI 修改代替上述剩余项。

## 交付

权威 EXE：`E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`，自包含 Windows x64 Release，ProductVersion 0.3.3.4，142497494 字节，SHA-256 `384CE5B5CCBF514A3C816D91F9E3628259A9B03837506A76C7968EDB68E78D27`。

MOD：`E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.4.jar`，1776312 字节，SHA-256 `5E3DE05A480B9AB13F5EAC5A1750C63E09727FAF6383F60F5493C956322E6B2C`。

机器可读信息：`evidence/Delivery.json`。此前交付哈希保留在早先报告中，当前权威路径以上述最新信息为准。

## 最终权威 EXE 复核

2026-09-29 19:40 后从权威 dist EXE 启动（使用隔离 settings/project），核对：

- `delivered-inline-actor-drop.png`：实际从资源库拖角色到画布正文，Inspector 同步；落下后保存 actor_name ID。
- `delivered-ime-composing.png` / `delivered-ime-saved.png`：第二句名称块后真实微软中文输入法 nihao 候选、空格选“你好”；容量留在底部，不遮挡输入。`evidence/name-item/delivered-session.json` 为保存读回。
- `delivered-reopen-inspector.png`：关闭并重新启动同一权威 EXE，名称块与“你好”仍在第二句。
- `delivered-repeat-four-modes.png` / `delivered-repeat-once.png` / `delivered-repeat-none.png` / `delivered-repeat-cooldown.png`：旧定时配置正确映射，四种模式可显示，节点和 Inspector 同步。测试中一次坐标点击额外新增了启动条件，已撤销；最终保存读回确认原两个条件及 daily 23:59 配置恢复，不将该误点击算作产品行为。
- 两项组合/缩放性能专项均通过（2m28s），见 `evidence/name-item/name-item-final-performance.log`。因此本轮 WPF 普通检查621项及性能专项2项通过，另1项原有显式跳过。

本轮实机辅助进程已结束，隔离工程保留用于复查；没有恢复或覆盖任何正式工程。
