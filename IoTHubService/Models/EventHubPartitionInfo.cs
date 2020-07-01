using System;
using System.Collections.Generic;
using System.Text;

namespace IoTHubService.Models
{
    public class EventHubPartitionInfo
    {
       
        public string EventHubPartitionID { get; set; }

        public long SequenceNumber { get; set; }

        public DateTimeOffset UpdateTimestamp { get; set; }
    }
}
