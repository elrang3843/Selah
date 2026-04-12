using Microsoft.Win32;
using System.Windows;

namespace Selah.App.Services;

/// <summary>
/// Windows 시스템 테마(다크/라이트) 변경을 감지하고 앱 리소스를 교체합니다.
/// </summary>
public static class SystemThemeService
{
    /// <summary>테마가 바뀔 때 발생합니다. bool = isDark</summary>
    public static event Action<bool>? ThemeChanged;

    public static bool IsDarkTheme => ReadIsDark();

    // ── 초기화 / 종료 ──

    public static void Initialize()
    {
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        Apply(ReadIsDark());
    }

    public static void Shutdown()
    {
        SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
    }

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

        ThemeChanged?.Invoke(isDark);
    }

    // ── 내부 ──

    private static bool ReadIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            // AppsUseLightTheme: 0 = dark, 1 = light
            return key?.GetValue("AppsUseLightTheme") is not int v || v == 0;
        }
        catch
        {
            return true; // 레지스트리 읽기 실패 시 다크 기본
        }
    }

    private static void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        bool dark = ReadIsDark();
        Application.Current?.Dispatcher.BeginInvoke(() => Apply(dark));
    }
}
