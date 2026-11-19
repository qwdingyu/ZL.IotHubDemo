// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotEngine 示例：从"能采集"到"能交付"（高级编排）
//
//  这个示例把引擎里最容易被忽略、但现场最有价值的能力串起来：
//    1. 业务壳 ManagedDeviceBase：驱动/连接/事件订阅由框架管，业务只写回调；
//    2. 可判别读取 ReadResult<T> / TryRead<T>：失败有原因，不是静默返回 0/false；
//    3. 缓存 + 质量码 GetValueResult<T> / GetTagValue：HMI 展示的正确读法；
//    4. 诊断 GetDiagnosticsReport + CaptureSnapshot：交付前/故障时的证据；
//    5. 运行时增删设备 AddDeviceAsync / RemoveDeviceAsync：不重启进程加一台；
//    6. DeviceManager 按「设备 + 点位」寻址读写：上层业务不直接持有驱动。
//
//  准备（另开一个窗口；fleet.json 会起 2 台仿真 PLC）：
//      plcsim --batch fleet.json --headless --auto-start
//  运行：
//      cd samples/Advanced && dotnet run -f net8.0
//
//  🔴 设备/点表在 devices.json + tags.json 里：改配置不用重新编译。
// ═══════════════════════════════════════════════════════════════════════

using System.Xml.Linq;
using ZL.IotEngine.Orchestration;
using ZL.IotAbstraction.Tag;
using ZL.IotAbstraction.Abstractions;

var configPath = ResolveConfigPath(args, "devices.json");
if (!File.Exists(configPath))
{
    Console.WriteLine($"找不到配置文件：{configPath}");
    Console.WriteLine("默认读同目录的 devices.json；也可以：dotnet run -f net8.0 -- --config 我的设备.json");
    return 1;
}

Console.WriteLine();
Console.WriteLine("══════ ZL.IotEngine 示例：高级编排能力 ══════");

// ── 1. 编排器 + 业务壳 ─────────────────────────────────────────────────
using var orchestrator = new MultiDeviceOrchestrator(configPath);
MultiDeviceOrchestrator.LogCallback = msg => Console.WriteLine($"      [编排] {msg}");

Console.WriteLine($"【1】配置里的设备顺序：{string.Join(" → ", orchestrator.GetDeviceIdsInConfigOrder())}");
var started = await orchestrator.StartAsync(CancellationToken.None);
foreach (var r in started)
{
    Console.WriteLine($"      {(r.IsSuccess ? "✅" : "🔴")} {r.DeviceId,-10} {r.Message}");
}

var dev = DeviceManager.GetDeviceRootById("ADV-01") as ZL.IotHub.Sample.Advanced.AdvancedDevice;
if (dev == null)
{
    Console.WriteLine("❌ 没拿到 ADV-01 业务壳（检查 devices.json 的 DeviceType 与程序集是否匹配）");
    return 1;
}

// 等首轮采集，让缓存/回调有值；写入一个已知值让输出可复现（TEMP 在 tags.json 中 ReadWrite）
await Task.Delay(1500);
await DeviceManager.WriteByDeviceIdAsync("ADV-01", "TEMP", 12.5f);
await Task.Delay(300);

// ── 2. 可判别读取：ReadResult<T> / TryRead<T> ───────────────────────────
Console.WriteLine();
Console.WriteLine("【2】可判别读取（失败有原因，不是静默 default）");

var live = dev.ReadResult<float>("TEMP");
Console.WriteLine(live.IsSuccess
    ? $"      ReadResult<float>(TEMP) = {live.Content}  ✅"
    : $"      ReadResult<float>(TEMP) 失败：{live.Kind}/{live.Owner} {live.Message}");

var liveAsync = await dev.ReadResultAsync<float>("TEMP");
Console.WriteLine(liveAsync.IsSuccess
    ? $"      ReadResultAsync<float>(TEMP) = {liveAsync.Content}  ✅"
    : $"      ReadResultAsync<float>(TEMP) 失败：{liveAsync.Message}");

var ok = dev.TryRead<float>("NO_SUCH_TAG", out var missingValue, out var missingError);
Console.WriteLine($"      TryRead<float>(NO_SUCH_TAG) = {ok}，value={missingValue}，error={missingError}");

