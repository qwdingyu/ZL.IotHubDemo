# DriverOnly · 只装 `ZL.IotDriver` 的最小快速入门

> **一句话：不引入引擎，不学编排，只装一个驱动包，直接连 PLC 读写。**
> 已实测：S7 / Modbus TCP / Omron FINS / 三菱 MC 四种协议，读写回读全部一致。

适合这些场景：

- 你已经有自己的调度器、线程池、数据库、日志、重连逻辑；
- 你只想“拿一个驱动，`ConnectServer()` 之后读写地址”；
- 你不想引入 `ZL.IotEngine` 的 `DriverFactory` / `DeviceManager` / 生命周期管理。

```bash
dotnet add package ZL.IotDriver --version 2.3.0
```

**不需要** `ZL.IotEngine`。

---

## 〇、前置条件

| 项 | 要求 | 说明 |
|---|---|---|
| .NET SDK | 8.0 或更高 | 示例目标框架 `net8.0`（同时可编译 `net48`） |
| PLC 或仿真器 | 二选一 | 没有真机时用 `PlcSimulator.Cli`，见下 |
| 网络 | 能访问 PLC 的 IP + 端口 | 真机请先把防火墙/端口放通 |

安装仿真器（可选）：

```bash
dotnet tool install -g PlcSimulator.Cli --version 2.2.14
plcsim --help
```

> 已有真机可以跳过这步，直接看第三节「真机怎么改」。

---

## 一、30 秒跑通（S7 示例）

```bash
# 窗口 1：起仿真器
plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start

# 窗口 2：跑示例
dotnet run --project samples/DriverOnly -f net8.0 -- --protocol s7
```

输出：

```text
[S7] 连接 127.0.0.1:31002（S7-1200）
  ✅ 已连接（SiemensS7Driver）

[S7 读] RUN=True COUNT=32767 TOTAL=1212747584 TEMP=NaN TEXT=''
[S7 写] COUNT=True TOTAL=True TEMP=True TEXT=True
[S7 回读] COUNT=1234 TOTAL=123456 TEMP=42.5 TEXT='HELLO'
  ✅ S7 读/写/回读一致
```

最小骨架（**只有 4 步**：new → Connect → Read/Write → Close）：

```csharp
using System.Text;
using ZL.IotAbstraction.Tag;
using ZL.IotDriver.Siemens;

using var plc = new SiemensS7Driver(
    SiemensCpuType.S7_1200, "127.0.0.1", 31002, rack: 0, slot: 1);

var c = plc.ConnectServer();
if (!c.IsSuccess) { Console.WriteLine(c.Message); return; }

// 读
var run   = plc.ReadBool("DB1.DBX0.0");
var count = plc.ReadInt16("DB1.DBW2");
var total = plc.ReadInt32("DB1.DBD4");
var temp  = plc.ReadFloat("DB1.DBD8");
var text  = plc.ReadS7String("DB1.DBB20", Encoding.UTF8);

// 写
plc.Write("DB1.DBW2", (short)1234);
plc.Write("DB1.DBD4", 123456);
plc.Write("DB1.DBD8", 42.5f);
plc.WriteString("DB1.DBB20", "HELLO", StringKind.S7String, 20, Encoding.UTF8);
```

> 所有 API 都返回 `DeviceResult` / `DeviceResult<T>`：
> 先看 `IsSuccess`，再取 `Content` 或 `Message`。**不要**用异常做正常错误处理。

---

## 二、四种协议，一张表

| 协议 | 驱动类 | 仿真器端口 | 真机端口 | 地址示例 | 本示例覆盖类型 |
|---|---|---:|---:|---|---|
| Siemens S7 | `SiemensS7Driver` | 31002 | 102 | `DB1.DBX0.0` / `DB1.DBW2` / `DB1.DBD4` | bool / short / int / float / S7String |
| Modbus TCP | `ModbusTcpDriver` | 31502 | 502 | `HR0` / `HR1` / `HR3` / `HR10` | short / int / float / Raw string |
| Omron FINS/TCP | `OmronFinsDriver` | 9600 | 9600 | `D0.0` / `D1` / `D2` / `D4` / `D10` | bool / short / int / float / Raw string |
| Mitsubishi MC | `MitsubishiMcDriver` | 32000 | 5000（以设备为准） | `M0` / `D0` / `D1` / `D3` / `D10` | bool / short / int / float / Raw string |

