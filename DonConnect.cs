using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private static DonationBridgeRuntime Runtime;
    private static readonly object GlobalDedupeLock = new object();
    private const string CurrentVersion = "0.12.1-beta.2.4";
    private const string UpdateFeedUrl = "https://raw.githubusercontent.com/DrPlankton/DonConnect/main/version.json";
    private const string BundledDonationAlertsClientId = "18717";
    private const string BundledDonationAlertsClientSecret = "XxOAjz0FeUQlzNWjmWwzxZGpeGb57hSEt0dZskB6";

    public void Init()
    {
        EnsureRuntime();
        Runtime.Logger.Info("DonConnect initialized.");
        Runtime.CheckForUpdatesAsync();
    }

    public void Dispose()
    {
        if (Runtime != null)
        {
            Runtime.Stop();
            Runtime.StopWidgetServer();
        }
    }

    public bool SetupDonationAlerts()
    {
        EnsureRuntime();
        string clientId = ReadArg("clientId");
        string clientSecret = ReadArg("clientSecret");

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            Runtime.Logger.Warn("Для настройки DonationAlerts нужны аргументы clientId и clientSecret.");
            CPH.SendMessage("DonConnect: укажите clientId и clientSecret в Action настройки.");
            return false;
        }

        Runtime.Settings.Set("donationalerts.clientId", clientId.Trim(), true);
        Runtime.Settings.Set("donationalerts.clientSecret", clientSecret.Trim(), true);
        Runtime.Settings.Set("donationalerts.authMode", "own", true);
        Runtime.Settings.Set("donationalerts.enabled", "true", true);
        Runtime.Logger.Info("DonationAlerts настроен. Client ID: " + SecretMask.Mask(clientId));
        return true;
    }

    public bool SetupSharedDonationAlerts()
    {
        EnsureRuntime();
        string clientId = ReadArg("sharedClientId");
        string clientSecret = ReadArg("sharedClientSecret");

        if (IsPlaceholder(clientId) || IsPlaceholder(clientSecret))
        {
            clientId = BundledDonationAlertsClientId;
            clientSecret = BundledDonationAlertsClientSecret;
        }

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            Runtime.Logger.Warn("Для общего режима нужны аргументы sharedClientId и sharedClientSecret.");
            return false;
        }

        Runtime.Settings.Set("donationalerts.sharedClientId", clientId.Trim(), true);
        Runtime.Settings.Set("donationalerts.sharedClientSecret", clientSecret.Trim(), true);
        Runtime.Settings.Set("donationalerts.authMode", "shared", true);
        Runtime.Settings.Set("donationalerts.enabled", "true", true);
        Runtime.Logger.Info("DonationAlerts переведен в shared mode. Shared Client ID: " + SecretMask.Mask(clientId));
        return true;
    }

    private bool IsPlaceholder(string value)
    {
        return string.IsNullOrWhiteSpace(value) || value.IndexOf("PASTE_", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public bool SetupAndAuthorizeDonationAlerts()
    {
        if (!SetupSharedDonationAlerts())
            return false;

        if (!AuthorizeDonationAlerts())
            return false;

        return StartBridge();
    }

    public bool SetupOwnAndAuthorizeDonationAlerts()
    {
        if (!SetupDonationAlerts())
            return false;

        if (!AuthorizeDonationAlerts())
            return false;

        return StartBridge();
    }

    public bool UseSharedDonationAlerts()
    {
        EnsureRuntime();
        Runtime.Settings.Set("donationalerts.authMode", "shared", true);
        Runtime.Settings.Set("donationalerts.enabled", "true", true);
        Runtime.Logger.Info("DonationAlerts auth mode: shared.");
        return true;
    }

    public bool UseOwnDonationAlerts()
    {
        EnsureRuntime();
        Runtime.Settings.Set("donationalerts.authMode", "own", true);
        Runtime.Settings.Set("donationalerts.enabled", "true", true);
        Runtime.Logger.Info("DonationAlerts auth mode: own.");
        return true;
    }

    public bool AuthorizeDonationAlerts()
    {
        EnsureRuntime();
        Runtime.ShowPopupBase64("0J/QvtC20LDQu9GD0LnRgdGC0LAsINCw0LLRgtC+0YDQuNC30YPQudGC0LXRgdGMINCyINCx0YDQsNGD0LfQtdGA0LUuINCf0L7RgdC70LUg0L/QvtC00YLQstC10YDQttC00LXQvdC40Y8g0LLQtdGA0L3QuNGC0LXRgdGMINCyIFN0cmVhbWVyLmJvdC4=");
        bool ok = Runtime.AuthorizeDonationAlerts();
        if (ok)
            Runtime.ShowPopupBase64("0JLRiyDQv9C+0LTQutC70Y7Rh9C10L3RiyDQuiBEb25Db25uZWN0INGD0LbQtSDQt9Cw0L/Rg9GJ0LXQvSDQuCDRgdC70YPRiNCw0LXRgiDQtNC+0L3QsNGC0Ysu");
        return ok;
    }

    public bool AuthorizeDonationAlertsAndStart()
    {
        if (!AuthorizeDonationAlerts())
            return false;

        return StartBridge();
    }

    public bool SetupGenericApi()
    {
        EnsureRuntime();
        string endpoint = ReadArg("genericEndpoint");
        string token = ReadArg("genericToken");

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            Runtime.Logger.Warn("Для Generic API нужен аргумент genericEndpoint.");
            return false;
        }

        Runtime.Settings.Set("generic.endpoint", endpoint.Trim(), true);
        Runtime.Settings.Set("generic.token", token ?? "", true);
        Runtime.Settings.Set("generic.enabled", "true", true);
        Runtime.Logger.Info("Generic API настроен: " + endpoint.Trim());
        return true;
    }

    public bool SetupStreamElements()
    {
        EnsureRuntime();
        string accountId = ReadArg("streamElementsAccountId");
        string jwtToken = ReadArg("streamElementsJwtToken");
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(jwtToken))
        {
            Runtime.Logger.Warn("StreamElements: укажите streamElementsToken.");
            return false;
        }

        Runtime.Settings.Set("streamelements.accountId", accountId.Trim(), true);
        Runtime.Settings.Set("streamelements.jwtToken", jwtToken.Trim(), true);
        Runtime.Settings.Set("streamelements.enabled", "true", true);
        Runtime.Logger.Info("StreamElements token сохранен. Реальный адаптер будет включен после добавления актуальной схемы API.");
        return true;
    }

    public bool SetupStreamlabs()
    {
        EnsureRuntime();
        string token = ReadArg("streamlabsToken");
        if (string.IsNullOrWhiteSpace(token))
        {
            Runtime.Logger.Warn("Streamlabs: укажите streamlabsToken.");
            return false;
        }

        Runtime.Settings.Set("streamlabs.token", token.Trim(), true);
        Runtime.Settings.Set("streamlabs.lastDonationId", "", true);
        Runtime.Settings.Set("streamlabs.enabled", "true", true);
        Runtime.Logger.Info("Streamlabs token сохранен. Реальный адаптер будет включен после добавления актуальной схемы API.");
        Runtime.Start();
        return true;
    }

    public bool SetupDonatePay()
    {
        string host = FirstNonEmpty(ReadArg("donatePayApiHost"), "https://donatepay.ru").Trim().TrimEnd('/');
        string key = host.IndexOf("donatepay.eu", StringComparison.OrdinalIgnoreCase) >= 0 ? "donatepayeu" : "donatepayru";
        string display = key == "donatepayeu" ? "DonatePay EU" : "DonatePay RU";
        return SetupDonatePayForHost(key, display, host);
    }

    public bool SetupDonatePayRu()
    {
        return SetupDonatePayForHost("donatepayru", "DonatePay RU", "https://donatepay.ru");
    }

    public bool SetupDonatePayEu()
    {
        return SetupDonatePayForHost("donatepayeu", "DonatePay EU", "https://donatepay.eu");
    }

    private bool SetupDonatePayForHost(string providerKey, string displayName, string apiHost)
    {
        EnsureRuntime();
        string apiKey = ReadArg("donatePayApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Runtime.Logger.Warn(displayName + ": укажите donatePayApiKey.");
            return false;
        }

        Runtime.Settings.Set(providerKey + ".apiKey", apiKey.Trim(), true);
        Runtime.Settings.Set(providerKey + ".apiHost", FirstNonEmpty(apiHost, "https://donatepay.ru").Trim().TrimEnd('/'), true);
        Runtime.Settings.Set(providerKey + ".pollSeconds", FirstNonEmpty(ReadArg("donatePayPollSeconds"), "20").Trim(), true);
        Runtime.Settings.Set(providerKey + ".lastTransactionId", "", true);
        Runtime.Settings.Set(providerKey + ".seenDonationIds", "", true);
        Runtime.Settings.Set(providerKey + ".enabledAt", DateTime.UtcNow.ToString("o"), true);
        Runtime.Settings.Set(providerKey + ".enabled", "true", true);
        Runtime.Logger.Info(displayName + " API key сохранен. Host=" + FirstNonEmpty(apiHost, "https://donatepay.ru").Trim().TrimEnd('/'));
        Runtime.Start();
        return true;
    }

    public bool SetupDonateStream()
    {
        EnsureRuntime();
        string token = FirstNonEmpty(ReadArg("donateStreamToken"), ReadArg("donateStreamApiToken"));
        string endpoint = ReadArg("donateStreamEndpoint");
        if (string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(endpoint))
        {
            Runtime.Logger.Warn("Donate.Stream: укажите donateStreamToken или donateStreamEndpoint.");
            return false;
        }

        Runtime.Settings.Set("donatestream.token", token.Trim(), true);
        Runtime.Settings.Set("donatestream.endpoint", endpoint.Trim(), true);
        Runtime.Settings.Set("donatestream.lastDonationId", "", true);
        Runtime.Settings.Set("donatestream.enabled", "true", true);
        Runtime.Logger.Info("Donate.Stream настройки сохранены.");
        Runtime.Start();
        return true;
    }

    public bool SetupDeStream()
    {
        EnsureRuntime();
        string clientId = ReadArg("deStreamClientId");
        string accessToken = ReadArg("deStreamAccessToken");
        string tokenType = FirstNonEmpty(ReadArg("deStreamTokenType"), "Bearer");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(accessToken))
        {
            Runtime.Logger.Warn("deStream: укажите deStreamClientId и deStreamAccessToken.");
            return false;
        }

        Runtime.Settings.Set("destream.clientId", clientId.Trim(), true);
        Runtime.Settings.Set("destream.accessToken", accessToken.Trim(), true);
        Runtime.Settings.Set("destream.tokenType", tokenType.Trim(), true);
        Runtime.Settings.Set("destream.lastDonationDate", "", true);
        Runtime.Settings.Set("destream.enabled", "true", true);
        Runtime.Logger.Info("deStream access token сохранен.");
        Runtime.Start();
        return true;
    }

    public bool SetupDonateX()
    {
        EnsureRuntime();
        string accessToken = ReadArg("donateXAccessToken");
        string apiBase = FirstNonEmpty(ReadArg("donateXApiBase"), "https://donatex.gg/api");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            Runtime.Logger.Warn("DonateX: укажите donateXAccessToken.");
            return false;
        }

        Runtime.Settings.Set("donatex.accessToken", accessToken.Trim(), true);
        Runtime.Settings.Set("donatex.apiBase", apiBase.Trim().TrimEnd('/'), true);
        Runtime.Settings.Set("donatex.pollSeconds", FirstNonEmpty(ReadArg("donateXPollSeconds"), "5").Trim(), true);
        Runtime.Settings.Set("donatex.lastDonationId", "", true);
        Runtime.Settings.Set("donatex.seenDonationIds", "", true);
        Runtime.Settings.Set("donatex.enabled", "true", true);
        Runtime.Logger.Info("DonateX access token сохранен.");
        Runtime.Start();
        return true;
    }

    public bool SetupOda()
    {
        EnsureRuntime();
        string accessToken = FirstNonEmpty(ReadArg("odaAccessToken"), ReadArg("odaToken"));
        string apiBase = FirstNonEmpty(ReadArg("odaApiBase"), "https://api.oda.digital");
        string historyEndpoint = ReadArg("odaHistoryEndpoint");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            Runtime.Logger.Warn("ODA: укажите odaAccessToken.");
            return false;
        }

        Runtime.Settings.Set("oda.accessToken", accessToken.Trim(), true);
        Runtime.Settings.Set("oda.apiBase", apiBase.Trim().TrimEnd('/'), true);
        Runtime.Settings.Set("oda.historyEndpoint", historyEndpoint.Trim(), true);
        Runtime.Settings.Set("oda.pollSeconds", FirstNonEmpty(ReadArg("odaPollSeconds"), "10").Trim(), true);
        Runtime.Settings.Set("oda.lastTimestamp", "", true);
        Runtime.Settings.Set("oda.seenDonationIds", "", true);
        Runtime.Settings.Set("oda.enabledAt", DateTime.UtcNow.ToString("o"), true);
        Runtime.Settings.Set("oda.enabled", "true", true);
        Runtime.Logger.Info("ODA access token сохранен. Если в ODA уже подключены DA/DPEU/DPRU/DX, не включайте эти же площадки второй раз без антидублей.");
        Runtime.WarnAboutProviderConflicts();
        Runtime.Start();
        return true;
    }

    public bool SendChatNotification()
    {
        EnsureRuntime();
        Runtime.SendCurrentDonationToChat();
        return true;
    }

    public bool SetupCreditsIntegration()
    {
        EnsureRuntime();
        string enabled = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_ENABLED"), ReadArg("streamerbotCreditsEnabled"), "true");
        string httpUrl = FirstNonEmpty(ReadArg("STREAMERBOT_HTTP_URL"), ReadArg("streamerbotHttpUrl"), "http://127.0.0.1:7474");
        string actionName = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_ACTION"), ReadArg("streamerbotCreditsAction"), "Add Donation To Credits");
        string section = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_SECTION"), ReadArg("streamerbotCreditsSection"), "\u0414\u043e\u043d\u0430\u0442\u044b");
        string fields = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_FIELDS"), ReadArg("streamerbotCreditsFields"), "name,amount");
        string configPath = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_CONFIG_PATH"), ReadArg("streamerbotCreditsConfigPath"), Runtime.DefaultCreditsConfigPath());
        string title = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_TITLE"), ReadArg("streamerbotCreditsTitle"), "\u0421\u043f\u0430\u0441\u0438\u0431\u043e \u0437\u0430 \u0441\u0442\u0440\u0438\u043c");
        string subtitle = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_SUBTITLE"), ReadArg("streamerbotCreditsSubtitle"), "\u0421\u0435\u0433\u043e\u0434\u043d\u044f \u0441 \u043d\u0430\u043c\u0438 \u0431\u044b\u043b\u0438");
        string outro = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_OUTRO"), ReadArg("streamerbotCreditsOutro"), "\u0423\u0432\u0438\u0434\u0438\u043c\u0441\u044f \u043d\u0430 \u0441\u043b\u0435\u0434\u0443\u044e\u0449\u0435\u043c \u0441\u0442\u0440\u0438\u043c\u0435");
        string duration = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_DURATION"), ReadArg("streamerbotCreditsDuration"), "90s");
        string accent = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_ACCENT"), ReadArg("streamerbotCreditsAccent"), "#ffcf5a");
        string text = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_TEXT"), ReadArg("streamerbotCreditsText"), "#f7f4ec");
        string muted = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_MUTED"), ReadArg("streamerbotCreditsMuted"), "#b9d8d2");
        string background = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_BG"), ReadArg("streamerbotCreditsBg"), "transparent");
        string font = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_FONT"), ReadArg("streamerbotCreditsFont"), "Segoe UI, Arial, sans-serif");
        string labels = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_SECTION_LABELS"), ReadArg("streamerbotCreditsSectionLabels"), DefaultCreditsLabels());
        string hideSections = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_HIDE_SECTIONS"), ReadArg("streamerbotCreditsHideSections"), "Users,allBits,monthBits,weekBits");
        string showSections = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_SHOW_SECTIONS"), ReadArg("streamerbotCreditsShowSections"), "");

        Runtime.Settings.Set("STREAMERBOT_CREDITS_ENABLED", enabled.Trim(), true);
        Runtime.Settings.Set("STREAMERBOT_HTTP_URL", httpUrl.Trim().TrimEnd('/'), true);
        Runtime.Settings.Set("STREAMERBOT_CREDITS_ACTION", actionName.Trim(), true);
        Runtime.Settings.Set("STREAMERBOT_CREDITS_SECTION", section.Trim(), true);
        Runtime.Settings.Set("STREAMERBOT_CREDITS_FIELDS", fields.Trim(), true);
        Runtime.Settings.Set("STREAMERBOT_CREDITS_CONFIG_PATH", configPath.Trim(), true);
        Runtime.Settings.Set("STREAMERBOT_CREDITS_HIDE_SECTIONS", hideSections.Trim(), true);
        Runtime.Settings.Set("STREAMERBOT_CREDITS_SHOW_SECTIONS", showSections.Trim(), true);
        Runtime.WriteCreditsOverlayConfig(configPath, title, subtitle, outro, duration, accent, text, muted, background, font, fields, labels, hideSections, showSections);
        Runtime.Logger.Info("Streamer.bot Credits integration: enabled=" + enabled.Trim()
            + ", url=" + httpUrl.Trim().TrimEnd('/')
            + ", action=" + actionName.Trim()
            + ", section=" + section.Trim()
            + ", fields=" + fields.Trim());
        return true;
    }

    public bool SetupDonationGoal()
    {
        EnsureRuntime();
        string enabled = FirstNonEmpty(ReadArg("donConnectGoalEnabled"), "true");
        string title = FirstNonEmpty(ReadArg("donConnectGoalTitle"), "\u0421\u0431\u043e\u0440");
        string target = FirstNonEmpty(ReadArg("donConnectGoalTarget"), "0");
        string current = FirstNonEmpty(ReadArg("donConnectGoalStartAmount"), ReadArg("donConnectGoalCurrent"), "0");
        string currency = FirstNonEmpty(ReadArg("donConnectGoalCurrency"), "RUB");
        string conversion = FirstNonEmpty(ReadArg("donConnectCurrencyConversion"), "auto");
        string rates = FirstNonEmpty(ReadArg("donConnectCurrencyRates"), "RUB=1;USD=90;EUR=100");
        string onError = FirstNonEmpty(ReadArg("donConnectCurrencyOnError"), "skip");

        Runtime.Settings.Set("goal.enabled", enabled.Trim(), true);
        Runtime.Settings.Set("goal.title", title.Trim(), true);
        Runtime.Settings.Set("goal.target", target.Trim(), true);
        Runtime.Settings.Set("goal.current", current.Trim(), true);
        Runtime.Settings.Set("goal.currency", currency.Trim(), true);
        Runtime.Settings.Set("currency.conversion", conversion.Trim(), true);
        Runtime.Settings.Set("currency.rates", rates.Trim(), true);
        Runtime.Settings.Set("currency.onError", onError.Trim(), true);
        Runtime.Settings.Set("currency.cacheMinutes", FirstNonEmpty(ReadArg("donConnectCurrencyCacheMinutes"), "60").Trim(), true);
        Runtime.Settings.Set("currency.apiUrl", FirstNonEmpty(ReadArg("donConnectCurrencyApiUrl"), "https://open.er-api.com/v6/latest/{FROM}").Trim(), true);
        Runtime.Logger.Info("Donation goal configured: " + title.Trim() + ", current=" + current.Trim() + ", target=" + target.Trim() + ", currency=" + currency.Trim());

        var fake = new UnifiedDonationEvent
        {
            ProviderName = "DonConnect",
            Source = "DonConnect",
            UserName = "Setup",
            Amount = 0,
            Currency = currency.Trim(),
            Timestamp = DateTime.UtcNow
        };
        Runtime.HandleGoalOnly(fake);
        return true;
    }

    public bool ResetDonationGoal()
    {
        EnsureRuntime();
        Runtime.Settings.Set("goal.current", "0", true);
        Runtime.Logger.Info("Donation goal reset to 0.");
        var fake = new UnifiedDonationEvent
        {
            ProviderName = "DonConnect",
            Source = "DonConnect",
            UserName = "Reset",
            Amount = 0,
            Currency = Runtime.Settings.Get("goal.currency", "RUB"),
            Timestamp = DateTime.UtcNow
        };
        Runtime.HandleGoalOnly(fake);
        return true;
    }

    public bool SetupDonationTimer()
    {
        EnsureRuntime();
        string enabled = FirstNonEmpty(ReadArg("donConnectTimerEnabled"), "true");
        string title = FirstNonEmpty(ReadArg("donConnectTimerTitle"), "\u0414\u043e\u043d\u0430\u0442\u043d\u044b\u0439 \u0442\u0430\u0439\u043c\u0435\u0440");
        string startSeconds = FirstNonEmpty(ReadArg("donConnectTimerStartSeconds"), "0");
        string unitAmount = FirstNonEmpty(ReadArg("donConnectTimerUnitAmount"), "100");
        string secondsPerUnit = FirstNonEmpty(ReadArg("donConnectTimerSecondsPerUnit"), "60");
        string maxSeconds = FirstNonEmpty(ReadArg("donConnectTimerMaxSeconds"), "0");
        string currency = FirstNonEmpty(ReadArg("donConnectTimerCurrency"), "RUB");
        string conversion = FirstNonEmpty(ReadArg("donConnectCurrencyConversion"), "auto");
        string rates = FirstNonEmpty(ReadArg("donConnectCurrencyRates"), "RUB=1;USD=90;EUR=100");
        string onError = FirstNonEmpty(ReadArg("donConnectCurrencyOnError"), "skip");

        Runtime.Settings.Set("timer.enabled", enabled.Trim(), true);
        Runtime.Settings.Set("timer.title", title.Trim(), true);
        Runtime.Settings.Set("timer.unitAmount", unitAmount.Trim(), true);
        Runtime.Settings.Set("timer.secondsPerUnit", secondsPerUnit.Trim(), true);
        Runtime.Settings.Set("timer.maxSeconds", maxSeconds.Trim(), true);
        Runtime.Settings.Set("timer.currency", currency.Trim(), true);
        Runtime.Settings.Set("timer.mode", FirstNonEmpty(ReadArg("donConnectTimerMode"), "countdown").Trim(), true);
        Runtime.Settings.Set("timer.startedAt", DateTime.UtcNow.ToString("o"), true);
        Runtime.Settings.Set("currency.conversion", conversion.Trim(), true);
        Runtime.Settings.Set("currency.rates", rates.Trim(), true);
        Runtime.Settings.Set("currency.onError", onError.Trim(), true);
        Runtime.Settings.Set("currency.cacheMinutes", FirstNonEmpty(ReadArg("donConnectCurrencyCacheMinutes"), "60").Trim(), true);
        Runtime.Settings.Set("currency.apiUrl", FirstNonEmpty(ReadArg("donConnectCurrencyApiUrl"), "https://open.er-api.com/v6/latest/{FROM}").Trim(), true);
        Runtime.Settings.Set("timer.endsAt", DateTime.UtcNow.AddSeconds(ParseDouble(startSeconds, 0)).ToString("o"), true);
        Runtime.Logger.Info("Donation timer configured: " + title.Trim() + ", startSeconds=" + startSeconds.Trim() + ", unitAmount=" + unitAmount.Trim() + ", secondsPerUnit=" + secondsPerUnit.Trim());
        Runtime.RefreshDonationTimerVariables();
        return true;
    }

    public bool ResetDonationTimer()
    {
        EnsureRuntime();
        Runtime.Settings.Set("timer.endsAt", DateTime.UtcNow.ToString("o"), true);
        Runtime.Settings.Set("timer.startedAt", DateTime.UtcNow.ToString("o"), true);
        Runtime.Logger.Info("Donation timer reset.");
        Runtime.RefreshDonationTimerVariables();
        return true;
    }

    public bool AddDonationTimerEvent()
    {
        EnsureRuntime();
        decimal amount;
        decimal seconds;
        decimal.TryParse(FirstNonEmpty(ReadArg("timerEventAmount"), ReadArg("bits"), ReadArg("amount"), "0"), NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
        decimal.TryParse(FirstNonEmpty(ReadArg("timerEventSeconds"), "0"), NumberStyles.Any, CultureInfo.InvariantCulture, out seconds);
        string currency = FirstNonEmpty(ReadArg("timerEventCurrency"), ReadArg("currency"), Runtime.Settings.Get("timer.currency", "RUB"));
        string source = FirstNonEmpty(ReadArg("timerEventSource"), "Streamer.bot event");
        Runtime.AddTimerContribution(amount, currency, seconds, source);
        return true;
    }

    public bool SetupGoalTimerOverlay()
    {
        EnsureRuntime();
        string configPath = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_CONFIG_PATH"), ReadArg("donConnectOverlayConfigPath"), Runtime.DefaultGoalTimerOverlayConfigPath());
        string statePath = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_STATE_PATH"), ReadArg("donConnectOverlayStatePath"), Runtime.DefaultGoalTimerOverlayStatePath());
        string mode = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_MODE"), ReadArg("donConnectOverlayMode"), "both");
        string title = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_TITLE"), ReadArg("donConnectOverlayTitle"), "DonConnect");
        string font = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_FONT"), ReadArg("donConnectOverlayFont"), "Segoe UI, Arial, sans-serif");
        string bg = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_BG"), ReadArg("donConnectOverlayBg"), "rgba(10, 12, 18, 0.72)");
        string text = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_TEXT"), ReadArg("donConnectOverlayText"), "#ffffff");
        string muted = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_MUTED"), ReadArg("donConnectOverlayMuted"), "#b8c0cc");
        string accent = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_ACCENT"), ReadArg("donConnectOverlayAccent"), "#35d07f");
        string barBg = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_BAR_BG"), ReadArg("donConnectOverlayBarBg"), "rgba(255,255,255,0.18)");
        string radius = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_RADIUS"), ReadArg("donConnectOverlayRadius"), "8px");
        string scale = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_SCALE"), ReadArg("donConnectOverlayScale"), "1");
        string bare = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_BARE"), ReadArg("donConnectOverlayBare"), "false");
        string showServices = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_SHOW_SERVICES"), ReadArg("donConnectOverlayShowServices"), "true");
        string width = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_WIDTH"), ReadArg("donConnectOverlayWidth"), "920px");
        string padding = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_PADDING"), ReadArg("donConnectOverlayPadding"), "22px");
        string titleSize = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_TITLE_SIZE"), ReadArg("donConnectOverlayTitleSize"), "26px");
        string valueSize = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_VALUE_SIZE"), ReadArg("donConnectOverlayValueSize"), "38px");
        string labelSize = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_LABEL_SIZE"), ReadArg("donConnectOverlayLabelSize"), "15px");
        string metaSize = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_META_SIZE"), ReadArg("donConnectOverlayMetaSize"), "16px");
        string shadow = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_SHADOW"), ReadArg("donConnectOverlayShadow"), "0 16px 44px rgba(0,0,0,0.28)");
        string goalFormat = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_GOAL_FORMAT"), ReadArg("donConnectOverlayGoalFormat"), "amount");
        string servicesTitle = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_SERVICES_TITLE"), ReadArg("donConnectOverlayServicesTitle"), "Подключено");

        Runtime.Settings.Set("overlay.configPath", configPath.Trim(), true);
        Runtime.Settings.Set("overlay.statePath", statePath.Trim(), true);
        Runtime.WriteGoalTimerOverlayConfig(configPath, mode, title, font, bg, text, muted, accent, barBg, radius, scale, bare, showServices, width, padding, titleSize, valueSize, labelSize, metaSize, shadow, goalFormat, servicesTitle);
        Runtime.WriteGoalTimerOverlayState();
        Runtime.Logger.Info("Goal/Timer overlay configured: " + configPath.Trim());
        return true;
    }

    private double ParseDouble(string value, double fallback)
    {
        double result;
        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : fallback;
    }

    private string DefaultCreditsLabels()
    {
        return "Follows=\u041d\u043e\u0432\u044b\u0435 \u0444\u043e\u043b\u043b\u043e\u0432\u0435\u0440\u044b;Cheers=Bits / Cheers;Subs=\u041f\u043e\u0434\u043f\u0438\u0441\u043a\u0438;ReSubs=\u041f\u0440\u043e\u0434\u043b\u0435\u043d\u0438\u044f \u043f\u043e\u0434\u043f\u0438\u0441\u043a\u0438;GiftSubs=\u041f\u043e\u0434\u0430\u0440\u043e\u0447\u043d\u044b\u0435 \u043f\u043e\u0434\u043f\u0438\u0441\u043a\u0438;GiftBombs=\u041c\u0430\u0441\u0441\u043e\u0432\u044b\u0435 \u0433\u0438\u0444\u0442\u044b;Raided=\u0420\u0435\u0439\u0434\u044b;Moderator=\u041c\u043e\u0434\u0435\u0440\u0430\u0442\u043e\u0440\u044b;VIPs=VIP;Users=\u0417\u0440\u0438\u0442\u0435\u043b\u0438;allBits=\u0422\u043e\u043f Bits \u0437\u0430 \u0432\u0441\u0435 \u0432\u0440\u0435\u043c\u044f;monthBits=\u0422\u043e\u043f Bits \u0437\u0430 \u043c\u0435\u0441\u044f\u0446;weekBits=\u0422\u043e\u043f Bits \u0437\u0430 \u043d\u0435\u0434\u0435\u043b\u044e;\u0434\u043e\u043d\u0430\u0442\u044b=\u0414\u043e\u043d\u0430\u0442\u044b";
    }

    public bool Status()
    {
        EnsureRuntime();
        Runtime.LogStatus();
        Runtime.ShowStatusPopup();
        return true;
    }

    public bool Diagnostics()
    {
        EnsureRuntime();
        Runtime.ShowDiagnosticsPopup();
        return true;
    }

    public bool StartBridge()
    {
        EnsureRuntime();
        Runtime.Start(true);
        return true;
    }

    public bool StopBridge()
    {
        EnsureRuntime();
        Runtime.Stop();
        return true;
    }

    public bool ResetBridge()
    {
        EnsureRuntime();
        Runtime.Stop();
        Runtime.Start(true);
        Runtime.Logger.Info("DonConnect restarted.");
        return true;
    }

    public bool TestDonation()
    {
        EnsureRuntime();
        var testEvent = new UnifiedDonationEvent
        {
            Source = "Test",
            ProviderName = "Test",
            EventType = "donation",
            DonationId = "test-" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture),
            UserName = "\u0422\u0435\u0441\u0442\u043e\u0432\u044b\u0439 \u0437\u0440\u0438\u0442\u0435\u043b\u044c",
            Amount = 100,
            Currency = "RUB",
            Message = "\u042d\u0442\u043e \u0442\u0435\u0441\u0442\u043e\u0432\u044b\u0439 \u0434\u043e\u043d\u0430\u0442 \u0438\u0437 DonConnect.",
            Timestamp = DateTime.UtcNow,
            IsAnonymous = false,
            RawJson = "{\"source\":\"Test\"}"
        };

        Runtime.HandleDonation(testEvent);
        Runtime.Logger.Info("Test donation was sent.");
        return true;
    }

    public bool StartWidgetServer()
    {
        try
        {
            EnsureRuntime();
            Runtime.StartWidgetServer();
            return true;
        }
        catch (Exception ex)
        {
            ReportActionError("Widget server was not started", ex);
            return false;
        }
    }

    public bool StopWidgetServer()
    {
        EnsureRuntime();
        Runtime.StopWidgetServer();
        return true;
    }

    public bool OpenWidgetEditor()
    {
        try
        {
            EnsureRuntime();
            Runtime.StartWidgetServer();
            Runtime.OpenWidgetEditor();
            return true;
        }
        catch (Exception ex)
        {
            ReportActionError("Widget editor was not opened", ex);
            return false;
        }
    }

    public bool ShowWidgetUrl()
    {
        EnsureRuntime();
        Runtime.StartWidgetServer();
        Runtime.ShowWidgetUrls();
        return true;
    }

    public bool TestWidgetDonation()
    {
        EnsureRuntime();
        Runtime.StartWidgetServer();
        Runtime.SendWidgetTestDonation("default");
        return true;
    }

    public bool ValidateDonationAlerts()
    {
        EnsureRuntime();
        var provider = Runtime.CreateDonationAlertsProvider();
        return provider.ValidateCredentialsAsync().GetAwaiter().GetResult();
    }

    public bool RegisterTriggers()
    {
        EnsureRuntime();
        Runtime.RegisterCustomTriggers();
        Runtime.Logger.Info("DonConnect custom triggers registered.");
        return true;
    }

    private void EnsureRuntime()
    {
        if (Runtime == null)
            Runtime = new DonationBridgeRuntime(CPH, args);
        else
            Runtime.UpdateArgs(args);

        Runtime.RegisterCustomTriggers();
    }

    private void ReportActionError(string title, Exception ex)
    {
        string message = "DonConnect: " + title + ".\n\n" + (ex == null ? "Unknown error" : ex.Message);
        try
        {
            if (Runtime != null)
            {
                Runtime.Logger.Warn(message.Replace("\n", " "));
                Runtime.ShowPopup(message);
                return;
            }
        }
        catch
        {
        }

        try
        {
            System.Windows.Forms.MessageBox.Show(message, "DonConnect");
        }
        catch
        {
        }
    }

    private string ReadArg(string name)
    {
        object value;
        if (args != null && args.TryGetValue(name, out value) && value != null)
            return value.ToString();
        return "";
    }

    private string FirstNonEmpty(params string[] values)
    {
        foreach (string value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        return "";
    }

    public class DonationBridgeRuntime
    {
        private readonly IInlineInvokeProxy CPH;
        private Dictionary<string, object> Args;
        private readonly List<IDonationProvider> Providers = new List<IDonationProvider>();
        private readonly DonationDeduplicator Deduplicator = new DonationDeduplicator(300);
        private readonly string InstanceId = Guid.NewGuid().ToString("N");
        private CancellationTokenSource RuntimeLeaseCancellation;
        private DonConnectWidgetServer WidgetServer;

        public BridgeSettings Settings { get; private set; }
        public BridgeLogger Logger { get; private set; }

        public DonationBridgeRuntime(IInlineInvokeProxy cph, Dictionary<string, object> args)
        {
            CPH = cph;
            Args = args;
            Settings = new BridgeSettings(cph);
            Logger = new BridgeLogger(cph, Settings);
        }

        public string DefaultCreditsConfigPath()
        {
            return Path.Combine(DefaultSettingsDirectory(), "credits", "credits-config.json");
        }

        public string DefaultGoalTimerOverlayConfigPath()
        {
            return Path.Combine(DefaultSettingsDirectory(), "overlays", "donconnect-overlay-config.json");
        }

        public string DefaultGoalTimerOverlayStatePath()
        {
            return Path.Combine(DefaultSettingsDirectory(), "overlays", "donconnect-overlay-state.json");
        }

        private string DefaultSettingsDirectory()
        {
            return DonConnectPaths.DataDirectory(Settings.Get("donconnect.dataDirectory", ""));
        }

        public void UpdateArgs(Dictionary<string, object> args)
        {
            Args = args;
        }

        public void CheckForUpdatesAsync()
        {
            try
            {
                Task.Run(delegate { CheckForUpdates(); });
            }
            catch (Exception ex)
            {
                Logger.Debug("Update check was not started: " + ex.Message);
            }
        }

        private void CheckForUpdates()
        {
            try
            {
                string lastCheck = Settings.Get("updates.lastCheckUtc", "");
                DateTime lastCheckUtc;
                if (DateTime.TryParse(lastCheck, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out lastCheckUtc)
                    && DateTime.UtcNow - lastCheckUtc.ToUniversalTime() < TimeSpan.FromHours(6))
                    return;

                Settings.Set("updates.lastCheckUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), true);
                JObject feed = JObject.Parse(DownloadText(UpdateFeedUrl));
                string latestVersion = JsonString(feed, "version", "");
                if (string.IsNullOrWhiteSpace(latestVersion))
                    return;

                if (CompareVersionNumbers(latestVersion, CurrentVersion) <= 0)
                    return;

                string lastNotified = Settings.Get("updates.lastNotifiedVersion", "");
                if (latestVersion.Equals(lastNotified, StringComparison.OrdinalIgnoreCase))
                    return;

                string downloadUrl = JsonString(feed, "downloadUrl", "https://github.com/DrPlankton/DonConnect");
                CPH.SendMessage("DonConnect: доступна новая версия " + latestVersion + ". Скачать: " + downloadUrl);
                Settings.Set("updates.lastNotifiedVersion", latestVersion, true);
                Logger.Info("Update available: " + latestVersion);
            }
            catch (Exception ex)
            {
                Logger.Debug("Update check skipped: " + ex.Message);
            }
        }

        private string DownloadText(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 5000;
            request.ReadWriteTimeout = 5000;
            request.UserAgent = "DonConnect/" + CurrentVersion;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static string JsonString(JObject json, string name, string fallback)
        {
            if (json == null || json[name] == null)
                return fallback;
            string value = json[name].ToString();
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static int CompareVersionNumbers(string left, string right)
        {
            List<int> a = ExtractVersionNumbers(left);
            List<int> b = ExtractVersionNumbers(right);
            int count = Math.Max(a.Count, b.Count);
            for (int i = 0; i < count; i++)
            {
                int av = i < a.Count ? a[i] : 0;
                int bv = i < b.Count ? b[i] : 0;
                if (av != bv)
                    return av.CompareTo(bv);
            }
            return 0;
        }

        private static List<int> ExtractVersionNumbers(string value)
        {
            var result = new List<int>();
            int current = 0;
            bool hasDigits = false;
            string text = value ?? "";
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch >= '0' && ch <= '9')
                {
                    hasDigits = true;
                    current = Math.Min(999999, current * 10 + (ch - '0'));
                }
                else if (hasDigits)
                {
                    result.Add(current);
                    current = 0;
                    hasDigits = false;
                }
            }

            if (hasDigits)
                result.Add(current);
            return result;
        }

        public void Start()
        {
            Start(false);
        }

        public void Start(bool force)
        {
            Stop();
            if (force)
                ClearRuntimeLease();

            if (!AcquireRuntimeLease())
                return;

            Logger.Info("Запуск DonConnect.");

            AddProvider(CreateDonationAlertsProvider());
            AddProvider(new GenericApiProvider(Settings, Logger));
            AddProvider(new StreamElementsProvider(Settings, Logger));
            AddProvider(new StreamlabsProvider(Settings, Logger));
            AddProvider(new DonatePayProvider(Settings, Logger, "donatepayru", "DonatePay RU", "https://donatepay.ru"));
            AddProvider(new DonatePayProvider(Settings, Logger, "donatepayeu", "DonatePay EU", "https://donatepay.eu"));
            AddProvider(new DonateStreamProvider(Settings, Logger));
            AddProvider(new DeStreamProvider(Settings, Logger));
            AddProvider(new DonateXProvider(Settings, Logger));
            AddProvider(new OdaProvider(Settings, Logger));
            WarnAboutProviderConflicts();

            foreach (var provider in Providers)
            {
                try
                {
                    provider.DonationReceived += HandleDonation;
                    provider.ConnectAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Logger.Warn(provider.ProviderName + ": провайдер не запущен. " + ex.Message);
                }
            }
        }

        public void Stop()
        {
            foreach (var provider in Providers)
            {
                try
                {
                    provider.DonationReceived -= HandleDonation;
                    provider.DisconnectAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Logger.Warn(provider.ProviderName + ": ошибка остановки. " + ex.Message);
                }
            }

            Providers.Clear();
            ReleaseRuntimeLease();
        }

        public void WarnAboutProviderConflicts()
        {
            if (Settings.GetBool("streamelements.enabled", false) && Settings.GetBool("donatepayeu.enabled", false))
                Logger.Warn("Включены StreamElements и DonatePay EU. Если DonatePay EU подключен внутри StreamElements, один донат может прийти дважды. DonConnect будет пытаться распознать такой дубль по сумме/валюте/сообщению.");

            if (Settings.GetBool("streamelements.enabled", false) && Settings.GetBool("donatepayru.enabled", false))
                Logger.Warn("Включены StreamElements и DonatePay RU. Если DonatePay RU подключен внутри StreamElements, один донат может прийти дважды. DonConnect будет пытаться распознать такой дубль.");

            if (Settings.GetBool("oda.enabled", false))
            {
                var duplicates = new List<string>();
                if (Settings.GetBool("donationalerts.enabled", false))
                    duplicates.Add("DonationAlerts");
                if (Settings.GetBool("donatepayeu.enabled", false))
                    duplicates.Add("DonatePay EU");
                if (Settings.GetBool("donatepayru.enabled", false))
                    duplicates.Add("DonatePay RU");
                if (Settings.GetBool("donatex.enabled", false))
                    duplicates.Add("DonateX.gg");
                if (duplicates.Count > 0)
                    Logger.Warn("ODA уже может агрегировать эти площадки: " + string.Join(", ", duplicates.ToArray()) + ". Если они включены и в DonConnect отдельно, возможны дубли. Антидубли включены, но лучше проверить тестовым донатом.");
            }
        }

        public void StartWidgetServer()
        {
            if (WidgetServer == null)
                WidgetServer = new DonConnectWidgetServer(Settings, Logger, HandleDonation);

            WidgetServer.Start();
            Logger.Info("Widget editor: " + WidgetServer.EditorUrl);
            Logger.Info("OBS widget URL: " + WidgetServer.WidgetUrl);
            Logger.Info("OBS dock URL: " + WidgetServer.DockUrl);
        }

        public void StopWidgetServer()
        {
            if (WidgetServer != null)
                WidgetServer.Stop();
        }

        public void OpenWidgetEditor()
        {
            if (WidgetServer == null || !WidgetServer.IsRunning)
                StartWidgetServer();

            try
            {
                DonConnectShell.OpenUrl(WidgetServer.EditorUrl);
            }
            catch (Exception ex)
            {
                Logger.Warn("Widget editor URL: " + WidgetServer.EditorUrl + ". Browser was not opened: " + ex.Message);
            }
        }

        public void ShowWidgetUrls()
        {
            if (WidgetServer == null || !WidgetServer.IsRunning)
                StartWidgetServer();

            ShowPopup("DonConnect widget editor:\n" + WidgetServer.EditorUrl + "\n\nOBS Browser Source URL:\n" + WidgetServer.WidgetUrl + "\n\nOBS Dock URL:\n" + WidgetServer.DockUrl);
        }

        public void SendWidgetTestDonation(string kind)
        {
            if (WidgetServer == null || !WidgetServer.IsRunning)
                StartWidgetServer();

            HandleDonation(CreateWidgetTestDonation(kind));
            Logger.Info("Widget test donation was sent.");
        }

        public void SendCurrentDonationToChat()
        {
            try
            {
                UnifiedDonationEvent e = CurrentDonationFromArgs();
                SendDonationChatNotification(e);
            }
            catch (Exception ex)
            {
                Logger.Warn("Chat notification was not sent. " + ex.Message);
            }
        }

        private UnifiedDonationEvent CreateWidgetTestDonation(string kind)
        {
            string mode = (kind ?? "").Trim().ToLowerInvariant();
            var e = new UnifiedDonationEvent
            {
                Source = "Widget Test",
                ProviderName = "Widget Test",
                EventType = "donation",
                DonationId = "widget-test-" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture),
                UserName = "\u0422\u0435\u0441\u0442\u043e\u0432\u044b\u0439 \u0434\u043e\u043d\u0430\u0442\u0435\u0440",
                Amount = 50,
                Currency = "RUB",
                Message = "\u042d\u0442\u043e \u0442\u0435\u0441\u0442 live preview.",
                Timestamp = DateTime.UtcNow,
                IsAnonymous = false,
                RawJson = "{\"source\":\"Widget Test\"}"
            };

            if (mode == "500")
            {
                e.Amount = 500;
                e.Message = "\u041a\u0440\u0443\u043f\u043d\u044b\u0439 \u0442\u0435\u0441\u0442\u043e\u0432\u044b\u0439 \u0434\u043e\u043d\u0430\u0442.";
            }
            else if (mode == "long")
            {
                e.Amount = 250;
                e.Message = "\u041e\u0447\u0435\u043d\u044c \u0434\u043b\u0438\u043d\u043d\u043e\u0435 \u0441\u043e\u043e\u0431\u0449\u0435\u043d\u0438\u0435, \u0447\u0442\u043e\u0431\u044b \u043f\u0440\u043e\u0432\u0435\u0440\u0438\u0442\u044c, \u043a\u0430\u043a \u0432\u0438\u0434\u0436\u0435\u0442 \u0432\u0435\u0434\u0435\u0442 \u0441\u0435\u0431\u044f \u0432 OBS Browser Source \u043d\u0430 \u0440\u0430\u0437\u043d\u044b\u0445 \u0440\u0430\u0437\u043c\u0435\u0440\u0430\u0445.";
            }
            else if (mode == "anonymous")
            {
                e.UserName = "Anonymous";
                e.Amount = 100;
                e.Message = "\u0410\u043d\u043e\u043d\u0438\u043c\u043d\u044b\u0439 \u0442\u0435\u0441\u0442\u043e\u0432\u044b\u0439 \u0434\u043e\u043d\u0430\u0442.";
                e.IsAnonymous = true;
            }

            return e;
        }

        private bool AcquireRuntimeLease()
        {
            string owner = Settings.Get("runtime.owner", "");
            string rawUntil = Settings.Get("runtime.lockUntil", "");
            DateTime lockUntil;
            bool hasActiveOwner = DateTime.TryParse(rawUntil, null, DateTimeStyles.RoundtripKind, out lockUntil)
                && lockUntil.ToUniversalTime() > DateTime.UtcNow
                && !string.IsNullOrWhiteSpace(owner)
                && owner != InstanceId;

            if (hasActiveOwner)
            {
                Logger.Warn("DonConnect уже запущен другим экземпляром. Новый запуск пропущен, чтобы не было дублей.");
                return false;
            }

            Settings.Set("runtime.owner", InstanceId, true);
            RefreshRuntimeLease();

            RuntimeLeaseCancellation = new CancellationTokenSource();
            var token = RuntimeLeaseCancellation.Token;
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        RefreshRuntimeLease();
                        await Task.Delay(TimeSpan.FromSeconds(15), token);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Runtime lease refresh failed. " + ex.Message);
                    }
                }
            }, token);

            return true;
        }

        private void ClearRuntimeLease()
        {
            Settings.Set("runtime.owner", "", true);
            Settings.Set("runtime.lockUntil", "", true);
        }

        private void RefreshRuntimeLease()
        {
            Settings.Set("runtime.lockUntil", DateTime.UtcNow.AddSeconds(45).ToString("o"), true);
        }

        private void ReleaseRuntimeLease()
        {
            try
            {
                if (RuntimeLeaseCancellation != null)
                    RuntimeLeaseCancellation.Cancel();
            }
            catch { }

            string owner = Settings.Get("runtime.owner", "");
            if (owner == InstanceId)
            {
                Settings.Set("runtime.owner", "", true);
                Settings.Set("runtime.lockUntil", "", true);
            }
        }

        public void RegisterCustomTriggers()
        {
            RegisterTrigger("Any donation", "donconnect.donation.any", "DonConnect", "Donations");
            RegisterTrigger("DonationAlerts donation", "donconnect.donation.donationalerts", "DonConnect", "Donations");
            RegisterTrigger("DonatePay donation", "donconnect.donation.donatepay", "DonConnect", "Donations");
            RegisterTrigger("DonatePay RU donation", "donconnect.donation.donatepayru", "DonConnect", "Donations");
            RegisterTrigger("DonatePay EU donation", "donconnect.donation.donatepayeu", "DonConnect", "Donations");
            RegisterTrigger("StreamElements donation", "donconnect.donation.streamelements", "DonConnect", "Donations");
            RegisterTrigger("Streamlabs donation", "donconnect.donation.streamlabs", "DonConnect", "Donations");
            RegisterTrigger("Generic API donation", "donconnect.donation.genericapi", "DonConnect", "Donations");
            RegisterTrigger("Donate.Stream donation", "donconnect.donation.donatestream", "DonConnect", "Donations");
            RegisterTrigger("deStream donation", "donconnect.donation.destream", "DonConnect", "Donations");
            RegisterTrigger("DonateX.gg donation", "donconnect.donation.donatex", "DonConnect", "Donations");
            RegisterTrigger("ODA donation", "donconnect.donation.oda", "DonConnect", "Donations");
        }

        private void RegisterTrigger(string triggerName, string eventName, params string[] categories)
        {
            try
            {
                CPH.RegisterCustomTrigger(triggerName, eventName, categories);
            }
            catch (Exception ex)
            {
                Logger.Warn("Custom trigger registration failed: " + triggerName + ". " + ex.Message);
            }
        }

        public void LogStatus()
        {
            Logger.Info("Status: DonationAlerts enabled=" + Settings.GetBool("donationalerts.enabled", false)
                + ", mode=" + Settings.Get("donationalerts.authMode", "own")
                + ", token=" + (string.IsNullOrWhiteSpace(Settings.Get("donationalerts.accessToken", "")) ? "missing" : "saved"));
            Logger.Info("Status: StreamElements enabled=" + Settings.GetBool("streamelements.enabled", false)
                + ", accountId=" + (string.IsNullOrWhiteSpace(Settings.Get("streamelements.accountId", "")) ? "missing" : "saved")
                + ", jwtToken=" + (string.IsNullOrWhiteSpace(Settings.Get("streamelements.jwtToken", "")) ? "missing" : "saved"));
            Logger.Info("Status: Streamlabs enabled=" + Settings.GetBool("streamlabs.enabled", false)
                + ", token=" + (string.IsNullOrWhiteSpace(Settings.Get("streamlabs.token", "")) ? "missing" : "saved"));
            Logger.Info("Status: DonatePay enabled=" + Settings.GetBool("donatepay.enabled", false)
                + ", apiKey=" + (string.IsNullOrWhiteSpace(Settings.Get("donatepay.apiKey", "")) ? "missing" : "saved"));
            Logger.Info("Status: Donate.Stream enabled=" + Settings.GetBool("donatestream.enabled", false)
                + ", token=" + (string.IsNullOrWhiteSpace(Settings.Get("donatestream.token", "")) ? "missing" : "saved"));
            Logger.Info("Status: deStream enabled=" + Settings.GetBool("destream.enabled", false)
                + ", accessToken=" + (string.IsNullOrWhiteSpace(Settings.Get("destream.accessToken", "")) ? "missing" : "saved"));
            Logger.Info("Status: DonateX enabled=" + Settings.GetBool("donatex.enabled", false)
                + ", accessToken=" + (string.IsNullOrWhiteSpace(Settings.Get("donatex.accessToken", "")) ? "missing" : "saved"));
            Logger.Info("Status: ODA enabled=" + Settings.GetBool("oda.enabled", false)
                + ", accessToken=" + (string.IsNullOrWhiteSpace(Settings.Get("oda.accessToken", "")) ? "missing" : "saved")
                + ", apiBase=" + Settings.Get("oda.apiBase", "https://api.oda.digital"));
            Logger.Info("Status: Generic API enabled=" + Settings.GetBool("generic.enabled", false)
                + ", endpoint=" + (string.IsNullOrWhiteSpace(Settings.Get("generic.endpoint", "")) ? "missing" : Settings.Get("generic.endpoint", "")));
            Logger.Info("Status: Streamer.bot Credits enabled=" + Settings.GetBool("STREAMERBOT_CREDITS_ENABLED", false)
                + ", url=" + Settings.Get("STREAMERBOT_HTTP_URL", "http://127.0.0.1:7474")
                + ", action=" + Settings.Get("STREAMERBOT_CREDITS_ACTION", "Add Donation To Credits")
                + ", section=" + Settings.Get("STREAMERBOT_CREDITS_SECTION", "\u0414\u043e\u043d\u0430\u0442\u044b")
                + ", fields=" + Settings.Get("STREAMERBOT_CREDITS_FIELDS", "name,amount"));
        }

        public void ShowStatusPopup()
        {
            var lines = new List<string>();
            lines.Add("DonConnect status");
            lines.Add("Bridge: " + (IsRuntimeLeaseActive() ? "running" : "stopped"));
            lines.Add("");
            lines.Add(StatusLine("DonationAlerts", Settings.GetBool("donationalerts.enabled", false),
                "mode=" + Settings.Get("donationalerts.authMode", "own")
                + ", token=" + SavedMissing(Settings.Get("donationalerts.accessToken", ""))));
            lines.Add(StatusLine("StreamElements", Settings.GetBool("streamelements.enabled", false),
                "accountId=" + SavedMissing(Settings.Get("streamelements.accountId", ""))
                + ", jwtToken=" + SavedMissing(Settings.Get("streamelements.jwtToken", ""))));
            lines.Add(StatusLine("Streamlabs", Settings.GetBool("streamlabs.enabled", false),
                "token=" + SavedMissing(Settings.Get("streamlabs.token", ""))));
            lines.Add(StatusLine("DonatePay RU", Settings.GetBool("donatepayru.enabled", false),
                "apiKey=" + SavedMissing(Settings.Get("donatepayru.apiKey", ""))));
            lines.Add(StatusLine("DonatePay EU", Settings.GetBool("donatepayeu.enabled", false),
                "apiKey=" + SavedMissing(Settings.Get("donatepayeu.apiKey", ""))));
            lines.Add(StatusLine("Donate.Stream", Settings.GetBool("donatestream.enabled", false),
                "token=" + SavedMissing(Settings.Get("donatestream.token", ""))));
            lines.Add(StatusLine("deStream", Settings.GetBool("destream.enabled", false),
                "clientId=" + SavedMissing(Settings.Get("destream.clientId", ""))
                + ", accessToken=" + SavedMissing(Settings.Get("destream.accessToken", ""))));
            lines.Add(StatusLine("DonateX.gg", Settings.GetBool("donatex.enabled", false),
                "accessToken=" + SavedMissing(Settings.Get("donatex.accessToken", ""))
                + ", apiBase=" + Settings.Get("donatex.apiBase", "https://donatex.gg/api")));
            lines.Add(StatusLine("ODA", Settings.GetBool("oda.enabled", false),
                "accessToken=" + SavedMissing(Settings.Get("oda.accessToken", ""))
                + ", apiBase=" + Settings.Get("oda.apiBase", "https://api.oda.digital")));
            lines.Add(StatusLine("Generic API", Settings.GetBool("generic.enabled", false),
                "endpoint=" + SavedMissing(Settings.Get("generic.endpoint", ""))));
            lines.Add(StatusLine("Credits", Settings.GetBool("STREAMERBOT_CREDITS_ENABLED", false),
                "section=" + Settings.Get("STREAMERBOT_CREDITS_SECTION", "\u0414\u043e\u043d\u0430\u0442\u044b")
                + ", fields=" + Settings.Get("STREAMERBOT_CREDITS_FIELDS", "name,amount")));

            ShowPopup(string.Join(Environment.NewLine, lines.ToArray()));
        }

        public void ShowDiagnosticsPopup()
        {
            var lines = new List<string>();
            lines.Add("DonConnect provider diagnostics");

            int enabledCount = 0;
            AddProviderHealth(lines, ref enabledCount, "DonationAlerts", "donationalerts", "accessToken");
            AddProviderHealth(lines, ref enabledCount, "StreamElements", "streamelements", "accountId", "jwtToken");
            AddProviderHealth(lines, ref enabledCount, "Streamlabs", "streamlabs", "token");
            AddProviderHealth(lines, ref enabledCount, "DonatePay RU", "donatepayru", "apiKey");
            AddProviderHealth(lines, ref enabledCount, "DonatePay EU", "donatepayeu", "apiKey");
            AddProviderHealth(lines, ref enabledCount, "Donate.Stream", "donatestream");
            AddProviderHealth(lines, ref enabledCount, "deStream", "destream", "clientId", "accessToken");
            AddProviderHealth(lines, ref enabledCount, "DonateX.gg", "donatex", "accessToken");
            AddProviderHealth(lines, ref enabledCount, "ODA", "oda", "accessToken");
            AddProviderHealth(lines, ref enabledCount, "Generic API", "generic", "endpoint");

            if (enabledCount == 0)
                lines.Add("No providers enabled.");

            ShowPopup(string.Join(Environment.NewLine, lines.ToArray()));
        }

        private void AddProviderHealth(List<string> lines, ref int enabledCount, string displayName, string key, params string[] requiredSettings)
        {
            if (!Settings.GetBool(key + ".enabled", false))
                return;

            enabledCount++;
            lines.Add(displayName + " - " + ProviderHealth(key, requiredSettings));
        }

        private string ProviderHealth(string key, string[] requiredSettings)
        {
            string missing = MissingRequiredSettings(key, requiredSettings);
            if (!string.IsNullOrWhiteSpace(missing))
                return "Enabled/Disconnected(missing " + missing + ")";

            string connection = Settings.Get(key + ".diagnostics.connection", "");
            string polling = Settings.Get(key + ".diagnostics.polling", "");
            string error = Settings.Get(key + ".diagnostics.lastError", "");
            string reason = ProviderDisconnectReason(connection, polling, error);

            if (!string.IsNullOrWhiteSpace(reason))
                return "Enabled/Disconnected(" + reason + ")";

            if (connection.Equals("connecting", StringComparison.OrdinalIgnoreCase))
                return "Enabled/Connecting";

            if (!IsRuntimeLeaseActive())
                return "Enabled/Disconnected(bridge stopped)";

            return "Enabled/Connected";
        }

        private string MissingRequiredSettings(string key, string[] requiredSettings)
        {
            if (requiredSettings == null || requiredSettings.Length == 0)
                return "";

            var missing = new List<string>();
            foreach (string settingName in requiredSettings)
            {
                if (string.IsNullOrWhiteSpace(Settings.Get(key + "." + settingName, "")))
                    missing.Add(FriendlySettingName(settingName));
            }

            return string.Join(", ", missing.ToArray());
        }

        private string FriendlySettingName(string settingName)
        {
            if (settingName == "accessToken")
                return "access token";
            if (settingName == "jwtToken")
                return "JWT token";
            if (settingName == "apiKey")
                return "API key";
            if (settingName == "accountId")
                return "account ID";
            if (settingName == "clientId")
                return "client ID";
            return settingName;
        }

        private string ProviderDisconnectReason(string connection, string polling, string error)
        {
            if (!IsNoneValue(error))
                return NormalizeDiagnosticReason(error);

            if (!string.IsNullOrWhiteSpace(polling) && polling.IndexOf("invalid-token", StringComparison.OrdinalIgnoreCase) >= 0)
                return "wrong token";

            if (connection.Equals("disconnected", StringComparison.OrdinalIgnoreCase))
                return "connection lost";

            if (connection.Equals("stopped", StringComparison.OrdinalIgnoreCase) && IsRuntimeLeaseActive())
                return "stopped";

            return "";
        }

        private bool IsNoneValue(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                || value.Equals("none", StringComparison.OrdinalIgnoreCase)
                || value.Equals("n/a", StringComparison.OrdinalIgnoreCase);
        }

        private string NormalizeDiagnosticReason(string value)
        {
            value = ShortError(value);
            string lower = value.ToLowerInvariant();

            if (lower.IndexOf("incorrect token", StringComparison.OrdinalIgnoreCase) >= 0
                || lower.IndexOf("invalid token", StringComparison.OrdinalIgnoreCase) >= 0
                || lower.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0
                || lower.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0)
                return "wrong token";

            if (lower.IndexOf("forbidden", StringComparison.OrdinalIgnoreCase) >= 0
                || lower.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0)
                return "no permission";

            if (lower.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                || lower.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                return "timeout";

            if (lower.IndexOf("no such host", StringComparison.OrdinalIgnoreCase) >= 0
                || lower.IndexOf("name resolution", StringComparison.OrdinalIgnoreCase) >= 0)
                return "network/DNS error";

            if (lower.IndexOf("connection refused", StringComparison.OrdinalIgnoreCase) >= 0)
                return "connection refused";

            return value;
        }

        private void AddProviderDiagnostics(List<string> lines, string displayName, string key, string secretKey)
        {
            string enabled = Settings.GetBool(key + ".enabled", false) ? "on" : "off";
            string token = SavedMissing(Settings.Get(key + "." + secretKey, ""));
            string polling = Settings.Get(key + ".diagnostics.polling", "not started");
            string fetch = Settings.Get(key + ".diagnostics.lastFetchCount", "n/a");
            string newCount = Settings.Get(key + ".diagnostics.lastNewCount", "n/a");
            string seen = Settings.Get(key + ".diagnostics.seenCount", "n/a");
            string retry = ShortTime(Settings.Get(key + ".diagnostics.nextRetryAt", "n/a"));
            string donation = ShortTime(Settings.Get(key + ".diagnostics.lastDonationAt", "never"));
            string error = ShortError(Settings.Get(key + ".diagnostics.lastError", "none"));

            lines.Add(displayName + ": " + enabled + ", " + secretKey + "=" + token + ", polling=" + polling);
            lines.Add("  fetch=" + fetch + ", new=" + newCount + ", seen=" + seen + ", retry=" + retry + ", donation=" + donation + ", error=" + error);
            if (key == "donatepayru" || key == "donatepayeu")
            {
                string host = Settings.Get(key + ".apiHost", key == "donatepayeu" ? "https://donatepay.eu" : "https://donatepay.ru");
                string root = Settings.Get(key + ".diagnostics.root", "");
                string shape = Settings.Get(key + ".diagnostics.firstItemShape", "");
                string skipped = Settings.Get(key + ".diagnostics.skipped", "");
                string apiStatus = Settings.Get(key + ".diagnostics.apiStatus", "");
                string apiMessage = Settings.Get(key + ".diagnostics.apiMessage", "");
                lines.Add("  host=" + ShortError(host));
                if (!string.IsNullOrWhiteSpace(root) || !string.IsNullOrWhiteSpace(shape) || !string.IsNullOrWhiteSpace(skipped))
                    lines.Add("  root=" + ShortError(root) + ", first=" + ShortError(shape) + ", skipped=" + ShortError(skipped));
                if (!string.IsNullOrWhiteSpace(apiStatus) || !string.IsNullOrWhiteSpace(apiMessage))
                    lines.Add("  apiStatus=" + ShortError(apiStatus) + ", apiMessage=" + ShortError(apiMessage));
            }
        }

        private string Short(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "none")
                return "none";
            return value.Length <= 8 ? value : value.Substring(0, 8);
        }

        private string ShortTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "never" || value == "n/a" || value == "now" || value == "none")
                return value;
            DateTime parsed;
            if (DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out parsed))
                return parsed.ToLocalTime().ToString("HH:mm:ss");
            return Short(value);
        }

        private string ShortError(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "none";
            value = value.Replace(Environment.NewLine, " ");
            return value.Length <= 90 ? value : value.Substring(0, 87) + "...";
        }

        private string StatusLine(string name, bool enabled, string details)
        {
            return name + ": " + (enabled ? "enabled" : "disabled") + " (" + details + ")";
        }

        private string SavedMissing(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "missing" : "saved";
        }

        private bool IsRuntimeLeaseActive()
        {
            string owner = Settings.Get("runtime.owner", "");
            string rawUntil = Settings.Get("runtime.lockUntil", "");
            DateTime lockUntil;
            return owner == InstanceId
                && DateTime.TryParse(rawUntil, null, DateTimeStyles.RoundtripKind, out lockUntil)
                && lockUntil.ToUniversalTime() > DateTime.UtcNow;
        }

        public bool AuthorizeDonationAlerts()
        {
            var provider = CreateDonationAlertsProvider();
            return provider.RunOAuthFlow();
        }

        public void ShowPopupBase64(string utf8Base64)
        {
            try
            {
                string message = Encoding.UTF8.GetString(Convert.FromBase64String(utf8Base64));
                ShowPopup(message);
            }
            catch (Exception ex)
            {
                Logger.Warn("Popup error: " + ex.Message);
            }
        }

        public void ShowPopup(string message)
        {
            try
            {
                System.Windows.Forms.MessageBox.Show(message ?? "", "DonConnect");
            }
            catch (Exception ex)
            {
                Logger.Warn("Popup error: " + ex.Message);
            }
        }

        public DonationAlertsProvider CreateDonationAlertsProvider()
        {
            return new DonationAlertsProvider(Settings, Logger, BundledDonationAlertsClientId, BundledDonationAlertsClientSecret);
        }

        public void WriteCreditsOverlayConfig(string configPath, string title, string subtitle, string outro, string duration, string accent, string text, string muted, string background, string font, string donationFields, string labels, string hideSections, string showSections)
        {
            if (string.IsNullOrWhiteSpace(configPath))
                return;

            try
            {
                var config = new JObject();
                config["title"] = title ?? "";
                config["subtitle"] = subtitle ?? "";
                config["outro"] = outro ?? "";
                config["duration"] = duration ?? "90s";
                config["donationFields"] = donationFields ?? "name,amount";

                var style = new JObject();
                style["bg"] = background ?? "transparent";
                style["text"] = text ?? "#f7f4ec";
                style["muted"] = muted ?? "#b9d8d2";
                style["accent"] = accent ?? "#ffcf5a";
                style["font"] = font ?? "Segoe UI, Arial, sans-serif";
                config["style"] = style;

                var labelObject = new JObject();
                foreach (var pair in ParseKeyValueList(labels))
                    labelObject[pair.Key] = pair.Value;
                config["sectionLabels"] = labelObject;
                config["hideSections"] = ParseStringList(hideSections);
                config["showSections"] = ParseStringList(showSections);

                string directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(configPath, config.ToString(Formatting.Indented), new UTF8Encoding(false));
                Logger.Info("Credits overlay config written: " + configPath);
            }
            catch (Exception ex)
            {
                Logger.Warn("Credits overlay config was not written. " + ex.Message);
            }
        }

        private Dictionary<string, string> ParseKeyValueList(string value)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value))
                return result;

            string[] items = value.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string item in items)
            {
                int index = item.IndexOf('=');
                if (index <= 0)
                    continue;
                string key = item.Substring(0, index).Trim();
                string val = item.Substring(index + 1).Trim();
                if (!string.IsNullOrWhiteSpace(key))
                    result[key] = val;
            }

            return result;
        }

        private JArray ParseStringList(string value)
        {
            var result = new JArray();
            if (string.IsNullOrWhiteSpace(value))
                return result;

            string[] items = value.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string item in items)
            {
                string trimmed = item.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    result.Add(trimmed);
            }

            return result;
        }

        public void HandleDonation(UnifiedDonationEvent donationEvent)
        {
            if (donationEvent == null)
                return;

            if (Deduplicator.Seen(donationEvent) || SeenGlobally(donationEvent))
            {
                Logger.Debug("Повторное событие пропущено: " + donationEvent.ProviderName + " / " + donationEvent.DonationId);
                return;
            }

            if (SeenProxyDuplicate(donationEvent))
            {
                Logger.Warn("Похожий дубль от прокси-площадки пропущен: " + donationEvent.ProviderName + " / " + donationEvent.DonationId);
                return;
            }

            ExportDonation(donationEvent);
            UpdateDonationGoal(donationEvent);
            UpdateDonationTimer(donationEvent);
            SaveDonationDiagnostics(donationEvent);
            TriggerDonationEvents(donationEvent);
            SendDonationToCredits(donationEvent);
            if (WidgetServer != null && WidgetServer.IsRunning)
                WidgetServer.PushDonation(donationEvent);
        }

        private void SendDonationChatNotification(UnifiedDonationEvent e)
        {
            if (e == null)
                return;

            string defaultTemplate = "\u0421\u043f\u0430\u0441\u0438\u0431\u043e {user} \u0437\u0430 \u0434\u043e\u043d\u0430\u0442 {amount} {currency}! {message}";
            string template = ReadRuntimeArg("chatNotificationTemplate", Settings.Get("chatNotification.template", defaultTemplate));
            if (IsBrokenImportedTemplate(template))
                template = defaultTemplate;
            if (!string.IsNullOrWhiteSpace(template))
                Settings.Set("chatNotification.template", template, true);
            string message = ApplyDonationTemplate(template, e);
            if (string.IsNullOrWhiteSpace(message))
                return;

            CPH.SendMessage(message);
            Logger.Info("Chat notification sent for donation " + (e.DonationId ?? ""));
        }

        private bool IsBrokenImportedTemplate(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
                return true;

            int questionMarks = 0;
            foreach (char c in template)
                if (c == '?')
                    questionMarks++;

            return questionMarks >= 4 && template.IndexOf("{user}", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private UnifiedDonationEvent CurrentDonationFromArgs()
        {
            string source = ReadRuntimeArg("donationSource", ReadRuntimeArg("tipSource", Settings.Get("lastDonation.source", "")));
            string provider = ReadRuntimeArg("donationProvider", source);
            string user = ReadRuntimeArg("donationUser", ReadRuntimeArg("tipUser", Settings.Get("lastDonation.user", "Anonymous")));
            string amountText = ReadRuntimeArg("donationAmount", ReadRuntimeArg("tipAmount", Settings.Get("lastDonation.amount", "0")));
            string currency = ReadRuntimeArg("donationCurrency", ReadRuntimeArg("tipCurrency", Settings.Get("lastDonation.currency", "")));
            string message = ReadRuntimeArg("donationMessage", ReadRuntimeArg("tipMessage", Settings.Get("lastDonation.message", "")));
            string id = ReadRuntimeArg("donationId", Settings.Get("lastDonation.id", ""));
            string raw = ReadRuntimeArg("donationRawJson", "");
            decimal amount;
            decimal.TryParse(amountText, NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
            return new UnifiedDonationEvent
            {
                Source = source,
                ProviderName = provider,
                EventType = "donation",
                DonationId = id,
                UserName = user,
                Amount = amount,
                Currency = currency,
                Message = message,
                Timestamp = DateTime.UtcNow,
                RawJson = raw,
                IsAnonymous = string.IsNullOrWhiteSpace(user) || user.Equals("Anonymous", StringComparison.OrdinalIgnoreCase)
            };
        }

        private string ReadRuntimeArg(string name, string fallback)
        {
            try
            {
                if (Args != null && Args.ContainsKey(name) && Args[name] != null)
                    return Args[name].ToString();
            }
            catch { }

            try
            {
                string global = CPH.GetGlobalVar<string>(name, true);
                if (!string.IsNullOrWhiteSpace(global))
                    return global;
            }
            catch { }

            return fallback ?? "";
        }

        private string ApplyDonationTemplate(string template, UnifiedDonationEvent e)
        {
            string text = template ?? "";
            text = text.Replace("{source}", e.Source ?? e.ProviderName ?? "");
            text = text.Replace("{provider}", e.ProviderName ?? e.Source ?? "");
            text = text.Replace("{user}", string.IsNullOrWhiteSpace(e.UserName) ? "Anonymous" : e.UserName);
            text = text.Replace("{donor}", string.IsNullOrWhiteSpace(e.UserName) ? "Anonymous" : e.UserName);
            text = text.Replace("{amount}", e.Amount.ToString("0.##", CultureInfo.InvariantCulture));
            text = text.Replace("{currency}", e.Currency ?? "");
            text = text.Replace("{message}", e.Message ?? "");
            text = text.Replace("{id}", e.DonationId ?? "");
            return text.Trim();
        }

        public void HandleGoalOnly(UnifiedDonationEvent donationEvent)
        {
            UpdateDonationGoal(donationEvent);
        }

        public void RefreshDonationTimerVariables()
        {
            UpdateDonationTimer(null);
        }

        public void AddTimerContribution(decimal amount, string currency, decimal seconds, string source)
        {
            if (seconds > 0)
            {
                DateTime now = DateTime.UtcNow;
                DateTime endsAt = ReadDateSetting("timer.endsAt", now);
                if (endsAt < now)
                    endsAt = now;
                Settings.Set("timer.endsAt", endsAt.AddSeconds((double)seconds).ToString("o"), true);
                if (Settings.Get("timer.mode", "countdown").Equals("countup-reset", StringComparison.OrdinalIgnoreCase))
                    Settings.Set("timer.startedAt", now.ToString("o"), true);
            }

            if (amount > 0)
            {
                UpdateDonationTimer(new UnifiedDonationEvent
                {
                    Source = source ?? "Streamer.bot event",
                    ProviderName = source ?? "Streamer.bot event",
                    UserName = "Streamer.bot event",
                    Amount = amount,
                    Currency = currency ?? Settings.Get("timer.currency", "RUB"),
                    Timestamp = DateTime.UtcNow
                });
            }
            else
            {
                UpdateDonationTimer(null);
            }
        }

        private void SaveDonationDiagnostics(UnifiedDonationEvent e)
        {
            string providerKey = NormalizeProviderKey(e.ProviderName);
            string summary = (e.ProviderName ?? e.Source ?? "unknown")
                + " / " + (e.UserName ?? "Anonymous")
                + " / " + e.Amount.ToString(CultureInfo.InvariantCulture)
                + " " + (e.Currency ?? "")
                + " / id=" + (e.DonationId ?? "");
            Settings.Set("diagnostics.lastDonation", summary, true);
            if (!string.IsNullOrWhiteSpace(providerKey))
            {
                Settings.Set(providerKey + ".diagnostics.lastDonationAt", DateTime.UtcNow.ToString("o"), true);
                Settings.Set(providerKey + ".diagnostics.lastDonation", summary, true);
            }
        }

        private void UpdateDonationGoal(UnifiedDonationEvent e)
        {
            if (!Settings.GetBool("goal.enabled", true))
                return;

            decimal current = ReadDecimalSetting("goal.current", 0);
            decimal target = ReadDecimalSetting("goal.target", 0);
            string currency = FirstNonEmptyLocal(Settings.Get("goal.currency", ""), e.Currency ?? "");
            decimal convertedAmount = ConvertDonationAmount(e, currency);
            decimal next = current + convertedAmount;
            Settings.Set("goal.current", next.ToString(CultureInfo.InvariantCulture), true);

            if (!string.IsNullOrWhiteSpace(currency))
                Settings.Set("goal.currency", currency, true);

            string title = FirstNonEmptyLocal(Settings.Get("goal.title", ""), "\u0421\u0431\u043e\u0440");
            decimal remaining = target > next ? target - next : 0;
            decimal percent = target > 0 ? Math.Min(100, Math.Round((next / target) * 100, 2)) : 0;

            string currentText = FormatAmount(next, currency);
            string targetText = target > 0 ? FormatAmount(target, currency) : "";
            string remainingText = FormatAmount(remaining, currency);
            string percentText = percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";
            string summary = target > 0
                ? currentText + " / " + targetText + " (" + percentText + ")"
                : currentText;

            SetGoalVar("donConnectGoalTitle", title);
            SetGoalVar("donConnectGoalCurrent", next.ToString(CultureInfo.InvariantCulture));
            SetGoalVar("donConnectGoalTarget", target.ToString(CultureInfo.InvariantCulture));
            SetGoalVar("donConnectGoalRemaining", remaining.ToString(CultureInfo.InvariantCulture));
            SetGoalVar("donConnectGoalPercent", percent.ToString("0.##", CultureInfo.InvariantCulture));
            SetGoalVar("donConnectGoalCurrency", currency);
            SetGoalVar("donConnectGoalCurrentText", currentText);
            SetGoalVar("donConnectGoalTargetText", targetText);
            SetGoalVar("donConnectGoalRemainingText", remainingText);
            SetGoalVar("donConnectGoalPercentText", percentText);
            SetGoalVar("donConnectGoalSummary", summary);
            SetGoalVar("donConnectLastDonationUser", string.IsNullOrWhiteSpace(e.UserName) ? "Anonymous" : e.UserName);
            SetGoalVar("donConnectLastDonationAmount", e.Amount.ToString(CultureInfo.InvariantCulture));
            SetGoalVar("donConnectLastDonationCurrency", e.Currency ?? "");
            SetGoalVar("donConnectLastDonationConvertedAmount", convertedAmount.ToString(CultureInfo.InvariantCulture));
            SetGoalVar("donConnectLastDonationConvertedCurrency", currency);
            SetGoalVar("donConnectLastDonationPlatform", e.ProviderName ?? e.Source ?? "");
            WriteGoalTimerOverlayState();
        }

        private void UpdateDonationTimer(UnifiedDonationEvent e)
        {
            if (!Settings.GetBool("timer.enabled", false))
                return;

            DateTime now = DateTime.UtcNow;
            string timerMode = Settings.Get("timer.mode", "countdown").Trim().ToLowerInvariant();
            DateTime endsAt = ReadDateSetting("timer.endsAt", now);
            DateTime startedAt = ReadDateSetting("timer.startedAt", now);
            if (endsAt < now)
                endsAt = now;

            double addedSeconds = 0;
            if (e != null && e.Amount > 0)
            {
                decimal unitAmount = ReadDecimalSetting("timer.unitAmount", 100);
                decimal secondsPerUnit = ReadDecimalSetting("timer.secondsPerUnit", 60);
                string timerCurrency = FirstNonEmptyLocal(Settings.Get("timer.currency", ""), e.Currency ?? "");
                decimal convertedAmount = ConvertDonationAmount(e, timerCurrency);
                if (unitAmount > 0 && secondsPerUnit > 0)
                    addedSeconds = (double)(convertedAmount / unitAmount * secondsPerUnit);

                if (addedSeconds > 0)
                    endsAt = endsAt.AddSeconds(addedSeconds);

                if (timerMode == "countup-reset")
                    startedAt = now;
            }

            double maxSeconds = (double)ReadDecimalSetting("timer.maxSeconds", 0);
            if (maxSeconds > 0 && (endsAt - now).TotalSeconds > maxSeconds)
                endsAt = now.AddSeconds(maxSeconds);

            double remainingSeconds = timerMode == "countup-reset"
                ? Math.Max(0, (now - startedAt).TotalSeconds)
                : Math.Max(0, (endsAt - now).TotalSeconds);
            Settings.Set("timer.endsAt", endsAt.ToString("o"), true);
            Settings.Set("timer.startedAt", startedAt.ToString("o"), true);

            string title = FirstNonEmptyLocal(Settings.Get("timer.title", ""), "\u0414\u043e\u043d\u0430\u0442\u043d\u044b\u0439 \u0442\u0430\u0439\u043c\u0435\u0440");
            string timerText = FormatDuration(remainingSeconds);
            string addedText = FormatDuration(addedSeconds);

            SetGoalVar("donConnectTimerTitle", title);
            SetGoalVar("donConnectTimerSeconds", Math.Floor(remainingSeconds).ToString(CultureInfo.InvariantCulture));
            SetGoalVar("donConnectTimerText", timerText);
            SetGoalVar("donConnectTimerEndsAt", endsAt.ToString("o"));
            SetGoalVar("donConnectTimerStartedAt", startedAt.ToString("o"));
            SetGoalVar("donConnectTimerMode", timerMode);
            SetGoalVar("donConnectTimerAddedSeconds", Math.Floor(addedSeconds).ToString(CultureInfo.InvariantCulture));
            SetGoalVar("donConnectTimerAddedText", addedText);
            SetGoalVar("donConnectTimerSummary", title + ": " + timerText);
            if (e != null)
            {
                SetGoalVar("donConnectTimerLastDonationUser", string.IsNullOrWhiteSpace(e.UserName) ? "Anonymous" : e.UserName);
                SetGoalVar("donConnectTimerLastDonationAmount", e.Amount.ToString(CultureInfo.InvariantCulture));
                SetGoalVar("donConnectTimerLastDonationPlatform", e.ProviderName ?? e.Source ?? "");
            }
            WriteGoalTimerOverlayState();
        }

        public void WriteGoalTimerOverlayConfig(string configPath, string mode, string title, string font, string bg, string text, string muted, string accent, string barBg, string radius, string scale, string bare, string showServices, string width, string padding, string titleSize, string valueSize, string labelSize, string metaSize, string shadow, string goalFormat, string servicesTitle)
        {
            if (string.IsNullOrWhiteSpace(configPath))
                return;

            try
            {
                var config = new JObject();
                config["mode"] = mode ?? "both";
                config["title"] = title ?? "DonConnect";
                config["refreshMs"] = 1000;
                config["showLastDonation"] = true;
                config["showServices"] = ParseBoolString(showServices, true);
                config["bare"] = ParseBoolString(bare, false);
                config["goalFormat"] = goalFormat ?? "amount";
                config["servicesTitle"] = servicesTitle ?? "Подключено";
                var style = new JObject();
                style["font"] = font ?? "Segoe UI, Arial, sans-serif";
                style["bg"] = bg ?? "rgba(10, 12, 18, 0.72)";
                style["text"] = text ?? "#ffffff";
                style["muted"] = muted ?? "#b8c0cc";
                style["accent"] = accent ?? "#35d07f";
                style["barBg"] = barBg ?? "rgba(255,255,255,0.18)";
                style["radius"] = radius ?? "8px";
                style["scale"] = scale ?? "1";
                style["width"] = width ?? "920px";
                style["padding"] = padding ?? "22px";
                style["titleSize"] = titleSize ?? "26px";
                style["valueSize"] = valueSize ?? "38px";
                style["labelSize"] = labelSize ?? "15px";
                style["metaSize"] = metaSize ?? "16px";
                style["shadow"] = shadow ?? "0 16px 44px rgba(0,0,0,0.28)";
                config["style"] = style;

                string directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(configPath, config.ToString(Formatting.Indented), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Logger.Warn("Goal/Timer overlay config was not written. " + ex.Message);
            }
        }

        private bool ParseBoolString(string value, bool fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            bool parsed;
            if (bool.TryParse(value, out parsed))
                return parsed;
            return value.Trim() == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        public void WriteGoalTimerOverlayState()
        {
            string statePath = Settings.Get("overlay.statePath", DefaultGoalTimerOverlayStatePath());
            if (string.IsNullOrWhiteSpace(statePath))
                return;

            try
            {
                var root = new JObject();
                root["updatedAt"] = DateTime.UtcNow.ToString("o");

                var goal = new JObject();
                goal["title"] = CPH.GetGlobalVar<string>("donConnectGoalTitle", true) ?? "";
                goal["current"] = CPH.GetGlobalVar<string>("donConnectGoalCurrent", true) ?? "0";
                goal["target"] = CPH.GetGlobalVar<string>("donConnectGoalTarget", true) ?? "0";
                goal["remaining"] = CPH.GetGlobalVar<string>("donConnectGoalRemaining", true) ?? "0";
                goal["percent"] = CPH.GetGlobalVar<string>("donConnectGoalPercent", true) ?? "0";
                goal["currency"] = CPH.GetGlobalVar<string>("donConnectGoalCurrency", true) ?? "";
                goal["currentText"] = CPH.GetGlobalVar<string>("donConnectGoalCurrentText", true) ?? "";
                goal["targetText"] = CPH.GetGlobalVar<string>("donConnectGoalTargetText", true) ?? "";
                goal["remainingText"] = CPH.GetGlobalVar<string>("donConnectGoalRemainingText", true) ?? "";
                goal["percentText"] = CPH.GetGlobalVar<string>("donConnectGoalPercentText", true) ?? "";
                goal["summary"] = CPH.GetGlobalVar<string>("donConnectGoalSummary", true) ?? "";
                root["goal"] = goal;

                var timer = new JObject();
                timer["headerTitle"] = Settings.Get("timer.headerTitle", "");
                timer["title"] = CPH.GetGlobalVar<string>("donConnectTimerTitle", true) ?? "";
                timer["subtitle"] = Settings.Get("timer.subtitle", "");
                timer["seconds"] = CPH.GetGlobalVar<string>("donConnectTimerSeconds", true) ?? "0";
                timer["text"] = CPH.GetGlobalVar<string>("donConnectTimerText", true) ?? "00:00:00";
                timer["endsAt"] = CPH.GetGlobalVar<string>("donConnectTimerEndsAt", true) ?? "";
                timer["startedAt"] = CPH.GetGlobalVar<string>("donConnectTimerStartedAt", true) ?? "";
                timer["mode"] = CPH.GetGlobalVar<string>("donConnectTimerMode", true) ?? "countdown";
                timer["addedSeconds"] = CPH.GetGlobalVar<string>("donConnectTimerAddedSeconds", true) ?? "0";
                timer["addedText"] = CPH.GetGlobalVar<string>("donConnectTimerAddedText", true) ?? "00:00:00";
                timer["summary"] = CPH.GetGlobalVar<string>("donConnectTimerSummary", true) ?? "";
                root["timer"] = timer;

                var last = new JObject();
                last["user"] = CPH.GetGlobalVar<string>("donConnectLastDonationUser", true) ?? CPH.GetGlobalVar<string>("donConnectTimerLastDonationUser", true) ?? "";
                last["amount"] = CPH.GetGlobalVar<string>("donConnectLastDonationAmount", true) ?? CPH.GetGlobalVar<string>("donConnectTimerLastDonationAmount", true) ?? "";
                last["currency"] = CPH.GetGlobalVar<string>("donConnectLastDonationCurrency", true) ?? "";
                last["platform"] = CPH.GetGlobalVar<string>("donConnectLastDonationPlatform", true) ?? CPH.GetGlobalVar<string>("donConnectTimerLastDonationPlatform", true) ?? "";
                root["lastDonation"] = last;
                root["services"] = EnabledServiceNames();

                string directory = Path.GetDirectoryName(statePath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(statePath, root.ToString(Formatting.Indented), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Logger.Warn("Goal/Timer overlay state was not written. " + ex.Message);
            }
        }

        private JArray EnabledServiceNames()
        {
            var services = new JArray();
            AddEnabledService(services, "DonationAlerts", "donationalerts.enabled");
            AddEnabledService(services, "DonatePay RU", "donatepayru.enabled");
            AddEnabledService(services, "DonatePay EU", "donatepayeu.enabled");
            AddEnabledService(services, "DonateX.gg", "donatex.enabled");
            AddEnabledService(services, "StreamElements", "streamelements.enabled");
            AddEnabledService(services, "Streamlabs", "streamlabs.enabled");
            AddEnabledService(services, "Donate.Stream", "donatestream.enabled");
            AddEnabledService(services, "deStream", "destream.enabled");
            AddEnabledService(services, "ODA", "oda.enabled");
            AddEnabledService(services, "Generic API", "generic.enabled");
            return services;
        }

        private void AddEnabledService(JArray services, string name, string enabledKey)
        {
            if (Settings.GetBool(enabledKey, false))
                services.Add(name);
        }

        private DateTime ReadDateSetting(string key, DateTime fallback)
        {
            DateTime value;
            return DateTime.TryParse(Settings.Get(key, fallback.ToString("o")), null, DateTimeStyles.RoundtripKind, out value) ? value.ToUniversalTime() : fallback;
        }

        private decimal ConvertDonationAmount(UnifiedDonationEvent e, string targetCurrency)
        {
            if (e == null || e.Amount <= 0)
                return 0;

            string from = NormalizeCurrency(e.Currency);
            string to = NormalizeCurrency(targetCurrency);
            if (string.IsNullOrWhiteSpace(to))
                to = from;
            if (string.IsNullOrWhiteSpace(from) || from == to)
            {
                SetConversionVars(e.Amount, e.Currency ?? "", e.Amount, to, 1, "same");
                return e.Amount;
            }

            decimal rate;
            string status;
            if (TryGetConversionRate(from, to, out rate, out status))
            {
                decimal converted = Math.Round(e.Amount * rate, 2);
                SetConversionVars(e.Amount, from, converted, to, rate, status);
                return converted;
            }

            SetConversionVars(e.Amount, from, 0, to, 0, status);
            if (Settings.Get("currency.onError", "skip").Equals("keepOriginal", StringComparison.OrdinalIgnoreCase))
                return e.Amount;
            Logger.Warn("Currency conversion skipped: " + e.Amount.ToString(CultureInfo.InvariantCulture) + " " + from + " -> " + to + ". " + status);
            return 0;
        }

        private bool TryGetConversionRate(string from, string to, out decimal rate, out string status)
        {
            rate = 0;
            status = "missing";
            string mode = Settings.Get("currency.conversion", "auto").Trim();
            if (mode.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                status = "off";
                return false;
            }

            if (mode.Equals("manual", StringComparison.OrdinalIgnoreCase))
                return TryGetManualRate(from, to, out rate, out status);

            if (TryGetCachedRate(from, to, out rate))
            {
                status = "cached";
                return true;
            }

            if (TryFetchRate(from, to, out rate, out status))
            {
                SaveCachedRate(from, to, rate);
                status = "auto";
                return true;
            }

            decimal manualRate;
            string manualStatus;
            if (TryGetManualRate(from, to, out manualRate, out manualStatus))
            {
                rate = manualRate;
                status = "manualFallback";
                return true;
            }

            return false;
        }

        private bool TryFetchRate(string from, string to, out decimal rate, out string status)
        {
            rate = 0;
            status = "fetchFailed";
            try
            {
                string template = Settings.Get("currency.apiUrl", "https://open.er-api.com/v6/latest/{FROM}");
                string url = template.Replace("{FROM}", Uri.EscapeDataString(from)).Replace("{TO}", Uri.EscapeDataString(to));
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Accept = "application/json";
                request.Timeout = 4000;
                request.ReadWriteTimeout = 4000;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    JToken root = JToken.Parse(reader.ReadToEnd());
                    JToken token = root["rates"] != null ? root["rates"][to] : null;
                    decimal parsed;
                    if (token != null && decimal.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) && parsed > 0)
                    {
                        rate = parsed;
                        return true;
                    }
                    status = "rateMissing";
                    return false;
                }
            }
            catch (Exception ex)
            {
                status = ex.Message;
                return false;
            }
        }

        private bool TryGetCachedRate(string from, string to, out decimal rate)
        {
            rate = 0;
            string key = "currency.rate." + from + "_" + to;
            DateTime fetchedAt;
            if (!DateTime.TryParse(Settings.Get(key + ".at", ""), null, DateTimeStyles.RoundtripKind, out fetchedAt))
                return false;
            int minutes;
            if (!int.TryParse(Settings.Get("currency.cacheMinutes", "60"), NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes))
                minutes = 60;
            if (minutes < 1)
                minutes = 1;
            if (DateTime.UtcNow - fetchedAt.ToUniversalTime() > TimeSpan.FromMinutes(minutes))
                return false;
            return decimal.TryParse(Settings.Get(key, "0"), NumberStyles.Any, CultureInfo.InvariantCulture, out rate) && rate > 0;
        }

        private void SaveCachedRate(string from, string to, decimal rate)
        {
            string key = "currency.rate." + from + "_" + to;
            Settings.Set(key, rate.ToString(CultureInfo.InvariantCulture), true);
            Settings.Set(key + ".at", DateTime.UtcNow.ToString("o"), true);
        }

        private bool TryGetManualRate(string from, string to, out decimal rate, out string status)
        {
            rate = 0;
            status = "manualMissing";
            var rates = ParseManualRates(Settings.Get("currency.rates", ""));
            if (!rates.ContainsKey(from) || !rates.ContainsKey(to) || rates[from] <= 0)
                return false;
            rate = rates[to] / rates[from];
            status = "manual";
            return rate > 0;
        }

        private Dictionary<string, decimal> ParseManualRates(string value)
        {
            var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            string[] items = (value ?? "").Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string item in items)
            {
                int index = item.IndexOf('=');
                if (index <= 0)
                    continue;
                string code = NormalizeCurrency(item.Substring(0, index));
                decimal parsed;
                if (!string.IsNullOrWhiteSpace(code) && decimal.TryParse(item.Substring(index + 1).Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
                    result[code] = parsed;
            }
            return result;
        }

        private string NormalizeCurrency(string value)
        {
            return (value ?? "").Trim().ToUpperInvariant();
        }

        private void SetConversionVars(decimal originalAmount, string originalCurrency, decimal convertedAmount, string convertedCurrency, decimal rate, string status)
        {
            SetGoalVar("donConnectLastDonationOriginalAmount", originalAmount.ToString(CultureInfo.InvariantCulture));
            SetGoalVar("donConnectLastDonationOriginalCurrency", originalCurrency ?? "");
            SetGoalVar("donConnectLastDonationConvertedAmount", convertedAmount.ToString(CultureInfo.InvariantCulture));
            SetGoalVar("donConnectLastDonationConvertedCurrency", convertedCurrency ?? "");
            SetGoalVar("donConnectLastDonationConversionRate", rate.ToString(CultureInfo.InvariantCulture));
            SetGoalVar("donConnectLastDonationConversionStatus", status ?? "");
        }

        private string FormatDuration(double totalSeconds)
        {
            long seconds = Math.Max(0, (long)Math.Floor(totalSeconds));
            long hours = seconds / 3600;
            long minutes = (seconds % 3600) / 60;
            long secs = seconds % 60;
            return hours.ToString("00", CultureInfo.InvariantCulture) + ":"
                + minutes.ToString("00", CultureInfo.InvariantCulture) + ":"
                + secs.ToString("00", CultureInfo.InvariantCulture);
        }

        private decimal ReadDecimalSetting(string key, decimal fallback)
        {
            decimal value;
            return decimal.TryParse(Settings.Get(key, fallback.ToString(CultureInfo.InvariantCulture)), NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private int ReadIntSetting(string key, int fallback)
        {
            int value;
            return int.TryParse(Settings.Get(key, fallback.ToString(CultureInfo.InvariantCulture)), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private string FormatAmount(decimal amount, string currency)
        {
            string text = amount.ToString("0.##", CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(currency) ? text : text + " " + currency;
        }

        private string FirstNonEmptyLocal(params string[] values)
        {
            foreach (string value in values)
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            return "";
        }

        private void SetGoalVar(string name, string value)
        {
            CPH.SetGlobalVar(name, value ?? "", true);
            CPH.SetArgument(name, value ?? "");
        }

        private string NormalizeProviderKey(string providerName)
        {
            string normalized = (providerName ?? "").Replace(".", "").Replace(" ", "").Replace("/", "").ToLowerInvariant();
            if (normalized == "donatepayru")
                return "donatepayru";
            if (normalized == "donatepayeu")
                return "donatepayeu";
            if (normalized == "donatepay")
                return "donatepayru";
            if (normalized == "donatexgg" || normalized == "donatex")
                return "donatex";
            if (normalized == "oda" || normalized == "opendonationassistant")
                return "oda";
            if (normalized == "streamelements")
                return "streamelements";
            if (normalized == "streamlabs")
                return "streamlabs";
            if (normalized == "donatestream")
                return "donatestream";
            if (normalized == "destream")
                return "destream";
            if (normalized == "donationalerts")
                return "donationalerts";
            if (normalized == "genericapi")
                return "generic";
            return "";
        }

        private void SendDonationToCredits(UnifiedDonationEvent donationEvent)
        {
            if (!Settings.GetBool("STREAMERBOT_CREDITS_ENABLED", false))
                return;

            string baseUrl = Settings.Get("STREAMERBOT_HTTP_URL", "http://127.0.0.1:7474").Trim().TrimEnd('/');
            string actionName = Settings.Get("STREAMERBOT_CREDITS_ACTION", "Add Donation To Credits").Trim();
            string section = Settings.Get("STREAMERBOT_CREDITS_SECTION", "\u0414\u043e\u043d\u0430\u0442\u044b").Trim();
            string fields = Settings.Get("STREAMERBOT_CREDITS_FIELDS", "name,amount").Trim();
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(actionName))
            {
                Logger.Warn("Streamer.bot Credits enabled, but HTTP URL or action name is empty.");
                return;
            }

            try
            {
                var payload = new JObject();
                var action = new JObject();
                action["name"] = actionName;
                payload["action"] = action;

                var args = new JObject();
                args["donorName"] = string.IsNullOrWhiteSpace(donationEvent.UserName) ? "Anonymous" : donationEvent.UserName;
                args["amount"] = donationEvent.Amount.ToString(CultureInfo.InvariantCulture);
                args["currency"] = donationEvent.Currency ?? "";
                args["platform"] = donationEvent.ProviderName ?? donationEvent.Source ?? "Donation";
                args["message"] = donationEvent.Message ?? "";
                args["creditsSection"] = string.IsNullOrWhiteSpace(section) ? "\u0414\u043e\u043d\u0430\u0442\u044b" : section;
                args["creditsFields"] = string.IsNullOrWhiteSpace(fields) ? "name,amount" : fields;
                payload["args"] = args;

                AddToCredits(section, payload["args"].ToString(Formatting.None));
                Logger.Debug("Donation sent to Streamer.bot Credits: " + donationEvent.ProviderName + " / " + donationEvent.DonationId);
            }
            catch (Exception ex)
            {
                Logger.Warn("Streamer.bot Credits: donation was not added. " + ex.Message);
            }
        }

        private void AddToCredits(string section, string json)
        {
            CPH.AddToCredits(section, json, true);
        }

        private void PostJson(string url, string json)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.Timeout = 3000;
            request.ReadWriteTimeout = 3000;

            byte[] body = Encoding.UTF8.GetBytes(json ?? "{}");
            request.ContentLength = body.Length;
            using (var stream = request.GetRequestStream())
                stream.Write(body, 0, body.Length);

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                int status = (int)response.StatusCode;
                if (status < 200 || status >= 300)
                    throw new InvalidOperationException("HTTP " + status.ToString(CultureInfo.InvariantCulture));
            }
        }

        private bool SeenGlobally(UnifiedDonationEvent donationEvent)
        {
            bool mutexCreated;
            using (var mutex = new Mutex(false, "Local\\DonConnectDonationDedupe", out mutexCreated))
            {
                bool lockTaken = false;
                try
                {
                    lockTaken = mutex.WaitOne(TimeSpan.FromSeconds(5));
                    if (!lockTaken)
                    {
                        Logger.Warn("DonConnect dedupe lock timeout. Donation skipped to prevent duplicate alert.");
                        return true;
                    }

                    lock (GlobalDedupeLock)
                    {
                        string key = "dedupe_" + BuildStableDonationKey(donationEvent);
                        string value = Settings.Get(key, "");
                        DateTime seenAt;
                        if (!string.IsNullOrWhiteSpace(value)
                            && DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out seenAt)
                            && DateTime.UtcNow - seenAt.ToUniversalTime() < TimeSpan.FromHours(24))
                            return true;

                        // Глобальная отметка защищает от дублей даже если DonationAlerts прислал одно событие несколько раз.
                        Settings.Set(key, DateTime.UtcNow.ToString("o"), true);
                        return false;
                    }
                }
                finally
                {
                    if (lockTaken)
                        mutex.ReleaseMutex();
                }
            }
        }

        private string BuildStableDonationKey(UnifiedDonationEvent donationEvent)
        {
            string source = (donationEvent.ProviderName ?? donationEvent.Source ?? "unknown").ToLowerInvariant();
            string id = donationEvent.DonationId ?? "";
            if (!string.IsNullOrWhiteSpace(id))
                return SanitizeKey(source + "_" + id);

            string fallback = source + "_"
                + (donationEvent.UserName ?? "").ToLowerInvariant() + "_"
                + donationEvent.Amount.ToString(CultureInfo.InvariantCulture) + "_"
                + (donationEvent.Currency ?? "").ToLowerInvariant() + "_"
                + (donationEvent.Message ?? "").ToLowerInvariant();
            return SanitizeKey(fallback);
        }

        private bool SeenProxyDuplicate(UnifiedDonationEvent donationEvent)
        {
            if (!Settings.GetBool("dedupe.proxy.enabled", true))
                return false;

            string signature = BuildProxyDonationSignature(donationEvent);
            if (string.IsNullOrWhiteSpace(signature))
                return false;

            DateTime now = DateTime.UtcNow;
            TimeSpan window = TimeSpan.FromSeconds(ReadIntSetting("dedupe.proxy.windowSeconds", 90));
            JArray records = ReadProxyDedupeRecords();
            bool duplicate = false;

            foreach (JObject record in records.OfType<JObject>().ToArray())
            {
                DateTime seenAt;
                if (!DateTime.TryParse(JsonRecordText(record, "timestamp"), null, DateTimeStyles.RoundtripKind, out seenAt)
                    || now - seenAt.ToUniversalTime() > TimeSpan.FromMinutes(5))
                {
                    record.Remove();
                    continue;
                }

                if (!JsonRecordText(record, "signature").Equals(signature, StringComparison.Ordinal))
                    continue;

                string oldProvider = JsonRecordText(record, "provider");
                string oldUser = JsonRecordText(record, "user");
                if (!IsProxyConflictPair(oldProvider, donationEvent.ProviderName))
                    continue;

                if (now - seenAt.ToUniversalTime() <= window && LooksLikeProxyDuplicate(oldProvider, oldUser, donationEvent))
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
            {
                var item = new JObject();
                item["signature"] = signature;
                item["provider"] = donationEvent.ProviderName ?? donationEvent.Source ?? "";
                item["user"] = donationEvent.UserName ?? "";
                item["id"] = donationEvent.DonationId ?? "";
                item["timestamp"] = now.ToString("o");
                records.Add(item);
                while (records.Count > 80)
                    records.RemoveAt(0);
                Settings.Set("dedupe.proxy.records", records.ToString(Formatting.None), true);
            }

            return duplicate;
        }

        private JArray ReadProxyDedupeRecords()
        {
            try
            {
                string raw = Settings.Get("dedupe.proxy.records", "");
                return string.IsNullOrWhiteSpace(raw) ? new JArray() : JArray.Parse(raw);
            }
            catch
            {
                return new JArray();
            }
        }

        private string JsonRecordText(JObject json, string name)
        {
            if (json == null || string.IsNullOrWhiteSpace(name) || json[name] == null)
                return "";
            return json[name].ToString();
        }

        private string BuildProxyDonationSignature(UnifiedDonationEvent e)
        {
            if (e == null || e.Amount <= 0)
                return "";

            string amount = Math.Round(e.Amount, 2).ToString("0.00", CultureInfo.InvariantCulture);
            string currency = NormalizeCurrency(e.Currency);
            string message = NormalizeDedupeText(e.Message);
            return SanitizeKey(amount + "_" + currency + "_" + message);
        }

        private string NormalizeDedupeText(string value)
        {
            value = (value ?? "").Trim().ToLowerInvariant();
            var sb = new StringBuilder();
            bool wasSpace = false;
            foreach (char c in value)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!wasSpace)
                        sb.Append(' ');
                    wasSpace = true;
                }
                else
                {
                    sb.Append(c);
                    wasSpace = false;
                }
            }

            string result = sb.ToString().Trim();
            if (result.Length > 90)
                result = result.Substring(0, 90);
            return result;
        }

        private bool IsProxyConflictPair(string providerA, string providerB)
        {
            string a = NormalizeProviderKey(providerA);
            string b = NormalizeProviderKey(providerB);
            if (a == b || string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;

            if ((a == "streamelements" && IsDonatePayKey(b)) || (b == "streamelements" && IsDonatePayKey(a)))
                return true;

            if (a == "oda" && IsOdaIntegratedProviderKey(b))
                return true;

            if (b == "oda" && IsOdaIntegratedProviderKey(a))
                return true;

            return false;
        }

        private bool IsDonatePayKey(string key)
        {
            return key == "donatepayeu" || key == "donatepayru";
        }

        private bool IsOdaIntegratedProviderKey(string key)
        {
            return key == "donationalerts" || key == "donatepayeu" || key == "donatepayru" || key == "donatex";
        }

        private bool LooksLikeProxyDuplicate(string oldProvider, string oldUser, UnifiedDonationEvent current)
        {
            string currentProvider = current.ProviderName ?? current.Source ?? "";
            if (IsProxyTechnicalDonation(oldProvider, oldUser) || IsProxyTechnicalDonation(currentProvider, current.UserName))
                return true;

            string message = NormalizeDedupeText(current.Message);
            return message.Length >= 2;
        }

        private bool IsProxyTechnicalDonation(string provider, string user)
        {
            string key = NormalizeProviderKey(provider);
            if (!IsDonatePayKey(key))
                return false;

            string value = (user ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value) || value.Equals("Anonymous", StringComparison.OrdinalIgnoreCase))
                return true;

            int letters = 0;
            int digits = 0;
            foreach (char c in value)
            {
                if (char.IsLetter(c))
                    letters++;
                else if (char.IsDigit(c))
                    digits++;
            }

            return digits >= 5 && letters == 0;
        }

        private string SanitizeKey(string key)
        {
            var sb = new StringBuilder();
            foreach (char c in key)
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')
                    sb.Append(c);
                else
                    sb.Append('_');
            }

            string result = sb.ToString();
            if (result.Length > 120)
                result = result.Substring(0, 120);
            return result;
        }

        private void TriggerDonationEvents(UnifiedDonationEvent donationEvent)
        {
            var eventArgs = BuildDonationArgs(donationEvent);
            CPH.TriggerCodeEvent("donconnect.donation.any", eventArgs);

            foreach (string providerEventName in GetProviderEventNames(donationEvent.ProviderName))
                if (!string.IsNullOrWhiteSpace(providerEventName))
                    CPH.TriggerCodeEvent(providerEventName, eventArgs);
        }

        private Dictionary<string, object> BuildDonationArgs(UnifiedDonationEvent e)
        {
            var result = new Dictionary<string, object>();
            result["donationSource"] = e.Source ?? e.ProviderName;
            result["donationProvider"] = e.ProviderName ?? "";
            result["donationEventType"] = e.EventType ?? "donation";
            result["donationUser"] = string.IsNullOrWhiteSpace(e.UserName) ? "Anonymous" : e.UserName;
            result["donationAmount"] = e.Amount.ToString(CultureInfo.InvariantCulture);
            result["donationCurrency"] = e.Currency ?? "";
            result["donationMessage"] = e.Message ?? "";
            result["donationId"] = e.DonationId ?? "";
            result["donationTimestamp"] = e.Timestamp.ToUniversalTime().ToString("o");
            result["donationRawJson"] = e.RawJson ?? "";
            result["donationIsAnonymous"] = e.IsAnonymous.ToString();
            result["donConnectGoalTitle"] = CPH.GetGlobalVar<string>("donConnectGoalTitle", true) ?? "";
            result["donConnectGoalCurrent"] = CPH.GetGlobalVar<string>("donConnectGoalCurrent", true) ?? "";
            result["donConnectGoalTarget"] = CPH.GetGlobalVar<string>("donConnectGoalTarget", true) ?? "";
            result["donConnectGoalRemaining"] = CPH.GetGlobalVar<string>("donConnectGoalRemaining", true) ?? "";
            result["donConnectGoalPercent"] = CPH.GetGlobalVar<string>("donConnectGoalPercent", true) ?? "";
            result["donConnectGoalCurrency"] = CPH.GetGlobalVar<string>("donConnectGoalCurrency", true) ?? "";
            result["donConnectGoalCurrentText"] = CPH.GetGlobalVar<string>("donConnectGoalCurrentText", true) ?? "";
            result["donConnectGoalTargetText"] = CPH.GetGlobalVar<string>("donConnectGoalTargetText", true) ?? "";
            result["donConnectGoalRemainingText"] = CPH.GetGlobalVar<string>("donConnectGoalRemainingText", true) ?? "";
            result["donConnectGoalPercentText"] = CPH.GetGlobalVar<string>("donConnectGoalPercentText", true) ?? "";
            result["donConnectGoalSummary"] = CPH.GetGlobalVar<string>("donConnectGoalSummary", true) ?? "";
            result["donConnectTimerTitle"] = CPH.GetGlobalVar<string>("donConnectTimerTitle", true) ?? "";
            result["donConnectTimerSeconds"] = CPH.GetGlobalVar<string>("donConnectTimerSeconds", true) ?? "";
            result["donConnectTimerText"] = CPH.GetGlobalVar<string>("donConnectTimerText", true) ?? "";
            result["donConnectTimerEndsAt"] = CPH.GetGlobalVar<string>("donConnectTimerEndsAt", true) ?? "";
            result["donConnectTimerAddedSeconds"] = CPH.GetGlobalVar<string>("donConnectTimerAddedSeconds", true) ?? "";
            result["donConnectTimerAddedText"] = CPH.GetGlobalVar<string>("donConnectTimerAddedText", true) ?? "";
            result["donConnectTimerSummary"] = CPH.GetGlobalVar<string>("donConnectTimerSummary", true) ?? "";
            result["donConnectLastDonationOriginalAmount"] = CPH.GetGlobalVar<string>("donConnectLastDonationOriginalAmount", true) ?? "";
            result["donConnectLastDonationOriginalCurrency"] = CPH.GetGlobalVar<string>("donConnectLastDonationOriginalCurrency", true) ?? "";
            result["donConnectLastDonationConvertedAmount"] = CPH.GetGlobalVar<string>("donConnectLastDonationConvertedAmount", true) ?? "";
            result["donConnectLastDonationConvertedCurrency"] = CPH.GetGlobalVar<string>("donConnectLastDonationConvertedCurrency", true) ?? "";
            result["donConnectLastDonationConversionRate"] = CPH.GetGlobalVar<string>("donConnectLastDonationConversionRate", true) ?? "";
            result["donConnectLastDonationConversionStatus"] = CPH.GetGlobalVar<string>("donConnectLastDonationConversionStatus", true) ?? "";
            result["tipSource"] = result["donationSource"];
            result["tipUser"] = result["donationUser"];
            result["tipUsername"] = result["donationUser"];
            result["tipName"] = result["donationUser"];
            result["tipAmount"] = result["donationAmount"];
            result["tipCurrency"] = result["donationCurrency"];
            result["tipMessage"] = result["donationMessage"];
            return result;
        }

        private IEnumerable<string> GetProviderEventNames(string providerName)
        {
            string normalized = (providerName ?? "").Replace(".", "").Replace(" ", "").Replace("/", "").ToLowerInvariant();
            if (normalized == "donationalerts")
            {
                yield return "donconnect.donation.donationalerts";
                yield break;
            }
            if (normalized == "donatepay")
            {
                yield return "donconnect.donation.donatepay";
                yield break;
            }
            if (normalized == "donatepayru")
            {
                yield return "donconnect.donation.donatepay";
                yield return "donconnect.donation.donatepayru";
                yield break;
            }
            if (normalized == "donatepayeu")
            {
                yield return "donconnect.donation.donatepay";
                yield return "donconnect.donation.donatepayeu";
                yield break;
            }
            if (normalized == "streamelements")
            {
                yield return "donconnect.donation.streamelements";
                yield break;
            }
            if (normalized == "streamlabs")
            {
                yield return "donconnect.donation.streamlabs";
                yield break;
            }
            if (normalized == "genericapi")
            {
                yield return "donconnect.donation.genericapi";
                yield break;
            }
            if (normalized == "donatestream")
            {
                yield return "donconnect.donation.donatestream";
                yield break;
            }
            if (normalized == "destream")
            {
                yield return "donconnect.donation.destream";
                yield break;
            }
            if (normalized == "donatexgg" || normalized == "donatex")
            {
                yield return "donconnect.donation.donatex";
                yield break;
            }
            if (normalized == "oda" || normalized == "opendonationassistant")
            {
                yield return "donconnect.donation.oda";
                yield break;
            }
        }

        private void AddProvider(IDonationProvider provider)
        {
            Providers.Add(provider);
        }

        private void ExportDonation(UnifiedDonationEvent e)
        {
            // Эти переменные видны последующим actions в Streamer.bot.
            string source = e.Source ?? e.ProviderName;
            string user = string.IsNullOrWhiteSpace(e.UserName) ? "Anonymous" : e.UserName;
            string amount = e.Amount.ToString(CultureInfo.InvariantCulture);
            string timestamp = e.Timestamp.ToUniversalTime().ToString("o");
            SetDonationArgument("donationSource", source);
            SetDonationArgument("donationProvider", e.ProviderName ?? "");
            SetDonationArgument("donationUser", user);
            SetDonationArgument("donationAmount", amount);
            SetDonationArgument("donationCurrency", e.Currency ?? "");
            SetDonationArgument("donationMessage", e.Message ?? "");
            SetDonationArgument("donationId", e.DonationId ?? "");
            SetDonationArgument("donationTimestamp", timestamp);
            SetDonationArgument("donationRawJson", e.RawJson ?? "");
            SetDonationArgument("donationIsAnonymous", e.IsAnonymous.ToString());

            SetDonationArgument("tipSource", source);
            SetDonationArgument("tipUser", user);
            SetDonationArgument("tipUsername", user);
            SetDonationArgument("tipName", user);
            SetDonationArgument("tipAmount", amount);
            SetDonationArgument("tipCurrency", e.Currency ?? "");
            SetDonationArgument("tipMessage", e.Message ?? "");

            Settings.Set("lastDonation.source", source ?? "", true);
            Settings.Set("lastDonation.provider", e.ProviderName ?? "", true);
            Settings.Set("lastDonation.user", user, true);
            Settings.Set("lastDonation.amount", amount, true);
            Settings.Set("lastDonation.currency", e.Currency ?? "", true);
            Settings.Set("lastDonation.message", e.Message ?? "", true);
            Settings.Set("lastDonation.id", e.DonationId ?? "", true);
            Settings.Set("lastDonation.timestamp", timestamp, true);

            Logger.Info("Донат: " + e.ProviderName + " / " + e.UserName + " / " + e.Amount.ToString(CultureInfo.InvariantCulture) + " " + e.Currency);
        }

        private void SetDonationArgument(string name, string value)
        {
            CPH.SetArgument(name, value ?? "");
            CPH.SetGlobalVar(name, value ?? "", true);
        }
    }
}

public static class DonConnectPaths
{
    public static string DataDirectory(string configuredDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
            return EnsureDirectory(configuredDirectory.Trim());

        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string directory = TryUseDirectory(baseDirectory);
        if (!string.IsNullOrWhiteSpace(directory))
            return directory;

        directory = TryUseDirectory(Environment.CurrentDirectory);
        if (!string.IsNullOrWhiteSpace(directory))
            return directory;

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            appData = AppDomain.CurrentDomain.BaseDirectory;
        return EnsureDirectory(Path.Combine(appData, "DonConnect"));
    }

    private static string TryUseDirectory(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return "";

        try
        {
            return EnsureDirectory(Path.Combine(root, "DonConnect"));
        }
        catch
        {
            return "";
        }
    }

    private static string EnsureDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        return directory;
    }
}

public static class DonConnectShell
{
    public static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return;
        }
        catch
        {
        }

        System.Diagnostics.Process.Start("explorer.exe", url);
    }

    public static void OpenDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }
}

public class UnifiedDonationEvent
{
    public string Source { get; set; }
    public string ProviderName { get; set; }
    public string EventType { get; set; }
    public string DonationId { get; set; }
    public string UserName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsAnonymous { get; set; }
    public string RawJson { get; set; }
}

public interface IDonationProvider
{
    string ProviderName { get; }
    event Action<UnifiedDonationEvent> DonationReceived;
    Task ConnectAsync();
    Task DisconnectAsync();
    Task<bool> ValidateCredentialsAsync();
}

public static class ProviderDiagnostics
{
    public static void Started(BridgeSettings settings, string key)
    {
        if (settings == null)
            return;

        settings.Set(key + ".diagnostics.connection", "connecting", true);
        settings.Set(key + ".diagnostics.polling", "started", true);
        settings.Set(key + ".diagnostics.lastError", "none", true);
    }

    public static void Connected(BridgeSettings settings, string key)
    {
        if (settings == null)
            return;

        settings.Set(key + ".diagnostics.connection", "connected", true);
        settings.Set(key + ".diagnostics.polling", "started", true);
        settings.Set(key + ".diagnostics.lastError", "none", true);
        settings.Set(key + ".diagnostics.lastConnectAt", DateTime.UtcNow.ToString("o"), true);
    }

    public static void PollOk(BridgeSettings settings, string key, int itemCount)
    {
        if (settings == null)
            return;

        Connected(settings, key);
        settings.Set(key + ".diagnostics.lastFetchAt", DateTime.UtcNow.ToString("o"), true);
        settings.Set(key + ".diagnostics.lastFetchCount", itemCount.ToString(CultureInfo.InvariantCulture), true);
    }

    public static void Missing(BridgeSettings settings, string key, string reason)
    {
        if (settings == null)
            return;

        settings.Set(key + ".diagnostics.connection", "disconnected", true);
        settings.Set(key + ".diagnostics.polling", "not-started", true);
        settings.Set(key + ".diagnostics.lastError", reason ?? "missing settings", true);
    }

    public static void Disconnected(BridgeSettings settings, string key, string reason)
    {
        if (settings == null)
            return;

        settings.Set(key + ".diagnostics.connection", "disconnected", true);
        settings.Set(key + ".diagnostics.lastError", string.IsNullOrWhiteSpace(reason) ? "connection lost" : reason, true);
    }

    public static void Stopped(BridgeSettings settings, string key)
    {
        if (settings == null)
            return;

        settings.Set(key + ".diagnostics.connection", "stopped", true);
        settings.Set(key + ".diagnostics.polling", "stopped", true);
    }
}

public class DonationAlertsProvider : IDonationProvider
{
    private const string ProviderKey = "donationalerts";
    private const string ProviderDisplayName = "DonationAlerts";
    private const string ApiHost = "https://www.donationalerts.com";
    private const string SocketUrl = "wss://centrifugo.donationalerts.com/connection/websocket";
    private const string RedirectUri = "http://127.0.0.1:8597/donconnect/donationalerts/callback/";
    private const string Scope = "oauth-user-show oauth-donation-subscribe oauth-donation-index";

    private readonly BridgeSettings Settings;
    private readonly BridgeLogger Logger;
    private readonly string BundledClientId;
    private readonly string BundledClientSecret;
    private ClientWebSocket Socket;
    private CancellationTokenSource Cancellation;

    public string ProviderName { get { return ProviderDisplayName; } }
    public event Action<UnifiedDonationEvent> DonationReceived;

    public DonationAlertsProvider(BridgeSettings settings, BridgeLogger logger, string bundledClientId, string bundledClientSecret)
    {
        Settings = settings;
        Logger = logger;
        BundledClientId = bundledClientId ?? "";
        BundledClientSecret = bundledClientSecret ?? "";
    }

    public Task ConnectAsync()
    {
        if (!Settings.GetBool(ProviderKey + ".enabled", false))
        {
            Logger.Debug("DonationAlerts выключен в настройках.");
            return Task.FromResult(0);
        }

        string accessToken = Settings.Get(ProviderKey + ".accessToken", "");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            Logger.Warn("DonationAlerts включен, но access token отсутствует. Запустите AuthorizeDonationAlerts.");
            ProviderDiagnostics.Missing(Settings, ProviderKey, "missing access token");
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
        ProviderDiagnostics.Started(Settings, ProviderKey);
        Task.Run(() => SocketLoop(accessToken, Cancellation.Token));
        Logger.Info("DonationAlerts: подключение запущено в фоне.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        try
        {
            if (Cancellation != null)
                Cancellation.Cancel();

            if (Socket != null)
            {
                Socket.Abort();
                Socket.Dispose();
                Socket = null;
            }
        }
        catch { }

        ProviderDiagnostics.Stopped(Settings, ProviderKey);
        return Task.FromResult(0);
    }

    public Task<bool> ValidateCredentialsAsync()
    {
        string accessToken = Settings.Get(ProviderKey + ".accessToken", "");
        if (string.IsNullOrWhiteSpace(accessToken))
            return Task.FromResult(false);

        try
        {
            GetProfile(accessToken);
            Logger.Info("DonationAlerts: токен успешно проверен.");
            return Task.FromResult(true);
        }
        catch (WebException ex)
        {
            if (TryRefreshToken())
            {
                Logger.Info("DonationAlerts: access token обновлен.");
                return Task.FromResult(true);
            }

            Logger.Warn("DonationAlerts: токен не прошел проверку. " + ex.Message);
            return Task.FromResult(false);
        }
    }

    public bool RunOAuthFlow()
    {
        var credentials = GetCredentials();
        string clientId = credentials.ClientId;
        string clientSecret = credentials.ClientSecret;

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            Logger.Warn("DonationAlerts: OAuth credentials не настроены. Выберите own mode или настройте shared mode.");
            return false;
        }

        string url = ApiHost + "/oauth/authorize?client_id=" + Uri.EscapeDataString(clientId)
            + "&redirect_uri=" + Uri.EscapeDataString(RedirectUri)
            + "&scope=" + Uri.EscapeDataString(Scope)
            + "&response_type=code";

        Logger.Info("DonationAlerts: открываю браузер для авторизации.");
        DonConnectShell.OpenUrl(url);

        string code = WaitForOAuthCode();
        if (string.IsNullOrWhiteSpace(code))
        {
            Logger.Warn("DonationAlerts: код авторизации не получен.");
            return false;
        }

        return ExchangeCodeForToken(code, clientId, clientSecret);
    }

    private async Task SocketLoop(string accessToken, CancellationToken token)
    {
        int retryDelayMs = 3000;

        while (!token.IsCancellationRequested)
        {
            try
            {
                await ConnectSocketOnce(accessToken, token);
                retryDelayMs = 3000;
            }
            catch (WebException ex)
            {
                Logger.Warn("DonationAlerts: HTTP ошибка. " + ex.Message);
                ProviderDiagnostics.Disconnected(Settings, ProviderKey, ex.Message);
                if (TryRefreshToken())
                {
                    accessToken = Settings.Get(ProviderKey + ".accessToken", accessToken);
                    ProviderDiagnostics.Started(Settings, ProviderKey);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("DonationAlerts: соединение прервано. " + ex.Message);
                ProviderDiagnostics.Disconnected(Settings, ProviderKey, ex.Message);
            }

            if (!token.IsCancellationRequested)
            {
                Logger.Info("DonationAlerts: повторное подключение через " + (retryDelayMs / 1000).ToString(CultureInfo.InvariantCulture) + " сек.");
                await Task.Delay(retryDelayMs, token);
                retryDelayMs = Math.Min(retryDelayMs * 2, 30000);
            }
        }
    }

    private async Task ConnectSocketOnce(string accessToken, CancellationToken token)
    {
        var profile = GetProfile(accessToken);
        Socket = new ClientWebSocket();
        await Socket.ConnectAsync(new Uri(SocketUrl), token);

        string clientId = await AuthorizeSocket(profile.SocketConnectionToken, token);
        var subscriptions = GetSubscriptionTokens(accessToken, profile.Id, clientId);

        foreach (var sub in subscriptions)
            await Subscribe(sub.Channel, sub.Token, token);

        Logger.Info("DonationAlerts: WebSocket подключен.");
        ProviderDiagnostics.Connected(Settings, ProviderKey);

        while (Socket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            string message = await ReceiveText(Socket, token);
            if (!string.IsNullOrWhiteSpace(message))
                HandleSocketMessage(message);
        }
    }

    private async Task<string> AuthorizeSocket(string socketToken, CancellationToken token)
    {
        var payload = JsonConvert.SerializeObject(new
        {
            id = 1,
            @params = new { token = socketToken }
        });

        await SendText(Socket, payload, token);
        string response = await ReceiveText(Socket, token);
        JObject json = JObject.Parse(response);
        string client = (string)json["result"]["client"];
        if (string.IsNullOrWhiteSpace(client))
            throw new Exception("DonationAlerts не вернул socket client id.");

        return client;
    }

    private async Task Subscribe(string channel, string channelToken, CancellationToken token)
    {
        var payload = JsonConvert.SerializeObject(new
        {
            id = 2,
            method = 1,
            @params = new { channel = channel, token = channelToken }
        });

        await SendText(Socket, payload, token);

        // Centrifugo может прислать служебный ответ и первое push-событие, поэтому читаем мягко.
        for (int i = 0; i < 2; i++)
        {
            string response = await ReceiveText(Socket, token);
            if (string.IsNullOrWhiteSpace(response))
                continue;

            Logger.Debug("DonationAlerts subscribe response: " + response);
            if (response.IndexOf(channel, StringComparison.OrdinalIgnoreCase) >= 0)
                break;
        }
    }

    private void HandleSocketMessage(string rawJson)
    {
        Logger.Debug("DonationAlerts socket: " + rawJson);

        try
        {
            JObject root = JObject.Parse(rawJson);
            JToken result = root["result"];
            if (result == null)
                return;

            string channel = (string)result["channel"] ?? "";
            if (channel.IndexOf("$alerts:donation", StringComparison.OrdinalIgnoreCase) < 0)
                return;

            JToken data = result["data"] != null ? result["data"]["data"] : null;
            if (data == null)
                return;

            var donation = new UnifiedDonationEvent
            {
                Source = ProviderDisplayName,
                ProviderName = ProviderDisplayName,
                EventType = "donation",
                DonationId = SafeString(data["id"]),
                UserName = FirstNonEmpty(SafeString(data["username"]), SafeString(data["name"]), "Anonymous"),
                Amount = SafeDecimal(data["amount"]),
                Currency = SafeString(data["currency"]),
                Message = SafeString(data["message"]),
                Timestamp = DateTime.UtcNow,
                IsAnonymous = string.IsNullOrWhiteSpace(SafeString(data["username"])),
                RawJson = rawJson
            };

            if (donation.Amount > 0 && DonationReceived != null)
                DonationReceived(donation);
        }
        catch (Exception ex)
        {
            Logger.Warn("DonationAlerts: не удалось разобрать событие. " + ex.Message);
        }
    }

    private ProfileData GetProfile(string accessToken)
    {
        string response = HttpJson("GET", ApiHost + "/api/v1/user/oauth", null, Bearer(accessToken));
        JObject json = JObject.Parse(response);
        JToken data = json["data"];
        return new ProfileData
        {
            Id = SafeInt(data["id"]),
            SocketConnectionToken = SafeString(data["socket_connection_token"])
        };
    }

    private List<SubscriptionData> GetSubscriptionTokens(string accessToken, int userId, string socketClientId)
    {
        string payload = JsonConvert.SerializeObject(new
        {
            client = socketClientId,
            channels = new[]
            {
                "$alerts:donation_" + userId.ToString(CultureInfo.InvariantCulture)
            }
        });

        string response = HttpJson("POST", ApiHost + "/api/v1/centrifuge/subscribe", payload, Bearer(accessToken));
        JObject json = JObject.Parse(response);
        var result = new List<SubscriptionData>();

        foreach (JToken item in json["channels"])
        {
            result.Add(new SubscriptionData
            {
                Channel = SafeString(item["channel"]),
                Token = SafeString(item["token"])
            });
        }

        return result;
    }

    private bool ExchangeCodeForToken(string code, string clientId, string clientSecret)
    {
        string payload = JsonConvert.SerializeObject(new
        {
            grant_type = "authorization_code",
            client_id = clientId,
            client_secret = clientSecret,
            code = code,
            redirect_uri = RedirectUri
        });

        try
        {
            string response = HttpJson("POST", ApiHost + "/oauth/token", payload, null);
            JObject json = JObject.Parse(response);
            SaveTokens(json);
            Settings.Set(ProviderKey + ".enabled", "true", true);
            Logger.Info("DonationAlerts: токены получены и сохранены.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn("DonationAlerts: не удалось получить токены. " + ex.Message);
            return false;
        }
    }

    private bool TryRefreshToken()
    {
        string refreshToken = Settings.Get(ProviderKey + ".refreshToken", "");
        var credentials = GetCredentials();
        string clientId = credentials.ClientId;
        string clientSecret = credentials.ClientSecret;

        if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return false;

        string payload = JsonConvert.SerializeObject(new
        {
            grant_type = "refresh_token",
            client_id = clientId,
            client_secret = clientSecret,
            refresh_token = refreshToken,
            scope = Scope
        });

        try
        {
            string response = HttpJson("POST", ApiHost + "/oauth/token", payload, null);
            SaveTokens(JObject.Parse(response));
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn("DonationAlerts: refresh token не сработал. " + ex.Message);
            return false;
        }
    }

    private OAuthCredentials GetCredentials()
    {
        string mode = Settings.Get(ProviderKey + ".authMode", "own").ToLowerInvariant();

        if (mode == "shared")
        {
            string sharedClientId = FirstNonEmpty(Settings.Get(ProviderKey + ".sharedClientId", ""), BundledClientId);
            string sharedClientSecret = FirstNonEmpty(Settings.Get(ProviderKey + ".sharedClientSecret", ""), BundledClientSecret);
            return new OAuthCredentials { ClientId = sharedClientId, ClientSecret = sharedClientSecret, Mode = "shared" };
        }

        return new OAuthCredentials
        {
            ClientId = Settings.Get(ProviderKey + ".clientId", ""),
            ClientSecret = Settings.Get(ProviderKey + ".clientSecret", ""),
            Mode = "own"
        };
    }

    private void SaveTokens(JObject json)
    {
        Settings.Set(ProviderKey + ".accessToken", SafeString(json["access_token"]), true);
        Settings.Set(ProviderKey + ".refreshToken", SafeString(json["refresh_token"]), true);
    }

    private string WaitForOAuthCode()
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);

        try
        {
            listener.Start();
            var task = listener.GetContextAsync();
            if (!task.Wait(TimeSpan.FromMinutes(2)))
                return "";

            HttpListenerContext context = task.Result;
            string code = context.Request.QueryString["code"];
            string html = "<!doctype html><html><head><meta charset=\"utf-8\"><title>DonConnect</title></head><body style=\"font-family:Segoe UI,Arial;margin:40px\"><h2>&#1040;&#1074;&#1090;&#1086;&#1088;&#1080;&#1079;&#1072;&#1094;&#1080;&#1103; &#1079;&#1072;&#1074;&#1077;&#1088;&#1096;&#1077;&#1085;&#1072;</h2><p>&#1052;&#1086;&#1078;&#1085;&#1086; &#1079;&#1072;&#1082;&#1088;&#1099;&#1090;&#1100; &#1101;&#1090;&#1091; &#1074;&#1082;&#1083;&#1072;&#1076;&#1082;&#1091; &#1080; &#1074;&#1077;&#1088;&#1085;&#1091;&#1090;&#1100;&#1089;&#1103; &#1074; Streamer.bot.</p></body></html>";
            byte[] bytes = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
            return code;
        }
        finally
        {
            try { listener.Close(); } catch { }
        }
    }

    private static async Task SendText(ClientWebSocket socket, string text, CancellationToken token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
    }

    private static async Task<string> ReceiveText(ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[8192];
        using (var stream = new MemoryStream())
        {
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close)
                    return "";

                stream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    private static Dictionary<string, string> Bearer(string token)
    {
        return new Dictionary<string, string> { { "Authorization", "Bearer " + token } };
    }

    private static string HttpJson(string method, string url, string payload, Dictionary<string, string> headers)
    {
        var request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = method;
        request.ContentType = "application/json";

        if (headers != null)
        {
            foreach (var pair in headers)
                request.Headers[pair.Key] = pair.Value;
        }

        if (!string.IsNullOrEmpty(payload))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            request.ContentLength = bytes.Length;
            using (Stream requestStream = request.GetRequestStream())
                requestStream.Write(bytes, 0, bytes.Length);
        }

        using (var response = (HttpWebResponse)request.GetResponse())
        using (var responseStream = response.GetResponseStream())
        using (var reader = new StreamReader(responseStream, Encoding.UTF8))
            return reader.ReadToEnd();
    }

    private static string SafeString(JToken token)
    {
        return token == null ? "" : token.ToString();
    }

    private static int SafeInt(JToken token)
    {
        int value;
        return int.TryParse(SafeString(token), NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : 0;
    }

    private static decimal SafeDecimal(JToken token)
    {
        decimal value;
        return decimal.TryParse(SafeString(token), NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : 0;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (string value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        return "";
    }

    private class ProfileData
    {
        public int Id { get; set; }
        public string SocketConnectionToken { get; set; }
    }

    private class SubscriptionData
    {
        public string Channel { get; set; }
        public string Token { get; set; }
    }

    private class OAuthCredentials
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string Mode { get; set; }
    }
}

public class GenericApiProvider : IDonationProvider
{
    private const string ProviderKey = "generic";
    private readonly BridgeSettings Settings;
    private readonly BridgeLogger Logger;
    private CancellationTokenSource Cancellation;

    public string ProviderName { get { return "GenericApi"; } }
    public event Action<UnifiedDonationEvent> DonationReceived;

    public GenericApiProvider(BridgeSettings settings, BridgeLogger logger)
    {
        Settings = settings;
        Logger = logger;
    }

    public Task ConnectAsync()
    {
        if (!Settings.GetBool("generic.enabled", false))
            return Task.FromResult(0);

        string endpoint = Settings.Get("generic.endpoint", "");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            Logger.Warn("Generic API включен, но endpoint не задан.");
            ProviderDiagnostics.Missing(Settings, ProviderKey, "missing endpoint");
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
        ProviderDiagnostics.Started(Settings, ProviderKey);
        Task.Run(() => PollLoop(endpoint, Cancellation.Token));
        Logger.Info("Generic API polling запущен.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        if (Cancellation != null)
            Cancellation.Cancel();
        ProviderDiagnostics.Stopped(Settings, ProviderKey);
        return Task.FromResult(0);
    }

    public Task<bool> ValidateCredentialsAsync()
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(Settings.Get("generic.endpoint", "")));
    }

    private async Task PollLoop(string endpoint, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var donations = Fetch(endpoint);
                ProviderDiagnostics.PollOk(Settings, ProviderKey, donations.Count);
                foreach (var donation in donations)
                {
                    if (DonationReceived != null)
                        DonationReceived(donation);
                }
            }
            catch (Exception ex)
            {
                ProviderDiagnostics.Disconnected(Settings, ProviderKey, ex.Message);
                Logger.Warn("Generic API: ошибка polling. " + ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(15), token);
        }
    }

    private List<UnifiedDonationEvent> Fetch(string endpoint)
    {
        var request = (HttpWebRequest)WebRequest.Create(endpoint);
        request.Method = "GET";
        request.Accept = "application/json";

        string token = Settings.Get("generic.token", "");
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers["Authorization"] = "Bearer " + token;

        using (var response = (HttpWebResponse)request.GetResponse())
        using (var stream = response.GetResponseStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            string raw = reader.ReadToEnd();
            JToken root = JToken.Parse(raw);
            var items = root.Type == JTokenType.Array ? root : root["items"];
            var result = new List<UnifiedDonationEvent>();

            if (items == null)
                return result;

            foreach (JToken item in items)
            {
                result.Add(new UnifiedDonationEvent
                {
                    Source = "GenericApi",
                    ProviderName = "GenericApi",
                    EventType = "donation",
                    DonationId = Value(item, "id"),
                    UserName = First(Value(item, "user"), Value(item, "username"), "Anonymous"),
                    Amount = DecimalValue(item, "amount"),
                    Currency = First(Value(item, "currency"), "USD"),
                    Message = Value(item, "message"),
                    Timestamp = DateTime.UtcNow,
                    IsAnonymous = string.IsNullOrWhiteSpace(First(Value(item, "user"), Value(item, "username"))),
                    RawJson = item.ToString(Formatting.None)
                });
            }

            return result;
        }
    }

    private static string Value(JToken token, string name)
    {
        return token[name] == null ? "" : token[name].ToString();
    }

    private static decimal DecimalValue(JToken token, string name)
    {
        decimal value;
        return decimal.TryParse(Value(token, name), NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : 0;
    }

    private static string First(params string[] values)
    {
        foreach (var value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        return "";
    }
}

public class StreamElementsProvider : IDonationProvider
{
    private const string ProviderKey = "streamelements";
    private const string SocketUrl = "wss://astro.streamelements.com/";
    private readonly BridgeSettings Settings;
    private readonly BridgeLogger Logger;
    private ClientWebSocket Socket;
    private CancellationTokenSource Cancellation;

    public string ProviderName { get { return "StreamElements"; } }
    public event Action<UnifiedDonationEvent> DonationReceived;

    public StreamElementsProvider(BridgeSettings settings, BridgeLogger logger)
    {
        Settings = settings;
        Logger = logger;
    }

    public Task ConnectAsync()
    {
        if (!Settings.GetBool("streamelements.enabled", false))
            return Task.FromResult(0);

        string accountId = Settings.Get("streamelements.accountId", "");
        string jwtToken = Settings.Get("streamelements.jwtToken", "");

        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(jwtToken))
        {
            Logger.Warn("StreamElements включен, но Account ID или JWT Token не задан.");
            ProviderDiagnostics.Missing(Settings, ProviderKey, "missing account ID or JWT token");
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
        ProviderDiagnostics.Started(Settings, ProviderKey);
        Task.Run(() => SocketLoop(accountId, jwtToken, Cancellation.Token));
        Logger.Info("StreamElements: подключение запущено в фоне.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        try
        {
            if (Cancellation != null)
                Cancellation.Cancel();

            if (Socket != null)
            {
                Socket.Abort();
                Socket.Dispose();
                Socket = null;
            }
        }
        catch { }

        ProviderDiagnostics.Stopped(Settings, ProviderKey);
        return Task.FromResult(0);
    }

    public Task<bool> ValidateCredentialsAsync()
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(Settings.Get("streamelements.accountId", ""))
            && !string.IsNullOrWhiteSpace(Settings.Get("streamelements.jwtToken", "")));
    }

    private async Task SocketLoop(string accountId, string jwtToken, CancellationToken token)
    {
        int retryDelayMs = 3000;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await ConnectOnce(accountId, jwtToken, token);
                retryDelayMs = 3000;
            }
            catch (Exception ex)
            {
                ProviderDiagnostics.Disconnected(Settings, ProviderKey, ex.Message);
                Logger.Warn("StreamElements: соединение прервано. " + ex.Message);
            }

            if (!token.IsCancellationRequested)
            {
                await Task.Delay(retryDelayMs, token);
                retryDelayMs = Math.Min(retryDelayMs * 2, 30000);
            }
        }
    }

    private async Task ConnectOnce(string accountId, string jwtToken, CancellationToken token)
    {
        Socket = new ClientWebSocket();
        await Socket.ConnectAsync(new Uri(SocketUrl), token);
        await ReceiveText(Socket, token); // welcome

        await Subscribe("channel.tips", accountId, jwtToken, token);
        Logger.Info("StreamElements: WebSocket подключен к channel.tips.");
        ProviderDiagnostics.Connected(Settings, ProviderKey);

        while (Socket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            string raw = await ReceiveText(Socket, token);
            if (!string.IsNullOrWhiteSpace(raw))
                HandleMessage(raw);
        }
    }

    private async Task Subscribe(string topic, string accountId, string jwtToken, CancellationToken token)
    {
        string payload = JsonConvert.SerializeObject(new
        {
            type = "subscribe",
            nonce = Guid.NewGuid().ToString("N"),
            data = new
            {
                topic = topic,
                room = accountId,
                token = jwtToken,
                token_type = "jwt"
            }
        });

        await SendText(Socket, payload, token);
        string response = await ReceiveText(Socket, token);
        Logger.Debug("StreamElements subscribe response: " + response);

        JObject json = JObject.Parse(response);
        if (json["error"] != null)
            throw new Exception("Subscribe error: " + json["error"].ToString());
    }

    private void HandleMessage(string rawJson)
    {
        try
        {
            JObject root = JObject.Parse(rawJson);
            if (!SafeString(root["type"]).Equals("message", StringComparison.OrdinalIgnoreCase))
                return;

            string topic = SafeString(root["topic"]);
            if (!topic.Equals("channel.tips", StringComparison.OrdinalIgnoreCase))
                return;

            JToken data = root["data"];
            if (data == null)
                return;

            JToken donation = data["donation"] ?? data;
            string id = FirstNonEmpty(SafeString(data["_id"]), SafeString(data["transactionId"]), SafeString(root["id"]));
            string userName = FirstNonEmpty(
                SafeString(donation["user"] != null ? donation["user"]["username"] : null),
                SafeString(donation["user"] != null ? donation["user"]["displayName"] : null),
                SafeString(donation["username"]),
                "Anonymous"
            );

            var donationEvent = new UnifiedDonationEvent
            {
                Source = "StreamElements",
                ProviderName = "StreamElements",
                EventType = "donation",
                DonationId = id,
                UserName = userName,
                Amount = SafeDecimal(donation["amount"]),
                Currency = SafeString(donation["currency"]),
                Message = SafeString(donation["message"]),
                Timestamp = SafeDate(FirstNonEmpty(SafeString(data["createdAt"]), SafeString(root["ts"]))),
                IsAnonymous = userName == "Anonymous",
                RawJson = rawJson
            };

            if (donationEvent.Amount > 0 && DonationReceived != null)
                DonationReceived(donationEvent);
        }
        catch (Exception ex)
        {
            Logger.Warn("StreamElements: не удалось разобрать событие. " + ex.Message);
        }
    }

    private static async Task SendText(ClientWebSocket socket, string text, CancellationToken token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
    }

    private static async Task<string> ReceiveText(ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[8192];
        using (var stream = new MemoryStream())
        {
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                if (result.MessageType == WebSocketMessageType.Close)
                    return "";
                stream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    private static string SafeString(JToken token)
    {
        return token == null ? "" : token.ToString();
    }

    private static decimal SafeDecimal(JToken token)
    {
        decimal value;
        return decimal.TryParse(SafeString(token), NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : 0;
    }

    private static DateTime SafeDate(string value)
    {
        DateTime parsed;
        return DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out parsed) ? parsed.ToUniversalTime() : DateTime.UtcNow;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (string value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        return "";
    }
}

public class StreamlabsProvider : IDonationProvider
{
    private const string ProviderKey = "streamlabs";
    private readonly BridgeSettings Settings;
    private readonly BridgeLogger Logger;
    private CancellationTokenSource Cancellation;

    public string ProviderName { get { return "Streamlabs"; } }
    public event Action<UnifiedDonationEvent> DonationReceived;

    public StreamlabsProvider(BridgeSettings settings, BridgeLogger logger)
    {
        Settings = settings;
        Logger = logger;
    }

    public Task ConnectAsync()
    {
        if (!Settings.GetBool("streamlabs.enabled", false))
            return Task.FromResult(0);

        string token = Settings.Get("streamlabs.token", "");
        if (string.IsNullOrWhiteSpace(token))
        {
            Logger.Warn("Streamlabs включен, но token не задан.");
            ProviderDiagnostics.Missing(Settings, ProviderKey, "missing token");
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
        ProviderDiagnostics.Started(Settings, ProviderKey);
        Task.Run(() => PollLoop(token, Cancellation.Token));
        Logger.Info("Streamlabs polling запущен.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        if (Cancellation != null)
            Cancellation.Cancel();
        ProviderDiagnostics.Stopped(Settings, ProviderKey);
        return Task.FromResult(0);
    }

    public Task<bool> ValidateCredentialsAsync()
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(Settings.Get("streamlabs.token", "")));
    }

    private async Task PollLoop(string token, CancellationToken cancel)
    {
        string lastId = Settings.Get("streamlabs.lastDonationId", "");
        bool baselineOnly = string.IsNullOrWhiteSpace(lastId);
        DateTime acceptFrom = DateTime.UtcNow.AddMinutes(-5);

        while (!cancel.IsCancellationRequested)
        {
            try
            {
                var donations = Fetch(token, lastId);
                ProviderDiagnostics.PollOk(Settings, ProviderKey, donations.Count);
                string newestId = lastId;
                foreach (var donation in donations)
                {
                    if (!string.IsNullOrWhiteSpace(donation.DonationId))
                        newestId = donation.DonationId;
                    if (!baselineOnly && donation.Timestamp >= acceptFrom && DonationReceived != null)
                        DonationReceived(donation);
                }

                if (!string.IsNullOrWhiteSpace(newestId) && newestId != lastId)
                {
                    lastId = newestId;
                    Settings.Set("streamlabs.lastDonationId", lastId, true);
                }

                if (baselineOnly)
                    baselineOnly = false;
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                ProviderDiagnostics.Disconnected(Settings, ProviderKey, ex.Message);
                Logger.Warn("Streamlabs: ошибка polling. " + ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(15), cancel);
        }
    }

    private List<UnifiedDonationEvent> Fetch(string token, string afterId)
    {
        string url = "https://streamlabs.com/api/v2.0/donations?limit=100";
        if (!string.IsNullOrWhiteSpace(afterId))
            url += "&after=" + Uri.EscapeDataString(afterId);

        string raw = HttpGet(url, Bearer(token));
        JToken items = Items(JToken.Parse(raw));
        var result = new List<UnifiedDonationEvent>();
        if (items == null)
            return result;

        var reversed = new List<JToken>();
        foreach (JToken item in items)
            reversed.Add(item);
        reversed.Reverse();

        foreach (JToken item in reversed)
        {
            var donation = new UnifiedDonationEvent
            {
                Source = "Streamlabs",
                ProviderName = "Streamlabs",
                EventType = "donation",
                DonationId = First(S(item["donation_id"]), S(item["id"])),
                UserName = First(S(item["name"]), S(item["from"]), S(item["username"]), "Anonymous"),
                Amount = Dec(First(S(item["amount"]), S(item["formatted_amount"]))),
                Currency = First(S(item["currency"]), "USD"),
                Message = First(S(item["message"]), S(item["comment"])),
                Timestamp = Date(First(S(item["created_at"]), S(item["createdAt"]), S(item["timestamp"]))),
                IsAnonymous = string.IsNullOrWhiteSpace(First(S(item["name"]), S(item["from"]), S(item["username"]))),
                RawJson = item.ToString(Formatting.None)
            };
            if (donation.Amount > 0)
                result.Add(donation);
        }

        return result;
    }

    private static Dictionary<string, string> Bearer(string token)
    {
        return new Dictionary<string, string> { { "Authorization", "Bearer " + token } };
    }

    internal static string HttpGet(string url, Dictionary<string, string> headers)
    {
        var request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = "GET";
        request.Accept = "application/json";
        if (headers != null)
            foreach (var header in headers)
                request.Headers[header.Key] = header.Value;

        using (var response = (HttpWebResponse)request.GetResponse())
        using (var stream = response.GetResponseStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
            return reader.ReadToEnd();
    }

    internal static JToken Items(JToken root)
    {
        if (root == null)
            return null;
        if (root.Type == JTokenType.Array)
            return root;
        if (root["data"] != null && root["data"].Type == JTokenType.Array)
            return root["data"];
        if (root["data"] != null && root["data"].Type == JTokenType.Object && root["data"]["items"] != null)
            return root["data"]["items"];
        if (root["items"] != null)
            return root["items"];
        if (root["result"] != null)
            return root["result"];
        return null;
    }

    internal static string S(JToken token)
    {
        return token == null ? "" : token.ToString();
    }

    internal static decimal Dec(string value)
    {
        decimal result;
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : 0;
    }

    internal static DateTime Date(string value)
    {
        DateTime parsed;
        return DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out parsed) ? parsed.ToUniversalTime() : DateTime.UtcNow;
    }

    internal static string First(params string[] values)
    {
        foreach (string value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        return "";
    }
}

public class DonatePayProvider : IDonationProvider
{
    private readonly string ProviderKey;
    private readonly string ProviderDisplayName;
    private readonly string DefaultApiHost;
    private readonly BridgeSettings Settings;
    private readonly BridgeLogger Logger;
    private CancellationTokenSource Cancellation;

    public string ProviderName { get { return ProviderDisplayName; } }
    public event Action<UnifiedDonationEvent> DonationReceived;

    public DonatePayProvider(BridgeSettings settings, BridgeLogger logger, string providerKey, string providerDisplayName, string defaultApiHost)
    {
        Settings = settings;
        Logger = logger;
        ProviderKey = providerKey;
        ProviderDisplayName = providerDisplayName;
        DefaultApiHost = defaultApiHost;
    }

    public Task ConnectAsync()
    {
        if (!Settings.GetBool(ProviderKey + ".enabled", false))
            return Task.FromResult(0);

        string apiKey = Settings.Get(ProviderKey + ".apiKey", "");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Logger.Warn(ProviderDisplayName + " включен, но API access key не задан.");
            ProviderDiagnostics.Missing(Settings, ProviderKey, "missing API key");
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
        ProviderDiagnostics.Started(Settings, ProviderKey);
        Task.Run(() => PollLoop(apiKey, Cancellation.Token));
        Logger.Info(ProviderDisplayName + " polling запущен.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        if (Cancellation != null)
            Cancellation.Cancel();
        ProviderDiagnostics.Stopped(Settings, ProviderKey);
        return Task.FromResult(0);
    }

    public Task<bool> ValidateCredentialsAsync()
    {
        string apiKey = Settings.Get(ProviderKey + ".apiKey", "");
        if (string.IsNullOrWhiteSpace(apiKey))
            return Task.FromResult(false);

        try
        {
            HttpGetJson(ApiBase() + "/api/v1/user?access_token=" + Uri.EscapeDataString(apiKey));
            Logger.Info(ProviderDisplayName + ": API key успешно проверен.");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Logger.Warn(ProviderDisplayName + ": API key не прошел проверку. " + ex.Message);
            return Task.FromResult(false);
        }
    }

    private async Task PollLoop(string apiKey, CancellationToken token)
    {
        string lastId = Settings.Get(ProviderKey + ".lastTransactionId", "");
        var seenIds = LoadSeenIds();
        bool baselineOnly = string.IsNullOrWhiteSpace(lastId) && seenIds.Count == 0;
        DateTime acceptFrom = DateTime.UtcNow.AddMinutes(-5);
        DateTime enabledAt = SafeDate(Settings.Get(ProviderKey + ".enabledAt", DateTime.UtcNow.ToString("o"))).AddSeconds(-10);
        TimeSpan pollDelay = TimeSpan.FromSeconds(PollSeconds());

        while (!token.IsCancellationRequested)
        {
            try
            {
                Settings.Set(ProviderKey + ".diagnostics.nextRetryAt", "now", true);
                List<UnifiedDonationEvent> donations = FetchDonations(apiKey, lastId);
                ProviderDiagnostics.PollOk(Settings, ProviderKey, donations.Count);
                Settings.Set(ProviderKey + ".diagnostics.polling", "started", true);
                Settings.Set(ProviderKey + ".diagnostics.lastFetchAt", DateTime.UtcNow.ToString("o"), true);
                Settings.Set(ProviderKey + ".diagnostics.lastFetchCount", donations.Count.ToString(CultureInfo.InvariantCulture), true);
                Settings.Set(ProviderKey + ".diagnostics.lastNewCount", "0", true);
                Settings.Set(ProviderKey + ".diagnostics.lastError", "none", true);
                Settings.Set(ProviderKey + ".diagnostics.seenCount", seenIds.Count.ToString(CultureInfo.InvariantCulture), true);
                Logger.Debug(ProviderDisplayName + ": fetched " + donations.Count.ToString(CultureInfo.InvariantCulture) + " donations.");
                string newestId = lastId;
                int newCount = 0;

                foreach (var donation in donations)
                {
                    if (!string.IsNullOrWhiteSpace(donation.DonationId))
                        newestId = donation.DonationId;

                    string seenId = BuildSeenId(donation);
                    if (seenIds.Contains(seenId))
                        continue;

                    seenIds.Add(seenId);
                    bool isFreshAfterSetup = donation.Timestamp >= enabledAt && donation.Timestamp >= acceptFrom;
                    if ((!baselineOnly || isFreshAfterSetup) && DonationReceived != null)
                    {
                        newCount++;
                        DonationReceived(donation);
                    }
                }
                Settings.Set(ProviderKey + ".diagnostics.lastNewCount", newCount.ToString(CultureInfo.InvariantCulture), true);
                SaveSeenIds(seenIds);
                Settings.Set(ProviderKey + ".diagnostics.seenCount", seenIds.Count.ToString(CultureInfo.InvariantCulture), true);

                if (!string.IsNullOrWhiteSpace(newestId) && newestId != lastId)
                {
                    lastId = newestId;
                    Settings.Set(ProviderKey + ".lastTransactionId", lastId, true);
                }

                if (baselineOnly)
                {
                    baselineOnly = false;
                    Logger.Info(ProviderDisplayName + ": текущая история отмечена, новые донаты будут отправляться в Streamer.bot.");
                }
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                ProviderDiagnostics.Disconnected(Settings, ProviderKey, ex.Message);
                Settings.Set(ProviderKey + ".diagnostics.lastError", ex.Message, true);
                if (ex.Message.IndexOf("429", StringComparison.OrdinalIgnoreCase) >= 0
                    || ex.Message.IndexOf("Too Many Requests", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pollDelay = TimeSpan.FromMinutes(5);
                    Settings.Set(ProviderKey + ".diagnostics.nextRetryAt", DateTime.UtcNow.Add(pollDelay).ToString("o"), true);
                }
                if (ex.Message.IndexOf("Incorrect token", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Settings.Set(ProviderKey + ".diagnostics.polling", "stopped-invalid-token", true);
                    Settings.Set(ProviderKey + ".diagnostics.nextRetryAt", "n/a", true);
                    Logger.Warn(ProviderDisplayName + ": API key отклонен сервисом. Проверьте ключ из DonatePay -> API -> Your API key.");
                    return;
                }
                Logger.Warn(ProviderDisplayName + ": ошибка polling. " + ex.Message);
            }

            await Task.Delay(pollDelay, token);
            pollDelay = TimeSpan.FromSeconds(PollSeconds());
        }
    }

    private int PollSeconds()
    {
        int seconds;
        if (!int.TryParse(Settings.Get(ProviderKey + ".pollSeconds", "20"), NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
            seconds = 20;
        if (seconds < 10)
            seconds = 10;
        return seconds;
    }

    private HashSet<string> LoadSeenIds()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string raw = Settings.Get(ProviderKey + ".seenDonationIds", "");
        if (string.IsNullOrWhiteSpace(raw))
            return result;
        foreach (string part in raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            result.Add(part);
        return result;
    }

    private void SaveSeenIds(HashSet<string> seenIds)
    {
        var items = new List<string>();
        foreach (string id in seenIds)
            items.Add(id);
        items.Sort(StringComparer.OrdinalIgnoreCase);
        if (items.Count > 300)
            items = items.GetRange(items.Count - 300, 300);
        Settings.Set(ProviderKey + ".seenDonationIds", string.Join("|", items.ToArray()), true);
    }

    private string BuildSeenId(UnifiedDonationEvent donation)
    {
        if (!string.IsNullOrWhiteSpace(donation.DonationId))
            return donation.DonationId.Trim();
        return (donation.UserName ?? "Anonymous") + "_"
            + donation.Amount.ToString(CultureInfo.InvariantCulture) + "_"
            + (donation.Currency ?? "") + "_"
            + donation.Timestamp.ToUniversalTime().ToString("o");
    }

    private List<UnifiedDonationEvent> FetchDonations(string apiKey, string afterId)
    {
        bool initialFetch = string.IsNullOrWhiteSpace(afterId);
        string url = ApiBase() + "/api/v1/transactions?access_token=" + Uri.EscapeDataString(apiKey)
            + "&limit=25&order=" + (initialFetch ? "DESC" : "ASC");

        if (!initialFetch)
            url += "&after=" + Uri.EscapeDataString(afterId);

        string raw = HttpGetJson(url);
        JToken root = JToken.Parse(raw);
        Settings.Set(ProviderKey + ".diagnostics.root", DescribeRoot(root), true);
        Settings.Set(ProviderKey + ".diagnostics.apiStatus", SafeString(root["status"]), true);
        Settings.Set(ProviderKey + ".diagnostics.apiMessage", SafeString(root["message"]), true);
        JToken items = ExtractItems(root);
        var result = new List<UnifiedDonationEvent>();

        if (items == null)
        {
            Settings.Set(ProviderKey + ".diagnostics.firstItemShape", "items-not-found", true);
            string apiStatus = SafeString(root["status"]);
            string apiMessage = SafeString(root["message"]);
            if (!string.IsNullOrWhiteSpace(apiStatus) || !string.IsNullOrWhiteSpace(apiMessage))
            {
                throw new Exception("DonatePay API: status=" + FirstNonEmpty(apiStatus, "n/a") + ", message=" + FirstNonEmpty(apiMessage, "n/a"));
            }
            return result;
        }

        Settings.Set(ProviderKey + ".diagnostics.firstItemShape", FirstItemShape(items), true);
        int skippedCashout = 0;
        int skippedStatus = 0;
        int skippedAmount = 0;

        foreach (JToken item in items)
        {
            string type = SafeString(item["type"]);
            string status = SafeString(item["status"]);
            if (type.Equals("cashout", StringComparison.OrdinalIgnoreCase))
            {
                skippedCashout++;
                continue;
            }
            if (status.Equals("cancel", StringComparison.OrdinalIgnoreCase)
                || status.Equals("cancelled", StringComparison.OrdinalIgnoreCase)
                || status.Equals("canceled", StringComparison.OrdinalIgnoreCase)
                || status.Equals("wait", StringComparison.OrdinalIgnoreCase)
                || status.Equals("pending", StringComparison.OrdinalIgnoreCase))
            {
                skippedStatus++;
                continue;
            }

            JToken vars = item["vars"];
            string userName = FirstNonEmpty(
                SafeString(item["what"]),
                SafeString(vars != null ? vars["name"] : null),
                SafeString(vars != null ? vars["username"] : null),
                "Anonymous"
            );

            var donation = new UnifiedDonationEvent
            {
                Source = ProviderDisplayNameForHost(),
                ProviderName = ProviderDisplayNameForHost(),
                EventType = "donation",
                DonationId = SafeString(item["id"]),
                UserName = userName,
                Amount = SafeDecimal(FirstNonEmpty(
                    SafeString(item["sum"]),
                    SafeString(item["amount"]),
                    SafeString(item["to_cash"]),
                    SafeString(item["to_pay"]),
                    SafeString(vars != null ? vars["sum"] : null),
                    SafeString(vars != null ? vars["amount"] : null))),
                Currency = FirstNonEmpty(SafeString(item["currency"]), SafeString(vars != null ? vars["currency"] : null), "RUB"),
                Message = FirstNonEmpty(SafeString(item["comment"]), SafeString(vars != null ? vars["comment"] : null)),
                Timestamp = SafeDate(SafeString(item["created_at"])),
                IsAnonymous = userName == "Anonymous",
                RawJson = item.ToString(Formatting.None)
            };

            if (donation.Amount > 0)
                result.Add(donation);
            else
                skippedAmount++;
        }

        if (initialFetch)
            result.Reverse();

        Settings.Set(ProviderKey + ".diagnostics.skipped",
            "cashout=" + skippedCashout.ToString(CultureInfo.InvariantCulture)
            + ",status=" + skippedStatus.ToString(CultureInfo.InvariantCulture)
            + ",amount=" + skippedAmount.ToString(CultureInfo.InvariantCulture), true);

        return result;
    }

    private string ProviderDisplayNameForHost()
    {
        string host = ApiBase().ToLowerInvariant();
        if (host.IndexOf("donatepay.ru", StringComparison.OrdinalIgnoreCase) >= 0)
            return "DonatePay RU";
        if (host.IndexOf("donatepay.eu", StringComparison.OrdinalIgnoreCase) >= 0)
            return "DonatePay EU";
        return ProviderDisplayName;
    }

    private string ApiBase()
    {
        return Settings.Get(ProviderKey + ".apiHost", DefaultApiHost).Trim().TrimEnd('/');
    }

    private static JToken ExtractItems(JToken root)
    {
        if (root == null)
            return null;
        if (root.Type == JTokenType.Array)
            return root;
        JToken status = root["status"];
        JToken message = root["message"];
        if (status != null && message != null && message.Type == JTokenType.Array)
            return message;
        if (status != null && root["data"] == null && root["items"] == null && root["transactions"] == null)
            return null;
        JToken data = root["data"];
        if (data != null && data.Type == JTokenType.Array)
            return data;
        if (data != null && data.Type == JTokenType.Object && data["items"] != null)
            return data["items"];
        if (data != null && data.Type == JTokenType.Object && data["transactions"] != null)
            return data["transactions"];
        if (data != null && data.Type == JTokenType.Object && data["list"] != null)
            return data["list"];
        if (root["items"] != null)
            return root["items"];
        if (root["transactions"] != null)
            return root["transactions"];
        if (root["list"] != null)
            return root["list"];
        JToken nested = FindFirstObjectArray(root);
        if (nested != null)
            return nested;
        return null;
    }

    private static JToken FindFirstObjectArray(JToken token)
    {
        if (token == null)
            return null;
        if (token.Type == JTokenType.Array)
        {
            foreach (JToken item in token)
                if (item != null && item.Type == JTokenType.Object)
                    return token;
            return null;
        }

        foreach (JToken child in token.Children())
        {
            JToken found = FindFirstObjectArray(child);
            if (found != null)
                return found;
        }

        return null;
    }

    private static string DescribeRoot(JToken root)
    {
        if (root == null)
            return "null";
        if (root.Type == JTokenType.Object)
        {
            var names = new List<string>();
            foreach (JProperty prop in root.Children<JProperty>())
                names.Add(prop.Name + ":" + prop.Value.Type);
            return string.Join(",", names.ToArray());
        }
        if (root.Type == JTokenType.Array)
        {
            int count = 0;
            foreach (JToken ignored in root)
                count++;
            return "array:" + count.ToString(CultureInfo.InvariantCulture);
        }
        return root.Type.ToString();
    }

    private static string FirstItemShape(JToken items)
    {
        if (items == null)
            return "none";
        JToken first = null;
        foreach (JToken item in items)
        {
            first = item;
            break;
        }
        if (first == null)
            return "empty";
        if (first.Type != JTokenType.Object)
            return first.Type.ToString();
        var names = new List<string>();
        foreach (JProperty prop in first.Children<JProperty>())
            names.Add(prop.Name);
        return string.Join(",", names.ToArray());
    }

    private static string HttpGetJson(string url)
    {
        var request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = "GET";
        request.Accept = "application/json";

        using (var response = (HttpWebResponse)request.GetResponse())
        using (var stream = response.GetResponseStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
            return reader.ReadToEnd();
    }

    private static string SafeString(JToken token)
    {
        return token == null ? "" : token.ToString();
    }

    private static decimal SafeDecimal(string value)
    {
        decimal result;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            return result;
        if (!string.IsNullOrWhiteSpace(value))
        {
            string normalized = value.Trim().Replace(" ", "").Replace(',', '.');
            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
                return result;
        }
        return 0;
    }

    private static DateTime SafeDate(string value)
    {
        DateTime parsed;
        return DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out parsed) ? parsed.ToUniversalTime() : DateTime.UtcNow;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (string value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        return "";
    }
}

public class DonateStreamProvider : IDonationProvider
{
    private const string ProviderKey = "donatestream";
    private readonly BridgeSettings Settings;
    private readonly BridgeLogger Logger;
    private CancellationTokenSource Cancellation;

    public string ProviderName { get { return "Donate.Stream"; } }
    public event Action<UnifiedDonationEvent> DonationReceived;

    public DonateStreamProvider(BridgeSettings settings, BridgeLogger logger)
    {
        Settings = settings;
        Logger = logger;
    }

    public Task ConnectAsync()
    {
        if (!Settings.GetBool("donatestream.enabled", false))
            return Task.FromResult(0);

        string endpoint = Settings.Get("donatestream.endpoint", "");
        string token = Settings.Get("donatestream.token", "");
        if (string.IsNullOrWhiteSpace(endpoint))
            endpoint = "https://donate.stream/api/v1/donateAlert.paginate?page=1";

        Cancellation = new CancellationTokenSource();
        ProviderDiagnostics.Started(Settings, ProviderKey);
        Task.Run(() => PollLoop(endpoint, token, Cancellation.Token));
        Logger.Info("Donate.Stream polling запущен.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        if (Cancellation != null)
            Cancellation.Cancel();
        ProviderDiagnostics.Stopped(Settings, ProviderKey);
        return Task.FromResult(0);
    }

    public Task<bool> ValidateCredentialsAsync()
    {
        return Task.FromResult(Settings.GetBool("donatestream.enabled", false));
    }

    private async Task PollLoop(string endpoint, string token, CancellationToken cancel)
    {
        string lastId = Settings.Get("donatestream.lastDonationId", "");
        bool baselineOnly = string.IsNullOrWhiteSpace(lastId);
        DateTime acceptFrom = DateTime.UtcNow.AddMinutes(-5);

        while (!cancel.IsCancellationRequested)
        {
            try
            {
                var donations = Fetch(endpoint, token);
                ProviderDiagnostics.PollOk(Settings, ProviderKey, donations.Count);
                string newestId = lastId;
                foreach (var donation in donations)
                {
                    if (!string.IsNullOrWhiteSpace(donation.DonationId))
                        newestId = donation.DonationId;
                    if (!baselineOnly && donation.Timestamp >= acceptFrom && DonationReceived != null && (string.IsNullOrWhiteSpace(lastId) || donation.DonationId != lastId))
                        DonationReceived(donation);
                }

                if (!string.IsNullOrWhiteSpace(newestId) && newestId != lastId)
                {
                    lastId = newestId;
                    Settings.Set("donatestream.lastDonationId", lastId, true);
                }

                if (baselineOnly)
                    baselineOnly = false;
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                ProviderDiagnostics.Disconnected(Settings, ProviderKey, ex.Message);
                Logger.Warn("Donate.Stream: ошибка polling. " + ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(15), cancel);
        }
    }

    private List<UnifiedDonationEvent> Fetch(string endpoint, string token)
    {
        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(token))
        {
            headers["Authorization"] = "Bearer " + token;
            headers["X-Api-Token"] = token;
        }

        string raw = StreamlabsProvider.HttpGet(endpoint, headers);
        JToken items = StreamlabsProvider.Items(JToken.Parse(raw));
        var result = new List<UnifiedDonationEvent>();
        if (items == null)
            return result;

        var reversed = new List<JToken>();
        foreach (JToken item in items)
            reversed.Add(item);
        reversed.Reverse();

        foreach (JToken item in reversed)
        {
            string id = StreamlabsProvider.First(V(item, "uid"), V(item, "id"), V(item, "donationId"));
            string user = StreamlabsProvider.First(V(item, "username"), V(item, "name"), V(item, "from"), V(item, "user"), "Anonymous");
            var donation = new UnifiedDonationEvent
            {
                Source = "Donate.Stream",
                ProviderName = "Donate.Stream",
                EventType = "donation",
                DonationId = id,
                UserName = user,
                Amount = StreamlabsProvider.Dec(StreamlabsProvider.First(V(item, "amount"), V(item, "sum"), V(item, "price"))),
                Currency = StreamlabsProvider.First(V(item, "currency"), V(item, "currency_code"), "RUB"),
                Message = StreamlabsProvider.First(V(item, "message"), V(item, "comment")),
                Timestamp = StreamlabsProvider.Date(StreamlabsProvider.First(V(item, "received_at"), V(item, "created_at"), V(item, "date"))),
                IsAnonymous = user == "Anonymous",
                RawJson = item.ToString(Formatting.None)
            };
            if (donation.Amount > 0)
                result.Add(donation);
        }

        return result;
    }

    private static string V(JToken item, string name)
    {
        return item[name] == null ? "" : item[name].ToString();
    }
}

public class DeStreamProvider : IDonationProvider
{
    private const string ProviderKey = "destream";
    private readonly BridgeSettings Settings;
    private readonly BridgeLogger Logger;
    private CancellationTokenSource Cancellation;

    public string ProviderName { get { return "deStream"; } }
    public event Action<UnifiedDonationEvent> DonationReceived;

    public DeStreamProvider(BridgeSettings settings, BridgeLogger logger)
    {
        Settings = settings;
        Logger = logger;
    }

    public Task ConnectAsync()
    {
        if (!Settings.GetBool("destream.enabled", false))
            return Task.FromResult(0);

        string clientId = Settings.Get("destream.clientId", "");
        string accessToken = Settings.Get("destream.accessToken", "");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(accessToken))
        {
            Logger.Warn("deStream включен, но clientId/accessToken не заданы.");
            ProviderDiagnostics.Missing(Settings, ProviderKey, "missing client ID or access token");
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
        ProviderDiagnostics.Started(Settings, ProviderKey);
        Task.Run(() => PollLoop(clientId, accessToken, Cancellation.Token));
        Logger.Info("deStream polling запущен.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        if (Cancellation != null)
            Cancellation.Cancel();
        ProviderDiagnostics.Stopped(Settings, ProviderKey);
        return Task.FromResult(0);
    }

    public Task<bool> ValidateCredentialsAsync()
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(Settings.Get("destream.clientId", ""))
            && !string.IsNullOrWhiteSpace(Settings.Get("destream.accessToken", "")));
    }

    private async Task PollLoop(string clientId, string accessToken, CancellationToken cancel)
    {
        string lastDate = Settings.Get("destream.lastDonationDate", "");
        bool baselineOnly = string.IsNullOrWhiteSpace(lastDate);
        DateTime acceptFrom = DateTime.UtcNow.AddMinutes(-5);

        while (!cancel.IsCancellationRequested)
        {
            try
            {
                var donations = Fetch(clientId, accessToken, lastDate);
                ProviderDiagnostics.PollOk(Settings, ProviderKey, donations.Count);
                string newestDate = lastDate;
                foreach (var donation in donations)
                {
                    newestDate = donation.Timestamp.ToString("o");
                    if (!baselineOnly && donation.Timestamp >= acceptFrom && DonationReceived != null)
                        DonationReceived(donation);
                }

                if (!string.IsNullOrWhiteSpace(newestDate) && newestDate != lastDate)
                {
                    lastDate = newestDate;
                    Settings.Set("destream.lastDonationDate", lastDate, true);
                }

                if (baselineOnly)
                    baselineOnly = false;
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                ProviderDiagnostics.Disconnected(Settings, ProviderKey, ex.Message);
                Logger.Warn("deStream: ошибка polling. " + ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(15), cancel);
        }
    }

    private List<UnifiedDonationEvent> Fetch(string clientId, string accessToken, string afterDate)
    {
        string url = "https://api.destream.net/api/v2/users/tips?offset=0&limit=30";
        if (!string.IsNullOrWhiteSpace(afterDate))
            url += "&after_date=" + Uri.EscapeDataString(afterDate);

        string tokenType = Settings.Get("destream.tokenType", "Bearer");
        var headers = new Dictionary<string, string>
        {
            { "X-Api-ClientId", clientId },
            { "Authorization", tokenType + " " + accessToken }
        };
        string raw = StreamlabsProvider.HttpGet(url, headers);
        JToken items = StreamlabsProvider.Items(JToken.Parse(raw));
        var result = new List<UnifiedDonationEvent>();
        if (items == null)
            return result;

        var reversed = new List<JToken>();
        foreach (JToken item in items)
            reversed.Add(item);
        reversed.Reverse();

        foreach (JToken item in reversed)
        {
            string user = StreamlabsProvider.First(S(item["sender"]), "Anonymous");
            var donation = new UnifiedDonationEvent
            {
                Source = "deStream",
                ProviderName = "deStream",
                EventType = "donation",
                DonationId = S(item["payment_id"]),
                UserName = user,
                Amount = StreamlabsProvider.Dec(S(item["amount"])),
                Currency = S(item["currency"]),
                Message = S(item["message"]),
                Timestamp = StreamlabsProvider.Date(S(item["date"])),
                IsAnonymous = user == "Anonymous",
                RawJson = item.ToString(Formatting.None)
            };
            if (donation.Amount > 0)
                result.Add(donation);
        }

        return result;
    }

    private static string S(JToken token)
    {
        return token == null ? "" : token.ToString();
    }
}

public class DonateXProvider : IDonationProvider
{
    private readonly BridgeSettings Settings;
    private readonly BridgeLogger Logger;
    private CancellationTokenSource Cancellation;

    public string ProviderName { get { return "DonateX.gg"; } }
    public event Action<UnifiedDonationEvent> DonationReceived;

    public DonateXProvider(BridgeSettings settings, BridgeLogger logger)
    {
        Settings = settings;
        Logger = logger;
    }

    public Task ConnectAsync()
    {
        if (!Settings.GetBool("donatex.enabled", false))
            return Task.FromResult(0);

        string accessToken = Settings.Get("donatex.accessToken", "");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            Logger.Warn("DonateX включен, но access token не задан.");
            ProviderDiagnostics.Missing(Settings, "donatex", "missing access token");
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
        ProviderDiagnostics.Started(Settings, "donatex");
        Task.Run(() => PollLoop(accessToken, Cancellation.Token));
        Logger.Info("DonateX polling запущен.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        if (Cancellation != null)
            Cancellation.Cancel();
        ProviderDiagnostics.Stopped(Settings, "donatex");
        return Task.FromResult(0);
    }

    public Task<bool> ValidateCredentialsAsync()
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(Settings.Get("donatex.accessToken", "")));
    }

    private async Task PollLoop(string accessToken, CancellationToken cancel)
    {
        var seenIds = LoadSeenIds();
        bool baselineOnly = seenIds.Count == 0;

        while (!cancel.IsCancellationRequested)
        {
            try
            {
                var donations = Fetch(accessToken);
                ProviderDiagnostics.PollOk(Settings, "donatex", donations.Count);
                Settings.Set("donatex.diagnostics.lastFetchAt", DateTime.UtcNow.ToString("o"), true);
                Settings.Set("donatex.diagnostics.lastFetchCount", donations.Count.ToString(CultureInfo.InvariantCulture), true);
                Settings.Set("donatex.diagnostics.lastError", "none", true);
                Logger.Debug("DonateX: fetched " + donations.Count.ToString(CultureInfo.InvariantCulture) + " donations.");
                int newCount = 0;
                int seenCountBefore = seenIds.Count;
                foreach (var donation in donations)
                {
                    string id = BuildSeenId(donation);
                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    if (seenIds.Contains(id))
                        continue;

                    seenIds.Add(id);
                    if (!baselineOnly && DonationReceived != null)
                    {
                        newCount++;
                        DonationReceived(donation);
                    }
                }

                SaveSeenIds(seenIds);
                Settings.Set("donatex.diagnostics.lastNewCount", newCount.ToString(CultureInfo.InvariantCulture), true);
                Settings.Set("donatex.diagnostics.seenCount", seenIds.Count.ToString(CultureInfo.InvariantCulture), true);

                if (baselineOnly)
                {
                    baselineOnly = false;
                    Logger.Info("DonateX: текущая история отмечена (" + (seenIds.Count - seenCountBefore).ToString(CultureInfo.InvariantCulture) + "), новые донаты будут отправляться в Streamer.bot.");
                }
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                ProviderDiagnostics.Disconnected(Settings, "donatex", ex.Message);
                Settings.Set("donatex.diagnostics.lastError", ex.Message, true);
                Logger.Warn("DonateX: ошибка polling. " + ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(PollSeconds()), cancel);
        }
    }

    private int PollSeconds()
    {
        int seconds;
        if (!int.TryParse(Settings.Get("donatex.pollSeconds", "5"), NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
            seconds = 5;
        if (seconds < 5)
            seconds = 5;
        return seconds;
    }

    private HashSet<string> LoadSeenIds()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string raw = Settings.Get("donatex.seenDonationIds", "");
        if (string.IsNullOrWhiteSpace(raw))
            return result;
        foreach (string part in raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            result.Add(part);
        return result;
    }

    private void SaveSeenIds(HashSet<string> seenIds)
    {
        var items = new List<string>();
        foreach (string id in seenIds)
            items.Add(id);
        items.Sort(StringComparer.OrdinalIgnoreCase);
        if (items.Count > 300)
            items = items.GetRange(items.Count - 300, 300);
        Settings.Set("donatex.seenDonationIds", string.Join("|", items.ToArray()), true);
    }

    private string BuildSeenId(UnifiedDonationEvent donation)
    {
        if (!string.IsNullOrWhiteSpace(donation.DonationId))
            return donation.DonationId.Trim();
        return (donation.UserName ?? "Anonymous") + "_"
            + donation.Amount.ToString(CultureInfo.InvariantCulture) + "_"
            + (donation.Currency ?? "") + "_"
            + donation.Timestamp.ToUniversalTime().ToString("o");
    }

    private List<UnifiedDonationEvent> Fetch(string accessToken)
    {
        string apiBase = Settings.Get("donatex.apiBase", "https://donatex.gg/api").TrimEnd('/');
        string url = apiBase + "/v1/donations?skip=0&take=100&hideTest=false";
        var headers = new Dictionary<string, string> { { "Authorization", "Bearer " + accessToken } };
        string raw = StreamlabsProvider.HttpGet(url, headers);
        JToken items = StreamlabsProvider.Items(JToken.Parse(raw));
        var result = new List<UnifiedDonationEvent>();
        if (items == null)
            return result;

        var reversed = new List<JToken>();
        foreach (JToken item in items)
            reversed.Add(item);
        reversed.Reverse();

        foreach (JToken item in reversed)
        {
            string user = StreamlabsProvider.First(
                S(item["username"]),
                S(item["userName"]),
                S(item["name"]),
                S(item["nickname"]),
                S(item["sender"]),
                S(item["payer"]),
                S(item["user"] != null ? item["user"]["name"] : null),
                S(item["user"] != null ? item["user"]["username"] : null),
                "Anonymous");
            string donationId = StreamlabsProvider.First(S(item["id"]), S(item["uid"]), S(item["donationId"]), S(item["paymentId"]));
            string amount = StreamlabsProvider.First(S(item["amount"]), S(item["sum"]), S(item["value"]), S(item["total"]));
            string currency = StreamlabsProvider.First(S(item["currency"]), S(item["currencyCode"]), S(item["currency_code"]), "RUB");
            string message = StreamlabsProvider.First(S(item["message"]), S(item["comment"]), S(item["text"]));
            string timestamp = StreamlabsProvider.First(S(item["timestamp"]), S(item["createdAt"]), S(item["created_at"]), S(item["paidAt"]), S(item["date"]));
            var donation = new UnifiedDonationEvent
            {
                Source = "DonateX.gg",
                ProviderName = "DonateX.gg",
                EventType = "donation",
                DonationId = donationId,
                UserName = user,
                Amount = StreamlabsProvider.Dec(amount),
                Currency = currency,
                Message = message,
                Timestamp = StreamlabsProvider.Date(timestamp),
                IsAnonymous = user == "Anonymous",
                RawJson = item.ToString(Formatting.None)
            };
            if (donation.Amount > 0)
                result.Add(donation);
        }

        return result;
    }

    private static string S(JToken token)
    {
        return token == null ? "" : token.ToString();
    }
}

public class OdaProvider : IDonationProvider
{
    private const string ProviderKey = "oda";
    private const string DefaultApiBase = "https://api.oda.digital";
    private readonly BridgeSettings Settings;
    private readonly BridgeLogger Logger;
    private CancellationTokenSource Cancellation;

    public string ProviderName { get { return "ODA"; } }
    public event Action<UnifiedDonationEvent> DonationReceived;

    public OdaProvider(BridgeSettings settings, BridgeLogger logger)
    {
        Settings = settings;
        Logger = logger;
    }

    public Task ConnectAsync()
    {
        if (!Settings.GetBool(ProviderKey + ".enabled", false))
            return Task.FromResult(0);

        string accessToken = Settings.Get(ProviderKey + ".accessToken", "");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            Logger.Warn("ODA включен, но access token отсутствует.");
            ProviderDiagnostics.Missing(Settings, ProviderKey, "missing access token");
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
        ProviderDiagnostics.Started(Settings, ProviderKey);
        Task.Run(() => PollLoop(accessToken, Cancellation.Token));
        Logger.Info("ODA polling запущен.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        try
        {
            if (Cancellation != null)
                Cancellation.Cancel();
        }
        catch { }

        ProviderDiagnostics.Stopped(Settings, ProviderKey);
        return Task.FromResult(0);
    }

    public Task<bool> ValidateCredentialsAsync()
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(Settings.Get(ProviderKey + ".accessToken", "")));
    }

    private async Task PollLoop(string accessToken, CancellationToken cancel)
    {
        bool baselineOnly = string.IsNullOrWhiteSpace(Settings.Get(ProviderKey + ".lastTimestamp", ""));
        DateTime enabledAt = StreamlabsProvider.Date(Settings.Get(ProviderKey + ".enabledAt", DateTime.UtcNow.ToString("o"))).AddSeconds(-10);

        while (!cancel.IsCancellationRequested)
        {
            try
            {
                string lastTimestamp = Settings.Get(ProviderKey + ".lastTimestamp", "");
                List<string> seenIds = ReadSeenIds();
                List<UnifiedDonationEvent> donations = Fetch(accessToken, lastTimestamp);
                ProviderDiagnostics.PollOk(Settings, ProviderKey, donations.Count);

                string newestTimestamp = lastTimestamp;
                int newCount = 0;
                foreach (UnifiedDonationEvent donation in donations)
                {
                    if (!string.IsNullOrWhiteSpace(donation.DonationId) && seenIds.Contains(donation.DonationId))
                        continue;

                    if (!string.IsNullOrWhiteSpace(donation.DonationId))
                        AddSeenId(seenIds, donation.DonationId);

                    if (string.IsNullOrWhiteSpace(newestTimestamp) || donation.Timestamp > StreamlabsProvider.Date(newestTimestamp))
                        newestTimestamp = donation.Timestamp.ToUniversalTime().ToString("o");

                    bool isFreshAfterSetup = donation.Timestamp >= enabledAt;
                    if ((!baselineOnly || isFreshAfterSetup) && DonationReceived != null)
                    {
                        DonationReceived(donation);
                        newCount++;
                    }
                }

                Settings.Set(ProviderKey + ".diagnostics.lastNewCount", newCount.ToString(CultureInfo.InvariantCulture), true);
                SaveSeenIds(seenIds);
                if (!string.IsNullOrWhiteSpace(newestTimestamp))
                    Settings.Set(ProviderKey + ".lastTimestamp", newestTimestamp, true);

                if (baselineOnly)
                    baselineOnly = false;
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                ProviderDiagnostics.Disconnected(Settings, ProviderKey, ex.Message);
                Settings.Set(ProviderKey + ".diagnostics.lastError", ex.Message, true);
                Logger.Warn("ODA: ошибка polling. " + ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(PollSeconds()), cancel);
        }
    }

    private List<UnifiedDonationEvent> Fetch(string accessToken, string after)
    {
        string url = HistoryEndpoint();
        url += url.IndexOf("?", StringComparison.Ordinal) >= 0 ? "&" : "?";
        url += "page=0&size=50&sort=timestamp,asc";
        if (!string.IsNullOrWhiteSpace(after))
            url += "&after=" + Uri.EscapeDataString(after);

        string raw = StreamlabsProvider.HttpGet(url, new Dictionary<string, string> { { "Authorization", "Bearer " + accessToken } });
        JToken root = JToken.Parse(raw);
        JToken items = root["content"] ?? StreamlabsProvider.Items(root);
        var result = new List<UnifiedDonationEvent>();
        if (items == null)
            return result;

        foreach (JToken item in items)
        {
            string type = S(item["type"]);
            JToken amountToken = item["amount"];
            decimal amount = ParseAmount(amountToken);
            if (amount <= 0)
                continue;

            string system = S(item["system"]);
            string id = StreamlabsProvider.First(S(item["id"]), S(item["originId"]));
            string user = StreamlabsProvider.First(S(item["nickname"]), "Anonymous");
            var donation = new UnifiedDonationEvent
            {
                Source = string.IsNullOrWhiteSpace(system) ? "ODA" : "ODA:" + system,
                ProviderName = "ODA",
                EventType = string.IsNullOrWhiteSpace(type) ? "donation" : type,
                DonationId = id,
                UserName = user,
                Amount = amount,
                Currency = Currency(amountToken),
                Message = S(item["message"]),
                Timestamp = StreamlabsProvider.Date(S(item["timestamp"])),
                IsAnonymous = string.IsNullOrWhiteSpace(S(item["nickname"])) || user == "Anonymous",
                RawJson = item.ToString(Formatting.None)
            };

            result.Add(donation);
        }

        return result;
    }

    private string HistoryEndpoint()
    {
        string endpoint = Settings.Get(ProviderKey + ".historyEndpoint", "").Trim();
        if (!string.IsNullOrWhiteSpace(endpoint))
            return endpoint;
        return Settings.Get(ProviderKey + ".apiBase", DefaultApiBase).Trim().TrimEnd('/') + "/history";
    }

    private int PollSeconds()
    {
        int seconds;
        if (!int.TryParse(Settings.Get(ProviderKey + ".pollSeconds", "10"), NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
            seconds = 10;
        if (seconds < 3)
            seconds = 3;
        if (seconds > 120)
            seconds = 120;
        return seconds;
    }

    private List<string> ReadSeenIds()
    {
        string raw = Settings.Get(ProviderKey + ".seenDonationIds", "");
        var result = new List<string>();
        foreach (string item in raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string id = item.Trim();
            if (!string.IsNullOrWhiteSpace(id) && !result.Contains(id))
                result.Add(id);
        }
        return result;
    }

    private void AddSeenId(List<string> seenIds, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || seenIds.Contains(id))
            return;
        seenIds.Add(id);
        while (seenIds.Count > 300)
            seenIds.RemoveAt(0);
    }

    private void SaveSeenIds(List<string> seenIds)
    {
        Settings.Set(ProviderKey + ".seenDonationIds", string.Join("|", seenIds.ToArray()), true);
        Settings.Set(ProviderKey + ".diagnostics.seenCount", seenIds.Count.ToString(CultureInfo.InvariantCulture), true);
    }

    private decimal ParseAmount(JToken amount)
    {
        if (amount == null)
            return 0;
        if (amount.Type != JTokenType.Object)
            return StreamlabsProvider.Dec(amount.ToString());

        decimal major = StreamlabsProvider.Dec(S(amount["major"]));
        decimal minor = StreamlabsProvider.Dec(S(amount["minor"]));
        if (minor > 0)
            return major + (minor / 100m);
        return major;
    }

    private string Currency(JToken amount)
    {
        if (amount != null && amount.Type == JTokenType.Object)
            return StreamlabsProvider.First(S(amount["currency"]), "RUB");
        return "RUB";
    }

    private static string S(JToken token)
    {
        return token == null ? "" : token.ToString();
    }
}

public class WidgetSettings
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int BorderRadius { get; set; }
    public int Padding { get; set; }
    public int FontSize { get; set; }
    public double Opacity { get; set; }
    public string BackgroundColor { get; set; }
    public string TextColor { get; set; }
    public string AccentColor { get; set; }
    public double AnimationDuration { get; set; }
    public string FontFamily { get; set; }
    public string DonorFontFamily { get; set; }
    public string AmountFontFamily { get; set; }
    public string MessageFontFamily { get; set; }
    public string PlatformFontFamily { get; set; }
    public string DonorTemplate { get; set; }
    public string AmountTemplate { get; set; }
    public string MessageTemplate { get; set; }
    public string PlatformTemplate { get; set; }
    public bool ShowBackground { get; set; }
    public bool ShowProgressBar { get; set; }
    public bool ShowPlatform { get; set; }
    public int DisplayDuration { get; set; }
    public string EntryAnimation { get; set; }
    public string ExitAnimation { get; set; }
    public string TextAnimation { get; set; }
    public string MediaFile { get; set; }
    public string SoundFile { get; set; }
    public string TextSoundFile { get; set; }
    public string MediaFit { get; set; }
    public string MediaPlacement { get; set; }
    public int MediaWidth { get; set; }
    public int MediaHeight { get; set; }
    public int MediaX { get; set; }
    public int MediaY { get; set; }
    public string TextAlign { get; set; }
    public int DonorX { get; set; }
    public int DonorY { get; set; }
    public int AmountX { get; set; }
    public int AmountY { get; set; }
    public int MessageX { get; set; }
    public int MessageY { get; set; }
    public int SoundVolume { get; set; }
    public int TextSoundVolume { get; set; }
    public bool SpeakDonation { get; set; }
    public bool SpeechReadDonor { get; set; }
    public bool SpeechReadAmount { get; set; }
    public bool SpeechReadPlatform { get; set; }
    public bool SpeechReadMessage { get; set; }
    public string SpeechVoice { get; set; }
    public double SpeechRate { get; set; }
    public double SpeechPitch { get; set; }
    public int SpeechVolume { get; set; }
    public bool VideoMuted { get; set; }
    public JArray AlertRules { get; set; }
    public string PresetName { get; set; }
    public string Language { get; set; }

    public static WidgetSettings Default()
    {
        return new WidgetSettings
        {
            Width = 680,
            Height = 360,
            BorderRadius = 18,
            Padding = 26,
            FontSize = 28,
            Opacity = 0.88,
            BackgroundColor = "#10131a",
            TextColor = "#f8fbff",
            AccentColor = "#35d07f",
            AnimationDuration = 650,
            FontFamily = "Segoe UI",
            DonorFontFamily = "",
            AmountFontFamily = "",
            MessageFontFamily = "",
            PlatformFontFamily = "",
            DonorTemplate = "{donor}",
            AmountTemplate = "{amount} {currency}",
            MessageTemplate = "{message}",
            PlatformTemplate = "{platform}",
            ShowBackground = true,
            ShowProgressBar = false,
            ShowPlatform = true,
            DisplayDuration = 9000,
            EntryAnimation = "fade",
            ExitAnimation = "fade",
            TextAnimation = "fade",
            MediaFile = "",
            SoundFile = "",
            TextSoundFile = "",
            MediaFit = "contain",
            MediaPlacement = "above",
            MediaWidth = 260,
            MediaHeight = 170,
            MediaX = 0,
            MediaY = 0,
            TextAlign = "center",
            DonorX = 0,
            DonorY = 0,
            AmountX = 0,
            AmountY = 0,
            MessageX = 0,
            MessageY = 0,
            SoundVolume = 75,
            TextSoundVolume = 45,
            SpeakDonation = false,
            SpeechReadDonor = true,
            SpeechReadAmount = true,
            SpeechReadPlatform = true,
            SpeechReadMessage = true,
            SpeechVoice = "",
            SpeechRate = 1.0,
            SpeechPitch = 1.0,
            SpeechVolume = 85,
            VideoMuted = true,
            AlertRules = new JArray(),
            PresetName = "Minimal Dark",
            Language = "en"
        };
    }

    public JObject ToJson()
    {
        var json = new JObject();
        json["Width"] = Width;
        json["Height"] = Height;
        json["BorderRadius"] = BorderRadius;
        json["Padding"] = Padding;
        json["FontSize"] = FontSize;
        json["Opacity"] = Opacity;
        json["BackgroundColor"] = BackgroundColor ?? "#10131a";
        json["TextColor"] = TextColor ?? "#f8fbff";
        json["AccentColor"] = AccentColor ?? "#35d07f";
        json["AnimationDuration"] = AnimationDuration;
        json["FontFamily"] = FontFamily ?? "Segoe UI";
        json["DonorFontFamily"] = DonorFontFamily ?? "";
        json["AmountFontFamily"] = AmountFontFamily ?? "";
        json["MessageFontFamily"] = MessageFontFamily ?? "";
        json["PlatformFontFamily"] = PlatformFontFamily ?? "";
        json["DonorTemplate"] = DonorTemplate ?? "{donor}";
        json["AmountTemplate"] = AmountTemplate ?? "{amount} {currency}";
        json["MessageTemplate"] = MessageTemplate ?? "{message}";
        json["PlatformTemplate"] = PlatformTemplate ?? "{platform}";
        json["ShowBackground"] = ShowBackground;
        json["ShowProgressBar"] = ShowProgressBar;
        json["ShowPlatform"] = ShowPlatform;
        json["DisplayDuration"] = DisplayDuration;
        json["EntryAnimation"] = EntryAnimation ?? "fade";
        json["ExitAnimation"] = ExitAnimation ?? "fade";
        json["TextAnimation"] = TextAnimation ?? "fade";
        json["MediaFile"] = MediaFile ?? "";
        json["SoundFile"] = SoundFile ?? "";
        json["TextSoundFile"] = TextSoundFile ?? "";
        json["MediaFit"] = MediaFit ?? "contain";
        json["MediaPlacement"] = MediaPlacement ?? "above";
        json["MediaWidth"] = MediaWidth;
        json["MediaHeight"] = MediaHeight;
        json["MediaX"] = MediaX;
        json["MediaY"] = MediaY;
        json["TextAlign"] = TextAlign ?? "center";
        json["DonorX"] = DonorX;
        json["DonorY"] = DonorY;
        json["AmountX"] = AmountX;
        json["AmountY"] = AmountY;
        json["MessageX"] = MessageX;
        json["MessageY"] = MessageY;
        json["SoundVolume"] = SoundVolume;
        json["TextSoundVolume"] = TextSoundVolume;
        json["SpeakDonation"] = SpeakDonation;
        json["SpeechReadDonor"] = SpeechReadDonor;
        json["SpeechReadAmount"] = SpeechReadAmount;
        json["SpeechReadPlatform"] = SpeechReadPlatform;
        json["SpeechReadMessage"] = SpeechReadMessage;
        json["SpeechVoice"] = SpeechVoice ?? "";
        json["SpeechRate"] = SpeechRate;
        json["SpeechPitch"] = SpeechPitch;
        json["SpeechVolume"] = SpeechVolume;
        json["VideoMuted"] = VideoMuted;
        json["AlertRules"] = AlertRules == null ? new JArray() : AlertRules.DeepClone();
        json["PresetName"] = PresetName ?? "Minimal Dark";
        json["Language"] = Language ?? "en";
        return json;
    }

    public static WidgetSettings FromJson(string raw)
    {
        var settings = Default();
        if (string.IsNullOrWhiteSpace(raw))
            return settings;

        JObject json = JObject.Parse(raw);
        settings.Width = ClampInt(IntValue(json, "Width", settings.Width), 240, 1920);
        settings.Height = ClampInt(IntValue(json, "Height", settings.Height), 90, 1080);
        settings.BorderRadius = ClampInt(IntValue(json, "BorderRadius", settings.BorderRadius), 0, 80);
        settings.Padding = ClampInt(IntValue(json, "Padding", settings.Padding), 0, 80);
        settings.FontSize = ClampInt(IntValue(json, "FontSize", settings.FontSize), 10, 72);
        settings.Opacity = ClampDouble(DoubleValue(json, "Opacity", settings.Opacity), 0.1, 1);
        settings.BackgroundColor = StringValue(json, "BackgroundColor", settings.BackgroundColor);
        settings.TextColor = StringValue(json, "TextColor", settings.TextColor);
        settings.AccentColor = StringValue(json, "AccentColor", settings.AccentColor);
        settings.AnimationDuration = ClampDouble(DoubleValue(json, "AnimationDuration", settings.AnimationDuration), 0, 5000);
        settings.FontFamily = StringValue(json, "FontFamily", settings.FontFamily);
        settings.DonorFontFamily = StringValue(json, "DonorFontFamily", settings.DonorFontFamily);
        settings.AmountFontFamily = StringValue(json, "AmountFontFamily", settings.AmountFontFamily);
        settings.MessageFontFamily = StringValue(json, "MessageFontFamily", settings.MessageFontFamily);
        settings.PlatformFontFamily = StringValue(json, "PlatformFontFamily", settings.PlatformFontFamily);
        settings.DonorTemplate = StringValue(json, "DonorTemplate", settings.DonorTemplate);
        settings.AmountTemplate = StringValue(json, "AmountTemplate", settings.AmountTemplate);
        settings.MessageTemplate = StringValue(json, "MessageTemplate", settings.MessageTemplate);
        settings.PlatformTemplate = StringValue(json, "PlatformTemplate", settings.PlatformTemplate);
        settings.ShowBackground = BoolValue(json, "ShowBackground", settings.ShowBackground);
        settings.ShowProgressBar = BoolValue(json, "ShowProgressBar", settings.ShowProgressBar);
        settings.ShowPlatform = BoolValue(json, "ShowPlatform", settings.ShowPlatform);
        settings.DisplayDuration = ClampInt(IntValue(json, "DisplayDuration", settings.DisplayDuration), 500, 60000);
        settings.EntryAnimation = ChoiceValue(json, "EntryAnimation", settings.EntryAnimation, "none", "fade", "slide-left", "slide-right", "slide-up", "slide-down", "zoom");
        settings.ExitAnimation = ChoiceValue(json, "ExitAnimation", settings.ExitAnimation, "none", "fade", "slide-left", "slide-right", "slide-up", "slide-down", "zoom", "scatter");
        settings.TextAnimation = ChoiceValue(json, "TextAnimation", settings.TextAnimation, "none", "fade", "typewriter", "reveal-left", "slide-up");
        settings.MediaFile = OptionalStringValue(json, "MediaFile", settings.MediaFile);
        settings.SoundFile = OptionalStringValue(json, "SoundFile", settings.SoundFile);
        settings.TextSoundFile = OptionalStringValue(json, "TextSoundFile", settings.TextSoundFile);
        settings.MediaFit = ChoiceValue(json, "MediaFit", settings.MediaFit, "contain", "cover");
        settings.MediaPlacement = ChoiceValue(json, "MediaPlacement", settings.MediaPlacement, "above", "below", "left", "right", "background");
        settings.MediaWidth = ClampInt(IntValue(json, "MediaWidth", settings.MediaWidth), 20, 1600);
        settings.MediaHeight = ClampInt(IntValue(json, "MediaHeight", settings.MediaHeight), 20, 1000);
        settings.MediaX = ClampInt(IntValue(json, "MediaX", settings.MediaX), -800, 800);
        settings.MediaY = ClampInt(IntValue(json, "MediaY", settings.MediaY), -600, 600);
        settings.TextAlign = ChoiceValue(json, "TextAlign", settings.TextAlign, "left", "center", "right");
        settings.DonorX = ClampInt(IntValue(json, "DonorX", settings.DonorX), -800, 800);
        settings.DonorY = ClampInt(IntValue(json, "DonorY", settings.DonorY), -600, 600);
        settings.AmountX = ClampInt(IntValue(json, "AmountX", settings.AmountX), -800, 800);
        settings.AmountY = ClampInt(IntValue(json, "AmountY", settings.AmountY), -600, 600);
        settings.MessageX = ClampInt(IntValue(json, "MessageX", settings.MessageX), -800, 800);
        settings.MessageY = ClampInt(IntValue(json, "MessageY", settings.MessageY), -600, 600);
        settings.SoundVolume = ClampInt(IntValue(json, "SoundVolume", settings.SoundVolume), 0, 100);
        settings.TextSoundVolume = ClampInt(IntValue(json, "TextSoundVolume", settings.TextSoundVolume), 0, 100);
        settings.SpeakDonation = BoolValue(json, "SpeakDonation", settings.SpeakDonation);
        settings.SpeechReadDonor = BoolValue(json, "SpeechReadDonor", settings.SpeechReadDonor);
        settings.SpeechReadAmount = BoolValue(json, "SpeechReadAmount", settings.SpeechReadAmount);
        settings.SpeechReadPlatform = BoolValue(json, "SpeechReadPlatform", settings.SpeechReadPlatform);
        settings.SpeechReadMessage = BoolValue(json, "SpeechReadMessage", settings.SpeechReadMessage);
        settings.SpeechVoice = OptionalStringValue(json, "SpeechVoice", settings.SpeechVoice);
        settings.SpeechRate = ClampDouble(DoubleValue(json, "SpeechRate", settings.SpeechRate), 0.5, 2.0);
        settings.SpeechPitch = ClampDouble(DoubleValue(json, "SpeechPitch", settings.SpeechPitch), 0.5, 2.0);
        settings.SpeechVolume = ClampInt(IntValue(json, "SpeechVolume", settings.SpeechVolume), 0, 100);
        settings.VideoMuted = BoolValue(json, "VideoMuted", settings.VideoMuted);
        settings.AlertRules = NormalizeAlertRules(json["AlertRules"] as JArray);
        settings.PresetName = StringValue(json, "PresetName", settings.PresetName);
        settings.Language = NormalizeLanguage(StringValue(json, "Language", settings.Language));
        return settings;
    }

    private static int IntValue(JObject json, string name, int fallback)
    {
        JToken token = json[name];
        int value;
        return token != null && int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
    }

    private static double DoubleValue(JObject json, string name, double fallback)
    {
        JToken token = json[name];
        double value;
        return token != null && double.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : fallback;
    }

    private static string StringValue(JObject json, string name, string fallback)
    {
        JToken token = json[name];
        string value = token == null ? "" : token.ToString();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string OptionalStringValue(JObject json, string name, string fallback)
    {
        JToken token = json[name];
        return token == null ? fallback : token.ToString();
    }

    private static bool BoolValue(JObject json, string name, bool fallback)
    {
        JToken token = json[name];
        bool value;
        return token == null || !bool.TryParse(token.ToString(), out value) ? fallback : value;
    }

    private static string ChoiceValue(JObject json, string name, string fallback, params string[] allowed)
    {
        string value = OptionalStringValue(json, name, fallback).Trim().ToLowerInvariant();
        foreach (string item in allowed)
            if (value == item)
                return item;
        return fallback;
    }

    private static JArray NormalizeAlertRules(JArray source)
    {
        var rules = new JArray();
        if (source == null)
            return rules;

        foreach (JToken token in source)
        {
            JObject input = token as JObject;
            if (input == null)
                continue;

            var rule = new JObject();
            rule["Id"] = OptionalStringValue(input, "Id", Guid.NewGuid().ToString("N"));
            rule["Name"] = OptionalStringValue(input, "Name", "Amount rule");
            rule["MinAmount"] = ClampDouble(DoubleValue(input, "MinAmount", 0), 0, 1000000000);
            rule["MaxAmount"] = ClampDouble(DoubleValue(input, "MaxAmount", 0), 0, 1000000000);
            rule["Randomize"] = BoolValue(input, "Randomize", true);
            rule["MediaFiles"] = StringArray(input["MediaFiles"] as JArray);
            rule["SoundFiles"] = StringArray(input["SoundFiles"] as JArray);
            rules.Add(rule);
            if (rules.Count >= 40)
                break;
        }
        return rules;
    }

    private static JArray StringArray(JArray source)
    {
        var result = new JArray();
        if (source == null)
            return result;
        foreach (JToken token in source)
        {
            string value = token == null ? "" : token.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value);
            if (result.Count >= 50)
                break;
        }
        return result;
    }

    private static int ClampInt(int value, int min, int max)
    {
        return Math.Min(max, Math.Max(min, value));
    }

    private static double ClampDouble(double value, double min, double max)
    {
        return Math.Min(max, Math.Max(min, value));
    }

    private static string NormalizeLanguage(string value)
    {
        string language = (value ?? "").Trim().ToLowerInvariant();
        return language == "ru" || language == "uk" || language == "en" ? language : "en";
    }
}

public class DonConnectWidgetServer
{
    private const string Host = "127.0.0.1";
    private const int DefaultPort = 3987;
    private const string EditorVersion = "0.12.1-beta.2.4";
    private const int MaxHttpBodyBytes = 48 * 1024 * 1024;
    private const int MaxAlertMediaBytes = 32 * 1024 * 1024;
    private readonly BridgeSettings BridgeSettings;
    private readonly BridgeLogger Logger;
    private readonly Action<UnifiedDonationEvent> DonationHandler;
    private readonly object StateLock = new object();
    private readonly object SpeechLock = new object();
    private readonly object NativeCreditsLock = new object();
    private TcpListener Listener;
    private CancellationTokenSource Cancellation;
    private WidgetSettings CurrentSettings;
    private JObject OverlaySettings;
    private JObject CreditsSettings;
    private JObject LeaderboardSettings;
    private JObject ContentFilterSettings;
    private JObject LastDonation;
    private readonly List<JObject> CreditItems = new List<JObject>();
    private readonly List<JObject> LeaderboardItems = new List<JObject>();
    private readonly List<JObject> RecentDonations = new List<JObject>();
    private int EventId;
    private int CreditsEventId;
    private int LeaderboardEventId;
    private int Port;
    private JObject NativeCreditsCache;
    private string NativeCreditsCachePath = "";
    private DateTime NativeCreditsCacheWriteUtc = DateTime.MinValue;
    private DateTime NativeCreditsLastHttpAttemptUtc = DateTime.MinValue;
    private int NativeCreditsHttpRefreshPending;

    public DonConnectWidgetServer(BridgeSettings settings, BridgeLogger logger, Action<UnifiedDonationEvent> donationHandler)
    {
        BridgeSettings = settings;
        Logger = logger;
        DonationHandler = donationHandler;
        Port = ReadPort();
        CurrentSettings = LoadSettings();
        OverlaySettings = LoadOverlaySettings();
        CreditsSettings = LoadCreditsSettings();
        LeaderboardSettings = LoadLeaderboardSettings();
        ContentFilterSettings = LoadContentFilterSettings();
        LoadLeaderboardData();
        EnsureAlertMediaLibrary();
        LastDonation = DonationToJson(DefaultDonation());
    }

    public bool IsRunning { get { return Listener != null; } }
    public string BaseUrl { get { return "http://" + Host + ":" + Port.ToString(CultureInfo.InvariantCulture) + "/donconnect"; } }
    public string EditorUrl { get { return BaseUrl + "/editor?v=" + EditorVersion; } }
    public string WidgetUrl { get { return BaseUrl + "/widget"; } }
    public string GoalUrl { get { return BaseUrl + "/goal"; } }
    public string TimerUrl { get { return BaseUrl + "/timer"; } }
    public string CreditsUrl { get { return BaseUrl + "/credits"; } }
    public string LeaderboardUrl { get { return BaseUrl + "/leaderboard"; } }
    public string DockUrl { get { return BaseUrl + "/dock"; } }

    public void Start()
    {
        if (IsRunning)
            return;

        int preferredPort = ReadPort();
        CurrentSettings = LoadSettings();
        OverlaySettings = LoadOverlaySettings();
        CreditsSettings = LoadCreditsSettings();
        LeaderboardSettings = LoadLeaderboardSettings();
        ContentFilterSettings = LoadContentFilterSettings();
        LoadLeaderboardData();
        EnsureAlertMediaLibrary();
        Cancellation = new CancellationTokenSource();
        StartListener(preferredPort);
        BridgeSettings.Set("widget.enabled", "true", true);
        BridgeSettings.Set("widget.lastPort", Port.ToString(CultureInfo.InvariantCulture), true);
        Task.Run(() => AcceptLoop(Cancellation.Token));
    }

    private void StartListener(int preferredPort)
    {
        Exception lastError = null;
        for (int i = 0; i < 12; i++)
        {
            int candidate = preferredPort + i;
            if (candidate > 65535)
                break;

            try
            {
                Listener = new TcpListener(IPAddress.Loopback, candidate);
                Listener.Start();
                Port = candidate;
                if (candidate != preferredPort)
                    Logger.Warn("Widget port " + preferredPort.ToString(CultureInfo.InvariantCulture) + " is busy. Started on " + candidate.ToString(CultureInfo.InvariantCulture) + ".");
                return;
            }
            catch (SocketException ex)
            {
                lastError = ex;
                try { if (Listener != null) Listener.Stop(); } catch { }
                Listener = null;
            }
        }

        throw new InvalidOperationException("Widget server was not started. Port is busy. " + (lastError == null ? "" : lastError.Message));
    }

    public void Stop()
    {
        try
        {
            if (Cancellation != null)
                Cancellation.Cancel();
        }
        catch { }

        try
        {
            if (Listener != null)
                Listener.Stop();
        }
        catch { }

        Listener = null;
        BridgeSettings.Set("widget.enabled", "false", true);
    }

    public void PushDonation(UnifiedDonationEvent donation)
    {
        JObject speechDonation;
        WidgetSettings speechSettings;
        lock (StateLock)
        {
            LastDonation = DonationToJson(donation ?? DefaultDonation());
            AddRecentDonation(LastDonation);
            AddCreditDonation(LastDonation);
            AddLeaderboardDonation(LastDonation);
            EventId++;
            speechDonation = (JObject)LastDonation.DeepClone();
            speechSettings = CurrentSettings;
        }

        SpeakDonationIfEnabled(speechDonation, speechSettings);
    }

    private void SpeakDonationIfEnabled(JObject donation, WidgetSettings settings)
    {
        if (settings == null || !settings.SpeakDonation)
            return;

        Thread thread = new Thread(new ThreadStart(delegate
        {
            JObject result = SpeakDonationResult(donation, settings, false);
            if (!JsonBool(result, "ok", false) && !JsonBool(result, "skipped", false))
            {
                Logger.Warn("Donation speech failed: " + JsonText(result, "error", "unknown error"));
            }
        }));
        thread.IsBackground = true;
        try { thread.SetApartmentState(ApartmentState.STA); } catch { }
        thread.Start();
    }

    private JObject SpeakDonationResult(JObject donation, WidgetSettings settings, bool force)
    {
        var result = new JObject();
        result["ok"] = false;
        result["engine"] = "";
        result["error"] = "";
        result["skipped"] = false;

        if (settings == null)
        {
            result["error"] = "Speech settings are empty.";
            return result;
        }

        if (!force && !settings.SpeakDonation)
        {
            result["ok"] = true;
            result["skipped"] = true;
            return result;
        }

        string text = BuildSpeechText(donation, settings);
        result["text"] = text;
        if (string.IsNullOrWhiteSpace(text))
        {
            result["error"] = "Speech text is empty.";
            return result;
        }

        try
        {
            lock (SpeechLock)
            {
                result["engine"] = SpeakWithWindowsVoice(text, settings);
            }
            result["ok"] = true;
        }
        catch (Exception ex)
        {
            result["error"] = UnwrapException(ex).Message;
        }

        return result;
    }

    private string BuildSpeechText(JObject donation, WidgetSettings settings)
    {
        if (donation == null)
            return "";

        var parts = new List<string>();
        if (settings == null || settings.SpeechReadDonor)
            AddSpeechPart(parts, JsonText(donation, "donor"));
        if (settings == null || settings.SpeechReadAmount)
        {
            string amount = string.Join(" ", new[] { JsonText(donation, "amount"), JsonText(donation, "currency") }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray());
            AddSpeechPart(parts, amount);
        }
        if (settings == null || settings.SpeechReadPlatform)
            AddSpeechPart(parts, JsonText(donation, "provider"));
        if (settings == null || settings.SpeechReadMessage)
            AddSpeechPart(parts, JsonText(donation, "message"));
        return string.Join(". ", parts.ToArray());
    }

    private static void AddSpeechPart(List<string> parts, string value)
    {
        string text = (value ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(text))
            parts.Add(text);
    }

    private string SpeakWithWindowsVoice(string text, WidgetSettings settings)
    {
        string systemError;
        if (TrySpeakWithSystemSpeech(text, settings, out systemError))
            return "System.Speech";

        string sapiError;
        if (TrySpeakWithSapiVoice(text, settings, out sapiError))
            return "SAPI.SpVoice";

        if (!string.IsNullOrWhiteSpace(settings.SpeechVoice))
        {
            WidgetSettings fallbackSettings = SpeechSettingsWithoutVoice(settings);
            string fallbackSystemError;
            if (TrySpeakWithSystemSpeech(text, fallbackSettings, out fallbackSystemError))
                return "System.Speech default voice";

            string fallbackSapiError;
            if (TrySpeakWithSapiVoice(text, fallbackSettings, out fallbackSapiError))
                return "SAPI.SpVoice default voice";

            systemError = systemError + " | default System.Speech: " + fallbackSystemError;
            sapiError = sapiError + " | default SAPI.SpVoice: " + fallbackSapiError;
        }

        throw new InvalidOperationException("System.Speech: " + systemError + " | SAPI.SpVoice: " + sapiError);
    }

    private static WidgetSettings SpeechSettingsWithoutVoice(WidgetSettings settings)
    {
        var fallback = WidgetSettings.Default();
        fallback.SpeakDonation = true;
        fallback.SpeechVoice = "";
        fallback.SpeechRate = settings.SpeechRate;
        fallback.SpeechPitch = settings.SpeechPitch;
        fallback.SpeechVolume = settings.SpeechVolume;
        return fallback;
    }

    private bool TrySpeakWithSystemSpeech(string text, WidgetSettings settings, out string error)
    {
        error = "";
        Type synthType = SpeechSynthesizerType();
        if (synthType == null)
        {
            error = "System.Speech is not available.";
            return false;
        }

        object synthesizer = Activator.CreateInstance(synthType);
        try
        {
            string voice = (settings.SpeechVoice ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(voice))
            {
                try
                {
                    synthType.GetMethod("SelectVoice", new[] { typeof(string) }).Invoke(synthesizer, new object[] { voice });
                }
                catch (Exception ex)
                {
                    error = "Voice not found in System.Speech: " + voice + ". " + UnwrapException(ex).Message;
                    return false;
                }
            }

            var rateProperty = synthType.GetProperty("Rate");
            if (rateProperty != null)
                rateProperty.SetValue(synthesizer, SpeechRateToWindows(settings.SpeechRate), null);

            var volumeProperty = synthType.GetProperty("Volume");
            if (volumeProperty != null)
                volumeProperty.SetValue(synthesizer, Math.Max(0, Math.Min(100, settings.SpeechVolume)), null);

            synthType.GetMethod("Speak", new[] { typeof(string) }).Invoke(synthesizer, new object[] { text });
            return true;
        }
        catch (Exception ex)
        {
            error = UnwrapException(ex).Message;
            return false;
        }
        finally
        {
            IDisposable disposable = synthesizer as IDisposable;
            if (disposable != null)
                disposable.Dispose();
        }
    }

    private Type SpeechSynthesizerType()
    {
        Type synthType = Type.GetType("System.Speech.Synthesis.SpeechSynthesizer, System.Speech");
        if (synthType != null)
            return synthType;

        try
        {
            var assembly = System.Reflection.Assembly.LoadWithPartialName("System.Speech");
            return assembly == null ? null : assembly.GetType("System.Speech.Synthesis.SpeechSynthesizer");
        }
        catch
        {
            return null;
        }
    }

    private bool TrySpeakWithSapiVoice(string text, WidgetSettings settings, out string error)
    {
        error = "";
        Type sapiType = Type.GetTypeFromProgID("SAPI.SpVoice");
        if (sapiType == null)
        {
            error = "SAPI.SpVoice is not available.";
            return false;
        }

        object speaker = null;
        object selectedVoice = null;
        try
        {
            speaker = Activator.CreateInstance(sapiType);
            string voice = (settings.SpeechVoice ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(voice))
            {
                selectedVoice = FindSapiVoice(speaker, voice);
                if (selectedVoice == null)
                {
                    error = "Voice not found in SAPI: " + voice;
                    return false;
                }

                sapiType.InvokeMember("Voice", System.Reflection.BindingFlags.SetProperty, null, speaker, new object[] { selectedVoice });
            }

            sapiType.InvokeMember("Rate", System.Reflection.BindingFlags.SetProperty, null, speaker, new object[] { SpeechRateToWindows(settings.SpeechRate) });
            sapiType.InvokeMember("Volume", System.Reflection.BindingFlags.SetProperty, null, speaker, new object[] { Math.Max(0, Math.Min(100, settings.SpeechVolume)) });
            sapiType.InvokeMember("Speak", System.Reflection.BindingFlags.InvokeMethod, null, speaker, new object[] { text, 0 });
            return true;
        }
        catch (Exception ex)
        {
            error = UnwrapException(ex).Message;
            return false;
        }
        finally
        {
            ReleaseComObject(selectedVoice);
            ReleaseComObject(speaker);
        }
    }

    private object FindSapiVoice(object speaker, string requestedVoice)
    {
        object voices = null;
        try
        {
            voices = speaker.GetType().InvokeMember("GetVoices", System.Reflection.BindingFlags.InvokeMethod, null, speaker, null);
            int count = Convert.ToInt32(voices.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, voices, null), CultureInfo.InvariantCulture);
            string requested = requestedVoice.Trim().ToLowerInvariant();
            for (int index = 0; index < count; index++)
            {
                object voice = GetComCollectionItem(voices, index);
                string description = SapiVoiceDescription(voice);
                string normalized = description.Trim().ToLowerInvariant();
                if (normalized == requested || normalized.StartsWith(requested + " -", StringComparison.OrdinalIgnoreCase) || requested.StartsWith(normalized + " -", StringComparison.OrdinalIgnoreCase))
                    return voice;

                ReleaseComObject(voice);
            }
        }
        catch
        {
        }
        finally
        {
            ReleaseComObject(voices);
        }

        return null;
    }

    private static object GetComCollectionItem(object collection, int index)
    {
        try
        {
            return collection.GetType().InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod, null, collection, new object[] { index });
        }
        catch
        {
            return collection.GetType().InvokeMember("Item", System.Reflection.BindingFlags.GetProperty, null, collection, new object[] { index });
        }
    }

    private static string SapiVoiceDescription(object voice)
    {
        if (voice == null)
            return "";

        try
        {
            object value = voice.GetType().InvokeMember("GetDescription", System.Reflection.BindingFlags.InvokeMethod, null, voice, null);
            return value == null ? "" : value.ToString();
        }
        catch
        {
            return "";
        }
    }

    private static void ReleaseComObject(object value)
    {
        try
        {
            if (value != null && System.Runtime.InteropServices.Marshal.IsComObject(value))
                System.Runtime.InteropServices.Marshal.ReleaseComObject(value);
        }
        catch
        {
        }
    }

    private static Exception UnwrapException(Exception ex)
    {
        var invocation = ex as System.Reflection.TargetInvocationException;
        return invocation != null && invocation.InnerException != null ? invocation.InnerException : ex;
    }

    private static int SpeechRateToWindows(double rate)
    {
        double normalized = Math.Max(0.5, Math.Min(2.0, rate));
        return Math.Max(-10, Math.Min(10, (int)Math.Round((normalized - 1.0) * 10.0)));
    }

    private JArray WindowsSpeechVoices()
    {
        var voices = new JArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddSystemSpeechVoices(voices, seen);
        AddSapiVoices(voices, seen);
        return voices;
    }

    private void AddSystemSpeechVoices(JArray voices, HashSet<string> seen)
    {
        Type synthType = SpeechSynthesizerType();
        if (synthType == null)
            return;

        object synthesizer = Activator.CreateInstance(synthType);
        try
        {
            object installedVoices = synthType.GetMethod("GetInstalledVoices", Type.EmptyTypes).Invoke(synthesizer, null);
            var enumerable = installedVoices as System.Collections.IEnumerable;
            if (enumerable == null)
                return;

            foreach (object voice in enumerable)
            {
                object info = voice.GetType().GetProperty("VoiceInfo").GetValue(voice, null);
                if (info == null)
                    continue;

                string name = Convert.ToString(info.GetType().GetProperty("Name").GetValue(info, null), CultureInfo.InvariantCulture);
                object cultureValue = info.GetType().GetProperty("Culture").GetValue(info, null);
                string culture = cultureValue == null ? "" : cultureValue.ToString();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                AddVoiceItem(voices, seen, name, culture, "System.Speech");
            }
        }
        catch (Exception ex)
        {
            Logger.Debug("Windows speech voice list is unavailable. " + ex.Message);
        }
        finally
        {
            IDisposable disposable = synthesizer as IDisposable;
            if (disposable != null)
                disposable.Dispose();
        }
    }

    private void AddSapiVoices(JArray voices, HashSet<string> seen)
    {
        Type sapiType = Type.GetTypeFromProgID("SAPI.SpVoice");
        if (sapiType == null)
            return;

        object speaker = null;
        object collection = null;
        try
        {
            speaker = Activator.CreateInstance(sapiType);
            collection = sapiType.InvokeMember("GetVoices", System.Reflection.BindingFlags.InvokeMethod, null, speaker, null);
            int count = Convert.ToInt32(collection.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, collection, null), CultureInfo.InvariantCulture);
            for (int index = 0; index < count; index++)
            {
                object voice = null;
                try
                {
                    voice = GetComCollectionItem(collection, index);
                    string description = SapiVoiceDescription(voice);
                    if (!string.IsNullOrWhiteSpace(description))
                        AddVoiceItem(voices, seen, description, "", "SAPI.SpVoice");
                }
                finally
                {
                    ReleaseComObject(voice);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug("SAPI speech voice list is unavailable. " + ex.Message);
        }
        finally
        {
            ReleaseComObject(collection);
            ReleaseComObject(speaker);
        }
    }

    private static void AddVoiceItem(JArray voices, HashSet<string> seen, string name, string language, string source)
    {
        string text = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text) || !seen.Add(text))
            return;

        var item = new JObject();
        item["name"] = text;
        item["lang"] = language ?? "";
        item["source"] = source ?? "";
        voices.Add(item);
    }

    private async Task AcceptLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client = null;
            try
            {
                client = await Listener.AcceptTcpClientAsync();
                TcpClient acceptedClient = client;
                client = null;
                ThreadPool.QueueUserWorkItem(delegate { HandleClient(acceptedClient); });
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    Logger.Warn("Widget server accept error: " + ex.Message);
                if (client != null)
                    try { client.Close(); } catch { }
            }
        }
    }

    private void HandleClient(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                using (NetworkStream stream = client.GetStream())
                {
                    HttpRequest request = ReadRequest(stream);
                    if (request == null)
                    {
                        WriteResponse(stream, 400, "text/plain; charset=utf-8", "Bad Request");
                        return;
                    }

                    Route(stream, request);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("Widget server request error: " + ex.Message);
            }
        }
    }

    private void Route(NetworkStream stream, HttpRequest request)
    {
        string method = NormalizeHttpMethod(request.Method);
        string path = NormalizeRoutePath(request.Path);

        if (method == "GET" && TryHandleGetRequest(stream, path))
            return;

        if (method == "POST" && TryHandlePostRequest(stream, path, request.Body))
            return;

        WriteResponse(stream, 404, "text/plain; charset=utf-8", "Not Found");
    }

    private bool TryHandleGetRequest(NetworkStream stream, string path)
    {
        switch (path)
        {
            case "/donconnect/editor":
                WriteResponse(stream, 200, "text/html; charset=utf-8", EditorHtml());
                return true;
            case "/donconnect/widget":
                WriteResponse(stream, 200, "text/html; charset=utf-8", WidgetHtml());
                return true;
            case "/donconnect/goal":
                WriteResponse(stream, 200, "text/html; charset=utf-8", GoalTimerHtml("goal"));
                return true;
            case "/donconnect/timer":
                WriteResponse(stream, 200, "text/html; charset=utf-8", GoalTimerHtml("timer"));
                return true;
            case "/donconnect/goal-timer":
                WriteResponse(stream, 200, "text/html; charset=utf-8", GoalTimerHtml("both"));
                return true;
            case "/donconnect/credits":
                WriteResponse(stream, 200, "text/html; charset=utf-8", CreditsHtml());
                return true;
            case "/donconnect/leaderboard":
                WriteResponse(stream, 200, "text/html; charset=utf-8", LeaderboardHtml());
                return true;
            case "/donconnect/dock":
                WriteResponse(stream, 200, "text/html; charset=utf-8", DockHtml());
                return true;
            case "/donconnect/api/settings":
                WriteJson(stream, CurrentSettings.ToJson());
                return true;
            case "/donconnect/api/overlay-settings":
                WriteJson(stream, OverlaySettingsForClient());
                return true;
            case "/donconnect/api/goal-state":
                GoalStateEndpoint(stream);
                return true;
            case "/donconnect/api/credits-settings":
                WriteJson(stream, CreditsSettingsForClient());
                return true;
            case "/donconnect/api/credits-state":
                CreditsStateEndpoint(stream);
                return true;
            case "/donconnect/api/leaderboard-settings":
                WriteJson(stream, LeaderboardSettingsForClient());
                return true;
            case "/donconnect/api/leaderboard-state":
                LeaderboardStateEndpoint(stream);
                return true;
            case "/donconnect/api/content-filter-settings":
                WriteJson(stream, ContentFilterSettingsForClient());
                return true;
            case "/donconnect/api/recent-donations":
                RecentDonationsEndpoint(stream);
                return true;
            case "/donconnect/api/latest":
                LatestEndpoint(stream);
                return true;
            case "/donconnect/api/status":
                StatusEndpoint(stream);
                return true;
            case "/donconnect/api/obs-url":
                ObsUrlEndpoint(stream);
                return true;
            case "/donconnect/api/fonts":
                WriteJson(stream, FontCatalogJson());
                return true;
            case "/donconnect/api/speech-voices":
                WriteJson(stream, SpeechVoicesEndpointJson());
                return true;
            case "/donconnect/api/alert-media":
                WriteJson(stream, AlertMediaLibraryJson());
                return true;
        }

        if (path.StartsWith("/donconnect/media/", StringComparison.OrdinalIgnoreCase))
        {
            ServeAlertMedia(stream, path.Substring("/donconnect/media/".Length));
            return true;
        }

        return false;
    }

    private bool TryHandlePostRequest(NetworkStream stream, string path, string body)
    {
        switch (path)
        {
            case "/donconnect/api/settings":
                SaveSettingsEndpoint(stream, body);
                return true;
            case "/donconnect/api/overlay-settings":
                SaveOverlaySettingsEndpoint(stream, body);
                return true;
            case "/donconnect/api/credits-settings":
                SaveCreditsSettingsEndpoint(stream, body);
                return true;
            case "/donconnect/api/leaderboard-settings":
                SaveLeaderboardSettingsEndpoint(stream, body);
                return true;
            case "/donconnect/api/leaderboard-reset":
                ResetLeaderboardEndpoint(stream);
                return true;
            case "/donconnect/api/leaderboard-entry":
                LeaderboardEntryEndpoint(stream, body);
                return true;
            case "/donconnect/api/content-filter-settings":
                SaveContentFilterSettingsEndpoint(stream, body);
                return true;
            case "/donconnect/api/test-donation":
                TestDonationEndpoint(stream, body);
                return true;
            case "/donconnect/api/timer-test":
                TimerTestEndpoint(stream, body);
                return true;
            case "/donconnect/api/speech-test":
                SpeechTestEndpoint(stream, body);
                return true;
            case "/donconnect/api/goal-reset":
                ResetGoalEndpoint(stream);
                return true;
            case "/donconnect/api/replay-donation":
                ReplayDonationEndpoint(stream, body);
                return true;
            case "/donconnect/api/delete-recent-donation":
                DeleteRecentDonationEndpoint(stream, body);
                return true;
            case "/donconnect/api/credits-test":
                CreditsTestEndpoint(stream);
                return true;
            case "/donconnect/api/alert-media-upload":
                UploadAlertMediaEndpoint(stream, body);
                return true;
            case "/donconnect/api/alert-media-delete":
                DeleteAlertMediaEndpoint(stream, body);
                return true;
            case "/donconnect/api/alert-media-open":
                OpenAlertMediaEndpoint(stream);
                return true;
            default:
                return false;
        }
    }

    private static string NormalizeHttpMethod(string method)
    {
        return string.IsNullOrWhiteSpace(method) ? "" : method.Trim().ToUpperInvariant();
    }

    private static string NormalizeRoutePath(string path)
    {
        path = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        while (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal))
            path = path.Substring(0, path.Length - 1);
        return path;
    }

    private void SaveSettingsEndpoint(NetworkStream stream, string body)
    {
        try
        {
            CurrentSettings = WidgetSettings.FromJson(body);
            SaveSettings(CurrentSettings);
            WriteJson(stream, CurrentSettings.ToJson());
        }
        catch (Exception ex)
        {
            WriteError(stream, 400, "Settings were not saved: " + ex.Message);
        }
    }

    private void SaveOverlaySettingsEndpoint(NetworkStream stream, string body)
    {
        try
        {
            OverlaySettings = NormalizeOverlaySettings(body);
            SaveOverlaySettings(OverlaySettings);
            ApplyOverlaySettings(OverlaySettings);
            WriteJson(stream, OverlaySettingsForClient());
        }
        catch (Exception ex)
        {
            WriteError(stream, 400, "Overlay settings were not saved: " + ex.Message);
        }
    }

    private void SaveCreditsSettingsEndpoint(NetworkStream stream, string body)
    {
        try
        {
            CreditsSettings = NormalizeCreditsSettings(body);
            SaveCreditsSettings(CreditsSettings);
            ApplyCreditsSettings(CreditsSettings);
            WriteJson(stream, CreditsSettingsForClient());
        }
        catch (Exception ex)
        {
            WriteError(stream, 400, "Credits settings were not saved: " + ex.Message);
        }
    }

    private void SaveLeaderboardSettingsEndpoint(NetworkStream stream, string body)
    {
        try
        {
            LeaderboardSettings = NormalizeLeaderboardSettings(body);
            SaveLeaderboardSettings(LeaderboardSettings);
            WriteJson(stream, LeaderboardSettingsForClient());
        }
        catch (Exception ex)
        {
            WriteError(stream, 400, "Leaderboard settings were not saved: " + ex.Message);
        }
    }

    private void SaveContentFilterSettingsEndpoint(NetworkStream stream, string body)
    {
        try
        {
            ContentFilterSettings = NormalizeContentFilterSettings(body);
            SaveContentFilterSettings(ContentFilterSettings);
            WriteJson(stream, ContentFilterSettingsForClient());
        }
        catch (Exception ex)
        {
            WriteError(stream, 400, "Content filter settings were not saved: " + ex.Message);
        }
    }

    private void TestDonationEndpoint(NetworkStream stream, string body)
    {
        string kind = "";
        JObject custom = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                JObject json = JObject.Parse(body);
                kind = json["kind"] == null ? "" : json["kind"].ToString();
                custom = json["custom"] as JObject;
            }
        }
        catch { }

        UnifiedDonationEvent test = CreateTestDonation(kind);
        ApplyCustomTestDonation(test, custom);
        if (DonationHandler != null)
            DonationHandler(test);
        else
        {
            ApplyTestDonationToGoalTimer(test);
            PushDonation(test);
        }
        LatestEndpoint(stream);
    }

    private void TimerTestEndpoint(NetworkStream stream, string body)
    {
        JObject custom = null;
        try
        {
            JObject json = string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body);
            JObject settings = json["settings"] as JObject;
            if (settings != null)
            {
                OverlaySettings = NormalizeOverlaySettings(settings.ToString(Formatting.None));
                SaveOverlaySettings(OverlaySettings);
                ApplyTimerSettingsWithoutReset(OverlaySettings);
            }
            custom = json["custom"] as JObject;
        }
        catch { }

        UnifiedDonationEvent test = CreateTestDonation("custom");
        ApplyCustomTestDonation(test, custom);
        ApplyTestDonationToTimerOnly(test);
        GoalStateEndpoint(stream);
    }

    private void RecentDonationsEndpoint(NetworkStream stream)
    {
        var result = new JObject();
        var items = new JArray();
        lock (StateLock)
        {
            foreach (JObject item in RecentDonations)
                items.Add(item.DeepClone());
        }
        result["items"] = items;
        WriteJson(stream, result);
    }

    private JObject SpeechVoicesEndpointJson()
    {
        var result = new JObject();
        result["items"] = WindowsSpeechVoices();
        result["engine"] = "windows";
        return result;
    }

    private void SpeechTestEndpoint(NetworkStream stream, string body)
    {
        WidgetSettings settings = CurrentSettings;
        try
        {
            if (!string.IsNullOrWhiteSpace(body))
                settings = WidgetSettings.FromJson(body);
        }
        catch { }

        if (settings != null)
            settings.SpeakDonation = true;

        var donation = new JObject();
        donation["donor"] = "DonConnect";
        donation["amount"] = "100";
        donation["currency"] = "RUB";
        donation["provider"] = "Voice test";
        donation["message"] = "This is a donation voice test.";
        WriteJson(stream, SpeakDonationResult(donation, settings, true));
    }

    private void ResetGoalEndpoint(NetworkStream stream)
    {
        JObject settings = OverlaySettings == null ? DefaultOverlaySettings() : (JObject)OverlaySettings.DeepClone();
        settings["GoalCurrent"] = "0";
        OverlaySettings = NormalizeOverlaySettings(settings.ToString(Formatting.None));
        SaveOverlaySettings(OverlaySettings);
        BridgeSettings.Set("goal.current", "0", true);
        WriteJson(stream, GoalTimerState());
    }

    private void ReplayDonationEndpoint(NetworkStream stream, string body)
    {
        string id = "";
        try
        {
            JObject json = string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body);
            id = JsonText(json, "id");
        }
        catch { }

        lock (StateLock)
        {
            JObject selected = null;
            foreach (JObject item in RecentDonations)
            {
                if (string.IsNullOrWhiteSpace(id) || JsonText(item, "id").Equals(id, StringComparison.OrdinalIgnoreCase))
                {
                    selected = (JObject)item.DeepClone();
                    break;
                }
            }
            if (selected == null)
                selected = LastDonation == null ? DonationToJson(DefaultDonation()) : (JObject)LastDonation.DeepClone();
            LastDonation = selected;
            EventId++;
        }

        LatestEndpoint(stream);
    }

    private void DeleteRecentDonationEndpoint(NetworkStream stream, string body)
    {
        string id = "";
        try
        {
            JObject json = string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body);
            id = JsonText(json, "id");
        }
        catch { }

        var result = new JObject();
        bool removed = false;
        lock (StateLock)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                for (int index = RecentDonations.Count - 1; index >= 0; index--)
                {
                    if (JsonText(RecentDonations[index], "id").Equals(id, StringComparison.OrdinalIgnoreCase))
                    {
                        RecentDonations.RemoveAt(index);
                        removed = true;
                    }
                }

                if (removed && LastDonation != null && JsonText(LastDonation, "id").Equals(id, StringComparison.OrdinalIgnoreCase))
                {
                    LastDonation = RecentDonations.Count > 0 ? (JObject)RecentDonations[0].DeepClone() : DonationToJson(DefaultDonation());
                    EventId++;
                }
            }

            result["itemsLeft"] = RecentDonations.Count;
        }

        result["ok"] = removed;
        WriteJson(stream, result);
    }

    private void LatestEndpoint(NetworkStream stream)
    {
        JObject result = new JObject();
        lock (StateLock)
        {
            result["eventId"] = EventId;
            result["donation"] = LastDonation == null ? DonationToJson(DefaultDonation()) : LastDonation.DeepClone();
        }

        WriteJson(stream, result);
    }

    private void GoalStateEndpoint(NetworkStream stream)
    {
        WriteJson(stream, GoalTimerState());
    }

    private void CreditsStateEndpoint(NetworkStream stream)
    {
        JObject nativeCredits = TryLoadNativeCredits();
        if (JsonBool(CreditsSettings, "UseNativeCredits", true) && nativeCredits != null)
        {
            var nativeResult = new JObject();
            nativeResult["eventId"] = CreditsEventId;
            nativeResult["source"] = "streamerbot";
            nativeResult["native"] = nativeCredits;
            nativeResult["sections"] = CreditsSectionCatalog(nativeCredits);
            WriteJson(stream, nativeResult);
            return;
        }

        JObject result = new JObject();
        JArray items = new JArray();
        lock (StateLock)
        {
            foreach (JObject item in CreditItems)
                items.Add(item.DeepClone());

            result["eventId"] = CreditsEventId;
        }

        if (items.Count == 0)
            items = SampleCredits();

        result["items"] = items;
        result["source"] = "donconnect";
        result["sections"] = CreditsSectionCatalog(null);
        WriteJson(stream, result);
    }

    private void CreditsTestEndpoint(NetworkStream stream)
    {
        JObject result = new JObject();
        bool requested = RequestNativeCreditsFromHttp(true, 1500) != null;
        JObject nativeCredits = TryLoadNativeCredits();
        result["ok"] = requested || nativeCredits != null;
        result["message"] = requested
            ? "Streamer.bot test credits loaded."
            : nativeCredits != null
                ? "Streamer.bot HTTP test did not answer, current Credits cache is used."
                : "Streamer.bot Credits are unavailable. Local preview samples are still enabled.";
        WriteJson(stream, result);
    }

    private JObject TryLoadNativeCredits()
    {
        JObject local = TryLoadNativeCreditsCacheFile();
        if (local != null)
            return local;

        JObject cached = CloneNativeCreditsCache();
        if (cached != null)
        {
            QueueNativeCreditsHttpRefresh();
            return cached;
        }

        QueueNativeCreditsHttpRefresh();
        return null;
    }

    private JObject TryLoadNativeCreditsCacheFile()
    {
        foreach (string path in NativeCreditsCachePaths())
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                DateTime writeUtc = File.GetLastWriteTimeUtc(path);
                lock (NativeCreditsLock)
                {
                    if (NativeCreditsCache != null
                        && path.Equals(NativeCreditsCachePath, StringComparison.OrdinalIgnoreCase)
                        && writeUtc == NativeCreditsCacheWriteUtc)
                        return (JObject)NativeCreditsCache.DeepClone();
                }

                string raw = File.ReadAllText(path, Encoding.UTF8);
                JObject parsed = string.IsNullOrWhiteSpace(raw) ? null : JObject.Parse(raw);
                if (parsed == null)
                    continue;

                lock (NativeCreditsLock)
                {
                    NativeCreditsCache = parsed;
                    NativeCreditsCachePath = path;
                    NativeCreditsCacheWriteUtc = writeUtc;
                    CreditsEventId++;
                    return (JObject)NativeCreditsCache.DeepClone();
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("Streamer.bot Credits cache was not read: " + ex.Message);
            }
        }

        return null;
    }

    private IEnumerable<string> NativeCreditsCachePaths()
    {
        var paths = new List<string>();
        AddUniquePath(paths, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "credits.cache"));
        AddUniquePath(paths, Path.Combine(Environment.CurrentDirectory, "data", "credits.cache"));

        string dataDirectory = SettingsDirectory();
        DirectoryInfo parent = string.IsNullOrWhiteSpace(dataDirectory) ? null : Directory.GetParent(dataDirectory);
        if (parent != null)
            AddUniquePath(paths, Path.Combine(parent.FullName, "data", "credits.cache"));

        return paths;
    }

    private static void AddUniquePath(List<string> paths, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        foreach (string current in paths)
            if (current.Equals(path, StringComparison.OrdinalIgnoreCase))
                return;
        paths.Add(path);
    }

    private JObject CloneNativeCreditsCache()
    {
        lock (NativeCreditsLock)
            return NativeCreditsCache == null ? null : (JObject)NativeCreditsCache.DeepClone();
    }

    private void QueueNativeCreditsHttpRefresh()
    {
        lock (NativeCreditsLock)
        {
            if (NativeCreditsHttpRefreshPending != 0)
                return;
            if ((DateTime.UtcNow - NativeCreditsLastHttpAttemptUtc).TotalSeconds < 3)
                return;
            NativeCreditsLastHttpAttemptUtc = DateTime.UtcNow;
            NativeCreditsHttpRefreshPending = 1;
        }

        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                RequestNativeCreditsFromHttp(false, 700);
            }
            finally
            {
                lock (NativeCreditsLock)
                    NativeCreditsHttpRefreshPending = 0;
            }
        });
    }

    private JObject RequestNativeCreditsFromHttp(bool test, int timeoutMs)
    {
        try
        {
            string baseUrl = BridgeSettings.Get("STREAMERBOT_HTTP_URL", "http://127.0.0.1:7474").Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
                return null;

            var request = (HttpWebRequest)WebRequest.Create(baseUrl + (test ? "/TestCredits" : "/GetCredits"));
            request.Method = "GET";
            request.Timeout = Math.Max(250, timeoutMs);
            request.ReadWriteTimeout = Math.Max(250, timeoutMs);
            request.KeepAlive = false;
            request.Proxy = null;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            using (var reader = new StreamReader(responseStream, Encoding.UTF8))
            {
                string raw = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(raw))
                    return test ? new JObject() : null;
                JObject parsed = JObject.Parse(raw);
                if (parsed == null)
                    return null;

                lock (NativeCreditsLock)
                {
                    NativeCreditsCache = parsed;
                    NativeCreditsCachePath = "";
                    NativeCreditsCacheWriteUtc = DateTime.MinValue;
                    CreditsEventId++;
                    return (JObject)NativeCreditsCache.DeepClone();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug("Streamer.bot Credits HTTP request did not answer: " + ex.Message);
            return null;
        }
    }

    private JArray CreditsSectionCatalog(JObject nativeCredits)
    {
        var result = new JArray();
        AddCreditsSection(result, "Follows");
        AddCreditsSection(result, "Cheers");
        AddCreditsSection(result, "Subs");
        AddCreditsSection(result, "ReSubs");
        AddCreditsSection(result, "Gift Subs");
        AddCreditsSection(result, "Gift Bombs");
        AddCreditsSection(result, "Raids");
        AddCreditsSection(result, "Reward Redemptions");
        AddCreditsSection(result, "Goal Contributions");
        AddCreditsSection(result, "Game Updates");
        AddCreditsSection(result, "Pyramids");
        AddCreditsSection(result, "Hype Trains");
        AddCreditsSection(result, "Hype Train Conductors");
        AddCreditsSection(result, "Hype Train Contributors");
        AddCreditsSection(result, "Editors");
        AddCreditsSection(result, "Moderators");
        AddCreditsSection(result, "Subscribers");
        AddCreditsSection(result, "VIPs");
        AddCreditsSection(result, "Users");
        AddCreditsSection(result, "Groups");
        AddCreditsSection(result, "All Bits");
        AddCreditsSection(result, "Month Bits");
        AddCreditsSection(result, "Week Bits");
        AddCreditsSection(result, "Channel Rewards");
        AddCreditsSection(result, JsonText(CreditsSettings, "SectionTitle", "Donations"));
        AddDynamicCreditsSections(result, nativeCredits == null ? null : nativeCredits["custom"] ?? nativeCredits["Custom"]);
        AddDynamicCreditsSections(result, nativeCredits == null ? null : nativeCredits["groups"] ?? nativeCredits["Groups"]);
        return result;
    }

    private static void AddDynamicCreditsSections(JArray result, JToken token)
    {
        JObject source = token as JObject;
        if (source == null)
            return;
        foreach (JProperty property in source.Properties())
            AddCreditsSection(result, PrettyCreditsTitle(property.Name));
    }

    private static void AddCreditsSection(JArray result, string title)
    {
        title = (title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
            return;
        foreach (JToken item in result)
            if (item.ToString().Equals(title, StringComparison.OrdinalIgnoreCase))
                return;
        result.Add(title);
    }

    private static string PrettyCreditsTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Credits";
        var builder = new StringBuilder();
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (index > 0 && char.IsUpper(current) && char.IsLower(value[index - 1]))
                builder.Append(' ');
            builder.Append(current);
        }
        return builder.ToString();
    }

    private void LeaderboardStateEndpoint(NetworkStream stream)
    {
        WriteJson(stream, LeaderboardState());
    }

    private void ResetLeaderboardEndpoint(NetworkStream stream)
    {
        lock (StateLock)
        {
            LeaderboardItems.Clear();
            LeaderboardEventId++;
            SaveLeaderboardData();
        }
        LeaderboardStateEndpoint(stream);
    }

    private void LeaderboardEntryEndpoint(NetworkStream stream, string body)
    {
        try
        {
            JObject json = JObject.Parse(body ?? "");
            string action = JsonText(json, "action", "add").Trim().ToLowerInvariant();
            string id = JsonText(json, "id");
            lock (StateLock)
            {
                if (action == "delete")
                {
                    LeaderboardItems.RemoveAll(item => JsonText(item, "id").Equals(id, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    JObject item = null;
                    foreach (JObject current in LeaderboardItems)
                        if (!string.IsNullOrWhiteSpace(id) && JsonText(current, "id").Equals(id, StringComparison.OrdinalIgnoreCase))
                            item = current;
                    if (item == null)
                    {
                        item = new JObject();
                        item["id"] = Guid.NewGuid().ToString("N");
                        LeaderboardItems.Add(item);
                    }
                    item["name"] = LimitText(JsonText(json, "name", "Anonymous"), 120);
                    item["amount"] = Math.Max(0, JsonDecimal(json, "amount", 0)).ToString(CultureInfo.InvariantCulture);
                    item["currency"] = LimitText(JsonText(json, "currency", "RUB"), 16);
                    item["platform"] = LimitText(JsonText(json, "platform", "Manual"), 80);
                    item["timestamp"] = JsonText(item, "timestamp", DateTime.UtcNow.ToString("o"));
                }
                LeaderboardEventId++;
                SaveLeaderboardData();
            }
            LeaderboardStateEndpoint(stream);
        }
        catch (Exception ex)
        {
            WriteError(stream, 400, "Leaderboard entry was not changed: " + ex.Message);
        }
    }

    private void StatusEndpoint(NetworkStream stream)
    {
        JObject result = new JObject();
        result["running"] = IsRunning;
        result["host"] = Host;
        result["port"] = Port;
        result["editorVersion"] = EditorVersion;
        result["editorUrl"] = EditorUrl;
        result["widgetUrl"] = WidgetUrl;
        result["goalUrl"] = GoalUrl;
        result["timerUrl"] = TimerUrl;
        result["creditsUrl"] = CreditsUrl;
        result["leaderboardUrl"] = LeaderboardUrl;
        result["dockUrl"] = DockUrl;
        result["settingsPath"] = SettingsPath();
        result["alertMediaDirectory"] = AlertMediaDirectory();
        result["network"] = "127.0.0.1 only";
        WriteJson(stream, result);
    }

    private void ObsUrlEndpoint(NetworkStream stream)
    {
        JObject result = new JObject();
        result["url"] = WidgetUrl;
        result["widgetUrl"] = WidgetUrl;
        result["goalUrl"] = GoalUrl;
        result["timerUrl"] = TimerUrl;
        result["goalTimerUrl"] = BaseUrl + "/goal-timer";
        result["creditsUrl"] = CreditsUrl;
        result["leaderboardUrl"] = LeaderboardUrl;
        result["dockUrl"] = DockUrl;
        WriteJson(stream, result);
    }

    private JObject FontCatalogJson()
    {
        JObject json = new JObject();
        json["windows"] = InstalledWindowsFonts();
        json["google"] = GoogleFontNames();
        return json;
    }

    private JArray GoogleFontNames()
    {
        var fonts = new JArray();
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JToken token in InstalledWindowsFonts())
            installed.Add(token.ToString());
        string[] names =
        {
            "Roboto", "Open Sans", "Montserrat", "Rubik", "Ubuntu", "PT Sans", "PT Serif",
            "Noto Sans", "Noto Serif", "Roboto Condensed", "Roboto Slab", "Russo One",
            "Comfortaa", "Manrope", "Exo 2", "Fira Sans", "Fira Mono", "JetBrains Mono", "Caveat"
        };
        foreach (string name in names)
            if (installed.Contains(name))
                fonts.Add(name);
        return fonts;
    }

    private JArray InstalledWindowsFonts()
    {
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            AddRegistryFonts(set, Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts"));
            AddRegistryFonts(set, Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts"));
            AddInstalledFontFamilies(set);
        }
        catch
        {
            // Если Streamer.bot запущен в ограниченной среде, редактор всё равно покажет базовый набор.
        }
        AddFallbackWindowsFonts(set);

        var fonts = new JArray();
        foreach (string name in set)
            fonts.Add(name);
        return fonts;
    }

    private void AddFallbackWindowsFonts(SortedSet<string> fonts)
    {
        string[] names =
        {
            "Segoe UI", "Arial", "Calibri", "Verdana", "Tahoma", "Trebuchet MS",
            "Times New Roman", "Georgia", "Consolas", "Courier New", "Impact", "Comic Sans MS"
        };
        foreach (string name in names)
            fonts.Add(name);
    }

    private void AddRegistryFonts(SortedSet<string> fonts, Microsoft.Win32.RegistryKey key)
    {
        if (key == null)
            return;
        using (key)
        {
            foreach (string valueName in key.GetValueNames())
            {
                string name = CleanFontName(valueName);
                if (!string.IsNullOrWhiteSpace(name))
                    fonts.Add(name);
            }
        }
    }

    private void AddInstalledFontFamilies(SortedSet<string> fonts)
    {
        Type collectionType = Type.GetType("System.Drawing.Text.InstalledFontCollection, System.Drawing");
        if (collectionType == null)
            return;

        object collection = null;
        try
        {
            collection = Activator.CreateInstance(collectionType);
            System.Reflection.PropertyInfo familiesProperty = collectionType.GetProperty("Families");
            object rawFamilies = familiesProperty == null ? null : familiesProperty.GetValue(collection, null);
            System.Collections.IEnumerable families = rawFamilies as System.Collections.IEnumerable;
            if (families == null)
                return;

            foreach (object family in families)
            {
                System.Reflection.PropertyInfo nameProperty = family == null ? null : family.GetType().GetProperty("Name");
                string name = nameProperty == null ? "" : Convert.ToString(nameProperty.GetValue(family, null), CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(name))
                    fonts.Add(name.Trim());
            }
        }
        catch
        {
            // Реестр уже дал основной список; эта ветка только повышает точность имен.
        }
        finally
        {
            IDisposable disposable = collection as IDisposable;
            if (disposable != null)
                disposable.Dispose();
        }
    }

    private string CleanFontName(string valueName)
    {
        string name = valueName ?? "";
        int index = name.IndexOf(" (", StringComparison.Ordinal);
        if (index > 0)
            name = name.Substring(0, index);
        name = name.Replace(" Regular", "").Trim();
        return name;
    }

    private string AlertMediaDirectory()
    {
        return Path.Combine(SettingsDirectory(), "alert-media");
    }

    private void EnsureAlertMediaLibrary()
    {
        try
        {
            string sounds = Path.Combine(AlertMediaDirectory(), "sounds");
            if (!Directory.Exists(sounds))
                Directory.CreateDirectory(sounds);

            WriteBuiltInSound(Path.Combine(sounds, "soft-pop.wav"), new[] { 620, 840 }, 120);
            WriteBuiltInSound(Path.Combine(sounds, "bright-chime.wav"), new[] { 740, 980, 1240 }, 130);
            WriteBuiltInSound(Path.Combine(sounds, "level-up.wav"), new[] { 440, 660, 880, 1180 }, 115);
            WriteBuiltInSound(Path.Combine(sounds, "coin.wav"), new[] { 1180, 1560 }, 90);
            WriteBuiltInSound(Path.Combine(sounds, "warm-bell.wav"), new[] { 520, 780, 1040 }, 180);
            WriteBuiltInSound(Path.Combine(sounds, "arcade.wav"), new[] { 330, 520, 760, 1040 }, 95);
            WriteBuiltInSound(Path.Combine(sounds, "tiny-fanfare.wav"), new[] { 520, 660, 780, 1040, 1320 }, 120);
            WriteBuiltInSound(Path.Combine(sounds, "typing.wav"), new[] { 1480, 1320, 1540, 1380, 1600 }, 42);
            WriteBuiltInSound(Path.Combine(sounds, "soft-pulse.wav"), new[] { 360, 480 }, 180);
            WriteBuiltInSound(Path.Combine(sounds, "success.wav"), new[] { 620, 780, 980, 1240 }, 145);
        }
        catch (Exception ex)
        {
            Logger.Warn("Alert media library was not initialized. " + ex.Message);
        }
    }

    private void WriteBuiltInSound(string path, int[] frequencies, int toneMilliseconds)
    {
        if (File.Exists(path) || IsDisabledBuiltInSound(path))
            return;

        const int sampleRate = 22050;
        const short channels = 1;
        const short bitsPerSample = 16;
        int silenceSamples = sampleRate * 35 / 1000;
        int toneSamples = sampleRate * toneMilliseconds / 1000;
        int totalSamples = frequencies.Length * (toneSamples + silenceSamples);
        int dataLength = totalSamples * channels * bitsPerSample / 8;

        using (var stream = File.Create(path))
        using (var writer = new BinaryWriter(stream, Encoding.ASCII))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * bitsPerSample / 8);
            writer.Write((short)(channels * bitsPerSample / 8));
            writer.Write(bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);

            foreach (int frequency in frequencies)
            {
                for (int i = 0; i < toneSamples; i++)
                {
                    double fade = Math.Min(1.0, Math.Min(i / (sampleRate * 0.012), (toneSamples - i) / (sampleRate * 0.04)));
                    double wave = Math.Sin(2.0 * Math.PI * frequency * i / sampleRate);
                    writer.Write((short)(wave * fade * 9500));
                }
                for (int i = 0; i < silenceSamples; i++)
                    writer.Write((short)0);
            }
        }
    }

    private JObject AlertMediaLibraryJson()
    {
        var json = new JObject();
        var items = new JArray();
        string root = AlertMediaDirectory();
        if (Directory.Exists(root))
        {
            string[] paths = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                string relative = RelativeAlertMediaPath(path);
                if (string.IsNullOrWhiteSpace(relative) || !IsAllowedMediaExtension(Path.GetExtension(path)))
                    continue;
                items.Add(AlertMediaItem(relative, new FileInfo(path)));
            }
        }
        json["directory"] = root;
        json["maxUploadBytes"] = MaxAlertMediaBytes;
        json["items"] = items;
        return json;
    }

    private JObject AlertMediaItem(string relative, FileInfo info)
    {
        var item = new JObject();
        item["file"] = relative;
        item["name"] = Path.GetFileName(relative);
        item["kind"] = AlertMediaKind(Path.GetExtension(relative));
        item["bytes"] = info == null ? 0 : info.Length;
        item["builtin"] = relative.StartsWith("sounds/", StringComparison.OrdinalIgnoreCase);
        item["url"] = BaseUrl + "/media/" + EncodeMediaPath(relative);
        return item;
    }

    private void UploadAlertMediaEndpoint(NetworkStream stream, string body)
    {
        try
        {
            JObject json = JObject.Parse(body ?? "");
            string name = json["name"] == null ? "" : json["name"].ToString();
            string data = json["data"] == null ? "" : json["data"].ToString();
            int comma = data.IndexOf(',');
            if (comma >= 0)
                data = data.Substring(comma + 1);

            byte[] bytes = Convert.FromBase64String(data);
            if (bytes.Length == 0 || bytes.Length > MaxAlertMediaBytes)
                throw new InvalidOperationException("Alert media file must be between 1 byte and 32 MB.");

            string fileName = SanitizeAlertMediaName(name);
            if (!IsAllowedMediaExtension(Path.GetExtension(fileName)))
                throw new InvalidOperationException("Supported formats: PNG, JPG, WEBP, GIF, MP4, WEBM, MP3, WAV, OGG, M4A.");

            string directory = AlertMediaDirectory();
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            if (File.Exists(path))
            {
                string stem = Path.GetFileNameWithoutExtension(fileName);
                string extension = Path.GetExtension(fileName);
                fileName = stem + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + extension;
                path = Path.Combine(directory, fileName);
            }

            File.WriteAllBytes(path, bytes);
            Logger.Info("Alert media uploaded: " + fileName + " (" + bytes.Length.ToString(CultureInfo.InvariantCulture) + " bytes).");
            WriteJson(stream, AlertMediaLibraryJson());
        }
        catch (Exception ex)
        {
            WriteError(stream, 400, "Alert media was not uploaded: " + ex.Message);
        }
    }

    private void DeleteAlertMediaEndpoint(NetworkStream stream, string body)
    {
        try
        {
            JObject json = JObject.Parse(body ?? "");
            string relative = json["file"] == null ? "" : json["file"].ToString();
            string path = ResolveAlertMediaPath(relative);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new InvalidOperationException("Media file was not found.");

            File.Delete(path);
            if (relative.StartsWith("sounds/", StringComparison.OrdinalIgnoreCase))
                DisableBuiltInSound(relative);
            Logger.Info("Alert media deleted: " + relative);
            WriteJson(stream, AlertMediaLibraryJson());
        }
        catch (Exception ex)
        {
            WriteError(stream, 400, "Alert media was not deleted: " + ex.Message);
        }
    }

    private void OpenAlertMediaEndpoint(NetworkStream stream)
    {
        try
        {
            string directory = AlertMediaDirectory();
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            DonConnectShell.OpenDirectory(directory);
            var result = new JObject();
            result["ok"] = true;
            result["directory"] = directory;
            WriteJson(stream, result);
        }
        catch (Exception ex)
        {
            WriteError(stream, 400, "Alert media directory was not opened: " + ex.Message);
        }
    }

    private string DisabledBuiltInSoundsPath()
    {
        return Path.Combine(SettingsDirectory(), "alert-media-disabled.json");
    }

    private bool IsDisabledBuiltInSound(string path)
    {
        string relative = RelativeAlertMediaPath(path);
        foreach (string disabled in LoadDisabledBuiltInSounds())
            if (disabled.Equals(relative, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void DisableBuiltInSound(string relative)
    {
        List<string> disabled = LoadDisabledBuiltInSounds();
        foreach (string item in disabled)
            if (item.Equals(relative, StringComparison.OrdinalIgnoreCase))
                return;
        disabled.Add(relative);
        File.WriteAllText(DisabledBuiltInSoundsPath(), JArray.FromObject(disabled).ToString(Formatting.Indented), new UTF8Encoding(false));
    }

    private List<string> LoadDisabledBuiltInSounds()
    {
        var result = new List<string>();
        try
        {
            string path = DisabledBuiltInSoundsPath();
            if (!File.Exists(path))
                return result;
            foreach (JToken token in JArray.Parse(File.ReadAllText(path, Encoding.UTF8)))
                if (token != null && !string.IsNullOrWhiteSpace(token.ToString()))
                    result.Add(token.ToString());
        }
        catch { }
        return result;
    }

    private void ServeAlertMedia(NetworkStream stream, string encodedRelative)
    {
        try
        {
            string relative = Uri.UnescapeDataString(encodedRelative ?? "").Replace('\\', '/');
            string path = ResolveAlertMediaPath(relative);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !IsAllowedMediaExtension(Path.GetExtension(path)))
            {
                WriteResponse(stream, 404, "text/plain; charset=utf-8", "Not Found");
                return;
            }

            WriteBinaryResponse(stream, 200, AlertMediaContentType(Path.GetExtension(path)), File.ReadAllBytes(path));
        }
        catch
        {
            WriteResponse(stream, 404, "text/plain; charset=utf-8", "Not Found");
        }
    }

    private string ResolveAlertMediaPath(string relative)
    {
        string normalized = (relative ?? "").Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.IndexOf("..", StringComparison.Ordinal) >= 0)
            return "";

        string root = Path.GetFullPath(AlertMediaDirectory()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(AlertMediaDirectory(), normalized.Replace('/', Path.DirectorySeparatorChar)));
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : "";
    }

    private string RelativeAlertMediaPath(string path)
    {
        string root = Path.GetFullPath(AlertMediaDirectory()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(path);
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length).Replace('\\', '/') : "";
    }

    private string SanitizeAlertMediaName(string name)
    {
        string fileName = Path.GetFileName(name ?? "").Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(fileName) ? "alert-media.bin" : fileName;
    }

    private string EncodeMediaPath(string relative)
    {
        string[] parts = (relative ?? "").Replace('\\', '/').Split('/');
        for (int i = 0; i < parts.Length; i++)
            parts[i] = Uri.EscapeDataString(parts[i]);
        return string.Join("/", parts);
    }

    private bool IsAllowedMediaExtension(string extension)
    {
        string value = (extension ?? "").Trim().ToLowerInvariant();
        return value == ".png" || value == ".jpg" || value == ".jpeg" || value == ".webp" || value == ".gif"
            || value == ".mp4" || value == ".webm" || value == ".mp3" || value == ".wav" || value == ".ogg" || value == ".m4a";
    }

    private string AlertMediaKind(string extension)
    {
        string value = (extension ?? "").Trim().ToLowerInvariant();
        if (value == ".mp4" || value == ".webm")
            return "video";
        if (value == ".mp3" || value == ".wav" || value == ".ogg" || value == ".m4a")
            return "audio";
        return "image";
    }

    private string AlertMediaContentType(string extension)
    {
        string value = (extension ?? "").Trim().ToLowerInvariant();
        if (value == ".png") return "image/png";
        if (value == ".jpg" || value == ".jpeg") return "image/jpeg";
        if (value == ".webp") return "image/webp";
        if (value == ".gif") return "image/gif";
        if (value == ".mp4") return "video/mp4";
        if (value == ".webm") return "video/webm";
        if (value == ".mp3") return "audio/mpeg";
        if (value == ".wav") return "audio/wav";
        if (value == ".ogg") return "audio/ogg";
        if (value == ".m4a") return "audio/mp4";
        return "application/octet-stream";
    }

    private HttpRequest ReadRequest(NetworkStream stream)
    {
        var bytes = new List<byte>();
        var buffer = new byte[4096];
        int headerEnd = -1;

        while (bytes.Count < 65536)
        {
            int read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
                break;
            for (int i = 0; i < read; i++)
                bytes.Add(buffer[i]);
            headerEnd = FindHeaderEnd(bytes);
            if (headerEnd >= 0)
                break;
        }

        if (headerEnd < 0)
            return null;

        string headers = Encoding.UTF8.GetString(bytes.GetRange(0, headerEnd).ToArray());
        string[] lines = headers.Split(new[] { "\r\n" }, StringSplitOptions.None);
        if (lines.Length == 0)
            return null;

        string[] first = lines[0].Split(' ');
        if (first.Length < 2)
            return null;

        int contentLength = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            int index = lines[i].IndexOf(':');
            if (index <= 0)
                continue;
            string name = lines[i].Substring(0, index).Trim();
            string value = lines[i].Substring(index + 1).Trim();
            if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out contentLength);
        }

        if (contentLength < 0 || contentLength > MaxHttpBodyBytes)
            return null;

        int bodyStart = headerEnd + 4;
        var bodyBytes = new List<byte>();
        for (int i = bodyStart; i < bytes.Count; i++)
            bodyBytes.Add(bytes[i]);

        while (bodyBytes.Count < contentLength)
        {
            int remaining = contentLength - bodyBytes.Count;
            int read = stream.Read(buffer, 0, Math.Min(buffer.Length, remaining));
            if (read <= 0)
                break;
            for (int i = 0; i < read; i++)
                bodyBytes.Add(buffer[i]);
        }

        string path = first[1];
        int queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
            path = path.Substring(0, queryIndex);

        return new HttpRequest
        {
            Method = first[0].ToUpperInvariant(),
            Path = path,
            Body = contentLength > 0 ? Encoding.UTF8.GetString(bodyBytes.ToArray(), 0, Math.Min(bodyBytes.Count, contentLength)) : ""
        };
    }

    private int FindHeaderEnd(List<byte> bytes)
    {
        for (int i = 3; i < bytes.Count; i++)
            if (bytes[i - 3] == 13 && bytes[i - 2] == 10 && bytes[i - 1] == 13 && bytes[i] == 10)
                return i - 3;
        return -1;
    }

    private void WriteJson(NetworkStream stream, JObject json)
    {
        WriteResponse(stream, 200, "application/json; charset=utf-8", json.ToString(Formatting.None));
    }

    private void WriteError(NetworkStream stream, int status, string message)
    {
        var json = new JObject();
        json["error"] = message;
        WriteResponse(stream, status, "application/json; charset=utf-8", json.ToString(Formatting.None));
    }

    private void WriteResponse(NetworkStream stream, int status, string contentType, string body)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body ?? "");
        string statusText = status == 200 ? "OK" : status == 400 ? "Bad Request" : status == 404 ? "Not Found" : "Error";
        string header = "HTTP/1.1 " + status.ToString(CultureInfo.InvariantCulture) + " " + statusText + "\r\n"
            + "Content-Type: " + contentType + "\r\n"
            + "Content-Length: " + bodyBytes.Length.ToString(CultureInfo.InvariantCulture) + "\r\n"
            + "Cache-Control: no-store\r\n"
            + "X-Content-Type-Options: nosniff\r\n"
            + "Connection: close\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(bodyBytes, 0, bodyBytes.Length);
    }

    private void WriteBinaryResponse(NetworkStream stream, int status, string contentType, byte[] bodyBytes)
    {
        bodyBytes = bodyBytes ?? new byte[0];
        string statusText = status == 200 ? "OK" : status == 404 ? "Not Found" : "Error";
        string header = "HTTP/1.1 " + status.ToString(CultureInfo.InvariantCulture) + " " + statusText + "\r\n"
            + "Content-Type: " + contentType + "\r\n"
            + "Content-Length: " + bodyBytes.Length.ToString(CultureInfo.InvariantCulture) + "\r\n"
            + "Cache-Control: no-store\r\n"
            + "X-Content-Type-Options: nosniff\r\n"
            + "Connection: close\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(bodyBytes, 0, bodyBytes.Length);
    }

    private int ReadPort()
    {
        int value;
        string raw = BridgeSettings.Get("widget.preferredPort", DefaultPort.ToString(CultureInfo.InvariantCulture));
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            value = DefaultPort;
        if (value < 1024 || value > 65535)
            value = DefaultPort;
        return value;
    }

    private string SettingsPath()
    {
        string configured = BridgeSettings.Get("widget.settingsPath", "");
        return string.IsNullOrWhiteSpace(configured) ? Path.Combine(SettingsDirectory(), "widget-settings.json") : configured;
    }

    private string SettingsDirectory()
    {
        string configured = BridgeSettings.Get("widget.dataDirectory", "");
        if (string.IsNullOrWhiteSpace(configured))
            configured = BridgeSettings.Get("donconnect.dataDirectory", "");
        return DonConnectPaths.DataDirectory(configured);
    }

    private string OverlaySettingsPath()
    {
        string configured = BridgeSettings.Get("widget.overlaySettingsPath", "");
        return string.IsNullOrWhiteSpace(configured) ? Path.Combine(SettingsDirectory(), "goal-timer-settings.json") : configured;
    }

    private string CreditsSettingsPath()
    {
        string configured = BridgeSettings.Get("widget.creditsSettingsPath", "");
        return string.IsNullOrWhiteSpace(configured) ? Path.Combine(SettingsDirectory(), "credits-settings.json") : configured;
    }

    private string LeaderboardSettingsPath()
    {
        string configured = BridgeSettings.Get("widget.leaderboardSettingsPath", "");
        return string.IsNullOrWhiteSpace(configured) ? Path.Combine(SettingsDirectory(), "leaderboard-settings.json") : configured;
    }

    private string LeaderboardDataPath()
    {
        string configured = BridgeSettings.Get("widget.leaderboardDataPath", "");
        return string.IsNullOrWhiteSpace(configured) ? Path.Combine(SettingsDirectory(), "leaderboard-data.json") : configured;
    }

    private string ContentFilterSettingsPath()
    {
        string configured = BridgeSettings.Get("widget.contentFilterSettingsPath", "");
        return string.IsNullOrWhiteSpace(configured) ? Path.Combine(SettingsDirectory(), "content-filter-settings.json") : configured;
    }

    private WidgetSettings LoadSettings()
    {
        try
        {
            string path = SettingsPath();
            if (File.Exists(path))
                return WidgetSettings.FromJson(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            Logger.Warn("Widget settings were not loaded. " + ex.Message);
        }

        return WidgetSettings.Default();
    }

    private void SaveSettings(WidgetSettings settings)
    {
        string path = SettingsPath();
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, settings.ToJson().ToString(Formatting.Indented), new UTF8Encoding(false));
    }

    private JObject LoadOverlaySettings()
    {
        try
        {
            string path = OverlaySettingsPath();
            if (File.Exists(path))
                return NormalizeOverlaySettings(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            Logger.Warn("Goal/timer settings were not loaded. " + ex.Message);
        }

        JObject settings = DefaultOverlaySettings();
        settings["GoalEnabled"] = BridgeSettings.GetBool("goal.enabled", true);
        settings["GoalHeaderTitle"] = BridgeSettings.Get("goal.headerTitle", JsonText(settings, "GoalHeaderTitle"));
        settings["GoalTitle"] = BridgeSettings.Get("goal.title", JsonText(settings, "GoalTitle"));
        settings["GoalCurrent"] = BridgeSettings.Get("goal.current", JsonText(settings, "GoalCurrent"));
        settings["GoalTarget"] = BridgeSettings.Get("goal.target", JsonText(settings, "GoalTarget"));
        settings["GoalCurrency"] = BridgeSettings.Get("goal.currency", JsonText(settings, "GoalCurrency"));
        settings["TimerEnabled"] = BridgeSettings.GetBool("timer.enabled", false);
        settings["TimerHeaderTitle"] = BridgeSettings.Get("timer.headerTitle", JsonText(settings, "TimerHeaderTitle"));
        settings["TimerTitle"] = BridgeSettings.Get("timer.title", JsonText(settings, "TimerTitle"));
        settings["TimerSubtitle"] = BridgeSettings.Get("timer.subtitle", JsonText(settings, "TimerSubtitle"));
        settings["TimerUnitAmount"] = BridgeSettings.Get("timer.unitAmount", JsonText(settings, "TimerUnitAmount"));
        settings["TimerSecondsPerUnit"] = BridgeSettings.Get("timer.secondsPerUnit", JsonText(settings, "TimerSecondsPerUnit"));
        settings["TimerMaxSeconds"] = BridgeSettings.Get("timer.maxSeconds", JsonText(settings, "TimerMaxSeconds"));
        settings["TimerCurrency"] = BridgeSettings.Get("timer.currency", JsonText(settings, "TimerCurrency"));
        return settings;
    }

    private JObject LoadCreditsSettings()
    {
        try
        {
            string path = CreditsSettingsPath();
            if (File.Exists(path))
                return NormalizeCreditsSettings(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            Logger.Warn("Credits settings were not loaded. " + ex.Message);
        }

        JObject settings = DefaultCreditsSettings();
        settings["CreditsEnabled"] = BridgeSettings.GetBool("STREAMERBOT_CREDITS_ENABLED", false);
        settings["Title"] = BridgeSettings.Get("STREAMERBOT_CREDITS_TITLE", JsonText(settings, "Title"));
        settings["Subtitle"] = BridgeSettings.Get("STREAMERBOT_CREDITS_SUBTITLE", JsonText(settings, "Subtitle"));
        settings["Outro"] = BridgeSettings.Get("STREAMERBOT_CREDITS_OUTRO", JsonText(settings, "Outro"));
        settings["Duration"] = BridgeSettings.Get("STREAMERBOT_CREDITS_DURATION", JsonText(settings, "Duration"));
        settings["DonationFields"] = BridgeSettings.Get("STREAMERBOT_CREDITS_FIELDS", JsonText(settings, "DonationFields"));
        return settings;
    }

    private JObject LoadLeaderboardSettings()
    {
        try
        {
            string path = LeaderboardSettingsPath();
            if (File.Exists(path))
                return NormalizeLeaderboardSettings(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            Logger.Warn("Leaderboard settings were not loaded. " + ex.Message);
        }

        return DefaultLeaderboardSettings();
    }

    private JObject LoadContentFilterSettings()
    {
        try
        {
            string path = ContentFilterSettingsPath();
            if (File.Exists(path))
                return NormalizeContentFilterSettings(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            Logger.Warn("Content filter settings were not loaded. " + ex.Message);
        }

        return DefaultContentFilterSettings();
    }

    private void LoadLeaderboardData()
    {
        lock (StateLock)
        {
            LeaderboardItems.Clear();
            try
            {
                string path = LeaderboardDataPath();
                if (!File.Exists(path))
                    return;

                JArray items = JArray.Parse(File.ReadAllText(path, Encoding.UTF8));
                foreach (JToken token in items)
                    if (token is JObject)
                        LeaderboardItems.Add((JObject)token.DeepClone());

                while (LeaderboardItems.Count > 500)
                    LeaderboardItems.RemoveAt(0);
            }
            catch (Exception ex)
            {
                Logger.Warn("Leaderboard data were not loaded. " + ex.Message);
            }
        }
    }

    private JObject OverlaySettingsForClient()
    {
        return OverlaySettings == null ? DefaultOverlaySettings() : (JObject)OverlaySettings.DeepClone();
    }

    private JObject CreditsSettingsForClient()
    {
        return CreditsSettings == null ? DefaultCreditsSettings() : (JObject)CreditsSettings.DeepClone();
    }

    private JObject LeaderboardSettingsForClient()
    {
        return LeaderboardSettings == null ? DefaultLeaderboardSettings() : (JObject)LeaderboardSettings.DeepClone();
    }

    private JObject ContentFilterSettingsForClient()
    {
        return ContentFilterSettings == null ? DefaultContentFilterSettings() : (JObject)ContentFilterSettings.DeepClone();
    }

    private void SaveOverlaySettings(JObject settings)
    {
        string path = OverlaySettingsPath();
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, settings.ToString(Formatting.Indented), new UTF8Encoding(false));
    }

    private void SaveCreditsSettings(JObject settings)
    {
        string path = CreditsSettingsPath();
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, settings.ToString(Formatting.Indented), new UTF8Encoding(false));
    }

    private void SaveLeaderboardSettings(JObject settings)
    {
        string path = LeaderboardSettingsPath();
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, settings.ToString(Formatting.Indented), new UTF8Encoding(false));
    }

    private void SaveLeaderboardData()
    {
        string path = LeaderboardDataPath();
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var items = new JArray();
        foreach (JObject item in LeaderboardItems)
            items.Add(item.DeepClone());
        File.WriteAllText(path, items.ToString(Formatting.Indented), new UTF8Encoding(false));
    }

    private void SaveContentFilterSettings(JObject settings)
    {
        string path = ContentFilterSettingsPath();
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, settings.ToString(Formatting.Indented), new UTF8Encoding(false));
    }

    private JObject DefaultOverlaySettings()
    {
        var json = new JObject();
        json["Mode"] = "both";
        json["PresetName"] = "Clean";
        json["GoalEnabled"] = true;
        json["GoalHeaderTitle"] = "Goal";
        json["GoalTitle"] = "Goal";
        json["GoalCurrent"] = "0";
        json["GoalTarget"] = "10000";
        json["GoalCurrency"] = "RUB";
        json["TimerEnabled"] = false;
        json["TimerHeaderTitle"] = "Timer";
        json["TimerTitle"] = "Timer";
        json["TimerSubtitle"] = "";
        json["TimerStartSeconds"] = "0";
        json["TimerUnitAmount"] = "100";
        json["TimerSecondsPerUnit"] = "60";
        json["TimerMaxSeconds"] = "0";
        json["TimerCurrency"] = "RUB";
        json["TimerMode"] = "countdown";
        json["TimerShowServices"] = false;
        json["TimerShowConversion"] = true;
        json["TimerX"] = 0;
        json["TimerY"] = 0;
        json["TimerWidth"] = 680;
        json["TimerTextAlign"] = "center";
        json["Width"] = 920;
        json["BorderRadius"] = 22;
        json["BarRadius"] = 22;
        json["Padding"] = 22;
        json["TitleSize"] = 30;
        json["ValueSize"] = 42;
        json["LabelSize"] = 17;
        json["MetaSize"] = 17;
        json["Opacity"] = 0.94;
        json["ContainerOpacity"] = 0.94;
        json["BarOpacity"] = 1.0;
        json["ShowPanelBackground"] = true;
        json["ShowGoalBar"] = true;
        json["ShowGoalProgress"] = true;
        json["ShowGoalMeta"] = true;
        json["ShowGoalText"] = true;
        json["ShowGoalValue"] = true;
        json["ShowGoalImage"] = false;
        json["GoalImageDataUrl"] = "";
        json["GoalImageName"] = "";
        json["GoalImageMode"] = "reveal";
        json["GoalImageFit"] = "contain";
        json["GoalImageWidth"] = 680;
        json["GoalImageHeight"] = 220;
        json["GoalImageX"] = 0;
        json["GoalImageY"] = 0;
        json["GoalBarVisualMode"] = "bar";
        json["GoalFillDirection"] = "horizontal";
        json["GoalBarLength"] = 680;
        json["ShowDecorImage"] = false;
        json["DecorImageDataUrl"] = "";
        json["DecorImageName"] = "";
        json["DecorImageX"] = 0;
        json["DecorImageY"] = 0;
        json["DecorImageWidth"] = 220;
        json["FontFamily"] = "Segoe UI";
        json["GoalHeaderFontFamily"] = "";
        json["GoalTitleFontFamily"] = "";
        json["GoalValueFontFamily"] = "";
        json["ServicesFontFamily"] = "";
        json["LastDonationFontFamily"] = "";
        json["TimerFontFamily"] = "";
        json["BackgroundColor"] = "#10131a";
        json["TextColor"] = "#f8fbff";
        json["MutedColor"] = "#b8c0cc";
        json["AccentColor"] = "#7c3cff";
        json["BarColor"] = "#1e2026";
        json["ShowServices"] = true;
        json["ServicesTitle"] = "Connected providers";
        json["ServicesTextAlign"] = "center";
        json["ServicesFontSize"] = 14;
        json["HiddenServices"] = new JArray();
        json["ShowLastDonation"] = true;
        json["ShowLastDonor"] = true;
        json["ShowLastAmount"] = true;
        json["ShowLastPlatform"] = true;
        json["LastDonationFontSize"] = 14;
        json["LastDonationTextAlign"] = "center";
        json["Bare"] = false;
        json["GoalFormat"] = "amount";
        json["GoalBarWidth"] = 100;
        json["GoalBarHeight"] = 84;
        json["GoalBarAlign"] = "center";
        json["GoalTextPlacement"] = "inside";
        json["GoalTextAlign"] = "center";
        json["GoalTextOffsetX"] = 0;
        json["GoalTextOffsetY"] = 0;
        return json;
    }

    private JObject DefaultCreditsSettings()
    {
        var json = new JObject();
        json["CreditsEnabled"] = true;
        json["PresetName"] = "Classic";
        json["Title"] = "Thanks for watching";
        json["Subtitle"] = "Today with us";
        json["Outro"] = "See you next stream";
        json["Duration"] = "70s";
        json["DurationSeconds"] = 70;
        json["LockDuration"] = false;
        json["UseNativeCredits"] = true;
        json["UseTestData"] = false;
        json["DonationFields"] = "name,amount,message";
        json["SectionTitle"] = "Donations";
        json["ShowAmounts"] = true;
        json["ShowPlatforms"] = true;
        json["ShowMessages"] = true;
        json["HiddenSections"] = new JArray();
        json["Width"] = 1120;
        json["FontSize"] = 48;
        json["FontFamily"] = "Segoe UI";
        json["TitleFontFamily"] = "";
        json["DetailFontFamily"] = "";
        json["TransparentBackground"] = true;
        json["BackgroundColor"] = "#000000";
        json["TextColor"] = "#f7f4ec";
        json["MutedColor"] = "#b9d8d2";
        json["AccentColor"] = "#ffcf5a";
        json["ShadowColor"] = "rgba(0,0,0,0.7)";
        return json;
    }

    private JObject DefaultContentFilterSettings()
    {
        var json = new JObject();
        json["BlockedNames"] = "";
        json["BlockedWords"] = "";
        json["ReplacementName"] = "Anonymous";
        json["ReplacementText"] = "[hidden]";
        return json;
    }

    private JObject DefaultLeaderboardSettings()
    {
        var json = new JObject();
        json["Enabled"] = true;
        json["Title"] = "Top donors";
        json["Mode"] = "overall";
        json["TopCount"] = 5;
        json["SlideDuration"] = 5000;
        json["ShowTitle"] = true;
        json["ShowRanks"] = true;
        json["ShowAmounts"] = true;
        json["ShowPlatforms"] = true;
        json["Width"] = 560;
        json["Padding"] = 18;
        json["BorderRadius"] = 16;
        json["FontSize"] = 22;
        json["TitleSize"] = 26;
        json["RowGap"] = 8;
        json["Opacity"] = 0.94;
        json["BackgroundColor"] = "#10131a";
        json["TextColor"] = "#f8fbff";
        json["MutedColor"] = "#b8c0cc";
        json["AccentColor"] = "#7c3cff";
        json["FontFamily"] = "Segoe UI";
        json["TitleFontFamily"] = "";
        json["AmountFontFamily"] = "";
        return json;
    }

    private JObject NormalizeOverlaySettings(string raw)
    {
        JObject incoming = string.IsNullOrWhiteSpace(raw) ? new JObject() : JObject.Parse(raw);
        JObject result = DefaultOverlaySettings();
        CopyJsonValues(result, incoming);
        result["Mode"] = NormalizeChoice(JsonText(result, "Mode"), "both", "goal", "timer", "both");
        result["GoalFormat"] = NormalizeChoice(JsonText(result, "GoalFormat"), "amount", "amount", "percent", "summary");
        result["Width"] = ClampInt(JsonInt(result, "Width", 720), 240, 1920);
        result["BorderRadius"] = ClampInt(JsonInt(result, "BorderRadius", 22), 0, 120);
        result["BarRadius"] = ClampInt(JsonInt(result, "BarRadius", JsonInt(result, "BorderRadius", 22)), 0, 120);
        result["Padding"] = ClampInt(JsonInt(result, "Padding", 22), 0, 120);
        result["TitleSize"] = ClampInt(JsonInt(result, "TitleSize", 24), 10, 96);
        result["ValueSize"] = ClampInt(JsonInt(result, "ValueSize", 42), 12, 140);
        result["LabelSize"] = ClampInt(JsonInt(result, "LabelSize", 15), 8, 64);
        result["MetaSize"] = ClampInt(JsonInt(result, "MetaSize", 16), 8, 64);
        result["Opacity"] = ClampDouble(JsonDouble(result, "Opacity", 0.9), 0.1, 1);
        result["ContainerOpacity"] = ClampDouble(JsonDouble(result, "ContainerOpacity", JsonDouble(result, "Opacity", 0.94)), 0, 1);
        result["BarOpacity"] = ClampDouble(JsonDouble(result, "BarOpacity", 1.0), 0, 1);
        result["GoalImageMode"] = NormalizeChoice(JsonText(result, "GoalImageMode"), "reveal", "overlay", "reveal");
        result["GoalImageFit"] = NormalizeChoice(JsonText(result, "GoalImageFit"), "contain", "contain", "cover");
        result["GoalImageWidth"] = ClampInt(JsonInt(result, "GoalImageWidth", 520), 20, 1600);
        result["GoalImageHeight"] = ClampInt(JsonInt(result, "GoalImageHeight", 160), 20, 1000);
        result["GoalImageX"] = ClampInt(JsonInt(result, "GoalImageX", 0), -800, 800);
        result["GoalImageY"] = ClampInt(JsonInt(result, "GoalImageY", 0), -600, 600);
        result["GoalBarVisualMode"] = NormalizeChoice(JsonText(result, "GoalBarVisualMode"), "bar", "bar", "image-reveal", "image-silhouette", "image-transparent", "image-inverse");
        result["GoalFillDirection"] = NormalizeChoice(JsonText(result, "GoalFillDirection"), "horizontal", "horizontal", "vertical");
        result["GoalBarLength"] = ClampInt(JsonInt(result, "GoalBarLength", 520), 40, 1600);
        result["DecorImageX"] = ClampInt(JsonInt(result, "DecorImageX", 0), -800, 800);
        result["DecorImageY"] = ClampInt(JsonInt(result, "DecorImageY", 0), -600, 600);
        result["DecorImageWidth"] = ClampInt(JsonInt(result, "DecorImageWidth", 220), 20, 1200);
        result["GoalBarWidth"] = 100;
        result["GoalBarHeight"] = ClampInt(JsonInt(result, "GoalBarHeight", 74), 6, 240);
        result["GoalBarAlign"] = NormalizeChoice(JsonText(result, "GoalBarAlign"), "center", "left", "center", "right");
        result["GoalTextPlacement"] = NormalizeChoice(JsonText(result, "GoalTextPlacement"), "inside", "above", "inside", "below");
        result["GoalTextAlign"] = NormalizeChoice(JsonText(result, "GoalTextAlign"), "center", "left", "center", "right");
        result["GoalTextOffsetX"] = ClampInt(JsonInt(result, "GoalTextOffsetX", 0), -300, 300);
        result["GoalTextOffsetY"] = ClampInt(JsonInt(result, "GoalTextOffsetY", 0), -160, 160);
        result["ServicesTextAlign"] = NormalizeChoice(JsonText(result, "ServicesTextAlign"), "center", "left", "center", "right");
        result["ServicesFontSize"] = ClampInt(JsonInt(result, "ServicesFontSize", 14), 8, 64);
        result["HiddenServices"] = NormalizeStringArray(result["HiddenServices"] as JArray, 20);
        result["LastDonationFontSize"] = ClampInt(JsonInt(result, "LastDonationFontSize", 14), 8, 64);
        result["LastDonationTextAlign"] = NormalizeChoice(JsonText(result, "LastDonationTextAlign"), "center", "left", "center", "right");
        result["TimerMode"] = NormalizeChoice(JsonText(result, "TimerMode"), "countdown", "countdown", "countup-reset");
        result["TimerX"] = ClampInt(JsonInt(result, "TimerX", 0), -800, 800);
        result["TimerY"] = ClampInt(JsonInt(result, "TimerY", 0), -600, 600);
        result["TimerWidth"] = ClampInt(JsonInt(result, "TimerWidth", 320), 80, 1600);
        result["TimerTextAlign"] = NormalizeChoice(JsonText(result, "TimerTextAlign"), "center", "left", "center", "right");
        return result;
    }

    private JObject NormalizeCreditsSettings(string raw)
    {
        JObject incoming = string.IsNullOrWhiteSpace(raw) ? new JObject() : JObject.Parse(raw);
        JObject result = DefaultCreditsSettings();
        CopyJsonValues(result, incoming);
        result["Width"] = ClampInt(JsonInt(result, "Width", 920), 320, 1920);
        result["FontSize"] = ClampInt(JsonInt(result, "FontSize", 42), 14, 120);
        result["DurationSeconds"] = ClampInt(JsonInt(result, "DurationSeconds", ParseDurationSeconds(JsonText(result, "Duration"), 70)), 5, 600);
        result["Duration"] = result["DurationSeconds"].ToString() + "s";
        result["HiddenSections"] = NormalizeStringArray(result["HiddenSections"] as JArray, 80);
        bool transparentBackground = JsonBool(result, "TransparentBackground", JsonText(result, "BackgroundColor", "transparent").Equals("transparent", StringComparison.OrdinalIgnoreCase));
        if (JsonText(result, "BackgroundColor", "#000000").Equals("transparent", StringComparison.OrdinalIgnoreCase))
            result["BackgroundColor"] = "#000000";
        result["TransparentBackground"] = transparentBackground;
        return result;
    }

    private JObject NormalizeContentFilterSettings(string raw)
    {
        JObject incoming = string.IsNullOrWhiteSpace(raw) ? new JObject() : JObject.Parse(raw);
        JObject result = DefaultContentFilterSettings();
        CopyJsonValues(result, incoming);
        result["BlockedNames"] = LimitText(JsonText(result, "BlockedNames"), 12000);
        result["BlockedWords"] = LimitText(JsonText(result, "BlockedWords"), 12000);
        result["ReplacementName"] = LimitText(JsonText(result, "ReplacementName", "Anonymous"), 120);
        result["ReplacementText"] = LimitText(JsonText(result, "ReplacementText", "[hidden]"), 240);
        return result;
    }

    private JObject NormalizeLeaderboardSettings(string raw)
    {
        JObject incoming = string.IsNullOrWhiteSpace(raw) ? new JObject() : JObject.Parse(raw);
        JObject result = DefaultLeaderboardSettings();
        CopyJsonValues(result, incoming);
        result["Mode"] = NormalizeChoice(JsonText(result, "Mode"), "overall", "overall", "platform-slides", "recent");
        result["TopCount"] = ClampInt(JsonInt(result, "TopCount", 5), 1, 10);
        result["SlideDuration"] = ClampInt(JsonInt(result, "SlideDuration", 5000), 1000, 30000);
        result["Width"] = ClampInt(JsonInt(result, "Width", 560), 240, 1920);
        result["Padding"] = ClampInt(JsonInt(result, "Padding", 18), 0, 120);
        result["BorderRadius"] = ClampInt(JsonInt(result, "BorderRadius", 16), 0, 120);
        result["FontSize"] = ClampInt(JsonInt(result, "FontSize", 22), 10, 96);
        result["TitleSize"] = ClampInt(JsonInt(result, "TitleSize", 26), 10, 120);
        result["RowGap"] = ClampInt(JsonInt(result, "RowGap", 8), 0, 48);
        result["Opacity"] = ClampDouble(JsonDouble(result, "Opacity", 0.94), 0, 1);
        return result;
    }

    private void ApplyOverlaySettings(JObject settings)
    {
        BridgeSettings.Set("goal.enabled", JsonBool(settings, "GoalEnabled", true) ? "true" : "false", true);
        BridgeSettings.Set("goal.headerTitle", JsonText(settings, "GoalHeaderTitle"), true);
        BridgeSettings.Set("goal.title", JsonText(settings, "GoalTitle"), true);
        BridgeSettings.Set("goal.current", JsonText(settings, "GoalCurrent"), true);
        BridgeSettings.Set("goal.target", JsonText(settings, "GoalTarget"), true);
        BridgeSettings.Set("goal.currency", JsonText(settings, "GoalCurrency"), true);

        BridgeSettings.Set("timer.enabled", JsonBool(settings, "TimerEnabled", false) ? "true" : "false", true);
        BridgeSettings.Set("timer.headerTitle", JsonText(settings, "TimerHeaderTitle"), true);
        BridgeSettings.Set("timer.title", JsonText(settings, "TimerTitle"), true);
        BridgeSettings.Set("timer.subtitle", JsonText(settings, "TimerSubtitle"), true);
        BridgeSettings.Set("timer.unitAmount", JsonText(settings, "TimerUnitAmount"), true);
        BridgeSettings.Set("timer.secondsPerUnit", JsonText(settings, "TimerSecondsPerUnit"), true);
        BridgeSettings.Set("timer.maxSeconds", JsonText(settings, "TimerMaxSeconds"), true);
        BridgeSettings.Set("timer.currency", JsonText(settings, "TimerCurrency"), true);
        BridgeSettings.Set("timer.mode", JsonText(settings, "TimerMode", "countdown"), true);

        double seconds = JsonDouble(settings, "TimerStartSeconds", 0);
        BridgeSettings.Set("timer.endsAt", DateTime.UtcNow.AddSeconds(Math.Max(0, seconds)).ToString("o"), true);
        BridgeSettings.Set("timer.startedAt", DateTime.UtcNow.ToString("o"), true);
    }

    private void ApplyTimerSettingsWithoutReset(JObject settings)
    {
        BridgeSettings.Set("timer.enabled", JsonBool(settings, "TimerEnabled", false) ? "true" : "false", true);
        BridgeSettings.Set("timer.headerTitle", JsonText(settings, "TimerHeaderTitle"), true);
        BridgeSettings.Set("timer.title", JsonText(settings, "TimerTitle"), true);
        BridgeSettings.Set("timer.subtitle", JsonText(settings, "TimerSubtitle"), true);
        BridgeSettings.Set("timer.unitAmount", JsonText(settings, "TimerUnitAmount"), true);
        BridgeSettings.Set("timer.secondsPerUnit", JsonText(settings, "TimerSecondsPerUnit"), true);
        BridgeSettings.Set("timer.maxSeconds", JsonText(settings, "TimerMaxSeconds"), true);
        BridgeSettings.Set("timer.currency", JsonText(settings, "TimerCurrency"), true);
        BridgeSettings.Set("timer.mode", JsonText(settings, "TimerMode", "countdown"), true);
    }

    private void ApplyCreditsSettings(JObject settings)
    {
        BridgeSettings.Set("STREAMERBOT_CREDITS_ENABLED", JsonBool(settings, "CreditsEnabled", true) ? "true" : "false", true);
        BridgeSettings.Set("STREAMERBOT_CREDITS_TITLE", JsonText(settings, "Title"), true);
        BridgeSettings.Set("STREAMERBOT_CREDITS_SUBTITLE", JsonText(settings, "Subtitle"), true);
        BridgeSettings.Set("STREAMERBOT_CREDITS_OUTRO", JsonText(settings, "Outro"), true);
        BridgeSettings.Set("STREAMERBOT_CREDITS_DURATION", JsonText(settings, "Duration"), true);
        BridgeSettings.Set("STREAMERBOT_CREDITS_FIELDS", JsonText(settings, "DonationFields"), true);
    }

    private JObject GoalTimerState()
    {
        JObject settings = OverlaySettingsForClient();
        decimal current = DecimalSetting("goal.current", JsonDecimal(settings, "GoalCurrent", 0));
        decimal target = DecimalSetting("goal.target", JsonDecimal(settings, "GoalTarget", 0));
        string currency = BridgeSettings.Get("goal.currency", JsonText(settings, "GoalCurrency"));
        decimal remaining = target > current ? target - current : 0;
        decimal percent = target > 0 ? Math.Min(100, Math.Round((current / target) * 100, 2)) : 0;

        JObject goal = new JObject();
        goal["enabled"] = BridgeSettings.GetBool("goal.enabled", JsonBool(settings, "GoalEnabled", true));
        goal["headerTitle"] = BridgeSettings.Get("goal.headerTitle", JsonText(settings, "GoalHeaderTitle"));
        goal["title"] = BridgeSettings.Get("goal.title", JsonText(settings, "GoalTitle"));
        goal["current"] = current.ToString(CultureInfo.InvariantCulture);
        goal["target"] = target.ToString(CultureInfo.InvariantCulture);
        goal["remaining"] = remaining.ToString(CultureInfo.InvariantCulture);
        goal["percent"] = percent.ToString("0.##", CultureInfo.InvariantCulture);
        goal["currency"] = currency;
        goal["currentText"] = FormatStateAmount(current, currency);
        goal["targetText"] = target > 0 ? FormatStateAmount(target, currency) : "";
        goal["remainingText"] = FormatStateAmount(remaining, currency);
        goal["percentText"] = percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";
        goal["summary"] = target > 0 ? goal["currentText"].ToString() + " / " + goal["targetText"].ToString() + " (" + goal["percentText"].ToString() + ")" : goal["currentText"].ToString();

        DateTime now = DateTime.UtcNow;
        string timerMode = BridgeSettings.Get("timer.mode", JsonText(settings, "TimerMode", "countdown"));
        DateTime endsAt = DateSetting("timer.endsAt", now);
        DateTime startedAt = DateSetting("timer.startedAt", now);
        double seconds = timerMode == "countup-reset" ? Math.Max(0, (now - startedAt).TotalSeconds) : Math.Max(0, (endsAt - now).TotalSeconds);
        JObject timer = new JObject();
        timer["enabled"] = BridgeSettings.GetBool("timer.enabled", JsonBool(settings, "TimerEnabled", false));
        timer["headerTitle"] = BridgeSettings.Get("timer.headerTitle", JsonText(settings, "TimerHeaderTitle"));
        timer["title"] = BridgeSettings.Get("timer.title", JsonText(settings, "TimerTitle"));
        timer["subtitle"] = BridgeSettings.Get("timer.subtitle", JsonText(settings, "TimerSubtitle"));
        timer["seconds"] = Math.Floor(seconds).ToString(CultureInfo.InvariantCulture);
        timer["text"] = FormatStateDuration(seconds);
        timer["endsAt"] = endsAt.ToString("o");
        timer["startedAt"] = startedAt.ToString("o");
        timer["mode"] = timerMode;
        timer["addedSeconds"] = "0";
        timer["addedText"] = "00:00:00";
        timer["conversionText"] = TimerConversionText(settings);
        timer["summary"] = timer["title"].ToString() + ": " + timer["text"].ToString();

        JObject last = new JObject();
        lock (StateLock)
        {
            JObject donation = LastDonation == null ? DonationToJson(DefaultDonation()) : LastDonation;
            last["user"] = JsonText(donation, "donor");
            last["amount"] = JsonText(donation, "amount");
            last["currency"] = JsonText(donation, "currency");
            last["platform"] = JsonText(donation, "provider");
        }

        JObject root = new JObject();
        root["updatedAt"] = DateTime.UtcNow.ToString("o");
        root["settings"] = settings;
        root["goal"] = goal;
        root["timer"] = timer;
        root["lastDonation"] = last;
        root["services"] = EnabledServiceNamesForWidget(settings);
        return root;
    }

    private JArray EnabledServiceNamesForWidget(JObject settings)
    {
        var services = new JArray();
        AddServiceName(services, settings, "DonationAlerts", "donationalerts.enabled");
        AddServiceName(services, settings, "StreamElements", "streamelements.enabled");
        AddServiceName(services, settings, "Streamlabs", "streamlabs.enabled");
        AddServiceName(services, settings, "DonatePay RU", "donatepayru.enabled");
        AddServiceName(services, settings, "DonatePay EU", "donatepayeu.enabled");
        AddServiceName(services, settings, "Donate.Stream", "donatestream.enabled");
        AddServiceName(services, settings, "deStream", "destream.enabled");
        AddServiceName(services, settings, "DonateX.gg", "donatex.enabled");
        AddServiceName(services, settings, "ODA", "oda.enabled");
        AddServiceName(services, settings, "Generic API", "generic.enabled");
        return services;
    }

    private void AddServiceName(JArray services, JObject settings, string name, string settingKey)
    {
        if (BridgeSettings.GetBool(settingKey, false) && !StringArrayContains(settings == null ? null : settings["HiddenServices"] as JArray, name))
            services.Add(name);
    }

    private void AddCreditDonation(JObject donation)
    {
        if (donation == null)
            return;

        var item = new JObject();
        item["name"] = JsonText(donation, "donor", "Anonymous");
        item["amount"] = JsonText(donation, "amount");
        item["currency"] = JsonText(donation, "currency");
        item["message"] = JsonText(donation, "message");
        item["platform"] = JsonText(donation, "provider", JsonText(donation, "source"));
        item["timestamp"] = DateTime.UtcNow.ToString("o");
        CreditItems.Add(item);
        while (CreditItems.Count > 120)
            CreditItems.RemoveAt(0);
        CreditsEventId++;
    }

    private void AddRecentDonation(JObject donation)
    {
        if (donation == null)
            return;

        RecentDonations.Insert(0, (JObject)donation.DeepClone());
        while (RecentDonations.Count > 10)
            RecentDonations.RemoveAt(RecentDonations.Count - 1);
    }

    private void AddLeaderboardDonation(JObject donation)
    {
        if (donation == null)
            return;

        var item = new JObject();
        item["id"] = Guid.NewGuid().ToString("N");
        item["name"] = JsonText(donation, "donor", "Anonymous");
        item["amount"] = JsonText(donation, "amount", "0");
        item["currency"] = JsonText(donation, "currency");
        item["platform"] = JsonText(donation, "provider", JsonText(donation, "source", "Donation"));
        item["timestamp"] = JsonText(donation, "timestamp", DateTime.UtcNow.ToString("o"));
        LeaderboardItems.Add(item);
        while (LeaderboardItems.Count > 500)
            LeaderboardItems.RemoveAt(0);
        LeaderboardEventId++;

        try
        {
            SaveLeaderboardData();
        }
        catch (Exception ex)
        {
            Logger.Warn("Leaderboard data were not saved. " + ex.Message);
        }
    }

    private JObject LeaderboardState()
    {
        JObject settings = LeaderboardSettingsForClient();
        int limit = ClampInt(JsonInt(settings, "TopCount", 5), 1, 10);
        var source = new List<JObject>();
        lock (StateLock)
        {
            foreach (JObject item in LeaderboardItems)
                source.Add((JObject)item.DeepClone());
        }

        if (source.Count == 0)
            source = SampleLeaderboardItems();

        var result = new JObject();
        result["eventId"] = LeaderboardEventId;
        result["settings"] = settings;
        result["overall"] = AggregateLeaderboard(source, "", limit);
        result["recent"] = RecentLeaderboard(source, limit);
        result["slides"] = PlatformLeaderboardSlides(source, limit);
        var data = new JArray();
        foreach (JObject item in source)
            data.Add(item.DeepClone());
        result["data"] = data;
        return result;
    }

    private JArray AggregateLeaderboard(List<JObject> items, string platformFilter, int limit)
    {
        var rows = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
        foreach (JObject item in items)
        {
            string platform = JsonText(item, "platform", "Donation");
            if (!string.IsNullOrWhiteSpace(platformFilter) && !platform.Equals(platformFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            string name = CleanDisplayName(JsonText(item, "name", "Anonymous"));
            string currency = JsonText(item, "currency");
            string key = name.Trim().ToLowerInvariant() + "|" + currency.Trim().ToUpperInvariant();
            JObject row;
            if (!rows.TryGetValue(key, out row))
            {
                row = new JObject();
                row["name"] = name;
                row["amount"] = 0m;
                row["currency"] = currency;
                row["platforms"] = new JArray();
                rows[key] = row;
            }

            decimal amount;
            if (!decimal.TryParse(JsonText(item, "amount", "0"), NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
                amount = 0;
            row["amount"] = JsonDecimal(row, "amount", 0) + amount;
            AddUniqueText((JArray)row["platforms"], platform);
        }

        var sorted = new List<JObject>();
        foreach (JObject row in rows.Values)
            sorted.Add(row);
        sorted.Sort((left, right) => JsonDecimal(right, "amount", 0).CompareTo(JsonDecimal(left, "amount", 0)));

        var result = new JArray();
        for (int i = 0; i < sorted.Count && i < limit; i++)
        {
            JObject row = sorted[i];
            row["rank"] = i + 1;
            result.Add(row);
        }
        return result;
    }

    private JArray RecentLeaderboard(List<JObject> items, int limit)
    {
        var result = new JArray();
        int rank = 1;
        for (int i = items.Count - 1; i >= 0 && result.Count < limit; i--)
        {
            JObject source = items[i];
            var row = new JObject();
            row["rank"] = rank++;
            row["name"] = CleanDisplayName(JsonText(source, "name", "Anonymous"));
            row["amount"] = JsonText(source, "amount", "0");
            row["currency"] = JsonText(source, "currency");
            var platforms = new JArray();
            AddUniqueText(platforms, JsonText(source, "platform", "Donation"));
            row["platforms"] = platforms;
            result.Add(row);
        }
        return result;
    }

    private JArray PlatformLeaderboardSlides(List<JObject> items, int limit)
    {
        var platforms = new List<string>();
        foreach (JObject item in items)
        {
            string platform = JsonText(item, "platform", "Donation");
            bool exists = false;
            foreach (string current in platforms)
                if (current.Equals(platform, StringComparison.OrdinalIgnoreCase))
                    exists = true;
            if (!exists)
                platforms.Add(platform);
        }

        var result = new JArray();
        foreach (string platform in platforms)
        {
            var slide = new JObject();
            slide["platform"] = platform;
            slide["items"] = AggregateLeaderboard(items, platform, limit);
            result.Add(slide);
        }
        return result;
    }

    private List<JObject> SampleLeaderboardItems()
    {
        return new List<JObject>
        {
            SampleLeaderboardItem("Alice", "1500", "RUB", "DonationAlerts"),
            SampleLeaderboardItem("Bob", "900", "RUB", "DonateX.gg"),
            SampleLeaderboardItem("Alice", "500", "RUB", "DonatePay RU"),
            SampleLeaderboardItem("Anonymous", "250", "RUB", "StreamElements")
        };
    }

    private JObject SampleLeaderboardItem(string name, string amount, string currency, string platform)
    {
        var item = new JObject();
        item["id"] = Guid.NewGuid().ToString("N");
        item["name"] = name;
        item["amount"] = amount;
        item["currency"] = currency;
        item["platform"] = platform;
        item["timestamp"] = DateTime.UtcNow.ToString("o");
        return item;
    }

    private void AddUniqueText(JArray items, string value)
    {
        foreach (JToken token in items)
            if (token.ToString().Equals(value, StringComparison.OrdinalIgnoreCase))
                return;
        items.Add(value);
    }

    private JArray SampleCredits()
    {
        var items = new JArray();
        items.Add(SampleCredit("Donations", "50", "RUB", "DonationAlerts", "Test donation"));
        items.Add(SampleCredit("Anonymous", "500", "RUB", "DonateX.gg", "Anonymous support"));
        items.Add(SampleCredit("Long Message", "250", "RUB", "Streamlabs", "A longer message for credits preview"));
        return items;
    }

    private JObject SampleCredit(string name, string amount, string currency, string platform, string message)
    {
        var item = new JObject();
        item["name"] = name;
        item["amount"] = amount;
        item["currency"] = currency;
        item["platform"] = platform;
        item["message"] = message;
        item["timestamp"] = DateTime.UtcNow.ToString("o");
        return item;
    }

    private void CopyJsonValues(JObject target, JObject source)
    {
        if (target == null || source == null)
            return;

        foreach (var property in source.Properties())
            target[property.Name] = property.Value == null ? null : property.Value.DeepClone();
    }

    private JArray NormalizeStringArray(JArray source, int limit)
    {
        var result = new JArray();
        if (source == null)
            return result;
        foreach (JToken token in source)
        {
            string value = token == null ? "" : token.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(value) && !StringArrayContains(result, value))
                result.Add(LimitText(value, 120));
            if (result.Count >= limit)
                break;
        }
        return result;
    }

    private bool StringArrayContains(JArray source, string value)
    {
        if (source == null)
            return false;
        foreach (JToken token in source)
            if (token != null && token.ToString().Equals(value ?? "", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private string LimitText(string value, int maxLength)
    {
        string text = value ?? "";
        return text.Length <= maxLength ? text : text.Substring(0, maxLength);
    }

    private int ParseDurationSeconds(string value, int fallback)
    {
        string raw = (value ?? "").Trim().TrimEnd('s', 'S');
        int seconds;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds) ? seconds : fallback;
    }

    private string TimerConversionText(JObject settings)
    {
        decimal amount = JsonDecimal(settings, "TimerUnitAmount", 100);
        decimal seconds = JsonDecimal(settings, "TimerSecondsPerUnit", 60);
        string currency = JsonText(settings, "TimerCurrency", "RUB");
        int minutes = (int)Math.Floor(seconds / 60);
        string duration = minutes > 0 && seconds % 60 == 0 ? minutes.ToString(CultureInfo.InvariantCulture) + " min" : seconds.ToString("0.##", CultureInfo.InvariantCulture) + " sec";
        return amount.ToString("0.##", CultureInfo.InvariantCulture) + " " + currency + " = " + duration;
    }

    private string NormalizeChoice(string value, string fallback, params string[] allowed)
    {
        string normalized = (value ?? "").Trim().ToLowerInvariant();
        foreach (string item in allowed)
            if (normalized == item)
                return item;
        return fallback;
    }

    private int ClampInt(int value, int min, int max)
    {
        return Math.Min(max, Math.Max(min, value));
    }

    private double ClampDouble(double value, double min, double max)
    {
        return Math.Min(max, Math.Max(min, value));
    }

    private int JsonInt(JObject json, string name, int fallback)
    {
        int value;
        return json != null && json[name] != null && int.TryParse(json[name].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
    }

    private double JsonDouble(JObject json, string name, double fallback)
    {
        double value;
        return json != null && json[name] != null && double.TryParse(json[name].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : fallback;
    }

    private decimal JsonDecimal(JObject json, string name, decimal fallback)
    {
        decimal value;
        return json != null && json[name] != null && decimal.TryParse(json[name].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : fallback;
    }

    private bool JsonBool(JObject json, string name, bool fallback)
    {
        if (json == null || json[name] == null)
            return fallback;
        bool value;
        if (bool.TryParse(json[name].ToString(), out value))
            return value;
        string raw = json[name].ToString();
        return raw == "1" || raw.Equals("yes", StringComparison.OrdinalIgnoreCase) || raw.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private string JsonText(JObject json, string name)
    {
        return JsonText(json, name, "");
    }

    private string JsonText(JObject json, string name, string fallback)
    {
        if (json == null || json[name] == null)
            return fallback;
        string value = json[name].ToString();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private decimal DecimalSetting(string key, decimal fallback)
    {
        decimal value;
        return decimal.TryParse(BridgeSettings.Get(key, fallback.ToString(CultureInfo.InvariantCulture)), NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : fallback;
    }

    private DateTime DateSetting(string key, DateTime fallback)
    {
        DateTime value;
        return DateTime.TryParse(BridgeSettings.Get(key, fallback.ToString("o")), null, DateTimeStyles.RoundtripKind, out value) ? value.ToUniversalTime() : fallback;
    }

    private string FormatStateAmount(decimal amount, string currency)
    {
        string text = amount.ToString("0.##", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(currency) ? text : text + " " + currency;
    }

    private string FormatStateDuration(double totalSeconds)
    {
        int seconds = (int)Math.Floor(Math.Max(0, totalSeconds));
        int hours = seconds / 3600;
        int minutes = (seconds % 3600) / 60;
        int secs = seconds % 60;
        return hours.ToString("00", CultureInfo.InvariantCulture) + ":" + minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + secs.ToString("00", CultureInfo.InvariantCulture);
    }

    private void ApplyTestDonationToGoalTimer(UnifiedDonationEvent donation)
    {
        if (donation == null)
            return;

        if (BridgeSettings.GetBool("goal.enabled", JsonBool(OverlaySettings, "GoalEnabled", true)))
        {
            decimal current = DecimalSetting("goal.current", JsonDecimal(OverlaySettings, "GoalCurrent", 0));
            current += donation.Amount;
            BridgeSettings.Set("goal.current", current.ToString(CultureInfo.InvariantCulture), true);
            if (!string.IsNullOrWhiteSpace(donation.Currency))
                BridgeSettings.Set("goal.currency", donation.Currency, true);
        }

        if (BridgeSettings.GetBool("timer.enabled", JsonBool(OverlaySettings, "TimerEnabled", false)))
        {
            decimal unitAmount = DecimalSetting("timer.unitAmount", JsonDecimal(OverlaySettings, "TimerUnitAmount", 100));
            decimal secondsPerUnit = DecimalSetting("timer.secondsPerUnit", JsonDecimal(OverlaySettings, "TimerSecondsPerUnit", 60));
            DateTime now = DateTime.UtcNow;
            DateTime endsAt = DateSetting("timer.endsAt", now);
            if (endsAt < now)
                endsAt = now;

            if (unitAmount > 0 && secondsPerUnit > 0)
                endsAt = endsAt.AddSeconds((double)(donation.Amount / unitAmount * secondsPerUnit));

            decimal maxSeconds = DecimalSetting("timer.maxSeconds", JsonDecimal(OverlaySettings, "TimerMaxSeconds", 0));
            if (maxSeconds > 0 && (endsAt - now).TotalSeconds > (double)maxSeconds)
                endsAt = now.AddSeconds((double)maxSeconds);

            BridgeSettings.Set("timer.endsAt", endsAt.ToString("o"), true);
            if (BridgeSettings.Get("timer.mode", JsonText(OverlaySettings, "TimerMode", "countdown")) == "countup-reset")
                BridgeSettings.Set("timer.startedAt", now.ToString("o"), true);
        }
    }

    private void ApplyTestDonationToTimerOnly(UnifiedDonationEvent donation)
    {
        if (donation == null)
            return;

        if (!BridgeSettings.GetBool("timer.enabled", JsonBool(OverlaySettings, "TimerEnabled", false)))
            return;

        decimal unitAmount = DecimalSetting("timer.unitAmount", JsonDecimal(OverlaySettings, "TimerUnitAmount", 100));
        decimal secondsPerUnit = DecimalSetting("timer.secondsPerUnit", JsonDecimal(OverlaySettings, "TimerSecondsPerUnit", 60));
        DateTime now = DateTime.UtcNow;
        DateTime endsAt = DateSetting("timer.endsAt", now);
        DateTime startedAt = DateSetting("timer.startedAt", now);
        string timerMode = BridgeSettings.Get("timer.mode", JsonText(OverlaySettings, "TimerMode", "countdown"));

        if (endsAt < now)
            endsAt = now;

        decimal addedSeconds = 0;
        if (unitAmount > 0 && secondsPerUnit > 0 && donation.Amount > 0)
            addedSeconds = donation.Amount / unitAmount * secondsPerUnit;

        if (timerMode.Equals("countup-reset", StringComparison.OrdinalIgnoreCase))
            startedAt = now;
        else if (addedSeconds > 0)
            endsAt = endsAt.AddSeconds((double)addedSeconds);

        decimal maxSeconds = DecimalSetting("timer.maxSeconds", JsonDecimal(OverlaySettings, "TimerMaxSeconds", 0));
        if (maxSeconds > 0 && (endsAt - now).TotalSeconds > (double)maxSeconds)
            endsAt = now.AddSeconds((double)maxSeconds);

        BridgeSettings.Set("timer.endsAt", endsAt.ToString("o"), true);
        BridgeSettings.Set("timer.startedAt", startedAt.ToString("o"), true);
    }

    private UnifiedDonationEvent CreateTestDonation(string kind)
    {
        string mode = (kind ?? "").Trim().ToLowerInvariant();
        var e = DefaultDonation();
        if (mode == "500")
        {
            e.Amount = 500;
            e.Message = "\u041a\u0440\u0443\u043f\u043d\u044b\u0439 \u0442\u0435\u0441\u0442\u043e\u0432\u044b\u0439 \u0434\u043e\u043d\u0430\u0442.";
        }
        else if (mode == "long")
        {
            e.Amount = 250;
            e.Message = "\u041e\u0447\u0435\u043d\u044c \u0434\u043b\u0438\u043d\u043d\u043e\u0435 \u0441\u043e\u043e\u0431\u0449\u0435\u043d\u0438\u0435 \u0434\u043b\u044f \u043f\u0440\u043e\u0432\u0435\u0440\u043a\u0438 \u043f\u0435\u0440\u0435\u043d\u043e\u0441\u043e\u0432, \u0432\u044b\u0441\u043e\u0442\u044b \u0431\u043b\u043e\u043a\u0430 \u0438 \u043e\u0431\u0449\u0435\u0433\u043e \u0432\u0438\u0434\u0430 \u0432 OBS.";
        }
        else if (mode == "anonymous")
        {
            e.UserName = "Anonymous";
            e.Amount = 100;
            e.Message = "\u0410\u043d\u043e\u043d\u0438\u043c\u043d\u044b\u0439 \u0442\u0435\u0441\u0442\u043e\u0432\u044b\u0439 \u0434\u043e\u043d\u0430\u0442.";
            e.IsAnonymous = true;
        }
        else
        {
            e.Amount = 50;
        }

        e.DonationId = "widget-test-" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
        e.Timestamp = DateTime.UtcNow;
        return e;
    }

    private void ApplyCustomTestDonation(UnifiedDonationEvent donation, JObject custom)
    {
        if (donation == null || custom == null)
            return;

        donation.UserName = LimitText(JsonText(custom, "donor", donation.UserName), 120);
        donation.Message = LimitText(JsonText(custom, "message", donation.Message), 800);
        donation.Currency = LimitText(JsonText(custom, "currency", donation.Currency), 16);
        donation.ProviderName = LimitText(JsonText(custom, "platform", donation.ProviderName), 80);
        donation.Source = donation.ProviderName;
        decimal amount = JsonDecimal(custom, "amount", donation.Amount);
        donation.Amount = Math.Max(0, amount);
        donation.IsAnonymous = JsonBool(custom, "isAnonymous", false);
    }

    private UnifiedDonationEvent DefaultDonation()
    {
        return new UnifiedDonationEvent
        {
            Source = "Widget Test",
            ProviderName = "Widget Test",
            EventType = "donation",
            DonationId = "widget-test",
            UserName = "\u0422\u0435\u0441\u0442\u043e\u0432\u044b\u0439 \u0434\u043e\u043d\u0430\u0442\u0435\u0440",
            Amount = 50,
            Currency = "RUB",
            Message = "\u042d\u0442\u043e live preview DonConnect.",
            Timestamp = DateTime.UtcNow,
            IsAnonymous = false,
            RawJson = "{\"source\":\"Widget Test\"}"
        };
    }

    private JObject DonationToJson(UnifiedDonationEvent e)
    {
        var json = new JObject();
        json["source"] = e.Source ?? e.ProviderName ?? "";
        json["provider"] = e.ProviderName ?? "";
        json["donor"] = CleanDisplayName(string.IsNullOrWhiteSpace(e.UserName) ? "Anonymous" : e.UserName);
        json["amount"] = e.Amount.ToString(CultureInfo.InvariantCulture);
        json["currency"] = e.Currency ?? "";
        json["message"] = CleanDisplayText(e.Message ?? "");
        json["id"] = e.DonationId ?? "";
        json["timestamp"] = e.Timestamp.ToUniversalTime().ToString("o");
        json["isAnonymous"] = e.IsAnonymous;
        return json;
    }

    private string CleanDisplayName(string value)
    {
        string text = string.IsNullOrWhiteSpace(value) ? "Anonymous" : value.Trim();
        foreach (string blocked in SplitLines(JsonText(ContentFilterSettings, "BlockedNames")))
            if (text.Equals(blocked, StringComparison.OrdinalIgnoreCase) || text.IndexOf(blocked, StringComparison.OrdinalIgnoreCase) >= 0)
                return JsonText(ContentFilterSettings, "ReplacementName", "Anonymous");
        foreach (string blocked in SplitLines(JsonText(ContentFilterSettings, "BlockedWords")))
            if (text.IndexOf(blocked, StringComparison.OrdinalIgnoreCase) >= 0)
                return JsonText(ContentFilterSettings, "ReplacementName", "Anonymous");
        return LimitText(text, 120);
    }

    private string CleanDisplayText(string value)
    {
        string text = value ?? "";
        string replacement = JsonText(ContentFilterSettings, "ReplacementText", "[hidden]");
        foreach (string blocked in SplitLines(JsonText(ContentFilterSettings, "BlockedWords")))
            text = ReplaceInsensitive(text, blocked, replacement);
        return LimitText(text, 800);
    }

    private List<string> SplitLines(string raw)
    {
        var result = new List<string>();
        foreach (string part in (raw ?? "").Replace("\r", "\n").Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string value = part.Trim();
            if (!string.IsNullOrWhiteSpace(value) && !result.Contains(value))
                result.Add(value);
        }
        return result;
    }

    private string ReplaceInsensitive(string source, string search, string replacement)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(search))
            return source ?? "";
        int index = 0;
        var result = new StringBuilder();
        while (index < source.Length)
        {
            int match = source.IndexOf(search, index, StringComparison.OrdinalIgnoreCase);
            if (match < 0)
            {
                result.Append(source.Substring(index));
                break;
            }
            result.Append(source.Substring(index, match - index));
            result.Append(replacement ?? "");
            index = match + search.Length;
        }
        return result.ToString();
    }

    private string EditorHtml()
    {
        return @"<!doctype html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>DonConnect Editors</title>
  <style>
    * { box-sizing: border-box; }
    body { margin:0; height:100vh; overflow:hidden; background:#f4f6f8; color:#17202a; font:15px/1.45 Segoe UI, Arial, sans-serif; }
    header { min-height:58px; display:flex; align-items:center; justify-content:space-between; gap:14px; padding:10px 22px; background:#111820; color:#fff; }
    header h1 { margin:0; font-size:18px; letter-spacing:0; }
    .language { display:flex; align-items:center; gap:8px; margin:0; color:#fff; font-weight:800; }
    .language select { min-width:138px; border:1px solid rgba(255,255,255,.28); border-radius:6px; padding:7px 9px; background:#19232d; color:#fff; font:inherit; }
    main { display:grid; grid-template-columns:minmax(360px, 460px) 1fr; height:calc(100vh - 58px); min-height:0; }
    .controls { min-height:0; overflow:auto; padding:18px; background:#fff; border-right:1px solid #dde3ea; }
    .preview { min-width:0; min-height:0; overflow:hidden; display:grid; grid-template-rows:auto 1fr; gap:12px; padding:20px; background:#20242b; }
    .preview-head { display:flex; align-items:center; gap:10px; color:#fff; font-weight:800; }
    .preview-head input { display:none; }
    .tabs { display:flex; flex-wrap:wrap; gap:8px; margin-bottom:14px; }
    button { border:1px solid #b9c4cf; background:#fff; color:#17202a; border-radius:7px; padding:8px 11px; font:inherit; font-weight:800; cursor:pointer; }
    button.tab { background:#eef3f8; }
    button.tab.active { background:#111820; border-color:#111820; color:#fff; }
    button.primary { background:#1f7a4d; border-color:#1f7a4d; color:#fff; }
    button.warn { background:#fff4e5; border-color:#e1b15e; }
    button.danger { background:#b91c1c; border-color:#b91c1c; color:#fff; }
    button.danger:hover { background:#991b1b; border-color:#991b1b; }
    .pane { display:none; }
    .pane.active { display:block; }
    fieldset { border:1px solid #d9e0e7; border-left:4px solid #4d7f71; border-radius:8px; margin:0 0 10px; padding:11px 12px; background:#fbfcfd; }
    fieldset.collapsed { padding:9px 12px; }
    fieldset.collapsed > :not(legend) { display:none; }
    legend { width:100%; padding:0 6px; font-weight:800; color:#25313d; cursor:pointer; user-select:none; display:flex; align-items:center; justify-content:space-between; gap:10px; }
    legend::after { content:""\25BC""; color:#1f7a4d; font-size:20px; line-height:1; font-weight:900; }
    fieldset.collapsed legend::after { content:""\25B6""; }
    label { display:grid; gap:5px; margin:0 0 9px; font-weight:700; }
    input[type=""range""] { width:100%; }
    input[type=""number""], input[type=""text""], select, textarea { width:100%; border:1px solid #cbd5df; border-radius:6px; padding:8px 10px; font:inherit; background:#fff; }
    input[type=""color""] { width:44px; height:34px; border:0; background:transparent; padding:0; }
    input[type=""checkbox""] { width:18px; height:18px; }
    .row { display:grid; grid-template-columns:1fr 72px; gap:10px; align-items:center; }
    .color-row { display:flex; align-items:center; justify-content:space-between; gap:12px; }
    .check-row { display:flex; align-items:center; gap:10px; }
    .drop { border:2px dashed #b9c4cf; border-radius:8px; padding:18px; text-align:center; color:#536170; font-weight:800; background:#f7fafc; cursor:pointer; margin:0 0 10px; }
    .drop.drag { border-color:#1f7a4d; background:#ecfdf3; color:#1f7a4d; }
    .image-name { color:#536170; font-size:13px; font-weight:800; min-height:18px; margin-bottom:10px; word-break:break-word; }
    .folder-path { color:#536170; font-size:12px; word-break:break-all; margin:7px 0 10px; }
    .media-grid { display:grid; gap:6px; margin-top:10px; }
    .media-item { display:grid; grid-template-columns:1fr auto; gap:8px; align-items:center; border:1px solid #d9e0e7; border-radius:6px; padding:7px 8px; background:#fff; }
    .media-item small { color:#536170; display:block; }
    .media-item { grid-template-columns:44px minmax(0,1fr) auto; }
    .media-thumb { width:40px; height:40px; display:grid; place-items:center; overflow:hidden; border-radius:5px; background:#eef3f8; color:#536170; font-size:11px; font-weight:900; }
    .media-thumb img { width:100%; height:100%; object-fit:cover; }
    .media-actions { display:flex; flex-wrap:wrap; justify-content:flex-end; gap:5px; }
    .media-actions button { padding:5px 7px; font-size:12px; }
    .note { color:#536170; font-size:12px; margin:0 0 10px; }
    .compact-grid { display:grid; grid-template-columns:1fr 1fr; gap:8px; }
    .compact-grid label { margin:0; }
    .rule-card { border:1px solid #d9e0e7; border-radius:7px; padding:10px; margin:0 0 9px; background:#f9fbfd; }
    .rule-card select[multiple] { min-height:92px; }
    .rule-range { display:grid; grid-template-columns:1fr 1fr; gap:8px; }
    .presets, .buttons { display:flex; flex-wrap:wrap; gap:8px; }
    .status { color:#536170; min-height:22px; margin-top:8px; font-weight:800; }
    .hidden { display:none !important; }
    .help { margin-top:14px; padding:14px; border:1px solid #d9e0e7; border-radius:8px; background:#f7fafc; color:#25313d; }
    .help h2 { margin:0 0 8px; font-size:16px; }
    .help ol { margin:0 0 12px 20px; padding:0; }
    .obs-url { display:grid; grid-template-columns:1fr auto; gap:8px; align-items:center; }
    .obs-url input { width:100%; border:1px solid #cbd5df; border-radius:6px; padding:8px 10px; font:inherit; background:#fff; }
    iframe { width:100%; height:100%; min-height:0; border:1px solid rgba(255,255,255,.14); border-radius:8px; background:transparent; }
    @media (max-width:900px) { body { overflow:auto; } main { height:auto; grid-template-columns:1fr; } .preview { min-height:520px; } iframe { min-height:480px; } }
  </style>
</head>
<body>
  <header><h1>DonConnect Editors</h1><label class=""language""><span id=""languageLabel"">Language</span><select id=""languageSelect""><option value=""en"">English</option><option value=""ru"">&#1056;&#1091;&#1089;&#1089;&#1082;&#1080;&#1081;</option><option value=""uk"">&#1059;&#1082;&#1088;&#1072;&#1111;&#1085;&#1089;&#1100;&#1082;&#1072;</option></select></label></header>  <main>
    <section class=""controls"">
      <nav class=""tabs""><button class=""tab active"" data-tab=""donation"">Donation</button><button class=""tab"" data-tab=""history"">OBS Dock</button><button class=""tab"" data-tab=""goal"">Goal</button><button class=""tab"" data-tab=""timer"">Timer</button><button class=""tab"" data-tab=""credits"">Credits</button><button class=""tab"" data-tab=""leaderboard"">Leaderboard</button><button class=""tab"" data-tab=""filter"">Blocked</button></nav>

      <div class=""pane active"" data-pane=""donation"">
        <fieldset><legend>Donation presets</legend><div class=""presets"" id=""donationPresets""></div></fieldset>
        <fieldset><legend>Donation size</legend>
          <label><span>Width</span><div class=""row""><input data-donation=""Width"" type=""range"" min=""240"" max=""1920""><input data-donation=""Width"" type=""number""></div></label>
          <label><span>Height</span><div class=""row""><input data-donation=""Height"" type=""range"" min=""90"" max=""1080""><input data-donation=""Height"" type=""number""></div></label>
          <label><span>Border radius</span><div class=""row""><input data-donation=""BorderRadius"" type=""range"" min=""0"" max=""80""><input data-donation=""BorderRadius"" type=""number""></div></label>
          <label><span>Padding</span><div class=""row""><input data-donation=""Padding"" type=""range"" min=""0"" max=""80""><input data-donation=""Padding"" type=""number""></div></label>
          <label><span>Font size</span><div class=""row""><input data-donation=""FontSize"" type=""range"" min=""10"" max=""72""><input data-donation=""FontSize"" type=""number""></div></label>
          <label><span>Base font</span><select data-donation=""FontFamily"" data-font-select></select></label>
          <label><span>Opacity</span><div class=""row""><input data-donation=""Opacity"" type=""range"" min=""0.1"" max=""1"" step=""0.01""><input data-donation=""Opacity"" type=""number"" min=""0.1"" max=""1"" step=""0.01""></div></label>
          <label><span>Animation, ms</span><div class=""row""><input data-donation=""AnimationDuration"" type=""range"" min=""0"" max=""5000"" step=""50""><input data-donation=""AnimationDuration"" type=""number""></div></label>
        </fieldset>
        <fieldset><legend>Donation colors</legend><label class=""color-row""><span>Background</span><input data-donation=""BackgroundColor"" type=""color""></label><label class=""color-row""><span>Text</span><input data-donation=""TextColor"" type=""color""></label><label class=""color-row""><span>Accent</span><input data-donation=""AccentColor"" type=""color""></label></fieldset>
        <fieldset><legend>Donation templates</legend><label><span>Text align</span><select data-donation=""TextAlign""><option value=""left"">Left</option><option value=""center"">Center</option><option value=""right"">Right</option></select></label><label><span>Donor</span><input data-donation=""DonorTemplate"" type=""text""></label><label><span>Donor font</span><select data-donation=""DonorFontFamily"" data-font-select></select></label><div class=""compact-grid""><label><span>Donor X</span><input data-donation=""DonorX"" type=""number""></label><label><span>Donor Y</span><input data-donation=""DonorY"" type=""number""></label></div><label><span>Amount</span><input data-donation=""AmountTemplate"" type=""text""></label><label><span>Amount font</span><select data-donation=""AmountFontFamily"" data-font-select></select></label><div class=""compact-grid""><label><span>Amount X</span><input data-donation=""AmountX"" type=""number""></label><label><span>Amount Y</span><input data-donation=""AmountY"" type=""number""></label></div><label><span>Message</span><input data-donation=""MessageTemplate"" type=""text""></label><label><span>Message font</span><select data-donation=""MessageFontFamily"" data-font-select></select></label><div class=""compact-grid""><label><span>Message X</span><input data-donation=""MessageX"" type=""number""></label><label><span>Message Y</span><input data-donation=""MessageY"" type=""number""></label></div><label class=""check-row""><input data-donation=""ShowPlatform"" type=""checkbox""><span>Show platform</span></label><label><span>Platform</span><input data-donation=""PlatformTemplate"" type=""text""></label><label><span>Platform font</span><select data-donation=""PlatformFontFamily"" data-font-select></select></label></fieldset>
        <fieldset><legend>Alert media library</legend><div class=""drop"" id=""alertMediaDrop"">Drop PNG/JPG/GIF/MP4/WebM/MP3/WAV here or click</div><input id=""alertMediaFile"" type=""file"" accept=""image/png,image/jpeg,image/webp,image/gif,video/mp4,video/webm,audio/mpeg,audio/wav,audio/ogg,audio/mp4"" multiple hidden><div class=""buttons""><button type=""button"" id=""openAlertMedia"">Open media folder</button></div><div class=""folder-path"" id=""alertMediaPath""></div><label><span>Default visual</span><select data-donation=""MediaFile"" id=""alertMediaSelect""></select></label><label><span>Alert sound</span><select data-donation=""SoundFile"" id=""alertSoundSelect""></select></label><label><span>Text animation sound</span><select data-donation=""TextSoundFile"" id=""alertTextSoundSelect""></select></label><label><span>Visual placement</span><select data-donation=""MediaPlacement""><option value=""above"">Above text</option><option value=""below"">Below text</option><option value=""left"">Left of text</option><option value=""right"">Right of text</option><option value=""background"">Behind text</option></select></label><label><span>Visual fit</span><select data-donation=""MediaFit""><option value=""contain"">Contain</option><option value=""cover"">Cover</option></select></label><label><span>Visual width</span><div class=""row""><input data-donation=""MediaWidth"" type=""range"" min=""20"" max=""1600""><input data-donation=""MediaWidth"" type=""number""></div></label><label><span>Visual height</span><div class=""row""><input data-donation=""MediaHeight"" type=""range"" min=""20"" max=""1000""><input data-donation=""MediaHeight"" type=""number""></div></label><label><span>Visual X</span><div class=""row""><input data-donation=""MediaX"" type=""range"" min=""-800"" max=""800""><input data-donation=""MediaX"" type=""number""></div></label><label><span>Visual Y</span><div class=""row""><input data-donation=""MediaY"" type=""range"" min=""-600"" max=""600""><input data-donation=""MediaY"" type=""number""></div></label><label class=""check-row""><input data-donation=""VideoMuted"" type=""checkbox""><span>Mute video audio</span></label><div class=""media-grid"" id=""alertMediaGrid""></div></fieldset>
        <fieldset><legend>Amount rules</legend><p class=""folder-path"">The most specific matching minimum amount wins. Select several files to randomize them.</p><div id=""alertRules""></div><button type=""button"" id=""addAlertRule"">Add amount rule</button></fieldset>
        <fieldset><legend>Alert animations</legend><label class=""check-row""><input data-donation=""ShowBackground"" type=""checkbox""><span>Show background</span></label><label><span>Visible duration, ms</span><input data-donation=""DisplayDuration"" type=""number"" min=""500"" max=""60000"" step=""100""></label><label><span>Entry animation</span><select data-donation=""EntryAnimation""><option value=""none"">None</option><option value=""fade"">Fade</option><option value=""slide-left"">Slide from left</option><option value=""slide-right"">Slide from right</option><option value=""slide-up"">Slide from bottom</option><option value=""slide-down"">Slide from top</option><option value=""zoom"">Zoom</option></select></label><label><span>Exit animation</span><select data-donation=""ExitAnimation""><option value=""none"">None</option><option value=""fade"">Fade</option><option value=""slide-left"">Slide left</option><option value=""slide-right"">Slide right</option><option value=""slide-up"">Slide up</option><option value=""slide-down"">Slide down</option><option value=""zoom"">Zoom</option><option value=""scatter"">Scatter</option></select></label><label><span>Donor text animation</span><select data-donation=""TextAnimation""><option value=""none"">None</option><option value=""fade"">Fade</option><option value=""typewriter"">Typewriter</option><option value=""reveal-left"">Reveal left to right</option><option value=""slide-up"">Slide up</option></select></label><label><span>Alert volume</span><div class=""row""><input data-donation=""SoundVolume"" type=""range"" min=""0"" max=""100""><input data-donation=""SoundVolume"" type=""number""></div></label><label><span>Text sound volume</span><div class=""row""><input data-donation=""TextSoundVolume"" type=""range"" min=""0"" max=""100""><input data-donation=""TextSoundVolume"" type=""number""></div></label></fieldset>
        <fieldset><legend>Donation voice</legend><p class=""note"" id=""speechHint"">Windows voice reads the donation from the extension itself. Use Test voice after choosing a voice.</p><label class=""check-row""><input data-donation=""SpeakDonation"" type=""checkbox""><span>Read donation text aloud</span></label><div class=""compact-grid""><label class=""check-row""><input data-donation=""SpeechReadDonor"" type=""checkbox""><span>Read donor name</span></label><label class=""check-row""><input data-donation=""SpeechReadAmount"" type=""checkbox""><span>Read amount</span></label><label class=""check-row""><input data-donation=""SpeechReadPlatform"" type=""checkbox""><span>Read platform</span></label><label class=""check-row""><input data-donation=""SpeechReadMessage"" type=""checkbox""><span>Read message</span></label></div><label><span>Voice</span><select data-donation=""SpeechVoice"" id=""speechVoiceSelect""></select></label><label><span>Voice speed</span><div class=""row""><input data-donation=""SpeechRate"" type=""range"" min=""0.5"" max=""2"" step=""0.05""><input data-donation=""SpeechRate"" type=""number"" min=""0.5"" max=""2"" step=""0.05""></div></label><label><span>Voice pitch</span><div class=""row""><input data-donation=""SpeechPitch"" type=""range"" min=""0.5"" max=""2"" step=""0.05""><input data-donation=""SpeechPitch"" type=""number"" min=""0.5"" max=""2"" step=""0.05""></div></label><label><span>Voice volume</span><div class=""row""><input data-donation=""SpeechVolume"" type=""range"" min=""0"" max=""100""><input data-donation=""SpeechVolume"" type=""number""></div></label><button type=""button"" id=""testSpeech"">Test voice</button></fieldset>
        <fieldset><legend>Custom test alert</legend><label><span>Donor</span><input id=""customTestDonor"" type=""text"" value=""Custom viewer""></label><label><span>Amount</span><input id=""customTestAmount"" type=""number"" value=""777"" step=""0.01""></label><label><span>Currency</span><input id=""customTestCurrency"" type=""text"" value=""RUB""></label><label><span>Platform</span><input id=""customTestPlatform"" type=""text"" value=""Custom test""></label><label><span>Message</span><textarea id=""customTestMessage"" rows=""3"">Custom alert preview</textarea></label><button type=""button"" id=""sendCustomTest"">Send custom alert</button></fieldset>
      </div>

      <div class=""pane"" data-pane=""history"">
        <fieldset><legend>OBS Dock</legend><p class=""note"">Compact OBS dock with recent donations. Replay only repeats the alert and does not add money to goal, timer, credits or leaderboard.</p><div class=""obs-url""><input id=""dockUrlInline"" type=""text"" readonly><button type=""button"" id=""copyDockUrl"">Copy</button></div><div class=""media-grid"" id=""recentDonations""></div></fieldset>
      </div>

      <div class=""pane"" data-pane=""goal"">
        <fieldset><legend>Goal presets</legend><div class=""presets"" id=""goalPresets""></div></fieldset>
        <fieldset><legend>Goal editor</legend><label class=""check-row""><input data-overlay=""GoalEnabled"" type=""checkbox""><span>Enable goal</span></label><label><span>Base font</span><select data-overlay=""FontFamily"" data-font-select></select></label><label><span>Title 1</span><input data-overlay=""GoalHeaderTitle"" type=""text""></label><label><span>Title 1 font</span><select data-overlay=""GoalHeaderFontFamily"" data-font-select></select></label><label><span>Title 2</span><input data-overlay=""GoalTitle"" type=""text""></label><label><span>Title 2 font</span><select data-overlay=""GoalTitleFontFamily"" data-font-select></select></label><label><span>Current</span><input data-overlay=""GoalCurrent"" type=""number"" step=""0.01""></label><label><span>Target</span><input data-overlay=""GoalTarget"" type=""number"" step=""0.01""></label><label><span>Currency</span><input data-overlay=""GoalCurrency"" type=""text""></label><label><span>Format</span><select data-overlay=""GoalFormat""><option value=""amount"">Amount</option><option value=""percent"">Percent</option><option value=""summary"">Summary</option></select></label></fieldset>
        <fieldset><legend>Goal text</legend><label class=""check-row""><input data-overlay=""ShowGoalText"" type=""checkbox""><span>Show goal text</span></label><label class=""check-row""><input data-overlay=""ShowGoalValue"" type=""checkbox""><span>Show amount text</span></label><label class=""check-row""><input data-overlay=""ShowGoalMeta"" type=""checkbox""><span>Show progress number</span></label><label><span>Goal amount font</span><select data-overlay=""GoalValueFontFamily"" data-font-select></select></label><label><span>Title size</span><div class=""row""><input data-overlay=""TitleSize"" type=""range"" min=""10"" max=""96""><input data-overlay=""TitleSize"" type=""number""></div></label><label><span>Value size</span><div class=""row""><input data-overlay=""ValueSize"" type=""range"" min=""12"" max=""140""><input data-overlay=""ValueSize"" type=""number""></div></label><label><span>Text placement</span><select data-overlay=""GoalTextPlacement""><option value=""inside"">Inside bar</option><option value=""above"">Above bar</option><option value=""below"">Below bar</option></select></label><label><span>Text align</span><select data-overlay=""GoalTextAlign""><option value=""left"">Left</option><option value=""center"">Center</option><option value=""right"">Right</option></select></label><label><span>Text X</span><div class=""row""><input data-overlay=""GoalTextOffsetX"" type=""range"" min=""-300"" max=""300""><input data-overlay=""GoalTextOffsetX"" type=""number""></div></label><label><span>Text Y</span><div class=""row""><input data-overlay=""GoalTextOffsetY"" type=""range"" min=""-160"" max=""160""><input data-overlay=""GoalTextOffsetY"" type=""number""></div></label></fieldset>
        <fieldset><legend>Goal bar</legend><label><span>Visual type</span><select data-overlay=""GoalBarVisualMode""><option value=""bar"">Bar</option><option value=""image-reveal"">Image: grayscale reveal</option><option value=""image-silhouette"">Image: silhouette reveal</option><option value=""image-transparent"">Image: transparent reveal</option><option value=""image-inverse"">Image: disappear on progress</option></select></label><label><span>Fill direction</span><select data-overlay=""GoalFillDirection""><option value=""horizontal"">Horizontal</option><option value=""vertical"">Vertical</option></select></label><label class=""check-row""><input data-overlay=""ShowGoalBar"" type=""checkbox""><span>Show bar</span></label><label class=""check-row""><input data-overlay=""ShowGoalProgress"" type=""checkbox""><span>Show progress fill</span></label><label class=""check-row""><input data-overlay=""ShowPanelBackground"" type=""checkbox""><span>Show background</span></label><label><span>Panel width</span><div class=""row""><input data-overlay=""Width"" type=""range"" min=""240"" max=""1920""><input data-overlay=""Width"" type=""number""></div></label><label><span>Bar length</span><div class=""row""><input data-overlay=""GoalBarLength"" type=""range"" min=""40"" max=""1600""><input data-overlay=""GoalBarLength"" type=""number""></div></label><label><span>Bar height</span><div class=""row""><input data-overlay=""GoalBarHeight"" type=""range"" min=""6"" max=""240""><input data-overlay=""GoalBarHeight"" type=""number""></div></label><label><span>Bar align</span><select data-overlay=""GoalBarAlign""><option value=""left"">Left</option><option value=""center"">Center</option><option value=""right"">Right</option></select></label><label><span>Box radius</span><div class=""row""><input data-overlay=""BorderRadius"" type=""range"" min=""0"" max=""120""><input data-overlay=""BorderRadius"" type=""number""></div></label><label><span>Bar radius</span><div class=""row""><input data-overlay=""BarRadius"" type=""range"" min=""0"" max=""120""><input data-overlay=""BarRadius"" type=""number""></div></label><label><span>Padding</span><div class=""row""><input data-overlay=""Padding"" type=""range"" min=""0"" max=""120""><input data-overlay=""Padding"" type=""number""></div></label><label><span>Background opacity</span><div class=""row""><input data-overlay=""ContainerOpacity"" type=""range"" min=""0"" max=""1"" step=""0.01""><input data-overlay=""ContainerOpacity"" type=""number"" min=""0"" max=""1"" step=""0.01""></div></label><label><span>Bar opacity</span><div class=""row""><input data-overlay=""BarOpacity"" type=""range"" min=""0"" max=""1"" step=""0.01""><input data-overlay=""BarOpacity"" type=""number"" min=""0"" max=""1"" step=""0.01""></div></label><label class=""color-row""><span>Background</span><input data-overlay=""BackgroundColor"" type=""color""></label><label class=""color-row""><span>Text</span><input data-overlay=""TextColor"" type=""color""></label><label class=""color-row""><span>Muted</span><input data-overlay=""MutedColor"" type=""color""></label><label class=""color-row""><span>Fill</span><input data-overlay=""AccentColor"" type=""color""></label><label class=""color-row""><span>Empty bar</span><input data-overlay=""BarColor"" type=""color""></label></fieldset>
        <fieldset><legend>Bar image</legend><label class=""check-row""><input data-overlay=""ShowGoalImage"" type=""checkbox""><span>Show image</span></label><label><span>Image fit</span><select data-overlay=""GoalImageFit""><option value=""contain"">Contain</option><option value=""cover"">Cover</option></select></label><div class=""drop"" id=""goalDrop"">Drop PNG/JPG/WebP here or click</div><input id=""goalImageFile"" type=""file"" accept=""image/png,image/jpeg,image/webp,image/gif"" hidden><div class=""image-name"" id=""goalImageName""></div><button type=""button"" id=""clearGoalImage"">Clear image</button><label><span>Image width</span><div class=""row""><input data-overlay=""GoalImageWidth"" type=""range"" min=""20"" max=""1600""><input data-overlay=""GoalImageWidth"" type=""number""></div></label><label><span>Image height</span><div class=""row""><input data-overlay=""GoalImageHeight"" type=""range"" min=""20"" max=""1000""><input data-overlay=""GoalImageHeight"" type=""number""></div></label><label><span>Image X</span><div class=""row""><input data-overlay=""GoalImageX"" type=""range"" min=""-800"" max=""800""><input data-overlay=""GoalImageX"" type=""number""></div></label><label><span>Image Y</span><div class=""row""><input data-overlay=""GoalImageY"" type=""range"" min=""-600"" max=""600""><input data-overlay=""GoalImageY"" type=""number""></div></label></fieldset>
        <fieldset><legend>Decor image</legend><label class=""check-row""><input data-overlay=""ShowDecorImage"" type=""checkbox""><span>Show decor image</span></label><div class=""drop"" id=""decorDrop"">Drop PNG/JPG/WebP here or click</div><input id=""decorImageFile"" type=""file"" accept=""image/png,image/jpeg,image/webp,image/gif"" hidden><div class=""image-name"" id=""decorImageName""></div><button type=""button"" id=""clearDecorImage"">Clear image</button><label><span>Image X</span><div class=""row""><input data-overlay=""DecorImageX"" type=""range"" min=""-800"" max=""800""><input data-overlay=""DecorImageX"" type=""number""></div></label><label><span>Image Y</span><div class=""row""><input data-overlay=""DecorImageY"" type=""range"" min=""-600"" max=""600""><input data-overlay=""DecorImageY"" type=""number""></div></label><label><span>Image width</span><div class=""row""><input data-overlay=""DecorImageWidth"" type=""range"" min=""20"" max=""1200""><input data-overlay=""DecorImageWidth"" type=""number""></div></label></fieldset>
        <fieldset><legend>Last donation</legend><label class=""check-row""><input data-overlay=""ShowLastDonor"" type=""checkbox""><span>Show last donor</span></label><label class=""check-row""><input data-overlay=""ShowLastAmount"" type=""checkbox""><span>Show last amount</span></label><label class=""check-row""><input data-overlay=""ShowLastPlatform"" type=""checkbox""><span>Show last platform</span></label><label><span>Last donation font</span><select data-overlay=""LastDonationFontFamily"" data-font-select></select></label><label><span>Last donation size</span><div class=""row""><input data-overlay=""LastDonationFontSize"" type=""range"" min=""8"" max=""64""><input data-overlay=""LastDonationFontSize"" type=""number""></div></label><label><span>Last donation align</span><select data-overlay=""LastDonationTextAlign""><option value=""left"">Left</option><option value=""center"">Center</option><option value=""right"">Right</option></select></label></fieldset>
        <fieldset><legend>Connected services</legend><label class=""check-row""><input data-overlay=""ShowServices"" type=""checkbox""><span>Show connected services</span></label><label><span>Services title</span><input data-overlay=""ServicesTitle"" type=""text""></label><label><span>Providers font</span><select data-overlay=""ServicesFontFamily"" data-font-select></select></label><label><span>Services align</span><select data-overlay=""ServicesTextAlign""><option value=""left"">Left</option><option value=""center"">Center</option><option value=""right"">Right</option></select></label><label><span>Services size</span><div class=""row""><input data-overlay=""ServicesFontSize"" type=""range"" min=""8"" max=""64""><input data-overlay=""ServicesFontSize"" type=""number""></div></label><p class=""note"">Only enabled providers are shown. Clear a checkbox to hide a provider from Goal.</p><div id=""serviceToggles"" class=""compact-grid""></div></fieldset>
      </div>

      <div class=""pane"" data-pane=""timer"">
        <fieldset><legend>Timer presets</legend><div class=""presets"" id=""timerPresets""></div></fieldset>
        <fieldset><legend>Timer editor</legend><label><span>Mode</span><select data-overlay=""TimerMode""><option value=""countdown"">Countdown: donations add time</option><option value=""countup-reset"">Count up: reset to zero on event</option></select></label><label><span>Title 1</span><input data-overlay=""TimerHeaderTitle"" type=""text""></label><label><span>Title 2</span><input data-overlay=""TimerTitle"" type=""text""></label><label><span>Subtitle</span><input data-overlay=""TimerSubtitle"" type=""text""></label><label><span>Start value, seconds</span><input data-overlay=""TimerStartSeconds"" type=""number""></label><label><span>Donation amount for one time step</span><input data-overlay=""TimerUnitAmount"" type=""number"" step=""0.01""></label><label><span>Seconds added for that amount</span><input data-overlay=""TimerSecondsPerUnit"" type=""number""></label><label><span>Maximum timer seconds, 0 = no limit</span><input data-overlay=""TimerMaxSeconds"" type=""number""></label><label><span>Currency</span><input data-overlay=""TimerCurrency"" type=""text""></label><p class=""note"">Example: amount 100 and seconds 3600 means 100 RUB = 60 min.</p></fieldset>
        <fieldset><legend>Timer visibility</legend><label class=""check-row""><input data-overlay=""TimerEnabled"" type=""checkbox""><span>Enable timer</span></label><label class=""check-row""><input data-overlay=""ShowPanelBackground"" type=""checkbox""><span>Show background</span></label><label class=""check-row""><input data-overlay=""TimerShowConversion"" type=""checkbox""><span>Show conversion line</span></label><label class=""check-row""><input data-overlay=""TimerShowServices"" type=""checkbox""><span>Show providers in timer</span></label></fieldset>
        <fieldset><legend>Timer look</legend><label><span>Timer font</span><select data-overlay=""TimerFontFamily"" data-font-select></select></label><label><span>Title size</span><div class=""row""><input data-overlay=""TitleSize"" type=""range"" min=""10"" max=""96""><input data-overlay=""TitleSize"" type=""number""></div></label><label><span>Label size</span><div class=""row""><input data-overlay=""LabelSize"" type=""range"" min=""8"" max=""64""><input data-overlay=""LabelSize"" type=""number""></div></label><label><span>Meta size</span><div class=""row""><input data-overlay=""MetaSize"" type=""range"" min=""8"" max=""64""><input data-overlay=""MetaSize"" type=""number""></div></label><label><span>Timer width</span><div class=""row""><input data-overlay=""TimerWidth"" type=""range"" min=""80"" max=""1600""><input data-overlay=""TimerWidth"" type=""number""></div></label><label><span>Timer X</span><div class=""row""><input data-overlay=""TimerX"" type=""range"" min=""-800"" max=""800""><input data-overlay=""TimerX"" type=""number""></div></label><label><span>Timer Y</span><div class=""row""><input data-overlay=""TimerY"" type=""range"" min=""-600"" max=""600""><input data-overlay=""TimerY"" type=""number""></div></label><label><span>Timer align</span><select data-overlay=""TimerTextAlign""><option value=""left"">Left</option><option value=""center"">Center</option><option value=""right"">Right</option></select></label><label><span>Background opacity</span><div class=""row""><input data-overlay=""ContainerOpacity"" type=""range"" min=""0"" max=""1"" step=""0.01""><input data-overlay=""ContainerOpacity"" type=""number"" min=""0"" max=""1"" step=""0.01""></div></label><label class=""color-row""><span>Background</span><input data-overlay=""BackgroundColor"" type=""color""></label><label class=""color-row""><span>Text</span><input data-overlay=""TextColor"" type=""color""></label><label class=""color-row""><span>Muted</span><input data-overlay=""MutedColor"" type=""color""></label></fieldset>
      </div>

      <div class=""pane"" data-pane=""credits"">
        <fieldset><legend>Credits presets</legend><div class=""presets"" id=""creditsPresets""></div></fieldset>
        <fieldset><legend>Credits editor</legend><label class=""check-row""><input data-credits=""CreditsEnabled"" type=""checkbox""><span>Enable credits collection</span></label><label class=""check-row""><input data-credits=""UseNativeCredits"" type=""checkbox""><span>Use Streamer.bot Credits data</span></label><div class=""buttons""><button type=""button"" id=""testCredits"">Load Streamer.bot test credits</button><button type=""button"" id=""pauseCredits"">Pause credits preview</button><button type=""button"" id=""restartCredits"">Restart credits preview</button></div><p class=""note"">Streamer.bot HTTP Server must be enabled on 127.0.0.1:7474. DonConnect falls back to local examples if it is unavailable.</p><label><span>Title</span><input data-credits=""Title"" type=""text""></label><label><span>Title font</span><select data-credits=""TitleFontFamily"" data-font-select></select></label><label><span>Subtitle</span><input data-credits=""Subtitle"" type=""text""></label><label><span>Outro</span><input data-credits=""Outro"" type=""text""></label><label><span>Donation section title</span><input data-credits=""SectionTitle"" type=""text""></label><label class=""check-row""><input data-credits=""ShowAmounts"" type=""checkbox""><span>Show amounts</span></label><label class=""check-row""><input data-credits=""ShowPlatforms"" type=""checkbox""><span>Show platforms</span></label><label class=""check-row""><input data-credits=""ShowMessages"" type=""checkbox""><span>Show messages</span></label><label><span>Details font</span><select data-credits=""DetailFontFamily"" data-font-select></select></label><label><span>Base roll duration, seconds</span><input data-credits=""DurationSeconds"" type=""number"" min=""5"" max=""600""></label><label class=""check-row""><input data-credits=""LockDuration"" type=""checkbox""><span>Keep fixed speed for long credits</span></label><label><span>Fields</span><input data-credits=""DonationFields"" type=""text""></label></fieldset>
        <fieldset><legend>Credits sections</legend><p class=""note"">Clear a checkbox to hide a section from Streamer.bot Credits.</p><div class=""compact-grid"" id=""creditsSectionToggles""></div></fieldset>
        <fieldset><legend>Credits look</legend><label><span>Base font</span><select data-credits=""FontFamily"" data-font-select></select></label><label><span>Width</span><div class=""row""><input data-credits=""Width"" type=""range"" min=""320"" max=""1920""><input data-credits=""Width"" type=""number""></div></label><label><span>Font size</span><div class=""row""><input data-credits=""FontSize"" type=""range"" min=""14"" max=""120""><input data-credits=""FontSize"" type=""number""></div></label><label class=""check-row""><input data-credits=""TransparentBackground"" type=""checkbox""><span>Transparent background</span></label><label class=""color-row""><span>Background</span><input data-credits=""BackgroundColor"" type=""color""></label><label class=""color-row""><span>Text</span><input data-credits=""TextColor"" type=""color""></label><label class=""color-row""><span>Muted</span><input data-credits=""MutedColor"" type=""color""></label><label class=""color-row""><span>Accent</span><input data-credits=""AccentColor"" type=""color""></label></fieldset>
      </div>

      <div class=""pane"" data-pane=""leaderboard"">
        <fieldset><legend>Leaderboard presets</legend><div class=""presets"" id=""leaderboardPresets""></div></fieldset>
        <fieldset><legend>Leaderboard editor</legend><label class=""check-row""><input data-leaderboard=""Enabled"" type=""checkbox""><span>Enable leaderboard</span></label><label class=""check-row""><input data-leaderboard=""ShowTitle"" type=""checkbox""><span>Show title</span></label><label><span>Title</span><input data-leaderboard=""Title"" type=""text""></label><label><span>Mode</span><select data-leaderboard=""Mode""><option value=""overall"">Overall top</option><option value=""platform-slides"">Platform slides</option><option value=""recent"">Recent donations</option></select></label><label><span>Rows</span><input data-leaderboard=""TopCount"" type=""number"" min=""1"" max=""10""></label><label><span>Slide duration, ms</span><input data-leaderboard=""SlideDuration"" type=""number"" min=""1000"" max=""30000"" step=""500""></label><label class=""check-row""><input data-leaderboard=""ShowRanks"" type=""checkbox""><span>Show ranks</span></label><label class=""check-row""><input data-leaderboard=""ShowAmounts"" type=""checkbox""><span>Show amounts</span></label><label class=""check-row""><input data-leaderboard=""ShowPlatforms"" type=""checkbox""><span>Show platforms</span></label></fieldset>
        <fieldset><legend>Leaderboard look</legend><label><span>Base font</span><select data-leaderboard=""FontFamily"" data-font-select></select></label><label><span>Title font</span><select data-leaderboard=""TitleFontFamily"" data-font-select></select></label><label><span>Amount font</span><select data-leaderboard=""AmountFontFamily"" data-font-select></select></label><label><span>Width</span><div class=""row""><input data-leaderboard=""Width"" type=""range"" min=""240"" max=""1920""><input data-leaderboard=""Width"" type=""number""></div></label><label><span>Padding</span><div class=""row""><input data-leaderboard=""Padding"" type=""range"" min=""0"" max=""120""><input data-leaderboard=""Padding"" type=""number""></div></label><label><span>Border radius</span><div class=""row""><input data-leaderboard=""BorderRadius"" type=""range"" min=""0"" max=""120""><input data-leaderboard=""BorderRadius"" type=""number""></div></label><label><span>Font size</span><div class=""row""><input data-leaderboard=""FontSize"" type=""range"" min=""10"" max=""96""><input data-leaderboard=""FontSize"" type=""number""></div></label><label><span>Title size</span><div class=""row""><input data-leaderboard=""TitleSize"" type=""range"" min=""10"" max=""120""><input data-leaderboard=""TitleSize"" type=""number""></div></label><label><span>Row gap</span><div class=""row""><input data-leaderboard=""RowGap"" type=""range"" min=""0"" max=""48""><input data-leaderboard=""RowGap"" type=""number""></div></label><label><span>Opacity</span><div class=""row""><input data-leaderboard=""Opacity"" type=""range"" min=""0"" max=""1"" step=""0.01""><input data-leaderboard=""Opacity"" type=""number"" min=""0"" max=""1"" step=""0.01""></div></label><label class=""color-row""><span>Background</span><input data-leaderboard=""BackgroundColor"" type=""color""></label><label class=""color-row""><span>Text</span><input data-leaderboard=""TextColor"" type=""color""></label><label class=""color-row""><span>Muted</span><input data-leaderboard=""MutedColor"" type=""color""></label><label class=""color-row""><span>Accent</span><input data-leaderboard=""AccentColor"" type=""color""></label></fieldset>
        <fieldset><legend>Leaderboard entries</legend><p class=""note"">Add a custom row or edit names from received donations. Deleting a row automatically moves the next place up.</p><div class=""compact-grid""><label><span>Name</span><input id=""leaderboardEntryName"" type=""text""></label><label><span>Amount</span><input id=""leaderboardEntryAmount"" type=""number"" value=""100"" step=""0.01""></label><label><span>Currency</span><input id=""leaderboardEntryCurrency"" type=""text"" value=""RUB""></label><label><span>Platform</span><input id=""leaderboardEntryPlatform"" type=""text"" value=""Manual""></label></div><button type=""button"" id=""addLeaderboardEntry"">Add custom row</button><div class=""media-grid"" id=""leaderboardEntries""></div></fieldset>
        <div class=""buttons""><button class=""warn"" id=""resetLeaderboardData"">Clear leaderboard data</button></div>
      </div>

      <div class=""pane"" data-pane=""filter"">
        <fieldset><legend>Blocked names and words</legend><p class=""note"">One item per line. The filter changes browser widgets only and keeps original Streamer.bot donation variables untouched.</p><label><span>Blocked nicknames</span><textarea data-filter=""BlockedNames"" rows=""7"" placeholder=""Bad nickname""></textarea></label><label><span>Blocked words</span><textarea data-filter=""BlockedWords"" rows=""7"" placeholder=""Bad word""></textarea></label><label><span>Replacement nickname</span><input data-filter=""ReplacementName"" type=""text""></label><label><span>Replacement text</span><input data-filter=""ReplacementText"" type=""text""></label></fieldset>
        <fieldset><legend>Blocked test donation</legend><div class=""compact-grid""><label><span>Donor</span><input id=""filterTestDonor"" type=""text"" value=""BadNick""></label><label><span>Amount</span><input id=""filterTestAmount"" type=""number"" value=""100"" step=""0.01""></label><label><span>Currency</span><input id=""filterTestCurrency"" type=""text"" value=""RUB""></label><label><span>Platform</span><input id=""filterTestPlatform"" type=""text"" value=""Filter test""></label></div><label><span>Message</span><textarea id=""filterTestMessage"" rows=""3"">This message has a bad word.</textarea></label><button type=""button"" id=""sendFilterTest"">Send blocked test</button></fieldset>
      </div>

      <fieldset id=""testsPanel""><legend>Tests</legend><div class=""buttons"" id=""donationTestButtons""><button data-test=""50"">Test 50</button><button data-test=""500"">Test 500</button><button data-test=""long"">Test long message</button><button data-test=""anonymous"">Test anonymous donation</button></div><div id=""timerTestPanel"" class=""hidden""><p class=""note"">Add time manually when a donation came outside DonConnect. It affects only the timer.</p><div class=""compact-grid""><label><span>Amount</span><input id=""timerTestAmount"" type=""number"" value=""50"" step=""0.01""></label><label><span>Currency</span><input id=""timerTestCurrency"" type=""text"" value=""RUB""></label></div><button type=""button"" id=""sendTimerTest"">Add timer time</button></div></fieldset>
      <div class=""buttons""><button class=""primary"" id=""save"">Save current editor</button><button class=""warn"" id=""reset"">Reset current editor</button><button id=""copy"">Copy current OBS URL</button></div>
      <div class=""status"" id=""status""></div>
      <section class=""help""><h2>OBS steps</h2><ol><li>Choose a tab and adjust the widget.</li><li>Click Save current editor.</li><li>Add Browser Source in OBS.</li><li>Paste the current URL below.</li></ol><p class=""note"" id=""obsSize""></p><div class=""obs-url""><input id=""obsUrl"" type=""text"" readonly><button id=""copy2"">Copy</button></div></section>
    </section>
    <section class=""preview""><div class=""preview-head""><span>Live preview</span></div><iframe id=""frame"" src=""/donconnect/widget?preview=1"" title=""Live preview""></iframe></section>
  </main>
  <script>
    let active = 'donation';
    let lang = 'en';
    const fallbackFonts = { windows:['Segoe UI','Arial','Calibri','Verdana','Tahoma','Trebuchet MS','Georgia','Times New Roman','Consolas','Courier New','Impact','Comic Sans MS'], google:[] };
    let donation = {}; let overlay = {}; let credits = {}; let leaderboard = {}; let contentFilter = {}; let fonts = cloneFontCatalog(fallbackFonts); let speechVoices = { items:[] }; let alertMedia = { items:[], directory:'', maxUploadBytes:33554432 };
    let creditsSectionCatalog = []; let creditsSectionLoadId = 0;
    const urls = { donation:'/donconnect/widget', history:'/donconnect/dock', goal:'/donconnect/goal', timer:'/donconnect/timer', credits:'/donconnect/credits', leaderboard:'/donconnect/leaderboard', filter:'/donconnect/widget' };
    const serviceNames = ['DonationAlerts','StreamElements','Streamlabs','DonatePay RU','DonatePay EU','Donate.Stream','deStream','DonateX.gg','ODA','Generic API'];
    const donationDefaults = { Width:680, Height:360, BorderRadius:18, Padding:26, FontSize:28, Opacity:.88, BackgroundColor:'#10131a', TextColor:'#f8fbff', AccentColor:'#35d07f', AnimationDuration:650, FontFamily:'Segoe UI', DonorFontFamily:'', AmountFontFamily:'', MessageFontFamily:'', PlatformFontFamily:'', DonorTemplate:'{donor}', AmountTemplate:'{amount} {currency}', MessageTemplate:'{message}', PlatformTemplate:'{platform}', ShowBackground:true, ShowProgressBar:false, ShowPlatform:true, DisplayDuration:9000, EntryAnimation:'fade', ExitAnimation:'fade', TextAnimation:'fade', MediaFile:'', SoundFile:'', TextSoundFile:'', MediaFit:'contain', MediaPlacement:'above', MediaWidth:260, MediaHeight:170, MediaX:0, MediaY:0, TextAlign:'center', DonorX:0, DonorY:0, AmountX:0, AmountY:0, MessageX:0, MessageY:0, SoundVolume:75, TextSoundVolume:45, SpeakDonation:false, SpeechReadDonor:true, SpeechReadAmount:true, SpeechReadPlatform:true, SpeechReadMessage:true, SpeechVoice:'', SpeechRate:1, SpeechPitch:1, SpeechVolume:85, VideoMuted:true, AlertRules:[], PresetName:'Minimal Dark', Language:'en' };
    const overlayDefaults = { Mode:'both', GoalEnabled:true, GoalHeaderTitle:'Goal', GoalTitle:'Goal', GoalCurrent:'0', GoalTarget:'10000', GoalCurrency:'RUB', TimerEnabled:false, TimerHeaderTitle:'Timer', TimerTitle:'Timer', TimerSubtitle:'', TimerMode:'countdown', TimerStartSeconds:'0', TimerUnitAmount:'100', TimerSecondsPerUnit:'60', TimerMaxSeconds:'0', TimerCurrency:'RUB', TimerShowServices:false, TimerShowConversion:true, TimerX:0, TimerY:0, TimerWidth:680, TimerTextAlign:'center', Width:920, BorderRadius:22, BarRadius:22, Padding:22, TitleSize:30, ValueSize:42, LabelSize:17, MetaSize:17, Opacity:.94, ContainerOpacity:.94, BarOpacity:1, ShowPanelBackground:true, ShowGoalBar:true, ShowGoalProgress:true, ShowGoalMeta:true, ShowGoalText:true, ShowGoalValue:true, ShowGoalImage:false, GoalImageDataUrl:'', GoalImageName:'', GoalImageMode:'reveal', GoalImageFit:'contain', GoalImageWidth:680, GoalImageHeight:220, GoalImageX:0, GoalImageY:0, GoalBarVisualMode:'bar', GoalFillDirection:'horizontal', GoalBarLength:680, ShowDecorImage:false, DecorImageDataUrl:'', DecorImageName:'', DecorImageX:0, DecorImageY:0, DecorImageWidth:220, FontFamily:'Segoe UI', GoalHeaderFontFamily:'', GoalTitleFontFamily:'', GoalValueFontFamily:'', ServicesFontFamily:'', LastDonationFontFamily:'', LastDonationFontSize:14, LastDonationTextAlign:'center', TimerFontFamily:'', BackgroundColor:'#10131a', TextColor:'#f8fbff', MutedColor:'#b8c0cc', AccentColor:'#7c3cff', BarColor:'#1e2026', ShowServices:true, ServicesTitle:'Connected providers', ServicesTextAlign:'center', ServicesFontSize:14, HiddenServices:[], ShowLastDonation:true, ShowLastDonor:true, ShowLastAmount:true, ShowLastPlatform:true, Bare:false, GoalFormat:'amount', GoalBarWidth:100, GoalBarHeight:84, GoalBarAlign:'center', GoalTextPlacement:'inside', GoalTextAlign:'center', GoalTextOffsetX:0, GoalTextOffsetY:0 };
    const creditsDefaults = { CreditsEnabled:true, UseNativeCredits:true, UseTestData:false, Title:'Thanks for watching', Subtitle:'Today with us', Outro:'See you next stream', Duration:'70s', DurationSeconds:70, LockDuration:false, DonationFields:'name,amount,message', SectionTitle:'Donations', ShowAmounts:true, ShowPlatforms:true, ShowMessages:true, HiddenSections:[], TransparentBackground:true, Width:1120, FontSize:48, FontFamily:'Segoe UI', TitleFontFamily:'', DetailFontFamily:'', BackgroundColor:'#000000', TextColor:'#f7f4ec', MutedColor:'#b9d8d2', AccentColor:'#ffcf5a', ShadowColor:'rgba(0,0,0,.7)' };
    const leaderboardDefaults = { Enabled:true, Title:'Top donors', Mode:'overall', TopCount:5, SlideDuration:5000, ShowTitle:true, ShowRanks:true, ShowAmounts:true, ShowPlatforms:true, Width:560, Padding:18, BorderRadius:16, FontSize:22, TitleSize:26, RowGap:8, Opacity:.94, BackgroundColor:'#10131a', TextColor:'#f8fbff', MutedColor:'#b8c0cc', AccentColor:'#7c3cff', FontFamily:'Segoe UI', TitleFontFamily:'', AmountFontFamily:'' };
    const donationPresets = { 'Minimal Dark':{ Width:680, Height:360, BorderRadius:18, Padding:26, FontSize:28, TextAlign:'center', MediaPlacement:'above', FontFamily:'Segoe UI', BackgroundColor:'#10131a', TextColor:'#f8fbff', AccentColor:'#35d07f', AnimationDuration:650 }, 'Clean Light':{ Width:720, Height:320, BorderRadius:10, Padding:28, FontSize:27, TextAlign:'left', MediaPlacement:'left', FontFamily:'Georgia', BackgroundColor:'#ffffff', TextColor:'#18212b', AccentColor:'#2c7be5', AnimationDuration:500 }, 'Twitch Style':{ Width:760, Height:380, BorderRadius:4, Padding:30, FontSize:30, TextAlign:'center', MediaPlacement:'above', FontFamily:'Trebuchet MS', BackgroundColor:'#18111f', TextColor:'#ffffff', AccentColor:'#9146ff', AnimationDuration:700 }, 'Neon':{ Width:780, Height:400, BorderRadius:24, Padding:30, FontSize:31, TextAlign:'center', MediaPlacement:'background', FontFamily:'Consolas', BackgroundColor:'#070812', TextColor:'#ecfbff', AccentColor:'#00f5ff', AnimationDuration:850 }, 'Mobile Compact':{ Width:440, Height:230, BorderRadius:12, Padding:16, FontSize:20, TextAlign:'center', MediaPlacement:'right', FontFamily:'Tahoma', BackgroundColor:'#111827', TextColor:'#f9fafb', AccentColor:'#f59e0b', AnimationDuration:450 } };
    const timerPresets = { 'Studio Clock':{ TimerWidth:620, TimerFontFamily:'Segoe UI', TitleSize:34, ValueSize:64, MetaSize:18, BackgroundColor:'#10131a', TextColor:'#f8fbff', MutedColor:'#b8c0cc', AccentColor:'#35d07f' }, 'Arcade Extend':{ TimerWidth:720, TimerFontFamily:'Consolas', TitleSize:30, ValueSize:72, MetaSize:19, BackgroundColor:'#10131a', TextColor:'#ffffff', MutedColor:'#84f1ff', AccentColor:'#00f5ff' }, 'Clean Broadcast':{ TimerWidth:680, TimerFontFamily:'Georgia', TitleSize:28, ValueSize:62, MetaSize:17, BackgroundColor:'#ffffff', TextColor:'#17202a', MutedColor:'#536170', AccentColor:'#2c7be5' }, 'Count Up Live':{ TimerWidth:660, TimerMode:'countup-reset', TimerFontFamily:'Trebuchet MS', TitleSize:28, ValueSize:68, MetaSize:17, BackgroundColor:'#18111f', TextColor:'#ffffff', MutedColor:'#d3c2e8', AccentColor:'#9146ff' } };
    const creditsPresets = { 'Classic':{ FontFamily:'Segoe UI', TitleFontFamily:'Georgia', TextColor:'#f7f4ec', MutedColor:'#b9d8d2', AccentColor:'#ffcf5a', Width:1120, FontSize:48 }, 'Cinema':{ FontFamily:'Georgia', TitleFontFamily:'Times New Roman', TextColor:'#ffffff', MutedColor:'#d6d6d6', AccentColor:'#ffcf5a', Width:1240, FontSize:50 }, 'Pixel Party':{ FontFamily:'Consolas', TitleFontFamily:'Impact', TextColor:'#f7fbff', MutedColor:'#a5f3fc', AccentColor:'#fb7185', Width:1080, FontSize:44 }, 'Clean':{ FontFamily:'Segoe UI', TitleFontFamily:'Trebuchet MS', TextColor:'#17202a', MutedColor:'#536170', AccentColor:'#2c7be5', BackgroundColor:'#ffffff', Width:1120, FontSize:46 } };
    const leaderboardPresets = { 'Compact Dark':{ FontFamily:'Segoe UI', Width:620, FontSize:24, TitleSize:30, BackgroundColor:'#10131a', TextColor:'#f8fbff', MutedColor:'#b8c0cc', AccentColor:'#7c3cff' }, 'Sports Board':{ FontFamily:'Impact', AmountFontFamily:'Consolas', Width:720, FontSize:28, TitleSize:38, BackgroundColor:'#141414', TextColor:'#ffffff', MutedColor:'#cccccc', AccentColor:'#ffcf5a' }, 'Clean List':{ FontFamily:'Georgia', Width:680, FontSize:24, TitleSize:32, BackgroundColor:'#ffffff', TextColor:'#17202a', MutedColor:'#536170', AccentColor:'#2c7be5' }, 'Neon Top':{ FontFamily:'Consolas', Width:700, FontSize:25, TitleSize:34, BackgroundColor:'#070812', TextColor:'#ecfbff', MutedColor:'#8bdcff', AccentColor:'#00f5ff' } };
    const filterDefaults = { BlockedNames:'', BlockedWords:'', ReplacementName:'Anonymous', ReplacementText:'[hidden]' };
    const goalPresets = { 'Classic Panel':{ Width:720, BorderRadius:16, BarRadius:999, Padding:22, TitleSize:24, ValueSize:42, LabelSize:15, MetaSize:16, Opacity:.9, BackgroundColor:'#10131a', TextColor:'#f8fbff', MutedColor:'#b8c0cc', AccentColor:'#35d07f', BarColor:'#2b3440', GoalBarWidth:100, GoalBarHeight:14, GoalBarLength:520, GoalFillDirection:'horizontal', GoalBarAlign:'center', GoalTextPlacement:'above', GoalTextAlign:'left', GoalTextOffsetX:0, GoalTextOffsetY:0, ShowServices:true, ServicesTitle:'Connected providers', ServicesTextAlign:'left', ServicesFontSize:14, GoalFormat:'amount' }, 'Week Support':{ Width:720, BorderRadius:22, BarRadius:22, Padding:18, TitleSize:26, ValueSize:34, LabelSize:15, MetaSize:16, Opacity:.94, BackgroundColor:'#1e2026', TextColor:'#ffffff', MutedColor:'#b8c0cc', AccentColor:'#7c00ff', BarColor:'#1e2026', GoalBarWidth:100, GoalBarHeight:74, GoalBarLength:520, GoalFillDirection:'horizontal', GoalBarAlign:'center', GoalTextPlacement:'inside', GoalTextAlign:'center', GoalTextOffsetX:0, GoalTextOffsetY:0, ShowServices:true, ServicesTitle:'Connected providers', ServicesTextAlign:'center', ServicesFontSize:14, GoalFormat:'amount' }, 'Clean Light':{ Width:760, BorderRadius:18, BarRadius:18, Padding:20, TitleSize:22, ValueSize:34, LabelSize:14, MetaSize:15, Opacity:.96, BackgroundColor:'#ffffff', TextColor:'#17202a', MutedColor:'#536170', AccentColor:'#2c7be5', BarColor:'#dfe8f3', GoalBarWidth:92, GoalBarHeight:64, GoalBarLength:560, GoalFillDirection:'horizontal', GoalBarAlign:'center', GoalTextPlacement:'inside', GoalTextAlign:'left', GoalTextOffsetX:10, GoalTextOffsetY:0, ShowServices:true, ServicesTitle:'Active services', ServicesTextAlign:'center', ServicesFontSize:13, GoalFormat:'amount' }, 'Neon Glass':{ Width:820, BorderRadius:28, BarRadius:28, Padding:24, TitleSize:24, ValueSize:38, LabelSize:14, MetaSize:15, Opacity:.86, BackgroundColor:'#070812', TextColor:'#ecfbff', MutedColor:'#8bdcff', AccentColor:'#00f5ff', BarColor:'#111827', GoalBarWidth:100, GoalBarHeight:82, GoalBarLength:600, GoalFillDirection:'horizontal', GoalBarAlign:'center', GoalTextPlacement:'inside', GoalTextAlign:'center', GoalTextOffsetX:0, GoalTextOffsetY:0, ShowServices:true, ServicesTitle:'Live platforms', ServicesTextAlign:'center', ServicesFontSize:14, GoalFormat:'amount' }, 'Slim Bottom':{ Width:680, BorderRadius:0, BarRadius:6, Padding:8, TitleSize:20, ValueSize:28, LabelSize:13, MetaSize:14, Opacity:.92, BackgroundColor:'#000000', TextColor:'#ffffff', MutedColor:'#cbd5e1', AccentColor:'#ffcf5a', BarColor:'#3a3a3a', GoalBarWidth:100, GoalBarHeight:10, GoalBarLength:520, GoalFillDirection:'horizontal', GoalBarAlign:'center', GoalTextPlacement:'above', GoalTextAlign:'center', GoalTextOffsetX:0, GoalTextOffsetY:0, ShowServices:false, ServicesTitle:'Connected providers', ServicesTextAlign:'center', ServicesFontSize:12, GoalFormat:'amount' }, 'Vertical Tower':{ Width:360, BorderRadius:22, BarRadius:999, Padding:22, TitleSize:22, ValueSize:30, LabelSize:13, MetaSize:14, Opacity:.9, ContainerOpacity:.9, BarOpacity:1, BackgroundColor:'#10131a', TextColor:'#f8fbff', MutedColor:'#a9b4c2', AccentColor:'#7c3cff', BarColor:'#232936', GoalBarWidth:100, GoalBarHeight:54, GoalBarLength:420, GoalFillDirection:'vertical', GoalBarAlign:'center', GoalTextPlacement:'above', GoalTextAlign:'center', GoalTextOffsetX:0, GoalTextOffsetY:0, ShowServices:false, ServicesTitle:'Connected providers', ServicesTextAlign:'center', ServicesFontSize:12, GoalFormat:'amount' } };
    const i18n = {
      en: { appTitle:'DonConnect editors', language:'Language', donation:'Donation', goal:'Goal', timer:'Timer', credits:'Credits', donationPresets:'Donation presets', donationSize:'Donation size', donationColors:'Donation colors', donationTemplates:'Donation templates', goalEditor:'Goal editor', goalLook:'Goal look', timerEditor:'Timer editor', timerLook:'Timer look', creditsEditor:'Credits editor', creditsLook:'Credits look', tests:'Tests', width:'Width', height:'Height', borderRadius:'Border radius', padding:'Padding', fontSize:'Font size', opacity:'Opacity', animationMs:'Animation, ms', background:'Background', text:'Text', accent:'Accent', donor:'Donor', amount:'Amount', message:'Message', enableGoal:'Enable goal', title:'Title', current:'Current', target:'Target', currency:'Currency', format:'Format', formatAmount:'Amount', formatPercent:'Percent', formatSummary:'Summary', radius:'Radius', valueSize:'Value size', enableTimer:'Enable timer', startSeconds:'Start seconds', donationAmountStep:'Donation amount per step', secondsPerStep:'Seconds per step', maxSeconds:'Max seconds, 0 = no limit', titleSize:'Title size', labelSize:'Label size', metaSize:'Meta size', muted:'Muted', bar:'Bar', enableCredits:'Enable credits collection', subtitle:'Subtitle', outro:'Outro', rollDuration:'Roll duration', fields:'Fields', test50:'Test 50', test500:'Test 500', testLong:'Test long message', testAnonymous:'Test anonymous donation', save:'Save current editor', reset:'Reset current editor', copyCurrent:'Copy current OBS URL', copy:'Copy', obsSteps:'OBS steps', obs1:'Choose a tab and adjust the widget.', obs2:'Click Save current editor.', obs3:'Add Browser Source in OBS.', obs4:'Paste the current URL below.', livePreview:'Live preview', settingsSaved:'Settings saved', testSent:'Test donation sent', copied:'OBS URL copied: ', promptTitle:'OBS Browser Source URL' },
      ru: { appTitle:'\u0420\u0435\u0434\u0430\u043a\u0442\u043e\u0440\u044b DonConnect', language:'\u042f\u0437\u044b\u043a', donation:'\u0414\u043e\u043d\u0430\u0442', goal:'\u0426\u0435\u043b\u044c', timer:'\u0422\u0430\u0439\u043c\u0435\u0440', credits:'\u0422\u0438\u0442\u0440\u044b', donationPresets:'\u041f\u0440\u0435\u0441\u0435\u0442\u044b \u0434\u043e\u043d\u0430\u0442\u0430', donationSize:'\u0420\u0430\u0437\u043c\u0435\u0440 \u0434\u043e\u043d\u0430\u0442\u0430', donationColors:'\u0426\u0432\u0435\u0442\u0430 \u0434\u043e\u043d\u0430\u0442\u0430', donationTemplates:'\u0428\u0430\u0431\u043b\u043e\u043d\u044b \u0434\u043e\u043d\u0430\u0442\u0430', goalEditor:'\u0420\u0435\u0434\u0430\u043a\u0442\u043e\u0440 \u0446\u0435\u043b\u0438', goalLook:'\u0412\u0438\u0434 \u0446\u0435\u043b\u0438', timerEditor:'\u0420\u0435\u0434\u0430\u043a\u0442\u043e\u0440 \u0442\u0430\u0439\u043c\u0435\u0440\u0430', timerLook:'\u0412\u0438\u0434 \u0442\u0430\u0439\u043c\u0435\u0440\u0430', creditsEditor:'\u0420\u0435\u0434\u0430\u043a\u0442\u043e\u0440 \u0442\u0438\u0442\u0440\u043e\u0432', creditsLook:'\u0412\u0438\u0434 \u0442\u0438\u0442\u0440\u043e\u0432', tests:'\u0422\u0435\u0441\u0442\u044b', width:'\u0428\u0438\u0440\u0438\u043d\u0430', height:'\u0412\u044b\u0441\u043e\u0442\u0430', borderRadius:'\u0421\u043a\u0440\u0443\u0433\u043b\u0435\u043d\u0438\u0435', padding:'\u041e\u0442\u0441\u0442\u0443\u043f\u044b', fontSize:'\u0420\u0430\u0437\u043c\u0435\u0440 \u0448\u0440\u0438\u0444\u0442\u0430', opacity:'\u041f\u0440\u043e\u0437\u0440\u0430\u0447\u043d\u043e\u0441\u0442\u044c', animationMs:'\u0410\u043d\u0438\u043c\u0430\u0446\u0438\u044f, \u043c\u0441', background:'\u0424\u043e\u043d', text:'\u0422\u0435\u043a\u0441\u0442', accent:'\u0410\u043a\u0446\u0435\u043d\u0442', donor:'\u0418\u043c\u044f \u0434\u043e\u043d\u0430\u0442\u0435\u0440\u0430', amount:'\u0421\u0443\u043c\u043c\u0430', message:'\u0421\u043e\u043e\u0431\u0449\u0435\u043d\u0438\u0435', enableGoal:'\u0412\u043a\u043b\u044e\u0447\u0438\u0442\u044c \u0446\u0435\u043b\u044c', title:'\u0417\u0430\u0433\u043e\u043b\u043e\u0432\u043e\u043a', current:'\u0421\u0435\u0439\u0447\u0430\u0441', target:'\u0426\u0435\u043b\u044c', currency:'\u0412\u0430\u043b\u044e\u0442\u0430', format:'\u0424\u043e\u0440\u043c\u0430\u0442', formatAmount:'\u0421\u0443\u043c\u043c\u0430', formatPercent:'\u041f\u0440\u043e\u0446\u0435\u043d\u0442', formatSummary:'\u0418\u0442\u043e\u0433', radius:'\u0420\u0430\u0434\u0438\u0443\u0441', valueSize:'\u0420\u0430\u0437\u043c\u0435\u0440 \u0447\u0438\u0441\u0435\u043b', enableTimer:'\u0412\u043a\u043b\u044e\u0447\u0438\u0442\u044c \u0442\u0430\u0439\u043c\u0435\u0440', startSeconds:'\u0421\u0442\u0430\u0440\u0442, \u0441\u0435\u043a\u0443\u043d\u0434\u044b', donationAmountStep:'\u0421\u0443\u043c\u043c\u0430 \u0437\u0430 \u0448\u0430\u0433', secondsPerStep:'\u0421\u0435\u043a\u0443\u043d\u0434 \u0437\u0430 \u0448\u0430\u0433', maxSeconds:'\u041c\u0430\u043a\u0441. \u0441\u0435\u043a\u0443\u043d\u0434, 0 = \u0431\u0435\u0437 \u043b\u0438\u043c\u0438\u0442\u0430', titleSize:'\u0420\u0430\u0437\u043c\u0435\u0440 \u0437\u0430\u0433\u043e\u043b\u043e\u0432\u043a\u0430', labelSize:'\u0420\u0430\u0437\u043c\u0435\u0440 \u043f\u043e\u0434\u043f\u0438\u0441\u0435\u0439', metaSize:'\u0420\u0430\u0437\u043c\u0435\u0440 \u0434\u0435\u0442\u0430\u043b\u0435\u0439', muted:'\u0412\u0442\u043e\u0440\u0438\u0447\u043d\u044b\u0439', bar:'\u041f\u043e\u043b\u043e\u0441\u0430', enableCredits:'\u0421\u043e\u0431\u0438\u0440\u0430\u0442\u044c \u0442\u0438\u0442\u0440\u044b', subtitle:'\u041f\u043e\u0434\u0437\u0430\u0433\u043e\u043b\u043e\u0432\u043e\u043a', outro:'\u0424\u0438\u043d\u0430\u043b', rollDuration:'\u0414\u043b\u0438\u0442\u0435\u043b\u044c\u043d\u043e\u0441\u0442\u044c', fields:'\u041f\u043e\u043b\u044f', test50:'\u0422\u0435\u0441\u0442 50', test500:'\u0422\u0435\u0441\u0442 500', testLong:'\u0414\u043b\u0438\u043d\u043d\u044b\u0439 \u0442\u0435\u043a\u0441\u0442', testAnonymous:'\u0410\u043d\u043e\u043d\u0438\u043c\u043d\u044b\u0439 \u0434\u043e\u043d\u0430\u0442', save:'\u0421\u043e\u0445\u0440\u0430\u043d\u0438\u0442\u044c', reset:'\u0421\u0431\u0440\u043e\u0441\u0438\u0442\u044c', copyCurrent:'\u0421\u043a\u043e\u043f\u0438\u0440\u043e\u0432\u0430\u0442\u044c URL \u0434\u043b\u044f OBS', copy:'\u041a\u043e\u043f\u0438\u044f', obsSteps:'\u041a\u0430\u043a \u0432\u0441\u0442\u0430\u0432\u0438\u0442\u044c \u0432 OBS', obs1:'\u0412\u044b\u0431\u0435\u0440\u0438 \u0432\u043a\u043b\u0430\u0434\u043a\u0443 \u0438 \u043d\u0430\u0441\u0442\u0440\u043e\u0439 \u0432\u0438\u0434\u0436\u0435\u0442.', obs2:'\u041d\u0430\u0436\u043c\u0438 \u0421\u043e\u0445\u0440\u0430\u043d\u0438\u0442\u044c.', obs3:'\u0412 OBS \u0434\u043e\u0431\u0430\u0432\u044c \u0438\u0441\u0442\u043e\u0447\u043d\u0438\u043a Browser.', obs4:'\u0412\u0441\u0442\u0430\u0432\u044c URL \u0441\u043d\u0438\u0437\u0443.', livePreview:'Live preview', settingsSaved:'\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0441\u043e\u0445\u0440\u0430\u043d\u0435\u043d\u044b', testSent:'\u0422\u0435\u0441\u0442\u043e\u0432\u044b\u0439 \u0434\u043e\u043d\u0430\u0442 \u043e\u0442\u043f\u0440\u0430\u0432\u043b\u0435\u043d', copied:'URL \u0434\u043b\u044f OBS \u0441\u043a\u043e\u043f\u0438\u0440\u043e\u0432\u0430\u043d: ', promptTitle:'URL \u0434\u043b\u044f OBS Browser Source' },
      uk: { appTitle:'\u0420\u0435\u0434\u0430\u043a\u0442\u043e\u0440\u0438 DonConnect', language:'\u041c\u043e\u0432\u0430', donation:'\u0414\u043e\u043d\u0430\u0442', goal:'\u0426\u0456\u043b\u044c', timer:'\u0422\u0430\u0439\u043c\u0435\u0440', credits:'\u0422\u0438\u0442\u0440\u0438', donationPresets:'\u041f\u0440\u0435\u0441\u0435\u0442\u0438 \u0434\u043e\u043d\u0430\u0442\u0443', donationSize:'\u0420\u043e\u0437\u043c\u0456\u0440 \u0434\u043e\u043d\u0430\u0442\u0443', donationColors:'\u041a\u043e\u043b\u044c\u043e\u0440\u0438 \u0434\u043e\u043d\u0430\u0442\u0443', donationTemplates:'\u0428\u0430\u0431\u043b\u043e\u043d\u0438 \u0434\u043e\u043d\u0430\u0442\u0443', goalEditor:'\u0420\u0435\u0434\u0430\u043a\u0442\u043e\u0440 \u0446\u0456\u043b\u0456', goalLook:'\u0412\u0438\u0433\u043b\u044f\u0434 \u0446\u0456\u043b\u0456', timerEditor:'\u0420\u0435\u0434\u0430\u043a\u0442\u043e\u0440 \u0442\u0430\u0439\u043c\u0435\u0440\u0430', timerLook:'\u0412\u0438\u0433\u043b\u044f\u0434 \u0442\u0430\u0439\u043c\u0435\u0440\u0430', creditsEditor:'\u0420\u0435\u0434\u0430\u043a\u0442\u043e\u0440 \u0442\u0438\u0442\u0440\u0456\u0432', creditsLook:'\u0412\u0438\u0433\u043b\u044f\u0434 \u0442\u0438\u0442\u0440\u0456\u0432', tests:'\u0422\u0435\u0441\u0442\u0438', width:'\u0428\u0438\u0440\u0438\u043d\u0430', height:'\u0412\u0438\u0441\u043e\u0442\u0430', borderRadius:'\u0417\u0430\u043e\u043a\u0440\u0443\u0433\u043b\u0435\u043d\u043d\u044f', padding:'\u0412\u0456\u0434\u0441\u0442\u0443\u043f\u0438', fontSize:'\u0420\u043e\u0437\u043c\u0456\u0440 \u0448\u0440\u0438\u0444\u0442\u0443', opacity:'\u041f\u0440\u043e\u0437\u043e\u0440\u0456\u0441\u0442\u044c', animationMs:'\u0410\u043d\u0456\u043c\u0430\u0446\u0456\u044f, \u043c\u0441', background:'\u0424\u043e\u043d', text:'\u0422\u0435\u043a\u0441\u0442', accent:'\u0410\u043a\u0446\u0435\u043d\u0442', donor:'\u0406\u043c\u044f \u0434\u043e\u043d\u0430\u0442\u0435\u0440\u0430', amount:'\u0421\u0443\u043c\u0430', message:'\u041f\u043e\u0432\u0456\u0434\u043e\u043c\u043b\u0435\u043d\u043d\u044f', enableGoal:'\u0423\u0432\u0456\u043c\u043a\u043d\u0443\u0442\u0438 \u0446\u0456\u043b\u044c', title:'\u0417\u0430\u0433\u043e\u043b\u043e\u0432\u043e\u043a', current:'\u0417\u0430\u0440\u0430\u0437', target:'\u0426\u0456\u043b\u044c', currency:'\u0412\u0430\u043b\u044e\u0442\u0430', format:'\u0424\u043e\u0440\u043c\u0430\u0442', formatAmount:'\u0421\u0443\u043c\u0430', formatPercent:'\u0412\u0456\u0434\u0441\u043e\u0442\u043e\u043a', formatSummary:'\u041f\u0456\u0434\u0441\u0443\u043c\u043e\u043a', radius:'\u0420\u0430\u0434\u0456\u0443\u0441', valueSize:'\u0420\u043e\u0437\u043c\u0456\u0440 \u0447\u0438\u0441\u0435\u043b', enableTimer:'\u0423\u0432\u0456\u043c\u043a\u043d\u0443\u0442\u0438 \u0442\u0430\u0439\u043c\u0435\u0440', startSeconds:'\u0421\u0442\u0430\u0440\u0442, \u0441\u0435\u043a\u0443\u043d\u0434\u0438', donationAmountStep:'\u0421\u0443\u043c\u0430 \u0437\u0430 \u043a\u0440\u043e\u043a', secondsPerStep:'\u0421\u0435\u043a\u0443\u043d\u0434 \u0437\u0430 \u043a\u0440\u043e\u043a', maxSeconds:'\u041c\u0430\u043a\u0441. \u0441\u0435\u043a\u0443\u043d\u0434, 0 = \u0431\u0435\u0437 \u043b\u0456\u043c\u0456\u0442\u0443', titleSize:'\u0420\u043e\u0437\u043c\u0456\u0440 \u0437\u0430\u0433\u043e\u043b\u043e\u0432\u043a\u0430', labelSize:'\u0420\u043e\u0437\u043c\u0456\u0440 \u043f\u0456\u0434\u043f\u0438\u0441\u0456\u0432', metaSize:'\u0420\u043e\u0437\u043c\u0456\u0440 \u0434\u0435\u0442\u0430\u043b\u0435\u0439', muted:'\u0414\u0440\u0443\u0433\u043e\u0440\u044f\u0434\u043d\u0438\u0439', bar:'\u0421\u043c\u0443\u0433\u0430', enableCredits:'\u0417\u0431\u0438\u0440\u0430\u0442\u0438 \u0442\u0438\u0442\u0440\u0438', subtitle:'\u041f\u0456\u0434\u0437\u0430\u0433\u043e\u043b\u043e\u0432\u043e\u043a', outro:'\u0424\u0456\u043d\u0430\u043b', rollDuration:'\u0422\u0440\u0438\u0432\u0430\u043b\u0456\u0441\u0442\u044c', fields:'\u041f\u043e\u043b\u044f', test50:'\u0422\u0435\u0441\u0442 50', test500:'\u0422\u0435\u0441\u0442 500', testLong:'\u0414\u043e\u0432\u0433\u0438\u0439 \u0442\u0435\u043a\u0441\u0442', testAnonymous:'\u0410\u043d\u043e\u043d\u0456\u043c\u043d\u0438\u0439 \u0434\u043e\u043d\u0430\u0442', save:'\u0417\u0431\u0435\u0440\u0435\u0433\u0442\u0438', reset:'\u0421\u043a\u0438\u043d\u0443\u0442\u0438', copyCurrent:'\u0421\u043a\u043e\u043f\u0456\u044e\u0432\u0430\u0442\u0438 URL \u0434\u043b\u044f OBS', copy:'\u041a\u043e\u043f\u0456\u044f', obsSteps:'\u042f\u043a \u0432\u0441\u0442\u0430\u0432\u0438\u0442\u0438 \u0432 OBS', obs1:'\u0412\u0438\u0431\u0435\u0440\u0438 \u0432\u043a\u043b\u0430\u0434\u043a\u0443 \u0456 \u043d\u0430\u043b\u0430\u0448\u0442\u0443\u0439 \u0432\u0456\u0434\u0436\u0435\u0442.', obs2:'\u041d\u0430\u0442\u0438\u0441\u043d\u0438 \u0417\u0431\u0435\u0440\u0435\u0433\u0442\u0438.', obs3:'\u0412 OBS \u0434\u043e\u0434\u0430\u0439 \u0434\u0436\u0435\u0440\u0435\u043b\u043e Browser.', obs4:'\u0412\u0441\u0442\u0430\u0432 URL \u043d\u0438\u0436\u0447\u0435.', livePreview:'Live preview', settingsSaved:'\u041d\u0430\u043b\u0430\u0448\u0442\u0443\u0432\u0430\u043d\u043d\u044f \u0437\u0431\u0435\u0440\u0435\u0436\u0435\u043d\u043e', testSent:'\u0422\u0435\u0441\u0442\u043e\u0432\u0438\u0439 \u0434\u043e\u043d\u0430\u0442 \u043d\u0430\u0434\u0456\u0441\u043b\u0430\u043d\u043e', copied:'URL \u0434\u043b\u044f OBS \u0441\u043a\u043e\u043f\u0456\u0439\u043e\u0432\u0430\u043d\u043e: ', promptTitle:'URL \u0434\u043b\u044f OBS Browser Source' }
    };
    Object.assign(i18n.en, { goalPresets:'Goal presets', goalText:'Goal text', goalBar:'Goal bar', goalImage:'Goal image', lastDonation:'Last donation', connectedServices:'Connected services', textPlacement:'Text placement', insideBar:'Inside bar', aboveBar:'Above bar', belowBar:'Below bar', textAlign:'Text align', left:'Left', center:'Center', right:'Right', textX:'Text X', textY:'Text Y', panelWidth:'Panel width', barWidth:'Bar width, %', barHeight:'Bar height', barAlign:'Bar align', boxRadius:'Box radius', barRadius:'Bar radius', fill:'Fill', emptyBar:'Empty bar', showServices:'Show connected services', servicesTitle:'Services title', servicesAlign:'Services align', servicesSize:'Services size', title1:'Title 1', title2:'Title 2', containerOpacity:'Background opacity', barOpacity:'Bar opacity', showLastDonor:'Show last donor', showLastAmount:'Show last amount', showLastPlatform:'Show last platform', showGoalText:'Show goal text', showGoalValue:'Show amount text', showGoalMeta:'Show progress number', showGoalBar:'Show bar', showGoalProgress:'Show progress fill', showPanelBackground:'Show background', showGoalImage:'Show image', imageMode:'Image mode', imageFit:'Image fit', imageReveal:'Grayscale reveal', imageOverlay:'Overlay', imageContain:'Contain', imageCover:'Cover', imageDrop:'Drop PNG/JPG/WebP here or click', clearImage:'Clear image' });
    Object.assign(i18n.ru, { goalPresets:'\u041f\u0440\u0435\u0441\u0435\u0442\u044b \u0446\u0435\u043b\u0438', goalText:'\u0422\u0435\u043a\u0441\u0442 \u0446\u0435\u043b\u0438', goalBar:'\u041f\u043e\u043b\u043e\u0441\u0430 \u0446\u0435\u043b\u0438', connectedServices:'\u041f\u043e\u0434\u043a\u043b\u044e\u0447\u0451\u043d\u043d\u044b\u0435 \u043f\u043b\u043e\u0449\u0430\u0434\u043a\u0438', textPlacement:'\u0413\u0434\u0435 \u0442\u0435\u043a\u0441\u0442', insideBar:'\u0412\u043d\u0443\u0442\u0440\u0438 \u043f\u043e\u043b\u043e\u0441\u044b', aboveBar:'\u041d\u0430\u0434 \u043f\u043e\u043b\u043e\u0441\u043e\u0439', belowBar:'\u041f\u043e\u0434 \u043f\u043e\u043b\u043e\u0441\u043e\u0439', textAlign:'\u0412\u044b\u0440\u0430\u0432\u043d\u0438\u0432\u0430\u043d\u0438\u0435 \u0442\u0435\u043a\u0441\u0442\u0430', left:'\u0421\u043b\u0435\u0432\u0430', center:'\u041f\u043e \u0446\u0435\u043d\u0442\u0440\u0443', right:'\u0421\u043f\u0440\u0430\u0432\u0430', textX:'\u0422\u0435\u043a\u0441\u0442 X', textY:'\u0422\u0435\u043a\u0441\u0442 Y', panelWidth:'\u0428\u0438\u0440\u0438\u043d\u0430 \u0432\u0438\u0434\u0436\u0435\u0442\u0430', barWidth:'\u0428\u0438\u0440\u0438\u043d\u0430 \u043f\u043e\u043b\u043e\u0441\u044b, %', barHeight:'\u0412\u044b\u0441\u043e\u0442\u0430 \u043f\u043e\u043b\u043e\u0441\u044b', barAlign:'\u041f\u043e\u0437\u0438\u0446\u0438\u044f \u043f\u043e\u043b\u043e\u0441\u044b', boxRadius:'\u0420\u0430\u0434\u0438\u0443\u0441 \u0431\u043e\u043a\u0441\u0430', barRadius:'\u0420\u0430\u0434\u0438\u0443\u0441 \u043f\u043e\u043b\u043e\u0441\u044b', fill:'\u0417\u0430\u043f\u043e\u043b\u043d\u0435\u043d\u0438\u0435', emptyBar:'\u041f\u0443\u0441\u0442\u0430\u044f \u043f\u043e\u043b\u043e\u0441\u0430', showServices:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u043f\u043b\u043e\u0449\u0430\u0434\u043a\u0438', servicesTitle:'\u041f\u043e\u0434\u043f\u0438\u0441\u044c \u043f\u043b\u043e\u0449\u0430\u0434\u043e\u043a', servicesAlign:'\u0412\u044b\u0440\u0430\u0432\u043d\u0438\u0432\u0430\u043d\u0438\u0435', servicesSize:'\u0420\u0430\u0437\u043c\u0435\u0440 \u0441\u0442\u0440\u043e\u043a\u0438', title1:'\u0417\u0430\u0433\u043e\u043b\u043e\u0432\u043e\u043a 1', title2:'\u0417\u0430\u0433\u043e\u043b\u043e\u0432\u043e\u043a 2', containerOpacity:'\u041f\u0440\u043e\u0437\u0440\u0430\u0447\u043d\u043e\u0441\u0442\u044c \u043a\u043e\u043d\u0442\u0435\u0439\u043d\u0435\u0440\u0430', barOpacity:'\u041f\u0440\u043e\u0437\u0440\u0430\u0447\u043d\u043e\u0441\u0442\u044c \u043f\u043e\u043b\u043e\u0441\u044b', showLastDonor:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u043f\u043e\u0441\u043b\u0435\u0434\u043d\u0435\u0433\u043e \u0434\u043e\u043d\u0430\u0442\u0435\u0440\u0430', showLastAmount:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0441\u0443\u043c\u043c\u0443', showLastPlatform:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u043f\u043b\u043e\u0449\u0430\u0434\u043a\u0443' });
    Object.assign(i18n.uk, { goalPresets:'\u041f\u0440\u0435\u0441\u0435\u0442\u0438 \u0446\u0456\u043b\u0456', goalText:'\u0422\u0435\u043a\u0441\u0442 \u0446\u0456\u043b\u0456', goalBar:'\u0421\u043c\u0443\u0433\u0430 \u0446\u0456\u043b\u0456', connectedServices:'\u041f\u0456\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0456 \u043f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0438', textPlacement:'\u0414\u0435 \u0442\u0435\u043a\u0441\u0442', insideBar:'\u0423\u0441\u0435\u0440\u0435\u0434\u0438\u043d\u0456 \u0441\u043c\u0443\u0433\u0438', aboveBar:'\u041d\u0430\u0434 \u0441\u043c\u0443\u0433\u043e\u044e', belowBar:'\u041f\u0456\u0434 \u0441\u043c\u0443\u0433\u043e\u044e', textAlign:'\u0412\u0438\u0440\u0456\u0432\u043d\u044e\u0432\u0430\u043d\u043d\u044f \u0442\u0435\u043a\u0441\u0442\u0443', left:'\u041b\u0456\u0432\u043e\u0440\u0443\u0447', center:'\u041f\u043e \u0446\u0435\u043d\u0442\u0440\u0443', right:'\u041f\u0440\u0430\u0432\u043e\u0440\u0443\u0447', textX:'\u0422\u0435\u043a\u0441\u0442 X', textY:'\u0422\u0435\u043a\u0441\u0442 Y', panelWidth:'\u0428\u0438\u0440\u0438\u043d\u0430 \u0432\u0456\u0434\u0436\u0435\u0442\u0430', barWidth:'\u0428\u0438\u0440\u0438\u043d\u0430 \u0441\u043c\u0443\u0433\u0438, %', barHeight:'\u0412\u0438\u0441\u043e\u0442\u0430 \u0441\u043c\u0443\u0433\u0438', barAlign:'\u041f\u043e\u0437\u0438\u0446\u0456\u044f \u0441\u043c\u0443\u0433\u0438', boxRadius:'\u0420\u0430\u0434\u0456\u0443\u0441 \u0431\u043e\u043a\u0441\u0430', barRadius:'\u0420\u0430\u0434\u0456\u0443\u0441 \u0441\u043c\u0443\u0433\u0438', fill:'\u0417\u0430\u043f\u043e\u0432\u043d\u0435\u043d\u043d\u044f', emptyBar:'\u041f\u043e\u0440\u043e\u0436\u043d\u044f \u0441\u043c\u0443\u0433\u0430', showServices:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u043f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0438', servicesTitle:'\u041f\u0456\u0434\u043f\u0438\u0441 \u043f\u043b\u0430\u0442\u0444\u043e\u0440\u043c', servicesAlign:'\u0412\u0438\u0440\u0456\u0432\u043d\u044e\u0432\u0430\u043d\u043d\u044f', servicesSize:'\u0420\u043e\u0437\u043c\u0456\u0440 \u0440\u044f\u0434\u043a\u0430', title1:'\u0417\u0430\u0433\u043e\u043b\u043e\u0432\u043e\u043a 1', title2:'\u0417\u0430\u0433\u043e\u043b\u043e\u0432\u043e\u043a 2', containerOpacity:'\u041f\u0440\u043e\u0437\u043e\u0440\u0456\u0441\u0442\u044c \u043a\u043e\u043d\u0442\u0435\u0439\u043d\u0435\u0440\u0430', barOpacity:'\u041f\u0440\u043e\u0437\u043e\u0440\u0456\u0441\u0442\u044c \u0441\u043c\u0443\u0433\u0438', showLastDonor:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u043e\u0441\u0442\u0430\u043d\u043d\u044c\u043e\u0433\u043e \u0434\u043e\u043d\u0430\u0442\u0435\u0440\u0430', showLastAmount:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u0441\u0443\u043c\u0443', showLastPlatform:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u043f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0443' });
    Object.assign(i18n.ru, { goalImage:'\u0418\u0437\u043e\u0431\u0440\u0430\u0436\u0435\u043d\u0438\u0435 \u0446\u0435\u043b\u0438', lastDonation:'\u041f\u043e\u0441\u043b\u0435\u0434\u043d\u0438\u0439 \u0434\u043e\u043d\u0430\u0442', showGoalText:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0442\u0435\u043a\u0441\u0442 \u0446\u0435\u043b\u0438', showGoalValue:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0441\u0443\u043c\u043c\u0443 \u0446\u0435\u043b\u0438', showGoalMeta:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0446\u0438\u0444\u0440\u0443 \u043f\u0440\u043e\u0433\u0440\u0435\u0441\u0441\u0430', showGoalBar:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u043f\u043e\u043b\u043e\u0441\u0443', showGoalProgress:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0437\u0430\u043f\u043e\u043b\u043d\u0435\u043d\u0438\u0435', showPanelBackground:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0444\u043e\u043d', containerOpacity:'\u041f\u0440\u043e\u0437\u0440\u0430\u0447\u043d\u043e\u0441\u0442\u044c \u0444\u043e\u043d\u0430', showGoalImage:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0443', imageMode:'\u0420\u0435\u0436\u0438\u043c \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0438', imageFit:'\u0420\u0430\u0437\u043c\u0435\u0440 \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0438', imageReveal:'\u041f\u0440\u043e\u044f\u0432\u043b\u0435\u043d\u0438\u0435 \u0438\u0437 \u0447/\u0431', imageOverlay:'\u041f\u043e\u0432\u0435\u0440\u0445 \u043f\u043e\u043b\u043e\u0441\u044b', imageContain:'\u0412\u043f\u0438\u0441\u0430\u0442\u044c', imageCover:'\u0417\u0430\u043f\u043e\u043b\u043d\u0438\u0442\u044c', imageDrop:'\u041f\u0435\u0440\u0435\u0442\u0430\u0449\u0438 PNG/JPG/WebP \u0441\u044e\u0434\u0430 \u0438\u043b\u0438 \u043d\u0430\u0436\u043c\u0438', clearImage:'\u0423\u0431\u0440\u0430\u0442\u044c \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0443' });
    Object.assign(i18n.uk, { goalImage:'\u0417\u043e\u0431\u0440\u0430\u0436\u0435\u043d\u043d\u044f \u0446\u0456\u043b\u0456', lastDonation:'\u041e\u0441\u0442\u0430\u043d\u043d\u0456\u0439 \u0434\u043e\u043d\u0430\u0442', showGoalText:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u0442\u0435\u043a\u0441\u0442 \u0446\u0456\u043b\u0456', showGoalValue:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u0441\u0443\u043c\u0443 \u0446\u0456\u043b\u0456', showGoalMeta:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u0446\u0438\u0444\u0440\u0443 \u043f\u0440\u043e\u0433\u0440\u0435\u0441\u0443', showGoalBar:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u0441\u043c\u0443\u0433\u0443', showGoalProgress:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u0437\u0430\u043f\u043e\u0432\u043d\u0435\u043d\u043d\u044f', showPanelBackground:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u0444\u043e\u043d', containerOpacity:'\u041f\u0440\u043e\u0437\u043e\u0440\u0456\u0441\u0442\u044c \u0444\u043e\u043d\u0443', showGoalImage:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0443', imageMode:'\u0420\u0435\u0436\u0438\u043c \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0438', imageFit:'\u0420\u043e\u0437\u043c\u0456\u0440 \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0438', imageReveal:'\u041f\u0440\u043e\u044f\u0432\u043b\u0435\u043d\u043d\u044f \u0437 \u0447/\u0431', imageOverlay:'\u041f\u043e\u0432\u0435\u0440\u0445 \u0441\u043c\u0443\u0433\u0438', imageContain:'\u0412\u043f\u0438\u0441\u0430\u0442\u0438', imageCover:'\u0417\u0430\u043f\u043e\u0432\u043d\u0438\u0442\u0438', imageDrop:'\u041f\u0435\u0440\u0435\u0442\u044f\u0433\u043d\u0438 PNG/JPG/WebP \u0441\u044e\u0434\u0438 \u0430\u0431\u043e \u043d\u0430\u0442\u0438\u0441\u043d\u0438', clearImage:'\u041f\u0440\u0438\u0431\u0440\u0430\u0442\u0438 \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0443' });
    Object.assign(i18n.en, { fonts:'Fonts', defaultFont:'Default', baseFont:'Base font', donorFont:'Donor font', amountFont:'Amount font', messageFont:'Message font', title1Font:'Title 1 font', title2Font:'Title 2 font', goalAmountFont:'Goal amount font', providersFont:'Providers font', lastDonationFont:'Last donation font', timerFont:'Timer font', titleFont:'Title font', detailsFont:'Details font', visualType:'Visual type', visualBar:'Regular bar', visualImageReveal:'Image: grayscale reveal', visualImageSilhouette:'Image: silhouette reveal', fillDirection:'Fill direction', horizontal:'Horizontal', vertical:'Vertical', barLength:'Bar length', barImage:'Bar image', decorImage:'Decor image', showDecorImage:'Show decor image', imageX:'Image X', imageY:'Image Y', imageWidth:'Image width', imageHeight:'Image height' });
    Object.assign(i18n.ru, { fonts:'\u0428\u0440\u0438\u0444\u0442\u044b', defaultFont:'\u041f\u043e \u0443\u043c\u043e\u043b\u0447\u0430\u043d\u0438\u044e', baseFont:'\u041e\u0441\u043d\u043e\u0432\u043d\u043e\u0439 \u0448\u0440\u0438\u0444\u0442', donorFont:'\u0428\u0440\u0438\u0444\u0442 \u0434\u043e\u043d\u0430\u0442\u0435\u0440\u0430', amountFont:'\u0428\u0440\u0438\u0444\u0442 \u0441\u0443\u043c\u043c\u044b', messageFont:'\u0428\u0440\u0438\u0444\u0442 \u0441\u043e\u043e\u0431\u0449\u0435\u043d\u0438\u044f', title1Font:'\u0428\u0440\u0438\u0444\u0442 \u0437\u0430\u0433\u043e\u043b\u043e\u0432\u043a\u0430 1', title2Font:'\u0428\u0440\u0438\u0444\u0442 \u0437\u0430\u0433\u043e\u043b\u043e\u0432\u043a\u0430 2', goalAmountFont:'\u0428\u0440\u0438\u0444\u0442 \u0441\u0443\u043c\u043c\u044b \u0446\u0435\u043b\u0438', providersFont:'\u0428\u0440\u0438\u0444\u0442 \u043f\u043b\u043e\u0449\u0430\u0434\u043e\u043a', lastDonationFont:'\u0428\u0440\u0438\u0444\u0442 \u043f\u043e\u0441\u043b\u0435\u0434\u043d\u0435\u0433\u043e \u0434\u043e\u043d\u0430\u0442\u0430', timerFont:'\u0428\u0440\u0438\u0444\u0442 \u0442\u0430\u0439\u043c\u0435\u0440\u0430', titleFont:'\u0428\u0440\u0438\u0444\u0442 \u0437\u0430\u0433\u043e\u043b\u043e\u0432\u043a\u0430', detailsFont:'\u0428\u0440\u0438\u0444\u0442 \u0434\u0435\u0442\u0430\u043b\u0435\u0439', visualType:'\u0422\u0438\u043f \u0432\u0438\u0437\u0443\u0430\u043b\u0430', visualBar:'\u041e\u0431\u044b\u0447\u043d\u0430\u044f \u043f\u043e\u043b\u043e\u0441\u0430', visualImageReveal:'\u041a\u0430\u0440\u0442\u0438\u043d\u043a\u0430: \u043f\u0440\u043e\u044f\u0432\u043b\u0435\u043d\u0438\u0435 \u0438\u0437 \u0447/\u0431', visualImageSilhouette:'\u041a\u0430\u0440\u0442\u0438\u043d\u043a\u0430: \u0441\u0438\u043b\u0443\u044d\u0442', fillDirection:'\u041d\u0430\u043f\u0440\u0430\u0432\u043b\u0435\u043d\u0438\u0435 \u0437\u0430\u043f\u043e\u043b\u043d\u0435\u043d\u0438\u044f', horizontal:'\u0413\u043e\u0440\u0438\u0437\u043e\u043d\u0442\u0430\u043b\u044c\u043d\u043e', vertical:'\u0412\u0435\u0440\u0442\u0438\u043a\u0430\u043b\u044c\u043d\u043e', barLength:'\u0414\u043b\u0438\u043d\u0430 \u043f\u043e\u043b\u043e\u0441\u044b', barImage:'\u041a\u0430\u0440\u0442\u0438\u043d\u043a\u0430 \u0446\u0435\u043b\u0438', decorImage:'\u0414\u0435\u043a\u043e\u0440\u0430\u0442\u0438\u0432\u043d\u0430\u044f \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0430', showDecorImage:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0434\u0435\u043a\u043e\u0440\u0430\u0442\u0438\u0432\u043d\u0443\u044e \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0443', imageX:'\u041a\u0430\u0440\u0442\u0438\u043d\u043a\u0430 X', imageY:'\u041a\u0430\u0440\u0442\u0438\u043d\u043a\u0430 Y', imageWidth:'\u0428\u0438\u0440\u0438\u043d\u0430 \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0438', imageHeight:'\u0412\u044b\u0441\u043e\u0442\u0430 \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0438' });
    Object.assign(i18n.uk, { fonts:'\u0428\u0440\u0438\u0444\u0442\u0438', defaultFont:'\u0417\u0430 \u0437\u0430\u043c\u043e\u0432\u0447\u0443\u0432\u0430\u043d\u043d\u044f\u043c', baseFont:'\u041e\u0441\u043d\u043e\u0432\u043d\u0438\u0439 \u0448\u0440\u0438\u0444\u0442', donorFont:'\u0428\u0440\u0438\u0444\u0442 \u0434\u043e\u043d\u0430\u0442\u0435\u0440\u0430', amountFont:'\u0428\u0440\u0438\u0444\u0442 \u0441\u0443\u043c\u0438', messageFont:'\u0428\u0440\u0438\u0444\u0442 \u043f\u043e\u0432\u0456\u0434\u043e\u043c\u043b\u0435\u043d\u043d\u044f', title1Font:'\u0428\u0440\u0438\u0444\u0442 \u0437\u0430\u0433\u043e\u043b\u043e\u0432\u043a\u0430 1', title2Font:'\u0428\u0440\u0438\u0444\u0442 \u0437\u0430\u0433\u043e\u043b\u043e\u0432\u043a\u0430 2', goalAmountFont:'\u0428\u0440\u0438\u0444\u0442 \u0441\u0443\u043c\u0438 \u0446\u0456\u043b\u0456', providersFont:'\u0428\u0440\u0438\u0444\u0442 \u043f\u043b\u0430\u0442\u0444\u043e\u0440\u043c', lastDonationFont:'\u0428\u0440\u0438\u0444\u0442 \u043e\u0441\u0442\u0430\u043d\u043d\u044c\u043e\u0433\u043e \u0434\u043e\u043d\u0430\u0442\u0443', timerFont:'\u0428\u0440\u0438\u0444\u0442 \u0442\u0430\u0439\u043c\u0435\u0440\u0430', titleFont:'\u0428\u0440\u0438\u0444\u0442 \u0437\u0430\u0433\u043e\u043b\u043e\u0432\u043a\u0430', detailsFont:'\u0428\u0440\u0438\u0444\u0442 \u0434\u0435\u0442\u0430\u043b\u0435\u0439', visualType:'\u0422\u0438\u043f \u0432\u0456\u0437\u0443\u0430\u043b\u0443', visualBar:'\u0417\u0432\u0438\u0447\u0430\u0439\u043d\u0430 \u0441\u043c\u0443\u0433\u0430', visualImageReveal:'\u041a\u0430\u0440\u0442\u0438\u043d\u043a\u0430: \u043f\u0440\u043e\u044f\u0432\u043b\u0435\u043d\u043d\u044f \u0437 \u0447/\u0431', visualImageSilhouette:'\u041a\u0430\u0440\u0442\u0438\u043d\u043a\u0430: \u0441\u0438\u043b\u0443\u0435\u0442', fillDirection:'\u041d\u0430\u043f\u0440\u044f\u043c \u0437\u0430\u043f\u043e\u0432\u043d\u0435\u043d\u043d\u044f', horizontal:'\u0413\u043e\u0440\u0438\u0437\u043e\u043d\u0442\u0430\u043b\u044c\u043d\u043e', vertical:'\u0412\u0435\u0440\u0442\u0438\u043a\u0430\u043b\u044c\u043d\u043e', barLength:'\u0414\u043e\u0432\u0436\u0438\u043d\u0430 \u0441\u043c\u0443\u0433\u0438', barImage:'\u041a\u0430\u0440\u0442\u0438\u043d\u043a\u0430 \u0446\u0456\u043b\u0456', decorImage:'\u0414\u0435\u043a\u043e\u0440\u0430\u0442\u0438\u0432\u043d\u0430 \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0430', showDecorImage:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u0434\u0435\u043a\u043e\u0440\u0430\u0442\u0438\u0432\u043d\u0443 \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0443', imageX:'\u041a\u0430\u0440\u0442\u0438\u043d\u043a\u0430 X', imageY:'\u041a\u0430\u0440\u0442\u0438\u043d\u043a\u0430 Y', imageWidth:'\u0428\u0438\u0440\u0438\u043d\u0430 \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0438', imageHeight:'\u0412\u0438\u0441\u043e\u0442\u0430 \u043a\u0430\u0440\u0442\u0438\u043d\u043a\u0438' });
    Object.assign(i18n.en, { visualImageTransparent:'Image: transparent reveal', visualImageInverse:'Image: disappear on progress', timerWidth:'Timer width', timerX:'Timer X', timerY:'Timer Y', timerAlign:'Timer align' });
    Object.assign(i18n.ru, { visualImageTransparent:'\u041a\u0430\u0440\u0442\u0438\u043d\u043a\u0430: \u043f\u0440\u043e\u044f\u0432\u043b\u0435\u043d\u0438\u0435 \u0438\u0437 \u043f\u0440\u043e\u0437\u0440\u0430\u0447\u043d\u043e\u0441\u0442\u0438', visualImageInverse:'\u041a\u0430\u0440\u0442\u0438\u043d\u043a\u0430: \u0438\u0441\u0447\u0435\u0437\u0430\u043d\u0438\u0435 \u043f\u043e \u043c\u0435\u0440\u0435 \u0437\u0430\u043f\u043e\u043b\u043d\u0435\u043d\u0438\u044f', timerWidth:'\u0428\u0438\u0440\u0438\u043d\u0430 \u0442\u0430\u0439\u043c\u0435\u0440\u0430', timerX:'\u0422\u0430\u0439\u043c\u0435\u0440 X', timerY:'\u0422\u0430\u0439\u043c\u0435\u0440 Y', timerAlign:'\u0412\u044b\u0440\u0430\u0432\u043d\u0438\u0432\u0430\u043d\u0438\u0435 \u0442\u0430\u0439\u043c\u0435\u0440\u0430' });
    Object.assign(i18n.uk, { visualImageTransparent:'\u041a\u0430\u0440\u0442\u0438\u043d\u043a\u0430: \u043f\u0440\u043e\u044f\u0432\u043b\u0435\u043d\u043d\u044f \u0437 \u043f\u0440\u043e\u0437\u043e\u0440\u043e\u0441\u0442\u0456', visualImageInverse:'\u041a\u0430\u0440\u0442\u0438\u043d\u043a\u0430: \u0437\u043d\u0438\u043a\u043d\u0435\u043d\u043d\u044f \u0437\u0430 \u043c\u0456\u0440\u043e\u044e \u0437\u0430\u043f\u043e\u0432\u043d\u0435\u043d\u043d\u044f', timerWidth:'\u0428\u0438\u0440\u0438\u043d\u0430 \u0442\u0430\u0439\u043c\u0435\u0440\u0430', timerX:'\u0422\u0430\u0439\u043c\u0435\u0440 X', timerY:'\u0422\u0430\u0439\u043c\u0435\u0440 Y', timerAlign:'\u0412\u0438\u0440\u0456\u0432\u043d\u044e\u0432\u0430\u043d\u043d\u044f \u0442\u0430\u0439\u043c\u0435\u0440\u0430' });
    Object.assign(i18n.en, { leaderboard:'Leaderboard', leaderboardEditor:'Leaderboard editor', leaderboardLook:'Leaderboard look', enableLeaderboard:'Enable leaderboard', showTitle:'Show title', mode:'Mode', overallTop:'Overall top', platformSlides:'Platform slides', recentDonations:'Recent donations', rows:'Rows', slideDuration:'Slide duration, ms', showRanks:'Show ranks', showAmounts:'Show amounts', showPlatforms:'Show platforms', amountFont:'Amount font', rowGap:'Row gap', clearLeaderboard:'Clear leaderboard data', leaderboardCleared:'Leaderboard data cleared' });
    Object.assign(i18n.ru, { leaderboard:'\u041b\u0438\u0434\u0435\u0440\u0431\u043e\u0440\u0434', leaderboardEditor:'\u0420\u0435\u0434\u0430\u043a\u0442\u043e\u0440 \u043b\u0438\u0434\u0435\u0440\u0431\u043e\u0440\u0434\u0430', leaderboardLook:'\u0412\u0438\u0434 \u043b\u0438\u0434\u0435\u0440\u0431\u043e\u0440\u0434\u0430', enableLeaderboard:'\u0412\u043a\u043b\u044e\u0447\u0438\u0442\u044c \u043b\u0438\u0434\u0435\u0440\u0431\u043e\u0440\u0434', showTitle:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0437\u0430\u0433\u043e\u043b\u043e\u0432\u043e\u043a', mode:'\u0420\u0435\u0436\u0438\u043c', overallTop:'\u041e\u0431\u0449\u0438\u0439 \u0442\u043e\u043f', platformSlides:'\u0421\u043b\u0430\u0439\u0434\u044b \u043f\u043e \u043f\u043b\u043e\u0449\u0430\u0434\u043a\u0430\u043c', recentDonations:'\u041f\u043e\u0441\u043b\u0435\u0434\u043d\u0438\u0435 \u0434\u043e\u043d\u0430\u0442\u044b', rows:'\u0421\u0442\u0440\u043e\u043a\u0438', slideDuration:'\u0414\u043b\u0438\u0442\u0435\u043b\u044c\u043d\u043e\u0441\u0442\u044c \u0441\u043b\u0430\u0439\u0434\u0430, \u043c\u0441', showRanks:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u043c\u0435\u0441\u0442\u0430', showAmounts:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0441\u0443\u043c\u043c\u044b', showPlatforms:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u043f\u043b\u043e\u0449\u0430\u0434\u043a\u0438', amountFont:'\u0428\u0440\u0438\u0444\u0442 \u0441\u0443\u043c\u043c\u044b', rowGap:'\u041e\u0442\u0441\u0442\u0443\u043f \u043c\u0435\u0436\u0434\u0443 \u0441\u0442\u0440\u043e\u043a\u0430\u043c\u0438', clearLeaderboard:'\u041e\u0447\u0438\u0441\u0442\u0438\u0442\u044c \u0434\u0430\u043d\u043d\u044b\u0435 \u043b\u0438\u0434\u0435\u0440\u0431\u043e\u0440\u0434\u0430', leaderboardCleared:'\u0414\u0430\u043d\u043d\u044b\u0435 \u043b\u0438\u0434\u0435\u0440\u0431\u043e\u0440\u0434\u0430 \u043e\u0447\u0438\u0449\u0435\u043d\u044b' });
    Object.assign(i18n.uk, { leaderboard:'\u041b\u0456\u0434\u0435\u0440\u0431\u043e\u0440\u0434', leaderboardEditor:'\u0420\u0435\u0434\u0430\u043a\u0442\u043e\u0440 \u043b\u0456\u0434\u0435\u0440\u0431\u043e\u0440\u0434\u0443', leaderboardLook:'\u0412\u0438\u0433\u043b\u044f\u0434 \u043b\u0456\u0434\u0435\u0440\u0431\u043e\u0440\u0434\u0443', enableLeaderboard:'\u0423\u0432\u0456\u043c\u043a\u043d\u0443\u0442\u0438 \u043b\u0456\u0434\u0435\u0440\u0431\u043e\u0440\u0434', showTitle:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u0437\u0430\u0433\u043e\u043b\u043e\u0432\u043e\u043a', mode:'\u0420\u0435\u0436\u0438\u043c', overallTop:'\u0417\u0430\u0433\u0430\u043b\u044c\u043d\u0438\u0439 \u0442\u043e\u043f', platformSlides:'\u0421\u043b\u0430\u0439\u0434\u0438 \u0437\u0430 \u043f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430\u043c\u0438', recentDonations:'\u041e\u0441\u0442\u0430\u043d\u043d\u0456 \u0434\u043e\u043d\u0430\u0442\u0438', rows:'\u0420\u044f\u0434\u043a\u0438', slideDuration:'\u0422\u0440\u0438\u0432\u0430\u043b\u0456\u0441\u0442\u044c \u0441\u043b\u0430\u0439\u0434\u0443, \u043c\u0441', showRanks:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u043c\u0456\u0441\u0446\u044f', showAmounts:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u0441\u0443\u043c\u0438', showPlatforms:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u043f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0438', amountFont:'\u0428\u0440\u0438\u0444\u0442 \u0441\u0443\u043c\u0438', rowGap:'\u0412\u0456\u0434\u0441\u0442\u0443\u043f \u043c\u0456\u0436 \u0440\u044f\u0434\u043a\u0430\u043c\u0438', clearLeaderboard:'\u041e\u0447\u0438\u0441\u0442\u0438\u0442\u0438 \u0434\u0430\u043d\u0456 \u043b\u0456\u0434\u0435\u0440\u0431\u043e\u0440\u0434\u0443', leaderboardCleared:'\u0414\u0430\u043d\u0456 \u043b\u0456\u0434\u0435\u0440\u0431\u043e\u0440\u0434\u0443 \u043e\u0447\u0438\u0449\u0435\u043d\u043e' });
    Object.assign(i18n.en, { creditsSectionTitle:'Donation section title', showMessages:'Show messages' });
    Object.assign(i18n.ru, { creditsSectionTitle:'\u0417\u0430\u0433\u043e\u043b\u043e\u0432\u043e\u043a \u0441\u0435\u043a\u0446\u0438\u0438 \u0434\u043e\u043d\u0430\u0442\u043e\u0432', showMessages:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0441\u043e\u043e\u0431\u0449\u0435\u043d\u0438\u044f' });
    Object.assign(i18n.uk, { creditsSectionTitle:'\u0417\u0430\u0433\u043e\u043b\u043e\u0432\u043e\u043a \u0441\u0435\u043a\u0446\u0456\u0457 \u0434\u043e\u043d\u0430\u0442\u0456\u0432', showMessages:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u043f\u043e\u0432\u0456\u0434\u043e\u043c\u043b\u0435\u043d\u043d\u044f' });
    Object.assign(i18n.en, { alertMedia:'Alert media library', alertRules:'Amount rules', alertAnimations:'Alert animations', platform:'Platform', platformFont:'Platform font', showPlatform:'Show platform', defaultVisual:'Default visual', alertSound:'Alert sound', textSound:'Text animation sound', visualFit:'Visual fit', visualWidth:'Visual width', visualHeight:'Visual height', visualX:'Visual X', visualY:'Visual Y', muteVideo:'Mute video audio', showBackground:'Show background', showAccentBar:'Show accent bar', visibleDuration:'Visible duration, ms', entryAnimation:'Entry animation', exitAnimation:'Exit animation', donorTextAnimation:'Donor text animation', alertVolume:'Alert volume', textSoundVolume:'Text sound volume', addRule:'Add amount rule', deleteFile:'Delete', uploaded:'Media library updated', deleted:'Media file deleted' });
    Object.assign(i18n.ru, { alertMedia:'\u041c\u0435\u0434\u0438\u0430\u0442\u0435\u043a\u0430 \u0430\u043b\u0451\u0440\u0442\u043e\u0432', alertRules:'\u041f\u0440\u0430\u0432\u0438\u043b\u0430 \u043f\u043e \u0441\u0443\u043c\u043c\u0430\u043c', alertAnimations:'\u0410\u043d\u0438\u043c\u0430\u0446\u0438\u0438 \u0430\u043b\u0451\u0440\u0442\u0430', platform:'\u041f\u043b\u043e\u0449\u0430\u0434\u043a\u0430', platformFont:'\u0428\u0440\u0438\u0444\u0442 \u043f\u043b\u043e\u0449\u0430\u0434\u043a\u0438', showPlatform:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u043f\u043b\u043e\u0449\u0430\u0434\u043a\u0443', defaultVisual:'\u0412\u0438\u0437\u0443\u0430\u043b \u043f\u043e \u0443\u043c\u043e\u043b\u0447\u0430\u043d\u0438\u044e', alertSound:'\u0417\u0432\u0443\u043a \u0430\u043b\u0451\u0440\u0442\u0430', textSound:'\u0417\u0432\u0443\u043a \u0430\u043d\u0438\u043c\u0430\u0446\u0438\u0438 \u0442\u0435\u043a\u0441\u0442\u0430', visualFit:'\u0412\u043f\u0438\u0441\u044b\u0432\u0430\u043d\u0438\u0435 \u0432\u0438\u0437\u0443\u0430\u043b\u0430', visualWidth:'\u0428\u0438\u0440\u0438\u043d\u0430 \u0432\u0438\u0437\u0443\u0430\u043b\u0430', visualHeight:'\u0412\u044b\u0441\u043e\u0442\u0430 \u0432\u0438\u0437\u0443\u0430\u043b\u0430', visualX:'\u0412\u0438\u0437\u0443\u0430\u043b X', visualY:'\u0412\u0438\u0437\u0443\u0430\u043b Y', muteVideo:'\u0412\u044b\u043a\u043b\u044e\u0447\u0438\u0442\u044c \u0437\u0432\u0443\u043a \u0432\u0438\u0434\u0435\u043e', showBackground:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0444\u043e\u043d', showAccentBar:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0430\u043a\u0446\u0435\u043d\u0442\u043d\u0443\u044e \u043f\u043e\u043b\u043e\u0441\u0443', visibleDuration:'\u0412\u0440\u0435\u043c\u044f \u043f\u043e\u043a\u0430\u0437\u0430, \u043c\u0441', entryAnimation:'\u0410\u043d\u0438\u043c\u0430\u0446\u0438\u044f \u043f\u043e\u044f\u0432\u043b\u0435\u043d\u0438\u044f', exitAnimation:'\u0410\u043d\u0438\u043c\u0430\u0446\u0438\u044f \u0438\u0441\u0447\u0435\u0437\u043d\u043e\u0432\u0435\u043d\u0438\u044f', donorTextAnimation:'\u0410\u043d\u0438\u043c\u0430\u0446\u0438\u044f \u0438\u043c\u0435\u043d\u0438 \u0434\u043e\u043d\u0430\u0442\u0435\u0440\u0430', alertVolume:'\u0413\u0440\u043e\u043c\u043a\u043e\u0441\u0442\u044c \u0430\u043b\u0451\u0440\u0442\u0430', textSoundVolume:'\u0413\u0440\u043e\u043c\u043a\u043e\u0441\u0442\u044c \u0437\u0432\u0443\u043a\u0430 \u0442\u0435\u043a\u0441\u0442\u0430', addRule:'\u0414\u043e\u0431\u0430\u0432\u0438\u0442\u044c \u043f\u0440\u0430\u0432\u0438\u043b\u043e', deleteFile:'\u0423\u0434\u0430\u043b\u0438\u0442\u044c', uploaded:'\u041c\u0435\u0434\u0438\u0430\u0442\u0435\u043a\u0430 \u043e\u0431\u043d\u043e\u0432\u043b\u0435\u043d\u0430', deleted:'\u0424\u0430\u0439\u043b \u0443\u0434\u0430\u043b\u0451\u043d' });
    Object.assign(i18n.uk, { alertMedia:'\u041c\u0435\u0434\u0456\u0430\u0442\u0435\u043a\u0430 \u0430\u043b\u0435\u0440\u0442\u0456\u0432', alertRules:'\u041f\u0440\u0430\u0432\u0438\u043b\u0430 \u0437\u0430 \u0441\u0443\u043c\u0430\u043c\u0438', alertAnimations:'\u0410\u043d\u0456\u043c\u0430\u0446\u0456\u0457 \u0430\u043b\u0435\u0440\u0442\u0443', platform:'\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430', platformFont:'\u0428\u0440\u0438\u0444\u0442 \u043f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0438', showPlatform:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u043f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0443', defaultVisual:'\u0412\u0456\u0437\u0443\u0430\u043b \u0437\u0430 \u0437\u0430\u043c\u043e\u0432\u0447\u0443\u0432\u0430\u043d\u043d\u044f\u043c', alertSound:'\u0417\u0432\u0443\u043a \u0430\u043b\u0435\u0440\u0442\u0443', textSound:'\u0417\u0432\u0443\u043a \u0430\u043d\u0456\u043c\u0430\u0446\u0456\u0457 \u0442\u0435\u043a\u0441\u0442\u0443', visualFit:'\u0412\u043f\u0438\u0441\u0443\u0432\u0430\u043d\u043d\u044f \u0432\u0456\u0437\u0443\u0430\u043b\u0443', visualWidth:'\u0428\u0438\u0440\u0438\u043d\u0430 \u0432\u0456\u0437\u0443\u0430\u043b\u0443', visualHeight:'\u0412\u0438\u0441\u043e\u0442\u0430 \u0432\u0456\u0437\u0443\u0430\u043b\u0443', visualX:'\u0412\u0456\u0437\u0443\u0430\u043b X', visualY:'\u0412\u0456\u0437\u0443\u0430\u043b Y', muteVideo:'\u0412\u0438\u043c\u043a\u043d\u0443\u0442\u0438 \u0437\u0432\u0443\u043a \u0432\u0456\u0434\u0435\u043e', showBackground:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u0444\u043e\u043d', showAccentBar:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u0430\u043a\u0446\u0435\u043d\u0442\u043d\u0443 \u0441\u043c\u0443\u0433\u0443', visibleDuration:'\u0427\u0430\u0441 \u043f\u043e\u043a\u0430\u0437\u0443, \u043c\u0441', entryAnimation:'\u0410\u043d\u0456\u043c\u0430\u0446\u0456\u044f \u043f\u043e\u044f\u0432\u0438', exitAnimation:'\u0410\u043d\u0456\u043c\u0430\u0446\u0456\u044f \u0437\u043d\u0438\u043a\u043d\u0435\u043d\u043d\u044f', donorTextAnimation:'\u0410\u043d\u0456\u043c\u0430\u0446\u0456\u044f \u0456\u043c\u0435\u043d\u0456 \u0434\u043e\u043d\u0430\u0442\u0435\u0440\u0430', alertVolume:'\u0413\u0443\u0447\u043d\u0456\u0441\u0442\u044c \u0430\u043b\u0435\u0440\u0442\u0443', textSoundVolume:'\u0413\u0443\u0447\u043d\u0456\u0441\u0442\u044c \u0437\u0432\u0443\u043a\u0443 \u0442\u0435\u043a\u0441\u0442\u0443', addRule:'\u0414\u043e\u0434\u0430\u0442\u0438 \u043f\u0440\u0430\u0432\u0438\u043b\u043e', deleteFile:'\u0412\u0438\u0434\u0430\u043b\u0438\u0442\u0438', uploaded:'\u041c\u0435\u0434\u0456\u0430\u0442\u0435\u043a\u0443 \u043e\u043d\u043e\u0432\u043b\u0435\u043d\u043e', deleted:'\u0424\u0430\u0439\u043b \u0432\u0438\u0434\u0430\u043b\u0435\u043d\u043e' });
    Object.assign(i18n.en, { ruleName:'Name', minAmount:'Minimum', maxAmount:'Maximum, 0 = unlimited', randomVariants:'Random variants', visualVariants:'Visual variants', soundVariants:'Sound variants', removeRule:'Remove rule' });
    Object.assign(i18n.ru, { ruleName:'\u041d\u0430\u0437\u0432\u0430\u043d\u0438\u0435', minAmount:'\u041c\u0438\u043d\u0438\u043c\u0443\u043c', maxAmount:'\u041c\u0430\u043a\u0441\u0438\u043c\u0443\u043c, 0 = \u0431\u0435\u0437 \u043b\u0438\u043c\u0438\u0442\u0430', randomVariants:'\u0421\u043b\u0443\u0447\u0430\u0439\u043d\u044b\u0435 \u0432\u0430\u0440\u0438\u0430\u043d\u0442\u044b', visualVariants:'\u0412\u0430\u0440\u0438\u0430\u043d\u0442\u044b \u043a\u0430\u0440\u0442\u0438\u043d\u043e\u043a', soundVariants:'\u0412\u0430\u0440\u0438\u0430\u043d\u0442\u044b \u0437\u0432\u0443\u043a\u043e\u0432', removeRule:'\u0423\u0434\u0430\u043b\u0438\u0442\u044c \u043f\u0440\u0430\u0432\u0438\u043b\u043e' });
    Object.assign(i18n.uk, { ruleName:'\u041d\u0430\u0437\u0432\u0430', minAmount:'\u041c\u0456\u043d\u0456\u043c\u0443\u043c', maxAmount:'\u041c\u0430\u043a\u0441\u0438\u043c\u0443\u043c, 0 = \u0431\u0435\u0437 \u043b\u0456\u043c\u0456\u0442\u0443', randomVariants:'\u0412\u0438\u043f\u0430\u0434\u043a\u043e\u0432\u0456 \u0432\u0430\u0440\u0456\u0430\u043d\u0442\u0438', visualVariants:'\u0412\u0430\u0440\u0456\u0430\u043d\u0442\u0438 \u0437\u043e\u0431\u0440\u0430\u0436\u0435\u043d\u044c', soundVariants:'\u0412\u0430\u0440\u0456\u0430\u043d\u0442\u0438 \u0437\u0432\u0443\u043a\u0456\u0432', removeRule:'\u0412\u0438\u0434\u0430\u043b\u0438\u0442\u0438 \u043f\u0440\u0430\u0432\u0438\u043b\u043e' });
    Object.assign(i18n.en, { filter:'Blocked', customAlert:'Custom test alert', timerPresets:'Timer presets', creditsPresets:'Credits presets', leaderboardPresets:'Leaderboard presets', leaderboardEntries:'Leaderboard entries', blockedEditor:'Blocked names and words', mediaPlacement:'Visual placement', textAlign:'Text align', donorX:'Donor X', donorY:'Donor Y', amountX:'Amount X', amountY:'Amount Y', messageX:'Message X', messageY:'Message Y', timerMode:'Timer mode', timerConversion:'Show conversion line', timerServices:'Show providers in timer', lastDonationSize:'Last donation size', lastDonationAlign:'Last donation align', rollSeconds:'Base roll duration, seconds', lockCreditsSpeed:'Keep fixed speed for long credits', useNativeCredits:'Use Streamer.bot Credits data', blockedNames:'Blocked nicknames', blockedWords:'Blocked words', replacementName:'Replacement nickname', replacementText:'Replacement text', openMedia:'Open media folder', loadTestCredits:'Load Streamer.bot test credits', sendCustomAlert:'Send custom alert', addLeaderboardEntry:'Add manual row' });
    Object.assign(i18n.ru, { filter:'\u0417\u0430\u043f\u0440\u0435\u0442', customAlert:'\u041a\u0430\u0441\u0442\u043e\u043c\u043d\u044b\u0439 \u0442\u0435\u0441\u0442 \u0430\u043b\u0451\u0440\u0442\u0430', timerPresets:'\u041f\u0440\u0435\u0441\u0435\u0442\u044b \u0442\u0430\u0439\u043c\u0435\u0440\u0430', creditsPresets:'\u041f\u0440\u0435\u0441\u0435\u0442\u044b \u0442\u0438\u0442\u0440\u043e\u0432', leaderboardPresets:'\u041f\u0440\u0435\u0441\u0435\u0442\u044b \u043b\u0438\u0434\u0435\u0440\u0431\u043e\u0440\u0434\u0430', leaderboardEntries:'\u0421\u0442\u0440\u043e\u043a\u0438 \u043b\u0438\u0434\u0435\u0440\u0431\u043e\u0440\u0434\u0430', blockedEditor:'\u0417\u0430\u043f\u0440\u0435\u0442\u043d\u044b\u0435 \u043d\u0438\u043a\u0438 \u0438 \u0441\u043b\u043e\u0432\u0430', mediaPlacement:'\u0420\u0430\u0441\u043f\u043e\u043b\u043e\u0436\u0435\u043d\u0438\u0435 \u0432\u0438\u0437\u0443\u0430\u043b\u0430', textAlign:'\u0412\u044b\u0440\u0430\u0432\u043d\u0438\u0432\u0430\u043d\u0438\u0435 \u0442\u0435\u043a\u0441\u0442\u0430', donorX:'\u0414\u043e\u043d\u0430\u0442\u0435\u0440 X', donorY:'\u0414\u043e\u043d\u0430\u0442\u0435\u0440 Y', amountX:'\u0421\u0443\u043c\u043c\u0430 X', amountY:'\u0421\u0443\u043c\u043c\u0430 Y', messageX:'\u0421\u043e\u043e\u0431\u0449\u0435\u043d\u0438\u0435 X', messageY:'\u0421\u043e\u043e\u0431\u0449\u0435\u043d\u0438\u0435 Y', timerMode:'\u0420\u0435\u0436\u0438\u043c \u0442\u0430\u0439\u043c\u0435\u0440\u0430', timerConversion:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0441\u0442\u0440\u043e\u043a\u0443 \u043a\u043e\u043d\u0432\u0435\u0440\u0442\u0430\u0446\u0438\u0438', timerServices:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u043f\u043b\u043e\u0449\u0430\u0434\u043a\u0438 \u0432 \u0442\u0430\u0439\u043c\u0435\u0440\u0435', lastDonationSize:'\u0420\u0430\u0437\u043c\u0435\u0440 \u0441\u0442\u0440\u043e\u043a\u0438 \u0434\u043e\u043d\u0430\u0442\u0430', lastDonationAlign:'\u041f\u043e\u0437\u0438\u0446\u0438\u044f \u0441\u0442\u0440\u043e\u043a\u0438 \u0434\u043e\u043d\u0430\u0442\u0430', rollSeconds:'\u0411\u0430\u0437\u043e\u0432\u0430\u044f \u0434\u043b\u0438\u0442\u0435\u043b\u044c\u043d\u043e\u0441\u0442\u044c, \u0441\u0435\u043a\u0443\u043d\u0434\u044b', lockCreditsSpeed:'\u041d\u0435 \u0443\u0441\u043a\u043e\u0440\u044f\u0442\u044c \u0434\u043b\u0438\u043d\u043d\u044b\u0435 \u0442\u0438\u0442\u0440\u044b', useNativeCredits:'\u0411\u0440\u0430\u0442\u044c \u0434\u0430\u043d\u043d\u044b\u0435 Credits \u0438\u0437 Streamer.bot', blockedNames:'\u0417\u0430\u043f\u0440\u0435\u0442\u043d\u044b\u0435 \u043d\u0438\u043a\u0438', blockedWords:'\u0417\u0430\u043f\u0440\u0435\u0442\u043d\u044b\u0435 \u0441\u043b\u043e\u0432\u0430', replacementName:'\u0417\u0430\u043c\u0435\u043d\u0430 \u043d\u0438\u043a\u0430', replacementText:'\u0417\u0430\u043c\u0435\u043d\u0430 \u0442\u0435\u043a\u0441\u0442\u0430', openMedia:'\u041e\u0442\u043a\u0440\u044b\u0442\u044c \u043f\u0430\u043f\u043a\u0443 \u043c\u0435\u0434\u0438\u0430\u0442\u0435\u043a\u0438', loadTestCredits:'\u0417\u0430\u0433\u0440\u0443\u0437\u0438\u0442\u044c \u0442\u0435\u0441\u0442\u043e\u0432\u044b\u0435 \u0442\u0438\u0442\u0440\u044b Streamer.bot', sendCustomAlert:'\u041e\u0442\u043f\u0440\u0430\u0432\u0438\u0442\u044c \u043a\u0430\u0441\u0442\u043e\u043c\u043d\u044b\u0439 \u0430\u043b\u0451\u0440\u0442', addLeaderboardEntry:'\u0414\u043e\u0431\u0430\u0432\u0438\u0442\u044c \u0441\u0442\u0440\u043e\u043a\u0443 \u0432\u0440\u0443\u0447\u043d\u0443\u044e' });
    Object.assign(i18n.uk, { filter:'\u0417\u0430\u0431\u043e\u0440\u043e\u043d\u0430', customAlert:'\u0412\u043b\u0430\u0441\u043d\u0438\u0439 \u0442\u0435\u0441\u0442 \u0430\u043b\u0435\u0440\u0442\u0443', timerPresets:'\u041f\u0440\u0435\u0441\u0435\u0442\u0438 \u0442\u0430\u0439\u043c\u0435\u0440\u0430', creditsPresets:'\u041f\u0440\u0435\u0441\u0435\u0442\u0438 \u0442\u0438\u0442\u0440\u0456\u0432', leaderboardPresets:'\u041f\u0440\u0435\u0441\u0435\u0442\u0438 \u043b\u0456\u0434\u0435\u0440\u0431\u043e\u0440\u0434\u0443', leaderboardEntries:'\u0420\u044f\u0434\u043a\u0438 \u043b\u0456\u0434\u0435\u0440\u0431\u043e\u0440\u0434\u0443', blockedEditor:'\u0417\u0430\u0431\u043e\u0440\u043e\u043d\u0435\u043d\u0456 \u043d\u0456\u043a\u0438 \u0442\u0430 \u0441\u043b\u043e\u0432\u0430', mediaPlacement:'\u0420\u043e\u0437\u0442\u0430\u0448\u0443\u0432\u0430\u043d\u043d\u044f \u0432\u0456\u0437\u0443\u0430\u043b\u0443', textAlign:'\u0412\u0438\u0440\u0456\u0432\u043d\u044e\u0432\u0430\u043d\u043d\u044f \u0442\u0435\u043a\u0441\u0442\u0443', donorX:'\u0414\u043e\u043d\u0430\u0442\u0435\u0440 X', donorY:'\u0414\u043e\u043d\u0430\u0442\u0435\u0440 Y', amountX:'\u0421\u0443\u043c\u0430 X', amountY:'\u0421\u0443\u043c\u0430 Y', messageX:'\u041f\u043e\u0432\u0456\u0434\u043e\u043c\u043b\u0435\u043d\u043d\u044f X', messageY:'\u041f\u043e\u0432\u0456\u0434\u043e\u043c\u043b\u0435\u043d\u043d\u044f Y', timerMode:'\u0420\u0435\u0436\u0438\u043c \u0442\u0430\u0439\u043c\u0435\u0440\u0430', timerConversion:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u0440\u044f\u0434\u043e\u043a \u043a\u043e\u043d\u0432\u0435\u0440\u0442\u0430\u0446\u0456\u0457', timerServices:'\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u0438 \u043f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0438 \u0432 \u0442\u0430\u0439\u043c\u0435\u0440\u0456', lastDonationSize:'\u0420\u043e\u0437\u043c\u0456\u0440 \u0440\u044f\u0434\u043a\u0430 \u0434\u043e\u043d\u0430\u0442\u0443', lastDonationAlign:'\u041f\u043e\u0437\u0438\u0446\u0456\u044f \u0440\u044f\u0434\u043a\u0430 \u0434\u043e\u043d\u0430\u0442\u0443', rollSeconds:'\u0411\u0430\u0437\u043e\u0432\u0430 \u0442\u0440\u0438\u0432\u0430\u043b\u0456\u0441\u0442\u044c, \u0441\u0435\u043a\u0443\u043d\u0434\u0438', lockCreditsSpeed:'\u041d\u0435 \u043f\u0440\u0438\u0441\u043a\u043e\u0440\u044e\u0432\u0430\u0442\u0438 \u0434\u043e\u0432\u0433\u0456 \u0442\u0438\u0442\u0440\u0438', useNativeCredits:'\u0411\u0440\u0430\u0442\u0438 \u0434\u0430\u043d\u0456 Credits \u0437 Streamer.bot', blockedNames:'\u0417\u0430\u0431\u043e\u0440\u043e\u043d\u0435\u043d\u0456 \u043d\u0456\u043a\u0438', blockedWords:'\u0417\u0430\u0431\u043e\u0440\u043e\u043d\u0435\u043d\u0456 \u0441\u043b\u043e\u0432\u0430', replacementName:'\u0417\u0430\u043c\u0456\u043d\u0430 \u043d\u0456\u043a\u0430', replacementText:'\u0417\u0430\u043c\u0456\u043d\u0430 \u0442\u0435\u043a\u0441\u0442\u0443', openMedia:'\u0412\u0456\u0434\u043a\u0440\u0438\u0442\u0438 \u043f\u0430\u043f\u043a\u0443 \u043c\u0435\u0434\u0456\u0430\u0442\u0435\u043a\u0438', loadTestCredits:'\u0417\u0430\u0432\u0430\u043d\u0442\u0430\u0436\u0438\u0442\u0438 \u0442\u0435\u0441\u0442\u043e\u0432\u0456 \u0442\u0438\u0442\u0440\u0438 Streamer.bot', sendCustomAlert:'\u041d\u0430\u0434\u0456\u0441\u043b\u0430\u0442\u0438 \u0432\u043b\u0430\u0441\u043d\u0438\u0439 \u0430\u043b\u0435\u0440\u0442', addLeaderboardEntry:'\u0414\u043e\u0434\u0430\u0442\u0438 \u0440\u044f\u0434\u043e\u043a \u0432\u0440\u0443\u0447\u043d\u0443' });
    Object.assign(i18n.en, { recommendedObsSize:'Recommended OBS Browser Source size: ', mediaDrop:'Drop PNG/JPG/GIF/MP4/WebM/MP3/WAV here or click', noVisual:'No visual', noSound:'No sound', noTextSound:'No text sound', previewFile:'Preview', builtIn:'built-in', name:'Name', saveRow:'Save row', deleteRow:'Delete row', timerNote:'Example: amount 100 and seconds 3600 means 100 RUB = 60 min.', servicesNote:'Only enabled providers are shown. Clear a checkbox to hide a provider from Goal.', creditsNote:'Streamer.bot HTTP Server must be enabled on 127.0.0.1:7474. DonConnect falls back to local examples if it is unavailable.', leaderboardNote:'Add a custom row or edit names from received donations. Deleting a row automatically moves the next place up.', filterNote:'One item per line. The filter changes browser widgets only and keeps original Streamer.bot donation variables untouched.', aboveText:'Above text', belowText:'Below text', leftText:'Left of text', rightText:'Right of text', behindText:'Behind text', contain:'Contain', cover:'Cover', countdownMode:'Countdown: donations add time', countupMode:'Count up: reset to zero on event' });
    Object.assign(i18n.ru, { recommendedObsSize:'Рекомендуемый размер Browser Source в OBS: ', mediaDrop:'Перетащите PNG/JPG/GIF/MP4/WebM/MP3/WAV сюда или нажмите', noVisual:'Без визуала', noSound:'Без звука', noTextSound:'Без звука текста', previewFile:'Просмотр', builtIn:'встроенный', name:'Имя', saveRow:'Сохранить строку', deleteRow:'Удалить строку', timerNote:'Пример: сумма 100 и 3600 секунд означают 100 RUB = 60 мин.', servicesNote:'Показываются только включенные площадки. Снимите галочку, чтобы скрыть площадку из цели.', creditsNote:'В Streamer.bot должен быть включен HTTP Server на 127.0.0.1:7474. Если он недоступен, DonConnect покажет локальные примеры.', leaderboardNote:'Добавьте строку вручную или исправьте имя из полученного доната. После удаления строки следующее место поднимется автоматически.', filterNote:'Один ник или слово на строку. Фильтр меняет только браузерные виджеты и сохраняет исходные переменные доната Streamer.bot.', aboveText:'Над текстом', belowText:'Под текстом', leftText:'Слева от текста', rightText:'Справа от текста', behindText:'За текстом', contain:'Вписать', cover:'Заполнить', countdownMode:'Обратный отсчет: донаты добавляют время', countupMode:'Отсчет вперед: событие сбрасывает таймер до нуля' });
    Object.assign(i18n.uk, { recommendedObsSize:'Рекомендований розмір Browser Source в OBS: ', mediaDrop:'Перетягніть PNG/JPG/GIF/MP4/WebM/MP3/WAV сюди або натисніть', noVisual:'Без візуалу', noSound:'Без звуку', noTextSound:'Без звуку тексту', previewFile:'Перегляд', builtIn:'вбудований', name:'Імʼя', saveRow:'Зберегти рядок', deleteRow:'Видалити рядок', timerNote:'Приклад: сума 100 і 3600 секунд означають 100 RUB = 60 хв.', servicesNote:'Показуються лише увімкнені платформи. Зніміть позначку, щоб приховати платформу з цілі.', creditsNote:'У Streamer.bot має бути увімкнений HTTP Server на 127.0.0.1:7474. Якщо він недоступний, DonConnect покаже локальні приклади.', leaderboardNote:'Додайте рядок вручну або виправте імʼя з отриманого донату. Після видалення рядка наступне місце підніметься автоматично.', filterNote:'Один нік або слово на рядок. Фільтр змінює лише браузерні віджети та зберігає початкові змінні донату Streamer.bot.', aboveText:'Над текстом', belowText:'Під текстом', leftText:'Ліворуч від тексту', rightText:'Праворуч від тексту', behindText:'За текстом', contain:'Вписати', cover:'Заповнити', countdownMode:'Зворотний відлік: донати додають час', countupMode:'Відлік уперед: подія скидає таймер до нуля' });
    Object.assign(i18n.en, { history:'Repeats', recentDonationsTitle:'Recent donations', noRecentDonations:'No recent donations yet', replay:'Replay', addFont:'Add font', customFontPlaceholder:'Installed font name', fontAdded:'Font added to the selectors', timerCustomTest:'Timer custom test', sendTimerTest:'Send timer test', creditsSections:'Credits sections', transparentBackground:'Transparent background', pauseCredits:'Pause credits preview', resumeCredits:'Resume credits preview', restartCredits:'Restart credits preview', hiddenSectionNote:'Clear a checkbox to hide a section from Streamer.bot Credits.', filterTestDonation:'Blocked test donation', sendFilterTest:'Send blocked test', donationVoice:'Donation voice', testSpeech:'Test voice', speechHint:'Windows reads donation text from DonConnect itself. Pick a voice, enable reading, then press Test voice.', speechTestStarted:'Voice test finished', speechTestFailed:'Voice test failed', speakDonation:'Read donation text aloud', speechVoice:'Voice', speechRate:'Voice speed', speechPitch:'Voice pitch', speechVolume:'Voice volume', defaultVoice:'Default Windows voice', lastTenOnly:'Only the latest 10 rows are shown here.' });
    Object.assign(i18n.ru, { history:'Повторы', recentDonationsTitle:'Последние донаты', noRecentDonations:'Последних донатов пока нет', replay:'Повторить', addFont:'Добавить шрифт', customFontPlaceholder:'Название установленного шрифта', fontAdded:'Шрифт добавлен в списки выбора', timerCustomTest:'Кастомный тест таймера', sendTimerTest:'Отправить тест таймера', creditsSections:'Секции титров', transparentBackground:'Прозрачный фон', pauseCredits:'Пауза титров', resumeCredits:'Продолжить титры', restartCredits:'Перезапустить титры', hiddenSectionNote:'Снимите галочку, чтобы скрыть секцию из титров Streamer.bot.', filterTestDonation:'Кастомный тест запрета', sendFilterTest:'Отправить тест запрета', donationVoice:'Озвучка доната', testSpeech:'Проверить голос', speechHint:'Текст доната зачитывается голосом Windows прямо из DonConnect. Выбери голос, включи озвучку и нажми проверку.', speechTestStarted:'Проверка голоса выполнена', speechTestFailed:'Проверка голоса не сработала', speakDonation:'Зачитывать текст доната', speechVoice:'Голос', speechRate:'Скорость голоса', speechPitch:'Тон голоса', speechVolume:'Громкость голоса', defaultVoice:'Голос Windows по умолчанию', lastTenOnly:'Здесь показываются только последние 10 строк.' });
    Object.assign(i18n.uk, { history:'Повтори', recentDonationsTitle:'Останні донати', noRecentDonations:'Останніх донатів поки немає', replay:'Повторити', addFont:'Додати шрифт', customFontPlaceholder:'Назва встановленого шрифту', fontAdded:'Шрифт додано до списків вибору', timerCustomTest:'Власний тест таймера', sendTimerTest:'Надіслати тест таймера', creditsSections:'Секції титрів', transparentBackground:'Прозорий фон', pauseCredits:'Пауза титрів', resumeCredits:'Продовжити титри', restartCredits:'Перезапустити титри', hiddenSectionNote:'Зніміть позначку, щоб приховати секцію з титрів Streamer.bot.', filterTestDonation:'Власний тест заборони', sendFilterTest:'Надіслати тест заборони', donationVoice:'Озвучення донату', testSpeech:'Перевірити голос', speechHint:'Текст донату читається голосом Windows прямо з DonConnect. Обери голос, увімкни озвучення і натисни перевірку.', speechTestStarted:'Перевірку голосу виконано', speechTestFailed:'Перевірка голосу не спрацювала', speakDonation:'Зачитувати текст донату', speechVoice:'Голос', speechRate:'Швидкість голосу', speechPitch:'Тон голосу', speechVolume:'Гучність голосу', defaultVoice:'Голос Windows за замовчуванням', lastTenOnly:'Тут показуються лише останні 10 рядків.' });
    Object.assign(i18n.en, { speechReadDonor:'Read donor name', speechReadAmount:'Read amount', speechReadPlatform:'Read platform', speechReadMessage:'Read message' });
    Object.assign(i18n.ru, { speechReadDonor:'Зачитывать имя донатера', speechReadAmount:'Зачитывать сумму', speechReadPlatform:'Зачитывать площадку', speechReadMessage:'Зачитывать сообщение' });
    Object.assign(i18n.uk, { speechReadDonor:'Зачитувати імʼя донатера', speechReadAmount:'Зачитувати суму', speechReadPlatform:'Зачитувати платформу', speechReadMessage:'Зачитувати повідомлення' });
    Object.assign(i18n.en, { donationAmountStep:'Amount that gives one time step', secondsPerStep:'Seconds added for that amount' });
    Object.assign(i18n.ru, { donationAmountStep:'Сумма, за которую добавляется шаг времени', secondsPerStep:'Сколько секунд добавить за эту сумму' });
    Object.assign(i18n.uk, { donationAmountStep:'Сума, за яку додається крок часу', secondsPerStep:'Скільки секунд додати за цю суму' });
    Object.assign(i18n.en, { obsDock:'OBS Dock', history:'OBS Dock', dockNote:'Compact OBS dock with recent donations. Replay only repeats the alert and does not add money to goal, timer, credits or leaderboard.', deleteDonation:'Delete', donationDeleted:'Donation removed from dock history', timerVisibility:'Timer visibility', timerManualNote:'Add time manually when a donation came outside DonConnect. It affects only the timer.', timerTimeAdded:'Timer time added', sendTimerTest:'Add timer time' });
    Object.assign(i18n.ru, { obsDock:'Док OBS', history:'Док OBS', dockNote:'Компактная док-панель OBS с последними донатами. Повтор запускает только алёрт и не добавляет деньги в цель, таймер, титры или лидерборд.', deleteDonation:'Удалить', donationDeleted:'Донат удален из истории дока', timerVisibility:'Что показывать в таймере', timerManualNote:'Добавь время вручную, если донат пришел не через DonConnect. Это влияет только на таймер.', timerTimeAdded:'Время добавлено в таймер', sendTimerTest:'Добавить время' });
    Object.assign(i18n.uk, { obsDock:'Док OBS', history:'Док OBS', dockNote:'Компактна док-панель OBS з останніми донатами. Повтор запускає лише алерт і не додає гроші в ціль, таймер, титри або лідерборд.', deleteDonation:'Видалити', donationDeleted:'Донат видалено з історії дока', timerVisibility:'Що показувати в таймері', timerManualNote:'Додай час вручну, якщо донат прийшов не через DonConnect. Це впливає лише на таймер.', timerTimeAdded:'Час додано в таймер', sendTimerTest:'Додати час' });
    boot();
    async function boot() {
      try {
        donation = Object.assign({}, donationDefaults, await fetchJson('/donconnect/api/settings', {}));
        overlay = Object.assign({}, overlayDefaults, await fetchJson('/donconnect/api/overlay-settings', {}));
        credits = Object.assign({}, creditsDefaults, await fetchJson('/donconnect/api/credits-settings', {}));
        leaderboard = Object.assign({}, leaderboardDefaults, await fetchJson('/donconnect/api/leaderboard-settings', {}));
        contentFilter = Object.assign({}, filterDefaults, await fetchJson('/donconnect/api/content-filter-settings', {}));
        fonts = normalizeFontCatalog(await fetchJson('/donconnect/api/fonts', {}));
        speechVoices = await fetchJson('/donconnect/api/speech-voices', speechVoices);
        alertMedia = await fetchJson('/donconnect/api/alert-media', alertMedia);
      } catch (error) {
        console.error(error);
        fonts = cloneFontCatalog(fallbackFonts);
        contentFilter = Object.assign({}, filterDefaults);
      }
      setHtml('donationPresets', presetButtons(donationPresets, 'preset'));
      setHtml('goalPresets', presetButtons(goalPresets, 'goal-preset'));
      setHtml('timerPresets', presetButtons(timerPresets, 'timer-preset'));
      setHtml('creditsPresets', presetButtons(creditsPresets, 'credits-preset'));
      setHtml('leaderboardPresets', presetButtons(leaderboardPresets, 'leaderboard-preset'));
      safeRun(initLanguage);
      safeRun(populateFontList);
      safeRun(populateSpeechVoices);
      safeRun(renderAlertMediaLibrary);
      safeRun(renderAlertRules);
      safeRun(renderServiceToggles);
      safeRun(loadCreditsSections);
      safeRun(loadRecentDonations);
      safeRun(bind);
      safeRun(bindAlertRuleControls);
      safeRun(bindAlertMediaUpload);
      safeRun(() => bindImageUpload('goalDrop', 'goalImageFile', 'clearGoalImage', 'GoalImageDataUrl', 'GoalImageName', 'ShowGoalImage'));
      safeRun(() => bindImageUpload('decorDrop', 'decorImageFile', 'clearDecorImage', 'DecorImageDataUrl', 'DecorImageName', 'ShowDecorImage'));
      safeRun(enableSections);
      safeRun(hideBaseFontRows);
      safeRun(translate);
      safeRun(fillAll);
      safeRun(loadLeaderboardEntries);
      safeRun(() => switchTab('donation'));
      safeRun(sendPreview);
    }
    function presetButtons(presets, key) { return Object.keys(presets).map(name => `<button data-${key}=""${name}"">${name}</button>`).join(''); }
    async function fetchJson(url, fallback) { try { const response = await fetch(url, { cache:'no-store' }); return response && response.ok ? await response.json() : fallback; } catch (error) { console.error(error); return fallback; } }
    function safeRun(fn) { try { return fn(); } catch (error) { console.error(error); showStatus('Editor error: ' + (error && error.message ? error.message : error)); return null; } }
    function setHtml(id, html) { const el = document.getElementById(id); if (el) el.innerHTML = html; }
    function bind() {
      const languageSelect = document.getElementById('languageSelect');
      if (languageSelect) {
        const handler = () => setLanguage(languageSelect.value);
        languageSelect.addEventListener('change', handler);
        languageSelect.addEventListener('input', handler);
      }
      document.querySelectorAll('[data-tab]').forEach(btn => btn.addEventListener('click', () => switchTab(btn.dataset.tab)));
      bindLive('[data-donation]', 'donation');
      bindLive('[data-overlay]', 'overlay');
      bindLive('[data-credits]', 'credits');
      bindLive('[data-leaderboard]', 'leaderboard');
      bindLive('[data-filter]', 'filter');
      bindPreset('preset', donationPresets, value => donation = Object.assign({}, donation, value, { Language:lang }));
      bindPreset('goalPreset', goalPresets, value => overlay = Object.assign({}, overlay, value));
      bindPreset('timerPreset', timerPresets, value => overlay = Object.assign({}, overlay, value));
      bindPreset('creditsPreset', creditsPresets, (value, name) => credits = Object.assign({}, credits, value, creditsPresetText(lang, name)));
      bindPreset('leaderboardPreset', leaderboardPresets, value => leaderboard = Object.assign({}, leaderboard, value));
      document.querySelectorAll('[data-test]').forEach(btn => btn.addEventListener('click', () => testDonation(btn.dataset.test)));
      const saveButton = document.getElementById('save'); if (saveButton) saveButton.addEventListener('click', save);
      const resetButton = document.getElementById('reset'); if (resetButton) resetButton.addEventListener('click', resetAndSave);
      const resetLeaderboard = document.getElementById('resetLeaderboardData'); if (resetLeaderboard) resetLeaderboard.addEventListener('click', resetLeaderboardData);
      const addLeaderboard = document.getElementById('addLeaderboardEntry'); if (addLeaderboard) addLeaderboard.addEventListener('click', addLeaderboardEntry);
      const openMedia = document.getElementById('openAlertMedia'); if (openMedia) openMedia.addEventListener('click', openAlertMedia);
      const customTest = document.getElementById('sendCustomTest'); if (customTest) customTest.addEventListener('click', sendCustomTest);
      const timerTest = document.getElementById('sendTimerTest'); if (timerTest) timerTest.addEventListener('click', sendTimerTest);
      const filterTest = document.getElementById('sendFilterTest'); if (filterTest) filterTest.addEventListener('click', sendFilterTest);
      const creditsTest = document.getElementById('testCredits'); if (creditsTest) creditsTest.addEventListener('click', testCredits);
      const pauseCredits = document.getElementById('pauseCredits'); if (pauseCredits) pauseCredits.addEventListener('click', toggleCreditsPause);
      const restartCredits = document.getElementById('restartCredits'); if (restartCredits) restartCredits.addEventListener('click', restartCreditsPreview);
      const testSpeechButton = document.getElementById('testSpeech'); if (testSpeechButton) testSpeechButton.addEventListener('click', testSpeech);
      if (window.speechSynthesis) window.speechSynthesis.onvoiceschanged = populateSpeechVoices;
      const copyButton = document.getElementById('copy'); if (copyButton) copyButton.addEventListener('click', copyObs);
      const copyButton2 = document.getElementById('copy2'); if (copyButton2) copyButton2.addEventListener('click', copyObs);
      const copyDockButton = document.getElementById('copyDockUrl'); if (copyDockButton) copyDockButton.addEventListener('click', copyObs);
    }
    function bindPreset(key, presets, apply) { document.querySelectorAll(`[data-${camelToDash(key)}]`).forEach(btn => btn.addEventListener('click', () => { const name = btn.dataset[key]; apply(Object.assign({}, presets[name] || {}), name); fillAll(); sendPreview(); })); }
    function creditsPresetText(language, name) { const packs = { en:{ standard:{ Title:'Thanks for watching', Subtitle:'Today with us', Outro:'See you next stream', SectionTitle:'Donations' }, party:{ Title:'What a stream!', Subtitle:'The wonderful people who made it happen', Outro:'See you next time!', SectionTitle:'Donation heroes' } }, ru:{ standard:{ Title:'Спасибо за стрим', Subtitle:'Сегодня с нами', Outro:'До следующего стрима', SectionTitle:'Донаты' }, party:{ Title:'Вот это был стрим!', Subtitle:'Люди, которые сделали этот эфир', Outro:'До скорой встречи!', SectionTitle:'Герои донатов' } }, uk:{ standard:{ Title:'Дякую за стрім', Subtitle:'Сьогодні з нами', Outro:'До наступного стріму', SectionTitle:'Донати' }, party:{ Title:'Оце був стрім!', Subtitle:'Люди, які зробили цей ефір', Outro:'До зустрічі!', SectionTitle:'Герої донатів' } } }; const pack = packs[normalizeLanguage(language)] || packs.en; return Object.assign({}, name === 'Pixel Party' ? pack.party : pack.standard); }
    function camelToDash(value) { return String(value).replace(/[A-Z]/g, ch => '-' + ch.toLowerCase()); }
    function bindLive(selector, name) { document.querySelectorAll(selector).forEach(el => { const handler = () => update(name === 'donation' ? donation : name === 'overlay' ? overlay : name === 'credits' ? credits : name === 'leaderboard' ? leaderboard : contentFilter, el, name); el.addEventListener('input', handler); el.addEventListener('change', handler); }); }
    function enableSections() { document.querySelectorAll('fieldset').forEach(fieldset => { fieldset.classList.add('collapsed'); const legend = fieldset.querySelector('legend'); if (!legend) return; legend.addEventListener('click', event => { if (event.target.closest('button,input,select,textarea,label')) return; fieldset.classList.toggle('collapsed'); }); }); }
    let creditsPaused = false;
    let statusTimer = null;
    function populateFontList() { fonts = normalizeFontCatalog(fonts); const selects = Array.from(document.querySelectorAll('[data-font-select]')); if (!selects.length) return; const windows = uniqueFonts(fonts.windows || []); const google = uniqueFonts(fonts.google || []); const options = '<option value="""">' + t('defaultFont') + '</option>' + fontGroup('Windows', windows) + fontGroup('Google Fonts', google); selects.forEach(select => { const value = select.value; select.innerHTML = options; ensureFontOption(select, value); select.value = value || ''; }); hideBaseFontRows(); }
    function hideBaseFontRows() { ['[data-donation=FontFamily]','[data-overlay=FontFamily]','[data-credits=FontFamily]','[data-leaderboard=FontFamily]'].forEach(selector => document.querySelectorAll(selector).forEach(input => { const label = input.closest('label'); if (label) label.style.display = 'none'; })); }
    function populateSpeechVoices() { const select = document.getElementById('speechVoiceSelect'); if (!select) return; const selected = donation.SpeechVoice || select.value || ''; const server = Array.isArray(speechVoices && speechVoices.items) ? speechVoices.items : []; const seen = new Set(); const all = server.filter(voice => { const name = String(voice && voice.name || '').trim(); const key = name.toLowerCase(); if (!name || seen.has(key)) return false; seen.add(key); return true; }); select.innerHTML = '<option value="""">' + escapeHtml(t('defaultVoice')) + '</option>' + all.map(voice => '<option value=""' + escapeAttr(voice.name) + '"">' + escapeHtml(voice.name + (voice.lang ? ' - ' + voice.lang : '') + (voice.source ? ' (' + voice.source + ')' : '')) + '</option>').join(''); ensureFontOption(select, selected); select.value = selected || ''; }
    async function testSpeech() { donation.SpeakDonation = true; sync('[data-donation=SpeakDonation]', true); const result = await post('/donconnect/api/speech-test', donation); if (result && result.ok) showStatus(t('speechTestStarted') + (result.engine ? ' (' + result.engine + ')' : '')); else showStatus(t('speechTestFailed') + ': ' + ((result && result.error) || 'unknown')); }
    function normalizeFontCatalog(source) { return { windows: uniqueFonts([...(fallbackFonts.windows || []), ...fontArray(source && source.windows)]), google: uniqueFonts([...(fallbackFonts.google || []), ...fontArray(source && source.google)]) }; }
    function fontArray(value) { if (Array.isArray(value)) return value; if (!value) return []; return [value]; }
    function cloneFontCatalog(source) { return { windows:[...((source && source.windows) || [])], google:[...((source && source.google) || [])] }; }
    function uniqueFonts(items) { const seen = new Set(); return items.map(name => String(name || '').trim()).filter(name => name && !seen.has(name.toLowerCase()) && seen.add(name.toLowerCase())); }
    function fontGroup(label, items) { return items.length ? '<optgroup label=""' + escapeAttr(label) + '"">' + items.map(name => '<option value=""' + escapeAttr(name) + '"">' + escapeHtml(name) + '</option>').join('') + '</optgroup>' : ''; }
    function ensureFontOption(select, value) { const name = String(value || '').trim(); if (!name || Array.from(select.options).some(option => option.value === name)) return; const option = document.createElement('option'); option.value = name; option.textContent = name; select.appendChild(option); }
    function bindImageUpload(dropId, inputId, clearId, dataKey, nameKey, showKey) { const drop = document.getElementById(dropId); const fileInput = document.getElementById(inputId); const clear = document.getElementById(clearId); if (!drop || !fileInput) return; drop.addEventListener('click', () => fileInput.click()); drop.addEventListener('dragover', event => { event.preventDefault(); drop.classList.add('drag'); }); drop.addEventListener('dragleave', () => drop.classList.remove('drag')); drop.addEventListener('drop', event => { event.preventDefault(); drop.classList.remove('drag'); const file = event.dataTransfer.files && event.dataTransfer.files[0]; if (file) readImageFile(file, dataKey, nameKey, showKey); }); fileInput.addEventListener('change', () => { const file = fileInput.files && fileInput.files[0]; if (file) readImageFile(file, dataKey, nameKey, showKey); fileInput.value = ''; }); if (clear) clear.addEventListener('click', () => { overlay[dataKey] = ''; overlay[nameKey] = ''; overlay[showKey] = false; fillAll(); sendPreview(); }); }
    function readImageFile(file, dataKey, nameKey, showKey) { if (!file || !/^image\//.test(file.type)) return; const reader = new FileReader(); reader.onload = () => { overlay[dataKey] = String(reader.result || ''); overlay[nameKey] = file.name || ''; overlay[showKey] = true; fillAll(); sendPreview(); }; reader.readAsDataURL(file); }
    function bindAlertMediaUpload() { const drop = document.getElementById('alertMediaDrop'); const input = document.getElementById('alertMediaFile'); if (!drop || !input) return; drop.addEventListener('click', () => input.click()); drop.addEventListener('dragover', event => { event.preventDefault(); drop.classList.add('drag'); }); drop.addEventListener('dragleave', () => drop.classList.remove('drag')); drop.addEventListener('drop', event => { event.preventDefault(); drop.classList.remove('drag'); uploadAlertFiles(event.dataTransfer.files); }); input.addEventListener('change', () => { uploadAlertFiles(input.files); input.value = ''; }); }
    async function uploadAlertFiles(files) { for (const file of Array.from(files || [])) { if (!file || file.size > Number(alertMedia.maxUploadBytes || 33554432)) { showStatus('File is too large. Maximum: 32 MB'); continue; } try { const data = await readFileDataUrl(file); alertMedia = await post('/donconnect/api/alert-media-upload', { name:file.name, data }); showStatus(t('uploaded')); } catch (error) { showStatus(error && error.message ? error.message : String(error)); } } renderAlertMediaLibrary(); renderAlertRules(); fillAll(); sendPreview(); }
    function readFileDataUrl(file) { return new Promise((resolve, reject) => { const reader = new FileReader(); reader.onload = () => resolve(String(reader.result || '')); reader.onerror = () => reject(reader.error || new Error('File read failed')); reader.readAsDataURL(file); }); }
    async function deleteAlertMedia(file) { try { alertMedia = await post('/donconnect/api/alert-media-delete', { file }); pruneAlertMedia(file); renderAlertMediaLibrary(); renderAlertRules(); fillAll(); sendPreview(); showStatus(t('deleted')); } catch (error) { showStatus(error && error.message ? error.message : String(error)); } }
    async function openAlertMedia() { try { const result = await post('/donconnect/api/alert-media-open', {}); showStatus(result.directory || alertMedia.directory || 'Media folder opened'); } catch (error) { showStatus(error && error.message ? error.message : String(error)); } }
    function previewAlertMedia(file, kind) { const url = mediaUrl(file); if (kind === 'audio') { try { const audio = new Audio(url); audio.volume = .8; audio.play().catch(() => {}); } catch {} return; } window.open(url, '_blank', 'noopener,noreferrer'); }
    function mediaUrl(file) { return '/donconnect/media/' + String(file || '').split('/').map(encodeURIComponent).join('/'); }
    function pruneAlertMedia(file) { if (donation.MediaFile === file) donation.MediaFile = ''; if (donation.SoundFile === file) donation.SoundFile = ''; if (donation.TextSoundFile === file) donation.TextSoundFile = ''; (donation.AlertRules || []).forEach(rule => { rule.MediaFiles = (rule.MediaFiles || []).filter(value => value !== file); rule.SoundFiles = (rule.SoundFiles || []).filter(value => value !== file); }); }
    function renderAlertMediaLibrary() { const items = Array.isArray(alertMedia && alertMedia.items) ? alertMedia.items : []; const visuals = items.filter(item => item.kind === 'image' || item.kind === 'video'); const sounds = items.filter(item => item.kind === 'audio'); setOptions('alertMediaSelect', visuals, donation.MediaFile, t('noVisual')); setOptions('alertSoundSelect', sounds, donation.SoundFile, t('noSound')); setOptions('alertTextSoundSelect', sounds, donation.TextSoundFile, t('noTextSound')); const path = document.getElementById('alertMediaPath'); if (path) path.textContent = alertMedia.directory || ''; const grid = document.getElementById('alertMediaGrid'); if (!grid) return; grid.innerHTML = items.map(item => `<div class='media-item'><div class='media-thumb'>${item.kind === 'image' ? `<img loading='lazy' src='${escapeAttr(item.url)}' alt=''>` : escapeHtml(item.kind === 'audio' ? 'AUDIO' : 'VIDEO')}</div><span>${escapeHtml(item.name || item.file)}<small>${escapeHtml(item.kind || '')} | ${formatBytes(item.bytes || 0)}${item.builtin ? ' | ' + escapeHtml(t('builtIn')) : ''}</small></span><div class='media-actions'><button type='button' data-preview-alert-media='${escapeAttr(item.file)}' data-kind='${escapeAttr(item.kind)}'>${escapeHtml(t('previewFile'))}</button><button type='button' data-delete-alert-media='${escapeAttr(item.file)}'>${escapeHtml(t('deleteFile'))}</button></div></div>`).join(''); grid.querySelectorAll('[data-delete-alert-media]').forEach(button => button.addEventListener('click', () => deleteAlertMedia(button.dataset.deleteAlertMedia))); grid.querySelectorAll('[data-preview-alert-media]').forEach(button => button.addEventListener('click', () => previewAlertMedia(button.dataset.previewAlertMedia, button.dataset.kind))); }
    function setOptions(id, items, selected, emptyText) { const select = document.getElementById(id); if (!select) return; select.innerHTML = `<option value=''>${escapeHtml(emptyText)}</option>` + items.map(item => `<option value='${escapeAttr(item.file)}'>${escapeHtml(item.name || item.file)}</option>`).join(''); select.value = selected || ''; }
    function formatBytes(bytes) { const value = Number(bytes || 0); return value < 1024 * 1024 ? Math.max(1, Math.round(value / 1024)) + ' KB' : (value / 1024 / 1024).toFixed(1) + ' MB'; }
    function renderAlertRules() { const box = document.getElementById('alertRules'); if (!box) return; if (!Array.isArray(donation.AlertRules)) donation.AlertRules = []; box.innerHTML = donation.AlertRules.map((rule, index) => `<div class='rule-card' data-rule-index='${index}'><label><span>${escapeHtml(t('ruleName'))}</span><input data-rule-key='Name' type='text' value='${escapeAttr(rule.Name || '')}'></label><div class='rule-range'><label><span>${escapeHtml(t('minAmount'))}</span><input data-rule-key='MinAmount' type='number' min='0' step='0.01' value='${escapeAttr(rule.MinAmount ?? 0)}'></label><label><span>${escapeHtml(t('maxAmount'))}</span><input data-rule-key='MaxAmount' type='number' min='0' step='0.01' value='${escapeAttr(rule.MaxAmount ?? 0)}'></label></div><label class='check-row'><input data-rule-key='Randomize' type='checkbox' ${rule.Randomize === false ? '' : 'checked'}><span>${escapeHtml(t('randomVariants'))}</span></label><label><span>${escapeHtml(t('visualVariants'))}</span><select data-rule-key='MediaFiles' multiple>${ruleOptions('visual', rule.MediaFiles)}</select></label><label><span>${escapeHtml(t('soundVariants'))}</span><select data-rule-key='SoundFiles' multiple>${ruleOptions('audio', rule.SoundFiles)}</select></label><button type='button' data-remove-rule='${index}'>${escapeHtml(t('removeRule'))}</button></div>`).join(''); box.querySelectorAll('[data-remove-rule]').forEach(button => button.addEventListener('click', () => { donation.AlertRules.splice(Number(button.dataset.removeRule), 1); renderAlertRules(); sendPreview(); })); }
    function ruleOptions(kind, selected) { const values = Array.isArray(selected) ? selected : []; const items = (alertMedia.items || []).filter(item => kind === 'audio' ? item.kind === 'audio' : item.kind === 'image' || item.kind === 'video'); return items.map(item => `<option value='${escapeAttr(item.file)}' ${values.includes(item.file) ? 'selected' : ''}>${escapeHtml(item.name || item.file)}</option>`).join(''); }
    function bindAlertRuleControls() { const add = document.getElementById('addAlertRule'); const box = document.getElementById('alertRules'); if (add) add.addEventListener('click', () => { if (!Array.isArray(donation.AlertRules)) donation.AlertRules = []; donation.AlertRules.push({ Id:'rule-' + Date.now(), Name:'Amount rule', MinAmount:500, MaxAmount:0, Randomize:true, MediaFiles:[], SoundFiles:[] }); renderAlertRules(); sendPreview(); }); if (box) { const handler = event => { const card = event.target.closest('[data-rule-index]'); const key = event.target.dataset.ruleKey; if (!card || !key) return; const rule = donation.AlertRules[Number(card.dataset.ruleIndex)]; if (!rule) return; rule[key] = event.target.multiple ? Array.from(event.target.selectedOptions).map(option => option.value) : event.target.type === 'checkbox' ? event.target.checked : event.target.type === 'number' ? Number(event.target.value || 0) : event.target.value; sendPreview(); }; box.addEventListener('input', handler); box.addEventListener('change', handler); } }
    function switchTab(tab) {
      active = urls[tab] ? tab : 'donation';
      document.querySelectorAll('[data-tab]').forEach(b => b.classList.toggle('active', b.dataset.tab === active));
      document.querySelectorAll('[data-pane]').forEach(p => p.classList.toggle('active', p.dataset.pane === active));
      const url = location.origin + urls[active];
      document.getElementById('obsUrl').value = url;
      const dockInline = document.getElementById('dockUrlInline');
      if (dockInline) dockInline.value = location.origin + urls.history;
      const tests = document.getElementById('testsPanel');
      if (tests) tests.style.display = active === 'credits' || active === 'history' ? 'none' : '';
      const donationTests = document.getElementById('donationTestButtons');
      if (donationTests) donationTests.classList.toggle('hidden', active === 'timer');
      const timerTests = document.getElementById('timerTestPanel');
      if (timerTests) timerTests.classList.toggle('hidden', active !== 'timer');
      if (active === 'history') loadRecentDonations();
      if (active === 'credits') loadCreditsSections();
      const size = document.getElementById('obsSize');
      if (size) size.textContent = t('recommendedObsSize') + ({ donation:'1280 x 720', history:'360 x 600 OBS Dock', goal:'1280 x 520', timer:'1280 x 420', credits:'1920 x 1080', leaderboard:'1280 x 720', filter:'1280 x 720' }[active] || '1280 x 720');
      const previewUrl = document.getElementById('previewUrl');
      if (previewUrl) previewUrl.value = url;
      document.getElementById('frame').src = urls[active] + '?preview=1';
      setTimeout(sendPreview, 250);
    }
    function update(store, el, name) { const key = el.dataset[name]; store[key] = valueOf(el); sync(`[data-${name}=""${key}""]`, store[key]); sendPreview(); }
    function fillAll() { renderAlertMediaLibrary(); renderAlertRules(); renderServiceToggles(); populateSpeechVoices(); fill('donation', donation); fill('overlay', overlay); fill('credits', credits); fill('leaderboard', leaderboard); fill('filter', contentFilter); refreshGoalImageName(); renderCreditsSections(); hideBaseFontRows(); }
    function fill(name, store) { document.querySelectorAll(`[data-${name}]`).forEach(el => setValue(el, store[el.dataset[name]])); }
    function valueOf(el) { if (el.type === 'checkbox') return el.checked; return el.type === 'range' || el.type === 'number' ? numberValue(el) : el.value; }
    function setValue(el, value) { if (el.type === 'checkbox') el.checked = value === true || value === 'true'; else { if (el.matches && el.matches('[data-font-select]')) ensureFontOption(el, value); el.value = value ?? ''; } }
    function refreshGoalImageName() { const bar = document.getElementById('goalImageName'); if (bar) bar.textContent = overlay.GoalImageName || (overlay.GoalImageDataUrl ? 'Image selected' : ''); const decor = document.getElementById('decorImageName'); if (decor) decor.textContent = overlay.DecorImageName || (overlay.DecorImageDataUrl ? 'Image selected' : ''); }
    function numberValue(el) { return el.step && el.step.includes('.') ? Number(el.value) : parseInt(el.value || '0', 10); }
    function sync(selector, value) { document.querySelectorAll(selector).forEach(el => setValue(el, value)); }
    function sendPreview() { const frame = document.getElementById('frame'); if (!frame || !frame.contentWindow) return; if (active === 'donation' || active === 'history' || active === 'filter') frame.contentWindow.postMessage({ type:'settings', settings:donation }, location.origin); if (active === 'goal' || active === 'timer') frame.contentWindow.postMessage({ type:'overlay-settings', settings:overlay }, location.origin); if (active === 'credits') frame.contentWindow.postMessage({ type:'credits-settings', settings:credits }, location.origin); if (active === 'leaderboard') frame.contentWindow.postMessage({ type:'leaderboard-settings', settings:leaderboard }, location.origin); }
    async function save() { donation.Language = lang; if (active === 'donation' || active === 'history') donation = await post('/donconnect/api/settings', donation); if (active === 'goal' || active === 'timer') overlay = await post('/donconnect/api/overlay-settings', overlay); if (active === 'credits') credits = await post('/donconnect/api/credits-settings', credits); if (active === 'leaderboard') leaderboard = await post('/donconnect/api/leaderboard-settings', leaderboard); if (active === 'filter') contentFilter = await post('/donconnect/api/content-filter-settings', contentFilter); fillAll(); translate(); sendPreview(); showStatus(t('settingsSaved')); }
    async function post(url, data) { const response = await fetch(url, { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(data) }); const json = await response.json(); if (!response.ok) throw new Error(json && json.error ? json.error : 'Request failed'); return json; }
    function resetActive() { if (active === 'donation' || active === 'history') donation = Object.assign({}, donationDefaults, { Language:lang }); if (active === 'goal' || active === 'timer') { overlay = Object.assign({}, overlayDefaults); clearOverlayImages(); } if (active === 'credits') { credits = Object.assign({}, creditsDefaults); creditsPaused = false; updateCreditsPauseButton(); restartCreditsPreview(); } if (active === 'leaderboard') leaderboard = Object.assign({}, leaderboardDefaults); if (active === 'filter') contentFilter = Object.assign({}, filterDefaults); fillAll(); translate(); sendPreview(); }
    async function resetAndSave() { resetActive(); await save(); reloadPreview(); }
    function clearOverlayImages() { overlay.GoalImageDataUrl = ''; overlay.GoalImageName = ''; overlay.ShowGoalImage = false; overlay.DecorImageDataUrl = ''; overlay.DecorImageName = ''; overlay.ShowDecorImage = false; clearFileInput('goalImageFile'); clearFileInput('decorImageFile'); }
    function clearFileInput(id) { const input = document.getElementById(id); if (input) input.value = ''; }
    function reloadPreview() { const frame = document.getElementById('frame'); if (!frame || !urls[active]) return; frame.src = urls[active] + '?preview=1&t=' + Date.now(); setTimeout(sendPreview, 400); }
    async function testDonation(kind) { await fetch('/donconnect/api/test-donation', { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({ kind }) }); const goalState = await fetchJson('/donconnect/api/goal-state', null); if (goalState && goalState.goal) { overlay.GoalCurrent = goalState.goal.current; overlay.GoalCurrency = goalState.goal.currency || overlay.GoalCurrency; fillAll(); } await loadRecentDonations(); showStatus(t('testSent')); setTimeout(sendPreview, 300); }
    async function sendCustomTest() { const custom = { donor:value('customTestDonor'), amount:Number(value('customTestAmount') || 0), currency:value('customTestCurrency'), platform:value('customTestPlatform'), message:value('customTestMessage') }; await post('/donconnect/api/test-donation', { kind:'custom', custom }); await loadRecentDonations(); showStatus(t('testSent')); setTimeout(sendPreview, 300); }
    function value(id) { const el = document.getElementById(id); return el ? el.value : ''; }
    async function sendTimerTest() { overlay.TimerEnabled = true; sync('[data-overlay=TimerEnabled]', true); const custom = { donor:'Manual timer', amount:Number(value('timerTestAmount') || 0), currency:value('timerTestCurrency') || overlay.TimerCurrency || 'RUB', platform:'Manual timer', message:'' }; await post('/donconnect/api/timer-test', { settings:overlay, custom }); reloadPreview(); setTimeout(() => showStatus(t('timerTimeAdded') || 'Timer time added'), 50); }
    async function sendFilterTest() { contentFilter = await post('/donconnect/api/content-filter-settings', contentFilter); const custom = { donor:value('filterTestDonor') || 'BadNick', amount:Number(value('filterTestAmount') || 0), currency:value('filterTestCurrency') || 'RUB', platform:value('filterTestPlatform') || 'Filter test', message:value('filterTestMessage') || 'Bad word test' }; await post('/donconnect/api/test-donation', { kind:'custom', custom }); await loadRecentDonations(); showStatus(t('testSent')); if (active !== 'filter') switchTab('filter'); else reloadPreview(); }
    async function testCredits() { const result = await post('/donconnect/api/credits-test', {}); showStatus(result.message || 'Credits test loaded'); await loadCreditsSections(); reloadPreview(); }
    async function resetLeaderboardData() { await fetch('/donconnect/api/leaderboard-reset', { method:'POST' }); showStatus(t('leaderboardCleared')); await loadLeaderboardEntries(); reloadPreview(); }
    async function addLeaderboardEntry() { await post('/donconnect/api/leaderboard-entry', { action:'add', name:value('leaderboardEntryName') || 'Anonymous', amount:Number(value('leaderboardEntryAmount') || 0), currency:value('leaderboardEntryCurrency') || 'RUB', platform:value('leaderboardEntryPlatform') || 'Manual' }); await loadLeaderboardEntries(); reloadPreview(); }
    async function updateLeaderboardEntry(id, row) { await post('/donconnect/api/leaderboard-entry', { action:'update', id, name:row.querySelector('[data-entry-name]').value, amount:Number(row.querySelector('[data-entry-amount]').value || 0), currency:row.querySelector('[data-entry-currency]').value, platform:row.querySelector('[data-entry-platform]').value }); await loadLeaderboardEntries(); reloadPreview(); }
    async function deleteLeaderboardEntry(id) { await post('/donconnect/api/leaderboard-entry', { action:'delete', id }); await loadLeaderboardEntries(); reloadPreview(); }
    async function loadLeaderboardEntries() { const state = await fetchJson('/donconnect/api/leaderboard-state', {}); const box = document.getElementById('leaderboardEntries'); if (!box || !Array.isArray(state.data)) return; const rows = state.data.slice(0, 10); box.innerHTML = '<p class=""note"">' + escapeHtml(t('lastTenOnly')) + '</p>' + rows.map(item => `<div class='rule-card' data-entry-id='${escapeAttr(item.id || '')}'><div class='compact-grid'><label><span>${escapeHtml(t('name'))}</span><input data-entry-name type='text' value='${escapeAttr(item.name || '')}'></label><label><span>${escapeHtml(t('amount'))}</span><input data-entry-amount type='number' value='${escapeAttr(item.amount || 0)}'></label><label><span>${escapeHtml(t('currency'))}</span><input data-entry-currency type='text' value='${escapeAttr(item.currency || '')}'></label><label><span>${escapeHtml(t('platform'))}</span><input data-entry-platform type='text' value='${escapeAttr(item.platform || '')}'></label></div><div class='buttons'><button type='button' data-update-entry>${escapeHtml(t('saveRow'))}</button><button type='button' data-delete-entry>${escapeHtml(t('deleteRow'))}</button></div></div>`).join(''); box.querySelectorAll('[data-entry-id]').forEach(row => { row.querySelector('[data-update-entry]').addEventListener('click', () => updateLeaderboardEntry(row.dataset.entryId, row)); row.querySelector('[data-delete-entry]').addEventListener('click', () => deleteLeaderboardEntry(row.dataset.entryId)); }); }
    async function loadRecentDonations() { const box = document.getElementById('recentDonations'); if (!box) return; const result = await fetchJson('/donconnect/api/recent-donations', { items:[] }); const items = Array.isArray(result && result.items) ? result.items : []; if (!items.length) { box.innerHTML = '<p class=""note"">' + escapeHtml(t('noRecentDonations')) + '</p>'; return; } box.innerHTML = items.map(item => `<div class='media-item'><span><b>${escapeHtml(item.donor || 'Anonymous')}</b><small>${escapeHtml([item.amount, item.currency].filter(Boolean).join(' '))} | ${escapeHtml(item.provider || item.source || '')}</small><small>${escapeHtml(item.message || '')}</small></span><div class='media-actions'><button type='button' data-replay-donation='${escapeAttr(item.id || '')}'>${escapeHtml(t('replay'))}</button><button type='button' class='danger' data-delete-recent-donation='${escapeAttr(item.id || '')}'>${escapeHtml(t('deleteDonation'))}</button></div></div>`).join(''); box.querySelectorAll('[data-replay-donation]').forEach(button => button.addEventListener('click', () => replayDonation(button.dataset.replayDonation))); box.querySelectorAll('[data-delete-recent-donation]').forEach(button => button.addEventListener('click', () => deleteRecentDonation(button.dataset.deleteRecentDonation))); }
    async function replayDonation(id) { await post('/donconnect/api/replay-donation', { id }); showStatus(t('testSent')); setTimeout(sendPreview, 300); await loadRecentDonations(); }
    async function deleteRecentDonation(id) { await post('/donconnect/api/delete-recent-donation', { id }); showStatus(t('donationDeleted')); await loadRecentDonations(); setTimeout(sendPreview, 250); }
    async function loadCreditsSections() { const requestId = ++creditsSectionLoadId; const state = await fetchJson('/donconnect/api/credits-state', {}); if (requestId !== creditsSectionLoadId) return; creditsSectionCatalog = creditSectionNames(state); renderCreditsSections(); }
    function renderCreditsSections() { const box = document.getElementById('creditsSectionToggles'); if (!box) return; const names = creditsSectionCatalog.length ? creditsSectionCatalog : defaultCreditSectionNames(); if (!Array.isArray(credits.HiddenSections)) credits.HiddenSections = []; const hidden = new Set(credits.HiddenSections.map(name => String(name || '').toLowerCase())); box.innerHTML = names.map(name => `<label class='check-row'><input type='checkbox' data-credit-section='${escapeAttr(name)}' ${hidden.has(name.toLowerCase()) ? '' : 'checked'}><span>${escapeHtml(name)}</span></label>`).join(''); box.querySelectorAll('[data-credit-section]').forEach(input => input.addEventListener('change', () => { const name = input.dataset.creditSection; const key = String(name || '').toLowerCase(); const current = Array.isArray(credits.HiddenSections) ? credits.HiddenSections : []; credits.HiddenSections = input.checked ? current.filter(item => String(item || '').toLowerCase() !== key) : [...current.filter(item => String(item || '').toLowerCase() !== key), name]; sendPreview(); })); }
    function creditSectionNames(state) { const names = []; const add = name => { name = String(name || '').trim(); if (name && !names.some(item => item.toLowerCase() === name.toLowerCase())) names.push(name); }; if (state && Array.isArray(state.sections)) state.sections.forEach(add); defaultCreditSectionNames().forEach(add); return names; }
    function defaultCreditSectionNames() { return ['Follows','Cheers','Subs','ReSubs','Gift Subs','Gift Bombs','Raids','Reward Redemptions','Goal Contributions','Game Updates','Pyramids','Hype Trains','Hype Train Conductors','Hype Train Contributors','Editors','Moderators','Subscribers','VIPs','Users','Groups','All Bits','Month Bits','Week Bits','Channel Rewards', credits.SectionTitle || 'Donations']; }
    function prettyTitle(value) { return String(value || 'Credits').replace(/([a-z])([A-Z])/g, '$1 $2'); }
    function toggleCreditsPause() { setCreditsPaused(!creditsPaused); }
    function setCreditsPaused(value) { creditsPaused = !!value; updateCreditsPauseButton(); postCreditsControl(creditsPaused ? 'pause' : 'resume'); }
    function updateCreditsPauseButton() { const button = document.getElementById('pauseCredits'); if (button) button.textContent = creditsPaused ? t('resumeCredits') : t('pauseCredits'); }
    function restartCreditsPreview() { postCreditsControl('restart'); }
    function postCreditsControl(action) { const frame = document.getElementById('frame'); if (frame && frame.contentWindow) frame.contentWindow.postMessage({ type:'credits-control', action }, location.origin); }
    function renderServiceToggles() { const box = document.getElementById('serviceToggles'); if (!box) return; if (!Array.isArray(overlay.HiddenServices)) overlay.HiddenServices = []; box.innerHTML = serviceNames.map(name => `<label class='check-row'><input type='checkbox' data-hidden-service='${escapeAttr(name)}' ${overlay.HiddenServices.includes(name) ? '' : 'checked'}><span>${escapeHtml(name)}</span></label>`).join(''); box.querySelectorAll('[data-hidden-service]').forEach(input => input.addEventListener('change', () => { const name = input.dataset.hiddenService; overlay.HiddenServices = input.checked ? overlay.HiddenServices.filter(item => item !== name) : [...new Set([...overlay.HiddenServices, name])]; sendPreview(); })); }
    async function copyObs() { const input = document.getElementById('obsUrl'); const url = input ? input.value : location.origin + (urls[active] || urls.donation); try { await navigator.clipboard.writeText(url); showStatus(t('copied') + url); } catch { prompt(t('promptTitle'), url); } }
    function initLanguage() { const saved = localStorage.getItem('donconnectEditorLanguage'); setLanguage(saved || donation.Language || 'en', false); }
    function setLanguage(value, persist = true) { lang = normalizeLanguage(value); donation.Language = lang; const select = document.getElementById('languageSelect'); if (select) select.value = lang; if (persist) localStorage.setItem('donconnectEditorLanguage', lang); safeRun(translate); sendPreview(); }
    function normalizeLanguage(value) { const text = String(value || '').toLowerCase(); if (text.startsWith('ru')) return 'ru'; if (text.startsWith('uk') || text.startsWith('ua')) return 'uk'; return 'en'; }
    function t(key) { const pack = i18n[lang] || i18n.en; return pack[key] || i18n.en[key] || key; }
    function translate() {
      document.documentElement.lang = lang;
      const languageSelect = document.getElementById('languageSelect');
      if (languageSelect) languageSelect.value = lang;
      setText('header h1', t('appTitle'));
      setText('#languageLabel', t('language'));
      setText('[data-tab=donation]', t('donation'));
      setText('[data-tab=history]', t('history'));
      setText('[data-tab=goal]', t('goal'));
      setText('[data-tab=timer]', t('timer'));
      setText('[data-tab=credits]', t('credits'));
      setText('[data-tab=leaderboard]', t('leaderboard'));
      setText('[data-tab=filter]', t('filter'));
      setLegend('donation', 1, t('donationPresets'));
      setLegend('donation', 2, t('donationSize'));
      setLegend('donation', 3, t('donationColors'));
      setLegend('donation', 4, t('donationTemplates'));
      setLegend('donation', 5, t('alertMedia'));
      setLegend('donation', 6, t('alertRules'));
      setLegend('donation', 7, t('alertAnimations'));
      setLegend('donation', 8, t('donationVoice'));
      setLegend('donation', 9, t('customAlert'));
      setLegend('goal', 1, t('goalPresets'));
      setLegend('goal', 2, t('goalEditor'));
      setLegend('goal', 3, t('goalText'));
      setLegend('goal', 4, t('goalBar'));
      setLegend('goal', 5, t('barImage'));
      setLegend('goal', 6, t('decorImage'));
      setLegend('goal', 7, t('lastDonation'));
      setLegend('goal', 8, t('connectedServices'));
      setLegend('timer', 1, t('timerPresets'));
      setLegend('timer', 2, t('timerEditor'));
      setLegend('timer', 3, t('timerVisibility'));
      setLegend('timer', 4, t('timerLook'));
      setLegend('credits', 1, t('creditsPresets'));
      setLegend('credits', 2, t('creditsEditor'));
      setLegend('history', 1, t('obsDock'));
      setLegend('credits', 3, t('creditsSections'));
      setLegend('credits', 4, t('creditsLook'));
      setLegend('leaderboard', 1, t('leaderboardPresets'));
      setLegend('leaderboard', 2, t('leaderboardEditor'));
      setLegend('leaderboard', 3, t('leaderboardLook'));
      setLegend('leaderboard', 4, t('leaderboardEntries'));
      setLegend('filter', 1, t('blockedEditor'));
      setLegend('filter', 2, t('filterTestDonation'));
      setText('.controls > fieldset legend', t('tests'));
      setDonationLabels();
      setOverlayLabels();
      setLabel('data-overlay', 'TimerWidth', t('timerWidth'));
      setLabel('data-overlay', 'TimerX', t('timerX'));
      setLabel('data-overlay', 'TimerY', t('timerY'));
      setLabel('data-overlay', 'TimerTextAlign', t('timerAlign'));
      setLabel('data-overlay', 'TimerMode', t('timerMode'));
      setLabel('data-overlay', 'TimerShowConversion', t('timerConversion'));
      setLabel('data-overlay', 'TimerShowServices', t('timerServices'));
      setLabel('data-overlay', 'LastDonationFontSize', t('lastDonationSize'));
      setLabel('data-overlay', 'LastDonationTextAlign', t('lastDonationAlign'));
      setCreditsLabels();
      setLeaderboardLabels();
      setFilterLabels();
      setText('select[data-overlay=GoalFormat] option[value=amount]', t('formatAmount'));
      setText('select[data-overlay=GoalFormat] option[value=percent]', t('formatPercent'));
      setText('select[data-overlay=GoalFormat] option[value=summary]', t('formatSummary'));
      setText('select[data-overlay=GoalTextPlacement] option[value=inside]', t('insideBar'));
      setText('select[data-overlay=GoalTextPlacement] option[value=above]', t('aboveBar'));
      setText('select[data-overlay=GoalTextPlacement] option[value=below]', t('belowBar'));
      setText('select[data-overlay=GoalImageMode] option[value=reveal]', t('imageReveal'));
      setText('select[data-overlay=GoalImageMode] option[value=overlay]', t('imageOverlay'));
      setText('select[data-overlay=GoalBarVisualMode] option[value=bar]', t('visualBar'));
      setText('select[data-overlay=GoalBarVisualMode] option[value=image-reveal]', t('visualImageReveal'));
      setText('select[data-overlay=GoalBarVisualMode] option[value=image-silhouette]', t('visualImageSilhouette'));
      setText('select[data-overlay=GoalBarVisualMode] option[value=image-transparent]', t('visualImageTransparent'));
      setText('select[data-overlay=GoalBarVisualMode] option[value=image-inverse]', t('visualImageInverse'));
      setText('select[data-overlay=GoalFillDirection] option[value=horizontal]', t('horizontal'));
      setText('select[data-overlay=GoalFillDirection] option[value=vertical]', t('vertical'));
      setText('select[data-overlay=GoalImageFit] option[value=contain]', t('imageContain'));
      setText('select[data-overlay=GoalImageFit] option[value=cover]', t('imageCover'));
      setText('select[data-leaderboard=Mode] option[value=overall]', t('overallTop'));
      setText('select[data-leaderboard=Mode] option[value=platform-slides]', t('platformSlides'));
      setText('select[data-leaderboard=Mode] option[value=recent]', t('recentDonations'));
      setText('#goalDrop', t('imageDrop'));
      setText('#clearGoalImage', t('clearImage'));
      setText('#decorDrop', t('imageDrop'));
      setText('#clearDecorImage', t('clearImage'));
      setText('#addAlertRule', t('addRule'));
      setText('#alertMediaDrop', t('mediaDrop'));
      document.querySelectorAll('option[value=left]').forEach(el => el.textContent = t('left'));
      document.querySelectorAll('option[value=center]').forEach(el => el.textContent = t('center'));
      document.querySelectorAll('option[value=right]').forEach(el => el.textContent = t('right'));
      setText('select[data-donation=MediaPlacement] option[value=above]', t('aboveText'));
      setText('select[data-donation=MediaPlacement] option[value=below]', t('belowText'));
      setText('select[data-donation=MediaPlacement] option[value=left]', t('leftText'));
      setText('select[data-donation=MediaPlacement] option[value=right]', t('rightText'));
      setText('select[data-donation=MediaPlacement] option[value=background]', t('behindText'));
      setText('select[data-donation=MediaFit] option[value=contain]', t('contain'));
      setText('select[data-donation=MediaFit] option[value=cover]', t('cover'));
      setText('select[data-overlay=TimerMode] option[value=countdown]', t('countdownMode'));
      setText('select[data-overlay=TimerMode] option[value=countup-reset]', t('countupMode'));
      setText('[data-pane=goal] fieldset:nth-of-type(8) .note', t('servicesNote'));
      setText('[data-pane=timer] .note', t('timerNote'));
      setText('[data-pane=credits] .note', t('creditsNote'));
      setText('[data-pane=credits] fieldset:nth-of-type(3) .note', t('hiddenSectionNote'));
      setText('[data-pane=history] .note', t('dockNote'));
      setText('#timerTestPanel .note', t('timerManualNote'));
      setText('[data-pane=leaderboard] fieldset:nth-of-type(4) .note', t('leaderboardNote'));
      setText('[data-pane=filter] .note', t('filterNote'));
      setControlLabel('#customTestDonor', t('donor'));
      setControlLabel('#customTestAmount', t('amount'));
      setControlLabel('#customTestCurrency', t('currency'));
      setControlLabel('#customTestPlatform', t('platform'));
      setControlLabel('#customTestMessage', t('message'));
      setControlLabel('#leaderboardEntryName', t('name'));
      setControlLabel('#leaderboardEntryAmount', t('amount'));
      setControlLabel('#leaderboardEntryCurrency', t('currency'));
      setControlLabel('#leaderboardEntryPlatform', t('platform'));
      setControlLabel('#timerTestDonor', t('donor'));
      setControlLabel('#timerTestAmount', t('amount'));
      setControlLabel('#timerTestCurrency', t('currency'));
      setControlLabel('#timerTestPlatform', t('platform'));
      setControlLabel('#timerTestMessage', t('message'));
      setControlLabel('#filterTestDonor', t('donor'));
      setControlLabel('#filterTestAmount', t('amount'));
      setControlLabel('#filterTestCurrency', t('currency'));
      setControlLabel('#filterTestPlatform', t('platform'));
      setControlLabel('#filterTestMessage', t('message'));
      setText('[data-test=""50""]', t('test50'));
      setText('[data-test=""500""]', t('test500'));
      setText('[data-test=long]', t('testLong'));
      setText('[data-test=anonymous]', t('testAnonymous'));
      setText('#save', t('save'));
      setText('#reset', t('reset'));
      setText('#copy', t('copyCurrent'));
      setText('#copyDockUrl', t('copy'));
      setText('.help h2', t('obsSteps'));
      setText('#copy2', t('copy'));
      setText('#resetLeaderboardData', t('clearLeaderboard'));
      setText('#openAlertMedia', t('openMedia'));
      setText('#testCredits', t('loadTestCredits'));
      setText('#pauseCredits', creditsPaused ? t('resumeCredits') : t('pauseCredits'));
      setText('#restartCredits', t('restartCredits'));
      setText('#sendTimerTest', t('sendTimerTest'));
      setText('#sendFilterTest', t('sendFilterTest'));
      setText('#sendCustomTest', t('sendCustomAlert'));
      setText('#testSpeech', t('testSpeech'));
      setText('#speechHint', t('speechHint'));
      setText('#addLeaderboardEntry', t('addLeaderboardEntry'));
      const size = document.getElementById('obsSize');
      if (size) size.textContent = t('recommendedObsSize') + ({ donation:'1280 x 720', history:'360 x 600 OBS Dock', goal:'1280 x 520', timer:'1280 x 420', credits:'1920 x 1080', leaderboard:'1280 x 720', filter:'1280 x 720' }[active] || '1280 x 720');
      const steps = document.querySelectorAll('.help ol li');
      if (steps[0]) steps[0].textContent = t('obs1');
      if (steps[1]) steps[1].textContent = t('obs2');
      if (steps[2]) steps[2].textContent = t('obs3');
      if (steps[3]) steps[3].textContent = t('obs4');
      setText('.preview-head span', t('livePreview'));
      safeRun(populateFontList);
      safeRun(renderAlertMediaLibrary);
      safeRun(renderAlertRules);
      safeRun(loadLeaderboardEntries);
      safeRun(loadRecentDonations);
      safeRun(renderCreditsSections);
      safeRun(populateSpeechVoices);
      safeRun(updateCreditsPauseButton);
      safeRun(hideBaseFontRows);
    }
    function setDonationLabels() { setLabel('data-donation', 'Width', t('width')); setLabel('data-donation', 'Height', t('height')); setLabel('data-donation', 'BorderRadius', t('borderRadius')); setLabel('data-donation', 'Padding', t('padding')); setLabel('data-donation', 'FontSize', t('fontSize')); setLabel('data-donation', 'Opacity', t('opacity')); setLabel('data-donation', 'AnimationDuration', t('animationMs')); setLabel('data-donation', 'BackgroundColor', t('background')); setLabel('data-donation', 'TextColor', t('text')); setLabel('data-donation', 'AccentColor', t('accent')); setLabel('data-donation', 'DonorTemplate', t('donor')); setLabel('data-donation', 'AmountTemplate', t('amount')); setLabel('data-donation', 'MessageTemplate', t('message')); setLabel('data-donation', 'PlatformTemplate', t('platform')); setLabel('data-donation', 'FontFamily', t('baseFont')); setLabel('data-donation', 'DonorFontFamily', t('donorFont')); setLabel('data-donation', 'AmountFontFamily', t('amountFont')); setLabel('data-donation', 'MessageFontFamily', t('messageFont')); setLabel('data-donation', 'PlatformFontFamily', t('platformFont')); setLabel('data-donation', 'ShowPlatform', t('showPlatform')); setLabel('data-donation', 'MediaFile', t('defaultVisual')); setLabel('data-donation', 'SoundFile', t('alertSound')); setLabel('data-donation', 'TextSoundFile', t('textSound')); setLabel('data-donation', 'MediaFit', t('visualFit')); setLabel('data-donation', 'MediaPlacement', t('mediaPlacement')); setLabel('data-donation', 'TextAlign', t('textAlign')); setLabel('data-donation', 'DonorX', t('donorX')); setLabel('data-donation', 'DonorY', t('donorY')); setLabel('data-donation', 'AmountX', t('amountX')); setLabel('data-donation', 'AmountY', t('amountY')); setLabel('data-donation', 'MessageX', t('messageX')); setLabel('data-donation', 'MessageY', t('messageY')); setLabel('data-donation', 'MediaWidth', t('visualWidth')); setLabel('data-donation', 'MediaHeight', t('visualHeight')); setLabel('data-donation', 'MediaX', t('visualX')); setLabel('data-donation', 'MediaY', t('visualY')); setLabel('data-donation', 'VideoMuted', t('muteVideo')); setLabel('data-donation', 'ShowBackground', t('showBackground')); setLabel('data-donation', 'DisplayDuration', t('visibleDuration')); setLabel('data-donation', 'EntryAnimation', t('entryAnimation')); setLabel('data-donation', 'ExitAnimation', t('exitAnimation')); setLabel('data-donation', 'TextAnimation', t('donorTextAnimation')); setLabel('data-donation', 'SoundVolume', t('alertVolume')); setLabel('data-donation', 'TextSoundVolume', t('textSoundVolume')); setLabel('data-donation', 'SpeakDonation', t('speakDonation')); setLabel('data-donation', 'SpeechReadDonor', t('speechReadDonor')); setLabel('data-donation', 'SpeechReadAmount', t('speechReadAmount')); setLabel('data-donation', 'SpeechReadPlatform', t('speechReadPlatform')); setLabel('data-donation', 'SpeechReadMessage', t('speechReadMessage')); setLabel('data-donation', 'SpeechVoice', t('speechVoice')); setLabel('data-donation', 'SpeechRate', t('speechRate')); setLabel('data-donation', 'SpeechPitch', t('speechPitch')); setLabel('data-donation', 'SpeechVolume', t('speechVolume')); }
    function setOverlayLabels() { setLabel('data-overlay', 'GoalEnabled', t('enableGoal')); setLabel('data-overlay', 'GoalHeaderTitle', t('title1')); setLabel('data-overlay', 'GoalTitle', t('title2')); setLabel('data-overlay', 'GoalCurrent', t('current')); setLabel('data-overlay', 'GoalTarget', t('target')); setLabel('data-overlay', 'GoalCurrency', t('currency')); setLabel('data-overlay', 'GoalFormat', t('format')); setLabel('data-overlay', 'FontFamily', t('baseFont')); setLabel('data-overlay', 'GoalHeaderFontFamily', t('title1Font')); setLabel('data-overlay', 'GoalTitleFontFamily', t('title2Font')); setLabel('data-overlay', 'GoalValueFontFamily', t('goalAmountFont')); setLabel('data-overlay', 'ServicesFontFamily', t('providersFont')); setLabel('data-overlay', 'LastDonationFontFamily', t('lastDonationFont')); setLabel('data-overlay', 'TimerFontFamily', t('timerFont')); setLabel('data-overlay', 'ShowGoalText', t('showGoalText')); setLabel('data-overlay', 'ShowGoalValue', t('showGoalValue')); setLabel('data-overlay', 'ShowGoalMeta', t('showGoalMeta')); setLabel('data-overlay', 'ShowGoalBar', t('showGoalBar')); setLabel('data-overlay', 'GoalBarVisualMode', t('visualType')); setLabel('data-overlay', 'GoalFillDirection', t('fillDirection')); setLabel('data-overlay', 'ShowGoalProgress', t('showGoalProgress')); setLabel('data-overlay', 'ShowPanelBackground', t('showPanelBackground')); setLabel('data-overlay', 'ShowGoalImage', t('showGoalImage')); setLabel('data-overlay', 'GoalImageFit', t('imageFit')); setLabel('data-overlay', 'GoalImageWidth', t('imageWidth')); setLabel('data-overlay', 'GoalImageHeight', t('imageHeight')); setLabel('data-overlay', 'GoalImageX', t('imageX')); setLabel('data-overlay', 'GoalImageY', t('imageY')); setLabel('data-overlay', 'ShowDecorImage', t('showDecorImage')); setLabel('data-overlay', 'DecorImageX', t('imageX')); setLabel('data-overlay', 'DecorImageY', t('imageY')); setLabel('data-overlay', 'DecorImageWidth', t('imageWidth')); setLabel('data-overlay', 'ShowLastDonor', t('showLastDonor')); setLabel('data-overlay', 'ShowLastAmount', t('showLastAmount')); setLabel('data-overlay', 'ShowLastPlatform', t('showLastPlatform')); setLabel('data-overlay', 'Width', t('panelWidth')); setLabel('data-overlay', 'GoalBarWidth', t('barWidth')); setLabel('data-overlay', 'GoalBarLength', t('barLength')); setLabel('data-overlay', 'GoalBarHeight', t('barHeight')); setLabel('data-overlay', 'GoalBarAlign', t('barAlign')); setLabel('data-overlay', 'GoalTextPlacement', t('textPlacement')); setLabel('data-overlay', 'GoalTextAlign', t('textAlign')); setLabel('data-overlay', 'GoalTextOffsetX', t('textX')); setLabel('data-overlay', 'GoalTextOffsetY', t('textY')); setLabel('data-overlay', 'BorderRadius', t('boxRadius')); setLabel('data-overlay', 'BarRadius', t('barRadius')); setLabel('data-overlay', 'Padding', t('padding')); setLabel('data-overlay', 'ValueSize', t('valueSize')); setLabel('data-overlay', 'ContainerOpacity', t('containerOpacity')); setLabel('data-overlay', 'BarOpacity', t('barOpacity')); setLabel('data-overlay', 'BackgroundColor', t('background')); setLabel('data-overlay', 'TextColor', t('text')); setLabel('data-overlay', 'MutedColor', t('muted')); setLabel('data-overlay', 'AccentColor', t('fill')); setLabel('data-overlay', 'ShowServices', t('showServices')); setLabel('data-overlay', 'ServicesTitle', t('servicesTitle')); setLabel('data-overlay', 'ServicesTextAlign', t('servicesAlign')); setLabel('data-overlay', 'ServicesFontSize', t('servicesSize')); setLabel('data-overlay', 'TimerEnabled', t('enableTimer')); setLabel('data-overlay', 'TimerHeaderTitle', t('title1')); setLabel('data-overlay', 'TimerTitle', t('title2')); setLabel('data-overlay', 'TimerSubtitle', t('subtitle')); setLabel('data-overlay', 'TimerStartSeconds', t('startSeconds')); setLabel('data-overlay', 'TimerUnitAmount', t('donationAmountStep')); setLabel('data-overlay', 'TimerSecondsPerUnit', t('secondsPerStep')); setLabel('data-overlay', 'TimerMaxSeconds', t('maxSeconds')); setLabel('data-overlay', 'TimerCurrency', t('currency')); setLabel('data-overlay', 'TitleSize', t('titleSize')); setLabel('data-overlay', 'LabelSize', t('labelSize')); setLabel('data-overlay', 'MetaSize', t('metaSize')); setLabel('data-overlay', 'Opacity', t('opacity')); setLabel('data-overlay', 'BarColor', t('emptyBar')); }
    function setCreditsLabels() { setLabel('data-credits', 'CreditsEnabled', t('enableCredits')); setLabel('data-credits', 'UseNativeCredits', t('useNativeCredits')); setLabel('data-credits', 'Title', t('title')); setLabel('data-credits', 'Subtitle', t('subtitle')); setLabel('data-credits', 'Outro', t('outro')); setLabel('data-credits', 'SectionTitle', t('creditsSectionTitle')); setLabel('data-credits', 'ShowAmounts', t('showAmounts')); setLabel('data-credits', 'ShowPlatforms', t('showPlatforms')); setLabel('data-credits', 'ShowMessages', t('showMessages')); setLabel('data-credits', 'DurationSeconds', t('rollSeconds')); setLabel('data-credits', 'LockDuration', t('lockCreditsSpeed')); setLabel('data-credits', 'DonationFields', t('fields')); setLabel('data-credits', 'Width', t('width')); setLabel('data-credits', 'FontSize', t('fontSize')); setLabel('data-credits', 'TransparentBackground', t('transparentBackground')); setLabel('data-credits', 'BackgroundColor', t('background')); setLabel('data-credits', 'TextColor', t('text')); setLabel('data-credits', 'MutedColor', t('muted')); setLabel('data-credits', 'AccentColor', t('accent')); setLabel('data-credits', 'FontFamily', t('baseFont')); setLabel('data-credits', 'TitleFontFamily', t('titleFont')); setLabel('data-credits', 'DetailFontFamily', t('detailsFont')); }
    function setLeaderboardLabels() { setLabel('data-leaderboard', 'Enabled', t('enableLeaderboard')); setLabel('data-leaderboard', 'ShowTitle', t('showTitle')); setLabel('data-leaderboard', 'Title', t('title')); setLabel('data-leaderboard', 'Mode', t('mode')); setLabel('data-leaderboard', 'TopCount', t('rows')); setLabel('data-leaderboard', 'SlideDuration', t('slideDuration')); setLabel('data-leaderboard', 'ShowRanks', t('showRanks')); setLabel('data-leaderboard', 'ShowAmounts', t('showAmounts')); setLabel('data-leaderboard', 'ShowPlatforms', t('showPlatforms')); setLabel('data-leaderboard', 'FontFamily', t('baseFont')); setLabel('data-leaderboard', 'TitleFontFamily', t('titleFont')); setLabel('data-leaderboard', 'AmountFontFamily', t('amountFont')); setLabel('data-leaderboard', 'Width', t('width')); setLabel('data-leaderboard', 'Padding', t('padding')); setLabel('data-leaderboard', 'BorderRadius', t('borderRadius')); setLabel('data-leaderboard', 'FontSize', t('fontSize')); setLabel('data-leaderboard', 'TitleSize', t('titleSize')); setLabel('data-leaderboard', 'RowGap', t('rowGap')); setLabel('data-leaderboard', 'Opacity', t('opacity')); setLabel('data-leaderboard', 'BackgroundColor', t('background')); setLabel('data-leaderboard', 'TextColor', t('text')); setLabel('data-leaderboard', 'MutedColor', t('muted')); setLabel('data-leaderboard', 'AccentColor', t('accent')); }
    function setFilterLabels() { setLabel('data-filter', 'BlockedNames', t('blockedNames')); setLabel('data-filter', 'BlockedWords', t('blockedWords')); setLabel('data-filter', 'ReplacementName', t('replacementName')); setLabel('data-filter', 'ReplacementText', t('replacementText')); }
    function setLegend(pane, index, text) { setText(`[data-pane=${pane}] fieldset:nth-of-type(${index}) legend`, text); }
    function setLabel(attr, key, text) { document.querySelectorAll(`[${attr}=${key}]`).forEach(input => { const span = input.closest('label') ? input.closest('label').querySelector('span') : null; if (span) span.textContent = text; }); }
    function setControlLabel(selector, text) { const input = document.querySelector(selector); const span = input && input.closest('label') ? input.closest('label').querySelector('span') : null; if (span) span.textContent = text; }
    function setText(selector, text) { const el = document.querySelector(selector); if (el) el.textContent = text; }
    function escapeHtml(value) { return String(value || '').replace(/[&<>'\x22]/g, ch => { const c = ch.charCodeAt(0); if (c === 38) return '&amp;'; if (c === 60) return '&lt;'; if (c === 62) return '&gt;'; if (c === 39) return '&#39;'; return '&quot;'; }); }
    function escapeAttr(value) { return escapeHtml(value); }
    function showStatus(text) { const box = document.getElementById('status'); if (!box) return; box.textContent = text; if (statusTimer) clearTimeout(statusTimer); statusTimer = setTimeout(() => { if (box) box.textContent = ''; statusTimer = null; }, 3500); }
  </script>
</body>
</html>";
    }
    private string WidgetHtml()
    {
        return @"<!doctype html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>DonConnect Alert</title>
  <style>
    :root { --w:680px; --h:360px; --r:18px; --p:26px; --fs:28px; --op:.88; --bg:#10131a; --text:#f8fbff; --accent:#35d07f; --dur:650ms; --media-w:260px; --media-h:170px; --media-x:0px; --media-y:0px; --donor-x:0px; --donor-y:0px; --amount-x:0px; --amount-y:0px; --message-x:0px; --message-y:0px; --align:center; --media-fit:contain; --font:""Segoe UI"", Arial, sans-serif; --donor-font:var(--font); --amount-font:var(--font); --message-font:var(--font); --platform-font:var(--font); }
    * { box-sizing: border-box; }
    html, body { margin:0; width:100%; height:100%; overflow:hidden; background:transparent; font-family:var(--font); color:var(--text); }
    body { display:grid; place-items:center; }
    .widget { position:relative; width:var(--w); height:var(--h); max-width:100vw; max-height:100vh; opacity:var(--op); background:var(--bg); border-radius:var(--r); padding:var(--p); display:grid; align-content:center; box-shadow:0 18px 46px rgba(0,0,0,.32); border:1px solid rgba(255,255,255,.12); overflow:hidden; }
    .widget.hidden { visibility:hidden; opacity:0; pointer-events:none; }
    .widget.no-bg { background:transparent; border-color:transparent; box-shadow:none; }
    .media { position:absolute; z-index:0; left:50%; top:50%; width:var(--media-w); height:var(--media-h); transform:translate(calc(-50% + var(--media-x)), calc(-50% + var(--media-y))); pointer-events:none; }
    .media.hidden, .platform.hidden { display:none; }
    .media img, .media video { width:100%; height:100%; display:none; object-fit:var(--media-fit); }
    .media img.active, .media video.active { display:block; }
    .copy { position:relative; z-index:1; display:grid; gap:10px; min-width:0; text-align:var(--align); justify-items:stretch; }
    .layout-above.has-media .copy { padding-top:calc(var(--media-h) * .62); } .layout-below.has-media .copy { padding-bottom:calc(var(--media-h) * .62); }
    .layout-left.has-media .copy { padding-left:calc(var(--media-w) * .72); } .layout-right.has-media .copy { padding-right:calc(var(--media-w) * .72); }
    .layout-above .media { top:24%; } .layout-below .media { top:76%; } .layout-left .media { left:22%; } .layout-right .media { left:78%; }
    .top { display:grid; justify-items:stretch; gap:5px; min-width:0; }
    .donor { min-width:0; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; transform:translate(var(--donor-x), var(--donor-y)); font-family:var(--donor-font); font-size:var(--fs); font-weight:900; }
    .amount { color:var(--accent); white-space:nowrap; transform:translate(var(--amount-x), var(--amount-y)); font-family:var(--amount-font); font-size:calc(var(--fs) * .9); font-weight:900; }
    .platform { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; color:var(--accent); font-family:var(--platform-font); font-size:calc(var(--fs) * .58); font-weight:800; text-transform:uppercase; }
    .message { min-width:0; overflow:hidden; display:-webkit-box; -webkit-line-clamp:3; -webkit-box-orient:vertical; transform:translate(var(--message-x), var(--message-y)); font-family:var(--message-font); font-size:calc(var(--fs) * .72); line-height:1.28; color:var(--text); opacity:.88; }
    .entry-fade { animation:entryFade var(--dur) ease both; } .exit-fade { animation:exitFade var(--dur) ease both; }
    .entry-slide-left { animation:entryLeft var(--dur) ease both; } .exit-slide-left { animation:exitLeft var(--dur) ease both; }
    .entry-slide-right { animation:entryRight var(--dur) ease both; } .exit-slide-right { animation:exitRight var(--dur) ease both; }
    .entry-slide-up { animation:entryUp var(--dur) ease both; } .exit-slide-up { animation:exitUp var(--dur) ease both; }
    .entry-slide-down { animation:entryDown var(--dur) ease both; } .exit-slide-down { animation:exitDown var(--dur) ease both; }
    .entry-zoom { animation:entryZoom var(--dur) ease both; } .exit-zoom { animation:exitZoom var(--dur) ease both; }
    .exit-scatter { animation:scatterOut var(--dur) ease both; }
    .text-fade { animation:textFade var(--dur) ease both; }
    .text-typewriter { animation:typewriter var(--dur) steps(24,end) both; }
    .text-reveal-left { animation:textRevealLeft var(--dur) ease both; }
    .text-slide-up { animation:textSlideUp var(--dur) ease both; }
    @keyframes entryFade { from { opacity:0; } to { opacity:var(--op); } }
    @keyframes exitFade { from { opacity:var(--op); } to { opacity:0; } }
    @keyframes entryLeft { from { opacity:0; transform:translateX(-70px); } to { opacity:var(--op); transform:translateX(0); } }
    @keyframes exitLeft { from { opacity:var(--op); transform:translateX(0); } to { opacity:0; transform:translateX(-70px); } }
    @keyframes entryRight { from { opacity:0; transform:translateX(70px); } to { opacity:var(--op); transform:translateX(0); } }
    @keyframes exitRight { from { opacity:var(--op); transform:translateX(0); } to { opacity:0; transform:translateX(70px); } }
    @keyframes entryUp { from { opacity:0; transform:translateY(55px); } to { opacity:var(--op); transform:translateY(0); } }
    @keyframes exitUp { from { opacity:var(--op); transform:translateY(0); } to { opacity:0; transform:translateY(-55px); } }
    @keyframes entryDown { from { opacity:0; transform:translateY(-55px); } to { opacity:var(--op); transform:translateY(0); } }
    @keyframes exitDown { from { opacity:var(--op); transform:translateY(0); } to { opacity:0; transform:translateY(55px); } }
    @keyframes entryZoom { from { opacity:0; transform:scale(.72); } to { opacity:var(--op); transform:scale(1); } }
    @keyframes exitZoom { from { opacity:var(--op); transform:scale(1); } to { opacity:0; transform:scale(.72); } }
    @keyframes scatterOut { from { opacity:var(--op); filter:blur(0); letter-spacing:0; transform:translateY(0); } to { opacity:0; filter:blur(10px); letter-spacing:10px; transform:translateY(35px); } }
    @keyframes textFade { from { opacity:0; } to { opacity:1; } }
    @keyframes typewriter { from { clip-path:inset(0 100% 0 0); } to { clip-path:inset(0 0 0 0); } }
    @keyframes textRevealLeft { from { opacity:0; clip-path:inset(0 100% 0 0); } to { opacity:1; clip-path:inset(0 0 0 0); } }
    @keyframes textSlideUp { from { opacity:0; transform:translateY(14px); } to { opacity:1; transform:translateY(0); } }
  </style>
</head>
<body>
  <section class=""widget hidden"" id=""widget"">
    <div class=""media hidden"" id=""media""><img id=""mediaImage"" alt=""""><video id=""mediaVideo"" playsinline></video></div>
    <div class=""copy"" id=""copy"">
      <div class=""top""><div class=""donor"" id=""donor"">Test donor</div><div class=""amount"" id=""amount"">50 RUB</div></div>
      <div class=""platform"" id=""platform"">Widget Test</div>
      <div class=""message"" id=""message"">DonConnect live preview.</div>
    </div>
  </section>
  <script>
    let settings = null;
    let lastEventId = -1;
    let hideTimer = null;
    let exitTimer = null;
    const preview = new URLSearchParams(location.search).has('preview');
    const browserTts = new URLSearchParams(location.search).has('browserTts');
    let donation = { donor:'Test donor', amount:'50', currency:'RUB', message:'DonConnect live preview.', provider:'Widget Test', source:'Widget Test' };
    boot();
    window.addEventListener('message', event => { if (event.data && event.data.type === 'settings') { settings = event.data.settings; render(false); if (preview) showPreview(); } });
    async function boot() {
      settings = await fetch('/donconnect/api/settings').then(r => r.json()).catch(() => null);
      render(false);
      if (preview) showPreview();
      await poll();
      setInterval(poll, 800);
    }
    async function poll() {
      const data = await fetch('/donconnect/api/latest', { cache:'no-store' }).then(r => r.json()).catch(() => null);
      if (!data) return;
      if (lastEventId < 0) {
        lastEventId = data.eventId;
        donation = data.donation || donation;
        render(false);
        if (preview) showPreview();
        return;
      }
      if (data.eventId !== lastEventId) {
        lastEventId = data.eventId;
        donation = data.donation || donation;
        render(true);
      }
    }
    function applySettings(current) {
      if (!current) return;
      const root = document.documentElement.style;
      root.setProperty('--w', px(current.Width));
      root.setProperty('--h', px(current.Height));
      root.setProperty('--r', px(current.BorderRadius));
      root.setProperty('--p', px(current.Padding));
      root.setProperty('--fs', px(current.FontSize));
      root.setProperty('--op', current.Opacity ?? .88);
      root.setProperty('--bg', current.BackgroundColor || '#10131a');
      root.setProperty('--text', current.TextColor || '#f8fbff');
      root.setProperty('--accent', current.AccentColor || '#35d07f');
      root.setProperty('--dur', (current.AnimationDuration ?? 650) + 'ms');
      root.setProperty('--media-w', px(current.MediaWidth || 240));
      root.setProperty('--media-h', px(current.MediaHeight || 150));
      root.setProperty('--media-x', px(current.MediaX || 0));
      root.setProperty('--media-y', px(current.MediaY || 0));
      root.setProperty('--donor-x', px(current.DonorX || 0));
      root.setProperty('--donor-y', px(current.DonorY || 0));
      root.setProperty('--amount-x', px(current.AmountX || 0));
      root.setProperty('--amount-y', px(current.AmountY || 0));
      root.setProperty('--message-x', px(current.MessageX || 0));
      root.setProperty('--message-y', px(current.MessageY || 0));
      root.setProperty('--align', ['left','center','right'].includes(current.TextAlign) ? current.TextAlign : 'center');
      root.setProperty('--media-fit', current.MediaFit === 'cover' ? 'cover' : 'contain');
      root.setProperty('--font', fontStack(current.FontFamily, 'Segoe UI, Arial, sans-serif'));
      root.setProperty('--donor-font', fontStack(current.DonorFontFamily, 'Segoe UI, Arial, sans-serif'));
      root.setProperty('--amount-font', fontStack(current.AmountFontFamily, 'Segoe UI, Arial, sans-serif'));
      root.setProperty('--message-font', fontStack(current.MessageFontFamily, 'Segoe UI, Arial, sans-serif'));
      root.setProperty('--platform-font', fontStack(current.PlatformFontFamily, 'Segoe UI, Arial, sans-serif'));
    }
    function render(animate) {
      const current = resolveSettings(donation);
      applySettings(current);
      const donor = donation.donor || 'Anonymous';
      const amount = donation.amount || '0';
      const currency = donation.currency || '';
      const message = donation.message || '';
      const platform = donation.provider || donation.source || '';
      document.getElementById('donor').textContent = tpl(current.DonorTemplate || '{donor}', donor, amount, currency, message, platform);
      document.getElementById('amount').textContent = tpl(current.AmountTemplate || '{amount} {currency}', donor, amount, currency, message, platform);
      document.getElementById('message').textContent = tpl(current.MessageTemplate || '{message}', donor, amount, currency, message, platform);
      const platformNode = document.getElementById('platform');
      platformNode.textContent = tpl(current.PlatformTemplate || '{platform}', donor, amount, currency, message, platform);
      platformNode.classList.toggle('hidden', current.ShowPlatform === false || !platformNode.textContent.trim());
      document.getElementById('widget').classList.toggle('no-bg', current.ShowBackground === false);
      renderMedia(current);
      if (animate) showAlert(current);
    }
    function resolveSettings(data) {
      const current = Object.assign({}, settings || {});
      const amount = Number(data && data.amount || 0);
      const matches = Array.isArray(current.AlertRules) ? current.AlertRules.filter(rule => amount >= Number(rule.MinAmount || 0) && (Number(rule.MaxAmount || 0) <= 0 || amount <= Number(rule.MaxAmount || 0))) : [];
      matches.sort((a, b) => Number(b.MinAmount || 0) - Number(a.MinAmount || 0));
      const rule = matches[0];
      if (rule) {
        current.MediaFile = pick(rule.MediaFiles, current.MediaFile, rule.Randomize !== false);
        current.SoundFile = pick(rule.SoundFiles, current.SoundFile, rule.Randomize !== false);
      }
      return current;
    }
    function pick(files, fallback, randomize) {
      const list = Array.isArray(files) ? files.filter(Boolean) : [];
      if (!list.length) return fallback || '';
      return list[randomize ? Math.floor(Math.random() * list.length) : 0];
    }
    function renderMedia(current) {
      const media = document.getElementById('media');
      const image = document.getElementById('mediaImage');
      const video = document.getElementById('mediaVideo');
      const file = String(current.MediaFile || '');
      const isVideo = /\.(mp4|webm)$/i.test(file);
      media.classList.toggle('hidden', !file);
      image.classList.toggle('active', !!file && !isVideo);
      video.classList.toggle('active', !!file && isVideo);
      if (!file) { image.removeAttribute('src'); video.removeAttribute('src'); return; }
      const url = mediaUrl(file);
      if (isVideo) {
        if (video.getAttribute('src') !== url) video.setAttribute('src', url);
        video.muted = current.VideoMuted !== false;
      } else if (image.getAttribute('src') !== url) {
        image.setAttribute('src', url);
      }
    }
    function showPreview() {
      const current = resolveSettings(donation);
      const box = document.getElementById('widget');
      box.classList.remove('hidden');
      box.className = widgetClass(current);
    }
    function showAlert(current) {
      clearTimeout(exitTimer); clearTimeout(hideTimer);
      const box = document.getElementById('widget');
      const donor = document.getElementById('donor');
      box.className = widgetClass(current);
      void box.offsetWidth;
      box.classList.add('entry-' + animationName(current.EntryAnimation, 'fade'));
      donor.className = 'donor'; void donor.offsetWidth;
      if ((current.TextAnimation || 'none') !== 'none') donor.classList.add('text-' + animationName(current.TextAnimation, 'fade'));
      const video = document.getElementById('mediaVideo');
      if (video.classList.contains('active')) { try { video.currentTime = 0; video.play().catch(() => {}); } catch {} }
      playAudio(current.SoundFile, current.SoundVolume);
      if ((current.TextAnimation || 'none') !== 'none') playAudio(current.TextSoundFile, current.TextSoundVolume);
      if (browserTts) speakDonation(current);
      const duration = Math.max(500, Number(current.DisplayDuration || 9000));
      const animation = Math.max(0, Number(current.AnimationDuration || 650));
      exitTimer = setTimeout(() => {
        box.className = widgetClass(current);
        void box.offsetWidth;
        box.classList.add('exit-' + animationName(current.ExitAnimation, 'fade'));
      }, Math.max(animation, duration - animation));
      hideTimer = setTimeout(() => { if (!preview) box.classList.add('hidden'); }, duration);
    }
    function playAudio(file, volume) {
      if (!file) return;
      try { const audio = new Audio(mediaUrl(file)); audio.volume = Math.max(0, Math.min(1, Number(volume ?? 75) / 100)); audio.play().catch(() => {}); } catch {}
    }
    function speakDonation(current) {
      if (!current || current.SpeakDonation !== true || !window.speechSynthesis) return;
      const parts = [];
      if (current.SpeechReadDonor !== false) parts.push(document.getElementById('donor').textContent || '');
      if (current.SpeechReadAmount !== false) parts.push(document.getElementById('amount').textContent || '');
      if (current.SpeechReadPlatform !== false) parts.push(document.getElementById('platform').textContent || '');
      if (current.SpeechReadMessage !== false) parts.push(document.getElementById('message').textContent || '');
      const text = parts.filter(part => String(part || '').trim()).join('. ');
      if (!text) return;
      try {
        const utterance = new SpeechSynthesisUtterance(text);
        const voice = findVoice(current.SpeechVoice);
        if (voice) utterance.voice = voice;
        utterance.rate = Math.max(.5, Math.min(2, Number(current.SpeechRate || 1)));
        utterance.pitch = Math.max(.5, Math.min(2, Number(current.SpeechPitch || 1)));
        utterance.volume = Math.max(0, Math.min(1, Number(current.SpeechVolume ?? 85) / 100));
        window.speechSynthesis.cancel();
        window.speechSynthesis.speak(utterance);
      } catch {}
    }
    function findVoice(name) {
      const target = String(name || '').trim().toLowerCase();
      if (!target || !window.speechSynthesis) return null;
      return (window.speechSynthesis.getVoices() || []).find(voice => String(voice.name || '').toLowerCase() === target) || null;
    }
    function widgetClass(current) { const placement = ['above','below','left','right','background'].includes(current.MediaPlacement) ? current.MediaPlacement : 'above'; return 'widget layout-' + placement + (current.MediaFile ? ' has-media' : '') + (current.ShowBackground === false ? ' no-bg' : ''); }
    function animationName(value, fallback) { return String(value || fallback).replace(/[^a-z-]/g, '') || fallback; }
    function mediaUrl(file) { return '/donconnect/media/' + String(file || '').split('/').map(encodeURIComponent).join('/'); }
    function tpl(template, donor, amount, currency, message, platform) {
      return String(template || '').replaceAll('{donor}', donor).replaceAll('{amount}', amount).replaceAll('{currency}', currency).replaceAll('{message}', message).replaceAll('{platform}', platform).replaceAll('{source}', platform);
    }
    function px(value) { return `${Number(value || 0)}px`; }
    function fontStack(value, fallback) { const name = String(value || '').trim(); const safe = name.replace(/[""\\]/g, '').replace(/\s+/g, ' ').trim(); return safe ? '""' + safe + '"", ' + fallback : fallback; }
  </script>
</body>
</html>";
    }

    private string GoalTimerHtml(string mode)
    {
        mode = NormalizeChoice(mode, "goal", "goal", "timer", "both");
        return @"<!doctype html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>DonConnect Goal Timer</title>
  <style>
    :root { --w:920px; --r:22px; --bar-r:22px; --p:22px; --title:30px; --value:42px; --label:17px; --meta:17px; --container-op:.94; --bar-op:1; --bg:#10131a; --bg-rgba:rgba(16,19,26,.94); --text:#f8fbff; --muted:#b8c0cc; --accent:#7c3cff; --bar:#1e2026; --barh:84px; --barl:680px; --bar-self:center; --bar-justify:center; --goal-align:center; --goal-x:0px; --goal-y:0px; --timer-x:0px; --timer-y:0px; --timer-w:680px; --timer-align:center; --services-align:center; --services-justify:center; --services-size:14px; --last-size:14px; --last-align:center; --pct:0%; --image-fit:contain; --imagew:680px; --imageh:220px; --image-x:0px; --image-y:0px; --decor-x:0px; --decor-y:0px; --decor-w:220px; --font:""Segoe UI"", Arial, sans-serif; --header-font:var(--font); --goal-title-font:var(--font); --goal-value-font:var(--font); --services-font:var(--font); --last-font:var(--font); --timer-font:var(--font); }
    * { box-sizing:border-box; }
    html, body { margin:0; width:100%; height:100%; overflow:hidden; background:transparent; color:var(--text); font-family:var(--font); }
    body { display:grid; place-items:center; }
    .panel { position:relative; width:min(var(--w), 100vw); background:var(--bg-rgba); border-radius:var(--r); padding:var(--p); box-shadow:0 18px 46px rgba(0,0,0,.32); border:1px solid rgba(255,255,255,.12); overflow:hidden; }
    .panel.timer-only { width:min(var(--timer-w), 100vw); text-align:var(--timer-align); }
    .panel.timer-only #timerBlock { margin:0 auto; }
    .panel > :not(.decor) { position:relative; z-index:1; }
    .panel.no-bg { background:transparent; box-shadow:none; border-color:transparent; }
    .title { margin:0 0 14px; font-family:var(--header-font); font-size:var(--title); font-weight:900; }
    .grid { display:grid; gap:16px; grid-template-columns:1fr 1fr; }
    .grid.single { grid-template-columns:1fr; }
    .goal-card { display:flex; flex-direction:column; gap:8px; align-items:stretch; }
    .goal-card.text-inside { display:grid; min-height:var(--barh); }
    .goal-card.text-inside.vertical { min-height:var(--barl); }
    .goal-card.text-inside .goal-bar, .goal-card.text-inside .goal-image { grid-area:1 / 1; align-self:center; justify-self:var(--bar-justify); }
    .goal-card.text-inside .goal-text { grid-area:1 / 1; z-index:2; align-self:center; justify-self:var(--bar-justify); width:var(--barl); max-width:100%; padding:0 18px; transform:translate(var(--goal-x), var(--goal-y)); pointer-events:none; }
    .goal-card.text-inside.vertical .goal-text { width:min(var(--w), 100%); }
    .goal-card.text-above .goal-text { order:1; transform:translate(var(--goal-x), var(--goal-y)); }
    .goal-card.text-above .goal-bar, .goal-card.text-above .goal-image { order:2; }
    .goal-card.text-below .goal-bar, .goal-card.text-below .goal-image { order:1; }
    .goal-card.text-below .goal-text { order:2; transform:translate(var(--goal-x), var(--goal-y)); }
    .goal-card.text-above .goal-text, .goal-card.text-below .goal-text { width:var(--barl); max-width:100%; align-self:var(--bar-self); }
    .goal-card.vertical.text-above .goal-text, .goal-card.vertical.text-below .goal-text { width:min(var(--w), 100%); }
    .goal-text { text-align:var(--goal-align); }
    .label { color:var(--muted); font-size:var(--label); font-weight:800; margin-bottom:6px; }
    .goal-text .label { color:var(--text); font-family:var(--goal-title-font); font-size:var(--title); font-weight:950; line-height:1.02; text-transform:uppercase; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; margin-bottom:2px; }
    .value { font-size:var(--value); font-weight:500; line-height:1.05; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .goal-text .value { font-family:var(--goal-value-font); }
    #timerBlock { width:min(100%, var(--timer-w)); font-family:var(--timer-font); text-align:var(--timer-align); transform:translate(var(--timer-x), var(--timer-y)); }
    .meta { color:var(--muted); font-size:var(--meta); margin-top:8px; min-height:20px; }
    .bar { position:relative; width:var(--barl); max-width:100%; height:var(--barh); border-radius:var(--bar-r); background:var(--bar); opacity:var(--bar-op); overflow:hidden; align-self:var(--bar-self); }
    .bar.vertical { width:var(--barh); height:var(--barl); }
    .fill { position:absolute; inset:0 auto 0 0; height:100%; width:0%; background:var(--accent); transition:width 450ms ease, height 450ms ease; z-index:1; }
    .bar.vertical .fill { inset:auto 0 0 0; width:100%; height:0%; }
    .goal-image { position:relative; width:var(--imagew); height:var(--imageh); max-width:100%; border-radius:var(--bar-r); overflow:hidden; align-self:var(--bar-self); transform:translate(var(--image-x), var(--image-y)); }
    .image-base, .image-fill { position:absolute; inset:0; z-index:2; background-position:left center; background-repeat:no-repeat; background-size:var(--image-fit); pointer-events:none; display:none; }
    .image-base { filter:grayscale(1); opacity:.72; }
    .image-fill { clip-path:inset(0 calc(100% - var(--pct)) 0 0); }
    .goal-image.vertical .image-base, .goal-image.vertical .image-fill { background-position:center bottom; }
    .goal-image.vertical .image-fill { clip-path:inset(calc(100% - var(--pct)) 0 0 0); }
    .goal-image.image-reveal .image-base, .goal-image.image-reveal .image-fill { display:block; }
    .goal-image.image-silhouette .image-base, .goal-image.image-silhouette .image-fill { display:block; }
    .goal-image.image-silhouette .image-base { filter:brightness(0); opacity:.9; }
    .goal-image.image-transparent .image-fill { display:block; }
    .goal-image.image-inverse .image-base { display:block; filter:none; opacity:1; clip-path:inset(0 0 0 var(--pct)); }
    .goal-image.vertical.image-inverse .image-base { clip-path:inset(0 0 var(--pct) 0); }
    .bar.no-progress .fill, .goal-image.no-progress .image-fill { display:none; }
    .last, .services { color:var(--muted); font-size:var(--services-size); margin-top:14px; text-align:var(--services-align); }
    .services { font-family:var(--services-font); }
    .last { font-family:var(--last-font); font-size:var(--last-size); text-align:var(--last-align); }
    .services-list { display:flex; flex-wrap:wrap; gap:5px; margin-top:7px; justify-content:var(--services-justify); }
    .service { color:var(--text); background:rgba(255,255,255,.12); border-radius:999px; padding:3px 7px; font-weight:800; }
    .services-list.dense { gap:3px; } .services-list.dense .service { padding:2px 5px; font-size:.82em; }
    .decor { position:absolute; z-index:0; left:50%; top:50%; width:var(--decor-w); max-width:none; transform:translate(calc(-50% + var(--decor-x)), calc(-50% + var(--decor-y))); pointer-events:none; opacity:1; }
    .hidden { display:none; }
  </style>
</head>
<body>
  <section class=""panel"">
    <img class=""decor hidden"" id=""decorImage"" alt="""">
    <h1 class=""title"" id=""title"">DonConnect</h1>
    <div class=""grid"" id=""grid""><article id=""goalBlock""><div class=""goal-card text-inside"" id=""goalCard""><div class=""goal-text"" id=""goalText""><div class=""label"" id=""goalTitle"">Goal</div><div class=""value"" id=""goalValue"">0 RUB</div></div><div class=""bar goal-bar"" id=""goalBar""><div class=""fill"" id=""goalFill""></div></div><div class=""goal-image hidden"" id=""goalImage""><div class=""image-base"" id=""goalImageBase""></div><div class=""image-fill"" id=""goalImageFill""></div></div></div><div class=""meta"" id=""goalMeta"">0%</div></article><article id=""timerBlock""><div class=""label"" id=""timerTitle"">Timer</div><div class=""meta"" id=""timerSubtitle""></div><div class=""value"" id=""timerValue"">00:00:00</div><div class=""meta"" id=""timerMeta""></div><div class=""meta"" id=""timerConversion""></div></article></div>
    <div class=""services"" id=""services""></div><div class=""last"" id=""last"" ></div>
  </section>
  <script>
    const pageMode = '" + mode + @"';
    let settings = null;
    let state = null;
    boot();
    window.addEventListener('message', event => { if (event.data && event.data.type === 'overlay-settings') { settings = event.data.settings; applySettings(); render(); } });
    async function boot() { settings = await fetch('/donconnect/api/overlay-settings').then(r => r.json()).catch(() => null); applySettings(); await poll(); setInterval(poll, 800); }
    async function poll() { const data = await fetch('/donconnect/api/goal-state', { cache:'no-store' }).then(r => r.json()).catch(() => null); if (!data) return; state = data; if (!settings && data.settings) settings = data.settings; applySettings(); render(); }
    function applySettings() { if (!settings) return; const root = document.documentElement.style; const barAlign = normalize(settings.GoalBarAlign, 'center', ['left','center','right']); const textAlign = normalize(settings.GoalTextAlign, 'center', ['left','center','right']); const servicesAlign = normalize(settings.ServicesTextAlign, 'center', ['left','center','right']); const lastAlign = normalize(settings.LastDonationTextAlign, 'center', ['left','center','right']); const bg = settings.BackgroundColor || '#10131a'; const bgOpacity = clamp(Number(settings.ContainerOpacity ?? settings.Opacity ?? .94), 0, 1); root.setProperty('--w', px(settings.Width)); root.setProperty('--r', px(settings.BorderRadius)); root.setProperty('--bar-r', px(settings.BarRadius ?? settings.BorderRadius)); root.setProperty('--p', px(settings.Padding)); root.setProperty('--title', px(settings.TitleSize)); root.setProperty('--value', px(settings.ValueSize)); root.setProperty('--label', px(settings.LabelSize)); root.setProperty('--meta', px(settings.MetaSize)); root.setProperty('--container-op', bgOpacity); root.setProperty('--bar-op', clamp(Number(settings.BarOpacity ?? 1), 0, 1)); root.setProperty('--bg', bg); root.setProperty('--bg-rgba', colorWithAlpha(bg, bgOpacity)); root.setProperty('--text', settings.TextColor || '#f8fbff'); root.setProperty('--muted', settings.MutedColor || '#b8c0cc'); root.setProperty('--accent', settings.AccentColor || '#7c3cff'); root.setProperty('--bar', settings.BarColor || '#1e2026'); root.setProperty('--barh', px(settings.GoalBarHeight || 74)); root.setProperty('--barl', px(settings.GoalBarLength || 520)); root.setProperty('--bar-self', barAlign === 'left' ? 'flex-start' : barAlign === 'right' ? 'flex-end' : 'center'); root.setProperty('--bar-justify', barAlign === 'left' ? 'start' : barAlign === 'right' ? 'end' : 'center'); root.setProperty('--goal-align', textAlign); root.setProperty('--goal-x', px(settings.GoalTextOffsetX || 0)); root.setProperty('--goal-y', px(settings.GoalTextOffsetY || 0)); root.setProperty('--services-align', servicesAlign); root.setProperty('--services-justify', servicesAlign === 'left' ? 'flex-start' : servicesAlign === 'right' ? 'flex-end' : 'center'); root.setProperty('--services-size', px(settings.ServicesFontSize || 14)); root.setProperty('--last-size', px(settings.LastDonationFontSize || 14)); root.setProperty('--last-align', lastAlign); root.setProperty('--image-fit', normalize(settings.GoalImageFit, 'contain', ['contain','cover'])); root.setProperty('--imagew', px(settings.GoalImageWidth || 520)); root.setProperty('--imageh', px(settings.GoalImageHeight || 160)); root.setProperty('--image-x', px(settings.GoalImageX || 0)); root.setProperty('--image-y', px(settings.GoalImageY || 0)); root.setProperty('--decor-x', px(settings.DecorImageX || 0)); root.setProperty('--decor-y', px(settings.DecorImageY || 0)); root.setProperty('--decor-w', px(settings.DecorImageWidth || 220)); root.setProperty('--font', fontStack(settings.FontFamily, 'Segoe UI, Arial, sans-serif')); root.setProperty('--header-font', fontStack(settings.GoalHeaderFontFamily, 'Segoe UI, Arial, sans-serif')); root.setProperty('--goal-title-font', fontStack(settings.GoalTitleFontFamily, 'Segoe UI, Arial, sans-serif')); root.setProperty('--goal-value-font', fontStack(settings.GoalValueFontFamily, 'Segoe UI, Arial, sans-serif')); root.setProperty('--services-font', fontStack(settings.ServicesFontFamily, 'Segoe UI, Arial, sans-serif')); root.setProperty('--last-font', fontStack(settings.LastDonationFontFamily, 'Segoe UI, Arial, sans-serif')); root.setProperty('--timer-font', fontStack(settings.TimerFontFamily, 'Segoe UI, Arial, sans-serif')); applyGoalImage(); applyDecorImage(); }
    function applyTimerSettings() { if (!settings) return; const root = document.documentElement.style; root.setProperty('--timer-x', px(settings.TimerX || 0)); root.setProperty('--timer-y', px(settings.TimerY || 0)); root.setProperty('--timer-w', px(settings.TimerWidth || 320)); root.setProperty('--timer-align', normalize(settings.TimerTextAlign, 'center', ['left','center','right'])); }
    function render() { applyTimerSettings(); const data = state || {}; const goal = goalWithSettings(data.goal || {}); const timer = data.timer || {}; const mode = pageMode; const showGoal = mode === 'goal' || mode === 'both'; const showTimer = mode === 'timer' || mode === 'both'; const placement = normalize(settings && settings.GoalTextPlacement, 'inside', ['above','inside','below']); const visual = normalize(settings && settings.GoalBarVisualMode, 'bar', ['bar','image-reveal','image-silhouette','image-transparent','image-inverse']); const direction = normalize(settings && settings.GoalFillDirection, 'horizontal', ['horizontal','vertical']); const vertical = direction === 'vertical'; const progress = clamp(Number(goal.percent || 0), 0, 100); const panel = document.querySelector('.panel'); document.getElementById('grid').className = 'grid ' + (showGoal && showTimer ? '' : 'single'); document.getElementById('goalBlock').classList.toggle('hidden', !showGoal); document.getElementById('timerBlock').classList.toggle('hidden', !showTimer); document.getElementById('goalCard').className = 'goal-card text-' + placement + (vertical ? ' vertical' : ''); panel.classList.toggle('no-bg', !boolSetting('ShowPanelBackground', true)); panel.classList.toggle('timer-only', showTimer && !showGoal); document.getElementById('goalText').classList.toggle('hidden', !boolSetting('ShowGoalText', true)); const bar = document.getElementById('goalBar'); bar.classList.toggle('hidden', visual !== 'bar' || !boolSetting('ShowGoalBar', true)); bar.classList.toggle('no-progress', !boolSetting('ShowGoalProgress', true)); bar.classList.toggle('vertical', vertical); const imageBox = document.getElementById('goalImage'); if (imageBox) { imageBox.classList.toggle('no-progress', !boolSetting('ShowGoalProgress', true)); imageBox.classList.toggle('vertical', vertical); } const headerText = mode === 'timer' ? textSetting('TimerHeaderTitle', timer.headerTitle || '') : (mode === 'goal' || mode === 'both') ? textSetting('GoalHeaderTitle', goal.headerTitle || '') : ''; setVisibleText('title', headerText); setVisibleText('goalTitle', goal.title || ''); document.getElementById('goalValue').textContent = goalDisplay(goal); document.getElementById('goalValue').classList.toggle('hidden', !boolSetting('ShowGoalValue', true)); document.documentElement.style.setProperty('--pct', progress + '%'); const goalFill = document.getElementById('goalFill'); goalFill.style.width = vertical ? '100%' : progress + '%'; goalFill.style.height = vertical ? progress + '%' : '100%'; const showMeta = boolSetting('ShowGoalMeta', true) && placement !== 'inside' && boolSetting('ShowGoalText', true); const meta = document.getElementById('goalMeta'); meta.classList.toggle('hidden', !showMeta); meta.textContent = showMeta ? (goal.percentText || '0%') + (goal.targetText ? ' | ' + goal.targetText : '') : ''; setVisibleText('timerTitle', textSetting('TimerTitle', timer.title || '')); setVisibleText('timerSubtitle', textSetting('TimerSubtitle', timer.subtitle || '')); document.getElementById('timerValue').textContent = currentTimerText(timer); document.getElementById('timerMeta').textContent = Number(timer.addedSeconds || 0) > 0 ? '+' + (timer.addedText || '00:00:00') : ''; document.getElementById('timerConversion').textContent = boolSetting('TimerShowConversion', true) ? timerConversionText(timer) : ''; const last = document.getElementById('last'); last.textContent = showTimer && !showGoal ? '' : lastDonationText(data.lastDonation || {}); renderServices(data.services || [], !showTimer || showGoal || boolSetting('TimerShowServices', false)); applyGoalImage(); }
    function goalWithSettings(goal) { const copy = Object.assign({}, goal || {}); if (settings) { copy.headerTitle = textSetting('GoalHeaderTitle', copy.headerTitle || ''); copy.title = textSetting('GoalTitle', copy.title || ''); if (copy.current == null) copy.current = textSetting('GoalCurrent', '0'); copy.target = textSetting('GoalTarget', copy.target || '0'); copy.currency = textSetting('GoalCurrency', copy.currency || ''); } const current = Number(copy.current || 0); const target = Number(copy.target || 0); const percent = target > 0 ? clamp((current / target) * 100, 0, 100) : 0; copy.percent = percent; copy.currentText = formatAmount(current, copy.currency); copy.targetText = target > 0 ? formatAmount(target, copy.currency) : ''; copy.percentText = percent.toFixed(percent % 1 ? 1 : 0) + '%'; copy.summary = copy.targetText ? copy.currentText + ' / ' + copy.targetText : copy.currentText; return copy; }
    function textSetting(key, fallback) { if (settings && Object.prototype.hasOwnProperty.call(settings, key)) return String(settings[key] ?? ''); return String(fallback ?? ''); }
    function boolSetting(key, fallback) { if (!settings || settings[key] == null) return fallback; return settings[key] === true || settings[key] === 'true'; }
    function setVisibleText(id, value) { const el = document.getElementById(id); const text = String(value ?? ''); el.textContent = text; el.classList.toggle('hidden', text.trim() === ''); }
    function lastDonationText(last) { if (!settings || settings.ShowLastDonation === false) return ''; const parts = []; const donor = last.donor || last.user || ''; const platform = last.source || last.platform || last.provider || ''; if (settings.ShowLastDonor !== false && donor) parts.push(donor); const amount = [last.amount, last.currency].filter(Boolean).join(' '); if (settings.ShowLastAmount !== false && amount) parts.push(amount); if (settings.ShowLastPlatform !== false && platform) parts.push(platform); return parts.join(' | '); }
    function applyGoalImage() { const box = document.getElementById('goalImage'); const base = document.getElementById('goalImageBase'); const fill = document.getElementById('goalImageFill'); if (!box || !base || !fill || !settings) return; const visual = normalize(settings.GoalBarVisualMode, 'bar', ['bar','image-reveal','image-silhouette','image-transparent','image-inverse']); const url = visual !== 'bar' && boolSetting('ShowGoalImage', false) ? String(settings.GoalImageDataUrl || '') : ''; const image = url ? `url(""${url.replace(/""/g, '%22')}"")` : ''; base.style.backgroundImage = image; fill.style.backgroundImage = image; box.classList.toggle('hidden', !url); box.classList.toggle('image-reveal', !!url && visual === 'image-reveal'); box.classList.toggle('image-silhouette', !!url && visual === 'image-silhouette'); box.classList.toggle('image-transparent', !!url && visual === 'image-transparent'); box.classList.toggle('image-inverse', !!url && visual === 'image-inverse'); }
    function applyDecorImage() { const image = document.getElementById('decorImage'); if (!image) return; const url = boolSetting('ShowDecorImage', false) ? String(settings.DecorImageDataUrl || '') : ''; image.src = url || ''; image.classList.toggle('hidden', !url); }
    function colorWithAlpha(color, alpha) { const hex = String(color || '').trim(); const match = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(hex); if (!match) return hex || 'transparent'; let value = match[1]; if (value.length === 3) value = value.split('').map(ch => ch + ch).join(''); const r = parseInt(value.slice(0,2), 16); const g = parseInt(value.slice(2,4), 16); const b = parseInt(value.slice(4,6), 16); return `rgba(${r},${g},${b},${clamp(Number(alpha),0,1)})`; }
    function goalDisplay(goal) { const format = (settings && settings.GoalFormat || 'amount').toLowerCase(); if (format === 'percent') return goal.percentText || '0%'; if (format === 'summary') return goal.summary || goal.currentText || '0'; return goal.currentText || goal.summary || '0'; }
    function formatAmount(amount, currency) { const value = Number.isFinite(amount) ? amount : 0; const text = value.toLocaleString(undefined, { maximumFractionDigits:2 }); return [text, currency].filter(Boolean).join(' '); }
    function renderServices(services, allowed) { const box = document.getElementById('services'); if (!allowed || !settings || !settings.ShowServices || !services.length) { box.innerHTML = ''; return; } const title = textSetting('ServicesTitle', 'Connected providers'); const heading = title.trim() ? '<div>' + escapeHtml(title) + '</div>' : ''; const short = name => ({'DonationAlerts':'DA','StreamElements':'SE','Streamlabs':'SL','DonatePay RU':'DP RU','DonatePay EU':'DP EU','Donate.Stream':'DS','deStream':'dS','DonateX.gg':'DX','ODA':'ODA','Generic API':'API'}[String(name)] || String(name)); box.innerHTML = heading + '<div class=""services-list ' + (services.length > 5 ? 'dense' : '') + '"">' + services.map(name => `<span class=""service"" title=""${escapeHtml(String(name))}"">${escapeHtml(short(name))}</span>`).join('') + '</div>'; }
    function currentTimerText(timer) { if (String(settings && settings.TimerMode || timer.mode || '') === 'countup-reset') { const startedAt = Date.parse(timer.startedAt || ''); return Number.isFinite(startedAt) ? formatDuration((Date.now() - startedAt) / 1000) : (timer.text || '00:00:00'); } const endsAt = Date.parse(timer.endsAt || ''); if (!Number.isFinite(endsAt)) return timer.text || '00:00:00'; return formatDuration(Math.max(0, (endsAt - Date.now()) / 1000)); }
    function timerConversionText(timer) { const amount = Number(settings && settings.TimerUnitAmount || 100); const seconds = Number(settings && settings.TimerSecondsPerUnit || 60); const currency = String(settings && settings.TimerCurrency || 'RUB'); const duration = seconds % 60 === 0 ? (seconds / 60) + ' min' : seconds + ' sec'; return amount + ' ' + currency + ' = ' + duration; }
    function formatDuration(total) { const seconds = Math.floor(Math.max(0, total)); const h = Math.floor(seconds / 3600); const m = Math.floor((seconds % 3600) / 60); const s = seconds % 60; return `${pad(h)}:${pad(m)}:${pad(s)}`; }
    function pad(v) { return String(v).padStart(2, '0'); }
    function px(v) { return `${Number(v || 0)}px`; }
    function fontStack(value, fallback) { const name = String(value || '').trim(); const safe = name.replace(/[""\\]/g, '').replace(/\s+/g, ' ').trim(); return safe ? '""' + safe + '"", ' + fallback : fallback; }
    function clamp(v,min,max) { return Math.min(max, Math.max(min, Number.isFinite(v) ? v : min)); }
    function normalize(value, fallback, allowed) { value = String(value || '').toLowerCase(); return allowed.includes(value) ? value : fallback; }
    function escapeHtml(value) { return String(value || '').replace(/[&<>'\x22]/g, ch => { const c = ch.charCodeAt(0); if (c === 38) return '&amp;'; if (c === 60) return '&lt;'; if (c === 62) return '&gt;'; if (c === 39) return '&#39;'; return '&quot;'; }); }
  </script>
</body>
</html>";
    }

    private string LeaderboardHtml()
    {
        return @"<!doctype html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>DonConnect Leaderboard</title>
  <style>
    :root { --w:560px; --p:18px; --r:16px; --fs:22px; --title:26px; --gap:8px; --op:.94; --bg:#10131a; --text:#f8fbff; --muted:#b8c0cc; --accent:#7c3cff; --font:""Segoe UI"", Arial, sans-serif; --title-font:var(--font); --amount-font:var(--font); }
    * { box-sizing:border-box; }
    html, body { margin:0; width:100%; height:100%; overflow:hidden; background:transparent; color:var(--text); font-family:var(--font); }
    body { display:grid; place-items:center; }
    .board { width:min(var(--w), 100vw); padding:var(--p); border-radius:var(--r); background:var(--bg); opacity:var(--op); border:1px solid rgba(255,255,255,.12); box-shadow:0 18px 46px rgba(0,0,0,.32); }
    h1 { margin:0 0 13px; color:var(--accent); font-family:var(--title-font); font-size:var(--title); line-height:1.05; }
    .platform { margin:0 0 10px; color:var(--muted); font-size:calc(var(--fs) * .72); font-weight:800; text-transform:uppercase; }
    ol { display:grid; gap:var(--gap); margin:0; padding:0; list-style:none; }
    li { display:grid; grid-template-columns:auto minmax(0,1fr) auto; gap:10px; align-items:center; min-width:0; padding:7px 9px; border-radius:8px; background:rgba(255,255,255,.055); font-size:var(--fs); }
    .rank { min-width:24px; color:var(--accent); font-weight:900; }
    .name { min-width:0; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; font-weight:900; }
    .amount { color:var(--text); font-family:var(--amount-font); font-weight:900; white-space:nowrap; }
    .platforms { grid-column:2 / 4; margin-top:-6px; color:var(--muted); font-size:calc(var(--fs) * .58); font-weight:700; }
    .hidden { display:none; }
  </style>
</head>
<body>
  <section class=""board"" id=""board""><h1 id=""title"">Top donors</h1><div class=""platform hidden"" id=""platform""></div><ol id=""rows""></ol></section>
  <script>
    let settings = null;
    let state = null;
    let slideIndex = 0;
    let lastSlideAt = 0;
    boot();
    window.addEventListener('message', event => { if (event.data && event.data.type === 'leaderboard-settings') { settings = event.data.settings; applySettings(); render(); } });
    async function boot() { settings = await fetch('/donconnect/api/leaderboard-settings').then(r => r.json()).catch(() => null); applySettings(); await poll(); setInterval(poll, 1000); setInterval(render, 500); }
    async function poll() { const data = await fetch('/donconnect/api/leaderboard-state', { cache:'no-store' }).then(r => r.json()).catch(() => null); if (!data) return; state = data; if (!settings && data.settings) settings = data.settings; applySettings(); render(); }
    function applySettings() { if (!settings) return; const root = document.documentElement.style; root.setProperty('--w', px(settings.Width || 560)); root.setProperty('--p', px(settings.Padding || 18)); root.setProperty('--r', px(settings.BorderRadius || 16)); root.setProperty('--fs', px(settings.FontSize || 22)); root.setProperty('--title', px(settings.TitleSize || 26)); root.setProperty('--gap', px(settings.RowGap || 8)); root.setProperty('--op', clamp(Number(settings.Opacity ?? .94), 0, 1)); root.setProperty('--bg', settings.BackgroundColor || '#10131a'); root.setProperty('--text', settings.TextColor || '#f8fbff'); root.setProperty('--muted', settings.MutedColor || '#b8c0cc'); root.setProperty('--accent', settings.AccentColor || '#7c3cff'); root.setProperty('--font', fontStack(settings.FontFamily, 'Segoe UI, Arial, sans-serif')); root.setProperty('--title-font', fontStack(settings.TitleFontFamily, 'Segoe UI, Arial, sans-serif')); root.setProperty('--amount-font', fontStack(settings.AmountFontFamily, 'Segoe UI, Arial, sans-serif')); }
    function render() { const data = state || {}; const mode = String(settings && settings.Mode || 'overall'); let rows = data.overall || []; let platform = ''; if (mode === 'recent') rows = data.recent || []; if (mode === 'platform-slides') { const slides = data.slides || []; const duration = Math.max(1000, Number(settings && settings.SlideDuration || 5000)); if (Date.now() - lastSlideAt >= duration) { slideIndex = slides.length ? (slideIndex + 1) % slides.length : 0; lastSlideAt = Date.now(); } const slide = slides[slideIndex] || {}; rows = slide.items || []; platform = slide.platform || ''; } const board = document.getElementById('board'); board.classList.toggle('hidden', settings && settings.Enabled === false); const title = document.getElementById('title'); title.textContent = String(settings && settings.Title || 'Top donors'); title.classList.toggle('hidden', settings && settings.ShowTitle === false); const platformNode = document.getElementById('platform'); platformNode.textContent = platform; platformNode.classList.toggle('hidden', !platform); document.getElementById('rows').innerHTML = rows.map(rowHtml).join(''); }
    function rowHtml(row) { const showRanks = !settings || settings.ShowRanks !== false; const showAmounts = !settings || settings.ShowAmounts !== false; const showPlatforms = !settings || settings.ShowPlatforms !== false; const platforms = Array.isArray(row.platforms) ? row.platforms.join(' + ') : ''; return `<li>${showRanks ? `<span class=""rank"">#${escapeHtml(row.rank || '')}</span>` : ''}<span class=""name"">${escapeHtml(row.name || 'Anonymous')}</span>${showAmounts ? `<span class=""amount"">${escapeHtml([row.amount, row.currency].filter(Boolean).join(' '))}</span>` : ''}${showPlatforms && platforms ? `<span class=""platforms"">${escapeHtml(platforms)}</span>` : ''}</li>`; }
    function px(value) { return `${Number(value || 0)}px`; }
    function clamp(v,min,max) { return Math.min(max, Math.max(min, Number.isFinite(v) ? v : min)); }
    function fontStack(value, fallback) { const name = String(value || '').trim(); const safe = name.replace(/[""\\]/g, '').replace(/\s+/g, ' ').trim(); return safe ? '""' + safe + '"", ' + fallback : fallback; }
    function escapeHtml(value) { return String(value || '').replace(/[&<>'\x22]/g, ch => { const c = ch.charCodeAt(0); if (c === 38) return '&amp;'; if (c === 60) return '&lt;'; if (c === 62) return '&gt;'; if (c === 39) return '&#39;'; return '&quot;'; }); }
  </script>
</body>
</html>";
    }

    private string DockHtml()
    {
        return @"<!doctype html>
<html>
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>DonConnect OBS Dock</title>
  <style>
    :root { color-scheme: dark; font-family: Segoe UI, Arial, sans-serif; background:#11161d; color:#eef4ff; }
    * { box-sizing:border-box; }
    body { margin:0; min-width:280px; background:#11161d; }
    header { position:sticky; top:0; z-index:2; display:flex; align-items:center; justify-content:space-between; gap:8px; padding:10px 12px; background:#151c25; border-bottom:1px solid #263241; }
    h1 { margin:0; font-size:14px; font-weight:800; }
    button { border:1px solid #3a4a5f; border-radius:6px; background:#202b37; color:#eef4ff; padding:6px 8px; font:inherit; font-weight:700; cursor:pointer; }
    button:hover { background:#2a3746; }
    button.danger { background:#7f1d1d; border-color:#b91c1c; color:#fff; }
    button.danger:hover { background:#991b1b; }
    .summary { display:grid; gap:8px; padding:10px 10px 0; }
    .mini { border:1px solid #263241; border-radius:8px; background:#171f29; padding:9px; }
    .mini-head { display:flex; align-items:center; justify-content:space-between; gap:8px; margin-bottom:7px; color:#c8d3e0; font-size:12px; font-weight:800; }
    .tiny { padding:3px 6px; border-radius:5px; font-size:11px; }
    .mini-bar { width:100%; height:9px; overflow:hidden; border-radius:999px; background:#0b1017; border:1px solid #2a3746; }
    .mini-bar > div { height:100%; width:0%; background:linear-gradient(90deg,#35d07f,#74f0ff); transition:width .25s ease; }
    .mini-text { margin-top:5px; color:#9aa8b8; font-size:11px; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .timer-mini { display:flex; align-items:center; justify-content:space-between; gap:10px; color:#c8d3e0; font-size:12px; }
    .timer-mini strong { color:#eef4ff; font-size:16px; font-variant-numeric:tabular-nums; }
    .list { display:grid; gap:8px; padding:10px; }
    .item { display:grid; grid-template-columns:1fr auto; gap:8px; align-items:center; padding:9px; border:1px solid #263241; border-radius:8px; background:#171f29; }
    .main { min-width:0; }
    .top { display:flex; gap:6px; align-items:baseline; min-width:0; }
    .name { font-size:13px; font-weight:800; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .amount { flex:none; font-size:12px; color:#9fe7b5; font-weight:800; }
    .message { margin-top:3px; color:#c8d3e0; font-size:12px; line-height:1.25; overflow:hidden; display:-webkit-box; -webkit-line-clamp:2; -webkit-box-orient:vertical; }
    .meta { margin-top:3px; color:#8291a3; font-size:11px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .media-actions { display:grid; gap:5px; }
    .empty { padding:20px 12px; color:#9aa8b8; text-align:center; font-size:13px; }
    .status { padding:0 10px 10px; color:#8291a3; font-size:11px; }
  </style>
</head>
<body>
  <header>
    <h1 id=""title"">DonConnect Dock</h1>
    <button id=""refresh"" type=""button"">Refresh</button>
  </header>
  <section class=""summary"">
    <div class=""mini"">
      <div class=""mini-head""><span id=""goalMiniTitle"">Goal</span><button id=""resetGoal"" class=""tiny danger"" type=""button"">Reset</button></div>
      <div class=""mini-bar""><div id=""goalMiniFill""></div></div>
      <div id=""goalMiniText"" class=""mini-text"">0 / 0</div>
    </div>
    <div class=""mini timer-mini""><span id=""timerMiniTitle"">Timer</span><strong id=""timerMiniText"">00:00:00</strong></div>
  </section>
  <main id=""list"" class=""list""><div class=""empty"">No donations yet</div></main>
  <div id=""status"" class=""status""></div>
  <script>
    const list = document.getElementById('list');
    const statusEl = document.getElementById('status');
    const refreshButton = document.getElementById('refresh');
    const resetGoalButton = document.getElementById('resetGoal');
    const goalFill = document.getElementById('goalMiniFill');
    const goalText = document.getElementById('goalMiniText');
    const timerText = document.getElementById('timerMiniText');
    let latestState = null;
    const i18n = {
      en:{ title:'DonConnect Dock', refresh:'Refresh', empty:'No donations yet', repeat:'Repeat', delete:'Delete', replayed:'Alert repeated without goal/timer/credits changes', deleted:'Donation removed from dock history', waiting:'Dock is waiting for DonConnect server', loading:'Refreshing...', goal:'Goal', timer:'Timer', resetGoal:'Reset', goalReset:'Goal reset' },
      ru:{ title:'Док DonConnect', refresh:'Обновить', empty:'Донатов пока нет', repeat:'Повторить', delete:'Удалить', replayed:'Алёрт повторён без изменения цели, таймера и титров', deleted:'Донат удалён из истории дока', waiting:'Док ждёт сервер DonConnect', loading:'Обновляю...', goal:'Цель', timer:'Таймер', resetGoal:'Сброс', goalReset:'Цель сброшена' },
      uk:{ title:'Док DonConnect', refresh:'Оновити', empty:'Донатів поки немає', repeat:'Повторити', delete:'Видалити', replayed:'Алерт повторено без зміни цілі, таймера й титрів', deleted:'Донат видалено з історії дока', waiting:'Док чекає сервер DonConnect', loading:'Оновлюю...', goal:'Ціль', timer:'Таймер', resetGoal:'Скинути', goalReset:'Ціль скинуто' }
    };
    const lang = normalizeLanguage(localStorage.getItem('donconnectEditorLanguage') || navigator.language || 'en');
    const t = key => (i18n[lang] && i18n[lang][key]) || i18n.en[key] || key;
    document.documentElement.lang = lang;
    document.getElementById('title').textContent = t('title');
    document.getElementById('goalMiniTitle').textContent = t('goal');
    document.getElementById('timerMiniTitle').textContent = t('timer');
    refreshButton.textContent = t('refresh');
    resetGoalButton.textContent = t('resetGoal');
    refreshButton.addEventListener('click', () => load(true));
    resetGoalButton.addEventListener('click', resetGoal);
    function normalizeLanguage(value) { const text = String(value || '').toLowerCase(); if (text.startsWith('ru')) return 'ru'; if (text.startsWith('uk') || text.startsWith('ua')) return 'uk'; return 'en'; }
    async function json(url, options) {
      const response = await fetch(url, options || {});
      if (!response.ok) throw new Error('HTTP ' + response.status);
      return await response.json();
    }
    function escapeHtml(value) {
      return String(value || '').replace(/[&<>'\x22]/g, ch => { const c = ch.charCodeAt(0); if (c === 38) return '&amp;'; if (c === 60) return '&lt;'; if (c === 62) return '&gt;'; if (c === 39) return '&#39;'; return '&quot;'; });
    }
    function money(item) {
      return [item.amount, item.currency].filter(Boolean).join(' ');
    }
    function numberValue(value) {
      const parsed = Number(String(value == null ? '' : value).replace(',', '.'));
      return Number.isFinite(parsed) ? parsed : 0;
    }
    function formatDuration(seconds) {
      const total = Math.max(0, Math.floor(Number(seconds) || 0));
      const hours = Math.floor(total / 3600);
      const minutes = Math.floor((total % 3600) / 60);
      const secs = total % 60;
      return [hours, minutes, secs].map(value => String(value).padStart(2, '0')).join(':');
    }
    function renderSummary(state) {
      latestState = state || latestState;
      const goal = latestState && latestState.goal ? latestState.goal : {};
      const timer = latestState && latestState.timer ? latestState.timer : {};
      const percent = Math.max(0, Math.min(100, numberValue(goal.percent)));
      goalFill.style.width = percent + '%';
      goalText.textContent = goal.summary || [goal.currentText, goal.targetText].filter(Boolean).join(' / ') || '0';
      timerText.textContent = timer.text || formatDuration(timer.seconds);
    }
    async function resetGoal() {
      const state = await json('/donconnect/api/goal-reset', { method:'POST' });
      renderSummary(state);
      statusEl.textContent = t('goalReset');
      setTimeout(() => statusEl.textContent = '', 2500);
    }
    async function replay(id) {
      await json('/donconnect/api/replay-donation', { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({ id }) });
      statusEl.textContent = t('replayed');
      setTimeout(() => statusEl.textContent = '', 2500);
    }
    async function deleteDonation(id) {
      await json('/donconnect/api/delete-recent-donation', { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({ id }) });
      statusEl.textContent = t('deleted');
      await load();
      setTimeout(() => statusEl.textContent = '', 2500);
    }
    async function load(manual) {
      try {
        if (manual) statusEl.textContent = t('loading');
        const responses = await Promise.all([
          json('/donconnect/api/recent-donations', { cache:'no-store' }),
          json('/donconnect/api/goal-state', { cache:'no-store' })
        ]);
        const result = responses[0];
        renderSummary(responses[1]);
        const items = Array.isArray(result.items) ? result.items : [];
        if (!items.length) {
          list.innerHTML = '<div class=""empty"">' + escapeHtml(t('empty')) + '</div>';
          if (manual) statusEl.textContent = '';
          return;
        }
        list.innerHTML = items.map(item => `
          <section class=""item"">
            <div class=""main"">
              <div class=""top""><span class=""name"">${escapeHtml(item.donor || 'Anonymous')}</span><span class=""amount"">${escapeHtml(money(item))}</span></div>
              <div class=""message"">${escapeHtml(item.message || '')}</div>
              <div class=""meta"">${escapeHtml(item.provider || item.source || '')}</div>
            </div>
            <div class=""media-actions""><button type=""button"" data-replay=""${escapeHtml(item.id || '')}"">${escapeHtml(t('repeat'))}</button><button type=""button"" class=""danger"" data-delete=""${escapeHtml(item.id || '')}"">${escapeHtml(t('delete'))}</button></div>
          </section>`).join('');
        list.querySelectorAll('[data-replay]').forEach(button => button.addEventListener('click', () => replay(button.dataset.replay)));
        list.querySelectorAll('[data-delete]').forEach(button => button.addEventListener('click', () => deleteDonation(button.dataset.delete)));
        if (manual) statusEl.textContent = '';
      } catch (error) {
        list.innerHTML = '<div class=""empty"">' + escapeHtml(t('waiting')) + '</div>';
        statusEl.textContent = error && error.message ? error.message : String(error);
      }
    }
    load(false);
    setInterval(() => renderSummary(latestState), 1000);
    setInterval(load, 2500);
  </script>
</body>
</html>";
    }

    private string CreditsHtml()
    {
        return @"<!doctype html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>DonConnect Credits</title>
  <style>
    :root { --bg:transparent; --text:#f7f4ec; --muted:#b9d8d2; --accent:#ffcf5a; --shadow:rgba(0,0,0,.7); --duration:70s; --w:920px; --fs:42px; --font:""Segoe UI"", Arial, sans-serif; --title-font:var(--font); --detail-font:var(--font); }
    * { box-sizing:border-box; }
    html, body { margin:0; width:100%; height:100%; overflow:hidden; background:var(--bg); color:var(--text); font-family:var(--font); }
    body { display:grid; place-items:center; }
    .stage { position:relative; width:min(100vw, var(--w)); height:100vh; overflow:hidden; mask-image:linear-gradient(to bottom, transparent 0%, black 14%, black 86%, transparent 100%); }
    .credits { position:absolute; left:50%; top:100%; width:min(90vw, var(--w)); transform:translateX(-50%); text-align:center; animation:roll var(--duration) linear infinite; text-shadow:0 3px 18px var(--shadow); }
    .credits.paused { animation-play-state:paused; }
    @keyframes roll { from { transform:translate(-50%, 0); } to { transform:translate(-50%, calc(-100% - 110vh)); } }
    .intro { min-height:30vh; display:grid; align-content:end; gap:18px; padding-bottom:64px; }
    h1 { margin:0; color:var(--accent); font-family:var(--title-font); font-size:calc(var(--fs) * 1.55); line-height:1; font-weight:900; }
    .intro p, .outro, .empty { margin:0; color:var(--muted); font-size:calc(var(--fs) * .72); font-weight:700; line-height:1.35; }
    .section { margin:0 0 54px; }
    .section h2 { margin:0 0 18px; color:var(--accent); font-size:calc(var(--fs) * .92); text-transform:uppercase; }
    .names { display:grid; gap:10px; margin:0; padding:0; list-style:none; font-size:var(--fs); line-height:1.18; font-weight:800; }
    .detail { display:block; color:var(--muted); font-family:var(--detail-font); font-size:.58em; font-weight:700; margin-top:4px; }
    .outro { min-height:60vh; display:grid; align-content:start; padding-top:30px; }
  </style>
</head>
<body>
  <main class=""stage""><article class=""credits"" id=""credits""></article></main>
  <script>
    let settings = null;
    let state = null;
    let paused = false;
    let polling = false;
    let lastMarkup = '';
    boot();
    window.addEventListener('message', event => { if (event.data && event.data.type === 'credits-settings') { settings = event.data.settings; applySettings(); render(); } if (event.data && event.data.type === 'credits-control') applyControl(event.data.action); });
    async function boot() { settings = await fetch('/donconnect/api/credits-settings').then(r => r.json()).catch(() => null); applySettings(); await poll(); setInterval(poll, 1500); }
    async function poll() { if (polling) return; polling = true; try { const data = await fetch('/donconnect/api/credits-state', { cache:'no-store' }).then(r => r.json()).catch(() => null); if (!data) return; state = data; render(); } finally { polling = false; } }
    function applySettings() { if (!settings) return; const root = document.documentElement.style; root.setProperty('--bg', settings.TransparentBackground === false ? (settings.BackgroundColor || '#000000') : 'transparent'); root.setProperty('--text', settings.TextColor || '#f7f4ec'); root.setProperty('--muted', settings.MutedColor || '#b9d8d2'); root.setProperty('--accent', settings.AccentColor || '#ffcf5a'); root.setProperty('--duration', Math.max(5, Number(settings.DurationSeconds || 70)) + 's'); root.setProperty('--w', px(settings.Width || 920)); root.setProperty('--fs', px(settings.FontSize || 42)); root.setProperty('--font', fontStack(settings.FontFamily, 'Segoe UI, Arial, sans-serif')); root.setProperty('--title-font', fontStack(settings.TitleFontFamily, 'Segoe UI, Arial, sans-serif')); root.setProperty('--detail-font', fontStack(settings.DetailFontFamily, 'Segoe UI, Arial, sans-serif')); }
    function render() { let sections = state && state.native ? nativeSections(state.native) : [{ title:(settings && settings.SectionTitle) || 'Donations', items:(state && state.items) || [] }]; sections = sections.filter(group => !hiddenSection(group.title)); const count = sections.reduce((sum, item) => sum + item.items.length, 0); const base = Math.max(5, Number(settings && settings.DurationSeconds || 70)); document.documentElement.style.setProperty('--duration', ((settings && settings.LockDuration) ? Math.max(base, 24 + count * 3) : base) + 's'); const body = sections.length ? sections.map(section).join('') : '<p class=""empty"">No credits yet</p>'; const html = [`<header class=""intro""><h1>${escapeHtml((settings && settings.Title) || 'Thanks for watching')}</h1><p>${escapeHtml((settings && settings.Subtitle) || 'Today with us')}</p></header>`, body, `<footer class=""outro"">${escapeHtml((settings && settings.Outro) || 'See you next stream')}</footer>`].join(''); const el = document.getElementById('credits'); if (html !== lastMarkup) { el.innerHTML = html; lastMarkup = html; } el.classList.toggle('paused', paused); }
    function section(group) { const items = group.items || []; if (!items.length) return ''; return '<section class=""section""><h2>' + escapeHtml(group.title || 'Credits') + '</h2><ul class=""names"">' + items.map(item => `<li>${escapeHtml(item.name || 'Anonymous')}<span class=""detail"">${escapeHtml(detail(item))}</span></li>`).join('') + '</ul></section>'; }
    function nativeSections(data) { const result = []; const events = data.Events || data.events || {}; const users = data.User || data.Users || data.user || data.users || {}; const hype = data.HypeTrain || data.hypeTrain || {}; const top = data.Top || data.top || {}; addNativeSection(result, 'Follows', firstArray(events.Follows, events.follows)); addNativeSection(result, 'Cheers', firstArray(events.Cheers, events.cheers)); addNativeSection(result, 'Subs', firstArray(events.Subs, events.subs)); addNativeSection(result, 'ReSubs', firstArray(events.ReSubs, events.ReSub, events.resubs, events.reSubs)); addNativeSection(result, 'Gift Subs', firstArray(events.GiftSubs, events.giftsubs, events.giftSubs)); addNativeSection(result, 'Gift Bombs', firstArray(events.GiftBombs, events.giftbombs, events.giftBombs)); addNativeSection(result, 'Raids', firstArray(events.Raided, events.Raids, events.raided, events.raids)); addNativeSection(result, 'Reward Redemptions', firstArray(events.RewardRedemptions, events.rewardredemptions, events.rewardRedemptions)); addNativeSection(result, 'Goal Contributions', firstArray(events.GoalContributions, events.goalcontributions, events.goalContributions)); addNativeSection(result, 'Game Updates', firstArray(events.GameUpdates, events.gameupdates, events.gameUpdates)); addNativeSection(result, 'Pyramids', firstArray(events.Pyramids, events.pyramids)); addNativeSection(result, 'Hype Trains', firstArray(events.HypeTrains, events.hypetrains, events.hypeTrains)); addNativeSection(result, 'Hype Train Conductors', firstArray(data.HypeTrainConductor, data.hypeTrainConductors, hype.Conductors, hype.conductors)); addNativeSection(result, 'Hype Train Contributors', firstArray(data.HypeTrainContributors, data.hypeTrainContributors, hype.Contributors, hype.contributors)); addNativeSection(result, 'Editors', firstArray(users.Editors, users.editors)); addNativeSection(result, 'Moderators', firstArray(users.Moderator, users.Moderators, users.moderator, users.moderators)); addNativeSection(result, 'Subscribers', firstArray(users.Subscriber, users.Subscribers, users.subscriber, users.subscribers)); addNativeSection(result, 'VIPs', firstArray(users.VIPs, users.Vips, users.vips)); addNativeSection(result, 'Users', firstArray(users.Users, users.users, users.regulars)); addNativeObjectSections(result, data.Groups || data.groups, 'Groups'); addNativeSection(result, 'All Bits', firstArray(data.TopBits && data.TopBits.All, top.allBits, top.AllBits)); addNativeSection(result, 'Month Bits', firstArray(data.TopBits && data.TopBits.Month, top.monthBits, top.MonthBits)); addNativeSection(result, 'Week Bits', firstArray(data.TopBits && data.TopBits.Week, top.weekBits, top.WeekBits)); addNativeSection(result, 'Channel Rewards', firstArray(data.TopChannelRewards, top.channelRewards, top.ChannelRewards)); addNativeObjectSections(result, data.Custom || data.custom, 'Custom'); return result; }
    function firstArray(...values) { return values.find(Array.isArray) || []; }
    function addNativeSection(result, title, values) { const items = (Array.isArray(values) ? values : []).map(nativeItem).filter(Boolean); if (items.length) result.push({ title, items }); }
    function addNativeObjectSections(result, source, fallbackTitle) { if (!source || typeof source !== 'object') return; if (Array.isArray(source)) { addNativeSection(result, fallbackTitle, source); return; } Object.entries(source).forEach(([key, value]) => { if (Array.isArray(value)) addNativeSection(result, prettyTitle(key), value); }); }
    function nativeItem(value) { if (value == null) return null; if (typeof value !== 'object') return { name:String(value) }; const name = value.name || value.user || value.userName || value.username || value.displayName || value.login || value.title || 'Viewer'; const parts = []; ['amount','currency','message','count','bits','tier','viewers'].forEach(key => { if (value[key] != null && String(value[key]).trim()) parts.push(String(value[key])); }); return { name:String(name), message:parts.join(' | ') }; }
    function prettyTitle(value) { return String(value || 'Credits').replace(/([a-z])([A-Z])/g, '$1 $2'); }
    function hiddenSection(title) { const hidden = Array.isArray(settings && settings.HiddenSections) ? settings.HiddenSections : []; const key = String(title || '').toLowerCase(); return hidden.some(item => String(item || '').toLowerCase() === key); }
    function applyControl(action) { if (action === 'pause') paused = true; if (action === 'resume') paused = false; if (action === 'restart') restartRoll(); const el = document.getElementById('credits'); if (el) el.classList.toggle('paused', paused); }
    function restartRoll() { const el = document.getElementById('credits'); if (!el) return; el.style.animation = 'none'; void el.offsetWidth; el.style.animation = ''; el.classList.toggle('paused', paused); }
    function detail(item) { const parts = []; if ((!settings || settings.ShowAmounts !== false) && item.amount) parts.push([item.amount, item.currency].filter(Boolean).join(' ')); if ((!settings || settings.ShowPlatforms !== false) && item.platform) parts.push(item.platform); if ((!settings || settings.ShowMessages !== false) && item.message) parts.push(item.message); return parts.join(' | '); }
    function px(v) { return `${Number(v || 0)}px`; }
    function fontStack(value, fallback) { const name = String(value || '').trim(); const safe = name.replace(/[""\\]/g, '').replace(/\s+/g, ' ').trim(); return safe ? '""' + safe + '"", ' + fallback : fallback; }
    function escapeHtml(value) { return String(value || '').replace(/[&<>'\x22]/g, ch => { const c = ch.charCodeAt(0); if (c === 38) return '&amp;'; if (c === 60) return '&lt;'; if (c === 62) return '&gt;'; if (c === 39) return '&#39;'; return '&quot;'; }); }
  </script>
</body>
</html>";
    }
    private class HttpRequest
    {
        public string Method;
        public string Path;
        public string Body;
    }
}

public class DonationDeduplicator
{
    private readonly int Limit;
    private readonly Queue<string> Order = new Queue<string>();
    private readonly HashSet<string> SeenKeys = new HashSet<string>();

    public DonationDeduplicator(int limit)
    {
        Limit = limit;
    }

    public bool Seen(UnifiedDonationEvent donation)
    {
        string key = BuildKey(donation);
        if (SeenKeys.Contains(key))
            return true;

        SeenKeys.Add(key);
        Order.Enqueue(key);

        while (Order.Count > Limit)
            SeenKeys.Remove(Order.Dequeue());

        return false;
    }

    private static string BuildKey(UnifiedDonationEvent donation)
    {
        if (!string.IsNullOrWhiteSpace(donation.DonationId))
            return donation.ProviderName + ":" + donation.DonationId;

        return donation.ProviderName + ":" + donation.UserName + ":" + donation.Amount.ToString(CultureInfo.InvariantCulture) + ":" + donation.Message;
    }
}

public class BridgeSettings
{
    private const string Prefix = "udb_";
    private readonly IInlineInvokeProxy CPH;

    public BridgeSettings(IInlineInvokeProxy cph)
    {
        CPH = cph;
    }

    public string Get(string key, string fallback)
    {
        try
        {
            string value = CPH.GetGlobalVar<string>(Prefix + key.Replace(".", "_"), true);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
        catch
        {
            return fallback;
        }
    }

    public void Set(string key, string value, bool persisted)
    {
        CPH.SetGlobalVar(Prefix + key.Replace(".", "_"), value ?? "", persisted);
    }

    public bool GetBool(string key, bool fallback)
    {
        string value = Get(key, fallback ? "true" : "false");
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}

public class BridgeLogger
{
    private readonly IInlineInvokeProxy CPH;
    private readonly BridgeSettings Settings;
    private const string Prefix = "[DonConnect] ";

    public BridgeLogger(IInlineInvokeProxy cph, BridgeSettings settings)
    {
        CPH = cph;
        Settings = settings;
    }

    public void Info(string message)
    {
        CPH.LogDebug(Prefix + message);
    }

    public void Debug(string message)
    {
        if (Settings.GetBool("debug", false))
            CPH.LogDebug(Prefix + message);
    }

    public void Warn(string message)
    {
        CPH.LogWarn(Prefix + message);
    }
}

public static class SecretMask
{
    public static string Mask(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (value.Length <= 8)
            return "***";

        return value.Substring(0, 4) + "..." + value.Substring(value.Length - 4);
    }
}
