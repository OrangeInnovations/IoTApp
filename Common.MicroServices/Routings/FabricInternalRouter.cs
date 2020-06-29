using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Query;
using System.Linq;
using System.Threading.Tasks;

namespace Common.MicroServices.Routings
{
    /// <summary>
    /// Fabric Internal Router
    /// </summary>
    public class FabricInternalRouter
    {
        private readonly FabricClient _fabricClient;
        public FabricInternalRouter(FabricClient fabricClient)
        {
            _fabricClient = fabricClient;
        }
        

        public async Task<Uri> BuilderStatefulInternalRandomHttpUrl(Uri serviceUri, string relativeUrl)
        {
            long servicePartitionKey =await GetRandomPartitionKeyAsync(serviceUri);
            Uri fullRri = new HttpServiceUriBuilder()
                .SetServiceName(serviceUri)
                .SetPartitionKey(servicePartitionKey)
                .SetServicePathAndQuery(relativeUrl)
                .Build();

            return fullRri;
        }

        private  readonly Random _random = new Random();
        private  readonly object _syncLock = new object();
        private async Task<long> GetRandomPartitionKeyAsync(Uri serviceUri)
        {
            ServicePartitionList partitions = await _fabricClient.QueryManager.GetPartitionListAsync(serviceUri);
            if ((partitions == null) || (!partitions.Any()))
            {
                throw new PartitionNotAvailableException($"{serviceUri.ToString()} is not Available now.");
            }

            List<long> list = partitions.Select(c => ((Int64RangePartitionInformation)c.PartitionInformation).LowKey).ToList();

            int max = list.Count;
            
            int index;
            lock (_syncLock)
            {
                index = _random.Next(0, max);
            }
            return list[index];
        }
    }
}
