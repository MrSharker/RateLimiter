using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RateLimiter
{
    public class SlidingWindowRateLimit : IRateLimitRule
    {
        private readonly int _maxRequests;

        private readonly TimeSpan _timeWindow;

        private readonly Queue<DateTime> _timestamps = new();

        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public SlidingWindowRateLimit(int maxRequests, TimeSpan timeWindow)
        {
            _maxRequests = maxRequests;
            _timeWindow = timeWindow;
        }

        public async Task<TimeSpan?> CanReserveAfterAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                CleanupOldTimestamps();

                if (_timestamps.Count >= _maxRequests)
                {
                    var oldest = _timestamps.Peek();
                    var waitTime = (oldest + _timeWindow) - DateTime.UtcNow;
                    if (waitTime > TimeSpan.Zero)
                    {
                        return waitTime;
                    }
                }

                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task ReserveSlotAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                CleanupOldTimestamps();
                _timestamps.Enqueue(DateTime.UtcNow);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        #region private methods
        private void CleanupOldTimestamps()
        {
            var threshold = DateTime.UtcNow - _timeWindow;
            while (_timestamps.Count > 0 && _timestamps.Peek() < threshold)
            {
                _timestamps.Dequeue();
            }
        }
        #endregion
    }
}
