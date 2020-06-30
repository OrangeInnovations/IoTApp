using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Utilities
{
    public static class RetryMath
    {
        /// <summary>
        /// http://msdn.microsoft.com/en-us/library/azure/hh508997.aspx
        /// </summary>
        /// <param name="retries"></param>
        /// <param name="defaultBackoff"></param>
        /// <param name="minimumBackOff"></param>
        /// <param name="maximumBackoff"></param>
        public static double CalculateRetryBackoffMilliseconds(int retries, TimeSpan defaultBackoff, TimeSpan minimumBackOff, TimeSpan maximumBackoff)
        {
            //int retries = 1;

            // Initialize variables with default values
            //TimeSpan defaultBackoff = TimeSpan.FromSeconds(30);
            //TimeSpan backoffMin = TimeSpan.FromSeconds(3);
            //TimeSpan backoffMax = TimeSpan.FromSeconds(90);

            var random = new Random();

            double backoff = random.Next(
                (int)(0.8D * defaultBackoff.TotalMilliseconds),
                (int)(1.2D * defaultBackoff.TotalMilliseconds));

            backoff *= (Math.Pow(2, retries) - 1);

            backoff = Math.Min(
                minimumBackOff.TotalMilliseconds + backoff,
                maximumBackoff.TotalMilliseconds);
            return backoff;

        }
    }
}
