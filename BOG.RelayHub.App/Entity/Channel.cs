using BOG.RelayHub.Common.Entity;

namespace BOG.RelayHub.Entity
{
    /// <summary>
    /// A location for queues and references in a group: admin token to create/delete
    /// </summary>
    public class Channel
    {
        /// <summary>
        /// THe main path for the channel
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// The path to the queues for the channel
        /// </summary>
        public string QueuePath { get; set; } = string.Empty;

        /// <summary>
        /// The path to the references for the channel
        /// </summary>
        public string ReferencePath { get; set; } = string.Empty;

        /// <summary>
        /// Up to the oldest 20 files names residing in the channel's queue folder
        /// </summary>
        public Dictionary<string, Queue<string>> QueuedFilenames { get; set; } = new Dictionary<string, Queue<string>>();

        /// <summary>
        /// Allow single user to create or remove a file in the queued items.
        /// </summary>
        public object LockQueuedFilenames { get; set; } = new object();

        /// <summary>
        /// Allow single user to create or remove a reference file.
        /// </summary>
        public object LockReferenceFilenames { get; set; } = new object();

        /// <summary>
        /// Contains the count and sizes of objects in the channel.
        /// </summary>
        public ChannelStatistics Statistics { get; set; } = new ChannelStatistics();

        /// <summary>
        /// The count of items in the queue (for all recipients)
        /// </summary>
        public int QueueFileCount { get; set; } = 0;

        /// <summary>
        /// The storeage size (bytes) consumed by items in the queue (for all recipients)
        /// </summary>
        public long QueueFileStorageSize{ get; set; } = 0L;

        /// <summary>
        /// The count of items in the reference folder.
        /// </summary>
        public int ReferenceFileCount { get; set; } = 0;

        /// <summary>
        /// The storeage size (bytes) consumed by items in the reference folder.
        /// </summary>
        public long ReferenceFileStorageSize { get; set; } = 0L;
    }
}
