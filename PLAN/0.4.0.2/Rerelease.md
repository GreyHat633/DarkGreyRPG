# 0.4.0.2 UI 修正版重新发布

2026-10-10，按用户“重新打包并releas至github和artifact”的明确指令，更新同版本本地成品、源码标签与 GitHub Release。

- 本地当前成品：`artifacts/DGR0.4.0.2`；只含 Mod、Studio、Docs、Verification。
- 旧成品完整保留：`.tooling/0402-rerelease-20261010/PreviousProduct`，原 JAR 为 `8B416C27…`。
- [GitHub Release v0.4.0.2](https://github.com/GreyHat633/DarkGreyRPG/releases/tag/v0.4.0.2) 已更新并保持 Latest，仍是 release ID 408610614；五个正式附件的大小、uploaded 状态和远端 SHA-256 均与本地一致，暂存与旧附件已经清理。
- 实现提交与标签目标：`c184e8fa7ea5da05c4123f39001253c8f144577d`，分支 `codex/0.4.0.2`。用户授权同版本替换后，标签由 `5a116c96ad0aba2c3c1a6b21ccd29a991f3ce881` 更新至修复提交；旧提交仍保留在历史中。
- HTTPS Git 三次连接失败后，使用 GitHub Git Data API 上传；68 个变更文件对应的 blob、整棵 tree 和 commit SHA 都与本地 Git 对象相同，未替换成不同来源的提交。

| 成品 | 字节 | SHA-256 |
| --- | ---: | --- |
| Runtime JAR | 1,901,883 | `cdbd9e556d48c91603bc491ae689ae1052b11f7e813b150be958245d43d17594` |
| Studio ZIP | 236,964,422 | `fb7556766fda946d3b19600fb49db20ff7c2efbbf12f08feb7518ab1a97fe91c` |

Runtime 包含 [UI 尺寸恢复](UiSizeCorrection.md) 和 [分数缩放采样修正](FontCoverageCorrection.md)。沿用已验证 JAR 的原始字节，没有为提交号重建。当前 build 与 42 项 Probe 通过，当前哈希实机对照 14 组，GUI 1–4；之前的尺寸恢复 19 组保留其 `00D03FC1…` 检查点和未变布局的继承范围。

Studio 的程序与源码相对首次 0.4.0.2 发布未改；ZIP 重新从权威白名单打包，只有包内来源说明更新。权威 EXE 仍为 ProductVersion `0.4.0.2`，204,288 字节，SHA-256 `8E569CC39547BCA0BB9E537BFC4BD42E048318B5720657735176C8F30CB97829`。程序没有重新部署，个人 Data 全路径、大小与 SHA-256 在包装前后相同。

重新核验 JAR/ZIP CRC、410 个程序文件与权威目录同字节、411 个解压文件、Data 排除、根目录仅 EXE，以及自包含运行库独立启动、首启深色空项目、本地默认项目创建、移动整体目录后恢复、FFmpeg/FFprobe exit 0。Studio Core510、WPF667/1SKIP 为未改源码与程序的已有回归，本次没有冒称重新跑过。

来源比对覆盖 1,086 个已提交输入：571 个 Runtime 输入与 515 个 Studio 输入；只允许 Git 的 CRLF/LF 规范化，历史忽略的本机 `_probe.txt` 不参与源码上传或成品。Runtime 物理来源指纹仍为 `BCF2C3A89C19B5D44D2009321204223E9CAFEAC78149881432C705A26AC23F6C`。

原双客户端 60 分钟长测、冷恢复和压力指标保留原进程哈希；当前 JAR 的未列出业务/存储/tick/队列/媒体条目逐字节相同，变化的字体和布局由新实机检查覆盖。未将旧整包实测写成修正版整包实测。人工听音、用户延后专项、原压力超限、旧 dialogue0333Probe 断言和其他既有保留项均不因重新发布关闭。

首发冻结记录仍在 [Release.md](Release.md)；当前发布状态以本页、[DELIVERY.json](DELIVERY.json)、[新成品清单](evidence/rerelease/release-manifest.json) 和 [远端回执](evidence/rerelease/github-release.json) 为准。发布后分支另追加记录提交，标签继续对应实现提交。原验收记录不是本次 JAR 哈希或新实机报告。
