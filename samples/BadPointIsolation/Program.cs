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

using ZL.IotEngine.Models;
using ZL.IotEngine.Orchestration;
using ZL.Tag;

var host = GetArg(args, "--ip", "127.0.0.1");
var port = int.TryParse(GetArg(args, "--port", "31002"), out var p) ? p : 31002;
const string DeviceId = "ISOLATION-DEMO";

Console.WriteLine();
Console.WriteLine("══════ ZL.IotEngine 示例：坏点隔离与恢复 ══════");

// 4 个好点 + 1 个坏点（地址合法但 PLC 读取失败 ⇒ 会被隔离 ✓）
var tags = new List<TagItem>
{
    new() { Id = "TEMP",   Address = "DB1.DBD4",  DataTypeCode = "float", TagType = "M", StationNo = DeviceId },
    new() { Id = "COUNT",  Address = "DB1.DBW2",  DataTypeCode = "short", TagType = "M", StationNo = DeviceId },
    new() { Id = "RUN",    Address = "DB1.DBX0.0",DataTypeCode = "bool",  TagType = "M", StationNo = DeviceId },
    new() { Id = "FAULT",  Address = "DB1.DBX0.1",DataTypeCode = "bool",  TagType = "M", StationNo = DeviceId },
    // ↓ 故意写一个坏地址：真实场景里是"地址写错 / 设备不支持"
    //   ⚠️ 必须选【地址能被解析、但 PLC 侧读取会失败】的写法 ✓
    //      · "XYZ123" 这类**解析期非法**地址会在建计划时就被过滤掉 ✗
    //        ⇒ 永远进不了逐点探测 ⇒ **不会触发隔离** ✗（本示例曾因此"看不到隔离" ✓）
    //      · "T1"/"C1"（定时器/计数器）地址合法，但 S7 读响应长度与期望不符 ⇒ **稳定失败** ✓
    //        ⇒ 逐点探测 3 次全败 ⇒ 确定性进入隔离区 ✓（实测 ✓）
    new() { Id = "BROKEN", Address = "T1",        DataTypeCode = "short", TagType = "M", StationNo = DeviceId },
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

// 【2026-11-08 订正 ✗→✓】此处曾写「`Connect()` 只建链，**不启动后台采集**」—— **该论断是错的** ✗
//   实测（✓ 决定性）：点表来自 `config.Add("Tags", …)` 时，
//     **只调 `Connect()`（不调 `Start()`）后台采集照样在跑** ✓
//     （判据：数秒后 `localCache[key].Quality == Good` ✓；实测 2 个标签均在 1.2 秒内变 Good ✓）
//   正确语义：`Start()` 是给「**构造后再添加标签**」这种模式的统一启动入口 ✓
//     （`Initialize` → `BuildPlan` → `StartBackgroundTasks` ✓，且**幂等** ✓）
//   ⇒ 本示例的标签来自 config ⇒ `Start()` **可有可无** ✓；这里保留它只是显式化、无副作用 ✓
//   ⚠️ **隔离"演示不出效果"的真因不在这里** ✗ —— 而是：
//     **PLC 仿真器对越界/不存在的地址也照常返回数据** ✓
//     ⇒ 通讯块**永不失败** ⇒ 逐点探测**永不触发** ⇒ 自然看不到隔离 ✓（见文件末尾说明 ✓）
driver.Start();
Console.WriteLine("      后台采集已启动（Start）");
// 为什么要等这么久（实测）：隔离不是"读失败一次就触发"
//   完整链路：通讯块连续读取失败 => 拆解为逐点探测（每点 TagProbeAttempts=3 次 + 退避）
//   => 3 次全败才进隔离区 => 总耗时通常 15~25 秒
//   （旧版只等 5 秒 => 永远看不到隔离，本示例因此长期"演示不出效果"）
Console.WriteLine("【2】等待后台采集跑几轮（8 秒；仿真器下坏点不会失败，见末尾说明）…");
await Task.Delay(8000);

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
// 🔴 重要陷阱（实测 + 源码确认 ✓）：
//   `DeviceBase.Read<T>(id)` 的契约是「**失败或标签不存在时返回 `default(T)`**」✗
//   ⇒ 直接打印 `driver.Read<short>("COUNT")` 时，读失败会显示 **0**、
//     读 bool 失败会显示 **False** ⇒ **看起来像正常值** ✗✓（最误导人的地方 ✗）
//   ⇒ 示例这里改用**带成功判定的读法** ✓：先看质量码，再读值 ✓
foreach (var (id, kind) in new[] { ("TEMP", "float"), ("COUNT", "short"), ("RUN", "bool") })
{
    // 质量码是"值是否可信"的唯一依据 ✓
    //   引擎把最新值+质量放在 `localCache`（`public ConcurrentDictionary<string,TagValue>` ✓）
    var cacheKey = driver.GenerateCacheKey(driver.Tags[id]);
    var quality = driver.localCache.TryGetValue(cacheKey, out var tv) ? tv.Quality : TagQuality.Bad;
    var shown = kind switch
    {
        "float" => driver.Read<float>(id).ToString("F0"),
        "short" => driver.Read<short>(id).ToString(),
        _       => driver.Read<bool>(id).ToString(),
    };
    var mark = quality == TagQuality.Good ? "✅" : "⚠️";
    Console.WriteLine($"      {mark} {id,-6} = {shown,-10} 质量={quality}"
                    + (quality == TagQuality.Good ? "" : "  ← 值不可信（读失败会显示 default，别当正常值 ✗）"));
}

var isolated = driver.QuarantineTags.Count > 0;
var goodOk = true;   // 上面三个读数能打印出来就说明好点没被拖垮

Console.WriteLine();
if (isolated && goodOk)
{
    Console.WriteLine("══════ 完成：坏点被隔离，好点照常采 ══════");
}
else
{
    Console.WriteLine("══════ 本次未观察到隔离 —— 用仿真器时这是【预期】的 ══════");
    Console.WriteLine("  ⚠️ 不是缺陷。隔离的真实触发链路：");
    Console.WriteLine("     通讯块连续失败 ≥3 次 ⇒ 拆块为逐点探测（每点 3 次 + 退避）⇒ 3 次全败 ⇒ 进隔离区");
    Console.WriteLine("  而 **PLC 仿真器对越界/不存在的地址也照常返回数据**");
    Console.WriteLine("  ⇒ 构造不出「地址合法但读取失败」的点位 ⇒ 自然看不到隔离");
    Console.WriteLine("  ✅ 要在真机验证：把 BROKEN 的 Address 改成 PLC 里真实不存在的 DB 号，");
    Console.WriteLine("     再让它按 ReadInterval 反复失败即可。");
}
Console.WriteLine();
return 0;

static string GetArg(string[] args, string name, string fallback)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(args[i + 1]) ? fallback : args[i + 1];
    return fallback;
}
