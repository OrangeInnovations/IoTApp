using System;
using System.Text;

namespace IoTHubService.Services
{
    public interface IPartitionServiceFinder
    {
        long FindPartitionKey(string id);
    }
}
