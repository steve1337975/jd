using System.Text;

namespace NChecker.models
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
    internal class SubscriptionUserData
    {
        private const long BytesInGb = 1024 * 1024 * 1024;
        long uploadBytes, downloadBytes, totalBytes, timestamp;
        internal SubscriptionUserData()
        {
            int min = Utils.RandomInt(0, 10);
            int max = Utils.RandomInt(0, 10);
            SetUploadBytes(Math.Min(min, max));
            SetDownloadBytes(Math.Max(min, max));
            SetTotalBytes(1337);
            SetTimestamp(DateTime.UtcNow.AddYears(1));
        }
        internal SubscriptionUserData(long a1, long a2, long a3, DateTime a4)
        {
            SetUploadBytes(a1);
            SetDownloadBytes(a2);
            SetTotalBytes(a3);
            SetTimestamp(a4);
        }
        void SetUploadBytes(long a1) => uploadBytes = a1 * BytesInGb;
        void SetDownloadBytes(long a1) => downloadBytes = a1 * BytesInGb;
        void SetTotalBytes(long a1) => totalBytes = a1 * BytesInGb;
        void SetTimestamp(DateTime a1) => timestamp = ((DateTimeOffset)a1.ToUniversalTime()).ToUnixTimeSeconds();
        internal string GetHeaderString()
        {
            return $"# subscription-userinfo: upload={uploadBytes}; download={downloadBytes}; total={totalBytes}; expire={timestamp}";
        }
    }
}
