# 分句标题交互修正（2026-09-17）

根据用户第二次反馈，否决“折叠箭头与删除减号并排置于右侧”的第一版。

最终布局：左侧拖拽把手、独立 ⋯ 操作菜单、可点击的标题文字和空白、右侧细线折叠箭头。右侧不再放直接删除按钮；删除本句位于 ⋯ 弹出菜单，需要先展开菜单再选择。最后一句依旧不可删除。节点内及 Inspector 共用 LinePagesEditor。

原生 Windows UI 验证：
- header-menu-03-collapse：鼠标点击标题空白区域，第一句收起，节点内和 Inspector 同步。
- header-menu-04-expand：点击标题展开第一句。
- header-menu-05-actions：点击左侧 ⋯ 打开所属句子的菜单，正文没有因此折叠。
- header-menu-06-deleted / 07-undo：选择删除第 1 句后剩余一条；Ctrl+Z 恢复原来的两条。
- LinePagesAuthoringTests：4 PASS，0 FAIL，0 SKIP，见 evidence/line-header-menu.trx。

最终 self-contained Windows x64 Release 已晋升：
E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe
ProductVersion: 0.3.3.2
Length: 142126806 bytes
SHA-256: E26072E67CF3CA33DC9854FD0D455BCB2964A6B64AE3A0852A6165E67F7B09CB

用户原来打开的进程未被强制关闭；旧程序映像保存在 .tooling/0332-ui/header-previous.exe。新的实机检查进程运行的是权威 dist 路径。没有提交或推送。本条记录不等于整份 0.3.3.2 计划验收通过。
