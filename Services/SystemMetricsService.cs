using System.Diagnostics;
using System.Runtime.InteropServices;
using AFMediaBar.Interop;
using AFMediaBar.Models;
using LibreHardwareMonitor.Hardware;

namespace AFMediaBar.Services;

/// <summary>
/// 采样系统内存、CPU、GPU 与当前进程内存，并复用跨周期计数器状态。
/// Samples system and process metrics while reusing counters across sampling intervals.
/// </summary>
internal sealed class SystemMetricsService : IDisposable
{
    private readonly Process _currentProcess = Process.GetCurrentProcess();
    private ulong _previousIdle;
    private ulong _previousKernel;
    private ulong _previousUser;
    private bool _hasPreviousCpuSample;
    private GpuUsageSampler? _gpuUsageSampler;

    internal SystemMetricsSnapshot Sample(MetricSettings settings)
    {
        var includeMetrics = settings.Enabled;
        var systemMemoryPercent = includeMetrics && settings.ShowSystemMemory
            ? ReadSystemMemoryPercent()
            : 0;
        var systemCpuPercent = includeMetrics && settings.ShowSystemCpu
            ? ReadSystemCpuPercent()
            : ResetCpuSample();
        var systemGpuPercent = ReadSystemGpuPercent(
            includeMetrics && settings.ShowSystemGpu);
        var processMemoryMegabytes = 0L;
        if (includeMetrics && settings.ShowProcessMemory)
        {
            _currentProcess.Refresh();
            processMemoryMegabytes = (long)Math.Round(
                _currentProcess.WorkingSet64 / 1024d / 1024d);
        }

        var batteryPercent = includeMetrics && settings.ShowBattery
            ? ReadBatteryPercent()
            : null;

        var (fanRpm, cpuTemperature) = includeMetrics &&
            (settings.ShowFan || settings.ShowTemperature)
                ? ReadHardwareSensors()
                : (null, null);

        return new SystemMetricsSnapshot(
            systemMemoryPercent,
            systemCpuPercent,
            systemGpuPercent,
            processMemoryMegabytes,
            batteryPercent,
            fanRpm,
            cpuTemperature);
    }

    private int? ResetCpuSample()
    {
        _hasPreviousCpuSample = false;
        return null;
    }

    private int? ReadSystemGpuPercent(bool includeGpu)
    {
        if (!includeGpu)
        {
            // PDH 查询持有原生句柄和非托管缓冲区，禁用 GPU 指标时立即释放。
            // PDH owns native handles and buffers; release them as soon as GPU metrics stop.
            _gpuUsageSampler?.Dispose();
            _gpuUsageSampler = null;
            return null;
        }

        _gpuUsageSampler ??= new GpuUsageSampler();
        return _gpuUsageSampler.Sample();
    }

    private static int ReadSystemMemoryPercent()
    {
        var status = NativeMethods.MemoryStatusEx.Create();
        if (!NativeMethods.GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0)
        {
            return 0;
        }

        var used = status.TotalPhysical - status.AvailablePhysical;
        return (int)Math.Clamp(Math.Round(used * 100d / status.TotalPhysical), 0, 100);
    }

    /// <summary>
    /// 读取电池剩余百分比。无电池或读取失败/未知时返回 null。
    /// </summary>
    private static int? ReadBatteryPercent()
    {
        var status = new NativeMethods.SystemPowerStatus();
        if (!NativeMethods.GetSystemPowerStatus(ref status))
        {
            return null;
        }

        // BatteryFlag 为 128 表示系统无电池（台式机）。
        // BatteryLifePercent 为 255 表示未知。
        if (status.BatteryFlag == 128 || status.BatteryLifePercent == 255)
        {
            return null;
        }

        return Math.Clamp((int)status.BatteryLifePercent, 0, 100);
    }

    // LibreHardwareMonitor 硬件句柄，仅当启用风扇/温度指标时懒加载。
    private Computer? _hardwareComputer;
    private int? _cachedFanRpm;
    private int? _cachedCpuTemperature;
    private DateTime _lastHardwareRead;

