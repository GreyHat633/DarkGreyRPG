# 对话头像左侧留白修正 · 2026-09-30

状态：IMPLEMENTED / AGENT_VERIFIED；USER_ACCEPTED=NO，RELEASE_READY=NO。

## 修改

头像在原位置向右移动 3 个 GUI 单位（GUI 因子 2／4 时为 6／12 屏幕像素）。名字列及长名字悬停区域同步移动，名字仍居中于头像。头像图片、边框与加载占位使用统一坐标。

完整分割线端点保持 left+8 与 right-8；正文起点、可用宽度、头像大小和垂直居中算法不变。头像右侧到正文起点保留 5 个 GUI 单位，避免这次微调改变分页容量。前景和后台对话共享绘制实现。

## 验证

构建、dialogueFontScaleProbe、canonicalSessionClientModelProbe、dialogue0333Probe 全部通过；涉及文件 diff --check 通过。沿用已有布局检查，不为小幅坐标变更新增镜像测试。

Windows 原生 Win32 操作隔离客户端 PID 37072（E:/Java/jdk1.8.0_471/bin/java.exe，.tooling/0334/Witness），连接隔离服务器 PID 37648（.tooling/0334/Server/font-pixel-world）。使用合成纵向测试头像，检查留白、名字居中、正文间距、分割线和按钮。

- 1280×960、GUI 因子 4、100%：inset-standard-100.png。
- 同尺寸 150%：inset-standard-150.png，名字按已有截断规则显示，未重叠。
- 原生缩小到 960×640、GUI 因子 2、150%（像素对齐 200%）：inset-small-150.png。
- 同小窗口 100%：inset-small-100.png，完整“酒馆老板”在头像上方居中。

截图位于 evidence/native-ui；设置点击前后、选择 150% 和恢复 100% 的画面也保留。检查了实际布局，未把源代码检查等同于实机美观性结论。没有重新穷尽所有主题、头像比例与加载延迟组合；上一轮相关待验项继续保留。

测试 JVM 已退出，八项隔离配置、MOD 和包恢复并逐项哈希匹配（evidence/portrait-inset/restore-proof.json）。未操作正式世界。未修改 Studio、资源生命周期、故事格式或网络协议。

## 交付

- 权威 JAR：E:/Java/MinecraftMod/DarkGreyRPG/dist/darkgrey_rpg-0.3.3.4.jar
- 版本：0.3.3.4
- 大小：1,800,727 字节
- SHA-256：28561D42DF314F5E1A9694888D0D49A382822ED31EDA9CCEE0BBA139C1C432A7
- 实机候选、构建与 dist 哈希一致；客户端与服务端使用同一 JAR。

本轮不提交、推送或标记用户验收。媒体、旧任务回顾、C10 与 C12 的未覆盖验证继续独立保留。
