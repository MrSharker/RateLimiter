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

        public RateLimiter(Func<TArg, Task> action, List<IRateLimitRule> rateLimitRules, TimeSpan? maxWaitTime = null)
        {
            _action = action;
            _rateLimitRules = rateLimitRules;
            _maxWaitTime = maxWaitTime;
        }

        public async Task Perform(TArg arg)
        {
            var startTime = DateTime.UtcNow;
            while (true)
            {
                if (_maxWaitTime.HasValue && DateTime.UtcNow - startTime > _maxWaitTime.Value)
                {
                    throw new TimeoutException($"Exceeded maximum wait time {_maxWaitTime.Value.TotalSeconds:F2} seconds.");
                }
                var reservedRules = new List<IRateLimitRule>();
                try
                {
                    foreach (var rule in _rateLimitRules)
                    {
                        await rule.ReserveSlotAsync();
                        reservedRules.Add(rule);
                    }

                    await _action(arg);

                    return;
                }
                catch (RateLimitExceededException ex)
                {
                    foreach (var reservedRule in reservedRules)
                    {
                        await reservedRule.RollbackLastReservationAsync();
                    }
                    if (_maxWaitTime.HasValue && ex.RetryAfter > _maxWaitTime.Value)
                    {
                        throw new TimeoutException($"Exceeded maximum wait time {_maxWaitTime.Value.TotalSeconds:F2} seconds. A retry is possible after {ex.RetryAfter.TotalSeconds:F2} seconds.");
                    }

                    Console.WriteLine(ex.Message);
                    await Task.Delay(ex.RetryAfter);
                }
            }
        }
    }
}
