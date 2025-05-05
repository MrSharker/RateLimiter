using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RateLimiter
{
    public class RateLimiter<TArg>
    {
        private readonly Func<TArg, Task> _action;

        private readonly List<IRateLimitRule> _rateLimitRules;

        private readonly TimeSpan? _maxWaitTime;

        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public RateLimiter(Func<TArg, Task> action, List<IRateLimitRule> rateLimitRules, TimeSpan? maxWaitTime = null)
        {
            _action = action;
            _rateLimitRules = rateLimitRules;
            _maxWaitTime = maxWaitTime;
        }

        public async Task Perform(TArg arg)
        {
            var deadline = _maxWaitTime.HasValue ? DateTime.UtcNow + _maxWaitTime.Value : (DateTime?)null;
            while (true)
            {
                if (deadline.HasValue && DateTime.UtcNow > deadline.Value)
                {
                    throw new TimeoutException($"Exceeded maximum wait time of {_maxWaitTime.Value.TotalSeconds:F2} seconds.");
                }

                await _semaphore.WaitAsync();

                try
                {
                    var delays = new List<TimeSpan>();

                    foreach (var rule in _rateLimitRules)
                    {
                        var waitTime = await rule.CanReserveAfterAsync();
                        if (waitTime.HasValue)
                            delays.Add(waitTime.Value);
                    }
                    if (delays.Any())
                    {
                        var delay = delays.Max();

                        if (deadline.HasValue && DateTime.UtcNow + delay > deadline.Value)
                            throw new TimeoutException($"Exceeded maximum wait time {_maxWaitTime.Value.TotalSeconds:F2} seconds. A retry is possible after {delay.TotalSeconds:F2} seconds.");

                        Console.WriteLine($"Waiting for {delay.TotalSeconds:F2} seconds before retrying.");
                        await Task.Delay(delay);
                        continue;
                    }

                    foreach (var rule in _rateLimitRules)
                    {
                        await rule.ReserveSlotAsync();
                    }

                    await _action(arg);

                    return;
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }
    }
}
