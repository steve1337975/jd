namespace NChecker.models
{
    internal class SubscriptionUserData
    {
        const long gb = 1024 * 1024 * 1024, mb = 1024 * 1024, kb = 1024, b = 1;
        long uploadBytes, downloadBytes, totalBytes, timestamp;
        internal SubscriptionUserData() => Init(mb, mb * 4, gb * 256, DateTime.UtcNow.AddYears(1));
        internal SubscriptionUserData(long a1, long a2, long a3, DateTime a4) => Init(a1, a2, a3, a4);
        internal string GetHeaderString()
        {
            return $"# subscription-userinfo: upload={uploadBytes}; download={downloadBytes}; total={totalBytes}; expire={timestamp}";
        }
        void Init(long a1, long a2, long a3, DateTime a4)
        {
            SetUploadBytes(a1);
            SetDownloadBytes(a2);
            SetTotalBytes(a3);
            SetTimestamp(a4);
        }
        void SetUploadBytes(long a1) => uploadBytes = a1;
        void SetDownloadBytes(long a1) => downloadBytes = a1;
        void SetTotalBytes(long a1) => totalBytes = a1;
        void SetTimestamp(DateTime a1) => timestamp = ((DateTimeOffset)a1.ToUniversalTime()).ToUnixTimeSeconds();
    }
}
