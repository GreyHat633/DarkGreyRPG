# 重复设置卡片与连续高度过渡

已实现并更新权威 Studio。USER_ACCEPTED=NO，RELEASE_READY=NO；未提交或推送。

- 外部标题“重复设置”；灰色卡片内部为“重复条件”、四种模式下拉框及必要详情，沿用奖励卡片的边框、背景、圆角和内边距。节点与 Inspector 共用组件。
- 删除模式切换时高度归零再展开的逻辑。根据内容自然高度，从当前显示高度直接过渡到目标高度；变短向上收缩，变长向下延伸。顶部固定，文字不缩放；220 ms CubicEase/EaseInOut，中途改选从动画当前高度继续。定时规则内部字段增减也走同一高度过渡。初始化与系统禁用动画时直接布局。
- 新增高度过渡回归覆盖增长、收缩、快速反向、归零、年周期额外字段；相关布局及既有连线动画回归共 9 项通过，见 evidence/repeat-height-regression.log。
- Windows 原生 UIA/Win32 在隔离 FourUiProject 操作：节点切到定时重复、Inspector 切到不可重复，双入口同步和卡片布局已读回。证据 evidence/native-ui/repeat-card-open.png、repeat-card-scheduled.png、repeat-card-once.png。动画中间高度由自动测试验证；不以静态截图声称完成主观动画验收。
- 仅本次 Studio 展示变化；原有版本待验项继续保留。

权威产物：`E:/Java/MinecraftMod/DarkGreyRPG/dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe`

- 自包含 Windows x64 Release，ProductVersion `0.3.3.4`
- 大小 `142501590` 字节
- SHA-256 `EDACAC74C138CBFFD44DB155AC97BC50BD7CBAF3E9EDDC2F7865A4A43F7D8D70`
- 已与发布候选逐字节哈希核对；用户原先运行的 Studio 未关闭，需重启加载新版本。
