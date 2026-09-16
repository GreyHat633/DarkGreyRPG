# 多格式图片导入

- 画面、角色默认头像和表情差分共用导入过滤器，支持 PNG/APNG、JPG/JPEG、WebP、BMP、GIF、TIF/TIFF；可选择所有文件，实际依据内容识别。
- 导入转换为保留透明通道的静态 RGBA PNG，动画及多页图片取第一帧；不改源文件，元数据分别记录源指纹与运行文件指纹。已有资源不改写。
- 保留源文件大小、像素上限、解码完整性检查、临时工作目录清理及原子安装。尺寸采用转换后的实际尺寸。
- 11 项真实 FFmpeg 媒体测试通过：八种静态/动画编码、透明度、第一帧、重复导入、原文件不变、错后缀、损坏及超大图片拒绝，以及既有音频导入回归。
- 22 项相关 WPF 测试通过。
- Windows 原生 UI/Win32：独立 ExperienceProject 的画面节点实际导入用户的 C7A67EEBC861BEC8E987A152B71CAB45.png（JPEG 内容），确认列表新增并显示缩略图/预览。证据：evidence/live/ImageFormats-UserImageImported.png。未修改用户项目。
- 正式程序：dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe，ProductVersion 0.3.3.1，133848559 bytes，SHA-256 6D9B91C11A186F553DFD10ED2F7D0CA573CDC81C788A1FEFF0CE03551EA43069。
- 保留当前用户的 Studio 进程；保存并重启后使用新版。未重新运行 Minecraft；本次没有修改模组或故事包数据接口。
