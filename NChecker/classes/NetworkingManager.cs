using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;
using NChecker.models;
using NChecker.models.subscription;
using NChecker.utils;
using Sayaka.Common;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NChecker.classes
{
    internal class NetworkingManager
    {
        private readonly HttpClient httpClient;
        private string[] sni_domains;
        private DatabaseReader reader;
        private MemoryStream dbMemoryStream;

        // Три независимых HashSet для каждого типа подписки
        private readonly HashSet<string> _blackHashes = new HashSet<string>();
        private readonly HashSet<string> _whiteHashes = new HashSet<string>();
        private readonly HashSet<string> _universalHashes = new HashSet<string>();

        internal NetworkingManager()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(5),
                ConnectTimeout = TimeSpan.FromSeconds(3)
            };
            httpClient = new HttpClient(handler);
            httpClient.Timeout = TimeSpan.FromSeconds(3);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(ProviderFakeUserAgent.Random);
        }

        internal async Task Init()
        {
            await InitializeDatabaseAsync();
            await InitSniDomains();
        }

        internal async Task InitSniDomains()
        {
            try
            {
                string data = await httpClient.GetStringAsync("https://cdn.jsdelivr.net/gh/hxehex/russia-mobile-internet-whitelist@main/whitelist.txt");
                sni_domains = data.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            }
            catch (Exception exc)
            {
                Logger.Error($"Ошибка инициализации SNI Whitelist => {exc.Message}");
                sni_domains = Array.Empty<string>();
            }
        }

        private async Task InitializeDatabaseAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    using (Stream httpStream = await client.GetStreamAsync("https://cdn.jsdelivr.net/npm/geolite2-country/GeoLite2-Country.mmdb.gz"))
                    {
                        using (GZipStream decompressionStream = new GZipStream(httpStream, CompressionMode.Decompress))
                        {
                            dbMemoryStream = new MemoryStream();
                            await decompressionStream.CopyToAsync(dbMemoryStream);
                            dbMemoryStream.Position = 0;
                        }
                    }
                }
                reader = new DatabaseReader(dbMemoryStream);

                Logger.Info("База GeoLite2 успешно загружена в память и готова к работе!");
            }
            catch (Exception ex)
            {
                Logger.Error($"Критическая ошибка при обновлении базы GeoIP => {ex.Message}");
            }
        }

        internal async Task<SubscriptionData[]> ParseSubscriptions(SettingsData a1, SourceSubscriptionData[] a2)
        {
            List<SubscriptionData> subscriptions = new List<SubscriptionData>();

            foreach (SourceSubscriptionData subscriptionSourceData in a2)
            {
                try
                {
                    string data = await httpClient.GetStringAsync(subscriptionSourceData.Url);
                    try
                    {
                        data = Encoding.UTF8.GetString(Convert.FromBase64String(data));
                    }
                    catch (FormatException) { }

                    SubscriptionData subscriptionData = SubscriptionData.Create(subscriptionSourceData);

                    // Выбираем правильный HashSet в зависимости от типа подписки
                    HashSet<string> targetHashSet = subscriptionData.FilterType switch
                    {
                        SubscriptionType.Black => _blackHashes,
                        SubscriptionType.White => _whiteHashes,
                        _ => _universalHashes
                    };

                    string[] lines = data.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    int i = 1;

                    foreach (string line in lines)
                    {
                        string trimmedLine = line.Trim();
                        if (trimmedLine.StartsWith("#")) continue;

                        ServerData serverData = new ServerData(trimmedLine);
                        if (!serverData.IsValid()) continue;

                        string hash = serverData.GetHashData();

                        // Дедупликация строго внутри соответствующего списка
                        if (!targetHashSet.Add(hash)) continue;

                        serverData.SortedIndex = i;
                        serverData.FilterType = subscriptionData.FilterType;
                        subscriptionData.Servers.Add(serverData);
                        i++;
                    }

                    subscriptions.Add(subscriptionData);
                    if (a1.Logging) Logger.Info($"Успешно загружено => {subscriptionData.Url} / {subscriptionData.Servers.Count} серверов");
                }
                catch (Exception exc)
                {
                    Logger.Error($"Ошибка парсинга подписки => {subscriptionSourceData.Url} / {exc.Message}");
                }
            }
            return subscriptions.ToArray();
        }

        #region Вынесенные сетевые тесты

        internal async Task ExecuteCountryTestAsync(ServerData server)
        {
            if (!server.IsValid()) return;
            CountryResponse response = null;
            try
            {
                if (!IPAddress.TryParse(server.Address, out IPAddress ip))
                {
                    using (CancellationTokenSource cts = new CancellationTokenSource(800))
                    {
                        IPAddress[] addresses = await Dns.GetHostAddressesAsync(server.Address, cts.Token);
                        ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses.FirstOrDefault();
                    }
                }
                if (ip == null) return;
                response = reader.Country(ip);
            }
            catch { }
            server.SetCountry(response);
        }

        internal async Task ExecuteTcpPingTestAsync(ServerData server, SettingsData settings)
        {
            server.IsAlive = false;
            server.Delay = -1;
            if (!server.IsValid()) return;

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.LingerState = new LingerOption(true, 0);
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.Timeout)))
                    {
                        await client.ConnectAsync(server.Address, server.Port, cts.Token);
                        stopwatch.Stop();

                        if (client.Connected)
                        {
                            server.Delay = stopwatch.ElapsedMilliseconds;
                            server.IsAlive = true;
                            //if (settings.Logging) Logger.Info($"[TCP +] {server.Address}:{server.Port} - {stopwatch.ElapsedMilliseconds} ms");
                            return;
                        }
                    }
                }
                //if (settings.Logging) Logger.Info($"[TCP -] {server.Address}:{server.Port} ");
            }
            catch
            {
                //if (settings.Logging) Logger.Info($"[TCP -] {server.Address}:{server.Port} ");
            }
        }

        #endregion
    }
}
