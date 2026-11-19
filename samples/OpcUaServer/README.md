# OpcUaServer · 普通协议采集 → 我们的程序作为 OPC UA Server 发放

> **一句话：PLC 用什么协议不重要，采集进来后统一从 OPC UA / REST / SignalR 三个口出去。**

这个示例演示 **ZL.IotHub.Server 的 OPC UA 对外暴露能力**：

```
plcsim (Siemens S7, 31002)
        │  普通协议采集
        ▼
ZL.IotEngine + ZL.IotDriver
        │  统一缓存 / 质量码 / 事件
        ▼
ZL.IotHub.Server
   ├── OPC UA Server  opc.tcp://127.0.0.1:4840   ← SCADA / MES / UaExpert
   ├── REST           http://localhost:5080/api  ← 看板 / 脚本
   └── SignalR        /scadahub                  ← Web 前端
```

采集侧可以是 S7 / Modbus / 三菱 / 欧姆龙……**任意已支持协议**；
对上层只暴露一套 OPC UA 地址空间。

## 跑

### ① 起一台 S7 仿真器（普通协议，不是 OPC UA）

```bash
plcsim --protocols siemens-s7 --ports s7=31002 --headless --stay-alive --auto-start
```

### ② 起 OPC UA 网关

```bash
dotnet run --project samples/OpcUaServer -f net8.0
```

- HTTP：`http://localhost:5080`
- OPC UA：`opc.tcp://127.0.0.1:4840`
- 设备/点表在 [`appsettings.json`](./appsettings.json) 的 `Scada:Devices`；
  改 IP / 端口 / 点表**不用重新编译**。
- ⚠️ 示例给 `Setpoint` 配了 `InitialValue=60.0` + `WriteInitialValue=true`（演示用，避免仿真器未初始化时读到 NaN）；
  **接真实 PLC 前请把 `WriteInitialValue` 改为 `false`**，避免启动时覆盖现场设定值。

启动日志会明确打印：

```
[OPC UA] 已用中枢最新值快照播种 5 个点位初始值（避免 SCADA 首连看到 0）
[OPC UA] 已对外暴露：opc.tcp://<host>:4840 ｜ 设备 1 个 / 点位 5 个 ｜ 节点规则 ns=2;s=tag.<设备>.<点位>
```

### ③ 验证三条出口

**REST（缓存读）**：

```bash
curl http://localhost:5080/health

curl -X POST http://localhost:5080/api/devices/PLC001/batch-read \
  -H 'Content-Type: application/json' \
  -d '{"tagIds":["Temperature","Pressure","Count","Run","Setpoint"]}'
```

**REST 写入并回读校验**：

```bash
curl -X POST 'http://localhost:5080/api/devices/PLC001/batch-write?verify=true' \
  -H 'Content-Type: application/json' \
  -d '{"writes":[{"tagId":"Setpoint","value":66.5}]}'
```

**OPC UA 读取**（用本仓的 OPC UA Client 示例）：

```bash
dotnet run --project samples/OpcUaClient -f net8.0 -- \
  --config samples/OpcUaClient/opcua-server.xml
```

输出：

```
✅ 已连接（OpcUaClientDriver）
      Temperature = 2.360172E-38   （ns=2;s=tag.PLC001.Temperature）
      Pressure    = 205885        （ns=2;s=tag.PLC001.Pressure）
      Count       = 32767         （ns=2;s=tag.PLC001.Count）
      Run         = True          （ns=2;s=tag.PLC001.Run）
      Setpoint    = 66.5          （ns=2;s=tag.PLC001.Setpoint）
```

> 也可以用 UaExpert / Ignition / KEPServerEX 客户端连
> `opc.tcp://127.0.0.1:4840`，节点为 `ns=2;s=tag.PLC001.Temperature`。

## 这个示例用到了 Server 的哪些优化

| 优化 | 说明 |
|---|---|
| **零配置 OPC UA** | `OpcUa:Enabled` 默认 `true`、端口默认 4840；不配也能用 |
| **自动建地址空间** | 启动时按 `DeviceManager` 的现有设备/点表自动建节点 |
| **统一 NodeId 规则** | `ns=2;s=tag.<设备>.<点位>`，不用手工算 namespace index |
| **初值播种** | 用 `PlcNotificationHub` 最新快照播种，SCADA 首连不会看到 0 |
| **逐设备故障隔离** | 一个设备注册失败只跳过该设备，其余照常暴露 |
| **质量码/时间戳** | 值更新走引擎路径，保留真实 `Quality` 与采集时间，不降级成 Good |
| **一套采集三套出口** | REST / SignalR / OPC UA 共用同一份采集结果，不重复读 PLC |

## 已知限制（2.3.0，务必先读）

1. **OPC UA 写入能力按版本区分**：
   - **NuGet 2.3.0（本示例当前引用）**：OPC UA 节点**只读**；写请走 REST `batch-write`
     （本示例已验证 66.5 写入并读回）；直接写 OPC UA 节点返回 `BadNotWritable`。
   - **下一个版本（源码已实现）**：点表显式配置 `AccessMode="ReadWrite"` 的点，
     OPC UA 客户端可**直接写**；未配置的点仍只读。写入复用同一条 `DeviceManager` 链路
     （权限/类型/缓存/审计不重复实现），并可用 `OpcUa:EnableWrite=false` 整体关闭。
     届时**无需改示例代码**，只需显式配置可写点。
2. **安全策略只支持 `None`（明文）**：仅建议内网/POC；生产需安全策略 + 证书。
   配置非 `None` 会 **fail-loud 取消启动**，不会静默降级成明文。
3. **启动后动态新增的设备不会自动出现在 OPC UA 地址空间**：
   地址空间只在启动时建一次；需重启服务（或等待动态注册能力）。
4. **`batch-read?live=true` 的顶层计数**：
   2.3.0 包存在“每个点位 `success=true`、但顶层 `succeeded=0`”的计数缺陷；
   源码已修复，待下一版发布。示例默认用**缓存读**，写读校验用 `batch-write?verify=true`，
   不受该缺陷影响。

## 安全提醒

- 本示例监听 `http://localhost:5080` 与 `opc.tcp://0.0.0.0:4840`；
- OPC UA 为明文 `None`，**不要直接暴露到公网**；
- 生产部署请启用鉴权、网络隔离与安全策略。

## 下一步

- 反向场景（仿真器/PLC 本身就是 OPC UA Server）→ [`../OpcUaClient`](../OpcUaClient)
- 业务壳/诊断/运行时增删 → [`../Advanced`](../Advanced)

## 相关

- 反向场景（仿真器/PLC 本身就是 OPC UA Server）→ [`../OpcUaClient`](../OpcUaClient)
- 普通协议最小闭环 → [`../QuickStart`](../QuickStart)
- Web/SCADA 网关（SignalR + REST）→ 见上方「REST 写入并回读校验」与三出口说明
