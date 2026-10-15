using Newtonsoft.Json;

namespace NChecker.models.vpn
{
    [Serializable]
    public class VmessJsonModel
    {
        [JsonProperty("v")] public string Version { get; set; }
        [JsonProperty("ps")] public string Remarks { get; set; }
        [JsonProperty("add")] public string Address { get; set; }
        [JsonProperty("port")] public object Port { get; set; }
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("aid")] public object AlterId { get; set; }
        [JsonProperty("scy")] public string Security { get; set; }
        [JsonProperty("net")] public string Net { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("host")] public string Host { get; set; }
        [JsonProperty("path")] public string Path { get; set; }
        [JsonProperty("tls")] public string Tls { get; set; }
        [JsonProperty("sni")] public string Sni { get; set; }
        [JsonProperty("alpn")] public string Alpn { get; set; }
        [JsonProperty("fp")] public string Fingerprint { get; set; }
    }
}
