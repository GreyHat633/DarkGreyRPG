> 当前状态（2026-09-23最终复核）：计划内开发者检查已闭环，人工验收待完成。以[PLAN_RECONCILIATION_0923.md](PLAN_RECONCILIATION_0923.md)、[DELIVERY_REPORT.md](DELIVERY_REPORT.md)和[CURRENT_ACCEPTANCE_GAPS.md](CURRENT_ACCEPTANCE_GAPS.md)为准。以下历史日志保留各自时间及产物边界，其中旧待办与旧哈希不代表当前状态。最近WPF全量605 PASS，新增帧采样单项PASS；Core原有12 SKIP不算通过。

# 0.3.3.3 同包双客户端验证

2026-09-21。以下是实际动作及其观察结果，范围为会话启动隔离、历史隔离、本地翻页隔离和字号独立。没有据此关闭全部多人/声音验收。

## 环境及一致性

- 两个独立 Java 8/Forge 1.7.10 正式客户端目录：`.tooling/0333/Client`、`.tooling/0333/Witness`。测试账号 Native0333 / Witness0333。production CNPC 和相同正式重混淆 DGR JAR。
- 两端 JAR SHA-256 均为 `655070ec56994ef9d453c8e07939d9591b6e307339667ab4ef889c44bc701fd7`，与固定交付 JAR 相同。
- 服务端是隔离的 Gradle `runServer` 开发运行环境，使用当前源码；不是正式重混淆服务端部署验证。只监听127.0.0.1:25592，测试世界来自旧隔离夹具的副本。
- 服务端读取原生 Studio 导出的 `Native0333Long.dgrs`，SHA-256 `c73417b1623ec8bee033d4d5c10d7c699f58dbae264001ec18fead17edb8a88b`，与导出文件一致。加载日志 generation `8c4b3e9f7e6a`。

## 实际动作与结果

1. 两端真实加入同一服务端；服务端日志识别两端 `darkgrey_rpg@0.3.3.3`。见 `evidence/dual-client-server-extract.log`、`.tooling/0333/server-dual-final.log`。
2. 主端在聊天框执行 `/dgr session play Native0332:acceptance aggregate_5c6dba9d366e498b836f86b681901f85`，显示 BEGIN0333。见 `dual-main-started.png`。
3. 切换见证端，仍无对话窗口；按H打开历史，显示“暂无对话记录”。见 `dual-witness-empty-history.png`。主端会话未泄漏到另一玩家历史。
4. 见证端独立执行相同命令，出现自己的 BEGIN0333。见 `dual-witness-started.png`。该端默认字号100%，主端保留150%偏好。
5. 主端完整显示后点击正文，进入下一本地显示页，开头不再是BEGIN0333。见 `dual-main-before-turn.png`、`dual-main-page2.png`。
6. 再切换见证端，仍停留在BEGIN0333开头的第一页，字号也保持100%。见 `dual-witness-still-page1.png`。两端显示段与阅读进度相互独立。
7. 测试账号从聊天框执行 `/stop`，观察Server closed；服务端日志保存三个维度后正常结束，Gradle BUILD SUCCESSFUL。关闭本轮两个测试客户端；保留用户原有Studio实例。

截图均在 `evidence/native-ui/`。记录界面状态支持上述具体结论，不单凭截图推断服务端未发包、声音无重播或所有隔离边界正确。

## 夹具问题及边界

首次把打开聊天和整条命令一次性发送时，得到Unknown command；重新分开打开聊天、观察完整命令、Enter提交后成功。没有将首个失败算作通过。

复制的开发服务端起初关闭RCON；启用后，原版开发源码RConThreadClient在认证循环finally关闭socket，下一步触发空指针。未修改原版源码；关闭RCON，为隔离测试账号预置权限并重启。最终验证采用聊天命令，最终退出正常保存。中间重启只涉及隔离世界，未操作用户世界。

尚未执行：跨服/重连/维度/死亡/251条历史完整矩阵、实际语音及声音矩阵、NPC/Story正常触发链、多人任务奖励与留声机多设备实机。USER_ACCEPTED=NO；RELEASE_READY=NO。
