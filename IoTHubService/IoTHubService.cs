using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Common.AzureResources.Hubs;
using Common.Microservices.Helpers;
using Common.MicroServices.Helpers;
using Common.MicroServices.Routings;
using Common.Utilities;
using IoTHubService.Models;
using IoTHubService.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.EventHubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace IoTHubService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class IoTHubService : StatefulService
    {
        private readonly Settings _settings;
        private readonly RetryService _retryService;
        private readonly MicroServiceProcessor _microServiceProcessor;
        private readonly LongServiceProcessor _longServiceProcessor;
        private readonly PartitionServiceFinder _partitionServiceFinder;
        private readonly IEventHubInfoDataService _eventHubInfoDataService;

        private readonly int _offsetInterval = 5;
        private readonly int _routerServiceBackupFrequentSeconds;
        private readonly Guid _servicePartitionId;
        private readonly Uri _routerServiceUri;
        private int _offsetIteration = 0;
        private long _latestSequenceNumber = -1;
        private long _backupSequenceNumber = -1;
        private string _eventHubPartitionId = string.Empty;

        public string EventHubPartitionId => _eventHubPartitionId;
        public IoTHubService(StatefulServiceContext context, Settings settings)
            : base(context)
        {
            _settings = settings;
            _offsetInterval = settings.OffsetInterval;
            _routerServiceBackupFrequentSeconds = settings.RouterServiceBackupFrequentSeconds;

            _servicePartitionId = context.PartitionId;
            ServiceUriBuilder routerServiceNameUriBuilder = new ServiceUriBuilder(Names.RouterServiceName);
            _routerServiceUri = routerServiceNameUriBuilder.Build();

            _retryService = new RetryService(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(30));
            _microServiceProcessor = new MicroServiceProcessor(context, ServiceEventSource.Current);

            _longServiceProcessor = new LongServiceProcessor(ServiceEventSource.Current, context);

            _partitionServiceFinder = new PartitionServiceFinder();
        }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new ServiceReplicaListener[]
            {
                new ServiceReplicaListener(serviceContext =>
                    new KestrelCommunicationListener(serviceContext, (url, listener) =>
                    {
                        ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting IoTHub Service Kestrel on {url}");

                        return new WebHostBuilder()
                                    .UseKestrel()
                                    .ConfigureServices(
                                        services => services
                                            .AddSingleton<StatefulServiceContext>(serviceContext)
                                            .AddSingleton<IReliableStateManager>(this.StateManager)
                                            .AddSingleton(this))
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<Startup>()
                                    .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.UseUniqueServiceUrl)
                                    .UseUrls(url)
                                    .Build();
                    }))
            };
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                ServiceEventSource.Current.ServiceMessage(Context,
                    $"RouterService start RunAsync at {_servicePartitionId} at {DateTimeOffset.UtcNow}");

                await Task.WhenAll(
                    RunRunEndlessStreamProcessTask(cancellationToken), RunEndlessDataBackupAsync(cancellationToken));
            }
            catch (Exception e)
            {
                string err = $"RouterService RunAsync stopped and met exception, exception type={e.GetType().Name}, " +
                             $" exception= {e.Message}, at partition ={Context.PartitionId}.";
                //ServiceEventSource.Current.CriticalError("RouterService", err);
                ServiceEventSource.Current.ServiceRequestStop("MessageService", err);
                throw;
            }
            finally
            {
                await BackupSequenceNumberAsync();
            }
        }

        private async Task RunRunEndlessStreamProcessTask(CancellationToken cancellationToken)
        {
            await _longServiceProcessor.RunEndLessTask(cancellationToken, RunEndlessStreamAnalysesAsync);
        }

        private async Task RunEndlessStreamAnalysesAsync(CancellationToken cancellationToken)
        {
            string info1 = $"RouterService RunEndlessStreamAnalysesAsync is starting at partition ={Context.PartitionId} at {DateTimeOffset.UtcNow}.";
            ServiceEventSource.Current.ServiceMessage(this.Context, info1);

            // this Reliable Dictionary is used to keep track of our position in Event Hub.
            // If this service fails over, this will allow it to pick up where it left off in the event stream.
            IReliableDictionary<string, string> streamOffsetDictionary =
                await this.StateManager.GetOrAddAsync<IReliableDictionary<string, string>>(Names.EventHubOffsetDictionaryName);

            PartitionReceiver partitionReceiver = null;
            try
            {
                partitionReceiver = await ConnectToEventHubAsync(_settings.EventHubConnectionString, _settings.EventHubName, streamOffsetDictionary);
                //using (HttpClient httpClient = new HttpClient(new HttpServiceClientHandler()))
                //{
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        await MicroProcessEventHubPartitionStreamAsync(cancellationToken, partitionReceiver);
                    }
                //}

            }
            catch (Exception exception)
            {

                string err = $"RouterService RunEndlessStreamAnalysesAsync met exception, event hub partition={_eventHubPartitionId}, " +
                             $"exception type={exception.GetType().Name}, exception= {exception.Message}, at partition ={Context.PartitionId}.";

                //ServiceEventSource.Current.CriticalError("RouterService", err);

                //ReportHealthError(cancellationToken, err);

                throw;
            }
            finally
            {
                if (partitionReceiver != null)
                {
                    await partitionReceiver.CloseAsync();
                }

                string info = $"RouterService RunEndlessStreamAnalysesAsync stopped when event hub partition={_eventHubPartitionId}," +
                              $" partition ={Context.PartitionId} at {DateTimeOffset.UtcNow}.";

                //ServiceEventSource.Current.CriticalError("RouterService", info);
            }
        }

        private async Task MicroProcessEventHubPartitionStreamAsync(CancellationToken cancellationToken, PartitionReceiver partitionReceiver)
        {
            await _microServiceProcessor.ProcessAsync(cancellationToken, async () =>
                  await ProcessEventHubPartitionStreamAsync(cancellationToken, partitionReceiver)
                );
        }

        private async Task ProcessEventHubPartitionStreamAsync(CancellationToken cancellationToken, PartitionReceiver partitionReceiver)
        {
            try
            {
                var ehEvents = await _retryService.RetryTask(() =>
                    partitionReceiver.ReceiveAsync(1, TimeSpan.FromSeconds(5)));

                var ehEvent = ehEvents?.First();
                if (ehEvent != null)
                {
                    var curSequenceNumber = ehEvent.SystemProperties.SequenceNumber;

                    bool suc = await _retryService.RetryTask(() =>
                       ProcessEventDataAsync(ehEvent,  cancellationToken));

                    if (suc)
                    {
                        _latestSequenceNumber = curSequenceNumber;
                    }
                }
            }
            catch (OperationCanceledException oe)
            {
                //if (oe.Message.Contains("The AMQP object session"))
                //{
                string err = $"RouterService ProcessEventHubPartitionStreamAsync met OperationCanceledException, event hub partition={_eventHubPartitionId}, " +
                             $"exception type: {oe.GetType().Name}, exception: {oe.Message}, at partition: {Context.PartitionId}";
                //ServiceEventSource.Current.Error("RouterService", err);

                throw;
                //}
            }
            catch (Exception e)
            {
                string err = $"RouterService ProcessEventHubPartitionStreamAsync met exception, event hub partition={_eventHubPartitionId}, " +
                             $"exception type: {e.GetType().Name}, exception: {e.Message},at partition: {Context.PartitionId}";
                //ServiceEventSource.Current.Error("RouterService", err);

                throw;
            }

        }

        private async Task<bool> ProcessEventDataAsync(Microsoft.Azure.EventHubs.EventData ehEvent, CancellationToken cancellationToken)
        {
            try
            {
                return true;

            }
            catch (Exception e)
            {
                //ServiceEventSource.Current.ServiceMessage(this.Context, $"RouterService ProcessEventDataAsync met exception= ( {e} )");

                string err = $"RouterService ProcessEventDataAsync met exception, exception type={e.GetType().Name}, exception= {e.Message}, at partition ={Context.PartitionId}.";
                //ServiceEventSource.Current.Error("RouterService", err);

                await HandleExceptionInsideUTask(cancellationToken, e);

                throw;
            }

        }
        private async Task HandleExceptionInsideUTask(CancellationToken cancellationToken, Exception e)
        {
            if (e is OutOfMemoryException)
            {
                GC.Collect(2);
                await Task.Delay(TimeSpan.FromMilliseconds(30), cancellationToken);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }

        private async Task<PartitionReceiver> ConnectToEventHubAsync(string eventHubConnectionString, string hubName,
            IReliableDictionary<string, string> streamOffsetDictionary)
        {
            var eventHubHelper = new EventHubHelper();
            var eventHubClient = eventHubHelper.CreatEventHubClientIfExist(eventHubConnectionString, hubName);

            EventHubRuntimeInformation eventHubRuntimeInfo = await eventHubClient.GetRuntimeInformationAsync();

            PartitionReceiver partitionReceiver = null;
            string[] partitionIds = eventHubRuntimeInfo.PartitionIds;
            _eventHubPartitionId = await GetMatchingEventHubPartitionId(partitionIds);

            try
            {
                using (ITransaction tx = this.StateManager.CreateTransaction())
                {
                    ConditionalValue<string> offsetResult = await streamOffsetDictionary.TryGetValueAsync(tx, Names.HubStreamOffSetKey);

                    EventPosition eventPosition;
                    if (offsetResult.HasValue)
                    {
                        // continue where the service left off before the last failover or restart.
                        eventPosition = EventPosition.FromSequenceNumber(long.Parse(offsetResult.Value));
                    }
                    else
                    {
                        // first time this service is running so there is no offset value yet. start with the current time.
                        // Load from database sequence number
                        eventPosition = await LoadEventPositionFromDatabaseAsync() ??
                                        EventPosition.FromEnqueuedTime(DateTime.UtcNow);
                        //EventPosition.FromEnqueuedTime(DateTime.UtcNow.Subtract(TimeSpan.FromHours(5)));//EventPosition.FromEnqueuedTime(DateTime.UtcNow);

                        if (eventPosition.SequenceNumber != null)
                        {
                            _latestSequenceNumber = eventPosition.SequenceNumber.Value;

                            await streamOffsetDictionary.SetAsync(tx, Names.HubStreamOffSetKey, eventPosition.SequenceNumber.ToString());
                            await tx.CommitAsync();
                        }
                    }

                    ServiceEventSource.Current.ServiceMessage(this.Context, "Creating EventHub listener on partition {0} with SequenceNumber {1}",
                        _eventHubPartitionId, eventPosition.SequenceNumber);

                    partitionReceiver = eventHubClient.CreateReceiver(PartitionReceiver.DefaultConsumerGroupName, _eventHubPartitionId, eventPosition);
                }
            }
            catch (Exception e)
            {
                //ServiceEventSource.Current.ServiceMessage(this.Context, $"RouterService ConnectToEventHubAsync met exception= ( {e} )");

                string err = $"RouterService ConnectToEventHubAsync met exception, exception type={e.GetType().Name}, exception= {e.Message}, at partition ={Context.PartitionId} .";
                //ServiceEventSource.Current.CriticalError("RouterService", err);

                throw;
            }


            return partitionReceiver;
        }

        private async Task<EventPosition> LoadEventPositionFromDatabaseAsync()
        {
            try
            {
                _latestSequenceNumber = await _retryService.RetryTask(() => _eventHubInfoDataService.FindHubProcessPartionSequenceNumberAsync(_eventHubPartitionId));

                if (_latestSequenceNumber >= 0)
                {
                    return EventPosition.FromSequenceNumber(_latestSequenceNumber);
                }
            }
            catch (Exception e)
            {
                //ServiceEventSource.Current.ServiceMessage(this.Context, $"RouterService LoadEventPositionFromDatabaseAsync met exception={e} ");

                string err = $"RouterService LoadEventPositionFromDatabaseAsync met exception at partition ={Context.PartitionId} and exception= {e.Message}.";

                //ServiceEventSource.Current.Error("RouterService", err);
            }


            return null;
        }

        private async Task<string> GetMatchingEventHubPartitionId(string[] eventHubPartitionIds)
        {
            var fabric = new FabricClient();
            var fabricHelper = new FabricHelper(fabric);
            var curPartitionIndex = await fabricHelper.FindServicePartitionIndexAsync(_routerServiceUri, _servicePartitionId);
            return eventHubPartitionIds[curPartitionIndex];

            //return eventHubPartitionIds[25];//for test
        }

        private async Task RunEndlessDataBackupAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(TimeSpan.FromSeconds(_routerServiceBackupFrequentSeconds), cancellationToken);
                    await MicroProcessDataBackupAsync(cancellationToken);
                }
            }
            catch (Exception exception)
            {
                //ServiceEventSource.Current.ServiceMessage(this.Context, $"RouterService RunEndlessDataBackupAsync exception ={exception}.");

                string err = $"RouterService RunEndlessDataBackupAsync stopped and met exception, exception type={exception.GetType().Name}, exception= {exception.Message}, at partition ={Context.PartitionId}.";

                //ServiceEventSource.Current.CriticalError("RouterService", err);


                await BackupSequenceNumberAsync();

                //ReportHealthError(cancellationToken, err);

                throw;
            }
        }

        private async Task MicroProcessDataBackupAsync(CancellationToken cancellationToken)
        {
            await _microServiceProcessor.ProcessAsync(cancellationToken,
                async () => await BackupSequenceNumberAsync());
        }

        private async Task BackupSequenceNumberAsync()
        {
            if (_latestSequenceNumber > _backupSequenceNumber)
            {
                try
                {
                    IReliableDictionary<string, string> streamOffsetDictionary =
                        await this.StateManager.GetOrAddAsync<IReliableDictionary<string, string>>(Names.EventHubOffsetDictionaryName);

                    using (ITransaction tx = this.StateManager.CreateTransaction())
                    {
                        await streamOffsetDictionary.SetAsync(tx, Names.HubStreamOffSetKey,
                            _latestSequenceNumber.ToString());
                        await tx.CommitAsync();
                    }
                }
                catch (Exception exception)
                {
                    string err = $"RouterService BackupSequenceNumberAsync met exception, event hub partition={_eventHubPartitionId}, " +
                                 $"exception type={exception.GetType().Name}, exception= {exception.Message}, at partition ={Context.PartitionId}.";

                    //ServiceEventSource.Current.CriticalError("RouterService", err);
                }

                var suc = await SaveEventDataSequenceNumberAsync();
                if (suc)
                {
                    _backupSequenceNumber = _latestSequenceNumber;
                }
            }

        }

        private async Task<bool> SaveEventDataSequenceNumberAsync()
        {
            try
            {
                if (_latestSequenceNumber <= 0)
                {
                    return true;
                }

                //ServiceEventSource.Current.ServiceMessage(this.Context,
                //    $"RouterService SaveEventDataSequenceNumberAsync Backup event hub partition {_eventHubPartitionId} with sequence  {_latestSequenceNumber} ");

                return await _retryService.RetryTask(
                     () => _eventHubInfoDataService.SaveHubProcessPartionInfoAsync(_eventHubPartitionId, _latestSequenceNumber));

            }
            catch (Exception e)
            {
                //ServiceEventSource.Current.ServiceMessage(this.Context,$"RouterService SaveEventDataSequenceNumberAsync met exception= ( {e.Message} )");

                string err = $"RouterService SaveEventDataSequenceNumberAsync met exception, exception type={e.GetType().Name}, exception= {e.Message}, at partition ={Context.PartitionId}. ";

                //ServiceEventSource.Current.Error("RouterService", err);
                throw;
            }

        }
    }
}
