using System.Text;
using ZL.IotDriver.Mitsubishi;

internal static class MelsecExample
{
    public static int Run(string host, int port)
    {
        Console.WriteLine($"[Melsec] 连接 {host}:{port}（三菱 MC，Qna-3E 二进制）");
        using var plc = new MitsubishiMcDriver(MitsubishiVersion.Qna_3E, host, port);

        var conn = plc.ConnectServer();
        if (!conn.IsSuccess)
        {
            Console.WriteLine($"  ❌ 连接失败：{conn.Message}");
            Console.WriteLine($"     先起仿真器：plcsim --protocols melsec-mc --ports melsec-mc={port} --headless --auto-start");
            return 1;
        }
        Console.WriteLine($"  ✅ 已连接（{plc.GetType().Name}）");
        Console.WriteLine();

        // ── 读：bool / short / int / float / string ──────────────────────
        var run = plc.ReadBool("M0");
        var count = plc.ReadInt16("D0");
        var total = plc.ReadInt32("D1");
        var temp = plc.ReadFloat("D3");
        var text = plc.ReadString("D10", 8, Encoding.ASCII);

        Console.WriteLine($"[Melsec 读] RUN={run.Content} COUNT={count.Content} TOTAL={total.Content} TEMP={temp.Content} TEXT='{text.Content}'");

        // ── 写：bool / short / int / float ───────────────────────────────
        var wRun = plc.Write("M0", true);
        var wCount = plc.Write("D0", (short)1234);
        var wTotal = plc.Write("D1", 123456);
        var wTemp = plc.Write("D3", 42.5f);

        // MitsubishiMcDriver 提供 ReadString，但没有 WriteString
        // ⇒ 字符串按原始字节写，长度由调用方补到字段宽度。
        var payload = new byte[8];
        Encoding.ASCII.GetBytes("HELLO").CopyTo(payload, 0);
        var wText = plc.WriteBytes("D10", payload);

        Console.WriteLine($"[Melsec 写] RUN={wRun.IsSuccess} COUNT={wCount.IsSuccess} TOTAL={wTotal.IsSuccess} TEMP={wTemp.IsSuccess} TEXT={wText.IsSuccess}");

        // ── 读回确认 ────────────────────────────────────────────────────
        var rRun = plc.ReadBool("M0");
        var rCount = plc.ReadInt16("D0");
        var rTotal = plc.ReadInt32("D1");
        var rTemp = plc.ReadFloat("D3");
        var rText = plc.ReadString("D10", 8, Encoding.ASCII);

        Console.WriteLine($"[Melsec 回读] RUN={rRun.Content} COUNT={rCount.Content} TOTAL={rTotal.Content} TEMP={rTemp.Content} TEXT='{rText.Content}'");

        var ok = rRun.Content
                 && rCount.Content == 1234
                 && rTotal.Content == 123456
                 && Math.Abs(rTemp.Content - 42.5f) < 1e-6
                 && rText.Content == "HELLO";

        Console.WriteLine(ok ? "  ✅ Melsec 读/写/回读一致" : "  ❌ Melsec 回读不一致");
        return ok ? 0 : 1;
    }
}
