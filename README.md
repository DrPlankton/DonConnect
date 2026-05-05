# DonConnect for Streamer.bot

DonConnect is a donation/tip bridge for Streamer.bot. It listens for donation events from supported platforms and exposes them through Streamer.bot Custom Code Event triggers with one normalized variable format.

## Current Status

Beta version: `0.8.1-beta`.

Working / beta-ready:

- Test donation event.
- DonationAlerts via OAuth and WebSocket.
- StreamElements via Astro WebSocket Gateway and `channel.tips`.
- DonatePay RU and DonatePay EU via `/api/v1/transactions` polling.
- Streamlabs via `/api/v2.0/donations` polling.
- DonateX via `/v1/donations` polling.
- deStream via `/api/v2/users/tips` polling.
- Donate.Stream via configurable polling endpoint.
- Unified Streamer.bot variables.
- Custom triggers per provider.
- Automatic restart after Streamer.bot restart when tokens are already saved.
- Deduplication for repeated donation events.
- Generic polling API provider.
- Native Streamer.bot Credits donation integration.
- Donation goal and donation marathon/timer variables.
- Optional OBS overlays for goal and timer.

Experimental / provider-dependent:

- Donate.Stream / DonateStream uses a configurable endpoint because its public cabinet API is not documented as a stable external API.

This is a beta build. Test with your own Streamer.bot profile before using it on a live production stream.

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
Custom donation triggers are registered automatically the first time any DonConnect action runs, including setup, status, test donation, start, and auto start.

## Beginner Setup

The recommended setup flow does not require chat commands.

1. Select `DonConnect - Setup DonationAlerts`.
2. Right-click the action or its trigger.
3. Choose **Test Trigger**.
4. Authorize in the browser.
5. DonConnect registers its triggers and starts listening automatically.

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

## Streamlabs

Use a Streamlabs API access token with donations read access.

```text
DonConnect - Setup Streamlabs
streamlabsToken = your Streamlabs access token
```

## DonateX

Use a DonateX access token with `donations.read`.

```text
DonConnect - Setup DonateX
donateXAccessToken = your DonateX access token
```

## deStream

Use the client id and user access token from deStream OAuth.

```text
DonConnect - Setup DeStream
deStreamClientId = your ClientId
deStreamAccessToken = your access_token
deStreamTokenType = Bearer
```

## DonatePay

Use the API access key from DonatePay `API` settings. DonatePay RU and DonatePay EU are separate because streamers can receive donations through both domains at the same time.

Setup actions:

```text
DonConnect - Setup DonatePay RU
DonConnect - Setup DonatePay EU
```

Arguments:

```text
donatePayApiKey = your DonatePay API access key from the same domain
donatePayApiHost = https://donatepay.ru or https://donatepay.eu
```

DonConnect polls successful donation transactions. On first start it marks the existing DonatePay history as already seen, so old donations are not replayed into Streamer.bot.

## Donate.Stream

Use the widget/API token from Donate.Stream. If Donate.Stream changes its private cabinet endpoint, pass a full `donateStreamEndpoint`.

```text
DonConnect - Setup DonateStream
donateStreamToken = your Donate.Stream token
donateStreamEndpoint = optional full polling endpoint
```

## Custom Triggers

Create your own Streamer.bot action and add:

```text
Add -> Custom -> DonConnect -> Donations
```

If the `DonConnect` submenu is not visible immediately after import, run the setup action for the provider you use or `DonConnect - Test Donation` once. This is only needed because Streamer.bot creates custom trigger menus after the code registers them.

Available triggers:

```text
Any donation
DonationAlerts donation
DonatePay donation
DonatePay RU donation
DonatePay EU donation
StreamElements donation
Streamlabs donation
Generic API donation
Donate.Stream donation
deStream donation
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
| `tipUsername` | Same as `donationUser`. Alias for older message templates. |
| `tipName` | Same as `donationUser` |
| `tipAmount` | Same as `donationAmount` |
| `tipCurrency` | Same as `donationCurrency` |
| `tipMessage` | Same as `donationMessage` |

Example chat message:

```text
Thank you, %donationUser%, for %donationAmount% %donationCurrency%! %donationMessage%
```

## Streamer.bot Credits

The import includes these Credits-related actions:

- `DonConnect - Credits` configures donation forwarding into Streamer.bot Credits.
- `Add Donation To Credits` is kept as an example/manual helper in the internal group. DonConnect now writes donations to Credits directly with `CPH.AddToCredits(...)`.

To enable it, run `DonConnect - Credits` with **Test Trigger**. Keep `Servers/Clients -> HTTP Server` enabled for the OBS overlay and `/GetCredits`.

Settings inside `DonConnect - Credits`:

```text
STREAMERBOT_CREDITS_ENABLED=true
STREAMERBOT_HTTP_URL=http://127.0.0.1:7474
STREAMERBOT_CREDITS_ACTION=Add Donation To Credits
STREAMERBOT_CREDITS_SECTION=Донаты
STREAMERBOT_CREDITS_FIELDS=name,amount
```

The HTTP server is only needed by the OBS overlay. Donation entries are added inside Streamer.bot directly.

DonConnect only adds donations to native Streamer.bot Credits. Follows, raids, chat users, top bits, moderators, and the other built-in sections are controlled by Streamer.bot `Settings -> Credits`.

Primary overlay settings live in the `DonConnect - Credits` action. Run that action after editing them; it writes:

```text
D:\SBBOTcodex\DonConnect\credits\credits-config.json
```

Recommended OBS URL:

```text
http://127.0.0.1:7474/credits/streamerbot-credits-overlay.html?v=6
```

Donation display fields:

| Value | Shows |
| --- | --- |
| `name` | Donor name. This is always the main line. |
| `amount` | Amount and currency. |
| `platform` | Donation provider. |
| `message` | Donation message. |

Overlay URL parameters:

| Parameter | Example | Description |
| --- | --- | --- |
| `duration` | `duration=90s` | Scroll speed. Higher is slower. |
| `accent` | `accent=%23ffcf5a` | Heading color. Use `%23` instead of `#`. |
| `text` | `text=%23ffffff` | Main text color. |
| `muted` | `muted=%23b9d8d2` | Detail text color. |
| `font` | `font=Arial` | Font family. |
| `top` | `top=week` | Show top bits. Default is `none`. |
| `donationFields` | `donationFields=name,amount,message` | Override donation fields in the overlay URL. |

Section labels are configured in `STREAMERBOT_CREDITS_SECTION_LABELS`:

```text
Follows=フォロー;Raided=レイド;Moderator=Moderators;Users=Viewers;донаты=Donations
```

Recommended OBS URL:

```text
http://127.0.0.1:7474/credits/streamerbot-credits-overlay.html?v=4&duration=90s
```

The credits overlay and helper examples live in:

```text
docs/examples/integrations/
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
