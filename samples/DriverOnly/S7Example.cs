using System.Text;
using ZL.IotAbstraction.Tag;
using ZL.IotDriver.Siemens;

internal static class S7Example
{
    public static int Run(string host, int port)
    {
        Console.WriteLine($"[S7] 连接 {host}:{port}（S7-1200）");
        using var plc = new SiemensS7Driver(SiemensCpuType.S7_1200, host, port, rack: 0, slot: 1);

        var conn = plc.ConnectServer();
        if (!conn.IsSuccess)
        {
            Console.WriteLine($"  ❌ 连接失败：{conn.Message}");
            Console.WriteLine($"     先起仿真器：plcsim --protocols siemens-s7 --ports s7={port} --headless --auto-start");
            return 1;
        }
        Console.WriteLine($"  ✅ 已连接（{plc.GetType().Name}）");
        Console.WriteLine();

        // ── 读：bool / short / int / float / string ──────────────────────
        var run = plc.ReadBool("DB1.DBX0.0");
        var count = plc.ReadInt16("DB1.DBW2");
        var total = plc.ReadInt32("DB1.DBD4");
        var temp = plc.ReadFloat("DB1.DBD8");
        var text = plc.ReadS7String("DB1.DBB20", Encoding.UTF8);

        Console.WriteLine($"[S7 读] RUN={run.Content} COUNT={count.Content} TOTAL={total.Content} TEMP={temp.Content} TEXT='{text.Content}'");

        // ── 写：bool / short / int / float / S7String ────────────────────
        // bool 做**双向**验证：先写 false 再写 true，证明两个方向都真的写进去了
        // （只写 true 的话，PLC 原本就是 true 时无法区分"写成功"与"没写"）
        var wRunFalse = plc.Write("DB1.DBX0.0", false);
        var rRunFalse = plc.ReadBool("DB1.DBX0.0");
        var wRunTrue = plc.Write("DB1.DBX0.0", true);
        var rRunTrue = plc.ReadBool("DB1.DBX0.0");

        var wCount = plc.Write("DB1.DBW2", (short)1234);
        var wTotal = plc.Write("DB1.DBD4", 123456);
        var wTemp = plc.Write("DB1.DBD8", 42.5f);
        var wText = plc.WriteString("DB1.DBB20", "HELLO", StringKind.S7String, 20, Encoding.UTF8);

        Console.WriteLine($"[S7 写] RUN=false:{wRunFalse.IsSuccess} RUN=true:{wRunTrue.IsSuccess} COUNT={wCount.IsSuccess} TOTAL={wTotal.IsSuccess} TEMP={wTemp.IsSuccess} TEXT={wText.IsSuccess}");

        // ── 读回确认 ────────────────────────────────────────────────────
        var rCount = plc.ReadInt16("DB1.DBW2");
        var rTotal = plc.ReadInt32("DB1.DBD4");
        var rTemp = plc.ReadFloat("DB1.DBD8");
        var rText = plc.ReadS7String("DB1.DBB20", Encoding.UTF8);

        Console.WriteLine($"[S7 回读] RUN(false→{rRunFalse.Content}, true→{rRunTrue.Content}) COUNT={rCount.Content} TOTAL={rTotal.Content} TEMP={rTemp.Content} TEXT='{rText.Content}'");

        var ok = !rRunFalse.Content
                 && rRunTrue.Content
                 && rCount.Content == 1234
                 && rTotal.Content == 123456
                 && Math.Abs(rTemp.Content - 42.5f) < 1e-6
                 && rText.Content == "HELLO";

        Console.WriteLine(ok ? "  ✅ S7 读/写/回读一致" : "  ❌ S7 回读不一致");
        return ok ? 0 : 1;
    }
}
