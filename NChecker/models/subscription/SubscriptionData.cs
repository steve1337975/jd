namespace NChecker.models.subscription
{
    internal class SubscriptionData : SourceSubscriptionData
    {
        internal List<ServerData> Servers { get; set; } = new List<ServerData>();
        internal static SubscriptionData Create(SourceSubscriptionData a1)
        {
            SubscriptionData v1 = new SubscriptionData
            {
                SortedIndex = a1.SortedIndex,
                Url = a1.Url,
                FilterType = a1.FilterType,
            };
            return v1;
        }
    }
    internal enum SubscriptionFilterType : int
    {
        Black,
        White
    }
}
