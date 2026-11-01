<#
.SYNOPSIS
Replaces the Configurator splash screen with a saved APOD, sized for WPF's SplashScreen

.DESCRIPTION
WPF shows the splash at its exact pixel size whatever the display scaling, and centres it using the
scaled screen size, so anything bigger than the scaled screen runs off the edge. Fitting inside
900x600 keeps it on screen down to 1366x768 at 100% and 1920x1080 at 175%.
Keeps the aspect ratio, never crops or upscales, writes a metadata free JPEG.

.EXAMPLE
.\scripts\Set-Splash.ps1
Today's saved picture from %AppData%\APODWallpaper\images

.EXAMPLE
.\scripts\Set-Splash.ps1 -Date 2026-10-31

.EXAMPLE
.\scripts\Set-Splash.ps1 -Path C:\Pictures\nebula.png
#>
[CmdletBinding(DefaultParameterSetName = 'Date')]
param(
    [Parameter(ParameterSetName = 'Date')]
    [datetime]$Date = [datetime]::Today,

    [Parameter(ParameterSetName = 'Path', Mandatory)]
    [string]$Path,

    [int]$MaxWidth = 900,
    [int]$MaxHeight = 600,

    [ValidateRange(1, 100)]
    [int]$Quality = 90
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$target = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\ConfiguratorGUI\Resources\Splash.jpg'))

if ($PSCmdlet.ParameterSetName -eq 'Date') {
    $images = Join-Path $env:APPDATA 'APODWallpaper\images'
    $name = $Date.ToString('yyyy-MM-dd')
    $Path = Get-ChildItem $images -Filter "$name.*" -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -notin '.json', '.part' } |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $Path) { throw "No saved picture for $name in $images, update the wallpaper first or pass -Path" }
}

$source = [System.Drawing.Image]::FromFile((Resolve-Path $Path).ProviderPath)
$bitmap = $null
$graphics = $null
$attributes = $null
$encoderParams = $null
try {
    # The output has no EXIF, so bake the camera rotation into the pixels
    $orientationTag = 0x0112
    if ($source.PropertyIdList -contains $orientationTag) {
        $flip = switch ($source.GetPropertyItem($orientationTag).Value[0]) {
            2 { 'RotateNoneFlipX' }
            3 { 'Rotate180FlipNone' }
            4 { 'Rotate180FlipX' }
            5 { 'Rotate90FlipX' }
            6 { 'Rotate90FlipNone' }
            7 { 'Rotate270FlipX' }
            8 { 'Rotate270FlipNone' }
            default { $null }
        }
        if ($flip) { $source.RotateFlip($flip) }
    }

    $scale = [Math]::Min(1.0, [Math]::Min($MaxWidth / $source.Width, $MaxHeight / $source.Height))
    $width = [Math]::Max(1, [int][Math]::Round($source.Width * $scale))
    $height = [Math]::Max(1, [int][Math]::Round($source.Height * $scale))

    $bitmap = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $bitmap.SetResolution(96, 96)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    # Transparent PNGs and GIFs end up on black, like the space around them
    $graphics.Clear([System.Drawing.Color]::Black)
    # Stops the edges blending with the transparent area outside the image
    $attributes = New-Object System.Drawing.Imaging.ImageAttributes
    $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
    $graphics.DrawImage($source, (New-Object System.Drawing.Rectangle 0, 0, $width, $height),
        0, 0, $source.Width, $source.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)

    $jpeg = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
    $encoderParams = New-Object System.Drawing.Imaging.EncoderParameters 1
    $encoderParams.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), ([long]$Quality)
    $bitmap.Save($target, $jpeg, $encoderParams)
}
finally {
    foreach ($disposable in $encoderParams, $attributes, $graphics, $bitmap, $source) {
        if ($disposable) { $disposable.Dispose() }
    }
}

$size = [Math]::Round((Get-Item $target).Length / 1KB)
Write-Host "Splash.jpg is now ${width}x${height} (${size} KB) from $Path"
