using System.Text;

namespace NChecker.models
{
    internal class SubscriptionProfileData
    {
        string title = "N";
        int updateInterval = 1;
        internal void SetTitle(string a1) => title = a1;
        internal string GetHeaderString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("# profile-title: " + title);
            stringBuilder.Append("# profile-update-interval: " + updateInterval);
            return stringBuilder.ToString();
        }
    }
}
