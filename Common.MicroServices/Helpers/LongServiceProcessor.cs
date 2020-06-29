using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Common.MicroServices.Helpers;

namespace Common.Microservices.Helpers
{
    public class LongServiceProcessor
    {
        private readonly IServiceEventSource _serviceEventSource;
        private readonly ServiceContext _serviceContext;

        public LongServiceProcessor(IServiceEventSource serviceEventSource = null, ServiceContext serviceContext = null)
        {
            _serviceEventSource = serviceEventSource;
            _serviceContext = serviceContext;
        }

        public async Task ProcessEndLessAsync(CancellationToken cancellationToken, Func<CancellationToken, Task> funcAsync)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await funcAsync(cancellationToken);
            }
        }

        //public async Task ProcessEndLessTask(CancellationToken cancellationToken,
        //    Func<CancellationToken, Task> funcAsync)
        //{
        //    await Task.Run(() => ProcessEndLessAsync(cancellationToken, funcAsync), cancellationToken);
        //}


        public async Task RunEndLessTask(CancellationToken cancellationToken, Func<CancellationToken, Task> funcAsync)
        {
            long retryNum = 0;
            while (true)
            {

                _serviceEventSource?.LoggingServiceMessage(_serviceContext,
                    "RunEndLessTask is starting funcAsync {0} times at {1} .", ++retryNum, DateTimeOffset.UtcNow.ToString());

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await funcAsync(cancellationToken);
                }
                catch (OperationCanceledException e)
                {
                    _serviceEventSource?.LoggingServiceMessage(_serviceContext,
                        "funcAsync met exception, the type: {0}, exception= {1} ", e.GetType().Name, e.ToString());
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                }
                catch (FabricNotPrimaryException e)
                {
                    _serviceEventSource?.LoggingServiceMessage(_serviceContext,
                        "funcAsync met exception, the type: {0}, exception= {1} ", e.GetType().Name, e.ToString());
                    throw;
                }
                catch (Exception e)
                {
                    _serviceEventSource?.LoggingServiceMessage(_serviceContext,
                        "funcAsync met exception, the type: {0}, exception= {1} ", e.GetType().Name, e.ToString());
                }
            }
        }
    }
}
