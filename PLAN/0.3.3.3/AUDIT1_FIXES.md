> 追加修正：用户随后指出空白画布旧菜单遗漏，现已移除无对象操作；当前 EXE 和验证边界见 DELIVERY_REPORT.md。下文哈希及截图属于此前审计批次。

# 临时审计1 修复与复验

2026-09-23。以用户提供的临时审计1.docx的17项问题为本轮范围。前轮“开发者检查完成”已因用户审计重新打开，本轮逐项修复和复验完成，不把原先截图当作本轮修复证据。USER_ACCEPTED=NO，RELEASE_READY=NO；主观听感与最终视觉审计仍由用户确认。系统DPI未改动。

## 逐项结果

| 项 | 问题 | 修复及验证 |
|---|---|---|
| 1 | 分组标题偏小 | 标题18 DIP半粗体；原生新组及缩放截图可见 |
| 2 | 多选缺少组合菜单 | 两节点Shift多选后，右键“组合→组合”实际创建新组 |
| 3 | 组框无法取消选中 | 清除选择时同步边框；空白处和其他节点点击回到组色；WPF状态和原生边框一致 |
| 4 | 组／节点菜单不一致，编辑无意义 | 共用菜单生成；流程图仅故事／会话／任务启用；复制、粘贴、组合、删除层级一致；删除在主菜单底部 |
| 5 | 分组缺少颜色 | 六色调色板，新组随机色；组合→修改颜色；原生改紫色并保存，sidecar颜色已核对，自动验证Undo/Redo及重开 |
| 6 | Shift不能多选 | Shift与Ctrl均可增减选择；参数输入框保持原生文本编辑，拖线和Shift插线规则保留；原生Shift两节点选中 |
| 7 | 普通拖动／G拖动组框缩放相反 | 普通拖动实时测量，G拖动使用拖动开始时的冻结边界，落下再更新成员与边界；中途截图、落下、撤销均验证 |
| 8 | 第一人称留声机太小、悬空 | 恢复单位尺寸并重新定位至右下角，最终平移(0.5,1.0,0.5)；实机持有模型贴近右下缘，不再远处悬浮 |
| 9 | 范围线框跟随玩家 | 用渲染视点实体与partialTicks插值位置抵消摄像变换，范围仍来自设备世界坐标；移动视点、转向后线框固定于设备范围 |
| 10 | 设置标题贴边 | 三页签连接为等宽栏、标题居中；恢复默认增加底部留白；深色／蔚蓝实机检查 |
| 11 | 滑块预览反复回到“这” | 累积已显示进度，改变速度不重置时间；拖动时不触发预览循环重启；速度／等待两个滑块中途截图均保持完整预览 |
| 12 | ESC关闭原版音乐仍继续 | 已复现Minecraft 1.7.10写入顺序问题，客户端END tick仅在MUSIC值变化时重新同步原版分类；最终JAR实测同源gain=0.5，设0后playing=false，DGR独立偏好不影响原版 |
| 13 | 自动按钮宽度／状态 | 与记录均33×16，文字固定“自动”，选中使用较深填充与边框；实机开启后保留状态 |
| 14 | 右引号跑到面板末尾 | 按最后一行实际文本宽度定位右引号；逐字未完成时不提前画到空行；短句实机＋显示完成探针通过 |
| 15 | 历史缺引号、角色层次、玩家名 | 角色独立行，正文缩进并加「」；确认后的选择使用当前账号玩家名；正常选择回执后原生历史显示Native0333 |
| 16 | 点击继续位置／符号 | 右下角浮动轮廓倒三角；自动时省略号；实机截图验证 |
| 17 | 指名器主题黑块 | 两操作面板改用语义主题色；蔚蓝主题下原生物品指名／解绑面板正常 |

## 关键根因与边界

原版音乐并非被DGR背景音乐滑块接管。GameSettings.setSoundLevel先调用SoundHandler，再写mapSoundLevels；SoundManager回调读取的仍是旧值。修复前audit1-v2-dgr-zero.txt与audit1-v2-delayed.txt显示minecraft:music.game在设置0后仍playing=true、gain=1。修复后audit1-final-music-half.txt与audit1-final-music-zero.txt显示同一播放源gain=0.5、随后playing=false。只重同步原版MUSIC，不修改其保存值，不将Master/Music重复乘进DGR音量，不改变媒体缓存或Story Runtime。

原版音乐复现使用临时客户端诊断代理调用正式GameSettings及SoundHandler入口，并读取实际Paulscode源；不是伪造听感或普通UI操作。代理仅在隔离测试客户端，未进入产品JAR。范围移动使用测试服务器普通tp命令；短句／选择／历史由正式故事会话驱动。第一人称独立截图期间临时关闭了客户端会话展示，重启后恢复正式服务器会话；不将这种展示隔离算作剧情结束。

本轮未动Story／Session／Task Runtime及CanonicalTaskPlayerTransactions。没有改系统DPI、用户实际项目或用户游戏目录；用户原先打开的Studio进程保留，磁盘固定EXE已升级，需保存并重新打开才能使用新版。

## 验证

- WPF针对性71 PASS：包含新增Audit1GroupTests、已有GroupTransactions0333Tests与CanonicalGraphEditorViewTests。最初2个旧断言失败是预期行为由“编辑所有节点”改为“按能力开放流程图”，已修订并重跑通过；初始日志保留。
- Core针对性14 PASS：分组、剪贴板与布局相关测试。
- Java：dialogue0333Probe、canonicalSessionClientModelProbe、gramophone0332Probe及playerPreferences0332Probe通过；限定本轮文件的Spotless与最终reobfJar通过。最后界面配色／留白调整后重跑对话与偏好探针；未宣称所有历史测试在最终哈希再次全量运行。
- 最终固定Studio为自包含win-x64 Release。旧Core12 SKIP仍是历史跳过，不在本轮通过计数中；没有通过重复全量矩阵扩大审计范围。
- Computer Use初始化成功但截图连续两次报SetIsBorderRequired 0x80004002，使用用户已授权的Win32/UIA后备。300节点UIA遍历耗时后切为经进程路径核验的Win32窗口截图输入；被终止的只是本轮卡住的截图助手，没有终止用户Studio。
- Agent Switch预检及责任记录均报Scheduler未运行，本轮MAIN承担实现与验证。

## 主要实机证据

均在evidence/native-ui：audit1-ab-menu／ab-submenu／new-color-group、frame-menu-valid／color-menu／group-deselected、normal-live-mid／g-frozen-mid／g-frozen／g-undo、held-v2／range-moved、preview-speed-live-mid／preview-wait-live-mid、auto-selected／next-line／history-final／nominator-azure。中途失败或旧候选截图保留但不计为最终通过。

自动日志位于evidence/audit1-wpf-regression.log、audit1-core.log、audit1-final-java.log、audit1-delivery-java.log。

## 固定产物

| 路径 | 版本 | 字节 | SHA-256 |
|---|---|---:|---|
| `E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe` | 0.3.3.3 | 142434518 | `a3ed3f3ecb8fd2318294d9b97e08d13666dd45c6a5e66462e5b81403ab4d7b7b` |
| `E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.3.jar` | 0.3.3.3 | 1697207 | `4b643368aa6b74508d0e1ddc5de5f875d2535851c184c73b6ab369245d68d636` |

未提交、push或创建Release。本轮测试窗口与服务器已关闭。
