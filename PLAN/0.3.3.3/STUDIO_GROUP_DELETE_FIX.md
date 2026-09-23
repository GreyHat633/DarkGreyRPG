# Studio 组合右键删除修复（2026-09-24）

根因：统一右键菜单调用 DeleteCurrentSelection，但该方法只接受节点选择；单独选中组合时直接返回。现在优先将组合选择转交已有 DeleteSelectedGroups，递归删除组合、子组合和节点，沿用连接清理、受保护节点检查及一次撤销/重做事务。取消组合的语义不变。

验证：首轮 20 项中 19 通过；新增测试因 JSON 属性顺序比较失败，修改为 JSON 结构相等断言后重跑 14 项全部通过（包括真实 WPF 菜单 Click 事件、嵌套删除、保留外部节点、撤销/重做）。首轮其他 6 项无需重复运行。记录：.tooling/0333/group-delete-tests/group-delete-final.trx。未声称本轮做过用户桌面的人工鼠标验收。

已发布 self-contained Windows x64 Release 至 dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe。
ProductVersion：0.3.3.3；Bytes：142434518。
SHA-256：a4b3658f77c8f7750f44c5078842fb0dc8a9daa8276dc0a9e823fb350878036c。
旧 EXE 保存在 .tooling/0333/BeforeGroupDeleteStudio.exe；保留正在运行的旧进程，用户保存后重启加载新版。
USER_ACCEPTED=NO；RELEASE_READY=NO。Java JAR 未更改。
