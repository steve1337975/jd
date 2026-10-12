namespace NChecker.models
{
    internal class SubscriptionUserData
    {
        long bytesInGb = 1024 * 1024 * 1024;
        long uploadBytes, downloadBytes, totalBytes, timestamp;
        internal SubscriptionUserData()
        {
            SetUploadBytes(0);
            SetDownloadBytes(0);
            SetTotalBytes(256);
            SetTimestamp(DateTime.UtcNow.AddYears(1));
        }
        internal SubscriptionUserData(long a1, long a2, long a3, DateTime a4)
        {
            SetUploadBytes(a1);
            SetDownloadBytes(a2);
            SetTotalBytes(a3);
            SetTimestamp(a4);
        }
        void SetUploadBytes(long a1) => uploadBytes = a1 * bytesInGb;
        void SetDownloadBytes(long a1) => downloadBytes = a1 * bytesInGb;
        void SetTotalBytes(long a1) => totalBytes = a1 * bytesInGb;
        void SetTimestamp(DateTime a1) => timestamp = ((DateTimeOffset)a1.ToUniversalTime()).ToUnixTimeSeconds();
        internal string GetHeaderString()
        {
            return $"# subscription-userinfo: upload={uploadBytes};download={downloadBytes};total={totalBytes};expire={timestamp}";
        }
    }
}