// 对比：Read<T> 失败返回 default(T)，对 bool 就是 false —— 与"真实 false"无法区分
var silent = dev.Read<float>("NO_SUCH_TAG");
Console.WriteLine($"      对比 Read<float>(NO_SUCH_TAG) = {silent}  ← 静默 default，不要用它做报警判断");

// ── 3. 缓存值 + 质量码（HMI/SCADA 的正确读法）──────────────────────────
Console.WriteLine();
Console.WriteLine("【3】缓存值 + 质量码（展示/巡检用，不产生 PLC IO）");
var cached = dev.GetValueResult<float>("TEMP");
var tagValue = dev.GetTagValue("TEMP");
Console.WriteLine($"      GetValueResult<float>(TEMP) = {(cached.IsSuccess ? cached.Content.ToString() : cached.Message)}");
Console.WriteLine($"      GetTagValue(TEMP): 值={tagValue?.Value}，质量={tagValue?.Quality}，时间={tagValue?.Timestamp:HH:mm:ss}");
var hasCached = dev.TryGetValueResult<float>("TEMP", out var cachedValue, out var cachedError);
Console.WriteLine(hasCached
    ? $"      TryGetValueResult<float>(TEMP) = True，value={cachedValue}"
    : $"      TryGetValueResult<float>(TEMP) = False，error={cachedError}");
Console.WriteLine($"      连接状态：{dev.ConnectionState.StateDescription}，成功率={dev.ConnectionState.SuccessRate:P0}");

// DataCollected：每完成一轮采集触发一次（统计吞吐/判定采集是否真的在跑）
var scanCount = 0;
dev.DataCollected += _ => Interlocked.Increment(ref scanCount);

// ── 4. 快照 + 诊断报告（交付前自检 / 故障取证）──────────────────────────
Console.WriteLine();
Console.WriteLine("【4】快照 + 诊断报告");
var snapshot = dev.CaptureSnapshot();
Console.WriteLine($"      CaptureSnapshot：{snapshot.Count} 个点位");
foreach (var kv in snapshot)
{
    Console.WriteLine($"        {kv.Key,-8} = {kv.Value}");
}

var diag = dev.GetDiagnosticsReport();
Console.WriteLine($"      GetDiagnosticsReport：运行 {diag.TotalRuntime.TotalSeconds:F1}s，{diag.Metrics.Count} 类指标");
foreach (var kv in diag.Metrics)
{
    var m = kv.Value;
    Console.WriteLine($"        {kv.Key,-20} 次数={m.TotalCount,-6} 成功率={m.SuccessRate,-6:P0} 平均={m.AverageDurationMs}ms 最大={m.MaxDurationMs}ms");
}

// Overrun 只给建议，不自动改调度（是否接受由业务/操作员决定）
var overrun = dev.Overrun.TryGetSuggestion(300, 300, out var suggestedScanRate)
    ? $"发现 overrun，建议 ScanRate={suggestedScanRate}ms"
    : "当前无 overrun（采集耗时未超过周期）";
Console.WriteLine($"      OverrunMonitor：{overrun}");

// 标签清单（诊断/对账用）
var tagIds = DeviceManager.GetTagsByDeviceId("ADV-01").Select(t => t.Id);
Console.WriteLine($"      GetTagsByDeviceId：{string.Join("、", tagIds)}");

// ── 5. 业务壳回调（ManagedDeviceBase 自动转发）──────────────────────────
await Task.Delay(700);   // 给 DataCollected 至少一个采集周期
Console.WriteLine();
Console.WriteLine("【5】业务壳回调（驱动事件 → OnManagedTriggerDataChange）");
Console.WriteLine($"      TEMP 回调次数={dev.TempTriggerCount}，最近值={dev.LastTemp}");
Console.WriteLine($"      DataCollected 采集轮次={Volatile.Read(ref scanCount)}（每轮采集完成触发一次）");

