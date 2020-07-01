using IoTHubService.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IoTHubService.Services
{
    public interface IEventHubInfoDataService
    {
        Task<long> FindHubProcessPartionSequenceNumberAsync(string eventHubPartitionId);
        Task<bool> SaveHubProcessPartionInfoAsync(string eventHubPartitionId, long sequenceNumber);

        List<EventHubPartitionInfo> FindAllEventHubPartitions();
    }
}
