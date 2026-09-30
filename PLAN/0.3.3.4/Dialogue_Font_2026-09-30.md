# 0.3.3.4 对话字体缩放与清晰度修正

USER_ACCEPTED=NO，RELEASE_READY=NO。仅更新 MOD，Studio 无改动。不提交、不推送。

## 实现与边界

- 名字不再写死倍率 1；NPC 名字、正文、对话记录和设置预览共用实际倍率。保留名字粗体、主题强调色、头像上方居中及完整分割线；按放大后宽度截断并提供全文悬停。
- 100% 保持原尺寸。原版 Unicode 字形的源像素占半个 GUI 单位；放大倍率取 `ceil(请求倍率 × GUI 因子 / 2) × 2 / GUI 因子`，使源像素整数映射到屏幕且不向下缩小。名字的居中起点对齐屏幕像素。125%／150% 仍是保存的用户选项，实际值不覆盖偏好。
- 标题栏随名字高度向下增加；分割线、头像居中区域、正文起点和自动／记录按钮同步定位。绘制、换行、行高及分页均用实际倍率，沿用原有来源锚点重排；100% 标准 320×240 的头像 48、正文起点 80、正文上下界 175/221 不变。
- 设置页在需要对齐时明确显示实际倍率，给说明独立行；滑条和预览向下移动，点击和滚动坐标同步。较小逻辑窗口可滚动查看预览。记录布局在字体资源重载和 Unicode 开关变化时也失效重排。
- 字体按原版资源位置读取当前字体纹理，仅在本次文字绘制内强制最近邻采样。按真实纹理 ID 去重，分别保存和恢复过滤参数，同时恢复纹理绑定、颜色、混合等 GL 状态。没有改变图片／物品纹理或全局字体设置，没有增加字体文件、存档或协议字段。
- 默认关闭的 `darkgrey.dialogue.font.diagnostics` 检查实际最近邻和过滤／绑定／混合恢复。`darkgrey.dialogue.font.samplingProbe` 在真实 GL 上验证非默认线性过滤也能恢复，检查后还原原值；测试开关仅加到隔离 JVM，本轮生产默认关闭。

## 自动验证

assemble、DialogueFontScaleProbe、canonicalSessionClientModelProbe、dialogue0333Probe 全部通过。前者覆盖 GUI 因子 1～8 的整数源像素映射、不向下缩小、居中起点、放大标题与按钮不重叠、头像仍居中和 100% 标准容量几何；原有身份／分页／阅读锚点和不重复布局测量回归通过。最终 compileJava、jar、reobfJar 均 UP-TO-DATE；构建与实机候选 JAR 字节一致。

## Windows 原生实机

应用 windows-native-ui 技能，所有窗口输入和截图均为 Win32／UIA，没有 Computer Use／cua。隔离 Witness 客户端和 font-pixel-world 独立服务器使用同一候选 JAR；固定测试包包含普通名字、长名字、竖／横头像、中文／英文／数字／标点和长台词。没有操作正式世界。

| 实际 GUI 因子 | 请求 100% | 请求 125% | 请求 150% |
| --- | --- | --- | --- |
| 1 | 100% | 200% | 200% |
| 2 | 100% | 200% | 200% |
| 3 | 100% | 133.33% | 200% |
| 4 | 100% | 150% | 150% |

十二项均有实际日志和原尺寸截图，见 evidence/font-pixel/native-matrix.json。因原版强制 Unicode 会把奇数 GUI 因子降到偶数，因子 3 的测试关闭 forceUnicodeFont；其他因子保持该开关开启。设置说明按整数百分比显示，133.33% 显示为 133%。

- 修复前／后小 GUI 同尺寸：evidence/native-ui/font-before-scale1-150-stable.png 与 font-after-scale1-150.png。名字随正文增大；原版非整数缩放导致的笔画采样不均通过像素档位消除，保留原版像素字形，不承诺位图字体变为矢量字体。
- 每个因子三个档位：font-after-scale{1,2,3,4}-{100,125,150}.png。浅白、蔚蓝、灰黑主题已有原生样本，未声称穷尽主题／尺寸交叉组合。
- 实际倍率说明与预览：font-preview-scale2-150.png；小逻辑窗口滚动预览：font-preview-scroll4.png。前景与设置背后的对话使用同一布局。
- 长名字全文悬停：font-long-name-tooltip1.png。缩小到 960×640 后重排：font-reflow-scale2-960.png。记录和真实滚轮：font-history-scale2.png、font-history-scroll2.png。
- 原生点击实际分页：font-long-nextpage4.png、font-click-page4-*.png、font-click-tail4-*.png；可见后续不同正文段落，未提前跳出长台词。部分早期 `font-long-page2-scale1`／`font-long-enter-scale1` 是 Enter 探测截图，不用于宣称继续台词成功；当前继续交互以正文点击为准。
- 无头像：替换隔离测试包中的首句角色后 reload 并重新进入触发区，font-no-avatar-scale4.png 确认正文不预留头像列、名字与分割线正常。该测试只修改隔离包。
- 四次 JVM 的 `DIALOGUE_FONT_SAMPLING_PROBE linearRestore=true` 和所有倍率的 `nearest=true filtersBindingBlendRestored=true` 均通过。实际检查了过滤参数、原纹理绑定及混合状态，没有以常量或静态推断替代采样核验。

## 恢复与剩余验证

测试 JVM 已退出，八项原隔离配置／MOD／包均恢复并逐项哈希匹配，见 restore-proof.json。测试输入、世界副本和日志留在 .tooling/0334/FontPixel，供复查。

正式故事包和完成历史与上一轮隔离基线哈希相同。正式任务实例与上一轮基线不同，其最后写入为 14:02:50，早于本轮隔离设置 14:28:02；不恢复该文件，不把旧基线差异当作本轮新增差异，也不声称与旧基线逐字节相同。详见 formal-read-only-proof.json。本轮所有测试启动、世界、配置及写入路径均在 .tooling/0334。

100% 小 GUI 有意维持原版字形和容量，仍可能显得小；新规则主要解决放大档位的采样与名字未跟随问题。原版字体的像素风格、不同资源包的字形质量不在本轮重设计。三主题与尺寸样本不是全排列，旧强制延迟占位抓拍、C10 长文本／语音／会话历史组合、C12 提交候选边界及媒体公网／多人压力等未覆盖项继续开放。不标记用户验收。

AGENTS 要求的 CAS 预检及初始／收敛分工记录返回 SCHEDULER_PAUSED；Main 完成跨模块修改、复查、实机和集成交付。

## 交付

- 权威路径：E:/Java/MinecraftMod/DarkGreyRPG/dist/darkgrey_rpg-0.3.3.4.jar
- mcmod.info 版本：0.3.3.4
- 大小：1,800,595 字节
- SHA-256：A95138D18823EA2BE66F5D55D90B3A9538D6FA543AE4EC958BA6D57136C8630C
- 构建、隔离实机客户端／服务端候选和 dist SHA-256 一致，详见 evidence/font-pixel/Delivery.json。
- 客户端／服务端配套使用该 JAR；Studio 权威 EXE 不变。
