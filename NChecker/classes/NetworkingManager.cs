using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using MaxMind.GeoIP2.Responses;
using NChecker.models;
using Newtonsoft.Json;
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

        async internal Task<SubscriptionData[]> ParseSubscriptions(SettingsData a1, SubscriptionSourceData[] a2)
        {
            List<SubscriptionData> subscriptions = new List<SubscriptionData>();

            foreach (SubscriptionSourceData subscriptionSourceData in a2)
            {
                if (string.IsNullOrEmpty(subscriptionSourceData.Url) || !subscriptionSourceData.Enabled) continue;
                try
                {
                    string data = await _httpClient.GetStringAsync(subscriptionSourceData.Url);
                    string[] lines = data.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string line in lines)
                    {
                        string trimmedLine = line.Trim();
                        if (trimmedLine.StartsWith("#")) continue;
                        try
                        {
                            ServerData serverData = new(trimmedLine);
                            if (serverData.Address.Equals("0.0.0.0") || serverData.Address.Equals("127.0.0.1")) continue;

                            SubscriptionData subscriptionData = new SubscriptionData(subscriptionSourceData.Url, subscriptionSourceData.FilterType);
                            serverData.Subscription = subscriptionData;

                            subscriptionData.AddServer(serverData);

                            subscriptions.Add(subscriptionData);
                        }
                        catch (JsonReaderException exc) { }
                        catch (Exception exc)
                        {
                            Console.WriteLine(subscriptionSourceData.Url);
                            Console.WriteLine(line);
                            Console.WriteLine(exc.ToString());
                        }
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
                Console.WriteLine(exc);
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
                    return;
                }
            }
            catch { }
        }

        #endregion
    }
}
