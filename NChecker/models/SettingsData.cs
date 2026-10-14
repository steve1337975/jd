namespace NChecker.models
{
    [Serializable]
    internal class SettingsData
    {
        public int Timeout { get; set; } = 5;
        public int MaxConcurrentChecks { get; set; } = 500;
        public int MaxLinesPerFile { get; set; } = 250;
        public bool ShuffleServers { get; set; } = true;
        public bool Logging { get; set; } = true;
    }
}
