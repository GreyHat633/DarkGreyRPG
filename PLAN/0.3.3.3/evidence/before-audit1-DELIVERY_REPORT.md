# 0.3.3.3 开发交付与人工验收交接

2026-09-23：按原始 Construction PLAN 复核37项需求，计划内实现与开发者检查已完成。剩余为用户实际听感和最终界面/交互验收；Windows系统DPI测试按用户要求豁免。

ENGINEERING_IMPLEMENTED=YES；DEVELOPER_PLAN_CHECKS=COMPLETE；USER_ACCEPTED=NO；CONSTRUCTION_COMPLETE=NO_UNTIL_MANUAL_ACCEPTANCE；RELEASE_READY=NO。

## 固定交付产物

| 路径 | 版本 | 字节 | SHA-256 |
|---|---|---:|---|
| `E:\Java\MinecraftMod\DarkGreyRPG\dist\DarkGreyRPGStudio\DarkGreyRPGStudio.exe` | 0.3.3.3 | 142429910 | `bbe9d7b46ab6a1f35631bb427e5838e6ee1e87c75286c2054f1ebc0ddf93aa22` |
| `E:\Java\MinecraftMod\DarkGreyRPG\dist\darkgrey_rpg-0.3.3.3.jar` | 0.3.3.3 | 1695888 | `c5131dd02f38c57944916059549a955ba28890885476f49c7693d8aaf3320869` |

Studio是自包含Windows x64 Release，以上固定EXE为唯一用户运行入口。本轮Studio只增加测试文件，产品未再修改，既有固定EXE保持有效。最终JAR补修留声机显示范围时窗口底板过透明导致下层正文干扰的问题，正式客户端已复验。

构建基线702a096ddf7523867d545c02cb9d7bd915036d3b，分支codex/0.3.3.3，产物来自未提交工作树。SOURCE_SNAPSHOT.json记录89个源码/构建文件的SHA-256。未提交、push、上传或创建Release；未处置无关脏文件。

## 核验结论

- 37项逐项对应实现和证据，见[需求追踪](REQUIREMENTS_TRACEABILITY.md)。冻结的Story/Session/Task Runtime和奖励事务核心未改动。
- 最近Core全量462 PASS / 12 SKIP；WPF全量605 PASS。之后新增真实可见WPF渲染采样1项PASS，不冒充606项全量重跑。原有SKIP不计通过。
- 本轮重跑对话状态/分页及任务奖励恢复探针，并补多设备＋试听＋Session音量、跨维度/跨服范围恢复、300节点渲染间隔和实际拖动IO证据。四源调量使用临时集成夹具，原生试听按钮真实操作，不能冒充通常UI入口。
- 同包完整链路和双客户端证据保留原产物/时间边界。最终JAR仅窗口底板修复，不宣称历史全部测试在最终哈希再次运行。
- 性能测量是WPF Rendering回调和实际进程IO，不冒充GPU帧呈现或恒定60FPS。系统WPR并非计划要求的唯一方法。

详细纠偏、测试结果与已知限制见[本轮复核报告](PLAN_RECONCILIATION_0923.md)。GUI1 Unicode基线可读性、透明底板低对比、300组折叠长尾、历史NetworkManager跨服异常归因不确定均保留披露。

## 交接

仅保留[人工待验清单](CURRENT_ACCEPTANCE_GAPS.md)：QQ/网易云/本地实际听感，以及界面、阅读、组合操作是否符合用户预期。不再扩大成全部布局笛卡尔积或重复重演所有自动用例。测试进程已正常关闭，临时诊断代理不包含在产品JAR。

历史交付叙述保存在evidence/delivery-before-plan-reconciliation.md；其旧哈希及待办不代表当前状态。
