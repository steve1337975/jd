using NChecker.models;
using Newtonsoft.Json;

namespace NChecker.classes
{
    internal class SubcriptionsManager
    {
        List<SubscriptionSourceData> m_black = new List<SubscriptionSourceData>();
        List<SubscriptionSourceData> m_white = new List<SubscriptionSourceData>();
        internal SubcriptionsManager()
        {
            FillBlackLists();
            FillWhiteLists();
        }
        internal SubscriptionSourceData[] GetSources()
        {
            SubscriptionSourceData[] v1 = new SubscriptionSourceData[m_black.Count + m_white.Count];
            for (int i = 0; i < m_black.Count; i++) v1[i] = m_black[i];
            for (int i = 0; i < m_white.Count; i++) v1[i + m_black.Count] = m_white[i];
            return v1.ToArray();
        }
        void FillBlackLists()
        {
            string[] lines = JsonConvert.DeserializeObject<string[]>(Properties.Resources.black);
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                if (line.Equals("-")) break;
                bool isDisabled = line.StartsWith('#');
                m_black.Add(new(isDisabled ? line.Substring(1) : line, !isDisabled, SubscriptionFilterType.Black));
            }
        }
        void FillWhiteLists()
        {
            string[] lines = JsonConvert.DeserializeObject<string[]>(Properties.Resources.white);
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                if (line.Equals("-")) break;
                bool isDisabled = line.StartsWith('#');
                m_white.Add(new(isDisabled ? line.Substring(1) : line, !isDisabled, SubscriptionFilterType.White));
            }
        }
    }
}
