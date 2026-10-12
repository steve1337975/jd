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
        // Сырые и распарсенные данные сервера
         string Url { get;  set; } = string.Empty;
        internal string Protocol { get; private set; } = "unknown";
        internal string Address { get; private set; } = "unknown";
        internal int Port { get; private set; } = 0;

        // Оригинальное имя из подписки (какое было изначально)
        internal string OriginalName { get; private set; } = "Unnamed";

        // Текущее имя (которое пойдет на экспорт)
        internal string Name { get; set; } = "Unnamed";

        // Параметры запроса (всё, что шло после '?' в vless/ss/trojan)
        // Пригодится, если настройки потребуют сохранить параметры сети (security, sni и т.д.)
        internal Dictionary<string, string> QueryParameters { get; private set; } = new();

        // Сетевые свойства (заполняются в App)
        internal string CountryCode { get; set; } = "UNK";
        internal long TcpPing { get; set; } = -1;
        internal bool IsAlive { get; set; }
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
                    ParseStandardUri(row);
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
                Port = Convert.ToInt32(jsonModel.Port ?? 0);

                string parsedName = !string.IsNullOrEmpty(jsonModel.Remarks)
                    ? HttpUtility.UrlDecode(jsonModel.Remarks).Trim()
                    : "Unnamed VMess";

                OriginalName = parsedName;
                Name = parsedName;
            }
        }

        private void ParseStandardUri(string row)
        {
            int schemeBounds = row.IndexOf("://");
            if (schemeBounds == -1) return;

            Protocol = row.Substring(0, schemeBounds).ToLower();
            string mainPart = row.Substring(schemeBounds + 3);

            // 1. Вытаскиваем оригинальное имя сервера (после '#')
            int hashIndex = mainPart.IndexOf('#');
            if (hashIndex != -1)
            {
                string rawName = mainPart.Substring(hashIndex + 1);
                string parsedName = HttpUtility.UrlDecode(rawName).Trim();

                OriginalName = parsedName;
                Name = parsedName;

                mainPart = mainPart.Substring(0, hashIndex);
            }

            // 2. Вытаскиваем параметры запроса (после '?')
            int queryIndex = mainPart.IndexOf('?');
            if (queryIndex != -1)
            {
                string queryString = mainPart.Substring(queryIndex + 1);
                ParseQueryString(queryString);

                mainPart = mainPart.Substring(0, queryIndex);
            }

            // 3. Отсекаем userInfo (токен/пароль до '@')
            int atIndex = mainPart.LastIndexOf('@');
            string connectionPart = atIndex != -1 ? mainPart.Substring(atIndex + 1) : mainPart;

            // 4. Парсим адрес и порт
            int portIndex = connectionPart.LastIndexOf(':');
            if (portIndex != -1)
            {
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

        private void ParseQueryString(string queryString)
        {
            if (string.IsNullOrEmpty(queryString)) return;

            // Бьем строку параметров на пары key=value
            string[] pairs = queryString.Split('&');
            foreach (string pair in pairs)
            {
                string[] kv = pair.Split('=');
                if (kv.Length == 2)
                {
                    string key = HttpUtility.UrlDecode(kv[0]).ToLower().Trim();
                    string value = HttpUtility.UrlDecode(kv[1]).Trim();
                    QueryParameters[key] = value;
                }
            }
        }

        internal string GetExportUrl()
        {
            if (Protocol == "broken" || string.IsNullOrEmpty(Url)) return Url;

            try
            {
                // Для VMess пересобираем Base64 JSON, чтобы обновить поле 'ps' (remarks)
                if (Protocol.Equals("vmess", StringComparison.OrdinalIgnoreCase))
                {
                    int schemeBounds = Url.IndexOf("://");
                    if (schemeBounds == -1) return Url;

                    string base64Part = Url.Substring(schemeBounds + 3).Trim();
                    int mod4 = base64Part.Length % 4;
                    if (mod4 > 0) base64Part += new string('=', 4 - mod4);

                    byte[] bytes = Convert.FromBase64String(base64Part);
                    string jsonString = Encoding.UTF8.GetString(bytes);

                    var jsonModel = JsonConvert.DeserializeObject<VmessJsonModel>(jsonString);
                    if (jsonModel != null)
                    {
                        jsonModel.Remarks = Name; // Подставляем переименованное имя

                        string updatedJson = JsonConvert.SerializeObject(jsonModel, Formatting.None);
                        string updatedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(updatedJson));

                        return "vmess://" + updatedBase64;
                    }
                    return Url;
                }

                // Для VLESS, Shadowsocks и Trojan просто отрезаем старое имя после '#' и приклеиваем новое
                int hashIndex = Url.IndexOf('#');
                if (hashIndex != -1)
                {
                    string urlWithoutName = Url.Substring(0, hashIndex);
                    // Имя кодируем обратно через UrlEncode, так как в ссылках спецсимволы и пробелы недопустимы
                    return $"{urlWithoutName}#{HttpUtility.UrlEncode(Name)}";
                }
                else
                {
                    return $"{Url}#{HttpUtility.UrlEncode(Name)}";
                }
            }
            catch
            {
                return Url; // В случае любой непредвиденной ошибки возвращаем исходную строку
            }
        }
    }
}
