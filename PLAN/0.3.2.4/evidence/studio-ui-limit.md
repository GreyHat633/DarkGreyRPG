# Studio 实机工具限制

隔离候选 EXE 启动成功。使用 computer-use 技能和 @oai/sky；没有关闭用户原有 Studio 进程。

- 截图两次失败：SetIsBorderRequired failed: 不支持此接口 (0x80004002)。
- 重新激活后，辅助功能树可读取，Ctrl+O 实际打开项目目录选择框。
- 索引点击失败：coordinate input geometry is unavailable。
- 重新观察后控件赋值失败：read UIA value read-only state: 所需属性不在 CacheRequest 中 (0x80070057)。
- 未完成导出文件名/Export Import Reference 三个实际文件对话框及重启记忆验收。不能将 49 项自动测试或目录选择框打开算作 Gate F/G PASS。

本轮使用的技能：E:/AI/CODEX/.codex/plugins/cache/openai-bundled/computer-use/26.901.22334/skills/computer-use/SKILL.md。
其要求读取的 docs/guidance.md 明确规定："Use only the Computer Use JS APIs for Windows app automation." 因此本轮没有混用 PowerShell UIA 绕过工具失败。

STUDIO_FILE_DIALOG_REAL_MACHINE=NOT_VERIFIED
