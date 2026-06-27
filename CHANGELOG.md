# Changelog

## 0.13.0-beta.3.14 - DonConnect Beta 3 Hotfix 14

- Timer decorative images are now separated from goal decorative images, so timer reset/upload no longer affects goal artwork.
- Fixed timer decor layer bindings in the direct preview editor, including move, resize, rotate, and reset.
- Repacked `DonConnect.Beta3.sb` so the Streamer.bot import contains hotfix 14 code.

## 0.13.0-beta.3.13 - DonConnect Beta 3 Hotfix 13

- Donation logs now use the compact daily line format: `dd/mm/yy - donor - amount currency - platform - message`.
- Moved the OBS Dock log-folder button next to Refresh and renamed it to Open logs.
- Moved the OBS Dock editor tab to the end of the editor navigation.
- Fixed goal preview resizing for the goal bar and goal text layers, including multi-selection reset behavior.
- Fixed timer preview layer selection for providers and hidden timer layers; timer-only mode no longer shows the last donation line.
- Added decorative image controls for donation alerts, timer, and leaderboard.

## 0.13.0-beta.3.12 - DonConnect Beta 3 Hotfix 12

- Fixed direct preview resizing for layered widgets: resizing one alert/goal/timer element now preserves the visual positions of the other editable layers.
- Stabilized donation alert layout slots so increasing donor, amount, platform, message, or media size no longer pushes neighboring alert elements.

## 0.13.0-beta.3.11 - DonConnect Beta 3 Hotfix 11

- Unified daily donation log paths: real donation logs and the editor/OBS Dock "open logs" button now use the same DonConnect data folder.
- Added height resizing for the goal/timer panel background in the direct preview editor.
- Rechecked direct preview editing, widget startup retries, profile export/import coverage, provider-secret exclusion, and embedded browser JavaScript before repacking Beta 3.

## 0.13.0-beta.3.10 - DonConnect Beta 3 Hotfix 10

- Hardened local widget server startup: DonConnect now retries startup and runs a watchdog after Streamer.bot starts, so OBS widgets should not require opening the editor to recover.
- Automatic Credits/leaderboard session reset now also runs from DonConnect startup with retries while Streamer.bot HTTP Server is still becoming available.
- Added a donation log folder button to the OBS dock.
- Renamed the editor OBS Dock log button to make daily donation logs easier to find.

## 0.13.0-beta.3.9 - DonConnect Beta 3 Hotfix 9

- Renamed the stream-start reset action to `DonConnect - Stream Start Reset`; it now resets Credits and leaderboard only once per local day.
- A repeated `Stream Online` event on the same day, such as after a short outage or quick stream restart, no longer clears Credits and leaderboard again.
- Manual Credits reset is now strict: if Streamer.bot HTTP Server does not answer `/ClearCredits`, DonConnect reports failure instead of treating the reset as successful.
- Leaderboard auto-reset on startup is also protected from repeated same-day resets.

## 0.13.0-beta.3.8 - DonConnect Beta 3 Hotfix 8

- Added a small “Reset credits” button to the OBS dock. It clears DonConnect credits and calls Streamer.bot’s native Credits reset when its HTTP Server is available.
- Added a `DonConnect - Reset Credits` action that can be assigned to `Twitch -> Channel -> Stream Online` for stream-start Credits resets.
- Prevented stale Streamer.bot Credits file cache from immediately reappearing after a manual credits reset.
- Rechecked Beta 3 critical paths: local server startup, media-preserving profiles, daily donation logs, provider secrets, timer, goal, and direct preview editing.

## 0.13.0-beta.3 - DonConnect Beta 3

- Added a separate goal deadline timer inside the goal widget. It is disabled by default, uses an end date/time, can be moved in preview, and has its own layer.
- Made Streamer.bot Credits filtering safer: if a Streamer.bot settings schema is not recognized, DonConnect no longer treats missing section arrays as disabled sections.
- Removed bundled DonationAlerts application credentials; users now connect through their own DonationAlerts app.
- Improved startup resilience: the widget/editor server starts independently from provider startup failures.
- Updated the DonationAlerts provider page with app-creation guidance, Redirect URL instructions, and Client ID/Secret validation before authorization.
- Clarified widget profile export/import: used media files are included, provider tokens are not.
- Confirmed provider status: Streamlabs, Donate.Stream, deStream, and the existing providers remain active; StreamEngine stays marked as in development.

## 0.12.1-beta.2.8 - DonConnect Beta 2 Hotfix 8

- Fixed Credits speed logic: the slider now behaves as speed, so moving it right makes Credits faster.
- DonConnect Credits now resolves donor names from `name`, `donor`, `user`, and related aliases instead of falling back to `Viewer`.
- Long Credits rolls auto-accelerate unless “Do not speed up long credits” is enabled.
- Improved installed Windows/custom font fallback for family names with `Regular/Bold/Italic` suffixes.
- Fixed grouped preview movement so nested selected elements no longer move at double speed.
- Prevented alert/goal text and media from being clipped while resizing in the preview editor.
- Made leaderboard slide transitions smoother.
- Updated the import package with the Hotfix 8 code.

