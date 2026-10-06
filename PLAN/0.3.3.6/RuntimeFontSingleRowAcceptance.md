# Runtime 字体清晰度与资源单行布局验收记录

日期：2026-10-06。版本保持 **0.3.3.6**。本轮仅调整 Runtime 展示及相关 Probe，保留工作区已有改动。

## 交付文件

- 文件：`dist/darkgrey_rpg-0.3.3.6.jar`。
- 大小：**1,981,658 字节**。
- SHA-256：`DE9DF67E86499F6D121EAAA0885C6F021784C78F97C810B35E59653A494B2BE0`。
- JAR 内 `mcmod.info` 版本：`0.3.3.6`。
- 构建产物、dist 文件、最终隔离客户端与服务端加载的 JAR，大小及 SHA-256 完全一致。
- 结构化记录：[RuntimeFontSingleRowArtifact.json](evidence/RuntimeUi/RuntimeFontSingleRowArtifact.json)。

## 实现结果

实体、物品指名器与故事包管理目录移除原先的分数倍字号，统一使用 1.0 倍原始字体。共同文字入口委托给任务界面使用的 `CanonicalDialogueRenderer.drawText` / `DialogueFontDrawing`，沿用像素对齐和字体纹理采样恢复。测量、截断和状态宽度均使用原始 `FontRenderer` 的实际字宽。

目录与资源行统一为 22 逻辑像素，文字垂直居中。标题和文件夹保持加粗、主题色、图标与层级导线。管理状态仍在标题右侧，原始字号重新计算预留宽度；窄侧栏显示状态标记，悬停提供名称、完整状态以及成员继承说明。悬停文字也采用同一字体入口，并换行及限制在屏幕范围内。

资源名称、`[引用]` 与来源统一在同一基线绘制。普通自有资源不显示来源；引用资源右端显示 `来源：故事名称`；全局搜索中的自有资源右端显示 `所属：故事名称`。来源最多占内容宽度的 40%，保留至少 64 逻辑像素的名称预算和 8 像素间隔。长内容使用省略号，截断时优先保留引用标记，悬停显示完整内容。实体与物品复用布局，绘制、点击和滚动使用同一行高及当前滚动偏移。

未修改任务、对话或全局字号设置，未修改流程图画布变换，未修改网络协议、故事包和存档格式。没有自动改写原故事包，也没有生成成品发布包或更新 Studio。

## 自动测试

最终执行：

```powershell
.\gradlew.bat --no-configuration-cache --console=plain `
  -I .tooling/0336-runtime-ui/font-format.gradle `
  spotlessJavaApply runtimeFontResourceRows0336Probe runtimeInteractionUi0336Probe `
  currentNominatorWireProbe smoothScroll0324Probe utilityWindow0324Probe build
