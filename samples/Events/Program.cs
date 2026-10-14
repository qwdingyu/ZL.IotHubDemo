// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotHub 示例：变化上报 / 事件订阅
//
//  采集程序最常见的浪费是「值没变也在上报」。本示例演示：
//  设备值一变，框架就回调你——不用自己轮询比对。
//
//  准备（另开一个窗口）：
//      plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
//  运行（默认 12 秒后自动退出）：
//      dotnet run
// ═══════════════════════════════════════════════════════════════════════

using ZL.IotHub;
using ZL.IotHub.Core;
using ZL.IotHub.Notifications;
using ZL.Tag;

var host = GetArg(args, "--ip", "127.0.0.1");
var port = int.TryParse(GetArg(args, "--port", "31002"), out var p) ? p : 31002;
var seconds = int.TryParse(GetArg(args, "--seconds", "12"), out var s) ? s : 12;
const string DeviceId = "EVENT-DEMO";

Console.WriteLine();
Console.WriteLine("══════ ZL.IotHub 示例：变化上报 / 事件订阅 ══════");

// ── 1. 订阅事件（连接状态 + 值变化）──────────────────────────────────────
var changeCount = 0;
using var subscription = PlcNotificationHub.Instance.Subscribe(evt =>
{
    if (!string.Equals(evt.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase)) return;

    if (evt.Kind == PlcRealtimeEventKind.Connection)
    {
        if (evt.Connected == true) Console.WriteLine("      [连接] 设备已上线");
        return;
    }

    if (evt.Kind != PlcRealtimeEventKind.TagChanges) return;
    var changes = evt.Changes;
    if (changes == null || changes.Count == 0) return;
    foreach (var c in changes)
    {
        changeCount++;
        Console.WriteLine($"      [变化] {c.TagId} = {c.Value}  (质量={c.Quality})");
    }
});
Console.WriteLine("【1】已订阅事件（连接状态 + 值变化）");

// ── 2. 配置设备并连接 ────────────────────────────────────────────────────
var tags = new List<TagItem>
{
    new() { Id = "COUNT", Address = "DB1.DBW2", DataTypeCode = "short", TagType = "M", StationNo = DeviceId },
    new() { Id = "TEMP",  Address = "DB1.DBD4", DataTypeCode = "float", TagType = "M", StationNo = DeviceId },
};

var cfg = new DeviceConfig();
cfg.Add("Protocol", "siemens-s7");
cfg.Add("DeviceId", DeviceId);
cfg.Add("DeviceIp", host);
cfg.Add("Port", port);
cfg.Add("Rack", 0);
cfg.Add("Slot", 1);
cfg.Add("ReadInterval", 500);        // 后台每 500ms 采一轮，值变了就回调
cfg.Add("Tags", tags);

using var driver = DriverFactory.Create(cfg);
if (!driver.Connect())
{
    Console.WriteLine();
    Console.WriteLine($"  ❌ 连接失败 {host}:{port}。忘了起仿真器？");
    Console.WriteLine($"      plcsim --protocols siemens-s7 --ports s7={port} --headless --auto-start");
    Console.WriteLine();
    return 1;
}
Console.WriteLine("【2】已连接，开始观察事件（下面会主动改值来触发变化）");
Console.WriteLine();

// ── 3. 每隔 2 秒改一次值，观察回调 ──────────────────────────────────────
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
var round = 0;
try
{
    while (!cts.IsCancellationRequested)
    {
        round++;
        var v = (short)(1000 + round * 111);
        driver.Write("COUNT", v);
        await Task.Delay(2000, cts.Token);
    }
}
catch (OperationCanceledException) { }

Console.WriteLine();
Console.WriteLine("══════ 完成 ══════");
Console.WriteLine($"  共收到 {changeCount} 次值变化回调。");
Console.WriteLine("  注意：只有值真的变了才会回调——这就是「变化上报」省掉的无效流量。");
Console.WriteLine();
return 0;

static string GetArg(string[] args, string name, string fallback)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(args[i + 1]) ? fallback : args[i + 1];
        }
    }
    return fallback;
}
