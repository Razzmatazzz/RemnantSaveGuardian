using System;
using System.Windows.Data;

namespace RemnantSaveGuardian.Helpers
{
    internal class CalculateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double intX = Math.Round((double)value);
            int intY = Int32.Parse((string)parameter);
            return (intX + intY);
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
