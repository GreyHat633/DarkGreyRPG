# DGR 0.3.3.1 开工记录

日期：2026-09-14。用户授权：开始 DGR 0.3.3.1 施工。需求依据为 `PLAN/DarkGrey_RPG_0.3.3.1_Construction_PLAN.md`；文档中的历史讨论和验收目标不当成已取得的结果。

```text
BASELINE=64dd990484a8cf2639013a41a7d4fe960e6b7409
BASELINE_TREE=dba507aab1d64bf3342172163354acda701b0f47
BRANCH=codex/0.3.3.1
CONSTRUCTION=IMPLEMENTED_AND_DELIVERED
FULL_ACCEPTANCE_MATRIX_COMPLETE=NO
USER_ACCEPTED=NO
```

源码 src/studio 开工时无未提交修改。已有 `.codex/config.toml`、AGENTS.md 修改、历史 PLAN 文件删除及未跟踪文件全部保留。初始完整清单见 `evidence/baseline/worktree-before.txt`。未执行远端发布。

## 基线验证

| 检查 | 结果 | 证据 |
|---|---|---|
| 完整 Runtime build（包含格式和静态检查） | PASS | evidence/baseline/runtime-build.log |
| Core Release | 440 PASS、3 SKIP、0 FAIL | evidence/baseline/core.trx |
| 两项媒体导入测试补齐 DGR_MEDIA_TOOLS 后复跑 | 2 PASS | evidence/baseline/media-core.trx |
| WPF Release | 497 PASS、0 FAIL | evidence/baseline/wpf.trx |
| 10 个相关 JavaExec 探针 | PASS | evidence/baseline/probes.log |

Core 尚跳过 `B3KillSlimesCopyMigratesReopensAndExportsWithoutChangingSource`：需要显式外部迁移 fixture；跳过不计通过。

JavaExec：task0330Probe、taskReward0330Probe、storyAction0330Probe、mediaPackage0330Probe、mediaTransfer0330Probe、sessionPresentation0330Probe、title0330Probe、canonicalStoryRuntimeProbe、canonicalStoryServerServiceProbe、canonicalTaskRuntimeProbe。并未声称所有历史探针均已执行。

实际工具链：`E:/Java/jdk-25.0.1` 启动当前 Gradle（与 gradle.properties 一致），`GRADLE_USER_HOME=<repo>/.gradle-user-home`；.NET 使用 `E:/Java/dotnet-sdk-10/dotnet.exe`，CLI Home/NuGet/temp 均为 `<repo>/.tooling/wpf-build` 下路径。Java temp 为 `<repo>/.tooling/temp`。沿用现有依赖，未安装 SDK。

## 数据与产物保护

11 个既有项目、存档及包目录已复制到 `<repo>/.tooling/0.3.3.1/backups`；准确源路径、备份路径、文件数及大小见 `evidence/baseline/backups.json`。457 个文件逐一 SHA-256 比对，差异 0，见 `evidence/baseline/backup-hashes.csv`。这是开工文件副本，未执行开发格式转换；不将热存档副本宣称应用一致性快照。

开工时权威 Studio 的原版本、大小、SHA-256 已记录于 `evidence/baseline/studio-artifact.json`。首批交互预览现已提升到权威目录，完整原目录备份位于 `.tooling/0.3.3.1/studio-before-wpb`。预览版本、哈希和范围见 `DELIVERY_WPB_PREVIEW.json`；不代表整版完成。

## 范围与分工

按 WP-B → C/D 契约 → A → D/E UI → F 媒体与最终集成推进。A01—A25、D01—D10 全部列于 AUDIT_TRACEABILITY.md，未实现项保持 OPEN。节点作者能力清单与受影响符号表同目录。

CAS Delegation Capability Preflight：DispatchReady=true，Native cas_luna_worker。主负责人持有基线、架构和最终集成；首个 Worker 只读调查 A11/A18/A19，禁止将静态分析代替真实鼠标交互证据。

冻结 Task 内 Logic、身份语义、服务器权威任务缓存与会话恢复基础。跨 Story、任务提交和媒体仅按本版明确变化扩展；不增加第二 Runtime、固定 Story Flow 输入、旧 EnterStory 替代入口或远程任意提交。
