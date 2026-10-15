using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using MaxMind.GeoIP2.Responses;
using NChecker.models;
using NChecker.models.subscription;
using NChecker.utils;
using Sayaka.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NChecker.classes
{
    internal class NetworkingManager
    {
        private readonly HttpClient _httpClient;

        internal NetworkingManager()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(5),
                ConnectTimeout = TimeSpan.FromSeconds(3)
            };
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(3);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(ProviderFakeUserAgent.Random);
        }
        async internal Task<SubscriptionData[]> ParseSubscriptions(SettingsData a1, SubscriptionSourceData[] a2)
        {
            List<SubscriptionData> subscriptions = new List<SubscriptionData>();
            foreach (SubscriptionSourceData subscriptionSourceData in a2)
            {
                if (string.IsNullOrEmpty(subscriptionSourceData.Url) || !subscriptionSourceData.Enabled) continue;
                try
                {
                    string data = await _httpClient.GetStringAsync(subscriptionSourceData.Url);
                    try
                    {
                        data = Encoding.UTF8.GetString(Convert.FromBase64String(data));
                    }
                    catch (FormatException) { }
                    SubscriptionData subscriptionData = new SubscriptionData(subscriptionSourceData.Url, subscriptionSourceData.FilterType);
                    string[] lines = data.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string line in lines)
                    {
                        string trimmedLine = line.Trim();
                        if (trimmedLine.StartsWith("#")) continue;
                        ServerData serverData = new(trimmedLine);
                        if (!serverData.IsValid()) continue;
                        serverData.Subscription = subscriptionData;
                        subscriptionData.AddServer(serverData);
                    }
                    subscriptions.Add(subscriptionData);
                    if (a1.Logging) Logger.Info("Успешно загружено => " + subscriptionData.Url);
                }
                catch (TaskCanceledException) { }
                catch (Exception exc)
                {
                    Logger.Error(exc.ToString());
                }
            }
            return subscriptions.ToArray();
        }

        #region Вынесенные сетевые тесты

        async internal Task ExecuteCountryTestAsync(ServerData server, DatabaseReader reader)
        {
            CountryResponse response = null;
            try
            {
                if (string.IsNullOrEmpty(server.Address) || server.Port == 0) return;
                try
                {
                    if (!IPAddress.TryParse(server.Address, out IPAddress? ip))
                    {
                        using var cts = new CancellationTokenSource(800);
                        IPAddress[] addresses = await Dns.GetHostAddressesAsync(server.Address, cts.Token);
                        ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                             ?? addresses.FirstOrDefault();
                    }
                    if (ip == null) return;
                    response = reader.Country(ip);
                    server.SetCountry(response);
                }
                catch (SocketException) { }
                catch (OperationCanceledException) { }
                catch (AddressNotFoundException) { }
            }
            catch (Exception exc)
            {
                server.SetCountry(response);
            }
        }

        async internal Task ExecuteTcpPingTestAsync(ServerData server, SettingsData settings)
        {
            server.IsAlive = false;
            server.Delay = -1;

            if (string.IsNullOrEmpty(server.Address) || server.Port == 0) return;

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
                    if (settings.Logging) Logger.Info($"[TCP +] {server.Address}:{server.Port} - {stopwatch.ElapsedMilliseconds} ms");
                    return;
                }
                if (settings.Logging) Logger.Info($"[TCP -] {server.Address}:{server.Port} ");
            }
            catch { }
        }

        #endregion
    }
}
