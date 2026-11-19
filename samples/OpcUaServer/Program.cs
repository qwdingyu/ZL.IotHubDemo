using ZL.IotHub.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();

// 通用 SCADA 网关：SignalR + REST + OPC UA 对外暴露桥（OpcUa:Enabled 默认 true）
builder.Services.AddZlScada(builder.Configuration);

var app = builder.Build();

// 配置化设备注册：从 appsettings 的 Scada:Devices 读取设备/点表。
// 必须在 app.Run() 之前完成 —— OPC UA 地址空间只在启动时建一次。
ScadaDeviceProvisioner.ProvisionFromConfiguration(app.Configuration, app.Logger);

// 静态文件 / health / scadahub / REST API
app.MapZlScada();

app.MapGet("/", () => Results.Text(
    "ZL.IotHub.Server OPC UA 示例\n" +
    "  GET  /health\n" +
    "  POST /api/devices/{id}/batch-read?live=true\n" +
    "  OPC UA: opc.tcp://127.0.0.1:4840  节点: ns=2;s=tag.<设备>.<点位>\n",
    "text/plain; charset=utf-8"));

app.Run();
