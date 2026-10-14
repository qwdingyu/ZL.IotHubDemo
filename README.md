# ZL.IotHub 示例仓库

**ZL.IotHub** 是面向 .NET 的**工业 PLC 数据采集运行时**：连接、地址合并批量读取、类型解包、
变化上报、坏点隔离与恢复，收敛成一个可嵌入的内核。

本仓库是官方示例集合——**每个示例都能独立编译运行**，你可以直接拷进自己的项目。

> 本仓库只放**示例代码**。产品本体以 NuGet 包发布，核心为**自研原生驱动**（不含任何第三方 PLC 通讯库）。

---

## 5 分钟跑通

没有 PLC 也能跑——用本机仿真器当"假 PLC"。

### 第 0 步：准备

- [.NET 8 SDK](https://dotnet.microsoft.com/download) 或更高
- 装一台仿真器（一次性）：

```bash
dotnet tool install -g PlcSimulator.Cli
```

### 第 1 步：起仿真器（一个窗口）

```bash
plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
```

看到 `HEADLESS_READY` 即就绪。

> 🔴 端口用 **31002**，不要用默认 102：macOS/Linux 上 < 1024 的端口需要 root。
> 🔴 `--protocols` 不能省：不传会加载全部协议，端口冲突保护会直接报错。

### 第 2 步：跑 QuickStart（另一个窗口）

```bash
cd samples/QuickStart
dotnet run
```

### 第 3 步：你应该看到

```
已连接（NativeUnifiedDriver）
RUN=True  COUNT=32767  TEMP=205885
✅ 写入=读回（12345），地址与类型正确。
```

这四行就是采集的最小闭环：**连接 → 读取 → 写入 → 读回比对**。
最后那步才是重点——**地址、类型、端序任一处写错，这里立刻暴露**，
而不是等到了现场才发现。

---

## 示例一览

| 示例 | 演示什么 | 难度 |
| --- | --- | --- |
| [`samples/QuickStart`](./samples/QuickStart) | 最小闭环：点表 → 连接 → 读 → **写 → 读回比对** | ⭐ |

> 更多示例（事件订阅与判变上报、Modbus 接入、多设备编排、Web 网关）会陆续加入本仓库。

---

## 用到你自己的设备上

只需要改 `Program.cs` 里的两处：

```csharp
const string Host = "127.0.0.1";   // 设备地址
const int   Port = 31002;          // 端口

var tags = new List<TagItem>
{
    new() { Id = "TEMP", Address = "DB1.DBD4", DataTypeCode = "float", TagType = "M", StationNo = DeviceId },
};
```

- **S7 地址写法**：`DB1.DBX0.0`（位）、`DB1.DBW2`（字）、`DB1.DBD4`（双字 · float）
- **`DataTypeCode`**：`bool` / `short` / `int` / `float` / `string` 等
- **Modbus** 需另加 `Station`（站号）与 `AddressStartWithZero`（地址是否从 0 开始）

---

## 免费额度与授权

- **免费版：≤ 10 台设备、协议全开**，可直接用于生产验证，无需任何授权配置
- **超过 10 台**：需正式设备授权（一次性设备授权码）
- **授权咨询：微信 `qwdingyu`**

---

## 环境与许可

| 项 | 说明 |
| --- | --- |
| 运行时 | `netstandard2.0` / `net48` / `net8.0` / `net10.0` |
| 端口权限 | macOS/Linux 上 < 1024 的端口需要 root，建议用 31002 / 31502 这类大端口 |
| 示例代码许可 | 见 [`LICENSE`](./LICENSE)（与产品同源） |
| 产品包 | [`ZL.IotHub` on NuGet](https://www.nuget.org/packages/ZL.IotHub) |
