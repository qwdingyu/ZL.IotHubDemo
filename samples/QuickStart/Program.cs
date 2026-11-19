// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotEngine 最小采集示例（可直接拷进你自己的项目）
//
//  它做的事：读几个点位 → 写一个已知值 → 读回来比对。
//  最后那步「写入 = 读回」就是采集程序交付前最便宜的自检：
//  地址/类型/端序任一处写错，这里立刻会暴露，而不是等到了现场。
//
//  准备：dotnet add package ZL.IotEngine
//        dotnet add package ZL.IotDriver   ← 驱动在独立包，必须一起装
//  运行：另开窗口起一台 S7 仿真器（没有真机也能练）
//        plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
//        然后：dotnet run
//
//  🔴 设备 IP / 端口 / 点表都在 quickstart.xml 里：改文件即可，不用重新编译。
//     想用另一份配置：dotnet run -f net8.0 -- --config 我的设备.xml
// ═══════════════════════════════════════════════════════════════════════

using System.Xml.Linq;
using ZL.IotEngine.Orchestration;
using ZL.IotAbstraction.Tag;

// ── 1. 从 XML 读设备与点表（TagKit 是生产环境同一套解析器）──────────────
var configPath = ResolveConfigPath(args, "quickstart.xml");
if (!File.Exists(configPath))
{
    Console.WriteLine($"找不到配置文件：{configPath}");
    Console.WriteLine("默认读同目录的 quickstart.xml；也可以：dotnet run -f net8.0 -- --config 我的设备.xml");
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

var deviceId = cfg.Get<string>("DeviceId", "MY-DEVICE");
foreach (var tag in tags.Values)
{
    if (string.IsNullOrWhiteSpace(tag.StationNo)) tag.StationNo = deviceId;
}
cfg.Add("Tags", tags);

var host = cfg.Get<string>("DeviceIp", "127.0.0.1");
var port = cfg.Get<int>("Port", 31002);

// ── 2. 创建驱动并连接 ────────────────────────────────────────────────────
using var driver = DriverFactory.Create(cfg);
if (!driver.Connect())
{
    Console.WriteLine($"连接失败 {host}:{port}。忘了起仿真器？");
    Console.WriteLine($"  plcsim --protocols siemens-s7 --ports s7={port} --headless --auto-start");
    Console.WriteLine($"  （端口要一致：改 quickstart.xml 里的 Port，或改仿真器 --ports）");
    return 1;
}
Console.WriteLine($"已连接（{driver.GetType().Name}）  点表 {tags.Count} 个点位");

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

// 未传 --config 时：优先用输出目录里的默认配置，找不到再按当前目录回退。
static string ResolveConfigPath(string[] args, string defaultFileName)
{
    var explicitPath = GetArg(args, "--config", string.Empty);
    if (!string.IsNullOrWhiteSpace(explicitPath)) return Path.GetFullPath(explicitPath);

    var besideExe = Path.Combine(AppContext.BaseDirectory, defaultFileName);
    if (File.Exists(besideExe)) return besideExe;

    return Path.GetFullPath(defaultFileName);
}

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
