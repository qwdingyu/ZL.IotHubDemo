# ZL.IotEngine 示例

**ZL.IotEngine 是面向 .NET 的工业 PLC 数据采集运行时。**
一句话：**它让"把几百个点位采回来"这件事变得又快又稳。**

本仓库是官方示例，每个示例都能独立编译运行，可以直接拷进你的项目。

> ✅ **只依赖 NuGet 2.3.0**：全部示例使用 `PackageReference`（`ZL.IotEngine` / `ZL.IotDriver` / `ZL.IotHub.Server`），
> **没有任何 `ProjectReference`** ⇒ 在 GitHub clone 后 `dotnet restore` 即可，不需要主仓源码。
> 版本单一真源：根目录 [`Directory.Build.props`](./Directory.Build.props) 的 `IotEngineVersion` / `IotServerVersion`。

---

## 推荐阅读顺序（不要从目录名猜）

建议**按这个顺序看**，每一步都建立在前一步的基础上：

| 顺序 | 示例 | 你会得到什么 |
|---|---|---|
| 1 | **[QuickStart](./samples/QuickStart)** | **5 分钟跑通最小闭环**：连上 → 读到值 → 写进去 → 读回一致 |
| 2 | **[BatchRead](./samples/BatchRead)** | **直接看到核心价值**：300 个点位 → 6 个读取块，请求数减少 98% |
| 3 | [Events](./samples/Events) / [Modbus](./samples/Modbus) / [ArrayIO](./samples/ArrayIO) / [StringIO](./samples/StringIO) / [BadPointIsolation](./samples/BadPointIsolation) | **按现场问题查**：变化上报、Modbus 地址、数组、字符串编码、坏点隔离 |
| 4 | **[MultiDevice](./samples/MultiDevice)** | **多设备故障隔离**：统一启动/健康可见/一台坏了不拖垮其他 |
| 5 | **[Advanced](./samples/Advanced)** | **交付/运维阶段**：业务壳、可判别读、诊断快照、运行时增删设备 |
| 6 | **[OpcUaClient](./samples/OpcUaClient)** / **[OpcUaServer](./samples/OpcUaServer)** | **OPC UA 两个方向**：PLC 是 OPC UA Server 时怎么采；普通协议采集后怎么以 OPC UA 发放 |

> 只想先看“它凭什么快”？直接跳到第 2 步 [BatchRead](./samples/BatchRead)——**不需要 PLC** 就能看到 300 → 6 的合并结果。
>
> 每一步的 README 末尾都有“下一步”链接，可以一路点下去。
>
> **只装驱动包**（不引入引擎）的独立入口 → [`DriverOnly`](./samples/DriverOnly)。

### OPC UA 专题（两个方向，都覆盖）

| 方向 | 场景 | 示例 |
|---|---|---|
| **我们做客户端** | PLC / 仿真器本身是 OPC UA Server，我们按 `ns=2;s=...` 采集 | [OpcUaClient](./samples/OpcUaClient) |
| **我们做服务端** | 普通协议（S7/Modbus/…）采集 → `ZL.IotHub.Server` 自动包装为 OPC UA，供 SCADA/MES 读取 | [OpcUaServer](./samples/OpcUaServer) |

> `OpcUaServer` 还同时暴露 REST 与 SignalR，三个出口共用同一份采集结果；
> 包含初值播种、逐设备故障隔离、质量码/时间戳保留等 Server 侧优化。

---

## 改设备 / 点表：只改配置文件，不重新编译

每个示例的 **IP、端口、点表** 都在同目录的配置文件里（XML 或 JSON），
`<TagItem>` 由 **`ZL.IotAbstraction.Tag.TagKit`** 解析（与生产环境同一套）：

| 示例 | 配置文件 |
|---|---|
| QuickStart | `quickstart.xml` |
| BatchRead | `batchread.xml`（也可写 `<TagItem>` 用自己的点表） |
| Events | `events.xml` |
| Modbus | `modbus.xml` |
| MultiDevice | `devices.json` + `tags.json`（`fleet.json` 只给仿真器） |
| ArrayIO | `arrayio.xml` |
| StringIO | `stringio.xml` |
| BadPointIsolation | `badpoint.xml` |
| Advanced | `devices.json` + `tags.json`（`fleet.json` 只给仿真器） |
| OpcUaClient | `opcua-sim.xml` / `opcua-server.xml` + `points.json` |
| OpcUaServer | `appsettings.json`（`Scada:Devices` + `OpcUa`） |
| DriverOnly | 无配置文件，命令行 `--protocol` / `--ip` / `--port` |

