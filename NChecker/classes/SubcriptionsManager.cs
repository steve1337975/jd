using NChecker.models.local;
using Newtonsoft.Json;

namespace NChecker.classes
{
    internal class SubcriptionsManager
    {
        internal FilteredSubscriptionsSourcesData[] Sources { get; private set; }
        internal SubcriptionsManager() => Sources = LoadSources();
        FilteredSubscriptionsSourcesData[] LoadSources()
        {
            try
            {
                return JsonConvert.DeserializeObject<FilteredSubscriptionsSourcesData[]>(Properties.Resources.subscriptions);
            }
            catch (Exception exc) { }
            return Array.Empty<FilteredSubscriptionsSourcesData>();
        }
    }
}
