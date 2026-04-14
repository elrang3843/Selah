using System.Windows;

namespace Selah.App.Services;

/// <summary>
/// 앱 테마(다크/라이트) 전환을 관리합니다.
/// </summary>
public static class SystemThemeService
{
    /// <summary>테마가 바뀔 때 발생합니다. bool = isDark</summary>
    public static event Action<bool>? ThemeChanged;

    /// <summary>현재 테마가 다크이면 true.</summary>
    public static bool IsDarkTheme { get; private set; } = true;

    // ── 초기화 / 종료 ──

    public static void Initialize()
    {
        // 다크 테마를 기본값으로 적용합니다.
        // OS 테마 자동 감지는 사용하지 않습니다 — 사용자가 메뉴에서 직접 전환합니다.
        Apply(true);
    }

    public static void Shutdown() { }

    // ── 테마 적용 ──

    public static void Apply(bool isDark)
    {
        var uri = new Uri(
            isDark ? "pack://application:,,,/Resources/Themes/Dark.xaml"
                   : "pack://application:,,,/Resources/Themes/Light.xaml",
            UriKind.Absolute);

        var merged = Application.Current.Resources.MergedDictionaries;
        var old = merged.FirstOrDefault(
            d => d.Source?.OriginalString.Contains("/Resources/Themes/") == true);

        var newDict = new ResourceDictionary { Source = uri };

        // 원자적 교체: 먼저 추가한 뒤 이전 것 제거 → 잠깐이라도 키 누락 없음
        merged.Add(newDict);
        if (old != null) merged.Remove(old);

        IsDarkTheme = isDark;
        ThemeChanged?.Invoke(isDark);
    }
}
