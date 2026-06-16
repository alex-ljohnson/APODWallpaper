## What's new

### Signed installer and application

APODWallpaper is now distributed as a **digitally signed** installer, and the
application binaries inside it (`APODWallpaper.exe` and the Configurator) are
signed as well.

In practice this means Windows can now verify the publisher: the SmartScreen
"unknown publisher" warning no longer appears, and you can confirm the installer
hasn't been tampered with from its **Digital Signatures** tab (right-click the
`.exe` → Properties). No action is required on your part — just download and run
the installer as usual.

## Other
Builds are now produced by an automated, reproducible release pipeline, so the
version you download always matches the published release exactly.
