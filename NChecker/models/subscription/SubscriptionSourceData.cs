namespace NChecker.models.subscription
{
    internal class SubscriptionSourceData
    {
        public string Url { get; private set; }
        public bool Enabled { get; private set; } = true;
        internal SubscriptionFilterType FilterType { get; private set; }
        internal SubscriptionSourceData(string a1, bool a2, SubscriptionFilterType a3)
        {
            Url = a1;
            Enabled = a2;
            FilterType = a3;
        }
    }
}
