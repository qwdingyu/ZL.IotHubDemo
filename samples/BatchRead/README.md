# BatchRead · 地址合并批量读取

**一句话：300 个点位，我们只发 6 次请求。**

采集周期压不下来，往往是因为**逐个点位读**。ZL.IotHub 会按地址间隙自动把点位
合并成少量读取块——这个示例把合并前后的对比直接打给你看。

## 跑

```bash
dotnet tool install -g PlcSimulator.Cli     # 一次性
plcsim --protocols siemens-s7 --ports s7=31002 --headless --stay-alive --auto-start   # 另开一个窗口
cd samples/BatchRead && dotnet run -f net8.0
```

## 你会看到

```
  设备 127.0.0.1:31002  点位数 300（内置生成）

【3】采集计划（核心）
      点位总数    : 300
      合并后块数  : 6
      请求数减少  : 98.00%

【4】写入 3 个已知值 → 实时读回验证
      TAG_0000 (DB1.DBD0) 写 1.5 → 实时读回 1.5  ✅
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
