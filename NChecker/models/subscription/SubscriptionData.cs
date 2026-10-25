namespace NChecker.models.subscription
{
    internal class SubscriptionData : SourceSubscriptionData
    {
        internal List<ServerData> Servers { get; set; } = new List<ServerData>();
        internal static SubscriptionData Create(SourceSubscriptionData a1)
        {
            return new() { SortedIndex = a1.SortedIndex, Url = a1.Url, FilterType = a1.FilterType };
        }
    }
}
