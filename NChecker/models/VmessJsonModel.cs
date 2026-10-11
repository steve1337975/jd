using Newtonsoft.Json;

namespace NChecker.models
{
    [Serializable]
    public class VmessJsonModel
    {
        [JsonProperty("ps")] public string Remarks { get; set; }
        [JsonProperty("add")] public string Address { get; set; }
        [JsonProperty("port")] public object Port { get; set; }
    }
}
