# DonConnect for Streamer.bot

DonConnect is a donation/tip bridge for Streamer.bot. It listens for donation events from supported platforms and exposes them through Streamer.bot Custom Code Event triggers with one normalized variable format.

## Current Status

Working:

- Test donation event.
- DonationAlerts via OAuth and WebSocket.
- StreamElements via Astro WebSocket Gateway and `channel.tips`.
- Unified Streamer.bot variables.
- Custom triggers per provider.
- Automatic restart after Streamer.bot restart when tokens are already saved.
- Deduplication for repeated donation events.
- Generic polling API provider.

Prepared / placeholder adapters:

- Streamlabs.
- DonatePay.
- Donate.Stream / DonateStream.
- DonateX.gg.

These services have setup actions and provider classes, but their final adapters should be enabled only after their current public API/realtime behavior is verified.

## Files

- `DonConnect.cs` - main Streamer.bot C# code.
- `DonConnect.install.sb` - ready-to-import Streamer.bot import file.
- `README_RU.md` - full Russian guide.
- `README.md` - English overview.

## Installation

1. Open Streamer.bot.
2. Click **Import**.
3. Open `DonConnect.install.sb` in a text editor.
4. Copy the entire file content.
5. Paste it into Streamer.bot **Import String**.
6. Click **Import**.

After import, you should see an Actions group named `DonConnect`.

## Beginner Setup

The recommended setup flow does not require chat commands.

1. Select `DonConnect - Setup DonationAlerts`.
2. Right-click the action or its trigger.
3. Choose **Test Trigger**.
4. Authorize in the browser.
5. DonConnect starts listening automatically.

After Streamer.bot restarts, `DonConnect - Auto Start` attempts to start listening again if tokens were already saved. It does not open a browser.

## DonationAlerts

DonationAlerts redirect URI:

```text
http://127.0.0.1:8597/donconnect/donationalerts/callback/
```

Scopes:

```text
oauth-user-show oauth-donation-subscribe oauth-donation-index
```

DonConnect supports two DonationAlerts modes:

- Simple / shared app mode, intended for public builds.
- Advanced own app mode, where a user enters their own OAuth app credentials.

Do not commit real `Client Secret` values to GitHub. Keep placeholders in public releases.

## StreamElements

Use these values from the StreamElements dashboard:

- `Account ID`
- `JWT Token`

Do not use the Overlay Token for DonConnect.

The JWT token is secret. Do not publish it.

## Custom Triggers

Create your own Streamer.bot action and add:

```text
Add -> Custom -> DonConnect -> Donations
```

Available triggers:

```text
Any donation
DonationAlerts donation
DonatePay donation
StreamElements donation
Streamlabs donation
Generic API donation
Donate.Stream donation
DonateX.gg donation
```

## Variables

DonConnect passes these variables to donation-triggered actions:

| Variable | Description |
| --- | --- |
| `donationSource` | Source name, for example `DonationAlerts` |
| `donationProvider` | Provider name |
| `donationEventType` | Event type, usually `donation` |
| `donationUser` | Donor/tipper name |
| `donationAmount` | Donation amount |
| `donationCurrency` | Currency |
| `donationMessage` | Donation message |
| `donationId` | Provider donation ID |
| `donationTimestamp` | UTC timestamp |
| `donationRawJson` | Raw provider JSON |
| `donationIsAnonymous` | `True` or `False` |

Aliases:

| Variable | Description |
| --- | --- |
| `tipSource` | Same as `donationSource` |
| `tipUser` | Same as `donationUser` |
| `tipAmount` | Same as `donationAmount` |
| `tipCurrency` | Same as `donationCurrency` |
| `tipMessage` | Same as `donationMessage` |

Example chat message:

```text
Thank you, %donationUser%, for %donationAmount% %donationCurrency%! %donationMessage%
```

## Chat Commands

Streamer.bot may import commands as disabled for safety. This is normal. The main setup flow uses **Test Trigger**, not chat commands.

If you enable commands manually, keep them restricted to `Broadcaster` only. The import file is prepared with Broadcaster-only permissions.

## Security

- Do not hardcode or commit real tokens.
- Do not publish DonationAlerts `Client Secret`.
- Do not publish StreamElements `JWT Token`.
- Streamer.bot global variables are convenient, but they are not a full secure secret vault.
- A shared DonationAlerts app is beginner-friendly, but a `Client Secret` inside `.sb` or C# can be extracted by advanced users.

## License

MIT. See `LICENSE`.
