using System.Text;
using ZL.IotDriver.Omron;

internal static class OmronExample
{
    public static int Run(string host, int port)
    {
        Console.WriteLine($"[Omron] 连接 {host}:{port}（FINS/TCP）");
        using var plc = new OmronFinsDriver(host, port);

        var conn = plc.ConnectServer();
        if (!conn.IsSuccess)
        {
            Console.WriteLine($"  ❌ 连接失败：{conn.Message}");
            Console.WriteLine($"     先起仿真器：plcsim --protocols fins-tcp --ports fins-tcp={port} --headless --auto-start");
            return 1;
        }
        Console.WriteLine($"  ✅ 已连接（{plc.GetType().Name}）");
        Console.WriteLine();

        // ── 读：bool / short / int / float / string ──────────────────────
        var run = plc.ReadBool("D0.0");
        var count = plc.ReadInt16("D1");
        var total = plc.ReadInt32("D2");
        var temp = plc.ReadFloat("D4");
        var text = plc.ReadString("D10", 8, Encoding.ASCII);

        Console.WriteLine($"[Omron 读] RUN={run.Content} COUNT={count.Content} TOTAL={total.Content} TEMP={temp.Content} TEXT='{text.Content}'");

        // ── 写：bool / short / int / float ───────────────────────────────
        var wRun = plc.Write("D0.0", true);
        var wCount = plc.Write("D1", (short)1234);
        var wTotal = plc.Write("D2", 123456);
        var wTemp = plc.Write("D4", 42.5f);

        // OmronFinsDriver 只提供 ReadString，没有 WriteString；
        // 只装 ZL.IotDriver 时也不能用 ZL.IotEngine 的 WriteString 扩展
        // ⇒ 字符串按原始字节写，长度由调用方补到字段宽度。
        var payload = new byte[8];
        Encoding.ASCII.GetBytes("HELLO").CopyTo(payload, 0);
        var wText = plc.WriteBytes("D10", payload);

        Console.WriteLine($"[Omron 写] RUN={wRun.IsSuccess} COUNT={wCount.IsSuccess} TOTAL={wTotal.IsSuccess} TEMP={wTemp.IsSuccess} TEXT={wText.IsSuccess}");

        // ── 读回确认 ────────────────────────────────────────────────────
        var rRun = plc.ReadBool("D0.0");
        var rCount = plc.ReadInt16("D1");
        var rTotal = plc.ReadInt32("D2");
        var rTemp = plc.ReadFloat("D4");
        var rText = plc.ReadString("D10", 8, Encoding.ASCII);

        Console.WriteLine($"[Omron 回读] RUN={rRun.Content} COUNT={rCount.Content} TOTAL={rTotal.Content} TEMP={rTemp.Content} TEXT='{rText.Content}'");

        var ok = rRun.Content
                 && rCount.Content == 1234
                 && rTotal.Content == 123456
                 && Math.Abs(rTemp.Content - 42.5f) < 1e-6
                 && rText.Content == "HELLO";

        Console.WriteLine(ok ? "  ✅ Omron 读/写/回读一致" : "  ❌ Omron 回读不一致");
        return ok ? 0 : 1;
    }
}
