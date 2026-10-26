using System.Text;

namespace NChecker.models.vpn.protocols
{
    internal abstract class VpnConfigBase
    {
        internal string Url { get; private set; }
        internal VpnProtocol Protocol { get; private set; } = VpnProtocol.Unknown;
        internal string Address { get; set; }
        internal int Port { get; set; }
        internal string Name { get; set; }

        protected VpnConfigBase(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            Url = url.Trim();

            int schemeBounds = Url.IndexOf("://", StringComparison.Ordinal);
            if (schemeBounds == -1) return;

            string scheme = Url.Substring(0, schemeBounds).ToLowerInvariant();
            Protocol = ParseProtocolScheme(scheme);

            Parse(Url);
        }
        protected abstract void Parse(string url);
        internal bool IsValid()
        {
            if (Protocol == VpnProtocol.Unknown) return false;
            if (string.IsNullOrWhiteSpace(Address)) return false;
            if (Port <= 0 || Port > 65535) return false;

            if (Address == "0.0.0.0" || Address == "127.0.0.1" || Address.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
        internal virtual string GetCoreSignature() => $"{Protocol}:{Address}:{Port}";
        protected string TryDecodeBase64(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return string.Empty;

            try
            {
                base64 = base64.Trim().Replace('-', '+').Replace('_', '/');

                // Дополняем паддинг '=', если длина не кратна 4
                int mod4 = base64.Length % 4;
                if (mod4 > 0) base64 += new string('=', 4 - mod4);

                byte[] bytes = Convert.FromBase64String(base64);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return string.Empty;
            }
        }
        private VpnProtocol ParseProtocolScheme(string scheme)
        {
            return scheme switch
            {
                "socks4" => VpnProtocol.Socks4,
                "socks5" => VpnProtocol.Socks5,
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
    }
}
