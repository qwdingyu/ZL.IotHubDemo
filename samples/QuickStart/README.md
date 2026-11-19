# QuickStart · 采集最小闭环

**一句话：连上设备 → 读到值 → 写进去 → 读回一致。**

最后那步「写入 = 读回」是**最便宜的自检**：地址、类型、端序任一处写错，
这里立刻暴露，而不是等到了现场才发现。

## 跑

```bash
dotnet tool install -g PlcSimulator.Cli     # 一次性
plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start   # 另开一个窗口
cd samples/QuickStart && dotnet run -f net8.0
```

## 你会看到

```
已连接（NativeUnifiedDriver）  点表 3 个点位
RUN=True  COUNT=32767  TEMP=205885
✅ 写入=读回（12345），地址与类型正确。
```

退出码：**0 = 通过；1 = 失败**（可直接用在脚本/CI 里）。

## 换成你自己的设备/点表（不用重新编译）

设备和点表都在 [`quickstart.xml`](./quickstart.xml)：

```xml
<Device DeviceId="MY-DEVICE" Protocol="siemens-s7"
        DeviceIp="127.0.0.1" Port="31002" Rack="0" Slot="1">
  <TagItem Id="COUNT" Address="DB1.DBW2" DataTypeCode="short" AccessMode="ReadWrite" />
  ...
</Device>
```

- 根节点属性 → `DeviceConfig`（`DeviceIp` / `Port` / `Protocol` / `Rack` / `Slot` …）
- `<TagItem>` 由 **`TagKit`** 解析（与生产点表同一套）
- 改完直接重跑；想用另一份配置：

```bash
dotnet run -f net8.0 -- --config ./我的设备.xml
```

地址写法：

- S7：`DB1.DBX0.0`（位）、`DB1.DBW2`（字）、`DB1.DBD4`（双字 · float）
- `DataTypeCode`：`bool` / `short` / `int` / `float` / `string`
- Modbus：把 XML 里的 `Protocol` 改成 `modbus-tcp`，并补 `Station` 与 `AddressStartWithZero`
- 要写入的点：显式加 `AccessMode="ReadWrite"`（默认只读）

> ⚠️ 示例代码按 `RUN` / `COUNT` / `TEMP` 三个 **Id** 读写；换点表时同步改 `Program.cs` 里这几个 Id。
> 地址/类型改 XML 即可；**Id 是代码与点表之间的契约**。

## 连不上？

程序会直接提示。最常见的是**忘了起仿真器**，或**端口不一致**
（`quickstart.xml` 里的 `Port` 必须与仿真器 `--ports` 的 `s7=` 相同）。
