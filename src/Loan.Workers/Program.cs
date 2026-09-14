using Loan.Application.Abstractions;
using Loan.Infrastructure.Documents;
using Loan.Infrastructure.Search;
using Loan.Workers.Indexing;
using Loan.Workers.Processing;

var builder = Host.CreateApplicationBuilder(args);

// Register Infrastructure dependencies
builder.Services.AddSingleton<IDocumentExtractor, SyntheticDocumentExtractor>();
builder.Services.AddSingleton<IPolicyRetriever, SyntheticPolicyRetriever>();

// Register Hosted Services
builder.Services.AddHostedService<DocumentProcessingWorker>();
builder.Services.AddHostedService<PolicyIndexingWorker>();

var host = builder.Build();
host.Run();
