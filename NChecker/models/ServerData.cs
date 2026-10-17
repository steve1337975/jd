using MaxMind.GeoIP2.Responses;
using NChecker.models.subscription;
using NChecker.models.vpn;

namespace NChecker.models
{
    internal class ServerData : VpnConfigSkeleton
    {
        private string m_countryCode = "UNK", m_country = "Unknown";
        ServerCountryCode m_registeredCountryCode;
        internal long Delay { get; set; } = -1;
        internal bool IsAlive { get; set; }
        internal int SortedIndex { get; set; }
        internal SubscriptionFilterType FilterType { get; set; }
        internal ServerData(string url) : base(url) { }
        internal string GetCountryCode()
        {
            if (m_registeredCountryCode != ServerCountryCode.UNK)
            {
                //Logger.Info($"Зарегистрированная страна => {m_countryCode}, {m_country}");
                return m_registeredCountryCode.ToString();
            }
            //Logger.Info($"Незарегистрированная страна => {m_countryCode}");
            return m_countryCode;
        }
        internal void SetCountry(CountryResponse response)
        {
            m_countryCode = response?.Country.IsoCode ?? "UNK";
            m_registeredCountryCode = Enum.TryParse(m_countryCode, out ServerCountryCode country) ? country : ServerCountryCode.UNK;
            m_country = response?.Country.Name ?? "Unknown";
        }
    }
}
