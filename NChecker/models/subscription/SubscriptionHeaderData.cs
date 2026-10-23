using System.Text;

namespace NChecker.models.subscription
{
    internal class SubscriptionHeaderData
    {
        private string announce;
        private SubscriptionSortOrderType sortOrder = SubscriptionSortOrderType.Ping;
        private SubscriptionProfileData profileData = new SubscriptionProfileData();
        private SubscriptionUserData userData = new SubscriptionUserData();
        private string generatedBy = "NChecker";
        private DateTime generatedAt = DateTime.Now;
        private int serversCount;
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
            stringBuilder.AppendLine("# sort-order: " + sortOrder.ToString().ToLower());

            stringBuilder.AppendLine("# generated-by: " + generatedBy);
            stringBuilder.AppendLine("# generated-at: " + generatedAt.ToString());
            stringBuilder.AppendLine("# servers-count: " + serversCount);
            return stringBuilder.ToString();
        }
    }
}
