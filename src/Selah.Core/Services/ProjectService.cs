using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Selah.Core.Models;

namespace Selah.Core.Services;

/// <summary>
/// 프로젝트 저장/불러오기 서비스.
/// 프로젝트 구조:
///   MyProject/
///     project.json.gz   ← 압축된 JSON 매니페스트
///     audio/            ← 컨폼된 WAV 캐시
///     peaks/            ← 파형 미리보기 데이터
///     recordings/       ← 녹음 파일
/// </summary>
public class ProjectService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public Project NewProject(string name, int sampleRate)
    {
        var project = new Project
        {
            Name = name,
            SampleRate = sampleRate
        };
        return project;
    }

    public async Task SaveAsync(Project project, string projectDir)
    {
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(projectDir, "audio"));
        Directory.CreateDirectory(Path.Combine(projectDir, "peaks"));
        Directory.CreateDirectory(Path.Combine(projectDir, "recordings"));

        project.ModifiedAt = DateTime.UtcNow;
        project.FilePath = projectDir;

        var json = JsonSerializer.Serialize(project, JsonOptions);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        var manifestPath = Path.Combine(projectDir, "project.json.gz");
        await using var fs = new FileStream(manifestPath, FileMode.Create, FileAccess.Write);
        await using var gz = new GZipStream(fs, CompressionLevel.Optimal);
        await gz.WriteAsync(bytes);

        project.IsDirty = false;
    }

    public async Task<Project> LoadAsync(string projectDir)
    {
        var manifestPath = Path.Combine(projectDir, "project.json.gz");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("프로젝트 파일을 찾을 수 없습니다.", manifestPath);

        await using var fs = new FileStream(manifestPath, FileMode.Open, FileAccess.Read);
        await using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var ms = new MemoryStream();
        await gz.CopyToAsync(ms);

        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        var project = JsonSerializer.Deserialize<Project>(json, JsonOptions)
            ?? throw new InvalidDataException("프로젝트 파일을 파싱할 수 없습니다.");

        project.FilePath = projectDir;
        project.IsDirty = false;

        // 오디오 소스 절대 경로 복원
        foreach (var source in project.AudioSources)
            source.AbsolutePath = Path.Combine(projectDir, source.RelPath);

        return project;
    }

    /// <summary>프로젝트 폴더를 단일 ZIP으로 패키징 (USB 공유용)</summary>
    public async Task PackageAsync(string projectDir, string outputZipPath)
    {
        await Task.Run(() =>
        {
            if (File.Exists(outputZipPath)) File.Delete(outputZipPath);
            ZipFile.CreateFromDirectory(
                projectDir, outputZipPath,
                CompressionLevel.Optimal,
                includeBaseDirectory: false);
        });
    }

    /// <summary>패키징된 ZIP을 지정 폴더에 압축 해제</summary>
    public async Task<string> UnpackageAsync(string zipPath, string targetDir)
    {
        await Task.Run(() =>
        {
            Directory.CreateDirectory(targetDir);
            ZipFile.ExtractToDirectory(zipPath, targetDir, overwriteFiles: true);
        });
        return targetDir;
    }
}
