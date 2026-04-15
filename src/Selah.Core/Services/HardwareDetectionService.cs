using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Selah.Core.Services;

/// <summary>
/// 하드웨어 가속 가능 여부 감지 서비스.
/// 우선순위: NVIDIA CUDA > DirectML (AMD/Intel GPU) > CPU
/// </summary>
public class HardwareDetectionService
{
    public HardwareInfo Detect()
    {
        var info = new HardwareInfo
        {
            CpuName = GetCpuName(),
            LogicalCores = Environment.ProcessorCount,
            AvailableRamMb = GetAvailableRamMb()
        };

        if (OperatingSystem.IsWindows())
        {
            info.HasNvidiaGpu = CheckNvidiaGpu(out string? driverVer);
            info.NvidiaDriverVersion = driverVer;
            info.HasDirectMl = CheckDirectMl();
        }

        info.RecommendedBackend = DetermineBackend(info);
        return info;
    }

    private static string GetCpuName()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var psi = new ProcessStartInfo("wmic", "cpu get name /value")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return "알 수 없음";
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);
                var line = output.Split('\n')
                                 .FirstOrDefault(l => l.StartsWith("Name=", StringComparison.OrdinalIgnoreCase));
                return line?.Split('=', 2).LastOrDefault()?.Trim() ?? "알 수 없음";
            }
        }
        catch { }
        return RuntimeInformation.ProcessArchitecture.ToString();
    }

    private static bool CheckNvidiaGpu(out string? driverVersion)
    {
        driverVersion = null;
        try
        {
            var psi = new ProcessStartInfo("nvidia-smi",
                "--query-gpu=driver_version --format=csv,noheader")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);
            if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                driverVersion = output;
                return true;
            }
        }
        catch { }
        return false;
    }

    private static bool CheckDirectMl()
    {
        // DirectML.dll가 시스템에 있으면 사용 가능
        try
        {
            var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (File.Exists(Path.Combine(system32, "DirectML.dll"))) return true;

            // Windows 11 / 최신 Windows 10 에는 기본 포함
            var version = Environment.OSVersion.Version;
            return version.Major >= 10 && version.Build >= 17763; // Windows 10 1809+
        }
        catch { return false; }
    }

    private static long GetAvailableRamMb()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var counter = new PerformanceCounter("Memory", "Available MBytes", true);
                return (long)counter.NextValue();
            }
        }
        catch { }
        return 0;
    }

    private static AccelerationBackend DetermineBackend(HardwareInfo info)
    {
        if (info.HasNvidiaGpu) return AccelerationBackend.Cuda;
        if (info.HasDirectMl) return AccelerationBackend.DirectMl;
        return AccelerationBackend.Cpu;
    }
}

public class HardwareInfo
{
    public string CpuName { get; set; } = string.Empty;
    public int LogicalCores { get; set; }
    public bool HasNvidiaGpu { get; set; }
    public string? NvidiaDriverVersion { get; set; }
    public bool HasDirectMl { get; set; }
    public long AvailableRamMb { get; set; }
    public AccelerationBackend RecommendedBackend { get; set; }

    public string BackendDisplayName => RecommendedBackend switch
    {
        AccelerationBackend.Cuda => $"NVIDIA CUDA (드라이버 {NvidiaDriverVersion})",
        AccelerationBackend.DirectMl => "DirectML (GPU 가속)",
        AccelerationBackend.Cpu => "CPU (소프트웨어 처리)",
        _ => "알 수 없음"
    };

    public string Summary =>
        $"{CpuName} / {LogicalCores}코어 / RAM {AvailableRamMb} MB 여유 / {BackendDisplayName}";
}

public enum AccelerationBackend
{
    Cpu,
    DirectMl,
    Cuda
}
