# 0.3.3.4 UI 与 OP 调试体验修正

本记录对应用户确认的五项修正。版本保持 0.3.3.4；没有修改故事存档格式、动态内容编码或故事执行规则。未提交、推送或创建 Release。USER_ACCEPTED=NO，RELEASE_READY=NO。

## 实现

- 台词卡片采用奖励卡片的 `#727272` 普通边框，选中保留主题强调色。快捷说明精确为“Enter 新建下一句”。节点内与 Inspector 复用同一组件。
- 台词动态内容入口移动到 `＋`、`−` 右侧；最后实际编辑的正文、光标及选区决定目标。同节点双入口共享目标，切换节点、点击外部、删除目标页、卸载或切换资源后失效。无目标禁用。其他节点保留各自入口。
- 动态内容菜单按名称、等级、物品分区；名称和等级为整行按钮，帮助文字使用现有主题样式。物品下拉框保留名称与稳定 ID、拖入及类型检查；未选择时禁用插入。菜单支持外部点击、Esc、失去激活和插入成功关闭，移除取消按钮；资源拖放期间保留菜单，取消或拒绝拖放后关闭。
- 开始节点的“重复设置”及“可重复”移到参数卡片外；参数卡片灰色边框，继续复用 220 ms 可反向高度动画。
- OP 根命令补全包含 `debug`，仍检查现有权限等级 2；执行及查询权限检查未放宽。
- 查询响应增加可选 `summary`，保留 `details` 和原有报文大小限制。默认显示可读状态、等待说明、重复条件、资源问题；节点/任务目标使用可读名称，任务目标包含进度与状态。无名称或资源缺失明确提示。时间采用服务器时区的 `yyyy-MM-dd HH:mm:ss`；已过期的重复时间不显示成未来“下次可用”。原始 ID、等待枚举和逻辑值放在默认折叠的技术详情。旧响应没有 summary 时仍显示原 details。查询仍只读。

## 本轮自动化验证

| 验证 | 结果 | 证据 |
|---|---|---|
| WPF 常规回归（两个大图专项分开运行） | 618 通过，0 失败，1 跳过 | `evidence/UiOpWpfStandard.trx` |
| 300 节点移动、折叠，0/300 组合 | 1/1 通过 | `evidence/UiOpGroupIsolated.trx` |
| 300 组合布局、成员关系及撤销 | 1/1 通过 | `evidence/UiOpGroupScale.trx` |
| 动态引用、保存和依赖 | 2/2 通过 | `evidence/UiOpCoreReferences.trx` |
| 可读诊断、只读投影、玩家隔离、报文往返 | 通过 | `evidence/ui-op-runtime-final.log` |
| 磁盘坏数据与报文边界 | 通过 | 同上 |
| 过期/未来/重复/超时/重开窗口响应拒绝 | 通过，10000 次交错循环 | 同上 |

常规回归包括新增的第二句选区替换、跨节点目标失效测试和原有折叠/连线、资源拖放、Undo/Redo 回归。跳过项是需显式开启的 `FixedThreeHundredNodeWorkload` 基准，并非本轮功能失败。

诊断探针覆盖完成、冷却、不可重复、运行中等待条件、等待资源缺失、异常结束、离线数据来源、任务目标名称/1-of-3 进度，核对查询前后 NBT 不变。客户端关联探针不冒充 socket 或渲染验收。旧响应 fallback 另经生产代码检查。

初始全量运行暴露新焦点注册表跨 STA 访问，已改为线程局部注册表。随后混合全量两次在大图创建窗口阶段中断；保留 `UiOpFullObserved.trx`、`UiOpWpfFinal.trx` 和诊断日志。两个大图专项均在独立进程通过，常规回归另行通过，不把中断的混合运行改记为成功。

## Windows 原生实机验证

所有桌面操作使用 UIAutomationClient/UIAutomationTypes、Win32 键鼠及 CopyFromScreen。仅使用 `.tooling/0334/Native/Project`、Server/Client/Witness 隔离实例。没有调用 Computer Use/cua。原生工具规则已存在于项目 Studio UI skill 和 `windows-native-ui` skill，并继续遵守。

| 操作与读回 | 本轮证据（`evidence/native-ui/`） |
|---|---|
| 深/浅主题灰色边框、工具栏、菜单文字层级 | `uiop-final-picker.png`、`uiop-light-picker.png`、`uiop-dark-restored.png` |
| 画布滚轮缩小后卡片、菜单及浅色主题 | `uiop-zoom-pan.png`、`uiop-zoom-picker.png`、`uiop-zoom-light.png`；检查后恢复 100% 和深色主题 |
| 重复标题及复选框在灰色参数卡片外 | `uiop-conversation-open.png` |
| 第二句选区替换，只改第二句 | `uiop-second-menu.png`、`uiop-second-session.json` |
| 撤销、重做、保存，文件恢复为插入后的相同哈希 | `uiop-second-redo-saved.png`；SHA-256 `79C35C1BC57C5DCEFFA857DC1D0B569735B68D5FF827EC4BEDC60B3E29FB59C2` |
| 画布最后编辑、Inspector 工具栏插入到原光标 | `uiop-cross-entry-menu.png`、`uiop-cross-entry-session.json`：第二句为 name + level |
| 画布入口打开、Esc 关闭 | `uiop-inline-menu.png`、`uiop-inline-escape.png` |
| 物品真实拖入、插入保存；错误任务类型拖入拒绝 | `uiop-final-item-drop.png`、`uiop-inserted-session.json`、`uiop-wrongtype-drop.png`；拒绝后文件哈希不变 |
| 外部点击关闭且原控件点击继续生效 | `uiop-light-dismiss.png`：View 菜单正常展开；`uiop-outside-closed.png` |
| 中文 IME 候选、选词与保存，无意外新增页 | `uiop-ime-composition.png`、`uiop-ime-session.json`：2 页，第二句含“你好” |
| 关闭 EXE 后重开，动态块及中文保持；已有块修改 | `uiop-reload-collapse.png`、`uiop-existing-block-menu.png`、`uiop-existing-block-session.json` |
| OP 的 `/dgr deb` + Tab 补全 debug；非 OP 不补全且拒绝查询 | `uiop-tab-after.png`、`uiop-nonop-tab.png`、`uiop-nonop-query.png` |
| 可读诊断默认显示、调整窗口大小、展开技术详情 | `uiop-debug-expanded.png`、`uiop-debug-technical.png` |

Studio 最后一轮第二句、双入口、IME、保存重开操作使用最终 EXE。游戏实机操作使用同轮早先 JAR；之后仅补充目标说明/未开始状态/缺失名称提示，最终 JAR 已重新运行诊断探针。没有把这部分文字补充称为再次完成全部游戏实机覆盖。

隔离 Session 测试结束后恢复，SHA-256 为 `A05745055ECDBB977817A7B7B77014C272894EC363F516A5972427940CB173CF`，与测试前相同。游戏隔离实例已停服，OP 权限恢复。用户原有 Studio 进程未强制关闭。

## 交付与保留项

最新版自包含 Windows x64 Release 已更新到权威 `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`，JAR 更新到 `dist/darkgrey_rpg-0.3.3.4.jar` 及三个隔离实例。版本、字节数、完整 SHA-256 见 `evidence/Delivery.json` 和 `E2E_Artifacts.md`。

本修正不替代此前 C10 长文本／语音／会话历史组合、C12 任务提交候选的剩余实机验证；保留 PARTIAL。个人 UI 验收仍由用户完成，不将本次实施和测试标记为用户验收通过。
