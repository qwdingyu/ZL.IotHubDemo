// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotEngine 示例：**数组批量读写**（一次搬一批，而不是一个点一个点搬）
//
//  为什么重要：真实点表里大量是"连续排列"的数据（配方、报警字、波形缓冲）。
//  逐个读 = 请求次数爆炸；按数组读 = **一次请求搬 N 个** ⇒ 采集周期压下来。
//
//  运行：plcsim --protocols siemens-s7 --ports s7=31002 --headless --auto-start
//        dotnet run --project samples/ArrayIO/ArrayIO.csproj
// ═══════════════════════════════════════════════════════════════════════

using ZL.Tag;
using ZL.IotEngine.Abstractions;
using ZL.IotEngine.Orchestration;

var host = GetArg(args, "--ip", "127.0.0.1");
var port = int.TryParse(GetArg(args, "--port", "31002"), out var p) ? p : 31002;
const string DeviceId = "ARR-DEVICE";
const string BaseAddr = "DB2.DBW0";     // 连续 8 个 short（16 字节）
const int Count = 8;

var cfg = new DeviceConfig();
cfg.Add("Protocol", "siemens-s7");
cfg.Add("DeviceId", DeviceId);
cfg.Add("DeviceIp", host);
cfg.Add("Port", port);
cfg.Add("Rack", 0);
cfg.Add("Slot", 1);
cfg.Add("Tags", new List<TagItem>
{
    new() { Id = "ARR", Address = BaseAddr, DataTypeCode = "short",
            TagType = "M", StationNo = DeviceId, Length = Count * 2,
            AccessMode = AccessMode.ReadWrite },
});

using var driver = DriverFactory.Create(cfg);
if (!driver.Connect())
{
    Console.WriteLine($"  ❌ 连接失败 {host}:{port}。忘了起仿真器？");
    return 1;
}
Console.WriteLine($"【1】已连接（{driver.GetType().Name}）");
IPlcDevice dev = driver.Device;
Console.WriteLine();

// ── 数组写 ──────────────────────────────────────────────────────────────
var payload = new short[Count];
for (var i = 0; i < Count; i++) payload[i] = (short)(100 + i);
Console.WriteLine($"【2】数组写入 {Count} 个 short：{string.Join(",", payload)}");
var w = await dev.WriteAsync(BaseAddr, payload).ConfigureAwait(false);
if (!w.IsSuccess)
{
    Console.WriteLine($"      ❌ 写入失败：{w.Message}");
    return 1;
}
Console.WriteLine("      ✅ 写入成功（**一次请求**搬 8 个值）");
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
var back = new short[Count];
for (var i2 = 0; i2 < Count; i2++)
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
Console.WriteLine("      （8 次 `ReadInt16` 调用，但引擎把它们**合并成 1 个块**发出去 ✓）");
Console.WriteLine();

// ── 自检 ────────────────────────────────────────────────────────────────
if (back.SequenceEqual(payload))
{
    Console.WriteLine($"══════ 完成：{Count} 个 short 数组写入 = 回读 ══════");
    Console.WriteLine("  对比：逐个读需 8 次请求；数组读只用 1 次 ⇒ 这就是批量采集的意义。");
    return 0;
}

Console.WriteLine("══════ 不一致 ══════");
Console.WriteLine($"  期望 {string.Join(",", payload)}");
Console.WriteLine($"  实得 {string.Join(",", back)}");
return 1;

static string GetArg(string[] args, string name, string fallback)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(args[i + 1]) ? fallback : args[i + 1];
    return fallback;
}