运行：

```bash
# S7
plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
dotnet run --project samples/DriverOnly -f net8.0 -- --protocol s7

# Modbus TCP
plcsim --protocols modbus-tcp --ports modbus=31502 --headless --auto-start
dotnet run --project samples/DriverOnly -f net8.0 -- --protocol modbus

# Omron FINS/TCP
plcsim --protocols fins-tcp --ports fins-tcp=9600 --headless --auto-start
dotnet run --project samples/DriverOnly -f net8.0 -- --protocol omron

# Mitsubishi MC（Qna-3E 二进制）
plcsim --protocols melsec-mc --ports melsec-mc=32000 --headless --auto-start
dotnet run --project samples/DriverOnly -f net8.0 -- --protocol melsec
```

> 四个示例的断言相同：**写进去 → 读回来 → 值必须相等**。
> 只要有一个不一致，程序返回非 0 退出码，可直接接进 CI。

---

## 三、真机怎么改

示例里的 IP 和端口全部是命令行参数，**不用改代码**：

```bash
dotnet run --project samples/DriverOnly -f net8.0 -- \
  --protocol s7 --ip 192.168.1.10 --port 102
```

各协议的构造参数：

| 协议 | 构造写法 | 关键参数 |
|---|---|---|
| S7-1200 / S7-1500 | `new SiemensS7Driver(SiemensCpuType.S7_1200, ip, 102, rack: 0, slot: 1)` | CPU 在 TIA 里的机架/槽位；连不上时试 `slot: 0` |
| S7-300 | `new SiemensS7Driver(SiemensCpuType.S7_300, ip, 102)` | 驱动按型号自动用机架 0 / 槽 2；不用显式传 |
| S7-200 SMART | `new SiemensS7Driver(SiemensCpuType.S7_200Smart, ip, 102)` | 字符串布局由驱动自动切换为 SMART 布局 |
| Modbus TCP | `new ModbusTcpDriver(ip, 502, stationNumber: 1, addressing: ModbusAddressingMode.Raw, defaultArea: "HR")` | 站号；Raw / Modicon 二选一 |
| Omron FINS/TCP | `new OmronFinsDriver(ip, 9600)` | 节点号默认自动协商 |
| Mitsubishi MC | `new MitsubishiMcDriver(MitsubishiVersion.Qna_3E, ip, 5000)` | 老设备用 `A_1E`；端口以设备侧设定为准 |

> **S7 连不上**的常见原因：① 没开 PUT/GET 或未允许外部访问；② rack/slot 不对（表现为握手超时）；
> ③ 端口不是 102。驱动不会把「TSAP 配错」翻译成明确错误，只表现为超时 —— 先查这两项。

---

## 四、只装 Driver 时，字符串怎么写？

这是最容易踩的边界：

| 驱动 | 字符串读 | 字符串写 |
|---|---|---|
| `SiemensS7Driver` | `ReadS7String(...)` | `WriteString(..., StringKind.S7String, ...)`（**实例方法**） |
| `ModbusTcpDriver` | `ReadString(...)` | `WriteString(..., StringKind.Raw, ...)`（**实例方法**） |
| `OmronFinsDriver` | `ReadString(...)` | **没有 `WriteString`** ⇒ `Encoding.GetBytes` + `WriteBytes(...)` 补到字段宽度 |
| `MitsubishiMcDriver` | `ReadString(...)` | **没有 `WriteString`** ⇒ 同上 |

