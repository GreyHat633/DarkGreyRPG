# 台词统一删除与多选（2026-09-17）

本记录替代 LINE_HEADER_INTERACTION_FIX.md 中逐句 ⋯ 删除及至少保留一句的方案。

- 顶部 ＋ 右侧增加统一 −；逐句删除按钮/菜单全部移除。
- 单击句子选中；Ctrl 切换该句选择；Shift 从锚点连续选择，Ctrl+Shift 合并区间。修饰键多选不触发折叠/字段操作；普通标题点击仍折叠。
- 选择状态按 host/node/page_id 共享，节点内及 Inspector 同步；排序后按新的列表顺序计算连续区间。选中只改变细蓝边框，不铺亮蓝背景。
- 批量删除一次写入，一次撤销恢复；允许删空，空列表可重新添加。选择状态不写图数据、不占撤销历史。
- Studio 空 pages 数组可保存、重开和复制；Mod 沿 flow_out 跳过空台词，保留自动流转循环保护。空列表不等于有一句空白正文，后者沿用原有正文校验。

验证：LinePagesAuthoringTests 5 PASS；SessionPagesTests 10 PASS。Java build、CanonicalSessionRuntime/Instance/ServerService 三探针 PASS（含空台词直接进入后续选择）。见 evidence/line-multiselect.trx、empty-pages.trx、empty-pages-runtime.log。

Windows UI 实测：multi-03-ctrl 为 Ctrl 多选（旧亮底，仅行为证据）；multi-soft-08-shift 为最终细边框和 Shift 连选；multi-soft-09-empty 全部删除，两处列表均为空；multi-soft-10-undo 一次 Ctrl+Z 恢复两句。

权威 Studio：E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe
ProductVersion: 0.3.3.2
Length: 142130902 bytes
SHA-256: 56F92F5A7A69C9293C1959244CD4F315E24E6821C8946D53890194AF24C7F1A7

Mod：E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.2.jar
Length: 1667270 bytes
SHA-256: 1346279BB988DA0388974DC8B4806A5E1357BF4C2B95055D29F81DAAD514CFDA

实机 UI 运行最终 EXE。Mod 此项使用真实 runtime/实例/服务端探针验证，未将其描述成 Minecraft UI 实机跳转验收。没有提交或推送。原先已打开的旧程序没有被强制关闭。
