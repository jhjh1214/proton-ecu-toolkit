using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ProtonEcuToolkit.App.ViewModels;

namespace ProtonEcuToolkit.App.Views;

public sealed class ThemeToTemplateConverter : IValueConverter
{
    public DataTemplate? DialTemplate { get; set; }

    public DataTemplate? DigitalTemplate { get; set; }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is GaugeTheme.Digital ? DigitalTemplate : DialTemplate;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
