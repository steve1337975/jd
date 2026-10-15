using NChecker.utils;

namespace NChecker.models
{
    internal class SubscriptionData
    {
        List<ServerData> m_servers = new List<ServerData>();
        internal string Url { get; private set; }
        internal SubscriptionFilterType FilterType { get; set; } = SubscriptionFilterType.Black;
        string Name { get; set; } = string.Empty;
        string Owner { get; set; } = string.Empty;
        internal SubscriptionData(string a1, SubscriptionFilterType a2)
        {
            Url = a1;
            FilterType = a2;
            /*
            string[] paths = new Uri(a1).LocalPath.Split('/');
            Owner = paths[1];
            Name = paths[2];
            */
        }
        internal void AddServer(ServerData a1) => m_servers.Add(a1);
        internal void ShuffleServers() => RandomUtil.Shuffle(m_servers);
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
