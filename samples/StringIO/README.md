# StringIO —— 字符串读写：中文 PLC 最容易踩的 4 个坑

字符串看起来简单，现场却最容易出"写进去读出来是乱码 / 长度不对 / 抛异常"。
本示例把 4 个坑**逐个演示并给出正解**：

| # | 坑 | 正解 |
|---|---|---|
| ① | `Encoding.Default` 跨 TFM 不一致（net8=UTF-8，net48=系统 ANSI） | **显式传编码**：跨 TFM 用 UTF-8；PLC 固定 GB2312 时显式用 936 |
| ② | `Encoding.GetEncoding(936)` 在 .NET 8 **默认不可用** | 本示例改用 UTF-8；要用 GB2312 需宿主注册 `CodePagesEncodingProvider` |
| ③ | `maxLength` 传 `-1` | S7 会读 PLC 头字段真实容量；容量不足会明确报错（这是保护） |
| ④ | `ReadString` 返回值含 S7 的 2 字节头 | 回读校验跳过前 2 字节（max/actual） |

## 跑

```bash
plcsim --protocols siemens-s7 --ports s7=31002 --headless --stay-alive --auto-start
dotnet run --project samples/StringIO/StringIO.csproj -f net8.0
```

> 🔴 设备/点表在 [`stringio.xml`](./stringio.xml) 里：`TEXT` 的 `Address` / `Length`（容量）
> 和 `DeviceIp` / `Port` 都改这个文件；由 `TagKit` 解析，改配置不用重新编译。

## 关键点

- 写入点必须显式 `AccessMode = AccessMode.ReadWrite`（默认只读，fail-safe）；
- 示例会打印 `Encoding.Default` 在当前运行时的真实值，提醒你**不要依赖它**；
- 回读时按 S7 STRING 头解析，验证"写入中文 → 读回一致"。
