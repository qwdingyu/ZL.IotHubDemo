# OpcUaClient · 仿真器/PLC 作为 OPC UA Server，我们作为客户端采集

> **一句话：PLC 本身就是 OPC UA Server 时，直接按 NodeId 读写，不必先转换协议。**

这个示例演示 OPC UA 的**反向链路**：

```
plcsim --protocols opcua  (OPC UA Server, 4899)
        │
        ▼
OpcUaClientDriver (本示例)
        │  NodeId: ns=2;s=opcua.temp
        ▼
控制台输出 / 后续可接入业务
```

配套的“我们作为 OPC UA Server”场景见 [`../OpcUaServer`](../OpcUaServer)。

## 场景 A：仿真器作为 OPC UA Server（默认）

```bash
cd samples/OpcUaClient

# ① 起 OPC UA 仿真器（--points-file 必须显式给，否则没有点位）
plcsim --protocols opcua --ports opcua=4899 \
       --points-file points.json --headless --stay-alive --auto-start

# ② 另开窗口，跑客户端
cd ../..
dotnet run --project samples/OpcUaClient -f net8.0
```

实测输出：

```
  端点：opc.tcp://127.0.0.1:4899

✅ 已连接（OpcUaClientDriver）

      count      = 65535   （ns=2;s=opcua.count）
      temp       = 42.5    （ns=2;s=opcua.temp）

【写入 → 读回：temp】
      temp 写 42.5 → 读回 42.5  ✅

══════ 完成：OPC UA 读/写链路验证通过 ══════
```

> `points.json` 里的 `address` 是**仿真器内部 PLC 内存地址**（`DB1.0`），
> 不是 OPC UA NodeId；NodeId 由 `tag` 字段生成：`ns=2;s=opcua.temp`。

## 场景 B：我们的程序作为 OPC UA Server

先启动 [`../OpcUaServer`](../OpcUaServer)（S7 采集 → OPC UA 发放），然后：

```bash
dotnet run --project samples/OpcUaClient -f net8.0 -- \
  --config samples/OpcUaClient/opcua-server.xml
```

实测输出：

```
  端点：opc.tcp://127.0.0.1:4840

✅ 已连接（OpcUaClientDriver）

      Temperature = 2.360172E-38   （ns=2;s=tag.PLC001.Temperature）
      Pressure    = 205885        （ns=2;s=tag.PLC001.Pressure）
      Count       = 32767         （ns=2;s=tag.PLC001.Count）
      Run         = True          （ns=2;s=tag.PLC001.Run）
      Setpoint    = 60            （ns=2;s=tag.PLC001.Setpoint）
```

节点规则与 Server 启动日志一致：`ns=2;s=tag.<设备>.<点位>`。

> OPC UA Server 示例的地址空间目前是**只读**的（写入走 REST `batch-write`），
> 所以 `opcua-server.xml` 里的 Setpoint 没有 `AccessMode=ReadWrite`，
> 客户端不会尝试 OPC UA 写入。

## 配置

| 文件 | 用途 |
|---|---|
| [`opcua-sim.xml`](./opcua-sim.xml) | 连仿真器 `4899`，NodeId `ns=2;s=opcua.*` |
| [`opcua-server.xml`](./opcua-server.xml) | 连本仓 `OpcUaServer` `4840`，NodeId `ns=2;s=tag.PLC001.*` |
| [`points.json`](./points.json) | 仿真器 `--points-file` 的点定义 |

改 IP / 端口 / NodeId 只改 XML，不用重新编译。

## 关键事实（实测）

1. **NodeId 是字符串**：`ns=2;s=opcua.temp`。
   `ns=2;i=10000` / `ns=2;i=2` 会返回 `BadNodeIdUnknown`（仿真器当前实现）。
2. **2.3.0 的 `NativeDriversPlugin` 还没有注册 `opcua` 协议键**，
   所以本示例直接使用 `OpcUaClientDriver`（而不是 `DriverFactory.Create`）。
   后续版本若已注册，可改为 `DriverFactory` + 点表。
3. **安全策略**：当前客户端/仿真器示例都走 `None`（明文），只适合内网/POC。
   `OpcUaClientDriver` 的非 `None` 策略尚未接线，配置了会明确失败而不是静默降级。
4. **写能力**：
   - 对仿真器 OPC UA Server：可写（已验证 42.5 写入并读回）；
   - 对 `OpcUaServer` 示例：OPC UA 节点只读，写请走 REST `batch-write`。

## 相关

- 我们作为 OPC UA Server → [`../OpcUaServer`](../OpcUaServer)
- 最小采集闭环 → [`../QuickStart`](../QuickStart)
- 批量读取核心价值 → [`../BatchRead`](../BatchRead)
