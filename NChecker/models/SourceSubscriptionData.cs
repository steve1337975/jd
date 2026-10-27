using NChecker.models.subscription;
using Newtonsoft.Json;

namespace NChecker.models
{
    internal class SourceSubscriptionData
    {
        internal int SortedIndex { get; set; } = 1;
        internal string Url { get; set; } = string.Empty;
        internal bool Enabled { get; set; }
        [JsonIgnore] internal SubscriptionType FilterType { get; set; }
        internal SourceSubscriptionData() { }
        internal SourceSubscriptionData(int a1, string a2)
        {
            SortedIndex = a1;
            Url = a2;
            Enabled = !string.IsNullOrEmpty(a2) && !a2.StartsWith("#");
        }
    }
}