```bash
# 换一套配置运行（不用改代码、不用重新编译）
dotnet run -f net8.0 -- --config 我的设备.xml
```

---

## 只装 `ZL.IotDriver`（不引入引擎）

如果你已经有自己的调度器、线程池、数据库、日志和重连逻辑，**只想拿驱动直接读写 PLC**：

```bash
dotnet add package ZL.IotDriver --version 2.3.0
```

```csharp
using var plc = new SiemensS7Driver(
    SiemensCpuType.S7_1200, "127.0.0.1", 31002, rack: 0, slot: 1);

plc.ConnectServer();
plc.Write("DB1.DBW2", (short)1234);
var value = plc.ReadInt16("DB1.DBW2");
```

- **不需要** `ZL.IotEngine`；
- 没有 `DriverFactory` / `DeviceManager` / 后台持续重连 / 缓存 / 批量合并，这些由你的程序自己管；
- S7 / Modbus / Omron / 三菱 MC 四种协议、bool/short/int/float/string 示例：
  **[DriverOnly](./samples/DriverOnly)** ⭐

> 只装 Driver 时，`PlcDeviceStringWriteExtensions.WriteString(this IPlcDevice, ...)` 扩展方法
> 在 `ZL.IotEngine` 里，不在 `ZL.IotDriver` 里；S7/Modbus 请用驱动实例的 `WriteString`，
> Omron / 三菱请用 `WriteBytes`。详见示例 README。

---

## 核心功能 → 看哪个示例

| 核心功能 | 一句话说明 | 示例 |
| --- | --- | --- |
| **地址合并批量读取** | 几百个点位自动合并成几个请求，采集周期才压得下来 | **[BatchRead](./samples/BatchRead)** ⭐ |
| **坏点隔离 / 恢复** | 一个坏点，拖不垮整块采集 | [BadPointIsolation](./samples/BadPointIsolation) |
| **变化上报** | 值变了才回调你，不用自己轮询比对 | [Events](./samples/Events) |
| **采集最小闭环** | 连上 → 读到值 → 写进去 → 读回一致 | [QuickStart](./samples/QuickStart) |
| **多协议接入** | 换 Modbus 只改三样东西 | [Modbus](./samples/Modbus) |
| **字符串读写** | 中文 PLC 最容易踩的 4 个坑（编码/S7 头/容量/代码页）| **[StringIO](./samples/StringIO)** ⭐ |
| **数组批量** | 一次搬一批；数组读的跨 TFM 差异 | [ArrayIO](./samples/ArrayIO) |
| **多设备编排** | 一台坏了不拖垮其他 | [MultiDevice](./samples/MultiDevice) |
| **高级编排** | 业务壳 / 可判别读 / 诊断 / 运行时增删设备 | **[Advanced](./samples/Advanced)** ⭐ |
| **OPC UA 客户端** | PLC/仿真器是 OPC UA Server 时，按 NodeId 采集 | **[OpcUaClient](./samples/OpcUaClient)** ⭐ |
| **OPC UA 网关** | 普通协议采集后，统一以 OPC UA 对外发放（ZL.IotHub.Server） | **[OpcUaServer](./samples/OpcUaServer)** ⭐ |
| **只装驱动包** | 不引入引擎，直接 `new` 驱动读写 S7/Modbus/Omron/三菱 MC | **[DriverOnly](./samples/DriverOnly)** ⭐ |

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
| `using ZL.Tag;` | `using ZL.IotAbstraction.Tag;` |

> 🔴 **只装 `ZL.IotEngine` 会报** `不支持的协议类型 'siemens-s7'。可选：（空）`
> —— 因为没有驱动包时注册表为空（**这是设计，不是缺陷**）。

**另两处行为差异**（都是刻意设计 ✓）：

| 项 | 说明 |
| --- | --- |
| **写入需显式授权** | 点表要写入必须配 `AccessMode = AccessMode.ReadWrite`（默认只读 ✗）|
| **GB2312 编码** | .NET 8 默认**不带**代码页；宿主需注册 `CodePagesEncodingProvider` 后 `Encoding.GetEncoding(936)` 才可用。示例默认用 UTF-8（跨 TFM 确定） |

