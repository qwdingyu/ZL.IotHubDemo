// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotHub 示例：Modbus TCP 接入
//
//  和 QuickStart（S7）是同一套用法，只换三样东西：
//      Protocol  → "modbus-tcp"
//      地址写法  → 纯数字偏移量（见下方 ⚠️）
//      多两个参数 → Station（站号）、AddressStartWithZero（地址是否从 0 开始）
//
//  准备（另开一个窗口）：
//      plcsim --protocols modbus-tcp --ports modbus=31502 --headless --auto-start
//  运行：
//      cd samples/Modbus && dotnet run -f net8.0
//
//  🔴 设备 IP/端口/点表在 modbus.xml 里：改文件不用重新编译。
// ═══════════════════════════════════════════════════════════════════════

using System.Xml.Linq;
using ZL.IotEngine.Orchestration;
using ZL.IotAbstraction.Tag;

// ── 1. 从 XML 读设备与点表（TagKit 是生产环境同一套解析器）──────────────
var configPath = ResolveConfigPath(args, "modbus.xml");
if (!File.Exists(configPath))
{
    Console.WriteLine($"找不到配置文件：{configPath}");
    Console.WriteLine("默认读同目录的 modbus.xml；也可以：dotnet run -f net8.0 -- --config 我的设备.xml");
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

var deviceId = cfg.Get<string>("DeviceId", "MODBUS-DEMO");
foreach (var tag in tags.Values)
{
    if (string.IsNullOrWhiteSpace(tag.StationNo)) tag.StationNo = deviceId;
}
cfg.Add("Tags", tags);

var host = cfg.Get<string>("DeviceIp", "127.0.0.1");
var port = cfg.Get<int>("Port", 31502);

Console.WriteLine();
Console.WriteLine("══════ ZL.IotHub 示例：Modbus TCP ══════");

// ⚠️ 地址写法（实测结论，容易踩）：
//    · 地址是**纯数字偏移量**：0 / 1 / 2 …
//    · **不要写 40001 这种 5 位传统编号**——它会被当成"偏移量 40001"，
//      写入看起来成功，但读回来是 0。
//    · **不要写 COIL1 / HR100 这种前缀写法**，原生驱动不识别。
//    · **区域由数据类型决定**：bool ⇒ 线圈；short/int/float ⇒ 保持寄存器。
//      所以下面 COIL_1 与 REG_1 用的是同一个数字地址 1，但互不干扰。

using var driver = DriverFactory.Create(cfg);
if (!driver.Connect())
{
    Console.WriteLine($"  ❌ 连接失败 {host}:{port}。忘了起仿真器？");
    Console.WriteLine($"      plcsim --protocols modbus-tcp --ports modbus={port} --headless --auto-start");
    return 1;
}
Console.WriteLine($"【1】已连接（{driver.GetType().Name}）  点表 {tags.Count} 个点位");
Console.WriteLine();

// ── 2. 写 → 读回（线圈与寄存器各一组）──────────────────────────────────
Console.WriteLine("【2】写入 → 读回");
var okAll = true;

var wc = driver.Write("COIL_1", true);
var rc = driver.Read<bool>("COIL_1");
Console.WriteLine($"      线圈 COIL_1  写 True  → 读 {rc}   {(wc.IsSuccess && rc ? "✅" : "❌")}");
okAll &= wc.IsSuccess && rc;

var wr = driver.Write("REG_1", (short)1234);
var rr = driver.Read<short>("REG_1");
Console.WriteLine($"      寄存器 REG_1 写 1234  → 读 {rr}  {(wr.IsSuccess && rr == 1234 ? "✅" : "❌")}");
okAll &= wr.IsSuccess && rr == 1234;

// ── 3. 证明"同地址不同区域互不干扰"─────────────────────────────────────
Console.WriteLine();
Console.WriteLine("【3】同地址 1 的线圈与寄存器互不干扰（区域由数据类型决定）");
Console.WriteLine($"      线圈  COIL_1 = {driver.Read<bool>("COIL_1")}");
Console.WriteLine($"      寄存器 REG_1  = {driver.Read<short>("REG_1")}");

Console.WriteLine();
Console.WriteLine(okAll ? "══════ 完成：Modbus 接入正常 ══════" : "══════ 有步骤失败 ══════");
Console.WriteLine();
return okAll ? 0 : 1;

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
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(args[i + 1]) ? fallback : args[i + 1];
    return fallback;
}
