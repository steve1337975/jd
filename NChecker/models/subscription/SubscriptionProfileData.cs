using System.Text;

namespace NChecker.models.subscription
{
    internal class SubscriptionProfileData
    {
        string m_title = "N";
        int updateInterval = 1;
        internal void SetTitle(string a1) => m_title = a1;
        internal string GetHeaderString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("# profile-title: " + m_title);
            stringBuilder.Append("# profile-update-interval: " + updateInterval);
            return stringBuilder.ToString();
        }
    }
}
