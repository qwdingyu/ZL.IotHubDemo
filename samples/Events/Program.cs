// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotHub 示例：变化上报 / 事件订阅
//
//  采集程序最常见的浪费是「值没变也在上报」。本示例演示：
//  设备值一变，框架就回调你——不用自己轮询比对。
//
//  准备（另开一个窗口）：
//      plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
//  运行（默认 12 秒后自动退出）：
//      cd samples/Events && dotnet run -f net8.0
//
//  🔴 设备 IP/端口/点表在 events.xml 里：改文件不用重新编译。
// ═══════════════════════════════════════════════════════════════════════

using System.Xml.Linq;
using ZL.IotEngine.Orchestration;
using ZL.IotEngine.Notifications;
using ZL.IotAbstraction.Tag;

var seconds = int.TryParse(GetArg(args, "--seconds", "12"), out var s) ? s : 12;

// ── 1. 从 XML 读设备与点表（TagKit 是生产环境同一套解析器）──────────────
var configPath = ResolveConfigPath(args, "events.xml");
if (!File.Exists(configPath))
{
    Console.WriteLine($"找不到配置文件：{configPath}");
    Console.WriteLine("默认读同目录的 events.xml；也可以：dotnet run -f net8.0 -- --config 我的设备.xml");
    return 1;
}

var root = XElement.Load(configPath);
if (root.Name != "Device")
{
    Console.WriteLine($"配置文件根节点应为 <Device>，实际是 <{root.Name}>：{configPath}");
    return 1;
}

var cfg = new DeviceConfig();
foreach (var attr in root.Attributes())
{
    cfg.Add(attr.Name.LocalName, attr.Value);
}

var tags = TagKit.LoadTags(configPath, out var tagDiag);
if (tags.Count == 0)
{
    Console.WriteLine($"点表没有加载出任何点位：{configPath}");
    Console.WriteLine($"{tagDiag.Outcome}：{tagDiag.Message}");
    return 1;
}

var deviceId = cfg.Get<string>("DeviceId", "EVENT-DEMO");
foreach (var tag in tags.Values)
{
    if (string.IsNullOrWhiteSpace(tag.StationNo)) tag.StationNo = deviceId;
}
cfg.Add("Tags", tags);

var host = cfg.Get<string>("DeviceIp", "127.0.0.1");
var port = cfg.Get<int>("Port", 31002);

Console.WriteLine();
Console.WriteLine("══════ ZL.IotHub 示例：变化上报 / 事件订阅 ══════");

// ── 2. 订阅事件（连接状态 + 值变化）──────────────────────────────────────
// ⚠️ 订阅者守则：**回调里不要抛异常**——中枢会把它隔离并计入
//    PlcNotificationHubMetrics.CallbackErrorCount（不影响其它订阅者/采集），
//    但你的业务逻辑就"静默没执行"了 ✗ ⇒ 在回调内部自行 try/catch 处理业务异常 ✓
var changeCount = 0;
using var subscription = PlcNotificationHub.Instance.Subscribe(evt =>
{
    if (!string.Equals(evt.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)) return;

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

// ── 3. 配置设备并连接 ────────────────────────────────────────────────────
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

// ── 4. 每隔 2 秒改一次值，观察回调 ──────────────────────────────────────
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

// 未传 --config 时：优先用输出目录里的默认配置，找不到再按当前目录回退。
static string ResolveConfigPath(string[] args, string defaultFileName)
{
    var explicitPath = GetArg(args, "--config", string.Empty);
    if (!string.IsNullOrWhiteSpace(explicitPath)) return Path.GetFullPath(explicitPath);

    var besideExe = Path.Combine(AppContext.BaseDirectory, defaultFileName);
    if (File.Exists(besideExe)) return besideExe;

    return Path.GetFullPath(defaultFileName);
}

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