---

## 目标框架（TFM）

`ZL.IotEngine` / `ZL.IotDriver` **同时提供 4 个 TFM**，按你的项目自动选最合适的：

| 你的项目 | 用哪个 TFM | 说明 |
| --- | --- | --- |
| **.NET Framework 4.8（WinForm 主力）** | `net48` | ✅ 完整支持；串口协议用框架自带 `System.IO.Ports` |
| .NET 8 / 10 | `net8.0` / `net10.0` | ✅ 推荐；串口走 `System.IO.Ports` NuGet 包 |
| 老框架 / 库项目 | `netstandard2.0` | ✅ 最低公约数 |

> ✅ **本仓示例已改为多目标 `net8.0;net48`** ✓ —— 全部示例源码在 net48 下**编译 0 error** ✓
> ⚠️ **例外**：`OpcUaServer` 单目标 `net8.0`（`ZL.IotHub.Server` 只提供 net8.0 / net10.0）。
> 运行它时仍然显式带 `-f net8.0`，与其它示例命令保持一致 ✓
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

## 判断「设备是否在线」必须用**质量码**，不要用 `IsConnected`

> ✅ **版本边界（2026-11-08）**：本节描述的"断网后 `IsConnected` 恒 `true`"缺陷
> **已在 2.2.20 修复**（断网 ⇒ 隔离清空计划 ⇒ 明确标记断开；重连后自动恢复）。
> **示例当前版本：2.3.0** ⇒ 用本仓构建的直接受益 ✓；仍 pin 2.2.19 的项目请升级。
> 以下实测记录保留，作为「为什么 2.2.19 上必须用质量码」的依据 ✓。

实测（2026-11-07，杀仿真器进程模拟断网，观察 60 秒）：

| 指标 | 断网后 | 可信？ |
| --- | --- | --- |
| `device.IsConnected` | **始终 `true`** | 不可信 |
| `device.ConnectionState.IsConnected` | **始终 `true`** | 不可信 |
| `device.ConnectionState.IsOperational` | **始终 `true`** | 不可信 |
| `device.ConnectionState.SuccessRate` | 卡住不动 | 不可信 |
| **`localCache[key].Quality`** | **`Bad`** | **唯一可信** |

**正确做法**：

```csharp
var key = driver.GenerateCacheKey(driver.Tags["COUNT"]);
var online = driver.localCache.TryGetValue(key, out var tv)
             && tv.Quality == TagQuality.Good;
```

> 为什么：`BatchReadLoop` 的读取失败路径**不会**更新 `ConnectionState`；
> 只有 `ReconnectionLoop` 重连失败才 `MarkDisconnected()`。
> 心跳连续失败 5 次也只进 `Suspect` 态，而 `IsConnected` **包含 Suspect** ⇒ 仍是 `true`。

### 📌 版本边界（重要）

| 版本 | `IsConnected` 可信吗 | 建议 |
| --- | --- | --- |
| **2.2.19 及更早** | ❌ **不可信**（断网后永远 `true`） | **必须用质量码** ✓ |
| **2.2.20 及以后** | ✅ 已修复（断网 5 秒内变 `false`） | 两者都可用；质量码更精细 ✓ |

> **修复内容**：主仓已在 `DeviceRoot` 的计划重建出口补上判定 ——
> **计划被隔离清空且存在隔离点 ⇒ 标记断开**。
> 实测：断网 **5 秒内** `IsConnected=false` / 状态「已断开」；恢复后自动回到「已连接」且隔离清零 ✓
>
> ⚠️ 但**质量码仍然是更精细的指标** ✓ —— 它反映**每个点位**的可信度，
> 而 `IsConnected` 只反映**设备级**通断 ✓。做"单点坏值告警"仍应看质量码 ✓。

---

## 字符串写入：编码必须显式指定

```csharp
// 跨 TFM 确定：用 UTF-8（S7 STRING 按字节写；中文 12 字节，容量 20 足够）
var enc = Encoding.UTF8;
IPlcDevice dev = driver.Device;   // 字符串读写定义在 IPlcDevice 上
await dev.WriteStringAsync("DB1.DBB10", "中文", StringKind.S7String, 20, enc);

// 若 PLC 侧固定 GB2312：宿主先注册代码页，再 GetEncoding(936)
// Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
// var enc936 = Encoding.GetEncoding(936);
```

