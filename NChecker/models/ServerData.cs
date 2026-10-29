using NChecker.models.subscription;
using NChecker.models.vpn;
using NChecker.models.vpn.protocols;
using System.Security.Cryptography;
using System.Text;

namespace NChecker.models
{
    internal class ServerData
    {
        // Конфиг конкретного протокола (Vless, Vmess, Shadowsocks, Trojan и т.д.)
        internal VpnConfigBase Config { get; private set; }

        private string _countryCode = "UNK", _country = "Unknown";
        private string _city = "Unknown";
        private ServerCountryCode _registeredCountryCode;

        internal bool IsAlive { get; set; }
        internal int SortedIndex { get; set; }
        internal SubscriptionType FilterType { get; set; }

        // Удобные прокси-свойства для быстрого доступа к базовым полям конфига
        internal string Url => Config?.Url;
        internal VpnProtocol Protocol => Config?.Protocol ?? VpnProtocol.Unknown;
        internal string Address => Config?.Address;
        internal int Port => Config?.Port ?? 0;
        internal string Name => Config?.Name;

        internal string Country { get; set; }
        internal string CountryCode
        {
            get
            {
                if (_registeredCountryCode != ServerCountryCode.UNK) return _registeredCountryCode.ToString();
                return _countryCode;
            }
            set
            {
                _countryCode = value;
                _registeredCountryCode = Enum.TryParse(_countryCode, out ServerCountryCode country) ? country : ServerCountryCode.UNK;
            }
        }
        internal string City { get; set; }

        internal ServerData(string url)
        {
            // Используем фабричный метод для создания правильного конфига
            Config = VpnConfigFactory.Create(url);
        }

        internal bool IsValid() => Config?.IsValid() ?? false;

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
