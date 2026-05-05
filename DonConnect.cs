using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
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
    private const string BundledDonationAlertsClientId = "18717";
    private const string BundledDonationAlertsClientSecret = "XxOAjz0FeUQlzNWjmWwzxZGpeGb57hSEt0dZskB6";

    public void Init()
    {
        EnsureRuntime();
        Runtime.Logger.Info("DonConnect initialized.");
    }

    public void Dispose()
    {
        if (Runtime != null)
            Runtime.Stop();
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

    public bool SetupCreditsIntegration()
    {
        EnsureRuntime();
        string enabled = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_ENABLED"), ReadArg("streamerbotCreditsEnabled"), "true");
        string httpUrl = FirstNonEmpty(ReadArg("STREAMERBOT_HTTP_URL"), ReadArg("streamerbotHttpUrl"), "http://127.0.0.1:7474");
        string actionName = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_ACTION"), ReadArg("streamerbotCreditsAction"), "Add Donation To Credits");
        string section = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_SECTION"), ReadArg("streamerbotCreditsSection"), "\u0414\u043e\u043d\u0430\u0442\u044b");
        string fields = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_FIELDS"), ReadArg("streamerbotCreditsFields"), "name,amount");
        string configPath = FirstNonEmpty(ReadArg("STREAMERBOT_CREDITS_CONFIG_PATH"), ReadArg("streamerbotCreditsConfigPath"), @"D:\SBBOTcodex\DonConnect\credits\credits-config.json");
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
        Runtime.Logger.Info("Donation timer reset.");
        Runtime.RefreshDonationTimerVariables();
        return true;
    }

    public bool SetupGoalTimerOverlay()
    {
        EnsureRuntime();
        string configPath = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_CONFIG_PATH"), ReadArg("donConnectOverlayConfigPath"), @"D:\SBBOTcodex\DonConnect\overlays\donconnect-overlay-config.json");
        string statePath = FirstNonEmpty(ReadArg("DONCONNECT_OVERLAY_STATE_PATH"), ReadArg("donConnectOverlayStatePath"), @"D:\SBBOTcodex\DonConnect\overlays\donconnect-overlay-state.json");
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

        public BridgeSettings Settings { get; private set; }
        public BridgeLogger Logger { get; private set; }

        public DonationBridgeRuntime(IInlineInvokeProxy cph, Dictionary<string, object> args)
        {
            CPH = cph;
            Args = args;
            Settings = new BridgeSettings(cph);
            Logger = new BridgeLogger(cph, Settings);
        }

        public void UpdateArgs(Dictionary<string, object> args)
        {
            Args = args;
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
            lines.Add("DonConnect diagnostics");
            lines.Add("Bridge=" + (IsRuntimeLeaseActive() ? "running" : "stopped")
                + " | owner=" + Short(Settings.Get("runtime.owner", "none"))
                + " | lockUntil=" + ShortTime(Settings.Get("runtime.lockUntil", "none"))
                + " | instance=" + Short(InstanceId));
            lines.Add("Last donation: " + Settings.Get("diagnostics.lastDonation", "none"));
            lines.Add("");
            AddProviderDiagnostics(lines, "DonatePay RU", "donatepayru", "apiKey");
            AddProviderDiagnostics(lines, "DonatePay EU", "donatepayeu", "apiKey");
            AddProviderDiagnostics(lines, "DonateX.gg", "donatex", "accessToken");
            AddProviderDiagnostics(lines, "StreamElements", "streamelements", "jwtToken");
            AddProviderDiagnostics(lines, "Streamlabs", "streamlabs", "token");
            AddProviderDiagnostics(lines, "Donate.Stream", "donatestream", "token");
            AddProviderDiagnostics(lines, "deStream", "destream", "accessToken");
            ShowPopup(string.Join(Environment.NewLine, lines.ToArray()));
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

            ExportDonation(donationEvent);
            UpdateDonationGoal(donationEvent);
            UpdateDonationTimer(donationEvent);
            SaveDonationDiagnostics(donationEvent);
            TriggerDonationEvents(donationEvent);
            SendDonationToCredits(donationEvent);
        }

        public void HandleGoalOnly(UnifiedDonationEvent donationEvent)
        {
            UpdateDonationGoal(donationEvent);
        }

        public void RefreshDonationTimerVariables()
        {
            UpdateDonationTimer(null);
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
            DateTime endsAt = ReadDateSetting("timer.endsAt", now);
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
            }

            double maxSeconds = (double)ReadDecimalSetting("timer.maxSeconds", 0);
            if (maxSeconds > 0 && (endsAt - now).TotalSeconds > maxSeconds)
                endsAt = now.AddSeconds(maxSeconds);

            double remainingSeconds = Math.Max(0, (endsAt - now).TotalSeconds);
            Settings.Set("timer.endsAt", endsAt.ToString("o"), true);

            string title = FirstNonEmptyLocal(Settings.Get("timer.title", ""), "\u0414\u043e\u043d\u0430\u0442\u043d\u044b\u0439 \u0442\u0430\u0439\u043c\u0435\u0440");
            string timerText = FormatDuration(remainingSeconds);
            string addedText = FormatDuration(addedSeconds);

            SetGoalVar("donConnectTimerTitle", title);
            SetGoalVar("donConnectTimerSeconds", Math.Floor(remainingSeconds).ToString(CultureInfo.InvariantCulture));
            SetGoalVar("donConnectTimerText", timerText);
            SetGoalVar("donConnectTimerEndsAt", endsAt.ToString("o"));
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
            string statePath = Settings.Get("overlay.statePath", @"D:\SBBOTcodex\DonConnect\overlays\donconnect-overlay-state.json");
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
                timer["title"] = CPH.GetGlobalVar<string>("donConnectTimerTitle", true) ?? "";
                timer["seconds"] = CPH.GetGlobalVar<string>("donConnectTimerSeconds", true) ?? "0";
                timer["text"] = CPH.GetGlobalVar<string>("donConnectTimerText", true) ?? "00:00:00";
                timer["endsAt"] = CPH.GetGlobalVar<string>("donConnectTimerEndsAt", true) ?? "";
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
                        if (!string.IsNullOrWhiteSpace(value))
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
        }

        private void AddProvider(IDonationProvider provider)
        {
            Providers.Add(provider);
        }

        private void ExportDonation(UnifiedDonationEvent e)
        {
            // Эти переменные видны последующим actions в Streamer.bot.
            CPH.SetArgument("donationSource", e.Source ?? e.ProviderName);
            CPH.SetArgument("donationUser", string.IsNullOrWhiteSpace(e.UserName) ? "Anonymous" : e.UserName);
            CPH.SetArgument("donationAmount", e.Amount.ToString(CultureInfo.InvariantCulture));
            CPH.SetArgument("donationCurrency", e.Currency ?? "");
            CPH.SetArgument("donationMessage", e.Message ?? "");
            CPH.SetArgument("donationId", e.DonationId ?? "");
            CPH.SetArgument("donationTimestamp", e.Timestamp.ToUniversalTime().ToString("o"));
            CPH.SetArgument("donationRawJson", e.RawJson ?? "");
            CPH.SetArgument("donationIsAnonymous", e.IsAnonymous.ToString());

            CPH.SetArgument("tipSource", e.Source ?? e.ProviderName);
            CPH.SetArgument("tipUser", string.IsNullOrWhiteSpace(e.UserName) ? "Anonymous" : e.UserName);
            CPH.SetArgument("tipUsername", string.IsNullOrWhiteSpace(e.UserName) ? "Anonymous" : e.UserName);
            CPH.SetArgument("tipName", string.IsNullOrWhiteSpace(e.UserName) ? "Anonymous" : e.UserName);
            CPH.SetArgument("tipAmount", e.Amount.ToString(CultureInfo.InvariantCulture));
            CPH.SetArgument("tipCurrency", e.Currency ?? "");
            CPH.SetArgument("tipMessage", e.Message ?? "");

            Logger.Info("Донат: " + e.ProviderName + " / " + e.UserName + " / " + e.Amount.ToString(CultureInfo.InvariantCulture) + " " + e.Currency);
        }
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
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
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
        System.Diagnostics.Process.Start(url);

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
                if (TryRefreshToken())
                    accessToken = Settings.Get(ProviderKey + ".accessToken", accessToken);
            }
            catch (Exception ex)
            {
                Logger.Warn("DonationAlerts: соединение прервано. " + ex.Message);
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
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
        Task.Run(() => PollLoop(endpoint, Cancellation.Token));
        Logger.Info("Generic API polling запущен.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        if (Cancellation != null)
            Cancellation.Cancel();
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
                foreach (var donation in Fetch(endpoint))
                {
                    if (DonationReceived != null)
                        DonationReceived(donation);
                }
            }
            catch (Exception ex)
            {
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

public class PlaceholderProvider : IDonationProvider
{
    private readonly BridgeSettings Settings;
    private readonly BridgeLogger Logger;
    private readonly string Key;

    public string ProviderName { get; private set; }
    public event Action<UnifiedDonationEvent> DonationReceived;

    public PlaceholderProvider(string providerName, BridgeSettings settings, BridgeLogger logger, string key)
    {
        ProviderName = providerName;
        Settings = settings;
        Logger = logger;
        Key = key;
    }

    public Task ConnectAsync()
    {
        if (Settings.GetBool(Key + ".enabled", false))
            Logger.Warn(ProviderName + ": заготовка включена, но реальный адаптер еще не реализован. Добавьте актуальную API-схему сервиса.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        return Task.FromResult(0);
    }

    public Task<bool> ValidateCredentialsAsync()
    {
        Logger.Warn(ProviderName + ": проверка токена пока недоступна в placeholder-адаптере.");
        return Task.FromResult(false);
    }
}

public class StreamElementsProvider : IDonationProvider
{
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
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
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
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
        Task.Run(() => PollLoop(token, Cancellation.Token));
        Logger.Info("Streamlabs polling запущен.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        if (Cancellation != null)
            Cancellation.Cancel();
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
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
        Settings.Set(ProviderKey + ".diagnostics.polling", "started", true);
        Settings.Set(ProviderKey + ".diagnostics.lastError", "none", true);
        Task.Run(() => PollLoop(apiKey, Cancellation.Token));
        Logger.Info(ProviderDisplayName + " polling запущен.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        if (Cancellation != null)
            Cancellation.Cancel();
        Settings.Set(ProviderKey + ".diagnostics.polling", "stopped", true);
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
        Task.Run(() => PollLoop(endpoint, token, Cancellation.Token));
        Logger.Info("Donate.Stream polling запущен.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        if (Cancellation != null)
            Cancellation.Cancel();
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
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
        Task.Run(() => PollLoop(clientId, accessToken, Cancellation.Token));
        Logger.Info("deStream polling запущен.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        if (Cancellation != null)
            Cancellation.Cancel();
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
            return Task.FromResult(0);
        }

        Cancellation = new CancellationTokenSource();
        Settings.Set("donatex.diagnostics.polling", "started", true);
        Settings.Set("donatex.diagnostics.lastError", "none", true);
        Task.Run(() => PollLoop(accessToken, Cancellation.Token));
        Logger.Info("DonateX polling запущен.");
        return Task.FromResult(0);
    }

    public Task DisconnectAsync()
    {
        if (Cancellation != null)
            Cancellation.Cancel();
        Settings.Set("donatex.diagnostics.polling", "stopped", true);
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
