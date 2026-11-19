// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotDriver 单独使用 —— 最小快速入门
//
//  只引用 ZL.IotDriver，不引用 ZL.IotEngine。
//  适合：已有自己的调度/存储/服务框架，只想“拿到驱动直接读写 PLC”。
//
//  运行（先起对应仿真器）：
//      dotnet run --project samples/DriverOnly -f net8.0 -- --protocol s7
//      dotnet run --project samples/DriverOnly -f net8.0 -- --protocol modbus
//      dotnet run --project samples/DriverOnly -f net8.0 -- --protocol omron
//      dotnet run --project samples/DriverOnly -f net8.0 -- --protocol melsec
//
//  ⚠️ 只装 Driver 时：
//    · 没有 DriverFactory / DeviceManager / 自动重连 / 地址合并 / 缓存；
//    · 连接、重连、采集循环、异常处理由你的程序自己负责；
//    · 需要这些能力时再加 ZL.IotEngine。
// ═══════════════════════════════════════════════════════════════════════

Console.WriteLine("══════ ZL.IotDriver 单独使用 · 最小快速入门 ══════");
Console.WriteLine("  只引用 ZL.IotDriver（不引用 ZL.IotEngine）");
Console.WriteLine();

var protocol = GetArg(args, "--protocol", "s7").ToLowerInvariant();
var host = GetArg(args, "--ip", "127.0.0.1");

return protocol switch
{
    "s7" or "siemens" => S7Example.Run(host, ParsePort(args, 31002)),
    "modbus" or "modbus-tcp" => ModbusExample.Run(host, ParsePort(args, 31502)),
    "omron" or "fins" or "fins-tcp" => OmronExample.Run(host, ParsePort(args, 9600)),
    "melsec" or "mitsubishi" or "mc" => MelsecExample.Run(host, ParsePort(args, 32000)),
    _ => Help()
};

static int Help()
{
    Console.WriteLine("用法:");
    Console.WriteLine("  dotnet run --project samples/DriverOnly -f net8.0 -- --protocol s7");
    Console.WriteLine("  dotnet run --project samples/DriverOnly -f net8.0 -- --protocol modbus");
    Console.WriteLine("  dotnet run --project samples/DriverOnly -f net8.0 -- --protocol omron");
    Console.WriteLine("  dotnet run --project samples/DriverOnly -f net8.0 -- --protocol melsec");
    Console.WriteLine();
    Console.WriteLine("可选: --ip 127.0.0.1  --port <端口>");
    return 1;
}

static int ParsePort(string[] args, int fallback)
    => int.TryParse(GetArg(args, "--port", string.Empty), out var p) ? p : fallback;

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