    /// <summary>
    /// 通过 LibreHardwareMonitor 只读读取风扇转速（RPM）与 CPU 温度（℃）。
    /// 12 秒内复用上次结果以降低后台硬件访问开销。
    /// </summary>
    private (int? FanRpm, int? CpuTemperature) ReadHardwareSensors()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastHardwareRead).TotalSeconds < 12 &&
            (_cachedFanRpm.HasValue || _cachedCpuTemperature.HasValue))
        {
            return (_cachedFanRpm, _cachedCpuTemperature);
        }

        _lastHardwareRead = now;
        try
        {
            EnsureHardwareComputer();
            if (_hardwareComputer is null)
            {
                return (null, null);
            }

            var visitor = new UpdateVisitor();
            _hardwareComputer.Accept(visitor);

            int? maxFan = null;
            int? maxTemperature = null;
            foreach (var hardware in _hardwareComputer.Hardware)
            {
                foreach (var sensor in hardware.Sensors)
                {
                    if (!sensor.Value.HasValue)
                    {
                        continue;
                    }

                    if (sensor.SensorType == SensorType.Fan &&
                        sensor.Value.Value > 0)
                    {
                        maxFan = maxFan is int f
                            ? Math.Max(f, (int)Math.Round(sensor.Value.Value))
                            : (int)Math.Round(sensor.Value.Value);
                    }
                    else if (sensor.SensorType == SensorType.Temperature)
                    {
                        maxTemperature = maxTemperature is int t
                            ? Math.Max(t, (int)Math.Round(sensor.Value.Value))
                            : (int)Math.Round(sensor.Value.Value);
                    }
                }
            }

            _cachedFanRpm = maxFan;
            _cachedCpuTemperature = maxTemperature;
            return (_cachedFanRpm, _cachedCpuTemperature);
        }
        catch
        {
            // 硬件访问受限（常见于非管理员运行）或无传感器时静默返回 null。
            return (null, null);
        }
    }

    private void EnsureHardwareComputer()
    {
        if (_hardwareComputer is not null)
        {
            return;
        }

        try
        {
            _hardwareComputer = new Computer
            {
                IsCpuEnabled = true,
                IsMotherboardEnabled = true,
                IsGpuEnabled = false,
                IsMemoryEnabled = false,
                IsStorageEnabled = false,
                IsControllerEnabled = false,
                IsNetworkEnabled = false,
                IsPsuEnabled = false,
                IsBatteryEnabled = false
            };
            _hardwareComputer.Open();
        }
        catch
        {
            _hardwareComputer = null;
        }
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware)
            {
                subHardware.Accept(this);
            }
        }

        public void VisitSensor(ISensor sensor) { }

        public void VisitParameter(IParameter parameter) { }
    }

    private int? ReadSystemCpuPercent()
    {
        if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return null;
        }

        var currentIdle = idle.ToUInt64();
        var currentKernel = kernel.ToUInt64();
        var currentUser = user.ToUInt64();

        if (!_hasPreviousCpuSample)
        {
            _previousIdle = currentIdle;
            _previousKernel = currentKernel;
            _previousUser = currentUser;
            _hasPreviousCpuSample = true;
            return null;
        }

        var idleDelta = currentIdle - _previousIdle;
        var kernelDelta = currentKernel - _previousKernel;
        var userDelta = currentUser - _previousUser;
        var totalDelta = kernelDelta + userDelta;

        _previousIdle = currentIdle;
        _previousKernel = currentKernel;
        _previousUser = currentUser;

        if (totalDelta == 0)
        {
            return 0;
        }

        return (int)Math.Clamp(Math.Round((totalDelta - idleDelta) * 100d / totalDelta), 0, 100);
    }

    public void Dispose()
    {
        _gpuUsageSampler?.Dispose();
        try
        {
            _hardwareComputer?.Close();
        }
        catch
        {
            // 忽略硬件关闭异常。
        }

        _hardwareComputer = null;
        _currentProcess.Dispose();
    }

    private sealed class GpuUsageSampler : IDisposable
    {
        private const string CounterPath = @"\GPU Engine(*)\Utilization Percentage";
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;
        private static readonly int ItemSize =
            Marshal.SizeOf<NativeMethods.PdhFmtCounterValueItem>();

        private readonly Dictionary<EngineKey, double> _engineTotals = [];
        private nint _query;
        private nint _counter;
        private nint _buffer;
        private uint _bufferCapacity;
        private bool _hasBaseline;

        internal GpuUsageSampler()
        {
            if (NativeMethods.PdhOpenQuery(null, nint.Zero, out _query) !=
                NativeMethods.ErrorSuccess)
            {
                _query = nint.Zero;
                return;
            }

            if (NativeMethods.PdhAddEnglishCounter(
                    _query,
                    CounterPath,
                    nint.Zero,
                    out _counter) == NativeMethods.ErrorSuccess)
            {
                return;
            }

            _ = NativeMethods.PdhCloseQuery(_query);
            _query = nint.Zero;
            _counter = nint.Zero;
        }

        internal int? Sample()
        {
            if (_query == nint.Zero ||
                NativeMethods.PdhCollectQueryData(_query) != NativeMethods.ErrorSuccess)
            {
                return null;
            }

            if (!_hasBaseline)
            {
                _hasBaseline = true;
                return null;
            }

            uint bufferSize = 0;
            uint itemCount = 0;
            var status = NativeMethods.PdhGetFormattedCounterArray(
                _counter,
                NativeMethods.PdhFmtDouble,
                ref bufferSize,
                ref itemCount,
                nint.Zero);
            if (status != NativeMethods.PdhMoreData || bufferSize == 0 || itemCount == 0)
            {
                return null;
            }

            EnsureBufferCapacity(bufferSize);
            bufferSize = _bufferCapacity;
            status = NativeMethods.PdhGetFormattedCounterArray(
                _counter,
                NativeMethods.PdhFmtDouble,
                ref bufferSize,
                ref itemCount,
                _buffer);
            if (status != NativeMethods.ErrorSuccess)
            {
                return null;
            }

            _engineTotals.Clear();
            var count = checked((int)itemCount);
            for (var index = 0; index < count; index++)
            {
                var itemPointer = IntPtr.Add(_buffer, checked(index * ItemSize));
                var item = Marshal.PtrToStructure<NativeMethods.PdhFmtCounterValueItem>(
                    itemPointer);
                if (item.Value.Status is not NativeMethods.PdhStatusValidData and
                    not NativeMethods.PdhStatusNewData)
                {
                    continue;
                }

                var key = GetEngineKey(item.Name);
                _engineTotals.TryGetValue(key, out var total);
                _engineTotals[key] = total + Math.Max(0, item.Value.DoubleValue);
            }

            if (_engineTotals.Count == 0)
            {
                return null;
            }

            var busiestEngine = _engineTotals.Values.Max();
            return (int)Math.Clamp(Math.Round(busiestEngine), 0, 100);
        }

        private void EnsureBufferCapacity(uint requiredCapacity)
        {
            if (_buffer != nint.Zero && _bufferCapacity >= requiredCapacity)
            {
                return;
            }

            if (_buffer != nint.Zero)
            {
                Marshal.FreeHGlobal(_buffer);
            }

            _buffer = Marshal.AllocHGlobal(checked((int)requiredCapacity));
            _bufferCapacity = requiredCapacity;
        }

        private static EngineKey GetEngineKey(nint instanceName)
        {
            var start = FindLuidOffset(instanceName);
            var hash = FnvOffsetBasis;
            var length = 0;
            for (var offset = start; ; offset += sizeof(char))
            {
                var character = unchecked((char)(ushort)Marshal.ReadInt16(instanceName, offset));
                if (character == '\0')
                {
                    break;
                }

                if (character is >= 'A' and <= 'Z')
                {
                    character = (char)(character + ('a' - 'A'));
                }

                hash ^= character;
                hash *= FnvPrime;
                length++;
            }

            return new EngineKey(hash, length);
        }

        private static int FindLuidOffset(nint instanceName)
        {
            ReadOnlySpan<char> token = "luid_";
            for (var offset = 0; ; offset += sizeof(char))
            {
                var character = unchecked((char)(ushort)Marshal.ReadInt16(instanceName, offset));
                if (character == '\0')
                {
                    return 0;
                }

                var matches = true;
                for (var tokenIndex = 0; tokenIndex < token.Length; tokenIndex++)
                {
                    var candidate = unchecked((char)(ushort)Marshal.ReadInt16(
                        instanceName,
                        offset + tokenIndex * sizeof(char)));
                    if (candidate == '\0' ||
                        char.ToLowerInvariant(candidate) != token[tokenIndex])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return offset;
                }
            }
        }

        public void Dispose()
        {
            if (_buffer != nint.Zero)
            {
                Marshal.FreeHGlobal(_buffer);
                _buffer = nint.Zero;
                _bufferCapacity = 0;
            }

            if (_query != nint.Zero)
            {
                _ = NativeMethods.PdhCloseQuery(_query);
                _query = nint.Zero;
                _counter = nint.Zero;
            }
        }

        private readonly record struct EngineKey(ulong Hash, int Length);
    }
}
