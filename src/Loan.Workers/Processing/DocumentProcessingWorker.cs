using Loan.Application.Abstractions;

namespace Loan.Workers.Processing;

public class DocumentProcessingWorker : BackgroundService
{
    private readonly IDocumentExtractor _documentExtractor;
    private readonly ILogger<DocumentProcessingWorker> _logger;

    public DocumentProcessingWorker(IDocumentExtractor documentExtractor, ILogger<DocumentProcessingWorker> logger)
    {
        _documentExtractor = documentExtractor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DocumentProcessingWorker started. Listening for background document intake events...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Simulate periodic background extraction job queue processing
                _logger.LogDebug("DocumentProcessingWorker polling queue...");
                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in DocumentProcessingWorker background loop.");
            }
        }

        _logger.LogInformation("DocumentProcessingWorker stopped.");
    }
}
