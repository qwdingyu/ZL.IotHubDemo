# 字符串读写：布局与编码

> 只装 `ZL.IotDriver` 时，字符串是最容易踩坑的一类数据。
> **一句话：`StringKind` 决定“在 PLC 里长什么样”，`Encoding` 决定“字节怎么排”。**

---

## 一、两个概念，别混

| 概念 | 决定什么 | 例子 |
|---|---|---|
| `StringKind` | 字符串在 PLC 里的**布局**：有没有长度头、头几字节、内容怎么排 | `Raw` / `S7String` / `S7WString` |
| `Encoding` | `string` → `byte[]` 用哪种**字符编码** | UTF-8 / ASCII / UTF-16 / GB2312 |

同一个 `StringKind` 可以配不同编码（WSTRING 除外，它固定 UTF-16）。

---

## 二、三种布局

| 布局 | 线上字节 | 用 `Encoding`？ | `Length` 单位 |
|---|---|---|---|
| `Raw` | `[内容字节]`，**无头** | ✅ | 字节 |
| `S7String` | `[容量 1B][实际长度 1B][内容]` | ✅ | 字节（最大 254） |
| `S7WString` | `[最大字数 2B][实际字数 2B][UTF-16 内容]` | ❌ 固定 UTF-16 | **字符数** |

示例：

```csharp
// Raw：无头，8 字节字段，ASCII
plc.WriteString("HR10", "HELLO", StringKind.Raw, 8, Encoding.ASCII);
// 字节：48 45 4C 4C 4F 00 00 00

// S7String：容量 20、实际 5
plc.WriteString("DB1.DBB20", "HELLO", StringKind.S7String, 20, Encoding.UTF8);
// 字节：14 05 48 45 4C 4C 4F ...

// S7WString：20 个字符容量、实际 5 个字符（大端 UTF-16）
plc.WriteString("DB2.DBW0", "HELLO", StringKind.S7WString, 20, Encoding.UTF8);
// Encoding.UTF8 被忽略；内容按 UTF-16 写
```

### 2.1 `Raw` 的补零与超长

| 条件 | 行为 |
|---|---|
| `Length > 0` 且内容短 | 补 `0x00` 到 `Length` |
| `Length > 0` 且内容长 | **失败**（不截断、不写穿） |
| `Length <= 0` | 不设上限、不补零 |

### 2.2 `S7String` vs `S7-200 SMART`

| CPU | 布局 |
|---|---|
| S7-300 / 400 / 1200 / 1500 | `[容量][实际长度][内容]`（2 字节头） |
| S7-200 SMART | `[实际长度][内容]`（1 字节头，无容量字段） |

> SMART 的布局由 `SiemensS7Driver` 按 `CpuType` **自动切换**，不用手配。

### 2.3 `S7WString` 的 `Length` 是字符数

`WSTRING[20]` 的 `20` 是**字符数**，不是字节数：

- BMP 汉字：1 个字符 = 1 个 UTF-16 码元 = 2 字节；
- emoji：1 个字符 = 2 个码元（代理对）= 4 字节。

> 配 WSTRING 时不要把 `Length` 当成字节数，否则容量会差一倍。

---

## 三、编码：net8 与 net48 不一样

### 3.1 不开 CodePages Provider 时（net8 / net10）

BCL 只有 **7 种**编码：

| 编码 | CodePage | 说明 |
|---|---:|---|
| UTF-8 | 65001 | 默认；中文 3 字节/字 |
| UTF-16 LE | 1200 | `Encoding.Unicode` |
| UTF-16 BE | 1201 | 大端 |
| UTF-32 LE / BE | 12000 / 12001 | PLC 很少用 |
| US-ASCII | 20127 | **仅 7 位英文**；中文变 `?` |
| ISO-8859-1 | 28591 | 西欧；不支持中文 |

`Encoding.GetEncoding("GB2312")` 在 net8 会抛 `ArgumentException`。

### 3.2 要用 GB2312 / GBK / Big5 / Shift-JIS

```csharp
// 需要 NuGet：System.Text.Encoding.CodePages
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var gb2312 = Encoding.GetEncoding("GB2312");
```

- **net48**：自带系统代码页，通常不需要注册；
- **net8 / net10**：不注册就只有上面 7 种。

### 3.3 产品的编码策略

| 配置 | 行为 |
|---|---|
| `StringEncoding` 为 null / 空 / `"Default"` | 统一 UTF-8（不用 `Encoding.Default`） |
| 本运行时能解析 | 使用该编码 |
| 显式配置但解析不了 | **写操作失败**，不会静默换编码 |

---

## 四、怎么选

| 场景 | 推荐 |
|---|---|
| 中文 / 多语言，S7-1200/1500 | `S7WString`（固定 UTF-16，不依赖代码页） |
| 中文，S7String 字段 | `UTF-8`（3 字节/字，字段要够）或 `GB2312`（2 字节/字，需运行时支持） |
| 英文 / ASCII 协议 | `ASCII` 或 `UTF-8` |
| Modbus / 通用 Raw | 与设备侧约定一致；优先 UTF-8 或 ASCII |
| S7-200 SMART | 不要手配 SMART 布局，驱动按 CPU 自动选 |

---

## 五、五个坑

1. **`ASCII` 会把中文写成 `?`** —— 不是异常，是静默替换；中文请用 UTF-8 或 WSTRING。
2. **`S7WString` 的 `Encoding` 参数无效** —— 它固定 UTF-16 码元。
3. **`Length` 在 WSTRING 下是字符数** —— 不是字节数。
4. **别把 UTF-16 塞进 `S7String`** —— 每个字符之间会出现 `00`，除非双方明确约定。
5. **net8 默认没有 GB2312** —— 要么注册 Provider，要么改用 UTF-8 / WSTRING。

---

## 六、只装 Driver 时的读写 API

| 驱动 | 读 | 写 |
|---|---|---|
| `SiemensS7Driver` | `ReadS7String(...)` | `WriteString(..., StringKind.S7String, ...)`（实例方法） |
| `ModbusTcpDriver` | `ReadString(...)` | `WriteString(..., StringKind.Raw, ...)`（实例方法） |
| `OmronFinsDriver` | `ReadString(...)` | **无** `WriteString` ⇒ `WriteBytes` + 自己补字段宽度 |
| `MitsubishiMcDriver` | `ReadString(...)` | **无** `WriteString` ⇒ 同上 |

> `PlcDeviceStringWriteExtensions.WriteString(this IPlcDevice, ...)` 在 `ZL.IotEngine` 里，
> 不在 `ZL.IotDriver` 里。只装 Driver 时不要调用那个扩展方法。
