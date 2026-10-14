# Modbus · 多协议接入

**一句话：换成 Modbus TCP，只改三样东西。**

和 [QuickStart](../QuickStart)（S7）完全同一套用法，只是换协议、换地址写法、多两个参数。

## 跑

```bash
dotnet tool install -g PlcSimulator.Cli     # 一次性
plcsim --protocols modbus-tcp --ports modbus=31502 --headless --auto-start   # 另开一个窗口
cd samples/Modbus && dotnet run
```

## 你会看到

```
【2】写入 → 读回
      线圈 COIL_1  写 True  → 读 True   ✅
      寄存器 REG_1 写 1234  → 读 1234  ✅

【3】同地址 1 的线圈与寄存器互不干扰（区域由数据类型决定）
```

## 和 S7 的三处差异

| | S7 | Modbus |
| --- | --- | --- |
| `Protocol` | `siemens-s7` | `modbus-tcp` |
| 地址写法 | `DB1.DBD4` / `DB1.DBW2` / `DB1.DBX0.0` | **纯数字偏移量**：`0` / `1` / `2` |
| 额外参数 | `Rack` / `Slot` | `Station`（站号）/ `AddressStartWithZero` |

## ⚠️ 地址写法（实测结论，最容易踩的三个坑）

1. **地址是纯数字偏移量**，不是 `40001` 这类 5 位传统编号。
   🔴 写 `40001` 会被当成"偏移量 40001"——**写入看起来成功，但读回来是 0**。
2. **不要写 `COIL1` / `HR100` 这种前缀写法**，原生驱动不识别。
3. **区域由数据类型决定**：`bool` ⇒ 线圈；`short` / `int` / `float` ⇒ 保持寄存器。
   所以示例里 `COIL_1` 与 `REG_1` 用的是同一个数字地址 `1`，但读到的值互不干扰。
