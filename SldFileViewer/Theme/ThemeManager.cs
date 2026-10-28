using System;
using System.Windows;

namespace SldFileViewer.Theme;

/// <summary>
/// Controls application theme resources and styles.
/// </summary>
public static class ThemeManager
{
    public static void ApplyTheme()
    {
        var app = Application.Current;
        if (app == null) return;

        var themeUri = new Uri("pack://application:,,,/SldFileViewer;component/Theme/Theme.xaml", UriKind.Absolute);
        var themeDict = new ResourceDictionary { Source = themeUri };

        app.Resources.MergedDictionaries.Add(themeDict);
    }
}
