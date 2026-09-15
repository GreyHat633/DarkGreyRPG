# Studio 0.3.3.1 媒体 UI 修复

## 本次修改

- 角色编辑器统一为“表情差分”与“导入”；导入使用文件名（不含扩展名），重名按 `(1)`、`(2)` 递增。名称框仅在重命名时出现，支持 F2、Enter 确认与 Escape 取消。弹窗复用同一编辑器。
- 台词字段顺序为说话角色、头像及预览、台词文本、语音/音效。节点内与 Inspector 共用相同头像预览。
- 角色媒体更新实时通知既有台词编辑器，保留已选择的表情，不产生额外图编辑历史；资源匹配与哈希保持稳定，避免影响角色选择和资源拖放。
- 语音/音效使用左右对称的导入/移除按钮，播放器为 -5s / ▶或暂停 / +5s，进度条位于下方。移除原先重复的试听、清除、无媒体状态说明。
- 画面预览按游戏的 CanonicalDialogueLayout 展示独立选项按钮，采用 DgrUiPalette 的不透明深灰按钮背景。预览覆盖层不拦截图片编辑。
- 数值拖动每次移动更新临时画面，松开只提交一次撤销记录；Escape 恢复原状态。

## 验证

- WPF 全量：544 通过、0 失败。最终颜色/裁切调整后的相关测试：9 通过、0 失败。
- 实际运行固定 dist EXE，使用独立 `.tooling/0.3.3.1/media-ui-project`。
- 同一图片连续导入产生 `expression` 与 `expression(1)`；F2 将第一项改名为 `smile_live`，无需预填名称。
- 未重启且未重新加载项目，打开台词下拉框即可看到新增及重命名的表情；选中后节点与 Inspector 同步显示新图片。
- 实际点击播放变为暂停且显示 30 秒时长；进度条跳转至 15 秒，+5s 为 20 秒，-5s 回到 15 秒。
- 鼠标按住拖动时位置 X 从 0.5 变为 0.66，截图中图片横坐标从 1632 移至 1681；松开后一次 Ctrl+Z 恢复为 0.5。
- 保存并重启，表情及台词选择仍保留。

截图：
- `evidence/live/media-expression-import-rename.png`
- `evidence/live/media-portrait-live-options.png`
- `evidence/live/media-line-portrait-audio-layout.png`
- `evidence/live/media-audio-seek.png`
- `evidence/live/media-screen-before-drag.png`
- `evidence/live/media-screen-during-drag.png`
- `evidence/live/media-screen-final-preview.png`

## 交付

- EXE: E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe
- ProductVersion: 0.3.3.1
- Bytes: 141939926
- SHA-256: 232DF27F0F6A5EC0F4C03FD51BAA8FE56E9890FECC803C9812B2D4BD8CFD3D81
- 自包含 Windows x64 Release，已核对候选与 dist 的 SHA-256 一致。
- USER_ACCEPTED=NO。本次记录不代表用户验收，也未执行 GitHub 推送。