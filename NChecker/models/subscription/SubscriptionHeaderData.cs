using System.Text;

namespace NChecker.models.subscription
{
    internal class SubscriptionHeaderData
    {
        string announce, sortOrder = "ping";
        SubscriptionProfileData profileData = new SubscriptionProfileData();
        SubscriptionUserData userData = new SubscriptionUserData();
        string generatedBy = "NChecker";
        DateTime generatedAt = DateTime.Now;
        int serversCount;
        internal SubscriptionHeaderData(string a1, int a2)
        {
            announce = "⚡ Проверяйте сервер кнопкой скорости или молнией. Чем меньше задержка (ms), тем лучше отклик; n/a означает, что проверка не удалась.";
            profileData.SetTitle(a1);
            serversCount = a2;
        }
        internal string GetHeaderString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("# announce: " + announce);
            stringBuilder.AppendLine(profileData.GetHeaderString());
            stringBuilder.AppendLine(userData.GetHeaderString());
            stringBuilder.AppendLine("# sort-order: " + sortOrder);
            stringBuilder.AppendLine("# generated-by: " + generatedBy);
            stringBuilder.AppendLine("# generated-at: " + generatedAt.ToString());
            stringBuilder.AppendLine("# servers-count: " + serversCount);
            return stringBuilder.ToString();
        }
    }
}
