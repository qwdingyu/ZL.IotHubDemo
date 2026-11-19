# BadPointIsolation · 坏点隔离与恢复

**一句话：一个坏点，拖不垮整块采集。**

真实产线上总有坏点（地址写错、设备不支持、线缆问题）。如果坏点每次都拖到超时，
整个采集周期都会被它拖慢。ZL.IotHub 会把反复失败的坏点**自动隔离**，好点照常采。

## 跑

```bash
dotnet tool install -g PlcSimulator.Cli     # 一次性
plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start   # 另开一个窗口
cd samples/BadPointIsolation && dotnet run -f net8.0
```

> 🔴 设备/点表在 [`badpoint.xml`](./badpoint.xml) 里：`DeviceIp` / `Port` / 所有 `<TagItem>`
> 都改这个文件；由 `TagKit` 解析，改配置不用重新编译。

## 你会看到

```
【1】已连接（NativeUnifiedDriver），点表 5 个（其中 1 个是坏点）
【3】隔离结果
      被隔离的点位数: 1
      🔴 BROKEN（地址 T1）已被隔离，不再拖慢采集

【4】好点照常采集（这就是隔离机制的意义）
      ✅ TEMP   = 12     质量=Good
      ✅ COUNT  = 1333   质量=Good
      ✅ RUN    = True   质量=Good
```

## 说明

- 示例点表里**故意放了一个坏地址**（`T1`）来触发隔离：
  - `XYZ123` 这类**解析期非法**地址会在建计划时就被过滤掉，**不会**触发隔离；
  - `T1`（定时器）地址合法，但 S7 读响应长度与期望不符 ⇒ **稳定失败** ⇒ 进入隔离。
- **真实场景里**的触发条件是「读失败」（超时、协议错误、设备不认这个地址）。
- 隔离的点位会进入 `driver.QuarantineTags`，框架会持续重试，**恢复了会自动放回**。
- 本地仿真器对越界/不存在的地址也照常返回数据 ⇒ 可能看不到隔离，**这是预期行为**，
  不是缺陷；真机验证请把 `BROKEN` 改成 PLC 里真实不存在的 DB 号。

## 下一步

按推荐顺序，下一步看 **[MultiDevice](../MultiDevice)**：
从“单台坏点隔离”升级到“多台设备故障隔离 + 健康可见”。
