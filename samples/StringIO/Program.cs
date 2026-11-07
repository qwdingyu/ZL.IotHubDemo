// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotEngine 示例：字符串读写（**最容易踩坑的一节**）
//
//  它演示 4 个真实坑（全部实测过）：
//   ① `Encoding.Default` **跨 TFM 不一致** —— .NET 8 是 UTF-8、net48 是系统 ANSI
//      ⇒ 同一句中文写出**不同字节** ⇒ 必须在 PLC 侧统一编码
//   ② `Encoding.GetEncoding(936)` **会抛异常** —— 必须先触发引擎的代码页注册
//   ③ `maxLength` **不能传 -1** —— S7 会读 PLC 头字段的真实容量，容量不足会明确报错
//   ④ `ReadString` 返回值**含 S7 的 2 字节头** —— 回读校验必须跳过前 2 字节
//
//  准备：dotnet add package ZL.IotEngine
//        dotnet add package ZL.IotDriver
//  运行：另开窗口起仿真器
//        plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
//        然后：dotnet run --project samples/StringIO/StringIO.csproj
// ═══════════════════════════════════════════════════════════════════════

using System.Text;
using ZL.Tag;
using ZL.IotEngine.Abstractions;
using ZL.IotEngine.Orchestration;

var host = GetArg(args, "--ip", "127.0.0.1");
var port = int.TryParse(GetArg(args, "--port", "31002"), out var p) ? p : 31002;
const string DeviceId = "STR-DEVICE";
const string Address = "DB1.DBB10";   // S7 STRING 字段起始地址
const int Capacity = 20;              // 字段容量（S7 的 STRING[20] 的 20）

var cfg = new DeviceConfig();
cfg.Add("Protocol", "siemens-s7");
cfg.Add("DeviceId", DeviceId);
cfg.Add("DeviceIp", host);
cfg.Add("Port", port);
cfg.Add("Rack", 0);
cfg.Add("Slot", 1);
cfg.Add("Tags", new List<TagItem>
{
    new() { Id = "TEXT", Address = Address, DataTypeCode = "string",
            TagType = "M", StationNo = DeviceId, Length = Capacity,
            AccessMode = AccessMode.ReadWrite },   // ← 要写就必须显式授权
});

using var driver = DriverFactory.Create(cfg);
if (!driver.Connect())
{
    Console.WriteLine($"  ❌ 连接失败 {host}:{port}。忘了起仿真器？");
    Console.WriteLine($"      plcsim --protocols siemens-s7 --ports s7={port} --headless --auto-start");
    return 1;
}
Console.WriteLine($"【1】已连接（{driver.GetType().Name}）");
Console.WriteLine();

// ── 坑②：必须先触发引擎的代码页注册，否则 GetEncoding(936) 抛异常 ──────────
Console.WriteLine("【2】准备编码（坑②：先触发引擎注册，否则 GB2312 不可用）");
_ = PlcStringWriteHelper.ResolveEncoding(null);          // 触发静态构造 ⇒ 注册代码页 ✓
var gb2312 = Encoding.GetEncoding(936);                  // 中文 PLC 现场最常用
Console.WriteLine($"      GB2312 已就绪（CodePage={gb2312.CodePage}）");
Console.WriteLine($"      ⚠️ 注意 Encoding.Default 在本运行时是 {Encoding.Default.WebName}"
                + $"（CodePage={Encoding.Default.CodePage}）—— **不要**依赖它 ✗");
Console.WriteLine();

// ── 坑①：同一句中文，两种编码字节数不同 ────────────────────────────────────
const string cn = "中文测试";
Console.WriteLine("【3】坑①：同一句中文，编码不同 ⇒ 字节数不同");
Console.WriteLine($"      文本「{cn}」：GB2312 = {gb2312.GetByteCount(cn)} 字节，"
                + $"UTF8 = {Encoding.UTF8.GetByteCount(cn)} 字节");
Console.WriteLine("      ⇒ 必须与 PLC 侧一致，否则**读写双向乱码** ✗");
Console.WriteLine();

// ── 写入 + 回读（坑③ maxLength、坑④ 跳过 2 字节头）────────────────────────
Console.WriteLine("【4】写入 → 回读 → 比对");
// 🔴 关键 API 事实：`WriteStringAsync` / `ReadString` 定义在 **IPlcDevice** 上 ✓
//   而 `DeviceBase`（driver）只提供按标签读写 ⇒ 字符串要拿底层设备：
//   `driver.Device`（`DeviceRoot.Device` 是 public IPlcDevice 字段 ✓）
IPlcDevice dev = driver.Device;
var write = await dev.WriteStringAsync(Address, cn, StringKind.S7String, Capacity, gb2312)
                       .ConfigureAwait(false);
if (!write.IsSuccess)
{
    Console.WriteLine($"      ❌ 写入失败：{write.Message}");
    Console.WriteLine("      （坑③：若 maxLength 传 -1 或超过字段容量，这里会明确报错 —— 这是**正确保护**）");
    return 1;
}
Console.WriteLine("      ✅ 写入成功");

var read = dev.ReadString(Address, (ushort)(Capacity + 2), gb2312);   // +2 = S7 的 2 字节头
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
    Console.WriteLine("══════ 完成：字符串写入 = 回读（GB2312），地址与编码正确 ══════");
    return 0;
}

Console.WriteLine($"══════ 不一致：期望「{cn}」实得「{body}」 ══════");
Console.WriteLine("  排查方向：① PLC 侧编码是否为 GB2312  ② 地址是否为 STRING 字段");
Console.WriteLine("           ③ 容量是否与 PLC 定义一致  ④ 是否忘了跳过 2 字节头");
return 1;

static string GetArg(string[] args, string name, string fallback)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(args[i + 1]) ? fallback : args[i + 1];
    return fallback;
}
