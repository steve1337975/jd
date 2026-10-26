using NChecker.models.vpn.protocols;

namespace NChecker.models.vpn
{
    internal class VpnConfigFactory
    {
        internal static VpnConfigBase Create(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            string trimmedUrl = url.Trim();
            int schemeBounds = trimmedUrl.IndexOf("://", StringComparison.Ordinal);
            if (schemeBounds == -1) return null;
            string scheme = trimmedUrl.Substring(0, schemeBounds).ToLowerInvariant();
            return scheme switch
            {
                "socks4" => new Socks4ConfigData(trimmedUrl),
                "socks5" => new Socks5ConfigData(trimmedUrl),
                "vmess" => new VmessConfigData(trimmedUrl),
                "vless" => new VlessConfigData(trimmedUrl),
                "ss" or "shadowsocks" => new ShadowsocksConfigData(trimmedUrl),
                "trojan" => new TrojanConfigData(trimmedUrl),
                "hysteria2" or "hysteria" or "hy2" => new HysteriaConfigData(trimmedUrl),
                _ => null
            };
        }
    }
}
