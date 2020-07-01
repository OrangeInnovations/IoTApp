using System;

namespace Common.AzureResources.Hubs
{
    public class MyEventHubPartitionInfo
    {
        public string PartitionId { get; set; }
        public long LastEnqueuedSequenceNumber { get; set; }

        public DateTime LastEnqueuedTimeUtc { get; set; }
    }
}