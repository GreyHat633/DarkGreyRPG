# 开始节点卡片统一

- 范围：仅开始节点的启动条件，节点内编辑区和 Inspector 同步调整；保留此前奖励卡片成果。
- 标题显示启动条件总数；每条使用清晰灰色边框、主题背景、8 DIP 内边距、4 DIP 圆角、10 DIP 卡片间距；删除按钮显示“删除”，保留排序、字段绑定和自动化标识。
- 选择、结算、画面未改为卡片。
- 新增项目 skill：`.agents/skills/studio-node-ui/SKILL.md`，并在 AGENTS.md 中加入读取入口。适用条件为一个节点包含多个独立模块，每个模块包含多个要素；简单列表项不拆卡片。

## 验证

- WPF Release：534 通过，0 失败，0 跳过。TRX：`.tooling/0.3.3.1/start-cards-tests/start-cards.trx`。
- skill-creator quick_validate：通过（Python UTF-8 模式）。
- self-contained Windows x64 Release 发布成功，正式 EXE 与候选 SHA-256 一致。
- 正式客户端成功启动；实机视觉验收未完成：Computer Use 截图返回 `SetIsBorderRequired failed: 不支持此接口 (0x80004002)`，点击返回 `coordinate input geometry is unavailable`。不将测试或启动记录当作视觉验收。

## 正式交付文件

- 路径：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`
- ProductVersion：`0.3.3.1`
- 文件大小：`141931734` 字节
- SHA-256：`51F7CE4B2A6016C85E220DE382B2C3C540C7962D73F9FDA77DC6B76BD4062627`

USER_ACCEPTED=NO
