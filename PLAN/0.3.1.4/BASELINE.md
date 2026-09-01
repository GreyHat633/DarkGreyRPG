# DarkGrey_RPG 0.3.1.4 — 开工基线（Gate 0C）

记录时间：2026-09-01 19:47:16 +08:00

## Git 基线

```text
git branch --show-current
codex/0.3.1.4

git rev-parse HEAD
b855abae465edb05b35655b8658c73a402ae912f

git merge-base codex/0.3.1.3 HEAD
b855abae465edb05b35655b8658c73a402ae912f
```

结论：当前施工分支从真实 `codex/0.3.1.3` HEAD 原位创建；未从 0.3.1.2B 或旧 Development Report 反推代码。

开工时 `git status --short`：

```text
 M .codex/config.toml
 M AGENTS.md
 M PLAN/0.3.1.2B/DEVELOPMENT_REPORT.md
?? .dotnet-home/
?? .nuget-packages/
?? PLAN/0.3.1.1审计.docx
?? PLAN/0.3.1.3审计.docx
?? PLAN/0.3.1.4/
?? PLAN/DarkGrey_RPG_0.3.1.1_Studio_Repair_Construction_Plan.md
?? PLAN/DarkGrey_RPG_0.3.1.4_Studio_Regression_Repair_Construction_Plan.md
?? PLAN/DarkGrey_RPG_Studio_2.1.3_Plan.md
?? PLAN/~$3.1.2审计.docx
?? PLAN/~WRL0753.tmp
?? PLAN/剪刀鼠标箭头.jpg
?? PLAN/审计清单.docx
?? PLAN/构思.docx
?? PLAN/验收清单_0.3.0.0_0.3.1.0.md
```

以上均按用户已有脏工作处理；0.3.1.4 施工不得 reset、clean、覆盖或误提交无关项。

## 环境

- .NET SDK：`10.0.302`，固定入口 `E:\Java\dotnet-sdk-10\dotnet.exe`
- Windows：Microsoft Windows 10 家庭中文版 `10.0.19045`，Build `19045`，64 位
- CLI Home / NuGet / TEMP / TMP：仓库 E 盘 `.tooling/wpf-build` 隔离目录
- `windir`：构建时显式复用 `SystemRoot`

## Release build

命令：

```powershell
$env:DOTNET_CLI_HOME='E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\wpf-build\dotnet-home'
$env:NUGET_PACKAGES='E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\wpf-build\nuget-packages'
$env:TEMP='E:\Java\MinecraftMod\DarkGrey_RPG\.tooling\wpf-build\temp'
$env:TMP=$env:TEMP
$env:windir=$env:SystemRoot
& 'E:\Java\dotnet-sdk-10\dotnet.exe' build '.\studio\src\DarkGreyRPG.Studio\DarkGreyRPG.Studio.csproj' -c Release --no-restore --nologo
```

结果：`PASS`，2026-09-01；0 warning / 0 error；elapsed 1.03 s。

## 自动化基线

| Suite | 结果 | Passed / Total | TRX |
|---|---|---:|---|
| Core | `PASS` | 354 / 354 | `evidence/baseline/Core/Core-0.3.1.3-baseline.trx` |
| WPF | `PASS` | 383 / 383 | `evidence/baseline/Wpf/Wpf-0.3.1.3-baseline.trx` |

TRX 时间范围：Core 2026-09-01 19:24:46–19:24:48 +08:00；WPF 19:24:58–19:25:05 +08:00。自动化基线只证明测试集合，不替代真实 Release EXE 交互。

## 开工时权威交付物身份

- 路径：`E:\Java\MinecraftMod\DarkGrey_RPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe`
- ProductVersion：`0.3.1.3-rc`
- FileVersion：`0.3.1.3`
- 文件大小：141,411,030 bytes
- SHA-256：`B3C5F282075376899C296E7601739C67BD0ADCA8A08100C9EF12A3A18C1F7924`

这是修复前 0.3.1.3 真实复现对象，不是 0.3.1.4 候选。
