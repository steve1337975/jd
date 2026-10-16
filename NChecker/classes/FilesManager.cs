using NChecker.models;
using NChecker.models.subscription;
using Newtonsoft.Json;

namespace NChecker.classes
{
    internal class FilesManager
    {
        List<SourceSubscriptionData> m_sources = new List<SourceSubscriptionData>();
        internal SettingsData Settings { get; private set; }
        internal FilesManager()
        {
            InitSettings();
            InitBlackLists();
            InitWhiteLists();
        }
        internal int GetSourcesCount() => m_sources.Count;
        internal SourceSubscriptionData[] GetSources()
        {
            return m_sources.ToArray();
        }
        void InitSettings()
        {
            try
            {
                string v1 = Properties.Resources.settings;
                Settings = JsonConvert.DeserializeObject<SettingsData>(v1);
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.ToString());
            }
        }
        void InitBlackLists()
        {
            try
            {
                string v1 = Properties.Resources.black;
                SourceSubscriptionData[] black = JsonConvert.DeserializeObject<SourceSubscriptionData[]>(v1);
                foreach (SourceSubscriptionData ssd in black)
                {
                    if (!string.IsNullOrEmpty(ssd.Url) && ssd.Enabled)
                    {
                        ssd.FilterType = SubscriptionFilterType.Black;
                        m_sources.Add(ssd);
                    }
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.ToString());
            }
        }
        void InitWhiteLists()
        {
            try
            {
                string v1 = Properties.Resources.white;
                SourceSubscriptionData[] white = JsonConvert.DeserializeObject<SourceSubscriptionData[]>(v1);
                foreach (SourceSubscriptionData ssd in white)
                {
                    if (!string.IsNullOrEmpty(ssd.Url) && ssd.Enabled)
                    {
                        ssd.FilterType = SubscriptionFilterType.White;
                        m_sources.Add(ssd);
                    }
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.ToString());
            }
        }
    }
}
