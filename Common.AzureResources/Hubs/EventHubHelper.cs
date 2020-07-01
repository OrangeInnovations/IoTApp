using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
//using Microsoft.ServiceBus;
//using Microsoft.ServiceBus.Messaging;
using EventHubClient = Microsoft.Azure.EventHubs.EventHubClient;

namespace Common.AzureResources.Hubs
{
    public class EventHubHelper
    {
        private readonly Microsoft.Azure.EventHubs.RetryPolicy _retryPolicy;

        public EventHubHelper(int minimalMilliseconds = 10, int maximumMilliseconds = 50, int retryTimes = 10)
        {
            var minimumBackoff = TimeSpan.FromMilliseconds(minimalMilliseconds);
            var maximumBackoff = TimeSpan.FromMilliseconds(maximumMilliseconds);

            _retryPolicy = Microsoft.Azure.EventHubs.RetryPolicy.Default;//new Microsoft.Azure.EventHubs.RetryExponential(minimumBackoff, maximumBackoff, retryTimes);
        }

        public EventHubHelper()
        {
            _retryPolicy = Microsoft.Azure.EventHubs.RetryPolicy.Default;
        }

        //public EventHubClient CreatEventHubClientWhetherHubExist(string servicebusConnectionstring, string hubPath)
        //{
        //    var namespaceManager = NamespaceManager.CreateFromConnectionString(servicebusConnectionstring);
        //    namespaceManager.Settings.OperationTimeout = TimeSpan.FromMinutes(10);
        //    namespaceManager.Settings.RetryPolicy = new Microsoft.ServiceBus.RetryExponential(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10), 10);

        //    bool suc = false;
        //    int tryTimes = 100;
        //    EventHubClient client = null;
        //    while (!suc && (--tryTimes > 0))
        //    {
        //        try
        //        {
        //            EventHubDescription description;
        //            if (!namespaceManager.EventHubExists(hubPath))
        //            {
        //                description = new EventHubDescription(hubPath)
        //                {
        //                    MessageRetentionInDays = 7,
        //                    PartitionCount = 32
        //                };
        //                if (!namespaceManager.EventHubExists(hubPath))
        //                {
        //                    namespaceManager.CreateEventHub(description);
        //                }
        //            }

        //            var connectionStringBuilder = new EventHubsConnectionStringBuilder(servicebusConnectionstring)
        //            {
        //                EntityPath = hubPath,
        //                OperationTimeout = TimeSpan.FromSeconds(180)
        //            };
        //            client = EventHubClient.CreateFromConnectionString(connectionStringBuilder.ToString());


        //            client.RetryPolicy = _retryPolicy;
        //            suc = true;
        //        }
        //        catch (Exception)
        //        {
        //            Task.Delay(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
        //        }
        //    }

        //    return client;
        //}


        public EventHubClient CreatEventHubClientIfExist(string hubsConnectionstring, string hubPath)
        {
            var connectionStringBuilder = new EventHubsConnectionStringBuilder(hubsConnectionstring)
            {
                EntityPath = hubPath,
                OperationTimeout = TimeSpan.FromSeconds(180)
            };
            var hubClient = EventHubClient.CreateFromConnectionString(connectionStringBuilder.ToString());

            hubClient.RetryPolicy = _retryPolicy;
            return hubClient;
        }


        public EventHubClient CreatEventHubClientIfExist(string hubConnectString)
        {

            var hubClient = EventHubClient.CreateFromConnectionString(hubConnectString);
            hubClient.RetryPolicy = _retryPolicy;
            return hubClient;
        }

        static string iotHubD2cEndpoint = "messages/events";
        public EventHubClient CreateEventHubClientFromIotHub(string eventHubCompatibleEndPoint, string compatibleName)
        {
            var connectionStringBuilder = new EventHubsConnectionStringBuilder(eventHubCompatibleEndPoint)
            {
                EntityPath = compatibleName,
                OperationTimeout = TimeSpan.FromSeconds(180)
            };
            var hubClient = EventHubClient.CreateFromConnectionString(connectionStringBuilder.ToString());

            hubClient.RetryPolicy = _retryPolicy;
            return hubClient;
        }


        public async Task<Dictionary<string, MyEventHubPartitionInfo>> GetCurrentSequencesAsync(EventHubClient eventHubClient)
        {
            Dictionary<string, MyEventHubPartitionInfo> dictionary=new Dictionary<string, MyEventHubPartitionInfo>();

            var eventHubRuntimeInfo = await eventHubClient.GetRuntimeInformationAsync();

            string[] partitionIds = eventHubRuntimeInfo.PartitionIds;

            foreach (var c in partitionIds)
            {
                var information = await eventHubClient.GetPartitionRuntimeInformationAsync(c);
                
                dictionary[c] = new MyEventHubPartitionInfo()
                {
                    PartitionId = c,
                    LastEnqueuedSequenceNumber = information.LastEnqueuedSequenceNumber,
                    LastEnqueuedTimeUtc = information.LastEnqueuedTimeUtc
                };
            }

            return dictionary;
        }

        public async Task<Dictionary<string, MyEventHubPartitionInfo>> GetCurrentSequencesAsync(string hubsConnectionstring,
            string hubPath)
        {
            EventHubClient eventHubClient = CreatEventHubClientIfExist(hubsConnectionstring, hubPath);
            return await GetCurrentSequencesAsync(eventHubClient);
        }
    }
}

