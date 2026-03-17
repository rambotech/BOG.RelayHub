using Newtonsoft.Json;

namespace BOG.RelayHub.Client.Entity
{
    [JsonObject(MemberSerialization = MemberSerialization.OptOut)]
    public class RelayHubApiResult
    {
        [JsonProperty(Required = Required.Always, PropertyName = "HandleAs")]
        public RelayHubApiResultHandling HandleAs { get; set; } = RelayHubApiResultHandling.Indeterminate;

        [JsonProperty(Required = Required.Always, PropertyName = "StatusCode")]
        public int StatusCode { get; set; } = 0;

        [JsonProperty(Required = Required.Always, PropertyName = "ErrorDetail")]
        public string StatusDetail { get; set; } = string.Empty;

        [JsonProperty(Required = Required.Always, PropertyName = "Payload")]
        public string Payload { get; set; } = string.Empty;
    }
}
