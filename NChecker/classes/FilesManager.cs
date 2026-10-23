using NChecker.models;
using NChecker.models.subscription;
using NChecker.utils;
using Newtonsoft.Json;

namespace NChecker.classes
{
    internal class FilesManager
    {
        private readonly List<SourceSubscriptionData> m_sources = new List<SourceSubscriptionData>();
        internal SettingsData Settings { get; private set; }
        internal FilesManager()
        {
            InitSettings();
            LoadList(Properties.Resources.black, SubscriptionType.Black);
            LoadList(Properties.Resources.white, SubscriptionType.White);
            LoadList(Properties.Resources.universal, SubscriptionType.Universal);
        }
        internal int GetSourcesCount() => m_sources.Count;
        internal SourceSubscriptionData[] GetSources() => m_sources.ToArray();
        private void InitSettings()
        {
            try
            {
                string json = Properties.Resources.settings;
                Settings = JsonConvert.DeserializeObject<SettingsData>(json);
            }
            catch (Exception exc)
            {
                Logger.Error($"Ошибка загрузки настроек => {exc.Message}");
            }
        }
        private void LoadList(string resourceContent, SubscriptionType filterType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(resourceContent)) return;
                SourceSubscriptionData[] items = JsonConvert.DeserializeObject<SourceSubscriptionData[]>(resourceContent);
                if (items == null) return;
                int localSubIndex = 1;
                foreach (SourceSubscriptionData ssd in items)
                {
                    if (ssd != null && !string.IsNullOrEmpty(ssd.Url) && ssd.Enabled)
                    {
                        ssd.FilterType = filterType;
                        ssd.SortedIndex = localSubIndex;

                        m_sources.Add(ssd);
                        localSubIndex++;
                    }
                }
            }
            catch (Exception exc)
            {
                Logger.Error($"Ошибка загрузки списка {filterType} => {exc.Message}");
            }
        }
    }
}
