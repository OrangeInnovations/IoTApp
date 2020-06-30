using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Utilities
{
    public class RetryService
    {
        
        
        private readonly TimeSpan _minimumBackOff;
        private readonly TimeSpan _defaultBackOff;
        private readonly TimeSpan _maxBackOff;
        private readonly int _retryNumber;
        public RetryService(int minimalBackOffSecond=1,int defaultBackOffSecond=2,int maxBackOffSecond=3, int retryNumber=10)
        {
            _minimumBackOff = TimeSpan.FromSeconds(minimalBackOffSecond);
            _defaultBackOff = TimeSpan.FromSeconds(defaultBackOffSecond);
            _maxBackOff = TimeSpan.FromSeconds(maxBackOffSecond);
            _retryNumber = retryNumber;
        }

        public RetryService(TimeSpan minBackOffTimeSpan,TimeSpan defaulBackOffTimeSpan,TimeSpan maxBackOffTimeSpan, int retryNumber = 10)
        {
            _minimumBackOff = minBackOffTimeSpan;
            _defaultBackOff = defaulBackOffTimeSpan;
            _maxBackOff = maxBackOffTimeSpan;
            _retryNumber = retryNumber;
        }

        public async Task<T> RetryTask<T>(Func<Task<T>> funcTask)
        {
            T result = default(T);
            var num = 0;
            
            do
            {
                try
                {
                    num++;
                    result = await funcTask();
                    break;
                }
                catch (Exception exception)
                {
                    //Console.WriteLine(exception.ToString());
                    if (num <= _retryNumber)
                    {
                        TimeSpan delayTimeSpan = TimeSpan.FromMilliseconds(RetryMath.CalculateRetryBackoffMilliseconds(num, _defaultBackOff, _minimumBackOff, _maxBackOff));
                        await Task.Delay(delayTimeSpan);
                    }
                    else
                    {
                        throw;
                    }
                    
                }
            } while (num <= _retryNumber);

            return result;
        }

        public async Task RetryTask(Func<Task> funcTask)
        {
           
            var num = 0;
            
            do
            {
                try
                {
                    num++;
                    await funcTask();
                    break;
                }
                catch (Exception exception)
                {
                    //Console.WriteLine(exception.ToString());
                    if (num <= _retryNumber)
                    {
                        TimeSpan delayTimeSpan = TimeSpan.FromMilliseconds(RetryMath.CalculateRetryBackoffMilliseconds(num, _defaultBackOff, _minimumBackOff, _maxBackOff));
                        await Task.Delay(delayTimeSpan);
                    }
                    else
                    {
                        throw;
                    }
                }

            } while  (num <= _retryNumber);
           
        }

        public  T Retry<T>(Func<T> func)
        {
            T result = default(T);
            var num = 0;

            do
            {
                try
                {
                    num++;
                    result = func();
                    break;
                }
                catch (Exception)
                {
                    if (num <= _retryNumber)
                    {
                        TimeSpan delayTimeSpan = TimeSpan.FromMilliseconds(RetryMath.CalculateRetryBackoffMilliseconds(num, _defaultBackOff, _minimumBackOff, _maxBackOff));
                        Thread.Sleep(delayTimeSpan);
                    }
                    else
                    {
                        throw;
                    }

                }
            } while (num <= _retryNumber);

            return result;
        }
    }
}
