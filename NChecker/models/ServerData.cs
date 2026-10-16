using MaxMind.GeoIP2.Responses;
using NChecker.models.subscription;
using NChecker.models.vpn;

namespace NChecker.models
{
    internal class ServerData : VpnConfigSkeleton
    {
        private string m_countryCode = "UNK";
        private string m_country = "Unknown";
        internal int SortedIndex { get; set; }
        internal SubscriptionFilterType FilterType { get; set; }
        internal long Delay { get; set; } = -1;
        internal bool IsAlive { get; set; }
        internal ServerData(string url) : base(url) { }
        internal string GetCountryCode() => m_countryCode;
        internal string GetCountry() => m_country;
        internal void SetCountry(CountryResponse response)
        {
            m_country = response?.Country.Name ?? "Unknown";
            m_countryCode = response?.Country.IsoCode ?? "UNK";
        }
    }
}
