using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOG.RelayHub.Common.Entity
{
    /// <summary>
    /// Client connections being monitored due to invalid authorization values.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class Security
    {
        /// <summary>
        /// The number of consecutive attempts which have failued for this IP address.
        /// </summary>
        [JsonProperty(Required = Required.Always, PropertyName = "FailedTokenValues")]
        public int FailedTokenValues { get; set; } = 1;
        /// <summary>
        /// The next time when an access attempt will be allowed.  Note: Only set when FailedTokenValues > 2
        /// </summary>
        [JsonProperty(Required = Required.Always, PropertyName = "DenyAccessAttemptsUntil")]
        public DateTime DenyAccessAttemptsUntil { get; set; } = DateTime.MinValue;
    }
}
