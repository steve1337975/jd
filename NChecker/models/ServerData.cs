using MaxMind.GeoIP2.Responses;
using NChecker.models.subscription;
using NChecker.models.vpn;
using System.Security.Cryptography;
using System.Text;

namespace NChecker.models
{
    internal class ServerData
    {
        // Конфиг конкретного протокола (Vless, Vmess, Shadowsocks, Trojan и т.д.)
        internal VpnConfigBase Config { get; private set; }

        private string m_countryCode = "UNK", m_country = "Unknown";
        private ServerCountryCode m_registeredCountryCode;

        internal long Delay { get; set; } = -1;
        internal bool IsAlive { get; set; }
        internal int SortedIndex { get; set; }
        internal SubscriptionType FilterType { get; set; }

        // Удобные прокси-свойства для быстрого доступа к базовым полям конфига
        internal string Url => Config?.Url;
        internal VpnProtocol Protocol => Config?.Protocol ?? VpnProtocol.Unknown;
        internal string Address => Config?.Address;
        internal int Port => Config?.Port ?? 0;
        internal string Name => Config?.Name;

        internal ServerData(string url)
        {
            // Используем фабричный метод для создания правильного конфига
            Config = VpnConfigFactory.Create(url);
        }

        internal bool IsValid() => Config?.IsValid() ?? false;

        internal string GetCountryCode()
        {
            if (m_registeredCountryCode != ServerCountryCode.UNK) return m_registeredCountryCode.ToString();
            return m_countryCode;
        }

        internal void SetCountry(CountryResponse response)
        {
            m_countryCode = response?.Country.IsoCode ?? "UNK";
            m_registeredCountryCode = Enum.TryParse(m_countryCode, out ServerCountryCode country) ? country : ServerCountryCode.UNK;
            m_country = response?.Country.Name ?? "Unknown";
        }

        internal string GetHashData()
        {
            if (Config == null) return string.Empty;

            // Получаем чистую техническую подпись от самого конфига
            string coreSignature = Config.GetCoreSignature()?.Trim().ToLowerInvariant() ?? string.Empty;

            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(coreSignature));
            return Convert.ToHexString(bytes);
        }

        internal bool IsExists(ServerData other)
        {
            if (other == null) return false;
            return GetHashData() == other.GetHashData();
        }
    }
}
