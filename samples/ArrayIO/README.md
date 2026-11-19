# ArrayIO —— 数组批量读写（一次搬一批）

真实点表里大量数据是**连续排列**的：配方、报警字、波形缓冲。
逐个点读 = 请求次数爆炸；**按数组读/写 = 一次请求搬 N 个值**。

## 演示什么

- 一次写入 8 个 `short`（**一次请求**）；
- 回读 8 个值，并解释一个容易误解的点：
  **数组读 API 的跨 TFM 差异**（net48 不支持 `where T : unmanaged` 的泛型数组读）
  ⇒ 示例改用 8 次 `ReadInt16`，但引擎会**自动合并成 1 个协议块**发出
  ⇒ 调用层是 8 次，**协议层仍是 1 次批量请求**。

## 跑

```bash
# 另开窗口
plcsim --protocols siemens-s7 --ports s7=31002 --headless --stay-alive --auto-start

# 仓库根目录
dotnet run --project samples/ArrayIO/ArrayIO.csproj -f net8.0
```

> 🔴 设备/点表在 [`arrayio.xml`](./arrayio.xml) 里：`ARR` 的 `Address` / `Length`（字节数）
> 与 `DeviceIp` / `Port` 都改这个文件；由 `TagKit` 解析，改配置不用重新编译。

## 关键输出

```
【2】数组写入 8 个 short：100,101,...,107
      ✅ 写入成功（**一次请求**搬 8 个值）
【3】数组回读
      回读：100,101,...,107
      （8 次 ReadInt16 调用，但引擎把它们合并成 1 个块发出去）
```

> 这个示例的注释里写清了"为什么不能直接用泛型 `ReadArray<T>`"——
> **跨 TFM 的 API 事实**，不是实现偷懒。
