// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotEngine 示例：字符串读写（**最容易踩坑的一节**）
//
//  它演示 4 个真实坑（全部实测过）：
//   ① `Encoding.Default` **跨 TFM 不一致** —— .NET 8 是 UTF-8、net48 是系统 ANSI
//      ⇒ 同一句中文写出**不同字节** ⇒ 必须在 PLC 侧统一编码
//   ② `Encoding.GetEncoding(936)` 在 .NET 8 **默认不可用** —— 宿主需注册代码页提供程序
//      ⇒ 本示例改用 UTF-8（跨 TFM 确定）；GB2312 留作现场按需注册
//   ③ `maxLength` **不能传 -1** —— S7 会读 PLC 头字段的真实容量，容量不足会明确报错
//   ④ `ReadString` 返回值**含 S7 的 2 字节头** —— 回读校验必须跳过前 2 字节
//
//  准备：dotnet add package ZL.IotEngine
//        dotnet add package ZL.IotDriver
//  运行：另开窗口起仿真器
//        plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
//        然后：dotnet run --project samples/StringIO/StringIO.csproj -f net8.0
//
//  🔴 设备 IP/端口/字符串地址/容量在 stringio.xml 里：改文件不用重新编译。
// ═══════════════════════════════════════════════════════════════════════

using System.Text;
using System.Xml.Linq;
using ZL.IotAbstraction.Tag;
using ZL.IotAbstraction.Abstractions;
using ZL.IotEngine.Orchestration;

// ── 1. 从 XML 读设备与点表（TagKit 是生产环境同一套解析器）──────────────
var configPath = ResolveConfigPath(args, "stringio.xml");
if (!File.Exists(configPath))
{
    Console.WriteLine($"找不到配置文件：{configPath}");
    Console.WriteLine("默认读同目录的 stringio.xml；也可以：dotnet run -f net8.0 -- --config 我的设备.xml");
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
if (!tags.TryGetValue("TEXT", out var textTag) || textTag.Length <= 0)
{
    Console.WriteLine($"点表里没有可用的 TEXT 字符串点位：{configPath}");
    Console.WriteLine($"{tagDiag.Outcome}：{tagDiag.Message}");
    return 1;
}

var deviceId = cfg.Get<string>("DeviceId", "STR-DEVICE");
foreach (var tag in tags.Values)
{
    if (string.IsNullOrWhiteSpace(tag.StationNo)) tag.StationNo = deviceId;
}
cfg.Add("Tags", tags);

var host = cfg.Get<string>("DeviceIp", "127.0.0.1");
var port = cfg.Get<int>("Port", 31002);
var address = textTag.Address;
var capacity = textTag.Length;   // S7 STRING 字段容量

using var driver = DriverFactory.Create(cfg);
if (!driver.Connect())
{
    Console.WriteLine($"  ❌ 连接失败 {host}:{port}。忘了起仿真器？");
    Console.WriteLine($"      plcsim --protocols siemens-s7 --ports s7={port} --headless --auto-start");
    return 1;
}
Console.WriteLine($"【1】已连接（{driver.GetType().Name}）  地址={address}，容量={capacity}");
Console.WriteLine();

// ── 坑②：GB2312 在 .NET 8 **不再内置**（需要宿主注册代码页提供程序）──────────
Console.WriteLine("【2】选择编码（坑②：GB2312 不是 BCL 内置，本示例改用 UTF-8）");
var encoding = Encoding.UTF8;   // S7 STRING 的字节按 UTF-8 写；中文 12 字节，容量 20 足够
Console.WriteLine($"      本示例使用 {encoding.WebName}（CodePage={encoding.CodePage}）");
Console.WriteLine($"      ⚠️ `Encoding.Default` 在本运行时是 {Encoding.Default.WebName}"
                + $"（CodePage={Encoding.Default.CodePage}）—— **不要**依赖它 ✗");
Console.WriteLine("      ⚠️ 若 PLC 侧固定 GB2312：宿主需注册代码页提供程序后");
Console.WriteLine("         `Encoding.GetEncoding(936)` 才可用（.NET 8 默认不带 936）");
Console.WriteLine();

// ── 坑①：同一句中文，编码不同 ⇒ 字节数不同 ────────────────────────────────
const string cn = "中文测试";
Console.WriteLine("【3】坑①：同一句中文，编码不同 ⇒ 字节数不同");
Console.WriteLine($"      文本「{cn}」：UTF-8 = {Encoding.UTF8.GetByteCount(cn)} 字节，"
                + $"UTF-16 = {Encoding.Unicode.GetByteCount(cn)} 字节");
Console.WriteLine("      ⇒ 必须与 PLC 侧一致，否则**读写双向乱码** ✗");
Console.WriteLine();

// ── 写入 + 回读（坑③ maxLength、坑④ 跳过 2 字节头）────────────────────────
Console.WriteLine("【4】写入 → 回读 → 比对");
// 🔴 关键 API 事实：`WriteStringAsync` / `ReadString` 定义在 **IPlcDevice** 上 ✓
//   而 `DeviceBase`（driver）只提供按标签读写 ⇒ 字符串要拿底层设备：
//   `driver.Device`（`DeviceRoot.Device` 是 public IPlcDevice 字段 ✓）
IPlcDevice dev = driver.Device;
var write = await dev.WriteStringAsync(address, cn, StringKind.S7String, capacity, encoding)
                       .ConfigureAwait(false);
if (!write.IsSuccess)
{
    Console.WriteLine($"      ❌ 写入失败：{write.Message}");
    Console.WriteLine("      （坑③：若 maxLength 传 -1 或超过字段容量，这里会明确报错 —— 这是**正确保护**）");
    return 1;
}
Console.WriteLine("      ✅ 写入成功");

var read = dev.ReadString(address, (ushort)(capacity + 2), encoding);   // +2 = S7 的 2 字节头
if (!read.IsSuccess)
{
    Console.WriteLine($"      ❌ 回读失败：{read.Message}");
    return 1;
}

// ── 坑④：ReadString 含 S7 头（Byte0=最大长度、Byte1=实际长度）──────────────
var raw = read.Content ?? string.Empty;
var body = raw.Length > 2 ? raw.Substring(2) : raw;      // ← 跳过 2 字节头 ✓
body = body.TrimEnd('\0');
Console.WriteLine($"      原始回读（含头）: [{string.Join(",", raw.Take(6).Select(c => (int)c))}…]");
Console.WriteLine($"      跳过 2 字节头后: 「{body}」");
Console.WriteLine();

// ── 自检 ────────────────────────────────────────────────────────────────
if (body == cn)
{
    Console.WriteLine("══════ 完成：字符串写入 = 回读（UTF-8），地址与编码正确 ══════");
    return 0;
}

Console.WriteLine($"══════ 不一致：期望「{cn}」实得「{body}」 ══════");
Console.WriteLine("  排查方向：① 编码是否与 PLC 侧一致（UTF-8 / GB2312）  ② 地址是否为 STRING 字段");
Console.WriteLine("           ③ 容量是否与 PLC 定义一致  ④ 是否忘了跳过 2 字节头");
return 1;

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
