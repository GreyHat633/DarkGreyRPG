# Studio 多句台词原生操作验证

测试项目：`E:\Java\MinecraftMod\DarkGreyRPG\.tooling\0.3.3.1\MultiPageStudio`，从独立 ExperienceProject 复制。没有操作用户项目。

工具：Windows UI Automation 定位真实窗口/按钮，Win32 鼠标按住移动、键盘快捷键，读取项目保存文件验证结果；截图使用 PrintWindow。未使用 computer-use。

已执行：

- 启动正式 `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`，进入故事/会话，点击 + 新增第二句，输入文本。
- 从第二句手柄拖至第一句，Ctrl+S 后 JSON 顺序为 `603e34d8571843e4b3c91bf71f09ecd2`、`node_713eb756c18a4bd59f731a39062fd14a:page:0`。两句配置跟随身份移动。
- 拖动时 Esc 取消，保存后仍为上述顺序。Ctrl+Z 恢复原顺序，Ctrl+Y 恢复新顺序；每步保存后核对 JSON。
- 新增第三句，点击删除，再 Ctrl+Z 撤销删除；保存后恰有三句，第三句 ID 为 `b17b97e5bedc4399a86d8260b92fd87f`。节点内与 Inspector 同时显示相同页数。
- 选择节点 Inspector 将第二条拖到第一条。保存后选项顺序改为 `dynamic_port_df8c52aafb774583a23aa848b1974689`、`dynamic_port_b8606f22058e44dba1abf7a1840e35b2`；各自 flow_port_id 未变。
- 结算节点新增一个结果，从第二条手柄拖至第一条。原端口 `dynamic_port_8a66683d24ed4c09b54b7eeb426000bb` order 从 0 变 1，新端口 order 从 1 变 0。保存前后 connections JSON 完全相等。
- 切换浅色主题，查看故事图谱珊瑚红表头、故事内部青绿会话/金黄任务；深色与浅色截图已保存。适应全部节点后缩放表头仍有对应配色，普通节点和连线保留原色。

自动化：WPF 完整 587 通过。之后增加选择/结算投影保留实例的改动，针对编辑器与 UI 再跑 54 项通过，覆盖页增删、配置、排序单次撤销、双投影同步、旧数据迁移、默认值、颜色与稳定行实例。

补充原生验证：重新打开独立项目后保留已保存的句子。追加五句形成八句长列表，修复卡片间隙拖放命中后，按住第二句在 Inspector 底部 4.5 秒，视口滚动超过 1000 像素；保存后第二句移动到第八句，其余 ID 不变。随后拖动末句到 Inspector 外放开，保存后顺序未变。对应截图 `pages-autoscroll-verified.png`、`pages-outside-cancel-verified.png`、`pages-reopen-observed.png`。

最终所有改动后 WPF 完整 587 通过，Core 完整 453 通过、12 跳过、0 失败。本记录仅证明上述范围，不表示全部交互已验收。

最终正式 EXE：版本 0.3.3.1，142089430 字节，SHA256 `11FBADBAC2EDE8B3F027D4A13EC2190B1B6A74D335F9FF62BE06713ECD278706`。
