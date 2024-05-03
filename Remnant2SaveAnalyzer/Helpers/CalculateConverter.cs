using System;
using System.Diagnostics;
using System.Windows.Data;

namespace Remnant2SaveAnalyzer.Helpers
{
    internal class CalculateConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            Debug.Assert(value != null, nameof(value) + " != null");
            Debug.Assert(parameter != null, nameof(parameter) + " != null");

            double intX = Math.Round((double)value);
            int intY = int.Parse((string)parameter);
            return (intX + intY);
        }
        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
