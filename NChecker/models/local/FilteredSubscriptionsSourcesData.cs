namespace NChecker.models.local
{
    [Serializable]
    internal class FilteredSubscriptionsSourcesData
    {
        public string SubscriptionType { get; set; } = SubscriptionFilterType.Black.ToString();
        public string Url { get; set; } = string.Empty;
        internal SubscriptionFilterType GetFilter()
        {
            return (SubscriptionFilterType)Enum.Parse(typeof(SubscriptionFilterType), SubscriptionType);
        }
        internal void SetFilter(SubscriptionFilterType filterType) => SubscriptionType = filterType.ToString();
    }
}
