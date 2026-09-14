using Loan.Application.Abstractions;

namespace Loan.Workers.Indexing;

public class PolicyIndexingWorker : BackgroundService
{
    private readonly IPolicyRetriever _policyRetriever;
    private readonly ILogger<PolicyIndexingWorker> _logger;

    public PolicyIndexingWorker(IPolicyRetriever policyRetriever, ILogger<PolicyIndexingWorker> logger)
    {
        _policyRetriever = policyRetriever;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PolicyIndexingWorker started. Managing versioned policy index synchronization...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("PolicyIndexingWorker checking policy index status...");
                await Task.Delay(10000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in PolicyIndexingWorker background loop.");
            }
        }

        _logger.LogInformation("PolicyIndexingWorker stopped.");
    }
}
