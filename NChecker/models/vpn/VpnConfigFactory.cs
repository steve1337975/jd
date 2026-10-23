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

            // Создаем нужного наследника в зависимости от схемы
            return scheme switch
            {
                "vmess" => new VmessConfigData(trimmedUrl),
                "vless" => new VlessConfigData(trimmedUrl),
                "ss" or "shadowsocks" => new ShadowsocksConfigData(trimmedUrl),
                "trojan" => new TrojanConfigData(trimmedUrl),
                _ => null // Если протокол не поддерживается или неизвестен
            };
        }
    }
}
