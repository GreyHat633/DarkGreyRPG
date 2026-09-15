# 0.3.3.1 节点名称与标题格式

2026-09-15。按用户要求，原“公共端口显示名”统一为“名称”，输入提示为“输入名称”。同类节点重名提示同步使用“名称”。

节点标题共用格式统一为 `类型「名称」`，类型与括号之间没有空格。终止、结束、逻辑输入、逻辑输出使用已填写的名称；会话、任务、故事引用及执行类型等附加标题使用同一括号格式。名称本身包含的方括号不改写。自定义名称等于类型名时仍显示，例如 `终止「终止」`。

未填写附加名称时不显示空括号；原有不允许提交空白名称的校验保持不变。未修改资源 ID、端口 ID、连接或数据格式。

## 验证

- WPF Release 528/528 通过：`.tooling/0.3.3.1/names-tests/names.trx`。
- 新增测试遍历全部边界节点定义，检查改名后标题、同名标题、撤销/重做与非空校验；检查会话/任务引用的统一格式。
- 实际交付客户端，在隔离 `live-project` 的终止节点输入“故事完成”，离开输入框后标题更新为 `终止「故事完成」`，右侧“名称”输入框同步。
- Ctrl+S 后核对磁盘 properties.display_name 为“故事完成”，正常关闭并重新启动客户端，重新进入故事后标题与会话引用格式仍正确。
- [改名后](evidence/live/names-terminate.png)、[重启后](evidence/live/names-reopened.png)。USER_ACCEPTED=NO。

## 最新客户端（替代此前修复记录中的 EXE 哈希）

- `E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`
- Windows x64 self-contained Release；ProductVersion `0.3.3.1`
- 大小 `141925078` 字节
- SHA-256 `C3F363F54130EDBF0965C84242DEF184D30073D49314F1A5B592699A2903B549`
- 已核对 candidate 与权威交付路径 EXE 哈希一致。
