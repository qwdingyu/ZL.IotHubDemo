using System.Text;
using ZL.IotAbstraction.Tag;
using ZL.IotDriver.ModbusTcp;

internal static class ModbusExample
{
    public static int Run(string host, int port)
    {
        Console.WriteLine($"[Modbus] 连接 {host}:{port}（Modbus TCP，Raw 寄存器模式）");
        // Raw 模式：HR0 就是保持寄存器 0，不涉及 40001 的 5 位编号换算
        using var plc = new ModbusTcpDriver(
            host,
            port,
            stationNumber: 1,
            timeoutMs: 3000,
            addressing: ModbusAddressingMode.Raw,
            defaultArea: "HR");

        var conn = plc.ConnectServer();
        if (!conn.IsSuccess)
        {
            Console.WriteLine($"  ❌ 连接失败：{conn.Message}");
            Console.WriteLine($"     先起仿真器：plcsim --protocols modbus-tcp --ports modbus={port} --headless --auto-start");
            return 1;
        }
        Console.WriteLine($"  ✅ 已连接（{plc.GetType().Name}）");
        Console.WriteLine();

        // ── 读：short / int / float / string ─────────────────────────────
        var count = plc.ReadInt16("HR0");
        var total = plc.ReadInt32("HR1");
        var temp = plc.ReadFloat("HR3");
        var text = plc.ReadString("HR10", 8, Encoding.ASCII);

        Console.WriteLine($"[Modbus 读] COUNT={count.Content} TOTAL={total.Content} TEMP={temp.Content} TEXT='{text.Content}'");

        // ── 写：short / int / float / Raw string ─────────────────────────
        var wCount = plc.Write("HR0", (short)1234);
        var wTotal = plc.Write("HR1", 123456);
        var wTemp = plc.Write("HR3", 42.5f);
        var wText = plc.WriteString("HR10", "HELLO", StringKind.Raw, 8, Encoding.ASCII);

        Console.WriteLine($"[Modbus 写] COUNT={wCount.IsSuccess} TOTAL={wTotal.IsSuccess} TEMP={wTemp.IsSuccess} TEXT={wText.IsSuccess}");

        // ── 读回确认 ────────────────────────────────────────────────────
        var rCount = plc.ReadInt16("HR0");
        var rTotal = plc.ReadInt32("HR1");
        var rTemp = plc.ReadFloat("HR3");
        var rText = plc.ReadString("HR10", 8, Encoding.ASCII);

        Console.WriteLine($"[Modbus 回读] COUNT={rCount.Content} TOTAL={rTotal.Content} TEMP={rTemp.Content} TEXT='{rText.Content}'");

        var ok = rCount.Content == 1234
                 && rTotal.Content == 123456
                 && Math.Abs(rTemp.Content - 42.5f) < 1e-6
                 && rText.Content == "HELLO";

        Console.WriteLine(ok ? "  ✅ Modbus 读/写/回读一致" : "  ❌ Modbus 回读不一致");
        return ok ? 0 : 1;
    }
}
