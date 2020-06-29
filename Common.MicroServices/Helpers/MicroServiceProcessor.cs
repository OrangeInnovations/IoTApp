using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Common.Microservices.Helpers;

namespace Common.MicroServices.Helpers
{
    public class MicroServiceProcessor
    {
        private readonly IServiceEventSource _serviceEventSource;
        private readonly ServiceContext _serviceContext;

        public MicroServiceProcessor(ServiceContext serviceContext, IServiceEventSource serviceEventSource = null)
        {
            _serviceEventSource = serviceEventSource;
            _serviceContext = serviceContext;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="mainFuncAsync">the main function</param>
        /// <param name="cancelFuncAsync">task cancel function</param>
        /// <param name="otherExceptionHandlerAsync"> other type of exception handler </param>
        /// <returns></returns>
        public async Task ProcessAsync(CancellationToken cancellationToken, Func<Task> mainFuncAsync, Func<Exception, Task> cancelFuncAsync = null, Func<CancellationToken, Exception, Task> otherExceptionHandlerAsync = null)
        {
            try
            {
                await mainFuncAsync();
            }
            catch (OperationCanceledException e)
            {
                _serviceEventSource?.LoggingServiceMessage(_serviceContext,
                    "ProcessAsync is canceled, the type:{0}, Exception: {1}", e.GetType().Name, e.ToString());
                if (cancellationToken.IsCancellationRequested && cancelFuncAsync != null)
                {
                    await cancelFuncAsync(e);
                }

                throw;

            }
            catch (TimeoutException te)
            {
                // transient error. Retry.
                _serviceEventSource?.LoggingServiceMessage(_serviceContext,
                    "ProcessAsync met TimeoutException, the type:{0}, Exception: {1}", te.GetType().Name, te.ToString());
            }
            catch (FabricTransientException fte)
            {
                // transient error. Retry.
                _serviceEventSource?.LoggingServiceMessage(_serviceContext,
                    "ProcessAsync met FabricTransientException: {0}", fte.Message);
            }
            catch (FabricNotPrimaryException fnpe)
            {
                // not primary any more, time to quit.
                _serviceEventSource?.LoggingServiceMessage(_serviceContext,
                    "ProcessAsync met FabricNotPrimaryException: {0}", fnpe.Message);

                if (cancelFuncAsync != null)
                {
                    await cancelFuncAsync(fnpe);
                }

                throw;
            }
            catch (Exception e)
            {
                _serviceEventSource?.LoggingServiceMessage(_serviceContext,
                    "ProcessAsync met exception, the type: {0}, exception= {1} ", e.GetType().Name, e.ToString());

                if (otherExceptionHandlerAsync != null)
                {
                    await otherExceptionHandlerAsync(cancellationToken, e);
                }
                else
                {
                    await DefaultHandleOtherExceptionAsync(cancellationToken, e);
                }

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="mainFuncAsync">the main function</param>
        /// <param name="cancelFuncAsync">task cancel function</param>
        /// <param name="otherExceptionHandlerAsync"> other type of exception handler </param>
        /// <returns></returns>
        public async Task<T> ProcessAsync<T>(CancellationToken cancellationToken, Func<Task<T>> mainFuncAsync, Func<Exception, Task> cancelFuncAsync = null, Func<CancellationToken, Exception, Task> otherExceptionHandlerAsync = null)
        {
            try
            {
                return await mainFuncAsync();
            }
            catch (OperationCanceledException e)
            {
                _serviceEventSource?.LoggingServiceMessage(_serviceContext,
                    "ProcessAsync is canceled, the type:{0}, Exception: {1}", e.GetType().Name, e.ToString());

                if (cancellationToken.IsCancellationRequested && cancelFuncAsync != null)
                {
                    await cancelFuncAsync(e);
                }

                throw;
            }
            catch (TimeoutException te)
            {
                // transient error. Retry.
                _serviceEventSource?.LoggingServiceMessage(_serviceContext,
                    "ProcessAsync met TimeoutException, the type:{0}, Exception: {1}", te.GetType().Name, te.ToString());
            }
            catch (FabricTransientException fte)
            {
                // transient error. Retry.
                _serviceEventSource?.LoggingServiceMessage(_serviceContext,
                    "ProcessAsync met FabricTransientException: {0}", fte.Message);
            }
            catch (FabricNotPrimaryException fnpe)
            {
                // not primary any more, time to quit.
                _serviceEventSource?.LoggingServiceMessage(_serviceContext,
                    "ProcessAsync met FabricNotPrimaryException: {0}", fnpe.Message);
                if (cancelFuncAsync != null)
                {
                    await cancelFuncAsync(fnpe);
                }

                throw;
            }
            catch (Exception e)
            {
                _serviceEventSource?.LoggingServiceMessage(_serviceContext,
                    "ProcessAsync met exception, the type: {0}, exception= {1}", e.GetType().Name, e.ToString());

                if (otherExceptionHandlerAsync != null)
                {
                    await otherExceptionHandlerAsync(cancellationToken, e);
                }
                else
                {
                    await DefaultHandleOtherExceptionAsync(cancellationToken, e);
                }
            }

            return default(T);
        }
        private async Task DefaultHandleOtherExceptionAsync(CancellationToken cancellationToken, Exception e)
        {
            if (e is OutOfMemoryException)
            {
                GC.Collect(2);
                await Task.Delay(TimeSpan.FromMilliseconds(30), cancellationToken);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }
}

