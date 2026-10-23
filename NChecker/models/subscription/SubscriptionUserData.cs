namespace NChecker.models.subscription
{
    internal class SubscriptionUserData
    {
        private const long pb = 1000L * 1000 * 1000 * 1000 * 1000;
        private const long tb = 1000L * 1000 * 1000 * 1000;
        private const long gb = 1000 * 1000 * 1000;
        private const long mb = 1000 * 1000;
        private const long kb = 1000, b = 1;
        private long uploadBytes, downloadBytes, totalBytes, timestamp;
        internal SubscriptionUserData() => Init(0, 0, tb, DateTime.UtcNow.AddYears(1).AddDays(1));
        //internal SubscriptionUserData(long a1, long a2, long a3, DateTime a4) => Init(a1, a2, a3, a4);
        internal string GetHeaderString()
        {
            return $"# subscription-userinfo: upload={uploadBytes}; download={downloadBytes}; total={totalBytes}; expire={timestamp}";
        }
        private void Init(long a1, long a2, long a3, DateTime a4)
        {
            uploadBytes = a1;
            downloadBytes = a2;
            totalBytes = a3;
            SetTimestamp(a4);
        }
        private long GetBytes(long a1, long a2, long a3, long a4, long a5) => tb * a1 + gb * a2 + mb * a3 + kb * a4 + a5;
        private void SetTimestamp(DateTime a1) => timestamp = ((DateTimeOffset)a1.ToUniversalTime()).ToUnixTimeSeconds();
    }
}
