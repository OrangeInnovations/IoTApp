using Common.MicroServices.HashAlgs;
using System.Collections.Generic;

namespace IoTHubService.Services
{
    public class PartitionServiceFinder : IPartitionServiceFinder
    {
        public PartitionServiceFinder()
        {
            _dictionary = new Dictionary<string, long>();
        }
        private Dictionary<string, long> _dictionary;

        public long FindPartitionKey(string id)
        {
            long partition;
            if (_dictionary.ContainsKey(id))
            {
                partition = _dictionary[id];
            }
            else
            {
                partition = FnvHash.Hash(id);
                _dictionary.Add(id, partition);
            }

            return partition;
        }
    }
}
