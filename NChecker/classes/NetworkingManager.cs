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
                    string data = await _httpClient.GetStringAsync(subscriptionSourceData.Url);
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
                if (string.IsNullOrEmpty(server.Address) || server.Port == 0 || server.Protocol == "broken") return;

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
            server.Delay = -1;

            if (string.IsNullOrEmpty(server.Address) || server.Port == 0 || server.Protocol == "broken") return;

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
                    server.Delay = stopwatch.ElapsedMilliseconds;
                    server.IsAlive = true;
                    return;
                }
            }
            catch { }
        }

        #endregion
    }
}
