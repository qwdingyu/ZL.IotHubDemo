// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotHub 示例：坏点隔离与恢复
//
//  真实产线上总有一两个点位是坏的（地址写错、设备不支持、线缆问题）。
//  如果坏点每次都拖到超时，整个采集周期都会被它拖慢——
//  本示例演示：坏点被自动隔离，好点照常采。
//
//  准备（另开一个窗口）：
//      plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
//  运行：
//      dotnet run
// ═══════════════════════════════════════════════════════════════════════

using ZL.IotHub;
using ZL.IotHub.Core;
using ZL.Tag;

var host = GetArg(args, "--ip", "127.0.0.1");
var port = int.TryParse(GetArg(args, "--port", "31002"), out var p) ? p : 31002;
const string DeviceId = "ISOLATION-DEMO";

Console.WriteLine();
Console.WriteLine("══════ ZL.IotHub 示例：坏点隔离与恢复 ══════");

// 4 个好点 + 1 个坏点（地址格式非法，无法解析 ⇒ 会被隔离）
var tags = new List<TagItem>
{
    new() { Id = "TEMP",   Address = "DB1.DBD4",  DataTypeCode = "float", TagType = "M", StationNo = DeviceId },
    new() { Id = "COUNT",  Address = "DB1.DBW2",  DataTypeCode = "short", TagType = "M", StationNo = DeviceId },
    new() { Id = "RUN",    Address = "DB1.DBX0.0",DataTypeCode = "bool",  TagType = "M", StationNo = DeviceId },
    new() { Id = "FAULT",  Address = "DB1.DBX0.1",DataTypeCode = "bool",  TagType = "M", StationNo = DeviceId },
    // ↓ 故意写一个坏地址（真实场景里是"地址写错/设备不支持"）
    new() { Id = "BROKEN", Address = "XYZ123",    DataTypeCode = "short", TagType = "M", StationNo = DeviceId },
};

var cfg = new DeviceConfig();
cfg.Add("Protocol", "siemens-s7");
cfg.Add("DeviceId", DeviceId);
cfg.Add("DeviceIp", host);
cfg.Add("Port", port);
cfg.Add("Rack", 0);
cfg.Add("Slot", 1);
cfg.Add("ReadInterval", 300);   // 后台每 300ms 采一轮，坏点会被反复尝试 → 触发隔离
cfg.Add("Tags", tags);

using var driver = DriverFactory.Create(cfg);
if (!driver.Connect())
{
    Console.WriteLine($"  ❌ 连接失败 {host}:{port}。忘了起仿真器？");
    Console.WriteLine($"      plcsim --protocols siemens-s7 --ports s7={port} --headless --auto-start");
    return 1;
}
Console.WriteLine($"【1】已连接（{driver.GetType().Name}），点表 {tags.Count} 个（其中 1 个是坏点）");
Console.WriteLine("【2】等待后台采集跑几轮（5 秒）…");
await Task.Delay(5000);

// ── 3. 看隔离结果 ─────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("【3】隔离结果");
Console.WriteLine($"      被隔离的点位数: {driver.QuarantineTags.Count}");
foreach (var id in driver.QuarantineTags.Keys)
{
    var t = tags.FirstOrDefault(x => x.Id == id);
    Console.WriteLine($"      🔴 {id}（地址 {t?.Address}）已被隔离，不再拖慢采集");
}

// ── 4. 证明好点不受影响 ────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("【4】好点照常采集（这就是隔离机制的意义）");
Console.WriteLine($"      TEMP  = {driver.Read<float>("TEMP")}");
Console.WriteLine($"      COUNT = {driver.Read<short>("COUNT")}");
Console.WriteLine($"      RUN   = {driver.Read<bool>("RUN")}");

var isolated = driver.QuarantineTags.Count > 0;
var goodOk = true;   // 上面三个读数能打印出来就说明好点没被拖垮

Console.WriteLine();
Console.WriteLine(isolated && goodOk
    ? "══════ 完成：坏点被隔离，好点照常采 ══════"
    : "══════ 注意：本次未观察到隔离（可能坏地址未被判定为非法）══════");
Console.WriteLine();
return 0;

static string GetArg(string[] args, string name, string fallback)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(args[i + 1]) ? fallback : args[i + 1];
    return fallback;
}
