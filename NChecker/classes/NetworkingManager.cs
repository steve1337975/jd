using NChecker.models;
using NChecker.models.local;

namespace NChecker.classes
{
    internal class NetworkingManager
    {
        HttpClient httpClient = new HttpClient();
        async internal Task<SubscriptionData[]> ParseSubscriptions(SettingsData a1, FilteredSubscriptionsSourcesData[] a2)
        {
            List<SubscriptionData> subscriptions = new List<SubscriptionData>();
            foreach (FilteredSubscriptionsSourcesData subscriptionSourceData in a2)
            {
                if (string.IsNullOrEmpty(subscriptionSourceData.Url) || !subscriptionSourceData.Enabled) continue;
                try
                {
                    string data = await httpClient.GetStringAsync(subscriptionSourceData.Url);

                    foreach (string line in data.Split('\n'))
                    {
                        try
                        {
                            SubscriptionData subscriptionData = new SubscriptionData(subscriptionSourceData.Url, subscriptionSourceData.GetFilter());
                            if (line.StartsWith("#") || line.Length < 10 || line.Contains("::/")) continue;
                            ServerData serverData = new(line);
                            serverData.Subscription = subscriptionData;
                            subscriptionData.AddServer(serverData);
                            subscriptions.Add(subscriptionData);
                            if (a1.Logging) Console.WriteLine("Успешно загружено => " + subscriptionData.Url);
                        }
                        catch (Exception exc2) { }
                    }
                }
                catch (Exception exc) { }
            }
            return subscriptions.ToArray();
        }
    }
}
