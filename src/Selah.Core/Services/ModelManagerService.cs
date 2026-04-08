using System.Diagnostics;
using Selah.Core.Models;

namespace Selah.Core.Services;

/// <summary>
/// AI 모델 카탈로그 관리 및 설치/감지 서비스.
/// 모델 파일은 %AppData%\Selah\models\ 에 저장됩니다.
/// </summary>
public class ModelManagerService
{
    private readonly string _modelsDir;
    private List<ModelInfo> _catalog = new();

    public string ModelsDir => _modelsDir;

    public ModelManagerService()
    {
        _modelsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Selah", "models");
        Directory.CreateDirectory(_modelsDir);
        BuildBuiltinCatalog();
        RefreshInstallStatus();
    }

    public IReadOnlyList<ModelInfo> GetCatalog() => _catalog.AsReadOnly();

    public ModelInfo? GetById(string id) =>
        _catalog.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private void BuildBuiltinCatalog()
    {
        _catalog = new List<ModelInfo>
        {
            new()
            {
                Id = "htdemucs",
                Name = "Hybrid Transformer Demucs",
                Description = "Meta AI의 최신 Hybrid Transformer 음원 분리 모델.\n" +
                              "드럼·베이스·기타·보컬 4-stem 분리 지원.\n" +
                              "CPU 전용으로도 동작하나 시간이 걸릴 수 있습니다.",
                License = "MIT",
                LicenseUrl = "https://github.com/facebookresearch/demucs/blob/main/LICENSE",
                SourceUrl = "https://github.com/facebookresearch/demucs",
                Version = "4.0.1",
                ModelType = ModelType.StemSeparation,
                StemType = StemType.FourStem,
                Engine = ModelEngine.PythonCLI,
                SizeBytes = 80L * 1024 * 1024,
                SuitableForPublicWorship = true,
                WorshipNote = "MIT 라이선스로 자유롭게 사용 가능합니다.\n" +
                              "단, 처리 대상 음원(찬양곡)의 저작권은 별도로 확인하세요.\n" +
                              "CCL 또는 원저작자 허락 여부를 먼저 확인하십시오.",
                Tags = new List<string> { "추천", "4-stem", "고품질", "MIT" }
            },
            new()
            {
                Id = "htdemucs_ft",
                Name = "Hybrid Transformer Demucs (Fine-tuned)",
                Description = "htdemucs의 Fine-tuning 버전. 일부 장르에서 분리 품질 향상.\n" +
                              "용량이 4배 더 크고 처리 시간도 더 걸립니다.",
                License = "MIT",
                LicenseUrl = "https://github.com/facebookresearch/demucs/blob/main/LICENSE",
                SourceUrl = "https://github.com/facebookresearch/demucs",
                Version = "4.0.1",
                ModelType = ModelType.StemSeparation,
                StemType = StemType.FourStem,
                Engine = ModelEngine.PythonCLI,
                SizeBytes = 320L * 1024 * 1024,
                SuitableForPublicWorship = true,
                WorshipNote = "MIT 라이선스. 고품질 분리가 필요한 경우 권장.\n" +
                              "저사양 PC에서는 처리에 수 분이 걸릴 수 있습니다.",
                Tags = new List<string> { "4-stem", "고품질", "대용량", "MIT" }
            },
            new()
            {
                Id = "mdx_extra",
                Name = "MDX-Net Extra (2-stem)",
                Description = "MDX 아키텍처 기반 보컬/반주 2-stem 분리.\n" +
                              "빠른 처리가 필요한 저사양 환경에 적합합니다.",
                License = "MIT",
                LicenseUrl = "https://github.com/facebookresearch/demucs/blob/main/LICENSE",
                SourceUrl = "https://github.com/facebookresearch/demucs",
                Version = "4.0.1",
                ModelType = ModelType.VocalSeparation,
                StemType = StemType.TwoStem,
                Engine = ModelEngine.PythonCLI,
                SizeBytes = 60L * 1024 * 1024,
                SuitableForPublicWorship = true,
                WorshipNote = "MIT 라이선스. 빠른 2-stem 분리.\n" +
                              "소형 교회 PC 환경(저사양)에 권장합니다.",
                Tags = new List<string> { "2-stem", "빠름", "입문 추천", "MIT" }
            }
        };
    }

    public void RefreshInstallStatus()
    {
        bool demucsInstalled = CheckPythonPackage("demucs");
        foreach (var model in _catalog)
        {
            if (model.Engine == ModelEngine.PythonCLI)
            {
                model.IsInstalled = demucsInstalled;
                model.LocalPath = demucsInstalled ? _modelsDir : null;
            }
        }
    }

    private static bool CheckPythonPackage(string package)
    {
        try
        {
            var psi = new ProcessStartInfo("python", $"-c \"import {package}; print('ok')\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            return proc.ExitCode == 0 && output.Contains("ok");
        }
        catch { return false; }
    }

    /// <summary>pip install demucs 를 실행하여 Demucs를 설치합니다.</summary>
    public async Task InstallDemucsAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report("pip install demucs 실행 중...");

        var psi = new ProcessStartInfo("pip", "install demucs")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) progress?.Report(e.Data);
        };
        proc.Start();
        proc.BeginOutputReadLine();

        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            var stderr = await proc.StandardError.ReadToEndAsync(ct);
            throw new Exception($"Demucs 설치 실패 (code {proc.ExitCode}):\n{stderr}");
        }

        RefreshInstallStatus();
    }
}
