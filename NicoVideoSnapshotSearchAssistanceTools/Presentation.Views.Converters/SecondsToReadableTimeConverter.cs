using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.Views.Converters
{
    public sealed class SecondsToReadableTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            TimeSpan? time = null;
            if (value is int intVal)
            {
                time = TimeSpan.FromSeconds(intVal);
            }
            else if (value is long longVal)
            {
                time = TimeSpan.FromSeconds(longVal);
            }

            if (time is not null and TimeSpan timeSpan)
            {
                if (parameter is string format)
                {
                    return timeSpan.ToString(format);
                }
                else
                {
                    if (timeSpan.Hours > 0)
                    {
                        return timeSpan.ToString(@"hh\:mm\:ss");
                    }
                    else
                    {
                        return timeSpan.ToString(@"mm\:ss");
                    }
                }
            }

            throw new NotSupportedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