```

结果：**BUILD SUCCESSFUL**。相关 5 个 Gradle Probe 任务通过，其中字体与资源行 Probe 同时执行 `DialogueFontScaleProbe`，共 6 个 Probe 入口通过。Spotless、Checkstyle、test、JAR 构建及 reobfJar 完成。日志：[FontFinalBuild.log](evidence/RuntimeUi/FontFinalBuild.log)。

| Probe | 结果与覆盖 |
|---|---|
| RuntimeFontResourceRows0336Probe | 使用已有真实字体 advance 表的 normal / unicode 两种模式，检查 0～640 像素宽度、长中英文名称、数字、长来源、加粗省略号、40% 来源限制、右对齐、64 像素预算、8 像素间隔与引用标记保留；覆盖四类资源、同名资源身份、引用与所属故事、37 行滚动及恢复 |
| DialogueFontScaleProbe | 通过；像素对齐、原始字号容量和既有字体行为 |
| RuntimeInteractionUi0336Probe | 通过；目录、引用、身份、状态恢复、分块传输、扫描与权限，及既有 NPC 仲裁回归 |
| CurrentNominatorWireProbe | 通过；现有目录协议往返及资源身份校验 |
| SmoothScroll0324Probe | 通过；滚动、命中、边界限制及刷新 |
| UtilityWindow0324Probe | 通过；窗口尺寸和交互几何 |

额外执行的 `dialogue0333Probe` 仍在 `shared profile geometry including rounded quote gutters` 断言失败。这是上一轮完整回归日志中已有的相同失败，不属于本轮字体或单行布局新增问题。失败记录：[FontKnownDialogue0333Failure.log](evidence/RuntimeUi/FontKnownDialogue0333Failure.log)。本轮没有重新执行完整 95 项 Probe，不能据此宣称完整 Runtime 回归全部通过。

## 代理实机

使用隔离 Minecraft 1.7.10 客户端与服务端、复制的测试存档和现有导出包，以 Win32 原生输入和截图操作验证。没有对用户原存档或原导出包做写入。最终文件已分别在 **WHITE / GUI 2 倍 / 1280×800** 与 **CHARCOAL / GUI 4 倍 / 1900×960** 下加载，Runtime 字号设置均为 **100%**。

| 场景 | 实测结果及截图 |
|---|---|
| 与任务界面对照 | 原始中文、英文 A/B、数字和加粗目录字形清晰；[最终任务界面 4 倍](evidence/RuntimeUi/141-Font-Task-Final-Gui4.png)、[最终任务界面 2 倍](evidence/RuntimeUi/159-Font-Task-Final-Light-Gui2.png) |
| 管理目录状态 | 标题与状态同排；宽侧栏状态文字和窄侧栏继承说明正常；[深色同排状态](evidence/RuntimeUi/144-Font-Manager-Final-InlineStates.png)、[深色最小窗口继承说明](evidence/RuntimeUi/142-Font-Manager-Final-Inheritance.png)、[浅色状态和继承说明](evidence/RuntimeUi/158-Font-Manager-Final-Light-Inheritance.png) |
| 同名自有／引用资源 | 同名测试员的两条内部身份保留，所属／来源分别显示在行右端；[深色全局搜索](evidence/RuntimeUi/149-Font-Global-Final-Gui4.png)、[浅色全局搜索](evidence/RuntimeUi/165-Font-Global-Final-Light-Gui2.png) |
| 单行与滚动命中 | 引用资源和自有角色组相邻，行高一致，无第二行空白；右侧滚动不改变左侧目录偏移，滚动后可正确选中；[深色混合行和选中](evidence/RuntimeUi/153-Font-Mixed-Final-Selected.png)、[浅色滚动后角色／角色组](evidence/RuntimeUi/166-Font-Actors-Final-Light-Gui2.png) |
| 完整悬停内容 | 引用名称、标记和来源完整显示，同一原始字体；[最终来源悬停](evidence/RuntimeUi/154-Font-Reference-Final-SourceTooltip.png) |
| 物品／物品组 | 铜币和剑按 22 像素连续排列，自有资源不显示来源，不产生空白第二行；[最终物品指名器](evidence/RuntimeUi/162-Font-Items-Final-Owned-Gui2.png) |
| 最小窗口与主题 | 实体 300×200、管理 300×180 逻辑像素下字号、状态预留、来源对齐及裁切正常；最终深浅主题截图见以上记录 |

长名称、长来源以及比正常最小窗口更窄的布局预算由自动 Probe 验证，本轮没有在实机中另造长名称导出包。四类资源已用真实包在实机显示；物品引用与长文本组合由自动目录夹具覆盖。

最终实机字体诊断在两种 GUI 缩放下均记录 `requested=1.0 effective=1.0 nearest=true filtersBindingBlendRestored=true`，以及 `linearRestore=true`：[4 倍客户端日志](evidence/RuntimeUi/FontNativeFinalClient.log)、[2 倍客户端日志](evidence/RuntimeUi/FontNativeFinalGui2Client.log)。既有 Forge 版本查询、缺少本地 Project 文件和 CustomNPC 资源警告保留在日志，故事包实际加载正常；未发现本轮绘制引入的异常。隔离客户端及服务端已关闭。

## 验收状态

- 自动测试：本轮相关 Probe 与构建通过；额外对话几何 Probe 的既有失败单独记录。
- 代理实机：上述已测场景通过，使用的最终候选与 dist 哈希一致。
- 用户验收：待用户使用更新后的 Runtime 确认观感。
