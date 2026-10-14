# Events · 变化上报 / 事件订阅

**一句话：值变了才回调你，不用自己轮询比对。**

采集程序最常见的浪费是「值没变也在上报」。订阅一次事件，框架替你把变化挑出来。

## 跑

```bash
dotnet tool install -g PlcSimulator.Cli     # 一次性
plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start   # 另开一个窗口
cd samples/Events && dotnet run
```

（默认运行 12 秒后自动退出；`--seconds 30` 可延长。）

## 你会看到

```
【1】已订阅事件（连接状态 + 值变化）
      [连接] 设备已上线
      [变化] COUNT = 1111  (质量=Good)
      [变化] COUNT = 1222  (质量=Good)

══════ 完成 ══════
  共收到 6 次值变化回调。
```

示例会**主动改值**来触发回调（真实场景里是设备自己变）。

## 关键代码

```csharp
using var subscription = PlcNotificationHub.Instance.Subscribe(evt =>
{
    if (evt.Kind == PlcRealtimeEventKind.TagChanges)
        foreach (var c in evt.Changes) Console.WriteLine($"{c.TagId} = {c.Value}");
});
```

配 `cfg.Add("ReadInterval", 500)` 后，后台每 500ms 采一轮，**值变了才回调**。
