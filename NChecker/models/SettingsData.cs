using Newtonsoft.Json;

namespace NChecker.models
{
    [Serializable]
    internal class SettingsData
    {
        [JsonProperty("timeout")] public double Timeout { get; set; } = 3;
        [JsonProperty("max_threads")] public int MaxConcurrentChecks { get; set; } = 500;
        [JsonProperty("max_servers_on_file")] public int MaxLinesPerFile { get; set; } = 750;
        [JsonProperty("logging")] public bool Logging { get; set; } = true;
    }
}
