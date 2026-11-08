#nullable enable annotations

using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace TypeDitor.View.Panels
{
    /// <summary>
    /// Converts a boolean indicating validation error state to a background color (red if error, transparent if no error).
    /// </summary>
    public class PropertyErrorBackgroundConverter : IValueConverter
    {
        /// <summary>Gets the singleton instance of the converter.</summary>
        public static readonly PropertyErrorBackgroundConverter Instance = new();

        /// <summary>Converts a boolean validation error state to a background brush.</summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            if (value is bool hasError && hasError)
            {
                return new SolidColorBrush(Color.FromRgb(255, 100, 100));
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        /// <summary>Not implemented.</summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
        {
            throw new NotImplementedException();
        }
    }
}
