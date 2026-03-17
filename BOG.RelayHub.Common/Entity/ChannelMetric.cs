using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BOG.RelayHub.Common.Entity
{
    [JsonObject]
    public class ChannelMetric
    {
        [JsonProperty(PropertyName = "MaxQueuedItemCount", Required = Required.Default)]
        public int MaxQueuedItemCount { get; set; } = 500;

        [JsonProperty(PropertyName = "MaxQueuedItemSize", Required = Required.Default)]
        public double MaxQueuedItemSize { get; set; } = 52428800.0;

        [JsonProperty(PropertyName = "MaxReferenceItemCount", Required = Required.Default)]
        public int MaxReferenceItemCount { get; set; } = 500;

        [JsonProperty(PropertyName = "MaxReferenceItemSize", Required = Required.Default)]
        public double MaxReferenceItemSize { get; set; } = 52428800.0;
    }
}