## 0.12.1-beta.2.7 - DonConnect Beta 2 Hotfix 7

- Made the provider connection page compact and collapsible, with diagnostic connection status in each header.
- Added navigation between the widget editor and provider setup, direct DonatePay API links, and a StreamEngine placeholder.
- Added direct move, eight-handle resize, rotation, and per-element reset for alert, goal, and timer previews.
- Added an optional alignment grid, canvas center axes, and selected-element center guides without snapping.
- Enabled direct manipulation for alert and goal images.
- Removed direct preview controls from Credits and Leaderboard until those editors are ready for element-level manipulation.

## 0.12.1-beta.2.6 - DonConnect Beta 2 Hotfix 6

- Credits now follow the enabled sections in Streamer.bot Credits settings; roll duration is calculated from the amount of content.
- Added a local JSON backup for provider settings, tokens, and goal/timer state next to Streamer.bot.
- Fixed decimal opacity persistence and added leaderboard text alignment.
- Added independent X/Y controls for donor, amount, message, and platform in donation alerts.
- Fixed manual fallback currency conversion and made timer tests use the production conversion path.
- Added widget profile export/import without provider secrets; used alert media files are included.
- Added a browser-based provider connection page that never returns saved secrets to the browser.
- Added direct drag and resize editing for supported preview elements.
- Made recommended OBS Browser Source dimensions more prominent.

## 0.12.1-beta.2.5 - DonConnect Beta 2 Hotfix 5

- Fixed test Credits being replaced by the regular `credits.cache` on the next poll.
- The test button now toggles a stable test mode and restores live Streamer.bot Credits on the second click.
- Streamer.bot Credits and DonConnect donations are now rendered together.
- Removed duplicate Streamer.bot section controls from the DonConnect editor; section visibility remains managed in Streamer.bot.
- Added DonConnect donation-section controls for title, donor names, amounts, platforms, and messages.
- Live mode no longer inserts fake donation rows when DonConnect has no donation history.

## 0.12.1-beta.2.4 - DonConnect Beta 2 Hotfix 4

- Fixed severe Credits widget stuttering caused by blocking Streamer.bot HTTP requests and repeated full DOM rebuilds.
- DonConnect now reads the local Streamer.bot `data/credits.cache` first and uses short background HTTP requests only as a fallback.
- Restored the complete Streamer.bot Credits section list, including empty sections, roles, Hype Train, Bits, rewards, groups, and custom sections.
- Fixed Credits section checkboxes returning to their previous state after saving or reloading.
- Saving Credits settings no longer forcibly restarts the rolling animation.

## 0.12.1-beta.2.3 - DonConnect Beta 2 Hotfix 3

- Added separate donation voice toggles for donor name, amount, platform, and message.
- The selected speech lines are saved to JSON and apply to real donations, the voice test button, and fallback browser TTS.
- All speech lines remain enabled by default to preserve existing behavior.

## 0.12.1-beta.2.2 - DonConnect Beta 2 Hotfix 2

- Fixed donation alert text-to-speech by loading `System.Speech` correctly and adding a Windows SAPI fallback.
- The voice test button now returns a real result and shows the speech engine or error.
- The editor now lists server-side Windows voices that DonConnect can actually use.
- If an old saved voice is unavailable, DonConnect falls back to the default Windows voice instead of staying silent.

## 0.12.1-beta.2.1 - DonConnect Beta 2 Hotfix 1

- Fixed donation text-to-speech by using Windows voices from DonConnect itself.
- Added voice toggle, voice selection, and a voice test button to the donation alert editor.
- Added a mini goal bar, goal reset button, and mini timer to the OBS dock.
- Fixed manual refresh in the OBS dock.
- Updated README instructions with right-click **Test Trigger** startup steps and upgrade notes.

## 0.12.1-beta.2 - DonConnect Beta 2

- Added the built-in browser widget editor served by the Streamer.bot extension itself.
- Added OBS widgets for donation alerts, goal, timer, credits, leaderboard, and compact OBS dock panel.
- Added media library, alert replay history, custom test alert, text-to-speech support, and blacklist filtering.
- Added more configurable goal, timer, credits, and leaderboard editors with presets and live preview.
- Added local JSON settings stored in the DonConnect folder next to Streamer.bot.
- Added startup update check using `version.json` from GitHub.
- Added ODA beta setup notes and duplicate-provider warnings for aggregator scenarios.
- Replaced older install artifacts with one Beta 2 import file: `DonConnect.Beta2.sb`.

## 0.8.1-beta

- Initial public beta build.
