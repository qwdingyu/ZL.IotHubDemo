# MultiDevice —— 多 PLC 统一管控（含“一台坏了”演示）

一台 PLC 谁都会连；**几十上百台怎么管**才是真问题：

- 统一启动，而不是每台写一遍连接代码；
- 逐台健康状态可见；
- **一台坏了不拖垮其他**；
- 统一优雅停止。

本示例用 `MultiDeviceOrchestrator` 管 4 台设备，其中 **PLC-OFFLINE 故意配错端口**，
用来演示“健康可见”和“故障隔离”。

## 跑

```bash
# 另开窗口：fleet.json 会起 3 台仿真 PLC（端口 31101/31102/31103）
cd samples/MultiDevice
plcsim --batch fleet.json --headless --stay-alive --auto-start

# 回到仓库根目录
cd ../..
dotnet run --project samples/MultiDevice/MultiDevice.csproj -f net8.0
```

## 设备/点表配置（改配置不用重新编译）

- [`devices.json`](./devices.json)：4 台设备的 `DeviceId` / `DeviceIp` / `Port` / `TagFilePath`；
  `TagFilePath` 相对该文件所在目录解析；
- [`tags.json`](./tags.json)：点表（`TEMP` / `COUNT`），由 **`TagKit`** 解析；
- [`fleet.json`](./fleet.json)：**只给仿真器**用（`plcsim --batch fleet.json`），与本程序配置分开。

> 端口改一处即可：`fleet.json` 和 `devices.json` 里的端口要保持一致（3 台在线设备的端口）。
> 想换一套设备/点表：`dotnet run -f net8.0 -- --config 我的舰队.json`。

## 关键输出

```
【1】舰队规模：4 台（其中 PLC-OFFLINE 故意配错，用于演示健康可见）
【2】统一启动（StartAsync）
      ✅ PLC-01 ...
      ✅ PLC-02 ...
      ✅ PLC-03 ...
      🔴 PLC-OFFLINE ...（连接失败原因）
【3】舰队健康状态（GetHealthStatus）
【4】逐台读同一个点位（DB1.DBD4）
      ✅ 三台正常返回；PLC-OFFLINE 失败但不影响其他
【5】统一停止
```

> 这个示例的重点不是“连上”，而是 **故障隔离 + 健康可见 + 生命周期统一**。
> 真实项目里，这三点决定“半夜一台 PLC 掉线，会不会把整个采集进程拖死”。
