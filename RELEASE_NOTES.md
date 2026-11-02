# What's Changed since v2026.10.31.1

Updating from v2026.07.28.1 or older? NASA has moved APOD and older versions can no longer update your
wallpaper. See the [v2026.10.31.1 notes](https://github.com/alex-ljohnson/APODWallpaper/releases/tag/v2026.10.31.1)
for everything that changed, this version includes all of it.

## Fixes
- Turning off "Update the wallpaper on startup" now sticks. Opening the Configurator or running an update
  used to turn it back on.
- Applying a new fill type now changes your current wallpaper straight away instead of on the next update.
- "Save Image..." and "View Content..." in Explore's right-click menu now work.
- The Configurator no longer crashes when started from a shortcut or folder other than where it's
  installed, and custom themes no longer reset to Light with an error when it's started that way.
- "Check for update..." tells you when it can't reach GitHub instead of closing the Configurator.
- If saving a picture from Explore fails, it goes back into Explore so you can try again.
- The saved pictures count and size update as you save and delete pictures.
- Reset to defaults updates the settings shown, the theme and the startup setting straight away.
- The splash screen is centred on displays with scaling turned on.

## Configurator
- Settings are reorganised into Wallpaper, NASA connection, Appearance and Tools, with clearer labels.
- Themes apply as soon as you pick one, there's no Update button any more.
- The window can be resized down to a minimum size, and maximising it no longer covers the taskbar.
- The maximise button shows the right icon after snapping the window or double clicking the title bar, and
  is visible on the Light theme.

## Under the hood
- 194 unit tests.
