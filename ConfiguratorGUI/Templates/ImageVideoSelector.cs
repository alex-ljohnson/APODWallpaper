using APODWallpaper.Utils;
using System.Windows;
using System.Windows.Controls;

namespace ConfiguratorGUI.Templates
{
    internal class ImageVideoSelector : DataTemplateSelector
    {
        public required DataTemplate ImageTemplate { get; set; }
        public required DataTemplate VideoTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is APODInfo apodInfo)
            {
                return apodInfo.MediaType?.StartsWith("image") == true ? ImageTemplate : VideoTemplate;
            }

            return base.SelectTemplate(item, container);
            //string filePath = item as string;
            //if (string.IsNullOrEmpty(filePath)) return null;

            //string ext = System.IO.Path.GetExtension(filePath).ToLower();
            //string[] imageExtensions = { ".jpg", ".png", ".bmp", ".gif" };

            //return imageExtensions.Contains(ext) ? ImageTemplate : VideoTemplate;
        }
    }
}