> 🔴 `PlcDeviceStringWriteExtensions.WriteString(this IPlcDevice, ...)` 在 **`ZL.IotEngine`** 里，
> 不在 `ZL.IotDriver` 里。只装 Driver 时**不要**写 `dev.WriteString(...)` 扩展调用。
> S7 / Modbus 能直接 `WriteString`，是因为它们的驱动类**自己提供了实例方法**，不是扩展方法。

Omron / 三菱 字符串写示例：

```csharp
var payload = new byte[8];                       // 字段 8 字节
Encoding.ASCII.GetBytes("HELLO").CopyTo(payload, 0);
plc.WriteBytes("D10", payload);

var text = plc.ReadString("D10", 8, Encoding.ASCII);
```

> 字段宽度必须由调用方保证：`WriteBytes` 只写你给的字节数，不会自动补零到字段末尾。
> 想让剩余字节为 0，就像上面一样先 `new byte[8]`（默认全 0）再拷内容。

字符串布局与编码的完整对照见：[`STRING.md`](./STRING.md)
（`Raw` / `S7String` / `S7WString` / SMART 两种布局，以及 net8 与 net48 的编码差异）。

---

## 五、只装 Driver vs 加 Engine

| 能力 | 只装 `ZL.IotDriver` | 加 `ZL.IotEngine` |
|---|---|---|
| 直接 `new` 驱动、Connect/Read/Write | ✅ | ✅ |
| 驱动自动选择（`DriverFactory`） | ❌ | ✅ |
| 设备单例 / 按 ID 寻址（`DeviceManager`） | ❌ | ✅ |
| 自动重连 / 心跳 / 后台采集 | ❌ 自己写（S7 驱动仅在检测到未连接时尝试**重连一次**） | ✅ 后台持续重连 |
| 地址合并 / 批量读取 | ❌ | ✅ |
| 缓存 / 质量码 / 隔离区 | ❌ | ✅ |
| OPC UA / REST / SignalR | ❌ | ✅（Server） |

> **只装 Driver 不是“残缺版”**：它是“驱动库”定位，适合已有框架的团队。
> 需要编排/缓存/批量/多设备管理时，再加 `ZL.IotEngine`。
>
> 驱动包只依赖 `ZL.IotAbstraction`，不会把整个引擎拖进来。
> 授权链路是 `ZL.IotDriver → ZL.IotAbstraction → ZL.License`。IO 通道有 L2 检查、写操作有 L3 门禁；
> **Trial 态无时长限制、不限协议、可读写**（产品策略），正常试用不会被拦。
> 正式授权按 `ZL.License` 的 license 文件配置。

---

## 六、类型速查

| 类型 | S7 | Modbus | Omron | Mitsubishi |
|---|---|---|---|---|
| `bool` | `ReadBool("DB1.DBX0.0")` | 见下方地址说明 | `ReadBool("D0.0")` | `ReadBool("M0")` |
| `short` | `ReadInt16("DB1.DBW2")` | `ReadInt16("HR0")` | `ReadInt16("D1")` | `ReadInt16("D0")` |
| `int` | `ReadInt32("DB1.DBD4")` | `ReadInt32("HR1")` | `ReadInt32("D2")` | `ReadInt32("D1")` |
| `float` | `ReadFloat("DB1.DBD8")` | `ReadFloat("HR3")` | `ReadFloat("D4")` | `ReadFloat("D3")` |
| `string` | `ReadS7String(...)` | `ReadString(..., Encoding.ASCII)` | `ReadString(..., Encoding.ASCII)` | `ReadString(..., Encoding.ASCII)` |

写入用同名 `Write` 重载：

```csharp
plc.Write(address, bool);
plc.Write(address, short);
plc.Write(address, int);
plc.Write(address, float);
plc.Write(address, byte[]);
```

> **`int` / `float` 占 2 个寄存器或 2 个字**。
> 所以 S7 用 `DBW2`（short）之后，下一个 `int` 要从 `DBD4` 开始；
> Modbus 用 `HR0`（short）之后，下一个 `int` 从 `HR1` 开始（占 HR1+HR2），
> 再下一个 `float` 从 `HR3` 开始。示例里的地址就是这样排的。

