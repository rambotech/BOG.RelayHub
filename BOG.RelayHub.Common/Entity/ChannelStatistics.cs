using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOG.RelayHub.Common.Entity
{
    /// <summary>
    /// The running counts of the channel's activity persisted during the channels lifetime.
    /// </summary>
    [JsonObject]
    public class ChannelStatistics
    {
        [JsonProperty(PropertyName = "ChannelName", Required = Required.Always)]
        /// <summary>
        /// The name of the channel
        /// </summary>
        public string ChannelName { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "ExistedAtStartup", Required = Required.Always)]
        /// <summary>
        /// The name of the channel
        /// </summary>
        public bool ExistedAtStartup { get; set; } = false;

        [JsonProperty(PropertyName = "CreatedOn", Required = Required.Always)]
        /// <summary>
        /// The name of the channel
        /// </summary>
        public DateTime CreatedOn { get; set; } = DateTime.Now;

        [JsonProperty(PropertyName = "LastActivity", Required = Required.Always)]
        /// <summary>
        /// The name of the channel
        /// </summary>
        public DateTime LastActivity { get; set; } = DateTime.Now;

        [JsonProperty(PropertyName = "QueueFileCount", Required = Required.Always)]
        /// <summary>
        /// The count of items in the queue (for all recipients)
        /// </summary>
        public Dictionary<string, int> QueueFileCount { get; set; } = new Dictionary<string, int>();

        [JsonProperty(PropertyName = "QueueFileStorageSize", Required = Required.Always)]
        /// <summary>
        /// The storeage size (bytes) consumed by items in the queue (for all recipients)
        /// </summary>
        public Dictionary<string, long> QueueFileStorageSize { get; set; } = new Dictionary<string, long>();

        [JsonProperty(PropertyName = "ReferenceFileCount", Required = Required.Always)]
        /// <summary>
        /// The count of items in the reference folder.
        /// </summary>
        public int ReferenceFileCount { get; set; } = 0;

        [JsonProperty(PropertyName = "ReferenceFileStorageSize", Required = Required.Always)]
        /// <summary>
        /// The storeage size (bytes) consumed by items in the reference folder.
        /// </summary>
        public long ReferenceFileStorageSize { get; set; } = 0L;
    }
}