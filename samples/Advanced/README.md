# Advanced · 从"能采集"到"能交付"（高级编排）

这个示例把 `ZL.IotEngine.Orchestration` 里**现场最有价值、但最容易被忽略**的能力串起来。
它不重复 QuickStart 的读写，而是回答：

> **采集跑通之后，怎么把它做成一个能交付、能运维、能排障的系统？**

## 跑

```bash
# 另开一个窗口：fleet.json 会起 2 台仿真 PLC（31002 / 31003）
cd samples/Advanced
plcsim --batch fleet.json --headless --stay-alive --auto-start

# 回到仓库根目录
cd ../..
dotnet run --project samples/Advanced -f net8.0
```

## 能力清单（本示例逐项演示）

| # | 能力 | API | 解决的问题 |
|---|---|---|---|
| 1 | **业务壳** | `ManagedDeviceBase` | 驱动创建/连接/重连/事件订阅全由框架管，业务只写回调 |
| 2 | **可判别读** | `ReadResult<T>` / `ReadResultAsync<T>` / `TryRead<T>` | `Read<T>` 失败返回 `default(T)`（bool= false），与真实值无法区分 |
| 3 | **缓存 + 质量码** | `GetValueResult<T>` / `TryGetValueResult<T>` / `GetTagValue` | HMI/巡检读缓存不产生 IO；质量码告诉你值可不可信 |
| 4 | **采集完成事件** | `DataCollected` | 每轮采集完成触发一次，适合统计吞吐/判断采集是否真的在跑 |
| 5 | **诊断报告** | `GetDiagnosticsReport()` | 每类 IO 的次数/成功率/平均/最大耗时；交付前自检、故障取证 |
| 6 | **点位快照** | `CaptureSnapshot()` | 一次性导出当前所有点位值，便于对拍/留档 |
| 7 | **设备寻址读写** | `DeviceManager.ReadByDeviceId(Async)` / `WriteByDeviceIdAsync` / `GetTagsByDeviceId` / `GetAllDeviceIds` | 上层业务不必持有驱动实例，按「设备 + 点位」寻址 |
| 8 | **运行时增删** | `AddDeviceAsync` / `RemoveDeviceAsync` | 不重启进程加/减一台设备（调试、扩容、临时接入） |
| 9 | **启动门禁** | `TryStart(out reason)` | 点表缺失/0 点位时**启动即拒绝**，不会"0 个点位照常跑" |
| 10 | **Overrun 建议** | `Overrun.TryGetSuggestion(...)` | 采集耗时逼近周期时给显式建议（不自动改调度） |
| 11 | **健康与优雅停止** | `GetHealthStatus()` / `StopAsync()` | 每台健康可见；停机会释放连接与授权名额 |

## 你会看到

```
【2】可判别读取（失败有原因，不是静默 default）
      ReadResult<float>(SETPOINT) = 12.5  ✅
      TryRead<float>(NO_SUCH_TAG) = False，value=0，error=标签不存在: 'NO_SUCH_TAG' ...
      对比 Read<float>(NO_SUCH_TAG) = 0  ← 静默 default，不要用它做报警判断

【4】快照 + 诊断报告
      CaptureSnapshot：3 个点位
      GetDiagnosticsReport：...
        ReadBlock            次数=5      成功率=100%   平均=2ms 最大=13ms

【7】运行时新增设备 ADV-02（端口 31003）→ 读数 → 移除
      AddDeviceAsync：✅ 连接成功
      ADV-02 TEMP=205885
      RemoveDeviceAsync：✅ 已移除并释放设备

【8】TryStart 失败快速验证（点表缺失 + RequireTags=true）
      TryStart=False（期望 False）
      reason=点表加载失败（FileNotFound）：点表文件【不存在】: .../no-such-tags.xml ...
```

## 选型：什么时候用哪个读 API

