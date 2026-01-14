# APODWallpaper
## What is this?
This is a program to download the "Astronomy Picture Of the Day" (APOD) from the NASA website and set it as your desktop wallpaper. When installed the program will run on startup,

## Installation
### Dependecies
- [Dotnet 10 desktop runtime](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-10.0.2-windows-x64-installer)
### Recommended
> Download all dependencies listed above

> Download an installer from the [releases page](https://github.com/MrTransparentBox/APODWallpaper/releases).

### From Source
> Can be compiled from source using [dotnet 10 sdk](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-10.0.102-windows-x64-installer).

## Usage
### First Run
#### API Key
As of `v2025.10.20`, an API key is required to access the NASA APOD API. While you can use `DEMO_KEY`, it is rate limited and may not work if the limit from your IP is exceeded - the limit is quite low!  
It is recommended that you sign up for your own free API key from NASA.  
Alternatively, you can continue using the previous key (set by default), but this may also be rate limited if many other users are using it.

On first run the program will prompt you to enter your NASA API key. You can get a free API key from [here (nasa.gov)](https://api.nasa.gov/).
### Settings
The settings can be accessed either from the Configurator GUI (if installed) or by editing the `config.json` file in the data directory `%appdata%/APODWallpaper/config.json`.

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
