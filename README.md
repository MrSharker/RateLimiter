# RateLimiter

## Description

`RateLimiter` is implemented with support for multiple rate limits based on the **Sliding Window** approach. The solution is designed to manage calls to asynchronous operations taking into account specified limits and provides protection against request rate overruns. 

Full asynchronous and thread-safe implementation was implemented, as required by the task. Moreover, during the development process, a maximum wait time limit (**`maxWaitTime`**) was additionally implemented to avoid infinite waiting when operations cannot be performed.

---

## Architecture

### RateLimiter
- Uses `SemaphoreSlim` to protect from races during concurrent access.
- Checks if slots are available
- Reserves slots for all rules rate limites before executing the action.
- If the limit is exceeded, waits the required time, or throws `TimeoutException` if the wait exceeds `maxWaitTime`.
- **`maxWaitTime`** limits the total wait time, preventing tasks from hanging in case of a long block.

### IRateLimitRule
- Interface for implementing the **Strategy Pattern**

### SlidingWindowRateLimit
- Rate limiting based on a **sliding window** approach.
- Uses `SemaphoreSlim` to protect the queue from races during concurrent access.


---

## Comparison of approaches

### Sliding Window

### Pros
- Smooth compliance with restrictions.
- No effect from sudden increase in requests.

### Cons
- Requires storage and regular cleaning of time stamps.

### Absolute

### Pros
- Lightness of implementation and verification.
- Clear interpretation of the limit for the user.

### Cons
- Possibility of reaching the limit at the beginning of the interval.
- Less flexibility for load distribution.

In conclusion, from my point of view, the Sliding Window approach is the favorite, as it allows for a smoother load distribution and does not allow all resources to be used up at the transition of limit renewals.

---

## Unit tests (xUnit)
Unit tests have been developed that cover the following cases:
- Checking the correct execution of operations while observing limits.
- Checking the wait between operations is correct when limits are exceeded.
- Checking the throw of `TimeoutException` if the wait exceeds `maxWaitTime`.
- Added a stress test that involves executing a large number of parallel requests at the same time(1000 tasks) (~20 sec compilation)

---
