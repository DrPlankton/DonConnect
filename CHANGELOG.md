# Changelog

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
