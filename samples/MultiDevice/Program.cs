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
//      cd samples/MultiDevice && dotnet run -f net8.0
//
//  🔴 设备与点表在 devices.json + tags.json 里：改配置不用重新编译。
//     fleet.json 只给仿真器用（plcsim --batch）；devices.json 给本程序用。
// ═══════════════════════════════════════════════════════════════════════

using ZL.IotEngine.Orchestration;

Console.WriteLine();
Console.WriteLine("══════ ZL.IotHub 示例：多 PLC 统一管控 ══════");

// ── 1. 从配置文件加载"设备舰队" ─────────────────────────────────────────
//  devices.json 里前 3 台的端口与 fleet.json 一致；第 4 台**故意指向没人监听的端口**。
//  TagFilePath 相对 devices.json 所在目录解析，点表由 TagKit 解析。
var configPath = ResolveConfigPath(args, "devices.json");
if (!File.Exists(configPath))
{
    Console.WriteLine($"找不到配置文件：{configPath}");
    Console.WriteLine("默认读同目录的 devices.json；也可以：dotnet run -f net8.0 -- --config 我的舰队.json");
    return 1;
}

using var orchestrator = new MultiDeviceOrchestrator(configPath);
MultiDeviceOrchestrator.LogCallback = msg => Console.WriteLine($"      [编排] {msg}");

var fleetSize = orchestrator.DeviceCount;
Console.WriteLine($"【1】舰队规模：{fleetSize} 台（来自 {Path.GetFileName(configPath)}；其中 PLC-OFFLINE 故意配错，用于演示健康可见）");
Console.WriteLine();

// ── 2. 编排器统一启动 ──────────────────────────────────────────────────
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
// 等一轮后台采集，让缓存有值（首次连接 + 建计划 + 首轮读需要一点时间）
await Task.Delay(1500);
Console.WriteLine();
Console.WriteLine("【4】逐台读同一个点位（DB1.DBD4）");
foreach (var id in orchestrator.GetDeviceIds())
{
    try
    {
        // 按「设备 + 点位」寻址读取（编排器管的设备走这个入口）。
        // 2.3.0 起同步版返回 DeviceResult<T>（不是裸 T）——必须看 IsSuccess。
        var result = DeviceManager.ReadByDeviceId<float>(id, "TEMP");
        Console.WriteLine(result.IsSuccess
            ? $"      {id,-14} TEMP = {result.Content}"
            : $"      {id,-14} 读取失败：{result.Message}");
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
Console.WriteLine($"  舰队 {fleetSize} 台：启动成功 {started.Count(r => r.IsSuccess)} 台，" +
                  $"启动时健康 {healthyAtStart} 台。");
Console.WriteLine("  注意：配错端口的那台**没有拖垮其他设备**，而且从健康表一眼就能看出是哪台。");
Console.WriteLine();
// 2026-11-07 修复：原实现**永远 return 0** ✗ —— 即使 4 台全失败也返回成功
//   ⇒ CI/脚本无法据此判断失败 ✗
//   语义：**故意配错的那台不算失败**（本示例就是演示"一台坏了不影响其他"）
//   ⇒ 只要有 ≥1 台成功即返回 0 ✓；**全部**失败才返回 1 ✓
var successCount = started.Count(r => r.IsSuccess);
var expectedMin = fleetSize - 1;   // 最后一台是故意配错的
return successCount >= expectedMin ? 0 : 1;

// 未传 --config 时：优先用输出目录里的默认配置，找不到再按当前目录回退。
static string ResolveConfigPath(string[] args, string defaultFileName)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals("--config", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(args[i + 1]);
    }

    var besideExe = Path.Combine(AppContext.BaseDirectory, defaultFileName);
    if (File.Exists(besideExe)) return besideExe;

    return Path.GetFullPath(defaultFileName);
}
