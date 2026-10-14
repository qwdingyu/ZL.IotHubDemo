// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotHub 最小采集示例（可直接拷进你自己的项目）
//
//  它做的事：读几个点位 → 写一个已知值 → 读回来比对。
//  最后那步「写入 = 读回」就是采集程序交付前最便宜的自检：
//  地址/类型/端序任一处写错，这里立刻会暴露，而不是等到了现场。
//
//  准备：dotnet add package ZL.IotHub
//  运行：另开窗口起一台 S7 仿真器（没有真机也能练）
//        plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
//        然后：dotnet run
// ═══════════════════════════════════════════════════════════════════════

using ZL.IotHub;
using ZL.IotHub.Core;
using ZL.Tag;

// 连接参数：命令行可覆盖（dotnet run -- --ip 192.168.1.10 --port 102）
var host = GetArg(args, "--ip", "127.0.0.1");
var port = int.TryParse(GetArg(args, "--port", "31002"), out var parsedPort) ? parsedPort : 31002;
const string DeviceId = "MY-DEVICE";

// ── 1. 点表：把这里换成你自己的地址 ──────────────────────────────────────
//    S7 地址写法：DB1.DBX0.0(位) / DB1.DBW2(字) / DB1.DBD4(双字·float)
var tags = new List<TagItem>
{
    new() { Id = "RUN",   Address = "DB1.DBX0.0", DataTypeCode = "bool",  TagType = "M", StationNo = DeviceId },
    new() { Id = "COUNT", Address = "DB1.DBW2",   DataTypeCode = "short", TagType = "M", StationNo = DeviceId },
    new() { Id = "TEMP",  Address = "DB1.DBD4",   DataTypeCode = "float", TagType = "M", StationNo = DeviceId },
};

var cfg = new DeviceConfig();
cfg.Add("Protocol", "siemens-s7");   // 走自研原生驱动
cfg.Add("DeviceId", DeviceId);
cfg.Add("DeviceIp", host);
cfg.Add("Port", port);
cfg.Add("Rack", 0);
cfg.Add("Slot", 1);
cfg.Add("Tags", tags);

// ── 2. 创建驱动并连接 ────────────────────────────────────────────────────
using var driver = DriverFactory.Create(cfg);
if (!driver.Connect())
{
    Console.WriteLine($"连接失败 {host}:{port}。忘了起仿真器？");
    Console.WriteLine($"  plcsim --protocols siemens-s7 --ports s7={port} --headless --auto-start");
    return 1;
}
Console.WriteLine($"已连接（{driver.GetType().Name}）");

// ── 3. 读 ────────────────────────────────────────────────────────────────
Console.WriteLine($"RUN={driver.Read<bool>("RUN")}  COUNT={driver.Read<short>("COUNT")}  TEMP={driver.Read<float>("TEMP")}");

// ── 4. 写 → 读回 → 比对（自检）──────────────────────────────────────────
const short expected = 12345;
var w = driver.Write("COUNT", expected);
if (!w.IsSuccess) { Console.WriteLine("写入失败：" + w.Message); return 1; }

var actual = driver.Read<short>("COUNT");
if (actual != expected)
{
    Console.WriteLine($"❌ 不一致：写 {expected}，读 {actual}。检查地址 / 数据类型 / 端序。");
    return 1;
}
Console.WriteLine($"✅ 写入=读回（{actual}），地址与类型正确。");
return 0;

// 取命令行参数：--name value（找不到就用默认值）
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
