using BOG.SwissArmyKnife;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOG.RelayHub.Client.Entity
{
    [JsonObject(MemberSerialization = MemberSerialization.OptOut)]
    public class RelayHubApiConfig
    {
        [JsonProperty(Required = Required.Always, PropertyName = "BaseURI")]
        public string BaseURI { get; set; } = string.Empty;

        [JsonProperty(Required = Required.Always, PropertyName = "UsageTokenValue")]
        public string UsageTokenValue { get; set; } = string.Empty;

        [JsonProperty(Required = Required.Always, PropertyName = "AdministrativeTokenValue")]
        public string AdministrativeTokenValue { get; set; } = string.Empty;

        [JsonProperty(Required = Required.Always, PropertyName = "ExecutiveTokenValue")]
        public string ExecutiveTokenValue { get; set; } = string.Empty;

        [JsonProperty(Required = Required.Always, PropertyName = "IgnoreBadSSL")]
        public bool IgnoreBadSSL { get; set; } = false;

        [JsonProperty(Required = Required.Always, PropertyName = "TimeoutSeconds")]
        public int TimeoutSeconds { get; set; } = 120;

        public void Validate()
        {
            if (!string.IsNullOrWhiteSpace(BaseURI))
            {
                var uri = new Url(BaseURI);
                if (!new string[] { "http", "https" }.Contains(uri.Scheme.ToLower()))
                {
                    throw new ArgumentNullException("BaseURI must be valid URI");
                }
            }
            else
            {
                throw new ArgumentNullException("BaseURI can not be empty");
            }
            if (string.IsNullOrWhiteSpace(ExecutiveTokenValue))
            {
                throw new ArgumentNullException("ExecutiveTokenValue can not be empty");
            }
            if (string.IsNullOrWhiteSpace(AdministrativeTokenValue))
            {
                throw new ArgumentNullException("AdministrativeTokenValue can not be empty");
            }
            if (string.IsNullOrWhiteSpace(UsageTokenValue))
            {
                throw new ArgumentNullException("UsageTokenValue can not be empty");
            }
            if (TimeoutSeconds < 20 || TimeoutSeconds > 900)
            {
                throw new ArgumentNullException($"TimeoutSeconds is {TimeoutSeconds}, but must be between 20 and 900");
            }
        }
    }
}
