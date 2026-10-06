# 导出位置窗口显示已有文件（0.3.3.6）

## 实现

单故事和故事组导出使用原生文件浏览窗口，显示所有文件和子文件夹，保留地址栏、搜索、目录导航及新建文件夹操作。文件名以故事或故事组显示名称生成，标注“文件名（固定）”并保持只读；确认按钮为“导出到此文件夹”，取消在右侧。已有文件的选择不会替换导出名称，覆盖仍需显式确认且默认选择“否”。

原来的 `OpenFolderDialog` 只显示文件夹。本次通过原生 `IFileDialog` 显示全部文件，仅取当前目录计算最终导出路径。文件名控件除只读外，还阻止 Shell 在选择回调结束后填入其他文件的名称；控件销毁和对话框关闭时移除回调。实现依据 Microsoft 的 [Common Item Dialog](https://learn.microsoft.com/en-us/windows/win32/shell/common-file-dialog)、[SetWindowSubclass](https://learn.microsoft.com/en-us/windows/win32/api/commctrl/nf-commctrl-setwindowsubclass) 及 [RemoveWindowSubclass](https://learn.microsoft.com/en-us/windows/win32/api/commctrl/nf-commctrl-removewindowsubclass) 文档。

仅修改 Studio 导出位置选择器，不改变格式、导出序列化、资源身份、Runtime 或既有工作区改动。

## 代理原生实机

复用 `.tooling/0336-ui-followup/Studio`，使用隔离项目与 `.tooling/0336-ui-followup/ExportBrowser/Exports`。通过 UI Automation 和 Win32 执行 Studio 的真实“项目 → 导出故事包”流程。

- 单故事与故事组窗口显示普通文本、`.dgrs`、`.dgrs.g` 和子目录。
- 文件名控件实际具有只读样式；选中全部文字并键入内容，名称保持不变。
- 点击另一个现有文件，名称保持当前故事名称；导出到正确的固定路径。
- 双击子目录、返回上一级，文件列表与只读文件名正常。
- 取消故事组导出不生成文件。
- 已有文件的覆盖确认默认“否”；拒绝覆盖重新显示文件浏览窗口，随后取消，原文件 SHA-256 不变。
- 两种格式均显示“已导出并验证”，生成包与此前已经通过 Runtime 正式加载验证的包逐字节一致。

| 导出文件 | 字节 | SHA-256 |
| --- | ---: | --- |
| 测试故事.dgrs | 3946025 | `4b03cf0eb7acd0f867b9bd542fbf19b462303d30b9d606720695fcb40c894e3a` |
| 故事组（1）.dgrs.g | 386546 | `8351d3e841d7ae870d287db41d5067a1f0a055bc864300187cffcd9fe8e307df` |

证据位于 `.tooling/0336-ui-followup/ExportBrowser`：`NativeResult.json`、`ExportHashes.json`、`06-single-readonly.png`、`07-single-other-selected.png`、`09-group-browser.png`、`10-group-subdirectory.png`、`12-overwrite-default-no.png`、`13-overwrite-declined-browser.png`。未向用户的 Minecraft 故事包目录写入测试包。

最后补充了私有 COM 对话框的完整释放，避免原生窗口对象等待 GC 回收。最终 DLL 连续三次打开并取消单故事／故事组窗口，文件列表与只读名称保持正常，关闭后无残留导出窗口；证据为 `FinalNativeCleanup.json`。

## 自动回归

相关回归 48 项通过，覆盖导出命名、路径与 Shell 导出路由。最终构建后的 WPF 功能回归 684 项通过、0 失败、1 跳过（原有默认不启用的 `FixedThreeHundredNodeWorkload` 基准），共 685 项；该运行显式排除了下述一个渲染压力测试。证据为 `Tests/related.trx`、`Tests/wpf-functional.trx`、`FunctionalWpf.log` 和 `TestCounts.json`。

COM 释放补充后重新构建并再次执行相关回归：48 通过、0 失败；证据为 `Tests/related-final.trx` 和 `RelatedFinal.log`。释放补充后的取消检查和权威启动另有上述原生证据，未将之前的完整功能运行冒充为再次运行。

默认渲染完整 WPF 运行中，已有 `GroupFrame0333Tests.VisibleRenderingDuringMoveAndPageCollapsePreservesGroups` 在 300 节点图的显示阶段长时间停滞；首次运行由代理停止，随后默认和软件渲染运行分别被 60 秒无进展保护中止。均不能记为完整套件通过。保留中止的 TRX、阶段日志、测试序列和 `FullWpf*Timeout.log`。该压力测试不调用导出窗口，本轮不修改图形渲染实现；其余功能回归单独执行，原生导出实机验收使用默认渲染。

## 交付与用户验收

沿用 0.3.3.6，通过 `studio/package-studio.ps1` 更新权威 `dist/DarkGreyRPGStudio`；程序校验、Data 保留和暂存回收记录在 `DeliveryProof.json`、`DistSmoke.json`、`RecycleProof-All.json`。复用一个验收候选目录，不生成成品 ZIP、不发布、不提交工作区。

最后部署前后以及最终权威启动、正常退出后，Data 2661 个文件、64211980 字节，按路径、大小和 SHA-256 比对没有改动。权威程序恢复“测试项目”，没有错误窗口。第一次启动验收正常退出时曾回写 `Data/Config/settings.json` 中的窗口布局；当时其余 2660 个 Data 文件内容不变，包含所有项目、资源、媒体及恢复记录。最终验证记录于 `DeliveryPromotionProof.json`、`PostSmokeDataCheck.json`；第一次窗口设置回写另保留于 `FirstPostSmokeDataCheck.json`。

| 程序文件 | 字节 | SHA-256 |
| --- | ---: | --- |
| dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe | 204288 | `8e8bf253b16b1516616d890b8bd604a907cf9d607469e3785d4ab113a5b3b20c` |
| dist/DarkGreyRPGStudio/Program/DarkGreyRPGStudio.dll | 1757696 | `a4c5516376853277ed8cf7c647094929d7140687059c440aee73673941497bb0` |
| dist/DarkGreyRPGStudio/Program/DarkGreyRPG.Studio.Core.dll | 1308160 | `13fde463fbf91877e36fa3c10111f8ea4bbc812328a9322e41200c22eefd5e6e` |
| dist/darkgrey_rpg-0.3.3.6.jar | 1953544 | `8cea55b7b577df8686e2a965caa895e229774b12bcc99c8d1492245e6b400031` |

EXE apphost 字节不变，本轮变化位于 Studio DLL；Core DLL 与 Runtime JAR 沿用已经验证的文件。程序白名单 405 项无缺失，根目录仅有 EXE。将本轮十个无用的发布暂存目录（5935823743 字节）送入回收站，并核对十个可恢复载荷及其中的 Studio DLL 校验值；没有清空回收站，没有处理此前四个历史暂存目录。

本报告区分自动回归、代理实机和用户验收；本次尚未记为用户验收通过。
