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
        Runtime.RegisterCustomTriggers();
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
        Runtime.Settings.Set("streamlabs.enabled", "true", true);
        Runtime.Logger.Info("Streamlabs token сохранен. Реальный адаптер будет включен после добавления актуальной схемы API.");
        return true;
    }

    public bool SetupDonatePay()
    {
        EnsureRuntime();
        string apiKey = ReadArg("donatePayApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Runtime.Logger.Warn("DonatePay: укажите donatePayApiKey.");
            return false;
        }

        Runtime.Settings.Set("donatepay.apiKey", apiKey.Trim(), true);
        Runtime.Settings.Set("donatepay.enabled", "true", true);
        Runtime.Logger.Info("DonatePay API key сохранен. Реальный polling-адаптер будет включен после проверки актуального endpoint.");
        return true;
    }

    public bool Status()
    {
        EnsureRuntime();
        Runtime.LogStatus();
        return true;
    }

    public bool StartBridge()
    {
        EnsureRuntime();
        Runtime.Start();
        return true;
    }

    public bool StopBridge()
    {
        EnsureRuntime();
        Runtime.Stop();
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

    private void EnsureRuntime()
    {
        if (Runtime == null)
            Runtime = new DonationBridgeRuntime(CPH, args);
        else
            Runtime.UpdateArgs(args);
    }

    private string ReadArg(string name)
    {
        object value;
        if (args != null && args.TryGetValue(name, out value) && value != null)
            return value.ToString();
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
            Stop();
            if (!AcquireRuntimeLease())
                return;

            Logger.Info("Запуск DonConnect.");

            AddProvider(CreateDonationAlertsProvider());
            AddProvider(new GenericApiProvider(Settings, Logger));
            AddProvider(new StreamElementsProvider(Settings, Logger));
            AddProvider(new StreamlabsProvider(Settings, Logger));
            AddProvider(new DonatePayProvider(Settings, Logger));
            AddProvider(new DonateStreamProvider(Settings, Logger));
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
                    RefreshRuntimeLease();
                    await Task.Delay(TimeSpan.FromSeconds(15), token);
                }
            }, token);

            return true;
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
            RegisterTrigger("StreamElements donation", "donconnect.donation.streamelements", "DonConnect", "Donations");
            RegisterTrigger("Streamlabs donation", "donconnect.donation.streamlabs", "DonConnect", "Donations");
            RegisterTrigger("Generic API donation", "donconnect.donation.genericapi", "DonConnect", "Donations");
            RegisterTrigger("Donate.Stream donation", "donconnect.donation.donatestream", "DonConnect", "Donations");
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
            Logger.Info("Status: Generic API enabled=" + Settings.GetBool("generic.enabled", false)
                + ", endpoint=" + (string.IsNullOrWhiteSpace(Settings.Get("generic.endpoint", "")) ? "missing" : Settings.Get("generic.endpoint", "")));
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
                System.Windows.Forms.MessageBox.Show(message, "DonConnect");
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
            TriggerDonationEvents(donationEvent);
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

            string providerEventName = GetProviderEventName(donationEvent.ProviderName);
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
            result["tipSource"] = result["donationSource"];
            result["tipUser"] = result["donationUser"];
            result["tipAmount"] = result["donationAmount"];
            result["tipCurrency"] = result["donationCurrency"];
            result["tipMessage"] = result["donationMessage"];
            return result;
        }

        private string GetProviderEventName(string providerName)
        {
            string normalized = (providerName ?? "").Replace(".", "").Replace(" ", "").Replace("/", "").ToLowerInvariant();
            if (normalized == "donationalerts")
                return "donconnect.donation.donationalerts";
            if (normalized == "donatepay")
                return "donconnect.donation.donatepay";
            if (normalized == "streamelements")
                return "donconnect.donation.streamelements";
            if (normalized == "streamlabs")
                return "donconnect.donation.streamlabs";
            if (normalized == "genericapi")
                return "donconnect.donation.genericapi";
            if (normalized == "donatestream")
                return "donconnect.donation.donatestream";
            if (normalized == "donatexgg" || normalized == "donatex")
                return "donconnect.donation.donatex";
            return "";
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

public class StreamlabsProvider : PlaceholderProvider
{
    public StreamlabsProvider(BridgeSettings settings, BridgeLogger logger)
        : base("Streamlabs", settings, logger, "streamlabs")
    {
    }
}

public class DonatePayProvider : PlaceholderProvider
{
    public DonatePayProvider(BridgeSettings settings, BridgeLogger logger)
        : base("DonatePay", settings, logger, "donatepay")
    {
    }
}

public class DonateStreamProvider : PlaceholderProvider
{
    public DonateStreamProvider(BridgeSettings settings, BridgeLogger logger)
        : base("Donate.Stream / DonateStream", settings, logger, "donatestream")
    {
    }
}

public class DonateXProvider : PlaceholderProvider
{
    public DonateXProvider(BridgeSettings settings, BridgeLogger logger)
        : base("DonateX.gg", settings, logger, "donatex")
    {
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
