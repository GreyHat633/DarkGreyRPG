# 0.4.0.0 成品打包与 GitHub Release

2026-10-08，用户明确要求“打包至artifact，并在github上release”，据此完成本轮成品交付。此授权替代此前开发修复轮次中的“不打包、不创建标签／Release”限制；此前记录保留其当时含义。

## 成品和远端

- 本地目录：`artifacts/DGR0.4.0.0`，根目录仅含 Mod、Studio、Docs、Verification。
- [GitHub Release v0.4.0.0](https://github.com/GreyHat633/DarkGreyRPG/releases/tag/v0.4.0.0)，正式发布，五个附件均为 uploaded。
- 标签指向已验证的实现提交 `a80040c81f12f22a34fac7fb8e20017dbb3a00b2`，来自 `codex/0.4.0.0`。本次没有生产代码修改或版本递增，沿用此前通过验证的产物字节。
- 公开附件：Runtime JAR、完整 Studio ZIP、README.md、ReleaseManifest.json、SHA256SUMS.txt。截图、原始测试日志和本机路径记录仅保留于本地 Verification。

| 文件 | 字节 | SHA-256 |
|---|---:|---|
| darkgrey_rpg-0.4.0.0.jar | 1,843,025 | `0d971907c95602ae94544b684d27e145014a9b5a4f9d1a79dedf1e6508ff8cd9` |
| DarkGreyRPGStudio-0.4.0.0-win-x64.zip | 236,964,268 | `ab5f2a3388de26e1a88b8ac97dd8e85c99fb0f41662f122ef0aa0f3e6f304e59` |

## 包装核验

JAR 元数据版本 0.4.0.0；JAR／ZIP CRC 检查通过。Studio 从权威 dist 的 409 条程序白名单和白名单文件本身打包，逐项核验 410 个路径／大小／SHA-256，再附 PackageContents.json，共 411 个 ZIP 文件。ZIP 根文件只有 apphost EXE，保留 Program、Tools、Docs 完整目录，排除全部 Data。

实际解压到全新隔离目录。设置 DOTNET_ROOT／DOTNET_ROOT_X64 为不存在的任务目录、限制 PATH 后，从不同工作目录原生启动。hostfxr、hostpolicy、coreclr、wpfgfx_cor3 均实际从本包 Program 加载；首次深色空项目，不读取权威副本设置。通过 UIA 新建默认 Data/Projects/Project，设置保存为相对路径；正常关闭、移动整个文件夹，再启动后恢复该项目。随附 FFmpeg 与 FFprobe 的 -version 均 exit 0。

权威 dist 不因此次包装重建或替换。前后 Data 2,690 个文件、68,773,074 字节及所有路径／大小／SHA-256 相同，汇总指纹 `1235f2f49819ec3eda505002d13826c3fd1949b28de85d3ecb6a0855baa85c7d`。权威 EXE ProductVersion 0.4.0.0、204,288 字节、SHA-256 `f68e1d6fab7a4717089b394c9842bedcfd7eaec93382e76d86e0e5725e9001b3`；主 DLL 1,458,688 字节、SHA-256 `542f8b01708a84bbf0d9a938f10e5949621549c19be50efe363a8ff152bed3ef`。

692 个生产源码内容与标签提交一致。Core DLL 的信息版本保留构建时基线提交，其修复来源与实际文件哈希由先前提交内容比对和本次 ReleaseManifest 记录，未以新提交号重新构建替换已验证二进制。

先创建草稿、上传全部附件并校验 GitHub 返回的大小／SHA-256，再发布。发布后再次核对公开 Release、附件、标签解析和 Latest；详见 DELIVERY.json 的 finished_product_release。源码分支追加此发布记录，成品对应的实现标签仍固定为 a80040c8。

## 验证范围

沿用本次实现的完整 Core 499 通过／11 跳过、WPF 667 通过／1 跳过、Runtime 构建及 22 个当前 Probe 通过，以及已执行的目标原生链。详细范围见 [DragCrashFixes.md](DragCrashFixes.md) 和 [SixPreUploadFixes.md](SixPreUploadFixes.md)。本轮仅针对成品包装新增解压及便携操作核验，不重复运行未变的生产源码测试。

额外物品 metadata／damage 过滤原生作者链、人工听音、所有环境／工具故障／拖动取消组合仍存在未运行项；两个旧夹具 Probe 不计通过。发布说明与成品清单保留这些限制，用户完整验收状态仍为 false。