| 坑 | 说明 |
| --- | --- |
| **`Encoding.Default` 随「运行时 + 系统区域」变化** | 它不是常量 ✗：<br>· **.NET 8/10** ⇒ **始终 UTF-8**（65001）✓【本机实测 ✓】<br>· **.NET Framework（真 Windows）** ⇒ **系统 ANSI 代码页**（中文 Windows = **936**；英文 = **1252**）⚠️【本机无真 Framework，**未实测** ✗，按 .NET 文档说明 ✓】<br>· **mono（本机实测）** ⇒ **UTF-8**（65001）✗ —— 与真 Framework **不同** ✓<br>⇒ **同一份源码在不同客户环境写出不同字节** ✗ ⇒ 必须**显式传编码** ✓ |
| **`Encoding.GetEncoding(936)` 在 .NET 8 默认不可用** | .NET 8 不再内置代码页；宿主需 `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`（中文现场按需）✓ |
| **`maxLength` 不能传 -1** | S7 会去读 PLC 头字段的真实容量；容量不足会明确报错（**这是正确保护**）✓ |
| **`ReadString` 返回值含 S7 的 2 字节头** | 回读校验时必须跳过前 2 字节（`Byte0=最大长度, Byte1=实际长度`）✓ |

---

## 5 分钟跑通

### ① 先看核心价值（**不需要 PLC / 仿真器**）

```bash
cd samples/BatchRead
dotnet run -f net8.0
```

你会看到：

```
【2】采集计划（核心）
      点位总数    : 300
      合并后块数  : 6
      请求数减少  : 98.00%
      ⇒ 原来要 300 次请求，现在约 6 次
```

### ② 再跑最小闭环（需要仿真器）

```bash
dotnet tool install -g PlcSimulator.Cli     # 一次性
plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
```

另开一个窗口：

```bash
cd samples/QuickStart
dotnet run -f net8.0
```

> ⚠️ **多目标示例必须带 `-f`**（本仓示例是 `net8.0;net48`）✓
> 不写会报 `Your project targets multiple frameworks` ✗
> （Windows 上想看 WinForm 同款运行时：`dotnet run -f net48`）

---

## Linux / 容器：license 指纹报错怎么办

在部分 Linux 容器 / GitHub Actions runner 上运行示例会报：

```
ZL.License.LicenseException: Linux 机器指纹采集失败，无法继续授权流程。
 ---> System.UnauthorizedAccessException: Access to the path '/sys/class/dmi/id/product_uuid' is denied.
```

原因：`ZL.License 2.3.0` 发现 `/sys/class/dmi/id/product_uuid` 存在就去读，但该文件在容器里通常是 root-only。
这不是授权绕过，只是容器环境的文件权限问题。二选一：

```bash
# ① 放开读权限（推荐；能读到真实 product_uuid）
sudo chmod a+r /sys/class/dmi/id/product_uuid

# ② sysfs 不允许 chmod 时：用空文件覆盖，让指纹回退到 /etc/machine-id
sudo mount --bind /dev/null /sys/class/dmi/id/product_uuid
```

> 公开仓 CI 就是这么处理的（见 `.github/workflows/build.yml` 的 `Prepare Linux license fingerprint`）。
> 真实生产环境若使用容器，建议在镜像/宿主机层面保证 `/etc/machine-id` 可读，并评估指纹来源是否符合授权策略。

---

## 换个端口 / 换个设备

设备 IP / 端口 / 点表都在示例目录的 XML（或 JSON）里，**改文件后重跑即可**：

```bash
# 例如 QuickStart
# 用编辑器打开 samples/QuickStart/quickstart.xml，改 DeviceIp / Port / <TagItem>
cd samples/QuickStart && dotnet run -f net8.0
```

想用另一份配置：

```bash
dotnet run -f net8.0 -- --config ./我的设备.xml
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
| 产品包 | [`ZL.IotEngine`](https://www.nuget.org/packages/ZL.IotEngine) / [`ZL.IotDriver`](https://www.nuget.org/packages/ZL.IotDriver) / [`ZL.IotHub.Server`](https://www.nuget.org/packages/ZL.IotHub.Server) **2.3.0** |
| 许可 | 见 [LICENSE](./LICENSE) |
