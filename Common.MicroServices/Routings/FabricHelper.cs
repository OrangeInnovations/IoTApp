using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Query;
using System.Linq;
using System.Threading.Tasks;

namespace Common.MicroServices.Routings
{
    public class FabricHelper
    {
        private readonly FabricClient _fabricClient;
        public FabricHelper(FabricClient fabricClient)
        {
            _fabricClient = fabricClient;
        }

        public async Task<int> FindServicePartitionIndexAsync(Uri serviceUri, Guid partitionId)
        {
            ServicePartitionList partitions = await _fabricClient.QueryManager.GetPartitionListAsync(serviceUri);

            List<Guid> partitionIdList = partitions.Select(c => c.PartitionInformation.Id).ToList();

            return partitionIdList.FindIndex(c => c.Equals(partitionId));
        }


        public async Task<(int totalPartions, int partitionIndex)> FindServiceTotalPartitionsAndIndexAsync(Uri serviceUri,
            Guid partitionId)
        {
            ServicePartitionList partitions = await _fabricClient.QueryManager.GetPartitionListAsync(serviceUri);

            List<Guid> partitionIdList = partitions.Select(c => c.PartitionInformation.Id).ToList();
            var index=partitionIdList.FindIndex(c => c.Equals(partitionId));

            return (partitionIdList.Count, index);
        }
    }
}