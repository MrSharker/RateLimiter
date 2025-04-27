using RateLimiter;

class Program
{
    static async Task Main(string[] args)
    {
        var rateLimiter = new RateLimiter<string>(
            async (arg) =>
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Performing action with arg: {arg}");
                await Task.Delay(100);
            },
            new List<IRateLimitRule>
            {
                    new SlidingWindowRateLimit(5, TimeSpan.FromSeconds(2)),
                    new SlidingWindowRateLimit(10, TimeSpan.FromSeconds(5)),
                    new SlidingWindowRateLimit(15, TimeSpan.FromDays(1))
            },
            TimeSpan.FromSeconds(50)
        );

        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            int capture = i;
            tasks.Add(Task.Run(async () => {
                try
                {
                    await rateLimiter.Perform($"Request {capture}");
                }
                catch (TimeoutException ex)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Request {capture} failed: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Request {capture} unexpected error: {ex.Message}");
                }
            }
            ));
        }

        await Task.WhenAll(tasks);
    }
}
