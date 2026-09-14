using Loan.Application.Abstractions;
using Loan.Infrastructure;
using Loan.Infrastructure.Documents;
using Loan.Workers.Indexing;
using Loan.Workers.Processing;

var builder = Host.CreateApplicationBuilder(args);

// Register Infrastructure dependencies
builder.Services.AddInfrastructurePersistence(builder.Configuration);
builder.Services.AddSingleton<IDocumentExtractor, SyntheticDocumentExtractor>();

// Register Hosted Services
builder.Services.AddHostedService<DocumentProcessingWorker>();
builder.Services.AddHostedService<PolicyIndexingWorker>();

var host = builder.Build();
host.Run();
