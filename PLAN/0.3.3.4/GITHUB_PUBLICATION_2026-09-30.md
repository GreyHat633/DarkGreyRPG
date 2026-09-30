# 0.3.3.4 GitHub 发布快照 · 2026-09-30

用户本轮明确授权上传 GitHub。目标 origin=https://github.com/GreyHat633/DarkGreyRPG.git，分支 codex/0.3.3.4，基线 641c0f3316356fb57dd9f80d3afa00b0bc003f54。

范围：当前版本的 Studio／MOD 源码、测试、构建任务、容量 schema／生成脚本、项目 skills、施工计划与修正记录，以及当前构建依赖的上一轮未提交实现。共 236 个审计路径：{'Build/schema/skills': 7, 'Documents': 37, 'MOD': 124, 'Studio': 68}。包括连续台词编辑、动态／资源名称引用、重复条件、OP 只读诊断、物品图标、媒体延迟修复、旧任务回顾和对话自适应／字体／留白修正。

不包括历史 PLAN 删除、.codex/config.toml、运行与测试目录、缓存、截图／日志／TRX 或 dist 二进制。保留其本地状态。没有 broad add、reset、clean、rebase 或 force push。

## 验证与边界

最新按钮修正：assemble、dialogueFontScaleProbe、canonicalSessionClientModelProbe、dialogue0333Probe 通过；Windows 原生隔离实机检查两个尺寸和按钮点击，八项隔离文件恢复。其他批次验证按各自报告，不将它们表述为本轮重新完整验收。

提交前检查显式路径清单、暂存差异、空白问题、凭据模式及本地路径／大文件；提交后以本地 HEAD、origin/codex/0.3.3.4 和 git ls-remote 三者一致为发布证明。本地证据清单与推送核验保存在 .tooling/0334/Publication，GitHub 最终提交值由 Git 历史记录，不在提交内自引用。

本次仅提交和非强制分支推送，不创建 PR、合并、标签、GitHub Release 或上传二进制。旧报告中的“未提交／不推送”描述是其施工时边界，本次新授权仅改变发布状态。USER_ACCEPTED=NO；RELEASE_READY=NO；C10/C12、媒体压力／听感及其他未覆盖项按 Acceptance.md 和各修正记录继续保留。

## 本地交付文件（不随本次源码推送上传）

- Studio：dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe，0.3.3.4，142,501,590 字节，SHA-256 EDACAC74C138CBFFD44DB155AC97BC50BD7CBAF3E9EDDC2F7865A4A43F7D8D70。
- MOD：dist/darkgrey_rpg-0.3.3.4.jar，0.3.3.4，1,800,727 字节，SHA-256 4BE0EB456D27D8DE709C32FE24980D11002F74786CCBCADE42B125F964DFC43E。

最新局部修正见 Dialogue_Button_Inset_2026-09-30.md 和 Portrait_Inset_2026-09-30.md；其余批次见当前版本目录。上一轮留声机等既有实现仅作为当前快照依赖上传，本轮未修改其生命周期。
