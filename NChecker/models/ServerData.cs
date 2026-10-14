using MaxMind.GeoIP2.Responses;
using Newtonsoft.Json;
using System.Text;

namespace NChecker.models
{

    internal class ServerData
    {
        internal string Url { get; private set; }
        internal ServerProtocol Protocol { get; private set; } = ServerProtocol.Unknown;
        internal string Address { get; private set; }
        internal int Port { get; private set; }

        private string m_countryCode = "UNK";
        private string m_country = "Unknown";

        internal long Delay { get; set; } = -1;
        internal bool IsAlive { get; set; }
        internal SubscriptionData Subscription { get; set; }

        internal ServerData(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            url = url.Trim();
            Url = url;

            int schemeBounds = url.IndexOf("://");
            if (schemeBounds == -1) return;

            string scheme = url.Substring(0, schemeBounds).ToLower();
            Protocol = ParseProtocolScheme(scheme);

            if (Protocol == ServerProtocol.Vmess)
            {
                ParseVmess(url);
            }
            else if (Protocol != ServerProtocol.Unknown)
            {
                ParseStandardVpnUri(url, scheme);
            }
        }
        internal string GetAddress()
        {
            return Address;
        }

        internal string GetCountryCode() => m_countryCode;
        internal string GetCountry() => m_country;

        internal void SetCountry(CountryResponse response)
        {
            m_country = response?.Country.Name ?? "Unknown";
            m_countryCode = response?.Country.IsoCode ?? "UNK";
        }

        private static ServerProtocol ParseProtocolScheme(string scheme)
        {
            return scheme switch
            {
                "vmess" => ServerProtocol.Vmess,
                "vless" => ServerProtocol.Vless,
                "ss" or "shadowsocks" => ServerProtocol.Shadowsocks,
                "ssr" => ServerProtocol.ShadowsocksR,
                "trojan" => ServerProtocol.Trojan,
                "hy2" or "hysteria2" => ServerProtocol.Hysteria2,
                "tuic" => ServerProtocol.Tuic,
                "wireguard" => ServerProtocol.Wireguard,
                _ => ServerProtocol.Unknown
            };
        }

        #region Приватные методы безопасного парсинга строк

        private void ParseVmess(string row)
        {
            string base64Part = row.Substring(8).Trim();

            int hashIndex = base64Part.IndexOf('#');
            if (hashIndex != -1)
            {
                base64Part = base64Part.Substring(0, hashIndex).Trim();
            }

            int lastEqualsIndex = base64Part.LastIndexOf('=');
            if (lastEqualsIndex != -1)
            {
                base64Part = base64Part.Substring(0, lastEqualsIndex + 1);
            }
            else if (base64Part.EndsWith("vmess", StringComparison.OrdinalIgnoreCase))
            {
                base64Part = base64Part.Substring(0, base64Part.Length - 5).Trim();
            }

            int mod4 = base64Part.Length % 4;
            if (mod4 > 0) base64Part += new string('=', 4 - mod4);

            byte[] bytes = Convert.FromBase64String(base64Part);
            string jsonString = Encoding.UTF8.GetString(bytes).Trim();

            var jsonModel = JsonConvert.DeserializeObject<VmessJsonModel>(jsonString);
            if (jsonModel != null)
            {
                Address = jsonModel.Address;
                Port = Convert.ToInt32(jsonModel.Port ?? 0);
            }
        }

        private void ParseStandardVpnUri(string row, string scheme)
        {
            string mainPart = row.Substring(scheme.Length + 3);

            int hashIndex = mainPart.IndexOf('#');
            if (hashIndex != -1)
            {
                mainPart = mainPart.Substring(0, hashIndex);
            }

            int queryIndex = mainPart.IndexOf('?');
            if (queryIndex != -1)
            {
                mainPart = mainPart.Substring(0, queryIndex);
            }

            int atIndex = mainPart.LastIndexOf('@');
            string connectionPart = atIndex != -1 ? mainPart.Substring(atIndex + 1) : mainPart;

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
                Port = 0;
            }
        }

        #endregion
    }
}
