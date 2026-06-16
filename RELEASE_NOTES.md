# What's Changed since v2026.01.14.1

## What's new

### Smoother, faster image history

Browsing your saved images and the explore view is now far more responsive. The
grid only renders the items currently on screen (true UI virtualization) and
decodes images off the UI thread, so scrolling through a large history no longer
stutters or freezes.

### More reliable "picture of the day"

Two long-standing date bugs have been fixed:

- The "today" check now uses NASA's release timezone (US Eastern) instead of
  UTC, so the latest image is picked up correctly rather than appearing a day
  early or late.
- Image dates are now read and written in a fixed, locale-independent format,
  so the app works correctly regardless of your system's regional settings.

### Automatic maintenance and self-repair

- On startup the app migrates older image and metadata files to a consistent
  ISO date naming scheme (`yyyy-MM-dd`). This happens once, automatically, with
  no action needed.
- Missing or mismatched image/metadata pairs are now detected and repaired when
  your history loads, making the gallery more robust against partial downloads.
- Cached metadata is validated before use, so incomplete or corrupt cache
  entries are refreshed instead of causing errors.

### Configurable network timeout

The network timeout you set in the configurator is now applied to each request,
giving you proper control over how long the app waits on slow connections.

## Other

- Builds are now produced by an automated, reproducible release pipeline (GitHub
  Actions: build, test, sign, and publish), so the version you download always
  matches the published release exactly.

## Under the hood

- Large internal refactor to dependency injection, removing global singletons
  and static state for better modularity and testability.
- Configuration saving is now thread-safe, with improved error handling and
  cleanup throughout the cache and configuration layers.
- Added a unit test suite covering date handling, metadata validation, and file
  migration.
