using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Fontloom.Desktop.Converters;

public sealed class NumericFontWeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var numericWeight = value switch
        {
            int intValue => intValue,
            double doubleValue => (int)Math.Round(doubleValue),
            string stringValue when int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 400
        };

        return (FontWeight)Math.Clamp(numericWeight, 1, 1000);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FontWeight fontWeight)
        {
            return (int)fontWeight;
        }

        return 400;
    }
}
