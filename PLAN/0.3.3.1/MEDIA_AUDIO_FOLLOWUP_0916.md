# 0.3.3.1 头像加载、音频与播放器修复

状态：代码修复与本地产物交付完成；Studio 实机视觉验收受工具阻塞。USER_ACCEPTED=NO；不是 GitHub Release。

## 复现与根因

- 使用用户故事包的隔离副本在真实 Minecraft 1.7.10 客户端复现，未修改原故事包或用户存档。
- 语音文件大小 5,080,224 字节；旧链路每 32 KiB 等待一次主线程/网络往返，首次实际播放约需 41 秒。提前点下一句会取消尚未加载完的语音。
- 真实 OpenAL 源观测到原版背景音乐与 `dgr_session_voice` 同时播放，时间戳持续增长；没有证据表明二者互斥。这项验证证明音频引擎播放状态，不等同于用户设备的听感验收。
- 第一轮本地直读仍有重复整文件校验：头像约 1.29 秒、语音约 6.22 秒。该结果没有作为完成依据，继续修复。

## 修改范围

- 单人已安装故事包走经过完整性校验的本地读取；不在渲染线程读取或解码图片。
- 缓存头像纹理，保留台词到选项的头像上下文；缺失图片不会无限阻挡整个对话框。
- 台词语音非阻塞，推进对话立即停止旧语音。
- 音乐使用卡片 UI，`导入`/`停止`等宽排列，删除“停止当前音乐”小字。
- 音乐和台词均提供音量；旧数据缺省 100%，新值参与 Studio 试听、运行时播放和音乐渐变。

## 验证与交付

已通过：`canonicalSessionRuntimeProbe`、`canonicalSessionNetworkCodecProbe`、`canonicalSessionServerServiceProbe`、`canonicalSessionClientModelProbe`、`sessionPresentation0330Probe`、`mediaTransfer0330Probe`、`dgrsMediaLifecycle0331Probe`。

音量测试覆盖：旧数据默认值、新值编码往返、语音 40%、音乐 25%、渐变中途保持相对增益、非法值拒绝。媒体生命周期环境中的 reparse-root 检查因环境不可用而跳过，不计作通过。

最终空媒体缓存实测（真实客户端、隔离故事包副本，先进入世界进行后台预热）：首次观察到 LINE 的 227 ms 时，其头像纹理已经就绪；5,080,224 字节语音在 464 ms 时 `actual_playing=true`。全过程网络媒体请求为 0。此计时包括测试入口和客户端主线程调度，不代表所有硬件的保证值。

`final-playing-bgm.json` 同时记录 DGR 语音和原版背景音乐实际播放；主音量、玩家、音乐类别均为 1。推进操作后语音立即停止。前一轮实机 `baseline-choice.json` / `voice-choice-0916.png` 已验证 CHOICE 保留头像。最后一次推进后立即采样仍是等待服务器更新的 LINE，不将该采样冒充 CHOICE 证据。

增加的包读取测试发现旧断言仍要求整个媒体驻留内存，已改为通过新 reader 读取全部块并核对 SHA-256，保留错误偏移拒绝及关闭后重新加载检查。`mediaPackage0330Probe` 随后通过。`reobfJar` 成功。

Studio：Core 444 通过、3 跳过；最终针对音量的 WPF 测试 9 通过，覆盖实时草稿、提交、持久化、撤销和重做。已启动正式 EXE 并读取实际窗口控件树。Windows Computer Use 两次截图均报 `SetIsBorderRequired failed: 不支持此接口 (0x80004002)`，输入报 `coordinate input geometry is unavailable`。测试宿主的 Fluent 截图对比度失真，未作为视觉通过证据。

正式交付：

| 文件 | 版本 | 字节数 | SHA-256 |
| --- | --- | ---: | --- |
| `dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe` | ProductVersion 0.3.3.1 | 141965014 | A40A0E43D9AB0954ABB4F5F1864E9F529E8620120585B2BBB3025F5DB5A39C84 |
| `dist/darkgrey_rpg-0.3.3.1.jar` | 0.3.3.1 | 1178980 | 815B161DE603B20A534E63ADC23EC42F3CCEF95CC801D116D2C4CEAB56B064A8 |

Studio 为 Windows x64 自包含 Release，正式 EXE 与发布候选逐字节哈希一致。

原始验证记录：`.tooling/0.3.3.1/media-followup/`。游戏画面：`.tooling/0.3.3.1/live-client/screenshots/final-fastload-line-0916.png`。

网络多人会话仍使用原有鉴权传输，本轮本地加速不能视为远程首次下载延迟已解决。