---

## 七、地址写法（**这一节请务必看**）

| 协议 | 写法 | 注意 |
|---|---|---|
| S7 | `DB1.DBX0.0`（位）/ `DB1.DBW2`（字）/ `DB1.DBD4`（双字） | 数据类型与地址宽度必须匹配 |
| S7 其它区 | `M0.0` / `I0.0` / `Q0.0` / `V0` | `E0.0`/`A0.0` 是德文兼容写法 |
| Modbus（Raw） | `HR0` / `IR0`；裸数字按 `defaultArea` | **0 基**，数字即 PDU 地址；推荐新人用这个 |
| Modbus（Modicon） | `40001`（保持寄存器）/ `30001`（输入寄存器） | **1 基归一**：`40001` = 保持寄存器偏移 0 |
| Modbus 线圈 | 裸数字 `0` / `1` / `2` | **0 基**，读写实测一致 |
| Omron FINS | `D0.0`（位）/ `D1`（字）/ `D2`（双字） | `D` 区按字；位用 `.0` |
| Mitsubishi MC | `M0`（位）/ `D0`（字）/ `D1`（双字） | 前缀齐全：`D/M/W/X/Y/L/B/F/V/S/R/ZR`… |

> ⚠️ **Modbus 5 位线圈地址（`00001` / `00002`）在 2.3.0 不要用于 `ReadBool`。**
> 实测：`Write("00001", true)` 落到**线圈偏移 0**（写路径做了一基归一），
> 而 `ReadBool("00001")` 读的是**线圈偏移 1**（位读路径未做一基归一）⇒ 写 A 点、读 B 点。
> 这是已知的「位读未裁定」项（主仓 `docs/564` §三.B），不是本示例的用法问题。
> **改用裸数字 `0` / `1`（0 基）**，位读写就一致。

---

## 八、常见错误

| 现象 | 原因 | 处理 |
|---|---|---|
| `连接失败：Connection refused` | 仿真器没起 / 端口不对 | 先确认 `plcsim` 那一行还在跑 |
| S7 一直超时 | rack/slot 不对，或 PLC 未允许外部访问 | 试 `slot: 0`；在 TIA 里确认槽位并允许 PUT/GET |
| `S7 WSTRING 容量…` | `Length` 按**字符数**，不是字节数 | WSTRING 的 `Length` 填字符数（见 docs/728） |
| `授权失效：通信被拒绝` | 授权/免费版策略不允许该协议 | 配好授权文件；或先确认协议在免费白名单内 |
| `XxxDriver 未连接` | 上次操作失败后连接被关闭 | 调 `ConnectServer()` 重连；只装 Driver 时**没有后台持续重连**（S7 驱动只会尝试一次） |
| 读回的 float 是 `NaN` | 该地址从未写过值（PLC 里是随机/初值） | 先写一次再读，或忽略首帧 |
| 中文写成 `?` | 用了 `Encoding.ASCII` | 改 `Encoding.UTF8`；或用 WSTRING |

---

## 九、下一步

- 想直接用完整采集引擎（自动重连/缓存/批量/事件） → [`../QuickStart`](../QuickStart)
- 想看地址合并 300 → 6 → [`../BatchRead`](../BatchRead)
- 想看字符串布局与编码 → [`STRING.md`](./STRING.md)
- 想看 OPC UA 双向 → [`../OpcUaClient`](../OpcUaClient) / [`../OpcUaServer`](../OpcUaServer)
- 回到 [仓库首页](../../README.md) 按推荐顺序看

---

## 十、口径

- 本文所有“实测”均指 **macOS + PlcSimulator 2.2.14 + ZL.IotDriver 2.3.0**；
- 真机行为可能与仿真器不同，**以真机为准**；
- 示例只做「最小可跑通」，不含生产级重连/限流/熔断；这些由调用方或 `ZL.IotEngine` 提供。
