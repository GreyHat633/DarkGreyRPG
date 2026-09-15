# 0.3.3.1 故事端口与介绍说明空行修复

2026-09-15。用户反馈：旧故事有两个终止但外部没有输出；介绍说明下方多出空行。用户特别要求实际验证流程输入。

## 原因与修复

- 旧终止节点没有 `properties.port_id`，外部投影直接跳过。现在在读取、序列化和投影的脱离磁盘快照中补齐缺失身份，身份由稳定节点 ID 派生，与名称及列表位置无关；已有显式身份保留，缺失名称默认“终止”。读取不改写原文件。保存及导出使用同一身份；导出写入的是升级后的实际 JSON。
- 返回项目首页保留故事工作区时，图谱未刷新。现在用当前编辑快照刷新输入、输出及连线，并通知画布重建端口控件。仅刷新 ViewModel 不足以更新保留的画布控件，此问题已通过实机操作发现并修复。
- 新入口尚未保存时，外部连线先保存对应故事声明，再保存连线。切换入口类型后移除失效端口及其连线；内部撤销后恢复仍有效的已存连线。
- 介绍说明下方的空 `LogicEditorError` 原先仍占一行。现在无错误时折叠，有实际错误时显示。

## 验证

- Core Release：446 通过，1 个既有迁移样例测试跳过，0 失败。`.tooling/0.3.3.1/story-ports-tests/story-ports-core.trx`。
- WPF Release：532/532 通过。`.tooling/0.3.3.1/story-ports-tests/story-ports-wpf.trx`。
- 后续加强已连接入口移除断言，专项 2/2 通过：`story-ports-connected-removal.trx`。
- 新回归测试使用原始旧 JSON，覆盖两个终止、同一 Start 新增两个流程驱动、名称修改、稳定身份、未保存入口接线、移除已接线入口后的保存/重开、导出 ZIP 原始 JSON 和源文件不改写，以及空错误行显隐。

## 实机

从权威 dist EXE 启动，使用隔离 `.tooling/0.3.3.1/live-project`。没有修改用户工程。

1. 原有两个旧终止恢复两个输出：[截图](evidence/live/story-ports-restored.png)。当时唯一原有启动类型为角色交互，无 Flow 输入是预期。
2. 实际添加两条启动条件，选择“流程驱动”，命名为外部入口甲/乙。各自映射独立输入。使用测试源故事的两个输出分别拖线接入：[两条连线](evidence/live/story-ports-two-inputs-connected.png)。磁盘 `story_logic_graph.json` 保存两条不同 target_port_id 的 Flow 连接。
3. 将乙改为角色交互，返回首页立即只保留甲输入与甲连线：[类型修改](evidence/live/story-ports-one-input-after-type-change.png)。两个终止输出不变。
4. 返回故事执行 Ctrl+Z，再回首页，恢复乙入口及连线：[撤销恢复](evidence/live/story-ports-two-inputs-undo-restored.png)。
5. 关闭并重新启动权威 EXE，仍是两个输入、两个输出、两条连线：[重开结果](evidence/live/story-ports-two-inputs-reopened.png)。已查看截图并核对 UIA 端口身份。

上述是 Studio 编辑、投影、持久化及导出验证，本次未重新进行 Minecraft 服务端运行验证。USER_ACCEPTED=NO。

## 交付

- 路径：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`
- Windows x64 self-contained Release，ProductVersion `0.3.3.1`
- 大小：`141930198` 字节
- SHA-256：`64064979D30205B54ED2270E058DB0EC3D97DB461A47DC81DF288B5294755FC5`
- dist 与本次发布 candidate 哈希一致。
