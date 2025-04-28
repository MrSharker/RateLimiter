using System.Diagnostics;
using Xunit;

namespace RateLimiter.Tests
{
    public class RateLimiterTests
    {
        [Fact]
        public async Task SingleCall_ShouldExecuteImmediately()
        {
            // Arrange
            bool called = false;
            var limiter = new RateLimiter<string>(
                async arg =>
                {
                    called = true;
                    await Task.CompletedTask;
                },
                new List<IRateLimitRule>
                {
                    new SlidingWindowRateLimit(10, TimeSpan.FromSeconds(1))
                });

            // Act
            await limiter.Perform("test");

            // Assert
            Assert.True(called);
        }

        [Fact]
        public async Task ExceedLimit_ShouldDelayExecution()
        {
            // Arrange
            var limiter = new RateLimiter<string>(
                async arg => await Task.CompletedTask,
                new List<IRateLimitRule>
                {
                    new SlidingWindowRateLimit(1, TimeSpan.FromSeconds(1))
                });

            // Act
            var stopwatch = Stopwatch.StartNew();
            await limiter.Perform("first");
            await limiter.Perform("second");

            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.ElapsedMilliseconds >= 900, $"Elapsed: {stopwatch.ElapsedMilliseconds:F2}ms");
        }

        [Fact]
        public async Task MultipleLimits_ShouldHonorAllLimits()
        {
            // Arrange
            var limiter = new RateLimiter<string>(
                async arg => await Task.CompletedTask,
                new List<IRateLimitRule>
                {
                    new SlidingWindowRateLimit(2, TimeSpan.FromSeconds(2)),
                    new SlidingWindowRateLimit(5, TimeSpan.FromSeconds(5))
                });

            // Act
            await limiter.Perform("1");
            await limiter.Perform("2");
            var stopwatch = Stopwatch.StartNew();
            await limiter.Perform("3");
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.ElapsedMilliseconds >= 900, $"Elapsed: {stopwatch.ElapsedMilliseconds:F2}ms");
        }

        [Fact]
        public async Task ConcurrentCalls_ShouldBeHandledCorrectly()
        {
            // Arrange
            var limiter = new RateLimiter<int>(
                async arg => await Task.Delay(50),
                new List<IRateLimitRule>
                {
                    new SlidingWindowRateLimit(3, TimeSpan.FromSeconds(1))
                });

            // Act
            var tasks = new List<Task>();
            for (int i = 0; i < 6; i++)
            {
                tasks.Add(limiter.Perform(i));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.True(true);
        }

        [Fact]
        public async Task StressTest_ManyParallelRequests_ShouldWorkCorrectly()
        {
            // Arrange
            int totalRequests = 1000;
            int maxRequestsPerSecond = 50;

            var limiter = new RateLimiter<int>(
                async arg => await Task.Delay(5),
                new List<IRateLimitRule>
                {
                    new SlidingWindowRateLimit(maxRequestsPerSecond, TimeSpan.FromSeconds(1))
                });

            var tasks = new List<Task>();
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < totalRequests; i++)
            {
                int capture = i;
                tasks.Add(Task.Run(() => limiter.Perform(capture)));
            }

            await Task.WhenAll(tasks);

            stopwatch.Stop();

            // Assert
            Console.WriteLine($"All {totalRequests} tasks completed in {stopwatch.ElapsedMilliseconds:F2}ms.");

            Assert.True(stopwatch.Elapsed.TotalSeconds >= 19, $"Expected ~20s, but was {stopwatch.Elapsed.TotalSeconds:F2}s");
        }

        [Fact]
        public async Task Perform_ShouldRespectRateLimit()
        {
            // Arrange
            int executedCount = 0;
            var limiter = new RateLimiter<string>(
                async arg =>
                {
                    executedCount++;
                    await Task.CompletedTask;
                },
                new List<IRateLimitRule>
                {
                    new SlidingWindowRateLimit(2, TimeSpan.FromSeconds(2))
                },
                maxWaitTime: TimeSpan.FromSeconds(10)
            );

            var stopwatch = Stopwatch.StartNew();

            // Act
            var tasks = new List<Task>();

            for (int i = 0; i < 5; i++)
            {
                int capture = i;
                tasks.Add(Task.Run(() => limiter.Perform($"Request {capture}")));
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            Assert.Equal(5, executedCount);
            Assert.True(stopwatch.Elapsed.TotalSeconds >= 4, $"Elapsed was {stopwatch.Elapsed.TotalSeconds:F2} seconds, expected at least ~4 seconds.");
        }

        [Fact]
        public async Task Perform_ShouldThrowTimeoutException_WhenMaxWaitTimeExceeded()
        {
            // Arrange
            var limiter = new RateLimiter<string>(
                async arg => await Task.CompletedTask,
                new List<IRateLimitRule>
                {
                    new SlidingWindowRateLimit(1, TimeSpan.FromDays(1))
                },
                maxWaitTime: TimeSpan.FromSeconds(2)
            );

            // Act
            await limiter.Perform("First Request");

            var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await limiter.Perform("Second Request");
            });

            // Assert
            Assert.Contains("Exceeded maximum wait time", exception.Message);
        }

        [Fact]
        public async Task Perform_ShouldRollback_WhenReservationFails()
        {
            // Arrange
            var goodRule = new FakeGoodRule();
            var badRule = new FakeBadRule();

            var rateLimiter = new RateLimiter<string>(
                async arg => await Task.CompletedTask,
                new List<IRateLimitRule> { goodRule, badRule },
                TimeSpan.FromSeconds(2)
            );

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() => rateLimiter.Perform("TestRequest"));

            Assert.Equal(goodRule.ReserveCalled, goodRule.RollbackCalled);
        }

        #region private
        private class FakeGoodRule : IRateLimitRule
        {
            public int ReserveCalled { get; private set; }
            public int RollbackCalled { get; private set; }

            public Task ReserveSlotAsync()
            {
                ReserveCalled++;
                return Task.CompletedTask;
            }

            public Task RollbackLastReservationAsync()
            {
                RollbackCalled++;
                return Task.CompletedTask;
            }
        }

        private class FakeBadRule : IRateLimitRule
        {
            public Task ReserveSlotAsync()
            {
                throw new RateLimitExceededException(TimeSpan.FromMilliseconds(100));
            }

            public Task RollbackLastReservationAsync()
            {
                return Task.CompletedTask;
            }
        }
        #endregion
    }
}