using Newtonsoft.Json;

namespace BOG.RelayHub.Common.Entity
{
    /// <summary>
    /// Configuration for the relay hub endpoint.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class RelayHubConfig
    {
        /// <summary>
        /// Hosts which can connect to the API.
        /// </summary>
        [JsonProperty(Required = Required.Always, PropertyName = "AllowedHosts")]
        public string AllowedHosts { get; set; } = "*";
        /// <summary>
        /// THe value the "AccessToken" header for executive functions (bulk channel deletion, resets, etc).
        /// </summary>
        [JsonProperty(Required = Required.Always, PropertyName = "ExecutiveTokenValue")]
        public string ExecutiveTokenValue { get; set; } = "YourExecutiveTokenValueHere";
        /// <summary>
        /// THe value the "AccessToken" header for administrative functions.
        /// </summary>
        [JsonProperty(Required = Required.Always, PropertyName = "AdminTokenValue")]
        public string AdminTokenValue { get; set; } = "YourAdministrativeTokenValueHere";
        /// <summary>
        /// THe value the "AccessToken" header for operational functions.
        /// </summary>
        [JsonProperty(Required = Required.Always, PropertyName = "UserTokenValue")]
        public string UserTokenValue { get; set; } = "YourUserTokenValueHere";
        /// <summary>
        /// The number of seconds, per failed attempt, to wait before submissions are again examined from the violating client.
        /// </summary>
        [JsonProperty(Required = Required.Always, PropertyName = "SecurityDelaySecondsFactor")]
        public int SecurityDelaySecondsFactor { get; set; } = 15;
        /// <summary>
        /// The number of failed attempts before imposing a delay on examining submissions from the client.
        /// </summary>
        [JsonProperty(Required = Required.Always, PropertyName = "SecurityMaxInvalidTokenAttempts")]
        public int SecurityMaxInvalidTokenAttempts { get; set; } = 5;
        /// <summary>
        /// The maximum number of queue filenames to load in memory for faster lookup.
        /// </summary>
        [JsonProperty(Required = Required.Always, PropertyName = "MaxCountQueuedFilenames")]
        public int MaxCountQueuedFilenames { get; set; } = 20;
        /// <summary>
        /// THe root folder for the channel storage.
        /// </summary>
        [JsonProperty(Required = Required.Always, PropertyName = "RootStoragePath")]
        public string RootStoragePath { get; set; } = "$HOME";
        /// <summary>
        /// The listening names and ports for the web API.
        /// </summary>
        [JsonProperty(Required = Required.Always, PropertyName = "Listeners")]
        public string Listeners { get; set; } = "http://*:5050";
        /// <summary>
        /// Defaults to recover exist channels and content at startup.  True destroys the existing content.
        /// </summary>
        [JsonProperty(Required = Required.Default, PropertyName = "FreshStart")]
        public bool FreshStart { get; set; } = false;
        /// <summary>
        /// The ChannelMetric used if the creation calls omits this information.
        /// </summary>
        [JsonProperty(Required = Required.Default, PropertyName = "DefaultChannelMetrics")]
        public ChannelMetric DefaultChannelMetrics { get; set; } = new ChannelMetric();
    }
}
