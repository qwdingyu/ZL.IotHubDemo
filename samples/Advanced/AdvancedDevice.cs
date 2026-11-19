// <copyright file="AdvancedDevice.cs" company="ZL.IotHub">
//   业务壳示例：只写业务逻辑，驱动创建/连接/事件订阅由 ManagedDeviceBase 自动完成。
// </copyright>

using System;
using System.Threading;
using ZL.IotEngine.Orchestration;
using ZL.IotAbstraction.Orchestration;
using ZL.IotAbstraction.Abstractions;
using ZL.IotAbstraction.Tag;

namespace ZL.IotHub.Sample.Advanced
{
    /// <summary>
    /// 业务端设备壳（推荐形态）。
    /// <para>
    /// 继承 <see cref="ManagedDeviceBase"/> 后，框架自动完成：
    /// ① `DriverFactory` 创建底层驱动（Native/HSL 自动路由）；
    /// ② 连接管理 / 重连 / 心跳；
    /// ③ 订阅驱动触发事件并转发到 <see cref="OnManagedTriggerDataChange"/>。
    /// 业务代码只关心"值变了要做什么"。
    /// </para>
    /// <para>
    /// 构造必须转发 **注入式** `(DeviceConfig, IPlcDriver)`：驱动由编排层创建后注入，
    /// 壳不再 lazy 自建驱动（避免授权名额泄漏与时序窗口）。
    /// </para>
    /// </summary>
    public sealed class AdvancedDevice : ManagedDeviceBase
    {
        private int _tempTriggerCount;
        private float _lastTemp;

        public AdvancedDevice(DeviceConfig config, IPlcDriver driver) : base(config, driver)
        {
        }

        /// <summary>收到 TEMP 变化回调的次数（仅演示用）。</summary>
        public int TempTriggerCount => Volatile.Read(ref _tempTriggerCount);

        /// <summary>最近一次 TEMP 回调值。</summary>
        public float LastTemp => Volatile.Read(ref _lastTemp);

        /// <summary>
        /// 业务钩子：M 标签值变化时回调。
        /// <para>
        /// 注意：这里只做"记录/轻量业务"，**不要**在回调里做阻塞 IO 或抛异常
        /// （回调链路异常会被隔离，但你的业务就静默不执行了）。
        /// </para>
        /// </summary>
        protected override void OnManagedTriggerDataChange(string id, object value, TagItem tag)
        {
            if (!string.Equals(id, "TEMP", StringComparison.OrdinalIgnoreCase)) return;
            if (value is float f)
            {
                Volatile.Write(ref _lastTemp, f);
                Interlocked.Increment(ref _tempTriggerCount);
            }
        }
    }
}
