# 0.3.2.4 用户截图复核修正（2026-09-11）

本轮针对用户六项反馈；之前的界面验证不能代替用户验收。本轮仍为 USER_ACCEPTED=NO。

## 修改

1. 物品指名器、实体指名器、复制器、任务界面统一四角缩放。每个角两侧各三点，共用角点；拖动固定对角，最小尺寸和屏幕边界限制生效。
2. 恢复物品指名/解绑两个独立深色描边面板；槽与按钮留出间隔；装备栏去掉整条外框，移至背包右侧剩余区域中间。
3. 两个指名器 ID释放 移至标题右侧，保留完整按钮和角点空间；搜索栏与按钮留有间隔。物品浏览区增加20逻辑像素高度，标题留白后仍增加14像素有效列表空间。
4. 故事包/资源标题在列表背景内下移4逻辑像素；行绘制、裁剪、命中和滚动同步调整。
5. 复制器使用当前配置的背包键关闭，实机改绑R验证。
6. 修正实际工程和运行包的第二目标文案，并保留当前存档进度。

## 第六项根因及修复范围

实际存档 run/client/saves/新的世界/data/darkgrey_rpg_canonical_tasks.dat 读取得到：
- node_cfc0a454a1ef4168890d4ce6fbb3e803：COMPLETED，progress=3。
- node_4feb6ff8c4944e078f9914cd808c9f17：ACTIVE，progress=0。

实际任务已经推进；interact_actor 目标的 description 错误重复了击杀目标文案。上一轮只修了隔离验收包，没有应用到实际包，不能据此声称用户问题已解决。

本轮找到并修正原工程：
E:/Java/MinecraftMod/RPGProject/TestProject/resources/canonical/tasks/x477265794861745f/x4b696c6c536c696d6573.json

同时修正 run/client/darkgrey_rpg_story_packages/kill_slimes.dgrs。
仅将上述 interact_actor.description 改成“与酒馆老板对话”；其余图节点、连接、ID、条件和归档条目保持原语义/原字节。

包内容变化会触发现有 generation 清理，故本轮针对这一个已验证的纯文案变更实施一次离线兼容迁移：只替换 task saved-data 的资源指纹、generation saved-data 的包指纹。两份解压 NBT 除64字节指纹外完全一致，保留所有进度、状态、UUID、时间和逻辑值。没有修改 Runtime/schema/generation 代码，也没有改变今后的正常更新规则。

备份、候选、前后哈希和实际应用记录位于 evidence/user-review-fixes/backup、migration-candidate、migration-manifest.json、migration-applied.json。

## 验证

- 四个GUI共16个角在游戏内实际调用输入处理：放大后对角固定；实体/物品右上释放进入确认框而不拖动窗口；复制器改绑R关闭。
- 自动探针覆盖四角尺寸增大、最小值、屏幕边界、对角固定；已有拖动和持久化探针保留。
- 使用实际存档副本，通过原版NBT解码和CanonicalTaskRuntime.restore验证恢复成功，击杀仍完成、交互仍激活，交互后结算。
- 将实际世界复制至独立游戏目录，加载修正包和迁移后的数据。游戏任务页显示“与酒馆老板对话”；与实际绑定老板交互后任务列表移除已完成任务。原始世界没有执行这次交互，仍停在待交互步骤。
- 1920×1030、GUI Scale 4 查看布局；最终截图和日志随证据归档。测试使用独立游戏世界及游戏内输入回调，不声称物理鼠标人工验收。

Studio本轮未修改。原版本Studio真实文件对话框Gate F/G仍未完成；不将本轮修正视为整个版本的用户验收。

## 最终成品

- dist/darkgrey_rpg-0.3.2.4.jar：977078 字节，SHA-256 `024F8A29A694668C157FF7B7E348ACE1E459EFBE6CFD1DB98880913DDE839F81`。与 build/libs 对应成品一致，mcmod.info 版本0.3.2.4，无Probe/验收Mod。
- 完整 build + 58 项 JavaExec 探针 PASS；实际工程包及实际存档路径再次加载，USER_SAVE_DESCRIPTION_MIGRATION=PASS。
- 最终成品再次实机验证16个角、右上释放与改绑背包键，通过。
- 最终截图：[物品指名](evidence/user-review-fixes/review-clean-item.png)、[实体指名](evidence/user-review-fixes/review-clean-entity.png)、[真实存档副本的下一目标](evidence/user-review-fixes/review-user-world-next-objective.png)。
