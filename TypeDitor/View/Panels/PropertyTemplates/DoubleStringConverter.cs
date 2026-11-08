#nullable enable

using Avalonia.Data.Converters;
using System.Globalization;

namespace TypeDitor.View.Panels.PropertyTemplates
{
    /// <summary>
    /// Converts between double values and their string representations, preserving decimal precision.
    /// </summary>
    public class DoubleStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                return doubleValue.ToString("G15", CultureInfo.InvariantCulture);
            }

            return value?.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                if (string.IsNullOrEmpty(stringValue))
                    return 0.0;

                if (double.TryParse(stringValue, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double result))
                    return result;
            }

            return 0.0;
        }
    }
}
