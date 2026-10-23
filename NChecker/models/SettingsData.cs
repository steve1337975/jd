namespace NChecker.models
{
    [Serializable]
    internal class SettingsData
    {
        public double Timeout { get; set; } = 3;
        public int MaxConcurrentChecks { get; set; } = 500;
        public int MaxLinesPerFile { get; set; } = 750;
        public bool Logging { get; set; } = true;
    }
}
