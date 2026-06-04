# DonConnect Beta 2

DonConnect is a Streamer.bot extension for receiving donations from several services and exposing them in one shared format for Streamer.bot actions, OBS widgets, goals, timers, credits, and leaderboards.

## Quick Start: Tokens And API Access

### DonationAlerts

For Beta 2 testers, DonationAlerts is the easiest provider to connect.

1. Import `DonConnect.Beta2.sb` into Streamer.bot.
2. Run the action `DonConnect - DonationAlerts Shared Auth`.
3. A DonationAlerts login page will open in your browser.
4. Log in to your own DonationAlerts account and allow access.
5. Return to Streamer.bot and run `DonConnect - Widget Editor`.

You do not need to create your own DonationAlerts API application for this beta. The shared test application credentials are bundled so testers can connect faster.

Useful links:

- DonationAlerts dashboard: https://www.donationalerts.com/dashboard
- DonationAlerts API documentation: https://www.donationalerts.com/apidoc

### Other Providers

Open the provider setup action in Streamer.bot and paste the token/API key from the matching service dashboard.

- StreamElements: account/channel settings and JWT token, https://streamelements.com/dashboard/account/channels
- StreamElements help: https://support.streamelements.com/hc/en-us/categories/10474362906642-Getting-Started
- DonatePay RU: https://donatepay.ru/
- DonatePay EU: https://donatepay.eu/
- DonatePay/DonationPay API docs: https://api.donationpay.org/documentation/
- DonateX.gg: https://donatex.gg/
- ODA docs: https://opendonationassistant.mintlify.app/auth
- Donate.Stream: https://donate.stream/

Do not paste tokens into stream chat, Discord screenshots, public GitHub issues, or OBS text sources.

## Install

Use one file:

```text
DonConnect.Beta2.sb
```

In Streamer.bot:

1. Open **Import**.
2. Open `DonConnect.Beta2.sb` as a text file or copy its full content.
3. Paste it into **Import String**.
4. Press **Import**.

After import, the DonConnect actions will appear in Streamer.bot.

## Open The Widget Editor

Run one action:

```text
DonConnect - Widget Editor
```

This action starts the built-in local server and opens the browser editor:

```text
http://127.0.0.1:3987/donconnect/editor
```

No extra software is required. Users do not need Node.js, npm, Python, Docker, Electron, or a separate server.

## OBS URLs

Use the editor button to copy the current widget URL, or paste one of these URLs manually.

```text
Donation alert:  http://127.0.0.1:3987/donconnect/widget
Goal:            http://127.0.0.1:3987/donconnect/goal
Timer:           http://127.0.0.1:3987/donconnect/timer
Credits:         http://127.0.0.1:3987/donconnect/credits
Leaderboard:     http://127.0.0.1:3987/donconnect/leaderboard
OBS dock panel:  http://127.0.0.1:3987/donconnect/dock
```

Recommended OBS Browser Source sizes:

```text
Donation alert:  1280 x 720
Goal:            1280 x 520
Timer:           1280 x 420
Credits:         1920 x 1080
Leaderboard:     1280 x 720
OBS dock panel:  OBS custom browser dock
```

## What Is Included In Beta 2

- Built-in browser editor served from the Streamer.bot extension itself.
- Donation alert widget with media library, custom tests, replay history, text-to-speech, and blacklist filtering.
- Goal widget with presets, horizontal/vertical progress, image-fill modes, provider labels, last donor settings, and detailed layout controls.
- Timer widget with presets, count-up mode, donation-to-time conversion, and manual test amount.
- Credits widget with Streamer.bot credits data, pause button, section toggles, presets, and live style editing.
- Leaderboard widget with presets, editable entries, blacklist filtering, and recent donation history.
- OBS dock panel for recent donations and replay buttons.
- Local JSON settings stored in the DonConnect folder next to Streamer.bot, not in AppData.
- Update check on startup.

## Update Notifications

On startup, DonConnect checks:

```text
https://raw.githubusercontent.com/DrPlankton/DonConnect/main/version.json
```

If the installed version is older, DonConnect sends one short chat message with the new version and download link. It does not upload tokens, settings, donation messages, or personal data.

## Duplicate Donation Warning

Some services can proxy donations through another provider. For example, StreamElements may send a donation that also appears through DonatePay EU, and ODA can aggregate DonationAlerts, DonatePay, DonateX, and other providers.

If you connect both the aggregator and the original provider, test carefully. DonConnect has deduplication, but provider APIs do not always expose the same donor name or transaction id.

## Variables Exposed To Streamer.bot

DonConnect keeps normalized donation variables available for user actions:

```text
donationSource
donationUser
donationAmount
donationCurrency
donationMessage
donationId
donationTimestamp
donationRawJson
donationIsAnonymous
```

Compatibility aliases are also kept:

```text
tipUser
tipAmount
tipCurrency
tipMessage
```

## Security

The local editor server binds only to:

```text
127.0.0.1
```

It is not exposed to the local network and does not require admin rights or HTTPS certificates. Tokens and secrets are not printed in full logs.

## Troubleshooting

If the editor does not open, run `DonConnect - Widget Editor` again and check the Streamer.bot log for the local URL.

If port `3987` is busy, close the other local process using that port or restart Streamer.bot.

If OBS does not update, make sure Streamer.bot is running, the widget editor action was started once, and the OBS Browser Source URL starts with `http://127.0.0.1:3987/donconnect/`.

If a provider does not connect, run the DonConnect diagnostics action and check whether the provider is enabled and connected.
