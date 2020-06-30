using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Microservices.Helpers;
using Common.MicroServices.Helpers;
using Common.Utilities;
using IoTHubService.Services;
using Microsoft.AspNetCore.Hosting;
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
        private readonly int _offsetInterval = 5;
        private readonly int _routerServiceBackupFrequentSeconds;
        private readonly Guid _servicePartitionId;
        private int _offsetIteration = 0;

        public IoTHubService(StatefulServiceContext context, Settings settings)
            : base(context)
        {
            _settings = settings;
            _offsetInterval = settings.OffsetInterval;
            _routerServiceBackupFrequentSeconds = settings.RouterServiceBackupFrequentSeconds;

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

            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            //var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>("myDictionary");

            //while (true)
            //{
            //    cancellationToken.ThrowIfCancellationRequested();

            //    using (var tx = this.StateManager.CreateTransaction())
            //    {
            //        var result = await myDictionary.TryGetValueAsync(tx, "Counter");

            //        ServiceEventSource.Current.ServiceMessage(this.Context, "Current Counter Value: {0}",
            //            result.HasValue ? result.Value.ToString() : "Value does not exist.");

            //        await myDictionary.AddOrUpdateAsync(tx, "Counter", 0, (key, value) => ++value);

            //        // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
            //        // discarded, and nothing is saved to the secondary replicas.
            //        await tx.CommitAsync();
            //    }

            //    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            //}
        }

        private async Task RunRunEndlessStreamProcessTask(CancellationToken cancellationToken)
        {
            await _longServiceProcessor.RunEndLessTask(cancellationToken, RunEndlessStreamAnalysesAsync);
        }

        private Task RunEndlessStreamAnalysesAsync(CancellationToken arg)
        {
            throw new NotImplementedException();
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
            //if (_latestSequenceNumber > _backupSequenceNumber)
            //{
            //    try
            //    {
            //        IReliableDictionary<string, string> streamOffsetDictionary =
            //            await this.StateManager.GetOrAddAsync<IReliableDictionary<string, string>>(Names.EventHubOffsetDictionaryName);

            //        using (ITransaction tx = this.StateManager.CreateTransaction())
            //        {
            //            await streamOffsetDictionary.SetAsync(tx, Names.HubStreamOffSetKey,
            //                _latestSequenceNumber.ToString());
            //            await tx.CommitAsync();
            //        }
            //    }
            //    catch (Exception exception)
            //    {
            //        string err = $"RouterService BackupSequenceNumberAsync met exception, event hub partition={_eventHubPartitionId}, " +
            //                     $"exception type={exception.GetType().Name}, exception= {exception.Message}, at partition ={Context.PartitionId}.";

            //        ServiceEventSource.Current.CriticalError("RouterService", err);
            //    }

            //    var suc = await SaveEventDataSequenceNumberAsync();
            //    if (suc)
            //    {
            //        _backupSequenceNumber = _latestSequenceNumber;
            //    }
            //}

        }
    }
}
