using Newtonsoft.Json;
using System.Text;
using System.Web;

namespace NChecker.models.vpn
{
    internal class VpnConfigSkeleton
    {
        internal string Url { get; private set; }
        internal VpnProtocol Protocol { get; private set; } = VpnProtocol.Unknown;
        internal string Address { get; private set; }
        internal int Port { get; private set; }
        internal string Name { get; private set; }
        internal string Uuid { get; private set; }
        internal string Sni { get; private set; }
        internal string Host { get; private set; }
        internal string Path { get; private set; }
        internal string Transport { get; private set; }
        internal string Security { get; private set; }
        internal string PublicKey { get; private set; }
        protected VpnConfigSkeleton(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            url = url.Trim();
            Url = url;

            try
            {
                int schemeBounds = url.IndexOf("://");
                if (schemeBounds == -1) return;

                string scheme = url.Substring(0, schemeBounds).ToLower();
                Protocol = ParseProtocolScheme(scheme);

                if (Protocol == VpnProtocol.Vmess)
                {
                    ParseVmess(url);
                }
                else if (Protocol != VpnProtocol.Unknown)
                {
                    ParseStandardVpnUri(url, scheme);
                }
            }
            catch { }
        }

        internal bool IsValid()
        {
            if (Protocol == VpnProtocol.Unknown) return false;
            if (string.IsNullOrWhiteSpace(Address)) return false;
            if (Port <= 0 || Port > 65535) return false;

            if (Address == "0.0.0.0" || Address == "127.0.0.1" || Address.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        internal bool HasSniMasking()
        {
            return !string.IsNullOrWhiteSpace(Sni);
        }

        private static VpnProtocol ParseProtocolScheme(string scheme)
        {
            return scheme switch
            {
                "vmess" => VpnProtocol.Vmess,
                "vless" => VpnProtocol.Vless,
                "ss" or "shadowsocks" => VpnProtocol.Shadowsocks,
                "ssr" => VpnProtocol.ShadowsocksR,
                "trojan" => VpnProtocol.Trojan,
                "hy2" or "hysteria2" => VpnProtocol.Hysteria2,
                "tuic" => VpnProtocol.Tuic,
                "wireguard" => VpnProtocol.Wireguard,
                _ => VpnProtocol.Unknown
            };
        }

        private void ParseVmess(string row)
        {
            string base64Part = row.Substring(8).Trim();

            int hashIndex = base64Part.IndexOf('#');
            if (hashIndex != -1)
            {
                string rawName = base64Part.Substring(hashIndex + 1);
                Name = HttpUtility.UrlDecode(rawName).Trim();
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
                Uuid = jsonModel.Id;
                Sni = jsonModel.Sni;
                Host = jsonModel.Host;
                Path = jsonModel.Path;
                Transport = jsonModel.Net;
                Security = jsonModel.Tls;

                if (!string.IsNullOrEmpty(jsonModel.Remarks))
                {
                    Name = HttpUtility.UrlDecode(jsonModel.Remarks).Trim();
                }
            }
        }

        private void ParseStandardVpnUri(string row, string scheme)
        {
            string mainPart = row.Substring(scheme.Length + 3);

            int hashIndex = mainPart.IndexOf('#');
            if (hashIndex != -1)
            {
                string rawName = mainPart.Substring(hashIndex + 1);
                Name = HttpUtility.UrlDecode(rawName).Trim();
                mainPart = mainPart.Substring(0, hashIndex).Trim();
            }

            int queryIndex = mainPart.IndexOf('?');
            string queryPart = string.Empty;
            if (queryIndex != -1)
            {
                queryPart = mainPart.Substring(queryIndex + 1);
                mainPart = mainPart.Substring(0, queryIndex).Trim();
            }

            if (mainPart.IndexOf('@') == -1 && (Protocol == VpnProtocol.Shadowsocks || Protocol == VpnProtocol.ShadowsocksR))
            {
                try
                {
                    string cleanedBase64 = mainPart.Trim();
                    int mod4 = cleanedBase64.Length % 4;
                    if (mod4 > 0) cleanedBase64 += new string('=', 4 - mod4);

                    byte[] bytes = Convert.FromBase64String(cleanedBase64);
                    mainPart = Encoding.UTF8.GetString(bytes).Trim();
                }
                catch { }
            }

            int atIndex = mainPart.LastIndexOf('@');
            if (atIndex != -1)
            {
                Uuid = mainPart.Substring(0, atIndex);
                mainPart = mainPart.Substring(atIndex + 1);
            }

            int portIndex = mainPart.LastIndexOf(':');
            if (portIndex != -1)
            {
                string portStr = mainPart.Substring(portIndex + 1);

                if (!portStr.Contains(']'))
                {
                    Address = mainPart.Substring(0, portIndex).Trim('[', ']');
                    if (int.TryParse(portStr, out int parsedPort))
                    {
                        Port = parsedPort;
                    }
                }
                else
                {
                    Address = mainPart.Trim('[', ']');
                    Port = 0;
                }
            }
            else
            {
                Address = mainPart;
                Port = 0;
            }

            if (!string.IsNullOrEmpty(queryPart))
            {
                var queryParams = HttpUtility.ParseQueryString(queryPart);
                Sni = queryParams["sni"];
                Host = queryParams["host"];
                Path = queryParams["path"];
                Transport = queryParams["type"] ?? queryParams["headerType"];
                Security = queryParams["security"];
                PublicKey = queryParams["pbk"];
            }
        }
    }
}
