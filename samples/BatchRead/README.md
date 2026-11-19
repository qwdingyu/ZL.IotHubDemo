# BatchRead · 地址合并批量读取

**一句话：300 个点位，我们只发 6 次请求。**

采集周期压不下来，往往是因为**逐个点位读**。ZL.IotHub 会按地址间隙自动把点位
合并成少量读取块——这个示例把合并前后的对比直接打给你看。

> ⚡ **默认不需要 PLC / 仿真器，10 秒就能看到结果**。
> 想验证写入，再起仿真器加 `--live`。

## 跑（默认离线）

```bash
cd samples/BatchRead
dotnet run -f net8.0
```

## 你会看到

```
  设备 127.0.0.1:31002  点位数 300（内置生成）

【2】采集计划（核心）
      点位总数    : 300
      合并后块数  : 6
      请求数减少  : 98.00%
      ⇒ 原来要 300 次请求，现在约 6 次

  ✅ 规划完成：这一步不需要连接 PLC，点表对不对先在这里看。
```

## 验证写读（可选，需要仿真器）

```bash
# 另开一个窗口
plcsim --protocols siemens-s7 --ports s7=31002 --headless --stay-alive --auto-start

# 本窗口
dotnet run -f net8.0 -- --live
```

追加输出：

```
【3】连接 127.0.0.1:31002
      已连接（NativeUnifiedDriver）

【4】写入 3 个已知值 → 读回验证
      TAG_0000 (DB1.DBD0) 写 1.5 → 实时读回 1.5  ✅
      TAG_0001 (DB1.DBD4) 写 12.5 → 实时读回 12.5  ✅
      TAG_0002 (DB1.DBD8) 写 100.25 → 实时读回 100.25  ✅
```

## 换成你自己的设备/点表（不用重新编译）

设备 IP/端口在 [`batchread.xml`](./batchread.xml)：

```xml
<Device DeviceId="BATCH-DEMO" Protocol="siemens-s7"
        DeviceIp="127.0.0.1" Port="31002" Rack="0" Slot="1">
  <!-- 想用自己的点表，就在这里写 <TagItem> -->
</Device>
```

- 默认不写 `<TagItem>`：示例生成 300 个连续 float（`DB1.DBD0` 起）演示地址合并；
- 写上 `<TagItem>`（由 `TagKit` 解析，与生产点表同一套）后，示例改用你的点表：

```xml
<TagItem Id="TEMP"  Address="DB1.DBD0" DataTypeCode="float" TagType="M" AccessMode="ReadWrite" />
<TagItem Id="COUNT" Address="DB1.DBW4" DataTypeCode="short" TagType="M" />
```

```bash
dotnet run -f net8.0 -- --config ./我的点表.xml
```

点位数也可以调（仅在未提供 `<TagItem>` 时生效）：

```bash
dotnet run -f net8.0 -- --tags 1000
```

## 这个数字对现场意味着什么

- 写入点位必须显式 `AccessMode = AccessMode.ReadWrite`（默认只读，fail-safe）；
- **请求次数**从 300 → 6：同样的采集周期，网络往返次数下降 98%；
- **PLC 负载**同步下降：每次请求的协议开销、连接/队列争用都省掉；
- 点表越"连续"，收益越大；**点表乱跳**时块数会变多，但引擎仍会做**最大 gap/块大小**的折中。

> 想看"坏点隔离"（一个坏点不拖垮整块）→ [`BadPointIsolation`](../BadPointIsolation)。
> 想看"变化上报/死区"→ [`Events`](../Events)。

## 下一步（按现场问题选）

- 想看**值变化才回调** → [`Events`](../Events)
- 想看**Modbus 地址怎么写** → [`Modbus`](../Modbus)
- 想看**数组批量读写** → [`ArrayIO`](../ArrayIO)
- 想看**字符串/中文编码** → [`StringIO`](../StringIO)
- 想看**坏点隔离** → [`BadPointIsolation`](../BadPointIsolation)

> 以上都跑过之后，进入多设备：[`MultiDevice`](../MultiDevice) →
> 交付/运维：[`Advanced`](../Advanced)。
