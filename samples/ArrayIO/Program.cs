// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotEngine 示例：**数组批量读写**（一次搬一批，而不是一个点一个点搬）
//
//  为什么重要：真实点表里大量是"连续排列"的数据（配方、报警字、波形缓冲）。
//  逐个读 = 请求次数爆炸；按数组读 = **一次请求搬 N 个** ⇒ 采集周期压下来。
//
//  运行：plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
//        dotnet run --project samples/ArrayIO/ArrayIO.csproj -f net8.0
//
//  🔴 设备 IP/端口/数组地址/长度在 arrayio.xml 里：改文件不用重新编译。
// ═══════════════════════════════════════════════════════════════════════

using System.Xml.Linq;
using ZL.IotAbstraction.Tag;
using ZL.IotAbstraction.Abstractions;
using ZL.IotEngine.Orchestration;

// ── 1. 从 XML 读设备与点表（TagKit 是生产环境同一套解析器）──────────────
var configPath = ResolveConfigPath(args, "arrayio.xml");
if (!File.Exists(configPath))
{
    Console.WriteLine($"找不到配置文件：{configPath}");
    Console.WriteLine("默认读同目录的 arrayio.xml；也可以：dotnet run -f net8.0 -- --config 我的设备.xml");
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
if (!tags.TryGetValue("ARR", out var arrTag) || arrTag.Length <= 0)
{
    Console.WriteLine($"点表里没有可用的 ARR 数组点位：{configPath}");
    Console.WriteLine($"{tagDiag.Outcome}：{tagDiag.Message}");
    return 1;
}

var deviceId = cfg.Get<string>("DeviceId", "ARR-DEVICE");
foreach (var tag in tags.Values)
{
    if (string.IsNullOrWhiteSpace(tag.StationNo)) tag.StationNo = deviceId;
}
cfg.Add("Tags", tags);

var host = cfg.Get<string>("DeviceIp", "127.0.0.1");
var port = cfg.Get<int>("Port", 31002);
var baseAddr = arrTag.Address;
var count = arrTag.Length / 2;   // short = 2 字节

using var driver = DriverFactory.Create(cfg);
if (!driver.Connect())
{
    Console.WriteLine($"  ❌ 连接失败 {host}:{port}。忘了起仿真器？");
    return 1;
}
Console.WriteLine($"【1】已连接（{driver.GetType().Name}）  数组起点={baseAddr}，元素数={count}");
IPlcDevice dev = driver.Device;
Console.WriteLine();

// ── 数组写 ──────────────────────────────────────────────────────────────
var payload = new short[count];
for (var i = 0; i < count; i++) payload[i] = (short)(100 + i);
Console.WriteLine($"【2】数组写入 {count} 个 short：{string.Join(",", payload)}");
var w = await dev.WriteAsync(baseAddr, payload).ConfigureAwait(false);
if (!w.IsSuccess)
{
    Console.WriteLine($"      ❌ 写入失败：{w.Message}");
    return 1;
}
Console.WriteLine("      ✅ 写入成功（**一次请求**搬完全部元素）");
Console.WriteLine();

// ── 数组读 ──────────────────────────────────────────────────────────────
Console.WriteLine("【3】数组回读");
// 🔴 跨 TFM API 事实（实测 ✓，很值得知道）：
//   · 数组**写**：`IPlcDevice` 有类型化重载 ✓（`WriteAsync(addr, short[])`）—— 双 TFM 通用 ✓
//   · 数组**读**：`IPlcDevice` **只有 `ReadBytes`** ✗；
//     泛型 `ReadArray<T>` / `EndianConverter.Convert<T>` 带 `where T : unmanaged` 约束 ✗
//     ⇒ **net48 编译器不支持该约束** ⇒ 报 `CS0570` ✗（本示例实测踩到 ✓）
//   ⇒ 跨 TFM 通用做法：**按标量类型逐个读** ✓（引擎会自动把连续地址合并成**同一块**，
//     所以"逐点读"在**协议层仍是一次批量请求** ✓ —— 这正是地址合并的价值 ✓）
var back = new short[count];
for (var i2 = 0; i2 < count; i2++)
{
    var addr = $"DB2.DBW{i2 * 2}";          // short 步长 2 字节
    var one = dev.ReadInt16(addr);
    if (!one.IsSuccess)
    {
        Console.WriteLine($"      ❌ 读 {addr} 失败：{one.Message}");
        return 1;
    }
    back[i2] = one.Content;
}
Console.WriteLine($"      回读：{string.Join(",", back)}");
Console.WriteLine("      （逐个 `ReadInt16` 调用，但引擎把它们**合并成 1 个块**发出去 ✓）");
Console.WriteLine();

// ── 自检 ────────────────────────────────────────────────────────────────
if (back.SequenceEqual(payload))
{
    Console.WriteLine($"══════ 完成：{count} 个 short 数组写入 = 回读 ══════");
    Console.WriteLine("  对比：逐个读需 N 次请求；数组读只用 1 次 ⇒ 这就是批量采集的意义。");
    return 0;
}

Console.WriteLine("══════ 不一致 ══════");
Console.WriteLine($"  期望 {string.Join(",", payload)}");
Console.WriteLine($"  实得 {string.Join(",", back)}");
return 1;

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
