# APODWallpaper
## What is this?
This is a program to download the "Astronomy Picture Of the Day" (APOD) from the NASA website and set it as your desktop wallpaper. When installed the program will run on startup,

## Installation
### Dependecies
- [Dotnet 8 desktop runtime](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.0-windows-x64-installer)
### Recommended
> Download all dependencies listed above

> Download an installer from the [releases page](https://github.com/MrTransparentBox/APODWallpaper/releases).

### From Source
> Can be run from source using [dotnet 8.0 sdk](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-8.0.100-windows-x64-installer).

## Customisation
### Custom themes
Custom themes can be created:

- Custom WPF resource dictionaries can be placed into the Styles folder at your install location.
- Available colour keys are: `Light`, `Mid`, `Dark`, `Selected`, `MouseOver`, `Foreground`.
- Default themes use `SolidColourBrush`.
- Below is the default dark theme file.

**Example theme file - dark.xaml:**
```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <SolidColorBrush x:Key="Light" Color="#3D3D4C"/>
    <SolidColorBrush x:Key="Mid" Color="#32323F"/>
    <SolidColorBrush x:Key="Dark" Color="#272730"/>
    <SolidColorBrush x:Key="Selected" Color="#4C4F62"/>
    <SolidColorBrush x:Key="MouseOver" Color="#5C5F74"/>
    <SolidColorBrush x:Key="Foreground" Color="#F0F0F0"/>

</ResourceDictionary>
```
