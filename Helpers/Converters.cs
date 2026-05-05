using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VaultApp.Views
{
    /// <summary>Returns Visible when string is null/empty (placeholder), Collapsed otherwise.</summary>
    public class EmptyStringToVisibility : IValueConverter
    {
        public static readonly EmptyStringToVisibility Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
