# AF Media Bar — 修改说明（Changes）

本项目在 [Fervent-Tempo/AF-Media-Bar](https://github.com/Fervent-Tempo/AF-Media-Bar)
（MIT License）基础上修改。原项目版权归 **Copyright (c) 2026 Fervent-Tempo**。

本修改保留原 LICENSE 与版权声明，新增功能如下。

---

## 版本历史

### v1.2.2（当前）
基于 v1.2.1，主要改动：
- 风扇转速读取改用 **LibreHardwareMonitor** 库（原 WMI `Win32_Fan` 在多数机型读不到）。
- 新增 **温度（TEMP）** 指标：读取 CPU 温度（℃）。
- 新增指标字号随字体大小缩放适配。
- 修复指标药丸宽度被裁剪的问题（改为按实际布局宽度计算，指标显示完整）。

### v1.2.1
基于 v1.1，新增：
- **风扇转速（FAN）** 指标（WMI `Win32_Fan` 只读，RPM）。

### v1.1
基于 v1.0，新增：
- 设置新增「**指标文字大小**」滑块（8–16，默认 11）。
- 新增「**无媒体时仅隐藏媒体区，保留性能指标**」开关。

### v1.0
基于原版（1.1.1），主要改动：
- 性能指标药丸由「轮动显示单个指标」改为「**同时显示** MEM/CPU/GPU」。
- 新增 **电池电量（BAT）** 指标。

---

## 说明

- 本仓库默认不含编译产物（`bin/`、`obj/` 已在 `.gitignore` 中排除）。
- 各版本的可执行文件请通过 GitHub **Releases** 发布。
- 原始 LICENSE：MIT（见根目录 `LICENSE`）。
