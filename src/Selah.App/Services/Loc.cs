using System.Windows;

namespace Selah.App.Services;

/// <summary>
/// 런타임 언어 전환을 지원하는 지역화 서비스.
/// App.xaml에 병합된 언어 ResourceDictionary에서 문자열을 읽습니다.
/// </summary>
public static class Loc
{
    private static string _currentLang = "ko";

    public static string CurrentLanguage => _currentLang;

    /// <summary>언어가 전환되었을 때 발생합니다. ViewModel 재알림에 사용합니다.</summary>
    public static event Action? LanguageChanged;

    /// <summary>
    /// 언어를 전환합니다. App.Resources의 언어 사전을 교체하여
    /// 모든 {DynamicResource} 바인딩이 즉시 갱신됩니다.
    /// </summary>
    public static void SetLanguage(string langCode)
    {
        if (_currentLang == langCode) return;

        var uri = new Uri(
            $"pack://application:,,,/Resources/Lang/{langCode}.xaml",
            UriKind.Absolute);
        var newDict = new ResourceDictionary { Source = uri };

        var merged = Application.Current.Resources.MergedDictionaries;
        var old = merged.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("/Resources/Lang/") == true);
        if (old != null) merged.Remove(old);
        merged.Add(newDict);

        _currentLang = langCode;
        LanguageChanged?.Invoke();
    }

    /// <summary>현재 언어 리소스에서 문자열을 가져옵니다. 키가 없으면 키 자체를 반환합니다.</summary>
    public static string Get(string key)
        => Application.Current?.Resources[key] as string ?? key;

    /// <summary>형식 문자열을 가져와 string.Format을 적용합니다.</summary>
    public static string Format(string key, params object[] args)
    {
        var template = Get(key);
        try { return string.Format(template, args); }
        catch { return template; }
    }
}
