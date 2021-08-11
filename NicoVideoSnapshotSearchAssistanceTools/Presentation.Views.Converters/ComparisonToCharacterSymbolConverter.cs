using NiconicoToolkit.SnapshotSearch.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.Views.Converters
{
    public sealed class ComparisonToCharacterSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is SimpleFilterComparison comparison)
            {
                return comparison switch
                {
                    SimpleFilterComparison.Equal => "=",
                    SimpleFilterComparison.GreaterThan => ">",
                    SimpleFilterComparison.GreaterThanOrEqual => ">=",
                    SimpleFilterComparison.LessThan => "<",
                    SimpleFilterComparison.LessThenOrEqual => "<=",
                    _ => throw new NotSupportedException(),
                };
            }

            throw new NotSupportedException(value?.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