| 场景 | 用哪个 | 为什么 |
|---|---|---|
| 控制/联锁/报警判断 | **`ReadResult<T>` / `TryRead<T>`** | 失败必须可见；`Read<T>` 的 `false/0` 可能是"读失败" |
| HMI 周期刷新 | **`GetValueResult<T>` / `GetTagValue`** | 读缓存，不产生 PLC IO；带质量码 |
| 写后确认 | **`ReadResult<T>`**（穿透读） | 写完缓存可能还没刷新，必须读设备实时值 |
| 批量规划/离线检查 | `BuildPlan()` + `ReadPlan` | 纯内存，不需要连接（见 `BatchRead`） |
| 交付前/故障时 | `GetDiagnosticsReport()` + `CaptureSnapshot()` | 性能与点位状态的证据 |

## 配置

- [`devices.json`](./devices.json)：初始设备 `ADV-01`，`DeviceType` 指向业务壳 `AdvancedDevice`；
- [`tags.json`](./tags.json)：点表（`TEMP` / `SETPOINT` / `COUNT`），由 `TagKit` 解析；
- [`runtime-add.xml`](./runtime-add.xml)：运行时新增 `ADV-02` 的配置（IP/端口/点表）；
- [`bad-device.xml`](./bad-device.xml)：`TryStart` 失败验证（点表缺失 + `RequireTags=true`）；
- [`fleet.json`](./fleet.json)：**只给仿真器**用，起 2 台 PLC（31002/31003）。

> ⚠️ 示例会写 `SETPOINT=12.5` 与 `COUNT=4321`（不写 `TEMP` 测量值）；接真实 PLC 前请确认这些地址确实是允许写入的设定/计数点。

> 🔴 `ADV-02` 是**运行时新增**的设备：`AddDeviceAsync` 读取 `runtime-add.xml`，
> 其中 `TagFilePath="tags.json"` 由编排器相对配置文件目录解析。

## 还没在这一个示例里展开的进阶点

这些能力存在，但需要**自定义设备壳/更高授权/更多硬件**才能演示，本示例刻意不堆在一起：

| 能力 | API / 位置 | 适用场景 |
|---|---|---|
| **分级写队列** | `DevicePro.EnqueueSafetyWriteAsync` / `EnqueueNormalWriteAsync` / `GetQueueStatus` | 联锁/急停写绝对优先、普通写可去重（需自定义 `DevicePro` 子类并注入 `IPlcDevice`） |
| **多步操作串行化** | `DeviceRoot.ExecuteWithDeviceLock(Async)` | 读-改-写这类不能被打断的复合操作 |
| **批量计划明细** | `BuildPlan()` + `readPlan` | 离线检查地址合并效果（见 `BatchRead`） |
| **坏点隔离/恢复** | `QuarantineTags` | 单点坏不拖垮整块（见 `BadPointIsolation`） |
| **实时事件通道** | `PlcNotificationHub.SubscribeChannel` / `Acknowledge` / `GetMetricsSnapshot` | 有背压/确认语义的高吞吐推送 |
| **舰队级健康检查** | `PlcFleetManager` / `PlcHealthCheck` | 超大规模设备群的统一巡检 |

> 原则：**示例只展示"能跑通、能验证"的组合**；没有可复现的硬件/配置时，
> 宁可写清边界，也不放一段"看起来能跑"的伪示例。

## 注意

- 业务壳的构造必须是 **`(DeviceConfig, IPlcDriver)` 注入式**：
  驱动由编排层创建后注入，壳不要自己 `new` 驱动（否则授权名额/时序会出问题）。
- `OnManagedTriggerDataChange` 里只做轻量业务；不要阻塞、不要抛异常。
- 免费版最多 10 台设备；`fleet.json` 只有 2 台，不会触限。

## 下一步

- 回到 [仓库首页](../../README.md) 按推荐顺序重看；
- 想补基础 → [`QuickStart`](../QuickStart) / [`BatchRead`](../BatchRead)；
- 想看多设备故障隔离 → [`MultiDevice`](../MultiDevice)；
- 想看 OPC UA 两个方向 → [`OpcUaClient`](../OpcUaClient) / [`OpcUaServer`](../OpcUaServer)。
