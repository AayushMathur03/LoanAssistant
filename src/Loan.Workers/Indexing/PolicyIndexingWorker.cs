using Loan.Infrastructure.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loan.Workers.Indexing;

public class PolicyIndexingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PolicyIndexingWorker> _logger;

    public PolicyIndexingWorker(IServiceProvider serviceProvider, ILogger<PolicyIndexingWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PolicyIndexingWorker started. Synchronizing versioned policy documents into Azure AI Search index...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var indexer = scope.ServiceProvider.GetService<PolicyIndexer>();
            if (indexer != null)
            {
                await indexer.SynchronizeIndexAndSeedAsync(cancellationToken: stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run initial policy index synchronization.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("PolicyIndexingWorker idle check...");
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
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
