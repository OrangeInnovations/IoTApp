using IoTHubService.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IoTHubService.Services
{
    public class EventHubInfoDataService : IEventHubInfoDataService
    {
        public List<EventHubPartitionInfo> FindAllEventHubPartitions()
        {
            throw new NotImplementedException();
        }

        public async Task<long> FindHubProcessPartionSequenceNumberAsync(string eventHubPartitionId)
        {
            return -1;
        }

        public async Task<bool> SaveHubProcessPartionInfoAsync(string eventHubPartitionId, long sequenceNumber)
        {
            return true;
        }
    }
}
