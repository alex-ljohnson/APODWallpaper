# APODWallpaper
## What is this?
This is a program to download the "Astronomy Picture Of the Day" (APOD) from the NASA website and set it as your desktop wallpaper. When installed the program will run on startup,

## Installation
### Dependecies
- [Dotnet 10 desktop runtime](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-10.0.2-windows-x64-installer)
### Recommended
> Download all dependencies listed above

> Download an installer from the [releases page](https://github.com/alex-ljohnson/APODWallpaper/releases).

### From Source
> Can be compiled from source using [dotnet 10 sdk](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-10.0.102-windows-x64-installer).

## Usage
### First Run
#### API Key
Pictures come from NASA's APOD API. NASA doesn't check API keys at the moment, so without one APODWallpaper uses NASA's `DEMO_KEY` and works out of the box.

NASA may start requiring keys, so adding your own free key from [api.nasa.gov](https://api.nasa.gov/) now avoids interruptions. It takes about a minute. If your key is ever missing, invalid or rate limited you'll be told so and offered the key setup.

You can enter your key:
- in the installer, on the "NASA API key" page
- in the Configurator, under Settings > API Key > Set up... (it checks the key with NASA before saving)
- from the command line: `APODWallpaper.exe --set-key YOUR_KEY` (pass an empty key to go back to `DEMO_KEY`)

Older versions shipped a shared key that every install used, so it kept hitting its limit. It is no longer used, and is cleared from existing settings automatically.
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
