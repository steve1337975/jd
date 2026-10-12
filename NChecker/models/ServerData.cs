using MaxMind.GeoIP2;
using NChecker.models.local;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;

namespace NChecker.models
{
    internal class ServerData
    {
        internal static string[] testUrls = { "http://cloudflare.com", "http://gstatic.com" };

        // Все свойства имеют дефолтные безопасные значения на случай ошибок парсинга
        internal string Url { get; private set; } = string.Empty;
        internal string Protocol { get; private set; } = "unknown";
        internal string Address { get; private set; } = "unknown";
        internal int Port { get; private set; } = 0;
        internal string Name { get; private set; } = "Unnamed";
        internal string CountryCode { get; private set; } = "UNK";
        internal long TcpPing { get; private set; } = -1;
        internal bool IsAlive { get; private set; }
        internal SubscriptionData Subscription { get; set; }
        internal ServerData(string row)
        {
            if (string.IsNullOrWhiteSpace(row)) return;

            row = row.Trim();
            Url = row;

            try
            {
                if (row.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase))
                {
                    ParseVmess(row);
                }
                else if (row.StartsWith("vless://", StringComparison.OrdinalIgnoreCase) ||
                         row.StartsWith("ss://", StringComparison.OrdinalIgnoreCase) ||
                         row.StartsWith("trojan://", StringComparison.OrdinalIgnoreCase) ||
                         row.StartsWith("shadowsocks://", StringComparison.OrdinalIgnoreCase))
                {
                    ParseStandardVpnUri(row);
                }
                else
                {

                    ParseGenericFallback(row);
                }
            }
            catch (Exception)
            {
                Protocol = "broken";
            }
        }

        #region Приватные методы безопасного парсинга строк

        private void ParseVmess(string row)
        {
            Protocol = "vmess";
            string base64Part = row.Substring(8).Trim();

            int mod4 = base64Part.Length % 4;
            if (mod4 > 0) base64Part += new string('=', 4 - mod4);

            byte[] bytes = Convert.FromBase64String(base64Part);
            string jsonString = Encoding.UTF8.GetString(bytes);

            var jsonModel = JsonConvert.DeserializeObject<VmessJsonModel>(jsonString);
            if (jsonModel != null)
            {
                Address = jsonModel.Address ?? "unknown";

                // Используем Convert.ToInt32, так как в JSON порт может прилететь и строкой "443", и числом 443
                Port = Convert.ToInt32(jsonModel.Port ?? 0);

                Name = !string.IsNullOrEmpty(jsonModel.Remarks)
                    ? HttpUtility.UrlDecode(jsonModel.Remarks)
                    : "Unnamed VMess";
            }
        }

        private void ParseStandardVpnUri(string row)
        {
            int schemeBounds = row.IndexOf("://");
            if (schemeBounds == -1) return;

            Protocol = row.Substring(0, schemeBounds).ToLower();

            // Извлекаем имя сервера (всё, что после знака '#')
            string mainPart = row.Substring(schemeBounds + 3);
            int hashIndex = mainPart.IndexOf('#');
            if (hashIndex != -1)
            {
                string rawName = mainPart.Substring(hashIndex + 1);
                Name = HttpUtility.UrlDecode(rawName).Trim();
                mainPart = mainPart.Substring(0, hashIndex); // Отрезаем имя от сетевой части
            }

            // Извлекаем параметры запроса (всё, что после знака '?')
            int queryIndex = mainPart.IndexOf('?');
            if (queryIndex != -1)
            {
                mainPart = mainPart.Substring(0, queryIndex);
            }

            // Отсекаем userInfo (токен или base64 авторизацию перед знаком '@')
            int atIndex = mainPart.LastIndexOf('@');
            string connectionPart = atIndex != -1 ? mainPart.Substring(atIndex + 1) : mainPart;

            // Вытаскиваем IP/Хост и Порт
            int portIndex = connectionPart.LastIndexOf(':');
            if (portIndex != -1)
            {
                // Очищаем от квадратных скобок на случай IPv6 адресов: [2001:db8::1]
                Address = connectionPart.Substring(0, portIndex).Trim('[', ']');
                if (int.TryParse(connectionPart.Substring(portIndex + 1), out int parsedPort))
                {
                    Port = parsedPort;
                }
            }
            else
            {
                Address = connectionPart;
            }
        }

        private void ParseGenericFallback(string row)
        {
            int portIndex = row.LastIndexOf(':');
            if (portIndex != -1 && int.TryParse(row.Substring(portIndex + 1), out int parsedPort))
            {
                Address = row.Substring(0, portIndex);
                Port = parsedPort;
                Protocol = "raw";
            }
        }

        #endregion

        #region Асинхронные тесты

        async internal Task TestCountry(DatabaseReader a1)
        {
            try
            {
                // Защита: если адрес не распарсился, выходим
                if (Address == "unknown" || Port == 0 || Protocol == "broken") return;

                if (!IPAddress.TryParse(Address, out IPAddress? ip))
                {
                    // На GitHub Actions жестко зажимаем таймаут DNS-запроса до 800мс.
                    // Без этого битые домены будут намертво вешать потоки проверки!
                    using var cts = new CancellationTokenSource(800);
                    IPAddress[] addresses = await Dns.GetHostAddressesAsync(Address, cts.Token);

                    ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                         ?? addresses.FirstOrDefault();
                }

                if (ip == null) return;

                var response = a1.Country(ip);
                CountryCode = response.Country.IsoCode ?? "UNK";
            }
            catch
            {
                CountryCode = "UNK";
            }
        }

        async internal Task TestTcpPing(SettingsData a1)
        {
            IsAlive = false;
            TcpPing = -1;

            // Быстрый отсев заведомо сломанных строк
            if (Address == "unknown" || Port == 0 || Protocol == "broken") return;

            try
            {
                using TcpClient client = new TcpClient();
                client.LingerState = new LingerOption(true, 0);

                Stopwatch stopwatch = Stopwatch.StartNew();

                // Ограничиваем максимальный таймаут до разумных 3 секунд для облачных серверов
                int timeoutSeconds = Math.Min(a1.Timeout, 3);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

                // .NET при отмене токена разорвет системный сокет и освободит ресурсы
                await client.ConnectAsync(Address, Port, cts.Token);
                stopwatch.Stop();

                if (client.Connected)
                {
                    TcpPing = stopwatch.ElapsedMilliseconds;
                    IsAlive = true;

                    // На GitHub Actions лог каждой строки не пишем, чтобы не тормозить I/O консоли
                    if (a1.Logging && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true")
                        Console.WriteLine($"[TCP +] {Address}:{Port} / {TcpPing} ms");

                    return;
                }
            }
            catch { }

            if (a1.Logging && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true")
                Console.WriteLine($"[TCP -] {Address}:{Port}");
        }

        #endregion
    }
}
