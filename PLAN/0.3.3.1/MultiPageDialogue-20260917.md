# 0.3.3.1 多句台词、条目拖拽与特殊节点配色

日期：2026-09-17。此文记录实现与验证证据，不代表用户验收或 GitHub Release。

## 已实现

- 一个台词节点包含有序的多句台词，共用说话角色；每句单独设置文本、头像变体、语音、音量和自定义速度。
- “台词文本”右侧 `+` 新增并聚焦；每句卡片提供拖拽手柄、序号和 `−`，至少保留一句；“本句设置”默认折叠。
- 新句使用默认头像、关闭语音和自定义速度；旧单句完整迁移到第一句。原有多个台词节点不会自动合并。
- 台词、选择、结算共用手柄拖拽机制：系统拖动阈值、插入线、滚动区边缘自动滚动；Esc 或无效区域放开取消，成功放下一次提交撤销历史。
- 选择和结算使用相同紧凑条目组件，保留原条目和端口身份；删除继续使用原确认机制，结算仍按列表顺序决定优先级。
- 上传前调整：选择和结算的拖拽手柄、删除按钮与对应文本框垂直居中，节点内和 Inspector 共用此布局。此最后调整通过 Release 发布构建，未另行进行实机复测。
- 任务表头 `#F5B53D`、会话 `#65C3AD`、故事 `#E58A83`，统一深色标题文字；其他节点仍使用主题蓝色，端口与连线含义不变。
- 客户端每次显示一句；最后一句完成后才进入下一节点。换句更新播放序号，切换语音、头像和逐字状态；进入选择保留最后一句内容。
- 会话快照保存 `line_page_index`；旧快照缺字段按第一页处理。动作携带播放序号，防止同节点内重复点击消息造成跳页。
- 所有句子的语音参与导出和媒体引用收集；角色默认头像及所有变体仍按原规则打包。缓存数量、并发和连线预加载规则未改变。
- 无效句子数据在编辑、复制、导出入口校验，失败不提交半成品。

## 数据接口与兼容

节点级保留 `speaker_actor_id`；新增非空数组 `pages`，每项包含唯一非空 `page_id`、`text`，以及可选 `portrait_variant`、`voice_ref`、`voice_volume`、`text_speed`、`custom_text_speed`。

语音仍是项目内 OGG 引用，音量范围 0–1。显示速度范围 0–120，0 为立即显示；未启用 `custom_text_speed` 时跟随玩家全局设置。新增页默认音量 1、速度 30、自定义速度关闭。

新版兼容读取旧节点级单句字段，保存和导出以 `pages` 为准；存在 `pages` 时不使用旧单句字段。新格式需要本次配套新版模组，不能向旧模组承诺兼容。资源封装版本及已有故事身份不变。

`CanonicalSessionSnapshot` 增加零基句序；NBT 增加可选整数 `line_page_index`。`CanonicalSessionAction` 新增可选播放序号扩展；新版客户端发送序号，服务端拒绝过期序号，多句节点拒绝无序号继续动作。

## 自动化验证

最终 Studio WPF：587 通过、0 失败；Core：453 通过、12 跳过、0 失败。包含最后的数据保护和拖拽命中修复。日志保存在 `evidence/MultiPage`。

Java 构建和以下探针通过（`.tmp/multipage-java-final.log`）：

- `canonicalSessionRuntimeProbe`：三句顺序、逐句配置、最后进入选择、第二句快照恢复、空数组和重复 ID 拒绝。
- `canonicalSessionInstanceProbe`：NBT 句序往返、旧字段缺失默认第一页、错误类型与负句序拒绝。
- `canonicalSessionClientModelProbe`：同节点播放序号变化更新头像与逐字状态，重复同步不重置。
- `canonicalSessionNetworkCodecProbe`：播放序号消息往返。
- `canonicalSessionServerServiceProbe`：重复继续动作不跳过下一句。
- `sessionPresentation0330Probe`：同节点换播放序号的音频切换与单次播放。
- `dgrsMediaLifecycle0331Probe`：后续句媒体引用、去重、旧格式、缺失声明及现有缓存生命周期回归。

Java 媒体探针中的 reparse-root 平台检查报告 `SKIP_UNAVAILABLE`，不计为通过。Core 跳过项保留日志记录，不宣称全部覆盖。

## 正式产物

模组：`E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.1.jar`

- 大小：1,247,601 字节。
- SHA-256：`FC95E09ED1D2D054274F7739A326AEE8D581D0D6BE1A652A88BEAB7D865E9575`。
- 已由 `assemble` 构建并复制到正式目录。

Studio：`E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`

- ProductVersion：`0.3.3.1`；Windows x64、自包含 Release。
- 大小：142,089,430 字节。
- SHA-256：`02269A28DF8FF07658EA153C05048046757BD1847FFACF0163FC35217051B565`。

## 独立实机验证

Studio 使用独立 `MultiPageStudio` 项目，Windows UI Automation 定位控件并使用 Win32 鼠标和键盘操作。新增、删除、首尾排序、Esc 取消、撤销重做、保存后重开、节点内与 Inspector 同步均执行。选择排序保留 flow_port_id；结算排序前后 connections JSON 相等。

长列表实测暴露出卡片间隙无法命中拖放的问题，已修复列表背景命中与子元素 DragLeave 判断。修复后按住第二句在 Inspector 底部 4.5 秒，视口滚动超过 1000 像素，保存后该句成为第八句，其余 ID 保持；随后拖出 Inspector 放开，保存顺序不变。深浅主题检查了任务、会话、故事表头和缩放后的显示。

Minecraft 由 IDEA `1. Run Client` 启动独立 `MultiPageClient`，新建 `DGRMultiPage20260917` 世界。游戏线程验收接口调用真实 NPC 交互网络与 GUI 鼠标事件，读取实际客户端状态，13 项观测断言通过：三句同节点翻页、全局速度及 0/120 自定义速度、点击补全、逐句头像与音频音量、第二句退出重进恢复、重复同步及 GUI 缩放不重置、最后一句进入选择保留文本头像、所有句子媒体预加载。IDEA 临时参数已恢复，独立游戏正常关闭。

证据入口：

- [Studio 原生操作记录](evidence/MultiPage/Studio/native-checks.md)
- [Minecraft 实机记录](evidence/MultiPage/Minecraft-live-verification.md)
- [实机断言](evidence/MultiPage/live-assertions.json)

验证范围为独立单机夹具和列出的自动化检查；音频验证依据真实音频引擎状态，未进行人工听音、多人数远程服务器或长期稳定性验收。游戏使用独立准备的夹具，尚未完成“Studio 导出多句多语音故事包，再将同一个导出包载入 Minecraft”的端到端实机验证。

按用户要求上传当前代码与验证记录至 `codex/0.3.3.1` 分支；分支上传不代表用户验收或 GitHub Release。
