namespace Common.MicroServices.Models
{
    public class ProcessMonitorData
    {

        public string StatefulServiceName { get; set; }

        public string StatefulPartitionId { get; set; }

        public string ReliableDictOrQueueName { get; set; }

        public string Key { get; set; }

        public long Length { get; set; }
    }
}