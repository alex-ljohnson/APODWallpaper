# What's Changed since v2026.07.28.1

## Please update - NASA has moved APOD

In September NASA moved the Astronomy Picture of the Day to science.nasa.gov. The old API that earlier
versions use has stopped working and shuts down for good on 1 December 2026, so v2026.07.28.1 and older
can no longer update your wallpaper. This version uses NASA's new API.

- Fixes the NASA logo being set as your wallpaper instead of the picture of the day. Since early October
  the old API has returned the logo in place of the picture. Logos saved on those days are deleted from
  your saved pictures automatically, and you can download the real pictures again from Explore.
- You can update with the Configurator's "Check for update..." button, or run the installer below.
- Your saved pictures are kept. The old cache of picture details is cleared and rebuilt as you browse.

## What's new

### Full quality pictures
- Downloads the original image NASA published instead of a resized copy whenever it can. Recent pictures
  take up 3-5x more disk space as a result.
- Every download is checked to make sure it's a real, complete image before it's used. If the original
  can't be used, NASA's standard copy is used instead.
- Saved pictures now have the right file extension (`.jpg`, `.png`, `.gif`, `.tif`). Your existing pictures
  are renamed automatically the first time the new version starts, and your current wallpaper is moved to
  the new name.

### Video days
- Recent video days come with a still image from NASA, and the still now becomes your wallpaper. To leave
  your wallpaper as it is on video days, untick "Use a video's still as the wallpaper" in Settings.
- Explore shows a still or a title tile for video days.

### NASA API key
- The shared key older versions came with is gone. Every install used it, so it kept running out. It's
  removed from your settings automatically.
- NASA's new API doesn't check keys at the moment, so the app works out of the box using NASA's
  `DEMO_KEY`. You can add your own free key from [api.nasa.gov](https://api.nasa.gov/) in the installer,
  in the Configurator under Settings > API Key > Set up..., or with `APODWallpaper.exe --set-key YOUR_KEY`.
- If NASA starts requiring keys and yours is missing or invalid, the app tells you and offers to set one up.

### More reliable updates
- Temporary errors from NASA's servers, which happen often, are retried automatically.
- NASA publishes the new picture at 00:05 US Eastern time. If it isn't out yet, the app sets the latest
  picture and picks up the new one on its next run.
- The network timeout setting now also covers downloading the image, so a stalled download no longer hangs.

### Explore
- Previews load smaller, faster copies of each picture, with the full image used if that fails.
- Hover over a preview to read NASA's description of the picture (mostly posts from 2021 on). Screen
  readers read it too.
- "View content" now asks before opening your browser.

## Under the hood
- Settings that only existed for the old API (`BaseUrl`, `UseHD`) are removed from your config.
- 188 unit tests covering NASA's responses, downloads, API keys and the file migration.
