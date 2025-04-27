using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RateLimiter
{
    public class RateLimitExceededException : Exception
    {
        public TimeSpan RetryAfter { get; }

        public RateLimitExceededException(TimeSpan retryAfter)
            : base($"Rate limit exceeded. Retry after {retryAfter.TotalMilliseconds:F2} ms.")
        {
            RetryAfter = retryAfter;
        }
    }
}