// ── 6. DeviceManager 按「设备 + 点位」寻址读写 ──────────────────────────
Console.WriteLine();
Console.WriteLine("【6】DeviceManager 寻址写入 → 读回");
var write = await DeviceManager.WriteByDeviceIdAsync("ADV-01", "COUNT", (short)4321);
Console.WriteLine($"      写入 COUNT=4321：{(write.IsSuccess ? "✅" : "❌ " + write.Message)}");
var readBack = await DeviceManager.ReadByDeviceIdAsync<short>("ADV-01", "COUNT");
Console.WriteLine(readBack.IsSuccess
    ? $"      读回 COUNT={readBack.Content}  {(readBack.Content == 4321 ? "✅" : "❌")}"
    : $"      读回失败：{readBack.Message}");

// ── 7. 运行时新增 / 移除设备（不重启进程）───────────────────────────────
Console.WriteLine();
Console.WriteLine("【7】运行时新增设备 ADV-02（配置来自 runtime-add.xml）→ 读数 → 移除");
var addConfigPath = Path.Combine(Path.GetDirectoryName(configPath) ?? ".", "runtime-add.xml");
if (!File.Exists(addConfigPath))
{
    Console.WriteLine($"      ⚠️ 找不到 {addConfigPath}，跳过运行时新增演示");
}
else
{
    var addConfig = new DeviceConfig();
    foreach (var attr in XElement.Load(addConfigPath).Attributes())
    {
        addConfig.Add(attr.Name.LocalName, attr.Value);
    }

    var added = await orchestrator.AddDeviceAsync(addConfig);
    Console.WriteLine($"      AddDeviceAsync：{(added.IsSuccess ? "✅ " + added.Message : "🔴 " + added.Message)}");
    if (added.IsSuccess)
    {
        await Task.Delay(1000);
        var adv2 = await DeviceManager.ReadByDeviceIdAsync<float>("ADV-02", "TEMP");
        Console.WriteLine(adv2.IsSuccess ? $"      ADV-02 TEMP={adv2.Content}" : $"      ADV-02 读取失败：{adv2.Message}");

        var removed = await orchestrator.RemoveDeviceAsync("ADV-02");
        Console.WriteLine($"      RemoveDeviceAsync：{(removed.IsSuccess ? "✅ " + removed.Message : "🔴 " + removed.Message)}");
    }
}

// ── 8. TryStart：点表加载失败时**启动就拒绝**，而不是"0 个点位照常跑" ──────
Console.WriteLine();
Console.WriteLine("【8】TryStart 失败快速验证（点表缺失 + RequireTags=true）");
var badConfigPath = Path.Combine(Path.GetDirectoryName(configPath) ?? ".", "bad-device.xml");
if (!File.Exists(badConfigPath))
{
    Console.WriteLine($"      ⚠️ 找不到 {badConfigPath}，跳过 TryStart 验证");
}
else
{
    var badConfig = new DeviceConfig();
    foreach (var attr in XElement.Load(badConfigPath).Attributes())
    {
        badConfig.Add(attr.Name.LocalName, attr.Value);
    }
    using (var badDevice = DriverFactory.Create(badConfig))
    {
        var canStart = badDevice.TryStart(out var reason);
        Console.WriteLine($"      TryStart={canStart}（期望 False）");
        Console.WriteLine($"      reason={reason}");
    }
}

// ── 9. 健康状态 + 优雅停止 ──────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("【9】健康状态 + 优雅停止");
Console.WriteLine($"      DeviceManager.GetAllDeviceIds：{string.Join("、", DeviceManager.GetAllDeviceIds())}");
var status = orchestrator.GetStatus("ADV-01");
Console.WriteLine($"      GetStatus(ADV-01)：{status?.StateDescription}，成功率={status?.SuccessRate:P0}");
foreach (var h in orchestrator.GetHealthStatus())
{
    Console.WriteLine($"      {h.DeviceId,-10} Healthy={h.IsHealthy}  {h.StatusText}");
}

var stopped = await orchestrator.StopAsync(CancellationToken.None);
foreach (var r in stopped)
{
    Console.WriteLine($"      {(r.IsSuccess ? "✅" : "🔴")} {r.DeviceId,-10} {r.Message}");
}

Console.WriteLine();
Console.WriteLine("══════ 完成：业务壳 / 可判别读 / 诊断 / 运行时增删 全部跑通 ══════");
Console.WriteLine();
return 0;

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
