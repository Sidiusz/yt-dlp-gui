using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Grabsy.Services;

/// <summary>Applies the configured theme to every registered window root.</summary>
public static class ThemeService
{
    private static readonly List<FrameworkElement> _roots = new();

    public static void Register(FrameworkElement? root)
    {
        if (root == null || _roots.Contains(root)) return;
        _roots.Add(root);
        Apply(root);
        root.Unloaded += (_, _) => _roots.Remove(root);
    }

    public static ElementTheme Current => SettingsService.Instance.Settings.Theme switch
    {
        "dark" => ElementTheme.Dark,
        "light" => ElementTheme.Light,
        _ => ElementTheme.Default
    };

    public static void Apply(FrameworkElement root) => root.RequestedTheme = Current;

    public static void ApplyAll()
    {
        foreach (var r in _roots) Apply(r);
    }

    private static ElementTheme ResolveTheme(string s) => s switch
    {
        "dark" => ElementTheme.Dark,
        "light" => ElementTheme.Light,
        _ => ElementTheme.Default
    };

    /// <summary>Resolve a themed brush by key for the current/effective theme.</summary>
    public static Brush GetBrush(string key, FrameworkElement? context = null)
    {
        var theme = context?.ActualTheme ?? ResolveTheme(SettingsService.Instance.Settings.Theme);
        if (theme == ElementTheme.Default)
            theme = Application.Current.RequestedTheme == ApplicationTheme.Light ? ElementTheme.Light : ElementTheme.Dark;
        var dictKey = theme == ElementTheme.Light ? "Light" : "Default";
        if (TryGetThemed(Application.Current.Resources, dictKey, key, out var v) && v is Brush b)
            return b;
        return (Brush)Application.Current.Resources[key];
    }

    private static bool TryGetThemed(ResourceDictionary rd, string dictKey, string key, out object? value)
    {
        value = null;
        if (rd.ThemeDictionaries.TryGetValue(dictKey, out var td)
            && td is ResourceDictionary themed
            && themed.TryGetValue(key, out value))
            return true;
        return false;
    }
}
