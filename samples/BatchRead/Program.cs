// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotHub 示例：地址合并批量读取
//
//  采集 300 个点位，逐个读就是 300 次请求——采集周期根本压不下来。
//  本示例展示 ZL.IotHub 怎么把它们合并成少量读取块，并打印合并前后的对比。
//
//  准备（另开一个窗口）：
//      plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
//  运行：
//      cd samples/BatchRead && dotnet run -f net8.0
//
//  🔴 设备 IP/端口在 batchread.xml 里；点表可由该文件的 <TagItem> 提供，
//     不写则默认生成 300 个连续 float。改配置不用重新编译。
// ═══════════════════════════════════════════════════════════════════════

using System.Xml.Linq;
using ZL.IotEngine.Orchestration;
using ZL.IotAbstraction.Tag;

var requestedTagCount = int.TryParse(GetArg(args, "--tags", "300"), out var n) ? n : 300;

// ── 1. 读设备配置（IP / 端口）──────────────────────────────────────────
var configPath = ResolveConfigPath(args, "batchread.xml");
if (!File.Exists(configPath))
{
    Console.WriteLine($"找不到配置文件：{configPath}");
    Console.WriteLine("默认读同目录的 batchread.xml；也可以：dotnet run -f net8.0 -- --config 我的设备.xml");
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

var deviceId = cfg.Get<string>("DeviceId", "BATCH-DEMO");
var host = cfg.Get<string>("DeviceIp", "127.0.0.1");
var port = cfg.Get<int>("Port", 31002);

// ── 2. 点表：配置文件里有 <TagItem> 就用它，否则生成连续地址做演示 ──────
//   TagKit 是生产环境同一套解析器（与 DeviceRoot 自动加载用的是同一个入口）。
var configuredTags = TagKit.LoadTags(configPath, out var tagDiag);
List<TagItem> tags;
var fromConfig = configuredTags.Count > 0;

if (fromConfig)
{
    tags = configuredTags.Values
        .OrderBy(t => t.Address, StringComparer.Ordinal)
        .ThenBy(t => t.Id, StringComparer.Ordinal)
        .ToList();
}
else if (root.Elements("TagItem").Any())
{
    Console.WriteLine($"点表没有加载出任何点位：{configPath}");
    Console.WriteLine($"{tagDiag.Outcome}：{tagDiag.Message}");
    return 1;
}
else
{
    tags = new List<TagItem>();
    for (var i = 0; i < requestedTagCount; i++)
    {
        tags.Add(new TagItem
        {
            Id = $"TAG_{i:D4}",
            Address = $"DB1.DBD{i * 4}",
            DataTypeCode = "float",
            TagType = "M",
            StationNo = deviceId,
            // 要写必须显式授权：默认 ReadOnly（fail-safe）
            AccessMode = AccessMode.ReadWrite,
        });
    }
}

foreach (var tag in tags)
{
    if (string.IsNullOrWhiteSpace(tag.StationNo)) tag.StationNo = deviceId;
}
cfg.Add("Tags", tags);

Console.WriteLine();
Console.WriteLine("══════ ZL.IotHub 示例：地址合并批量读取 ══════");
Console.WriteLine($"  设备 {host}:{port}  点位数 {tags.Count}（{(fromConfig ? "来自配置文件" : "内置生成")}）");
Console.WriteLine();

// ── 3. 创建驱动并连接 ────────────────────────────────────────────────────
using var driver = DriverFactory.Create(cfg);
if (!driver.Connect())
{
    Console.WriteLine($"  ❌ 连接失败 {host}:{port}。忘了起仿真器？");
    Console.WriteLine($"      plcsim --protocols siemens-s7 --ports s7={port} --headless --auto-start");
    Console.WriteLine($"      （端口要一致：改 batchread.xml 里的 Port，或改仿真器 --ports）");
    Console.WriteLine();
    return 1;
}
Console.WriteLine($"【2】已连接（{driver.GetType().Name}）");

// ── 4. 看采集计划：点位被合并成了几个读取块 ──────────────────────────────
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

// ── 5. 写几个已知值再读回（顺便证明"写"也能用同一条链路）──────────────
var writableFloats = tags
    .Where(t => t.AccessMode == AccessMode.ReadWrite && IsFloat(t.DataTypeCode))
    .Take(3)
    .ToList();

Console.WriteLine();
Console.WriteLine("【4】写入 3 个已知值 → 读回验证");
if (writableFloats.Count == 0)
{
    Console.WriteLine("      点表里没有可写的 float 点位，跳过写读验证（规划结果仍然有效）。");
}
else
{
    var probe = new[] { 1.5f, 12.5f, 100.25f };
    for (var i = 0; i < writableFloats.Count; i++)
    {
        var tag = writableFloats[i];
        var w = driver.Write(tag.Id, probe[i]);
        // 读设备实时值：缓存读在写完瞬间可能还没刷新
        var backResult = driver.Device.ReadFloat(tag.Address);
        var back = backResult.Content;
        var ok = w.IsSuccess && backResult.IsSuccess && Math.Abs(back - probe[i]) < 1e-6;
        Console.WriteLine($"      {tag.Id} ({tag.Address}) 写 {probe[i]} → 实时读回 {back}  {(ok ? "✅" : "❌")}");
    }
}

Console.WriteLine();
Console.WriteLine("══════ 完成 ══════");
Console.WriteLine($"  {tags.Count} 个点位 → {blocks} 个读取块，这就是采集周期能压下来的原因。");
Console.WriteLine();
return 0;

static bool IsFloat(string dataTypeCode)
{
    return !string.IsNullOrWhiteSpace(dataTypeCode)
        && (dataTypeCode.Equals("float", StringComparison.OrdinalIgnoreCase)
            || dataTypeCode.Equals("real", StringComparison.OrdinalIgnoreCase)
            || dataTypeCode.Equals("float32", StringComparison.OrdinalIgnoreCase)
            || dataTypeCode.Equals("single", StringComparison.OrdinalIgnoreCase));
}

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
