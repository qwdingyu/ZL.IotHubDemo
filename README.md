# ZL.IotHub 示例

**ZL.IotHub 是面向 .NET 的工业 PLC 数据采集运行时。**
一句话：**它让"把几百个点位采回来"这件事变得又快又稳。**

本仓库是官方示例，每个示例都能独立编译运行，可以直接拷进你的项目。

---

## 核心功能 → 看哪个示例

| 核心功能 | 一句话说明 | 示例 |
| --- | --- | --- |
| **地址合并批量读取** | 几百个点位自动合并成几个请求，采集周期才压得下来 | **[BatchRead](./samples/BatchRead)** ⭐ |
| **坏点隔离 / 恢复** | 一个坏点，拖不垮整块采集 | [BadPointIsolation](./samples/BadPointIsolation) |
| **变化上报** | 值变了才回调你，不用自己轮询比对 | [Events](./samples/Events) |
| **采集最小闭环** | 连上 → 读到值 → 写进去 → 读回一致 | [QuickStart](./samples/QuickStart) |
| **多协议接入** | 换 Modbus 只改三样东西 | [Modbus](./samples/Modbus) |

---

## 5 分钟跑通

**① 起一台仿真器**（没有真机也能练）：

```bash
dotnet tool install -g PlcSimulator.Cli     # 一次性
plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
```

**② 跑示例**（另开一个窗口，进对应目录）：

```bash
cd samples/BatchRead
dotnet run
```

**③ 你会看到**：

```
【3】采集计划（核心）
      点位总数    : 300
      合并后块数  : 6
      请求数减少  : 98.00%
      ⇒ 原来要 300 次请求，现在约 6 次
```

---

## 换个端口 / 换个设备

```bash
dotnet run -- --ip 192.168.1.10 --port 102
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
| 产品包 | [`ZL.IotHub` on NuGet](https://www.nuget.org/packages/ZL.IotHub) |
| 许可 | 见 [LICENSE](./LICENSE) |
