namespace Loan.Infrastructure.Resilience;

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}

public class ResiliencePolicyOptions
{
    public int MaxRetries { get; set; } = 3;
    public int InitialBackoffMs { get; set; } = 100;
    public int ConsecutiveFailuresToOpenCircuit { get; set; } = 5;
    public TimeSpan CircuitBreakDuration { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan CallTimeout { get; set; } = TimeSpan.FromSeconds(10);
}

public class ResiliencePolicy
{
    private readonly ResiliencePolicyOptions _options;
    private readonly Random _random = new();
    private int _consecutiveFailures;
    private DateTime _circuitOpenedTime = DateTime.MinValue;
    private CircuitState _state = CircuitState.Closed;
    private readonly object _lock = new();

    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                if (_state == CircuitState.Open && DateTime.UtcNow - _circuitOpenedTime > _options.CircuitBreakDuration)
                {
                    _state = CircuitState.HalfOpen;
                }
                return _state;
            }
        }
    }

    public ResiliencePolicy(ResiliencePolicyOptions? options = null)
    {
        _options = options ?? new ResiliencePolicyOptions();
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        if (State == CircuitState.Open)
        {
            throw new CircuitBreakerOpenException("Circuit breaker is OPEN due to consecutive transient failures.");
        }

        int attempt = 0;
        while (true)
        {
            attempt++;

            // Check if caller token was already cancelled before starting attempt
            cancellationToken.ThrowIfCancellationRequested();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.CallTimeout);

            try
            {
                var result = await action(cts.Token);
                OnSuccess();
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Explicit caller cancellation - DO NOT retry, DO NOT record circuit breaker failure
                throw;
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt <= _options.MaxRetries)
            {
                var backoffMs = _options.InitialBackoffMs * (int)Math.Pow(2, attempt - 1);
                var jitterMs = _random.Next(0, 50);
                await Task.Delay(backoffMs + jitterMs, cancellationToken);
            }
            catch (Exception)
            {
                OnFailure();
                throw;
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _consecutiveFailures = 0;
            _state = CircuitState.Closed;
            _circuitOpenedTime = DateTime.MinValue;
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _consecutiveFailures = 0;
            _state = CircuitState.Closed;
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= _options.ConsecutiveFailuresToOpenCircuit)
            {
                _state = CircuitState.Open;
                _circuitOpenedTime = DateTime.UtcNow;
            }
        }
    }

    public static bool IsTransientException(Exception ex)
    {
        if (ex is OperationCanceledException || ex is TimeoutException) return true;
        if (ex is System.Net.Http.HttpRequestException) return true;

        var msg = ex.Message.ToLowerInvariant();
        if (msg.Contains("429") || msg.Contains("rate limit") || msg.Contains("timeout") || msg.Contains("503") || msg.Contains("500") || msg.Contains("502"))
        {
            return true;
        }

        return ex.InnerException != null && IsTransientException(ex.InnerException);
    }
}
