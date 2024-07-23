using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APODConfiguratorNeo.Converters
{

    internal class LongConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var v = (long)value;
            return (int)v;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var v = (int)value;
            return (long)v;
        }
    }
}
