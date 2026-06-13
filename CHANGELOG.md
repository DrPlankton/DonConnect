# Changelog

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
- Added widget profile export/import without provider secrets or media library files.
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
