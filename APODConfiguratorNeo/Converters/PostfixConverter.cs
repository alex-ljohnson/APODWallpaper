using Microsoft.UI.Xaml.Data;

namespace APODConfiguratorNeo.Converters
{
    internal class PostfixConverter : IValueConverter
    {
        public object ConvertBack(object value, Type targetType, object parameter, string culture)
        {
            if (parameter is not string par) { return value; }
            if (value is string val)
            {
                return val + par;
            }
            else if (value is ICollection<string> collection)
            {
                return collection.Select(x => x + par);
            }
            else return value;
        }

        public object Convert(object value, Type targetType, object parameter, string culture)
        {
            if (parameter is not string par) { return value; }
            if (value is string val)
            {
                return val[..val.IndexOf(par)];
            }
            else if (value is ICollection<string> collection)
            {
                return collection.Select(x => x[..x.IndexOf(par)]);
            }
            else return value;

        }
    }
}
