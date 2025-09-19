using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PackageConsole.Helpers
{
    public class RouteSelectedBrushConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var currentRoute = values.Length > 0 ? values[0]?.ToString() : null;
                var tag = values.Length > 1 ? values[1]?.ToString() : null;
                bool isSelected = !string.IsNullOrEmpty(currentRoute) && string.Equals(currentRoute, tag, StringComparison.OrdinalIgnoreCase);

                // Get brushes from app resources with reasonable fallbacks
                var res = Application.Current.Resources;
                var normal = (res["SidebarButtonBackground"] as Brush) ?? new SolidColorBrush(Color.FromRgb(0xFF, 0x57, 0x33));
                var selected = (res["SidebarButtonSelectedBackground"] as Brush) ?? new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07));

                return isSelected ? selected : normal;
            }
            catch
            {
                return new SolidColorBrush(Color.FromRgb(0xFF, 0x57, 0x33));
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

