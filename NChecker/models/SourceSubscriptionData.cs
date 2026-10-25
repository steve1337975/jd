using NChecker.models.subscription;
using Newtonsoft.Json;

namespace NChecker.models
{
    internal class SourceSubscriptionData
    {
        [JsonProperty("index")] internal int SortedIndex { get; set; } = 1;
        [JsonProperty("url")] internal string Url { get; private set; } = string.Empty;
        [JsonProperty("enabled")] internal bool Enabled { get; private set; }
        [JsonIgnore] internal SubscriptionType FilterType { get; set; }
        internal bool IsEnabled() => !string.IsNullOrEmpty(Url) && Enabled;
    }
}
