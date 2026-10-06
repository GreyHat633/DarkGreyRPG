# 第三方组件与授权核对

本轮候选仍为 0.3.3.7。DGR 自有代码的项目级许可为 **PROJECT_LICENSE_PENDING_OWNER**：没有默认套用 MIT／GPL，也没有作出商业或衍生作品授权声明。第三方许可仅适用于其对应内容。

## 精确组件清单

| 组件 | 用途／分发位置 | 版本与来源 | 许可材料／核对状态 |
|---|---|---|---|
| CustomNPC+ | 可选实体适配；开发及集成测试依赖；仓库 libs 跟踪，DGR JAR／Studio 不随附 | 1.11.1-fixed-v1；上游 [KAMKEEL](https://github.com/KAMKEEL/CustomNPC-Plus)，具体 fixed-v1 修改与对应源码未查明 | 本二进制嵌入 LICENSE.txt 声明 MIT，原样留存 third_party/licenses/CustomNPC-Plus/LICENSE.txt；该文本没有补造作者行。PATCHED_BINARY_SOURCE_PENDING，不能将最新版上游许可自动套到完整修订副本 |
| UniMixins | 指定 CNPC 构建的集成环境；仓库 libs 跟踪，不随附 DGR／Studio | 0.3.1 all；[精确发布](https://github.com/LegacyModdingMC/UniMixins/releases/tag/0.3.1) 的 asset digest 与本地一致 | 主许可 Unlicense，多个模块另有 LGPL／MIT 等原文；保留二进制内部 META-INF/licenses，并原样提取到 third_party/licenses/UniMixins。不能简写成全部 Unlicense |
| JLayer | Runtime MP3 解码；libs 与 DGR JAR 的 javazoom/jl，未改库源码 | javazoom:jlayer:1.0.1；[Maven Central 目录](https://repo.maven.apache.org/maven2/javazoom/jlayer/1.0.1/) | LGPL 2.1 文本与对应 source JAR 已在 libs/jlayer-license；随 Runtime META-INF/licenses/jlayer，保留替换／重建说明。二进制和源码哈希固定 |
| FFmpeg／FFprobe | Studio 独立子进程媒体转换／探测；Tools/FFmpeg | 9.0-full_build-www.gyan.dev；[Gyan 9.0 原始发布](https://github.com/GyanD/codexffmpeg/releases/tag/9.0)，FFmpeg 源修订 d32b387f2b | 原始 LICENSE 为 GPLv3，README 保留配置与外部库清单。实际 --enable-gpl、--enable-version3，不能写成默认 LGPL。原始四文件和说明随 Studio；完整静态组合各外部库的对应源码／分发履行仍 SOURCE_MATERIALS_PENDING，不宣称已经完全核清公开再分发 |
| .NET／WPF | Studio Program 自包含运行库；SDK 仅开发 | 本轮 SDK 10.0.302、运行库 10.0.10；[runtime v10.0.10](https://github.com/dotnet/runtime/tree/v10.0.10)、[WPF](https://github.com/dotnet/wpf) | 使用实际选中 SDK 根的 LICENSE.txt、ThirdPartyNotices.txt，复制到 Docs；保留 .NET Foundation 与第三方条款。每次候选 manifest 记录实际文件哈希，不能拿另一 SDK 材料替代 |
| MSTest | Core／WPF 测试，未进入 Studio 程序白名单 | NuGet MSTest 4.0.2；[Microsoft testfx](https://github.com/microsoft/testfx) | MIT；测试包依赖由 NuGet 恢复，保留缓存随附声明，正式程序不包含测试程序集 |
| Gradle／GTNH convention | wrapper／构建工具，不进 Runtime／Studio | Gradle 9.3.1，GTNH settings convention 2.0.20／blowdryer 0.2.2，来自所配官方仓库 | 构建工具声明随原始分发；[Gradle license](https://github.com/gradle/gradle/blob/v9.3.1/LICENSE)，GTNH 具体传递构建依赖原文留在恢复产物。开发工具不冒充成品随附库 |
| Minecraft／Forge 及其运行依赖 | 独立游戏安装提供，DGR 正式 JAR 不打入它们 | Minecraft 1.7.10／Forge 10.13.4.1614，包含游戏提供的 Gson／Guava／LWJGL 等 | 各安装包的原有许可／使用条件独立适用。DGR 不授权 Minecraft 代码、声音或纹理；测试的安装材料不作为新成品上传 |

## 锁定哈希

SHA-256（小写）：

| 文件 | SHA-256 |
|---|---|
| CustomNPC-Plus-1.11.1-fixed-v1.jar | 48ab697e63e8cc059592597f88a9fa00fad23e447b553996e1ef04510be2f2d3 |
| +unimixins-all-1.7.10-0.3.1.jar | ad0ea4f92daf7bf7ec5c10e16258425e47929126954aea5d80df69ce26cd7318 |
| jlayer-1.0.1.jar | 850508c837454a1b06017c32a36876fae516de1e89a829f725fee1e6dcc52000 |
| jlayer-1.0.1-sources.jar | ecde410fc8940ab5d8d5a1d5c585870a3a194f4001701e66a27b8dd8cb7b75ce |
| ffmpeg-9.0-full_build.zip | f42f0c4b04eae3ac918707ff66e3e0ff0cee527bfa6d322624d4bc1160d5055e |
| ffmpeg.exe | 05f4251bce9293c2ab492cb17ca7724a0ffd0d06c881ba2ee83b82a89c2fc740 |
| ffprobe.exe | 51e0780cd881f83749b029ed716cbb841c2eac6289f418050f2f2961b158896b |
| FFmpeg LICENSE | 8ceb4b9ee5adedde47b31e975c1d90c73ad27b6b165a1dcd80c7c545eb65b903 |
| FFmpeg README.txt | c9dd4a230f66f8f15f43fbfa56fa9d15edfac2b19a52297e03237f5988cbdac7 |

.NET 分发目录由 SDK 选定，实际运行库／notice 哈希列入本轮 Delivery 与程序白名单指纹，不能为不同 SDK 固定一个假哈希。

FFmpeg 的独立命令行使用与链接 avcodec 等库不同；[FFmpeg 官方说明](https://ffmpeg.org/legal.html) 的 LGPL 链接清单不能直接当作本 GPL 静态二进制已完成的证明。这里保留已核实原文、构建信息及准确来源，明确对应源码材料缺口。

仓库二进制也构成分发范围，即使不在最终 DGR JAR 中。此轮不删除 libs、不改 Git 历史、不改既有 Release。上述待确认项分别涉及自有代码授权、CNPC 修订来源、FFmpeg 静态组合来源材料；不将它们合并成“所有内部运行不可用”。本轮不作完整法律合规结论。
