# BatchRead · 地址合并批量读取

**一句话：300 个点位，我们只发 6 次请求。**

采集周期压不下来，往往是因为**逐个点位读**。ZL.IotHub 会按地址间隙自动把点位
合并成少量读取块——这个示例把合并前后的对比直接打给你看。

## 跑

```bash
dotnet tool install -g PlcSimulator.Cli     # 一次性
plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start   # 另开一个窗口
cd samples/BatchRead && dotnet run
```

## 你会看到

```
【3】采集计划（核心）
      点位总数    : 300
      合并后块数  : 6
      请求数减少  : 98.00%

【4】写入 3 个已知值 → 读回验证
      TAG_0000 (DB1.DBD0) 写 1.5 → 读回 1.5  ✅
```

## 换成你自己的点表

改 `tags` 的地址即可（S7：`DB1.DBD0` 起，float 每 4 字节一个）：

```csharp
Address = $"DB1.DBD{i * 4}",
DataTypeCode = "float",
```

点位数也可以调：

```bash
dotnet run -- --tags 1000
```
