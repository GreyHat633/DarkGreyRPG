# 规模对比预设（运行前记录）
基线：HEAD 641c0f3（0.3.3.3），隔离归档，不改用户工作树。
候选：当前 0.3.3.4。相同机器、SDK、Release、顺序运行，关闭游戏减少干扰。
固定 3 Session × 100 Line × 5 Page，另复用 300 Node / 0、300 Group 拖动及 Rendering 测试。
每操作预热 3 次、样本 20 次；记录 median/P95/max/working set。
排查判据：median 或 P95 同时超过基线 1.5 倍且增加 20ms 以上，须重测确认；内存超过 1.5 倍且增加 100MB 排查。
这些是托管操作和 WPF 布局计时，不代表鼠标到屏幕延迟、GPU FPS 或冷磁盘启动。
已改用施工前 EXE 的原始程序集作为精确二进制基线（ProductVersion 0.3.3.3）。
HEAD + tracked patch 不含全部旧 untracked 依赖，未将其构建结果用于比较。
测试工具：.tooling/0334/Native/extract-baseline.py；只提取本地可信 EXE 中两个 PE 程序集，校验 manifest 边界、文件长度及 PE 标记。
