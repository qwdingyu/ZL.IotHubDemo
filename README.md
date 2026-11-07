# ZL.IotEngine 示例

**ZL.IotEngine 是面向 .NET 的工业 PLC 数据采集运行时。**
一句话：**它让"把几百个点位采回来"这件事变得又快又稳。**

本仓库是官方示例，每个示例都能独立编译运行，可以直接拷进你的项目。

---

## 核心功能 → 看哪个示例

| 核心功能 | 一句话说明 | 示例 |
| --- | --- | --- |
| **地址合并批量读取** | 几百个点位自动合并成几个请求，采集周期才压得下来 | **[BatchRead](./samples/BatchRead)** ⭐ |
| **坏点隔离 / 恢复** | 一个坏点，拖不垮整块采集 | [BadPointIsolation](./samples/BadPointIsolation) |
| **变化上报** | 值变了才回调你，不用自己轮询比对 | [Events](./samples/Events) |
| **采集最小闭环** | 连上 → 读到值 → 写进去 → 读回一致 | [QuickStart](./samples/QuickStart) |
| **多协议接入** | 换 Modbus 只改三样东西 | [Modbus](./samples/Modbus) |
| **字符串读写** | 中文 PLC 最容易踩的 4 个坑（编码/S7 头/容量/注册）| **[StringIO](./samples/StringIO)** ⭐ |
| **数组批量** | 一次搬一批；数组读的跨 TFM 差异 | [ArrayIO](./samples/ArrayIO) |
| **多设备编排** | 一台坏了不拖垮其他 | [MultiDevice](./samples/MultiDevice) |

---

## ⚠️ 从 ZL.IotHub 升级（2026-11-07）

`ZL.IotHub` 已**冻结停发**，拆成两个包 —— **必须同时引用**：

```bash
dotnet add package ZL.IotEngine    # 编排内核（地址合并/批量采集/事件）
dotnet add package ZL.IotDriver    # 64 个协议驱动（独立插件包）
```

| 旧 | 新 |
| --- | --- |
| `using ZL.IotHub;` + `using ZL.IotHub.Core;` | `using ZL.IotEngine.Orchestration;` |
| `using ZL.IotHub.Notifications;` | `using ZL.IotEngine.Notifications;` |
| `using ZL.Tag;` | **不变** ✓ |

> 🔴 **只装 `ZL.IotEngine` 会报** `不支持的协议类型 'siemens-s7'。可选：（空）`
> —— 因为没有驱动包时注册表为空（**这是设计，不是缺陷**）。

**另两处行为差异**（都是刻意设计 ✓）：

| 项 | 说明 |
| --- | --- |
| **写入需显式授权** | 点表要写入必须配 `AccessMode = AccessMode.ReadWrite`（默认只读 ✗）|
| **GB2312 编码** | 需先触碰一次引擎入口触发注册：`_ = PlcStringWriteHelper.ResolveEncoding(null);` 之后 `Encoding.GetEncoding(936)` 才可用 ✓ |

---

## 目标框架（TFM）

`ZL.IotEngine` / `ZL.IotDriver` **同时提供 4 个 TFM**，按你的项目自动选最合适的：

| 你的项目 | 用哪个 TFM | 说明 |
| --- | --- | --- |
| **.NET Framework 4.8（WinForm 主力）** | `net48` | ✅ 完整支持；串口协议用框架自带 `System.IO.Ports` |
| .NET 8 / 10 | `net8.0` / `net10.0` | ✅ 推荐；串口走 `System.IO.Ports` NuGet 包 |
| 老框架 / 库项目 | `netstandard2.0` | ✅ 最低公约数 |

> ✅ **本仓示例已改为多目标 `net8.0;net48`** ✓ —— 6 个示例源码在 net48 下**编译 0 error** ✓
> ⚠️ `net48` 只能在 **Windows** 上运行（Linux/macOS 无 .NET Framework 运行时 ✗）
> ```bash
> # Windows 上跑 net48 版本（WinForm 同款运行时）
> dotnet run --project samples/QuickStart/QuickStart.csproj -f net48
> # 其它平台跑 net8.0
> dotnet run --project samples/QuickStart/QuickStart.csproj -f net8.0
> ```

---

## 写入点位必须显式授权

```csharp
new TagItem {
    Id = "COUNT", Address = "DB1.DBW2", DataTypeCode = "short",
    AccessMode = AccessMode.ReadWrite,   // ← 不写就**只读**，写入会失败
}
```

> 🔴 默认 `AccessMode` 是**只读** ✓ —— 这是刻意的安全设计（防止误写 PLC）✓
> 报错信息会明确提示：`标签为只读模式，无法写入: COUNT`。

---

## 字符串写入：编码必须显式指定

```csharp
// ⚠️ 中文 PLC 现场必须用 GB2312（=936），不能用 Encoding.Default
_ = PlcStringWriteHelper.ResolveEncoding(null);   // 先触发引擎注册代码页
var enc = Encoding.GetEncoding(936);
await driver.WriteStringAsync("DB1.DBB10", "中文", StringKind.S7String, 20, enc);
```

| 坑 | 说明 |
| --- | --- |
| **`Encoding.Default` 跨 TFM 不一致** | .NET 8 上是 **UTF-8**、net48 上是**系统 ANSI** ⇒ 同一句中文写出**不同字节** ✗ |
| **`Encoding.GetEncoding(936)` 会抛异常** | 必须先触碰一次引擎的编码入口（`ResolveEncoding`）才会注册代码页 ✓ |
| **`maxLength` 不能传 -1** | S7 会去读 PLC 头字段的真实容量；容量不足会明确报错（**这是正确保护**）✓ |
| **`ReadString` 返回值含 S7 的 2 字节头** | 回读校验时必须跳过前 2 字节（`Byte0=最大长度, Byte1=实际长度`）✓ |

---

## 5 分钟跑通

**① 起一台仿真器**（没有真机也能练）：

```bash
dotnet tool install -g PlcSimulator.Cli     # 一次性
plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
```

**② 跑示例**（另开一个窗口，进对应目录）：

```bash
cd samples/BatchRead
dotnet run
```

**③ 你会看到**：

```
【3】采集计划（核心）
      点位总数    : 300
      合并后块数  : 6
      请求数减少  : 98.00%
      ⇒ 原来要 300 次请求，现在约 6 次
```

---

## 换个端口 / 换个设备

```bash
dotnet run -- --ip 192.168.1.10 --port 102
```

---

## 免费额度与授权

- **免费版：≤ 10 台设备、协议全开**，可直接用于生产验证
- **超过 10 台**：需正式设备授权（一次性设备授权码）
- **授权咨询：微信 `qwdingyu`**

---

## 环境

| 项 | 说明 |
| --- | --- |
| .NET | 8.0 及以上（产品本身支持 `netstandard2.0` / `net48` / `net8.0` / `net10.0`）|
| 端口 | macOS/Linux 上 < 1024 的端口需要 root，示例统一用 31002 这类大端口 |
| 产品包 | [`ZL.IotEngine` + `ZL.IotDriver` on NuGet](https://www.nuget.org/packages/ZL.IotEngine) |
| 许可 | 见 [LICENSE](./LICENSE) |
