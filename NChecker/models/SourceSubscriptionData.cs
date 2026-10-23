using NChecker.models.subscription;
using Newtonsoft.Json;

namespace NChecker.models
{
    internal class SourceSubscriptionData
    {
        [JsonProperty("index")] internal int SortedIndex { get; set; } = 1;
        [JsonProperty("url")] internal string Url { get; set; } = string.Empty;
        [JsonProperty("enabled")] internal bool Enabled { get; set; }
        [JsonIgnore] internal SubscriptionType FilterType { get; set; }
    }
}
