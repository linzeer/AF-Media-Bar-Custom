# AF Media Bar（Custom Fork by linzeer）

> 本仓库是 [Fervent-Tempo/AF-Media-Bar](https://github.com/Fervent-Tempo/AF-Media-Bar) 的自定义修改版。
> 原项目版权归 **Copyright (c) 2026 Fervent-Tempo**，本仓库保留原 `LICENSE`（MIT）与版权声明。
> 所有修改基于 MIT 协议合法再分发。

<div align="center">

  <img src="assets/af-media-bar.png" alt="AF Media Bar" width="160" height="160">

  <h1>AF Media Bar — Custom</h1>

  <p>Windows 10/11 任务栏上的媒体控制、音频设备切换与轻量系统指标（增强版）。</p>

</div>

---

## ✨ 本仓库相比原版新增的功能

**v1.2.2（当前）**
- 性能指标药丸**同时显示** MEM / CPU / GPU（原版为轮动跳显）。
- 新增 **电池电量（BAT）** 指标。
- 新增「**指标文字大小**」滑块（8–16），指标文本实时缩放。
- 新增「**无媒体时仅隐藏媒体区，保留性能指标**」开关。
- 新增 **风扇转速（FAN）** 指标，用 **LibreHardwareMonitor** 库只读读取。
- 新增 **温度（TEMP）** 指标（CPU 温度，℃）。
- 修复指标药丸宽度被裁剪的问题（按实际布局宽度计算，显示完整）。

（v1.0 / v1.1 / v1.2.1 的逐步改动见下方版本历史，或见仓库根目录 `CHANGES.md`）

---

## 📋 版本历史

| 版本 | 主要改动 |
|------|----------|
| **v1.0** | 性能指标药丸由轮动改为同时显示 MEM/CPU/GPU；新增电池电量（BAT）。 |
| **v1.1** | 新增「指标文字大小」滑块；新增「无媒体时仅隐藏媒体区，保留性能指标」。 |
| **v1.2.1** | 新增「风扇转速（FAN）」指标（WMI 只读）。 |
| **v1.2.2** | 风扇改用 LibreHardwareMonitor 库；新增「温度（TEMP）」；修复指标药丸宽度裁剪。 |

各版本可执行文件已发布在 [Releases](https://github.com/linzeer/AF-Media-Bar-Custom/releases)（Assets 中可直接下载）。

---

## 🔧 构建

依赖 **.NET 8 SDK**（含 Windows Desktop 运行时）。

```powershell
dotnet publish .\AFMediaBar.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

或直接运行仓库根目录的 `build.cmd`。

---

## 🤖 关于本仓库的修改

> 本仓库的全部修改由 **DSH（DeepSeek Harness）+ v4-flash** 模型自动完成，**全程无任何手动编辑/输入**。
> 代码修改、各版本构建、功能描述、上传 GitHub 均由自动化智能体生成并执行。

---

## ⚖️ 版权与许可

- 原项目：**Fervent-Tempo/AF-Media-Bar**（MIT License）— Copyright (c) 2026 Fervent-Tempo
- 本仓库基于 MIT 协议再分发，保留原 LICENSE 全文（见 `LICENSE`）。
- 原项目完整代码（多项目结构）已归档在 `archive-original/` 目录以备参考。

---

## 📄 更多
- 修改说明：`CHANGES.md`
- 英文 README：`README.en-US.md`（原版，未改写）
