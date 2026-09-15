using System.Net;
using Loan.Application.Abstractions;
using Loan.Application.DTOs;
using Loan.Infrastructure.AzureOpenAI;
using Loan.Infrastructure.Resilience;
using Loan.Infrastructure.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Loan.EndToEndTests;

[TestFixture]
public class ResilienceTests
{
    [Test]
    public async Task ResiliencePolicy_RetriesOnTransientError_AndSucceedsWhenRecovered()
    {
        // Arrange
        var options = new ResiliencePolicyOptions
        {
            MaxRetries = 3,
            InitialBackoffMs = 10,
            CallTimeout = TimeSpan.FromSeconds(2)
        };
        var policy = new ResiliencePolicy(options);

        int attempts = 0;

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new HttpRequestException("429 Too Many Requests");
            }
            await Task.Yield();
            return "SuccessAfterRetry";
        });

        // Assert
        Assert.That(result, Is.EqualTo("SuccessAfterRetry"));
        Assert.That(attempts, Is.EqualTo(3));
        Assert.That(policy.State, Is.EqualTo(CircuitState.Closed));
    }

    [Test]
    public void ResiliencePolicy_CallerCancellation_DoesNotRetryAndThrowsImmediately()
    {
        // Arrange
        var options = new ResiliencePolicyOptions
        {
            MaxRetries = 3,
            InitialBackoffMs = 10,
            CallTimeout = TimeSpan.FromSeconds(5)
        };
        var policy = new ResiliencePolicy(options);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Caller explicit cancellation

        int attempts = 0;

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await policy.ExecuteAsync<string>(async ct =>
            {
                attempts++;
                ct.ThrowIfCancellationRequested();
                return await Task.FromResult("Result");
            }, cts.Token);
        });

        Assert.That(attempts, Is.LessThanOrEqualTo(1));
        Assert.That(policy.State, Is.EqualTo(CircuitState.Closed)); // Caller cancellation does not count as circuit failure
    }

    [Test]
    public async Task ResiliencePolicy_TransientTimeout_ExecutesBoundedRetries()
    {
        // Arrange
        var options = new ResiliencePolicyOptions
        {
            MaxRetries = 2,
            InitialBackoffMs = 5,
            CallTimeout = TimeSpan.FromMilliseconds(50) // Internal short call timeout
        };
        var policy = new ResiliencePolicy(options);

        int attempts = 0;

        // Act & Assert: Call that exceeds call timeout should fail with timeout/cancellation after max retries
        Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await policy.ExecuteAsync<string>(async ct =>
            {
                attempts++;
                await Task.Delay(200, ct); // Exceeds 50ms call timeout
                return "Delayed";
            });
        });

        Assert.That(attempts, Is.EqualTo(3)); // 1 initial + 2 retries = 3 total attempts
    }

    [Test]
    public async Task ResiliencePolicy_OpensCircuitBreaker_AfterConsecutiveFailures()
    {
        // Arrange
        var options = new ResiliencePolicyOptions
        {
            MaxRetries = 1,
            InitialBackoffMs = 5,
            ConsecutiveFailuresToOpenCircuit = 3,
            CircuitBreakDuration = TimeSpan.FromSeconds(10),
            CallTimeout = TimeSpan.FromSeconds(1)
        };
        var policy = new ResiliencePolicy(options);

        // Act: trigger 3 consecutive failures
        for (int i = 0; i < 3; i++)
        {
            Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await policy.ExecuteAsync<string>(ct => throw new HttpRequestException("503 Service Unavailable"));
            });
        }

        // Assert: Circuit state should now be OPEN
        Assert.That(policy.State, Is.EqualTo(CircuitState.Open));

        // Subsequent call should immediately throw CircuitBreakerOpenException
        var ex = Assert.ThrowsAsync<CircuitBreakerOpenException>(async () =>
        {
            await policy.ExecuteAsync<string>(ct => Task.FromResult("Should not run"));
        });

        Assert.That(ex!.Message, Does.Contain("Circuit breaker is OPEN"));
    }

    [Test]
    public async Task ResiliencePolicy_OpenAiAndSearchBreakers_AreIndependent()
    {
        // Arrange
        var openAiPolicy = new ResiliencePolicy(new ResiliencePolicyOptions { ConsecutiveFailuresToOpenCircuit = 2, MaxRetries = 0 });
        var searchPolicy = new ResiliencePolicy(new ResiliencePolicyOptions { ConsecutiveFailuresToOpenCircuit = 2, MaxRetries = 0 });

        // Trigger 2 failures on OpenAI policy
        for (int i = 0; i < 2; i++)
        {
            Assert.ThrowsAsync<HttpRequestException>(async () =>
                await openAiPolicy.ExecuteAsync<string>(ct => throw new HttpRequestException("500 Internal Error")));
        }

        // Assert: OpenAI breaker is OPEN, Search breaker remains CLOSED
        Assert.That(openAiPolicy.State, Is.EqualTo(CircuitState.Open));
        Assert.That(searchPolicy.State, Is.EqualTo(CircuitState.Closed));

        // Search policy can execute successfully
        var searchResult = await searchPolicy.ExecuteAsync(ct => Task.FromResult("SearchSuccess"));
        Assert.That(searchResult, Is.EqualTo("SearchSuccess"));
    }

    [Test]
    public async Task AzureOpenAIChatModel_DegradesGracefully_OnUnconfiguredOrOutage()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AzureOpenAI:Endpoint", "https://mock-openai.openai.azure.com/" },
                { "AzureOpenAI:ApiKey", "mock-key" },
                { "AzureOpenAI:DeploymentName", "mock-gpt4" }
            })
            .Build();

        var resiliencePolicy = new ResiliencePolicy(new ResiliencePolicyOptions
        {
            MaxRetries = 1,
            InitialBackoffMs = 5,
            CallTimeout = TimeSpan.FromMilliseconds(200)
        });

        var chatModel = new AzureOpenAIChatModel(config, resiliencePolicy: resiliencePolicy);
        var messages = new[] { new ChatMessage("user", "Hello") };

        // Act
        var result = await chatModel.GenerateCompletionAsync(messages);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("Degraded Service"));
    }

    [Test]
    public async Task AzureAiSearchPolicyRetriever_ReturnsSafeEmptyResults_OnOutageWithoutSyntheticFallback()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AzureAISearch:Endpoint", "https://mock-search.search.windows.net" },
                { "AzureAISearch:ApiKey", "mock-search-key" },
                { "AzureAISearch:IndexName", "loan-policies-index" },
                { "AzureOpenAI:Endpoint", "https://mock-openai.openai.azure.com/" },
                { "AzureOpenAI:ApiKey", "mock-openai-key" }
            })
            .Build();

        var resiliencePolicy = new ResiliencePolicy(new ResiliencePolicyOptions
        {
            MaxRetries = 1,
            InitialBackoffMs = 5,
            CallTimeout = TimeSpan.FromMilliseconds(200)
        });

        var retriever = new AzureAiSearchPolicyRetriever(
            config,
            NullLogger<AzureAiSearchPolicyRetriever>.Instance,
            resiliencePolicy: resiliencePolicy);

        // Act
        var results = await retriever.SearchPolicyAsync("Personal loan max limit");

        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results, Is.Empty); // Must return empty enumerable, NEVER fall back to synthetic policies
    }
}
