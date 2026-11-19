// ═══════════════════════════════════════════════════════════════════════
//  ZL.IotEngine 示例：OPC UA Client（仿真器作为 OPC UA Server）
//
//  这个示例演示“**PLC 侧本来就是 OPC UA Server**”时的采集方式：
//    仿真器/真实 PLC 暴露 OPC UA 地址空间 → 我们用 OpcUaClientDriver 连接并读写。
//
//  准备（另开一个窗口，在 samples/OpcUaClient 目录）：
//      plcsim --protocols opcua --ports opcua=4899 \
//             --points-file points.json --headless --stay-alive --auto-start
//  运行：
//      cd samples/OpcUaClient && dotnet run -f net8.0
//
//  想验证“我们的程序作为 OPC UA Server”（普通协议采集 → OPC UA 对外发放）：
//      先启动 samples/OpcUaServer，再：
//      dotnet run -f net8.0 -- --config opcua-server.xml
//
//  🔴 设备/NodeId 在 XML 里：改配置不用重新编译。
// ═══════════════════════════════════════════════════════════════════════

using System.Xml.Linq;
using ZL.IotAbstraction.Abstractions;
using ZL.IotAbstraction.Tag;
using ZL.IotDriver.OpcUa;

var configPath = ResolveConfigPath(args, "opcua-sim.xml");
if (!File.Exists(configPath))
{
    Console.WriteLine($"找不到配置文件：{configPath}");
    Console.WriteLine("默认读同目录的 opcua-sim.xml；也可以：dotnet run -f net8.0 -- --config opcua-server.xml");
    return 1;
}

var root = XElement.Load(configPath);
if (root.Name != "Device")
{
    Console.WriteLine($"配置文件根节点应为 <Device>，实际是 <{root.Name}>：{configPath}");
    return 1;
}

var host = (string)root.Attribute("DeviceIp") ?? "127.0.0.1";
var port = int.TryParse((string)root.Attribute("Port"), out var p) ? p : 4899;

var tags = TagKit.LoadTags(configPath, out var tagDiag);
if (tags.Count == 0)
{
    Console.WriteLine($"点表没有加载出任何点位：{configPath}");
    Console.WriteLine($"{tagDiag.Outcome}：{tagDiag.Message}");
    return 1;
}

Console.WriteLine();
Console.WriteLine("══════ ZL.IotEngine 示例：OPC UA Client ══════");
Console.WriteLine($"  配置文件：{configPath}");
Console.WriteLine($"  端点：opc.tcp://{host}:{port}");
Console.WriteLine();

// 说明：2.3.0 的 NativeDriversPlugin 尚未注册 opcua 协议键，
// 所以这里直接使用 OpcUaClientDriver（协议调试/客户端接入的正确形状）。
// 若后续版本已注册，可改为 DriverFactory.Create(cfg) + Read<T>。
using var driver = new OpcUaClientDriver(host, port);
var wrote = false;
try
{
    var conn = driver.ConnectServer();
    if (!conn.IsSuccess)
    {
        Console.WriteLine($"❌ 连接失败：{conn.Message}");
        Console.WriteLine("   仿真器启动了吗？端口/NodeId 是否与配置一致？");
        return 1;
    }
    Console.WriteLine($"✅ 已连接（{driver.GetType().Name}）");
    Console.WriteLine();

    foreach (var tag in tags.Values)
    {
        var type = PlcDataTypeMap.Normalize(tag.DataTypeCode);
        switch (type)
        {
            case "float":
                Print(tag, driver.ReadFloat(tag.Address));
                break;
            case "ushort":
                Print(tag, driver.ReadUInt16(tag.Address));
                break;
            case "short":
                Print(tag, driver.ReadInt16(tag.Address));
                break;
            case "bool":
                Print(tag, driver.ReadBool(tag.Address));
                break;
            default:
                Console.WriteLine($"      {tag.Id,-10} 暂不支持的演示类型：{tag.DataTypeCode}");
                break;
        }
    }

    // 写一个已知值 → 读回，验证 OPC UA 写链路
    //   仿真器配置里写 temp；OpcUaServer 配置里 Temperature/Setpoint 为只读 ⇒ 跳过写入。
    var writable = tags.Values.FirstOrDefault(t =>
        t.AccessMode == AccessMode.ReadWrite
        && string.Equals(PlcDataTypeMap.Normalize(t.DataTypeCode), "float", StringComparison.OrdinalIgnoreCase));
    if (writable != null)
    {
        wrote = true;
        Console.WriteLine();
        Console.WriteLine($"【写入 → 读回：{writable.Id}】");
        var w = driver.WriteAsync(writable.Address, 42.5f).GetAwaiter().GetResult();
        var back = driver.ReadFloat(writable.Address);
        Console.WriteLine(w.IsSuccess
            ? $"      {writable.Id} 写 42.5 → 读回 {back.Content}  {(back.IsSuccess && Math.Abs(back.Content - 42.5f) < 1e-6 ? "✅" : "❌")}"
            : $"      {writable.Id} 写入失败：{w.Message}");
    }

    Console.WriteLine();
    Console.WriteLine($"══════ 完成：OPC UA {(wrote ? "读/写" : "读取")}链路验证通过 ══════");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"❌ OPC UA 异常：{ex.GetType().Name}: {ex.Message}");
    return 1;
}
finally
{
    driver.ConnectClose();
}

static void Print<T>(TagItem tag, DeviceResult<T> result)
{
    Console.WriteLine(result.IsSuccess
        ? $"      {tag.Id,-10} = {result.Content}   （{tag.Address}）"
        : $"      {tag.Id,-10} 读取失败：{result.Message}");
}

static string ResolveConfigPath(string[] args, string defaultFileName)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals("--config", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(args[i + 1]);
    }

    var besideExe = Path.Combine(AppContext.BaseDirectory, defaultFileName);
    if (File.Exists(besideExe)) return besideExe;

    return Path.GetFullPath(defaultFileName);
}
