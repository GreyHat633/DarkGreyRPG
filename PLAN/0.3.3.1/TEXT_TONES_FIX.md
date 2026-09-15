# 0.3.3.1 说明与输入提示文字层次

2026-09-15。此前将水印直接绑定正文颜色，丢失了用户要求的灰度层次；说明文字也未完整使用小字号。此记录修正 READABILITY_FIX.md 中“提示跟随正文颜色”的方案。

## 呈现规则

| 文字用途 | 深色主题 | 浅色主题 | 字号 |
|---|---|---|---|
| 说明文字 | 浅灰 #B8B8B8 | 灰 #666666 | 11 DIP，常规字重，自动换行 |
| 待输入提示 | 深灰 #909090 | 灰 #888888 | 跟随输入框字号 |
| 正文、已输入内容 | 原主题正文色 | 原主题正文色 | 不变 |

目标描述说明、前置条件说明、区域判定说明、标题播放说明、奖励说明、指令建议、资源状态说明、节点介绍及项目图谱介绍应用相同辅助文字规则。错误提示没有应用辅助文字弱化。

共用 AuthoringText 行为负责用途对应的颜色。通过控件的动态主题资源更新颜色，水印通过实际输入框解析资源，不依赖装饰层继承，也不复制正文颜色。空文本折叠行为仍保留。

## 验证

- WPF Release 529/529 通过；结果 `.tooling/0.3.3.1/text-tones-tests/text-tones.trx`。
- 测试明确断言说明的字号、字重、灰度；水印独立灰度、正文不改变；同一实例深浅主题资源来回切换后颜色正确。
- 实际权威 EXE、隔离 live-project、100% 画布。清空一个目标描述以同时查看输入提示和辅助说明；相邻有内容的输入框用于正文对照。
- [深色目标](evidence/live/text-tones-objective-dark.png)、[浅色目标](evidence/live/text-tones-objective-light.png)、[切回深色](evidence/live/text-tones-objective-dark-return.png)、[标题播放说明](evidence/live/text-tones-title-dark.png)。已逐张查看。
- UIA 实测说明文字行高约 17–18 像素，普通输入提示行高 22 像素（当前系统缩放）。USER_ACCEPTED=NO。

## 最新交付（替代之前记录中的 EXE 哈希）

- `E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`
- Windows x64 self-contained Release；ProductVersion `0.3.3.1`
- 大小 `141925078` 字节
- SHA-256 `5BBE8803401959FCEBC2D3FD96008C052280AAF9CA043028A767963F12F290D1`
- candidate 与最终交付文件哈希一致，已从权威路径启动并实机检查。
