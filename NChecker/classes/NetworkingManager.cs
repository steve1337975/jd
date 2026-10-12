using MaxMind.GeoIP2;
using NChecker.models;
using NChecker.models.local;
using Sayaka.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace NChecker.classes
{
    internal class NetworkingManager
    {
        // Одиночный HttpClient с настроенным SocketsHttpHandler для предотвращения утечки сокетов
        private readonly HttpClient _httpClient;

        internal NetworkingManager()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(15),
                ConnectTimeout = TimeSpan.FromSeconds(3)
            };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(ProviderFakeUserAgent.Random);
        }

        async internal Task<SubscriptionData[]> ParseSubscriptions(SettingsData a1, FilteredSubscriptionsSourcesData[] a2)
        {
            List<SubscriptionData> subscriptions = new List<SubscriptionData>();

            foreach (FilteredSubscriptionsSourcesData subscriptionSourceData in a2)
            {
                if (string.IsNullOrEmpty(subscriptionSourceData.Url) || !subscriptionSourceData.Enabled) continue;
                try
                {
                    // Используем общий настроенный HttpClient
                    string data = await _httpClient.GetStringAsync(subscriptionSourceData.Url);

                    // Корректно бьем по любым символам переноса строки (\r\n или \n)
                    string[] lines = data.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string line in lines)
                    {
                        try
                        {
                            string trimmedLine = line.Trim();
                            if (trimmedLine.StartsWith("#") || trimmedLine.Length < 10 || trimmedLine.Contains("::/")) continue;

                            SubscriptionData subscriptionData = new SubscriptionData(subscriptionSourceData.Url, subscriptionSourceData.GetFilter());

                            ServerData serverData = new(trimmedLine);
                            serverData.Subscription = subscriptionData;

                            subscriptionData.AddServer(serverData);
                            subscriptions.Add(subscriptionData);
                        }
                        catch { }
                    }

                    if (a1.Logging) Console.WriteLine("Успешно загружено => " + subscriptionSourceData.Url);
                }
                catch { }
            }
            return subscriptions.ToArray();
        }

        #region Вынесенные сетевые тесты

        async internal Task ExecuteCountryTestAsync(ServerData server, DatabaseReader reader)
        {
            try
            {
                if (server.Address == "unknown" || server.Port == 0 || server.Protocol == "broken") return;

                if (!IPAddress.TryParse(server.Address, out IPAddress? ip))
                {
                    using var cts = new CancellationTokenSource(800);
                    IPAddress[] addresses = await Dns.GetHostAddressesAsync(server.Address, cts.Token);

                    ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                         ?? addresses.FirstOrDefault();
                }

                if (ip == null) return;

                var response = reader.Country(ip);
                server.CountryCode = response.Country.IsoCode ?? "UNK";
            }
            catch
            {
                server.CountryCode = "UNK";
            }
        }

        async internal Task ExecuteTcpPingTestAsync(ServerData server, SettingsData settings)
        {
            server.IsAlive = false;
            server.TcpPing = -1;

            if (server.Address == "unknown" || server.Port == 0 || server.Protocol == "broken") return;

            try
            {
                using TcpClient client = new TcpClient();
                client.LingerState = new LingerOption(true, 0);

                Stopwatch stopwatch = Stopwatch.StartNew();
                int timeoutSeconds = Math.Min(settings.Timeout, 3);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

                await client.ConnectAsync(server.Address, server.Port, cts.Token);
                stopwatch.Stop();

                if (client.Connected)
                {
                    server.TcpPing = stopwatch.ElapsedMilliseconds;
                    server.IsAlive = true;
                    return;
                }
            }
            catch { }
        }

        async internal Task ExecuteHttpPingTestAsync(ServerData server, SettingsData settings)
        {
            server.IsAlive = false;
            server.TcpPing = -1;

            if (server.Address == "unknown" || server.Port == 0 || server.Protocol == "broken") return;

            foreach (string testUrl in ServerData.testUrls)
            {
                try
                {
                    int timeoutSeconds = Math.Min(settings.Timeout, 3);
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    using HttpResponseMessage response = await _httpClient.GetAsync(testUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    stopwatch.Stop();

                    if (response.IsSuccessStatusCode)
                    {
                        server.TcpPing = stopwatch.ElapsedMilliseconds;
                        server.IsAlive = true;
                        return;
                    }
                }
                catch { }
            }
        }

        #endregion
    }
}
