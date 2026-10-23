namespace NChecker.models.subscription
{
    internal class SubscriptionData : SourceSubscriptionData
    {
        internal List<ServerData> Servers { get; set; } = new List<ServerData>();
        internal bool IsExists(ServerData a1)
        {
            foreach (ServerData server in Servers)
            {
                if (server.IsExists(a1)) return true;
            }
            return false;
        }
        internal bool IsExistsByHash(ServerData a1)
        {
            foreach (ServerData server in Servers)
            {
                if (server.IsExists(a1) && server.GetHashData() == a1.GetHashData()) return true;
            }
            return false;
        }
        internal static SubscriptionData Create(SourceSubscriptionData a1)
        {
            return new() { SortedIndex = a1.SortedIndex, Url = a1.Url, FilterType = a1.FilterType };
        }
    }
}
