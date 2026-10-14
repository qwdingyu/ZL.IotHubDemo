# QuickStart · 采集最小闭环

**一句话：连上设备 → 读到值 → 写进去 → 读回一致。**

最后那步「写入 = 读回」是**最便宜的自检**：地址、类型、端序任一处写错，
这里立刻暴露，而不是等到了现场才发现。

## 跑

```bash
dotnet tool install -g PlcSimulator.Cli     # 一次性
plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start   # 另开一个窗口
cd samples/QuickStart && dotnet run
```

## 你会看到

```
已连接（NativeUnifiedDriver）
RUN=True  COUNT=32767  TEMP=205885
✅ 写入=读回（12345），地址与类型正确。
```

退出码：**0 = 通过；1 = 失败**（可直接用在脚本/CI 里）。

## 换成你自己的设备

```bash
dotnet run -- --ip 192.168.1.10 --port 102
```

改点表：

```csharp
new() { Id = "TEMP", Address = "DB1.DBD4", DataTypeCode = "float", TagType = "M", StationNo = DeviceId },
```

- S7 地址：`DB1.DBX0.0`（位）、`DB1.DBW2`（字）、`DB1.DBD4`（双字 · float）
- `DataTypeCode`：`bool` / `short` / `int` / `float` / `string`
- Modbus：把 `Protocol` 改成 `modbus-tcp`，并补 `Station` 与 `AddressStartWithZero`

## 连不上？

程序会直接提示。最常见的是**忘了起仿真器**，或**端口不一致**
（`--port` 必须与仿真器 `--ports` 的 `s7=` 相同）。
