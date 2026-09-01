# 0.3.1.3 Manual Acceptance

候选程序：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`

状态：**RC — NO-GO / NEED_USER_VERIFICATION**。以下未勾选项目需要用户使用本轮权威 EXE 实际验收；勾选项为代理已用 Release EXE 操作，不等于 `USER_ACCEPTED`。

## 已执行

- [x] Start 四条条件字段名、术语、卡片边界与 Inspector 滚动。
- [x] Session/Task 从资源树拖入，ghost 带鼠标锚点。
- [x] 普通新增、单线重连、Ctrl bundle 的代表性 Flow 操作。
- [x] 删除 Session 资源同步清理 placement/wire；后续编辑保存不复活。
- [x] 只删除 Task placement 时 Task 资源文件保留。
- [x] 角色 before 重排预览与落盘顺序。
- [x] Story 125%/pan 与 Task 90%/pan 来回切换恢复。
- [x] Objective 切换物品收集/角色交互；角色拖入属性参数后节点数不变。
- [x] Light/Dark、1364×868 和 1700×980，实际 `GetDpiForWindow=119`。
- [x] 非法半径近场中文与 Problems 技术详情分层。
- [x] 焦点内有效中文 Ctrl+S 落盘；非法数字 Ctrl+S 不污染磁盘。

有效保存前后文件 SHA-256：`DD963563149B0C85538E672746886D05488B649D811EE06F8BFD7745B0F3C6B1`（作为非法输入保存时的稳定基线；非法输入后哈希未改变）。

## 用户需验收

- [ ] #1：用连续操作确认 Start 下拉选择后类型、参数、JSON、Undo/Redo 全链变化。
- [ ] #5：按住 Alt，肉眼确认 16×16 黑白剪刀与热点手感。
- [ ] #7：实际点击端口中心、+3/+4 DIP、标签和空白，确认完整命中矩阵。
- [ ] #9：完成 C7 的 Flow/Logic、多容量/单容量、新增/重连/Ctrl bundle/取消全矩阵。
- [ ] #10：逐一打开角色、角色组、物品、物品组 Inspector。
- [ ] #12：打开 palette，确认“逻辑输出”在“条件判断”之前。
- [ ] #15：补做实体击杀 ×10、物品/角色资源选择和 100-resource fixture；确认普通单次操作不冻结超过 1 秒。
- [ ] #17：用至少 5 个资源验证 before/after、取消、同/跨文件夹行为。
- [ ] #22：四类资源各做一次 DisplayName 修改、保存、关闭重开并抽查扩展字段不丢。

## DPI 说明

验收机本轮实际 DPI 为 119（约 124%）。未修改用户 Windows 会话去伪造 100%/150%；依 PLAN 使用实际 DPI 并明确记录。建议用户在自己的 100% 与 150% 环境各复查一次 Light/Dark。

