namespace NChecker.models
{
    internal class SubscriptionData
    {
        List<ServerData> m_servers = new List<ServerData>();
        internal string Url { get; private set; }
        internal SubscriptionFilterType FilterType { get; set; } = SubscriptionFilterType.Black;
        internal string Name { get; private set; }
        internal string Owner { get; private set; }
        internal SubscriptionData(string a1, SubscriptionFilterType a2)
        {
            Url = a1;
            FilterType = a2;
            string[] paths = new Uri(a1).LocalPath.Split('/');
            Owner = paths[1];
            Name = paths[2];
        }
        internal void AddServer(ServerData a1) => m_servers.Add(a1);
        internal void ShuffleServers() => Utils.Shuffle(m_servers);
        internal ServerData[] GetServers() => m_servers.ToArray();
    }
    internal enum SubscriptionFilterType : int
    {
        Black,
        White
    }
    internal enum SubscriptionGroupType
    {
        Ungrouped,
        Partial,
        Full
    }
}
