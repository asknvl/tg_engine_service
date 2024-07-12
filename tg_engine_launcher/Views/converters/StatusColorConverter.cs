using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using tg_engine.dm;

namespace tg_engine_launcher.Views.converters
{
    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            var status = (DMHandlerStatus)value;
            switch (status)
            {
                case DMHandlerStatus.inactive:
                    return Brushes.Red;
                case DMHandlerStatus.verification:
                    return Brushes.LightGreen;
                case DMHandlerStatus.active:
                    return Brushes.Green;
                case DMHandlerStatus.revoked:
                    return Brushes.Yellow;
                case DMHandlerStatus.banned:
                    return Brushes.Black;                
                default:
                    return Brushes.Transparent;
            }  
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
