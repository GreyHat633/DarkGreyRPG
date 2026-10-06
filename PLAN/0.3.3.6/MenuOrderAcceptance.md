# 项目菜单顺序修正

2026-10-06。沿用 Studio 0.3.3.6，本轮仅调整项目菜单顺序。

当前顺序：新建故事 → 引用故事包 → 迁移故事内容 → 导入故事包 → 导出故事包 → 准备 Runtime 重载 → 验证项目。命令绑定与 AutomationId 保持原有值。

## 验证

- 自动构建：Windows x64 self-contained Release 发布成功。未为这项菜单排序改动新增测试或重复运行完整套件；此前回归记录不代表本轮重新执行。
- 代理实机：重新启动权威 dist 版本，通过原生 UI Automation 展开项目菜单，核对七项菜单的名称及实际垂直顺序，并检查截图。验收后收起菜单，Studio 保持运行。
- 用户验收：待用户确认。
- 数据保留：部署前后 Data 均为 2,661 个文件、64,212,068 字节，逐文件路径、大小和 SHA-256 比较差异为零。
- 程序布局：405 项程序清单均存在，根目录仅保留 apphost EXE。Runtime JAR 未改变。
- 临时文件：本轮两个发布暂存目录共 1,187,165,363 字节已送入回收站，回收站内容验证通过；既有目录未清理，未清空回收站。
- 未创建成品包、ZIP 或发布，保留现有工作区改动。

## 权威交付

路径：`E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`

| 文件 | ProductVersion | 大小（字节） | SHA-256 |
| --- | --- | ---: | --- |
| DarkGreyRPGStudio.exe | 0.3.3.6 | 204288 | 8e8bf253b16b1516616d890b8bd604a907cf9d607469e3785d4ab113a5b3b20c |
| Program/DarkGreyRPGStudio.dll | 0.3.3.6 | 1757696 | e69588f2f7c36a453596e0f760c7c6ce099a11ec4ef734c916466e784154f957 |
| Program/DarkGreyRPG.Studio.Core.dll | 1.0.0+99a3253d7186127bdbe37898824ecbd32219f136 | 1308160 | 13fde463fbf91877e36fa3c10111f8ea4bbc812328a9322e41200c22eefd5e6e |

菜单改动包含在 Studio DLL 中，apphost EXE 与 Core DLL 的内容哈希保持不变。

证据目录：`.tooling/0336-ui-followup/MenuOrder/`。包括 `Publish.log`、`NativeProof.json`、`MenuOrder.png`、`DeliveryProof.json`、`DataBefore.json`、`DataAfter.json` 与 `RecycleProof.json`。
