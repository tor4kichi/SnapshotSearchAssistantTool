using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.Views.Converters
{
    public sealed class HumanReadbleDateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
            {
                return "";
            }
            else if (value is DateTimeOffset dateTimeOffset)
            {
                if (parameter is string format)
                {
                    return dateTimeOffset.ToString(format);
                }
                else
                {
                    return dateTimeOffset.ToString("g");
                }
            }
            else if (value is DateTime dateTime)
            {
                if (parameter is string format)
                {
                    return dateTime.ToString(format);
                }
                else
                {
                    return dateTime.ToString("g");
                }
            }
            else if (value is TimeSpan timeSpan)
            {
                if (parameter is string format)
                {
                    return timeSpan.ToString(format);
                }
                else
                {
                    if (timeSpan.Hours > 0)
                    {
                        return timeSpan.ToString("hh:mm:ss");
                    }
                    else
                    {
                        return timeSpan.ToString("mm:ss");
                    }
                }
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
