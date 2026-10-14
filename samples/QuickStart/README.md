# QuickStart —— 最小采集闭环

演示最短路径：**点表 → 连接 → 读取 → 写入 → 读回比对**。

## 跑起来

**① 起仿真器**（另开一个窗口）：

```bash
dotnet tool install -g PlcSimulator.Cli     # 一次性
plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
```

**② 跑示例**：

```bash
dotnet run
```

**③ 预期输出**：

```
已连接（NativeUnifiedDriver）
RUN=True  COUNT=32767  TEMP=205885
✅ 写入=读回（12345），地址与类型正确。
```

退出码：**0 = 通过；1 = 失败**（可直接用在脚本/CI 里）。

## 改连接参数

```bash
dotnet run -- --ip 192.168.1.10 --port 102
```

## 换协议（Modbus）

把 `Protocol` 改为 `modbus-tcp`，并补上 `Station` 与 `AddressStartWithZero`：

```csharp
cfg.Add("Protocol", "modbus-tcp");
cfg.Add("Station", (byte)1);
cfg.Add("AddressStartWithZero", true);

var tags = new List<TagItem>
{
    new() { Id = "COIL_RUN", Address = "COIL1", DataTypeCode = "bool",  TagType = "M", StationNo = DeviceId },
    new() { Id = "HR_TEMP",  Address = "HR100", DataTypeCode = "short", TagType = "M", StationNo = DeviceId },
};
```

对应仿真器：

```bash
plcsim --protocols modbus-tcp --ports modbus=31502 --headless --auto-start
dotnet run -- --port 31502
```

## 连不上怎么办

程序会直接提示。最常见的原因：**忘了起仿真器**，或**端口不一致**
（示例的 `Port` 必须与仿真器 `--ports` 的 `s7=` 相同）。

若提示与授权有关：免费版支持 **≤ 10 台设备、协议全开**；
超过 10 台需要正式设备授权——**授权咨询：微信 `qwdingyu`**。
