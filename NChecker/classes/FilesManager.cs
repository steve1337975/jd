using NChecker.models;
using NChecker.models.subscription;
using NChecker.utils;
using Newtonsoft.Json;

namespace NChecker.classes
{
    internal class FilesManager
    {
        private readonly List<SourceSubscriptionData> _sources = new List<SourceSubscriptionData>();
        internal SettingsData Settings { get; private set; }
        internal FilesManager()
        {
            InitSettings();
            LoadList(Properties.Resources.black, SubscriptionType.Black);
            LoadList(Properties.Resources.white, SubscriptionType.White);
            LoadList(Properties.Resources.universal, SubscriptionType.Universal);
        }
        internal int GetSourcesCount() => _sources.Count;
        internal SourceSubscriptionData[] GetSources() => _sources.ToArray();
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
                string[] items = JsonConvert.DeserializeObject<string[]>(resourceContent);
                if (items == null) return;
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] == "-") break;
                    SourceSubscriptionData ssd = new SourceSubscriptionData(i + 1, items[i]);
                    ssd.FilterType = filterType;
                    if (ssd.Enabled) _sources.Add(ssd);
                }
            }
            catch (Exception exc)
            {
                Logger.Error($"Ошибка загрузки списка {filterType} => {exc.Message}");
            }
        }
    }
}
