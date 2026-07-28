using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace APODBenchmarks.Wpf;

public enum TemplateKind { Synthetic, Image }
public enum PanelKind { Wrap, VirtualizingWrap }

public static class Templates
{
    private const string Ns =
        "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
        "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
        "xmlns:b='clr-namespace:APODBenchmarks.Wpf;assembly=APODBenchmarks' " +
        "xmlns:c='clr-namespace:ConfiguratorGUI.Controls;assembly=ConfiguratorGUI'";

    public static DataTemplate BuildItemTemplate(TemplateKind kind)
    {
        string inner = kind == TemplateKind.Image
            ? @"<Image Stretch='UniformToFill' Width='220' Height='160'>
                  <Image.Source>
                    <Binding Path='Seed'>
                      <Binding.Converter><b:SyntheticImageConverter/></Binding.Converter>
                    </Binding>
                  </Image.Source>
                </Image>"
            : "";

        string xaml =
            $@"<DataTemplate {Ns}>
                 <Border Width='220' Height='240' Margin='5'>
                   <StackPanel>
                     <TextBlock Text='{{Binding Title}}' FontWeight='Bold'/>
                     <TextBlock Text='{{Binding Subtitle}}'/>
                     <TextBlock Text='{{Binding Body}}' TextWrapping='Wrap'/>
                     {inner}
                   </StackPanel>
                 </Border>
               </DataTemplate>";
        return (DataTemplate)XamlReader.Parse(xaml);
    }

    public static ItemsPanelTemplate BuildPanelTemplate(PanelKind kind)
    {
        string panel = kind == PanelKind.VirtualizingWrap
            ? "<c:VirtualizingWrapPanel ItemWidth='230' ItemHeight='250'/>"
            : "<WrapPanel Orientation='Horizontal'/>";
        string xaml = $"<ItemsPanelTemplate {Ns}>{panel}</ItemsPanelTemplate>";
        return (ItemsPanelTemplate)XamlReader.Parse(xaml);
    }
}
