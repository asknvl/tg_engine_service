using Avalonia.Data.Converters;
using Avalonia.Media;
using logger;
using System;
using System.Globalization;


namespace tg_engine_launcher.Views.converters
{
    public class LogMessageTypeToBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                LogMessageType.inf => new SolidColorBrush(0x00000000),
                LogMessageType.inf_urgent => new SolidColorBrush(0xFF3CB371),
                LogMessageType.err => new SolidColorBrush(0xFFCD5C5C),
                LogMessageType.warn => new SolidColorBrush(0xFFFFFF00),
                LogMessageType.dbg => new SolidColorBrush(0xFFC0C0C0),
                LogMessageType.user_input => new SolidColorBrush(0xFFD2691E),

                _ => new SolidColorBrush(0xFF000000)
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
