// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotHub 示例：地址合并批量读取
//
//  采集 300 个点位，逐个读就是 300 次请求——采集周期根本压不下来。
//  本示例展示 ZL.IotHub 怎么把它们合并成少量读取块，并打印合并前后的对比。
//
//  准备（另开一个窗口）：
//      plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
//  运行：
//      dotnet run
// ═══════════════════════════════════════════════════════════════════════

using System.Diagnostics;
using ZL.IotHub;
using ZL.IotHub.Core;
using ZL.Tag;

var host = GetArg(args, "--ip", "127.0.0.1");
var port = int.TryParse(GetArg(args, "--port", "31002"), out var p) ? p : 31002;
var tagCount = int.TryParse(GetArg(args, "--tags", "300"), out var n) ? n : 300;
const string DeviceId = "BATCH-DEMO";

Console.WriteLine();
Console.WriteLine("══════ ZL.IotHub 示例：地址合并批量读取 ══════");
Console.WriteLine($"  设备 {host}:{port}  点位数 {tagCount}");
Console.WriteLine();

// ── 1. 造一份"连续地址"的点表（真实产线常见形态：一段连续的 DB 区）─────
//     S7 里 float 占 4 字节，所以地址按 4 递增：DB1.DBD0 / DB1.DBD4 / ...
var tags = new List<TagItem>();
for (var i = 0; i < tagCount; i++)
{
    tags.Add(new TagItem
    {
        Id = $"TAG_{i:D4}",
        Address = $"DB1.DBD{i * 4}",
        DataTypeCode = "float",
        TagType = "M",
        StationNo = DeviceId,
    });
}
Console.WriteLine($"【1】已生成 {tags.Count} 个点位（DB1.DBD0 起，连续 float）");

// ── 2. 创建驱动并连接 ────────────────────────────────────────────────────
var cfg = new DeviceConfig();
cfg.Add("Protocol", "siemens-s7");
cfg.Add("DeviceId", DeviceId);
cfg.Add("DeviceIp", host);
cfg.Add("Port", port);
cfg.Add("Rack", 0);
cfg.Add("Slot", 1);
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
Console.WriteLine($"【2】已连接（{driver.GetType().Name}）");

// ── 3. 看采集计划：点位被合并成了几个读取块 ──────────────────────────────
driver.BuildPlan();
var blocks = driver.readPlan?.Count ?? 0;
Console.WriteLine();
Console.WriteLine("【3】采集计划（核心）");
Console.WriteLine($"      点位总数    : {tags.Count}");
Console.WriteLine($"      合并后块数  : {blocks}");
if (blocks > 0)
{
    var reduction = (1 - (double)blocks / tags.Count) * 100;
    Console.WriteLine($"      请求数减少  : {reduction:F2}%");
    Console.WriteLine($"      ⇒ 原来要 {tags.Count} 次请求，现在约 {blocks} 次");
}

// ── 4. 写几个已知值再读回（顺便证明"写"也能用同一条链路）──────────────
Console.WriteLine();
Console.WriteLine("【4】写入 3 个已知值 → 读回验证");
var probe = new[] { 1.5f, 12.5f, 100.25f };
for (var i = 0; i < probe.Length; i++)
{
    var w = driver.Write(tags[i].Id, probe[i]);
    var back = driver.Read<float>(tags[i].Id);
    var ok = w.IsSuccess && Math.Abs(back - probe[i]) < 1e-6;
    Console.WriteLine($"      {tags[i].Id} ({tags[i].Address}) 写 {probe[i]} → 读回 {back}  {(ok ? "✅" : "❌")}");
}

Console.WriteLine();
Console.WriteLine("══════ 完成 ══════");
Console.WriteLine($"  {tags.Count} 个点位 → {blocks} 个读取块，这就是采集周期能压下来的原因。");
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
