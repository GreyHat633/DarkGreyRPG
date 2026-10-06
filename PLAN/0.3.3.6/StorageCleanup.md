# DGR 文件清理记录

2026-10-05；按用户要求继续清理。用户已确认，前轮回收站中消失的 9 个暂存目录是其手工清空回收站所致。

仓库由约 **65.07 GiB** 降至 **19.31 GiB**；本轮通过 Windows 回收站接口处理 **45.76 GiB**，共 96 批。根目录小文件及审计记录变化不计入分类总量。

主要原因：历史发布与实机验收留下 86 份 Studio 副本，程序载荷合计 36.57 GiB；每份自包含副本约 0.55 GiB。发布脚本每次创建 portable-publish 和 organized-publish 两份暂存，验收还会建立独立运行副本。旧 WPF 超时转储重复保存两份，每份约 2.86 GiB。

## 本轮处理

| 类别 | GiB |
| --- | ---: |
| Superseded Studio | 36.016 |
| Historical WPF timeout dump | 5.713 |
| Historical Studio startup dump | 0.022 |
| Duplicate Gradle cache | 2.671 |
| Expanded JDK 8 download archive | 0.101 |
| Expanded JDK 17 download archive | 0.271 |
| Expanded JDK 21 download archive | 0.284 |
| Rebuildable obsolete Debug output | 0.515 |
| Generated configuration-cache HTML reports | 0.170 |

85 份闲置 Studio 的程序文件已处理，保留其 Data、项目、日志、脚本及证据。仍有进程占用的 `.tooling/0336-ui-reform/Studio` 被跳过。旧 Debug 输出仅处理不含 Data/Projects 的三个目录；WPF Debug 因含 Data 而保留。正式 dist、运行世界与项目、源码、Git 数据、当前开发依赖及已交付成品均保留。

## 校验

- 4767 个保留文件进行 SHA-256 比对；变化/缺失：0。
- Git 已跟踪文件状态差异：0。现有工作区改动保持。
- 正式 Studio：0.3.3.6，EXE 204288 字节；EXE SHA-256 `8e8bf253b16b1516616d890b8bd604a907cf9d607469e3785d4ab113a5b3b20c`，Studio DLL SHA-256 `b7300e1a6b54aaba5f62cac77918d872f1b8ed11b19fdccdb44004fc0027c2e9`。
- 清理接口无报错，全部来源已移出仓库。最终在回收站找到 95 批可恢复负载；重复 Gradle 缓存一批已不在回收站，无法确认是手动清理或系统处理。该缓存可由构建重新生成，主 Gradle 缓存及已解压 JDK 保留。
- 本次未清空回收站。仓库体积减少与磁盘释放空间不同：仍在 E 盘回收站中的文件需用户清空后才释放磁盘空间。

## 记录

[清理汇总](../../.tooling/cleanup-20261005/summary.json)、[逐项原路径清单](../../.tooling/cleanup-20261005/recycled.json)、[保留文件校验](../../.tooling/cleanup-20261005/protected-audit.json)、[回收站负载核对](../../.tooling/cleanup-20261005/recycle-bin-audit.json)。
# 本轮发布暂存清理补充

2026-10-05 Task UI 修正交付后，8 个本轮 `portable-publish-*`／`organized-publish-*` 暂存目录通过 SendToRecycleBin 移入回收站，合计 4,748,393,164 字节，原路径均已消失。未清空回收站，未触及 Data、项目或主缓存。记录见 [evidence/TaskUIFix/recycled-staging.json](evidence/TaskUIFix/recycled-staging.json)。
