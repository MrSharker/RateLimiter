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

        private readonly List<DateTime> _timestamps = new();

        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public SlidingWindowRateLimit(int maxRequests, TimeSpan timeWindow)
        {
            _maxRequests = maxRequests;
            _timeWindow = timeWindow;
        }

        public async Task ReserveSlotAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                CleanupOldTimestamps();

                if (_timestamps.Count >= _maxRequests)
                {
                    var oldest = _timestamps.Last();
                    var waitTime = (oldest + _timeWindow) - DateTime.UtcNow;
                    if (waitTime > TimeSpan.Zero)
                    {
                        throw new RateLimitExceededException(waitTime);
                    }
                }

                _timestamps.Add(DateTime.UtcNow);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task RollbackLastReservationAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_timestamps.Count > 0)
                {
                    _timestamps.RemoveAt(_timestamps.Count - 1);
                }
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
            _timestamps.RemoveAll(t => t < threshold);
        }
        #endregion
    }
}
