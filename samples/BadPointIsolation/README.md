# BadPointIsolation · 坏点隔离与恢复

**一句话：一个坏点，拖不垮整块采集。**

真实产线上总有坏点（地址写错、设备不支持、线缆问题）。如果坏点每次都拖到超时，
整个采集周期都会被它拖慢。ZL.IotHub 会把反复失败的坏点**自动隔离**，好点照常采。

## 跑

```bash
dotnet tool install -g PlcSimulator.Cli     # 一次性
plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start   # 另开一个窗口
cd samples/BadPointIsolation && dotnet run
```

## 你会看到

```
【3】隔离结果
      被隔离的点位数: 1
      🔴 BROKEN（地址 XYZ123）已被隔离，不再拖慢采集

【4】好点照常采集（这就是隔离机制的意义）
      TEMP  = 205885
      COUNT = 32767
      RUN   = True
```

## 说明

- 示例点表里**故意放了一个坏地址**（`XYZ123`）来触发隔离。
- **真实场景里**的触发条件是「读失败」（超时、协议错误、设备不认这个地址）；
  本地仿真器很宽容，所以示例用「格式非法的地址」来触发。
- 隔离的点位会进入 `driver.QuarantineTags`，框架会持续重试，**恢复了会自动放回**。
- 想自己验证：把 `BROKEN` 的地址改成 `DB1.DBD4`，再跑一次，隔离数就会变成 0。
