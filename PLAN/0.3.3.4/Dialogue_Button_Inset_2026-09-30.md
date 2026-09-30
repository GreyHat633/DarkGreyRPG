# 对话按钮右侧留白修正 · 2026-09-30

“自动”和“记录”整体向左移动 3 个 GUI 单位，与头像的内侧留白呼应。GUI 因子 2／4 对应 6／12 屏幕像素。记录 x 从 right-39 改为 right-42，自动 x 从 right-75 改为 right-78。按钮宽高、相互间距与纵向位置不变；使用按钮自身坐标绘制和命中。分割线端点、头像及正文布局不变。

构建、dialogueFontScaleProbe、canonicalSessionClientModelProbe、dialogue0333Probe 和相关文件 diff --check 通过。

通过 Windows 原生 Win32 操作隔离客户端 PID 14416（E:/Java/jdk1.8.0_471/bin/java.exe；.tooling/0334/Witness），连接隔离服务器 PID 1560（.tooling/0334/Server/font-pixel-world）。实机检查 1280×960／GUI 因子 4 和 960×640／GUI 因子 2：按钮整体左移，完整分割线未变，无重叠。原生点击“记录”进入记录页面，返回后仍显示原对话；点击“自动”后出现自动等待提示，隔离配置记录 dialogue.automatic=true，确认新位置能够启用自动播放。随后关闭进程并恢复原隔离配置；连续第二次点击未作为关闭自动播放的成功证据。

画面及点击结果见 evidence/native-ui/button-inset-*.png。测试进程退出，八项隔离原始配置／MOD／包逐项恢复并哈希匹配，见 evidence/dialogue-button-inset/restore-proof.json。没有操作正式世界、修改故事、资源生命周期或 Studio。

权威 JAR：E:/Java/MinecraftMod/DarkGreyRPG/dist/darkgrey_rpg-0.3.3.4.jar

- 版本：0.3.3.4
- 大小：1,800,727 字节
- SHA-256：4BE0EB456D27D8DE709C32FE24980D11002F74786CCBCADE42B125F964DFC43E
- 构建、实机候选和 dist 哈希一致，客户端／服务端配套使用。

USER_ACCEPTED=NO；RELEASE_READY=NO。不提交或推送。此前媒体、旧任务、字体和组合验证的未覆盖项继续保留，本轮仅验证按钮小幅平移。
