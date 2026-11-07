// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotHub 示例：多 PLC 统一管控
//
//  一台 PLC 谁都会连；**几十上百台怎么管**才是真问题：
//  统一启动、逐台健康可见、一台坏了不拖垮其余的、统一停止。
//  本示例用编排器（MultiDeviceOrchestrator）管理 4 台设备——
//  其中**故意有 1 台配错端口**，用来演示"健康可见"。
//
//  准备（另开一个窗口；fleet.json 会起 3 台仿真 PLC）：
//      plcsim --batch fleet.json --headless --auto-start
//  运行：
//      dotnet run
// ═══════════════════════════════════════════════════════════════════════

using ZL.IotEngine.Orchestration;
using ZL.Tag;

Console.WriteLine();
Console.WriteLine("══════ ZL.IotHub 示例：多 PLC 统一管控 ══════");

// ── 1. 描述这支"设备舰队" ──────────────────────────────────────────────
//  前 3 台的端口与 fleet.json 一致；第 4 台**故意指向没人监听的端口**。
var fleet = new List<DeviceConfig>
{
    Build("PLC-01", 31101),
    Build("PLC-02", 31102),
    Build("PLC-03", 31103),
    Build("PLC-OFFLINE", 31199),   // ← 故意配错：演示"一台坏了不影响其他"
};

Console.WriteLine($"【1】舰队规模：{fleet.Count} 台（其中 PLC-OFFLINE 故意配错，用于演示健康可见）");
Console.WriteLine();

// ── 2. 编排器统一启动 ──────────────────────────────────────────────────
using var orchestrator = new MultiDeviceOrchestrator(fleet);
MultiDeviceOrchestrator.LogCallback = msg => Console.WriteLine($"      [编排] {msg}");

Console.WriteLine("【2】统一启动（StartAsync）");
var started = await orchestrator.StartAsync(CancellationToken.None);
foreach (var r in started)
{
    Console.WriteLine($"      {(r.IsSuccess ? "✅" : "🔴")} {r.DeviceId,-12} {r.Message}");
}

// ── 3. 舰队健康状态（一眼看出哪台有问题）────────────────────────────────
Console.WriteLine();
Console.WriteLine("【3】舰队健康状态（GetHealthStatus）");
Console.WriteLine($"      {"设备",-14}{"健康",-8}状态");
foreach (var h in orchestrator.GetHealthStatus())
{
    Console.WriteLine($"      {h.DeviceId,-14}{(h.IsHealthy ? "✅" : "🔴"),-8}{h.StatusText}");
}

// ── 4. 逐台读数（坏的那台读不到，但**不影响**其他）───────────────────────
Console.WriteLine();
Console.WriteLine("【4】逐台读同一个点位（DB1.DBD4）");
foreach (var id in orchestrator.GetDeviceIds())
{
    try
    {
        // 按「设备 + 点位」寻址读取（编排器管的设备走这个入口）
        var v = DeviceManager.ReadByDeviceId<float>(id, "TEMP");
        Console.WriteLine($"      {id,-14} TEMP = {v}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"      {id,-14} 读取失败：{ex.Message}");
    }
}

// ── 5. 统一停止 ────────────────────────────────────────────────────────
Console.WriteLine();
var healthyAtStart = orchestrator.GetHealthStatus().Count(h => h.IsHealthy);

Console.WriteLine("【5】统一停止（StopAsync）");
var stopped = await orchestrator.StopAsync(CancellationToken.None);
foreach (var r in stopped)
{
    Console.WriteLine($"      {(r.IsSuccess ? "✅" : "🔴")} {r.DeviceId,-12} {r.Message}");
}

Console.WriteLine();
Console.WriteLine("══════ 完成 ══════");
Console.WriteLine($"  舰队 {fleet.Count} 台：启动成功 {started.Count(r => r.IsSuccess)} 台，" +
                  $"启动时健康 {healthyAtStart} 台。");
Console.WriteLine("  注意：配错端口的那台**没有拖垮其他设备**，而且从健康表一眼就能看出是哪台。");
Console.WriteLine();
return 0;

static DeviceConfig Build(string deviceId, int port)
{
    var cfg = new DeviceConfig();
    cfg.Add("Protocol", "siemens-s7");
    cfg.Add("DeviceId", deviceId);
    cfg.Add("DeviceIp", "127.0.0.1");
    cfg.Add("Port", port);
    cfg.Add("Rack", 0);
    cfg.Add("Slot", 1);
    cfg.Add("ReadInterval", 1000);
    cfg.Add("Tags", new List<TagItem>
    {
        new() { Id = "TEMP",  Address = "DB1.DBD4", DataTypeCode = "float", TagType = "M", StationNo = deviceId },
        new() { Id = "COUNT", Address = "DB1.DBW2", DataTypeCode = "short", TagType = "M", StationNo = deviceId },
    });
    return cfg;
}
