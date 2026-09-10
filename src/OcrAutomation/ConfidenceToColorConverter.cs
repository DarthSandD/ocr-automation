using System.Windows.Data;
using System.Windows.Media;

namespace OcrAutomation;

public class ConfidenceToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is double d)
        {
            if (d >= 0.85) return Brushes.DarkGreen;
            if (d >= 0.6) return Brushes.DarkOrange;
            return Brushes.DarkRed;
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
