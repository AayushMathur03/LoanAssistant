using System.ClientModel;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Loan.Application.Abstractions;
using Loan.Application.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace Loan.Infrastructure.Search;

public class AzureAiSearchPolicyRetriever : IPolicyRetriever
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureAiSearchPolicyRetriever> _logger;
    private readonly ITelemetryCollector? _telemetryCollector;
    private readonly Resilience.ResiliencePolicy _resiliencePolicy;

    public AzureAiSearchPolicyRetriever(
        IConfiguration configuration,
        ILogger<AzureAiSearchPolicyRetriever> logger,
        ITelemetryCollector? telemetryCollector = null,
        Resilience.ResiliencePolicy? resiliencePolicy = null)
    {
        _configuration = configuration;
        _logger = logger;
        _telemetryCollector = telemetryCollector;
        _resiliencePolicy = resiliencePolicy ?? new Resilience.ResiliencePolicy();
    }

    public async Task<IEnumerable<PolicySearchResultDto>> SearchPolicyAsync(
        string query,
        string? targetProductId = null,
        string? effectiveVersion = null,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var searchEndpoint = _configuration["AzureAISearch:Endpoint"];
        var searchApiKey = _configuration["AzureAISearch:ApiKey"];
        var indexName = _configuration["AzureAISearch:IndexName"] ?? "loan-policies-index";
        var openAiEndpoint = _configuration["AzureOpenAI:Endpoint"];
        var openAiApiKey = _configuration["AzureOpenAI:ApiKey"];
        var embeddingDeployment = _configuration["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-3-small";

        if (string.IsNullOrWhiteSpace(searchEndpoint) || string.IsNullOrWhiteSpace(searchApiKey) ||
            string.IsNullOrWhiteSpace(openAiEndpoint) || string.IsNullOrWhiteSpace(openAiApiKey))
        {
            _logger.LogWarning("Azure AI Search or Azure OpenAI configuration is incomplete. Returning safe empty policy results.");
            _telemetryCollector?.RecordError("SearchUnavailable");
            return Enumerable.Empty<PolicySearchResultDto>();
        }

        try
        {
            return await _resiliencePolicy.ExecuteAsync(async ct =>
            {
                // 1. Generate query embedding vector
                var openAiUri = new Uri(openAiEndpoint);
                var openAiCredential = new ApiKeyCredential(openAiApiKey);
                var azureOpenAiClient = new AzureOpenAIClient(openAiUri, openAiCredential);
                var embeddingClient = azureOpenAiClient.GetEmbeddingClient(embeddingDeployment);

                _logger.LogInformation("Generating query embedding for search query: '{Query}'", query);
                OpenAIEmbedding queryEmbedding = await embeddingClient.GenerateEmbeddingAsync(query, cancellationToken: ct);
                ReadOnlyMemory<float> queryVector = queryEmbedding.ToFloats();

                // 2. Perform Hybrid Search on Azure AI Search
                var searchUri = new Uri(searchEndpoint);
                var searchCredential = new AzureKeyCredential(searchApiKey);
                var searchClient = new SearchClient(searchUri, indexName, searchCredential);

                var searchOptions = new SearchOptions
                {
                    Size = topK,
                    VectorSearch = new VectorSearchOptions
                    {
                        Queries =
                        {
                            new VectorizedQuery(queryVector)
                            {
                                KNearestNeighborsCount = topK * 5,
                                Fields = { "contentVector" }
                            }
                        }
                    }
                };

                // Build filter string using standard Azure AI Search OData syntax
                var filters = new List<string>();
                if (!string.IsNullOrWhiteSpace(targetProductId))
                {
                    filters.Add($"search.in(productId, '{targetProductId}, ALL', ',')");
                }
                if (!string.IsNullOrWhiteSpace(effectiveVersion))
                {
                    filters.Add($"policyVersion eq '{effectiveVersion}'");
                }
                else
                {
                    filters.Add("isActive eq true");
                }

                if (filters.Count > 0)
                {
                    searchOptions.Filter = string.Join(" and ", filters);
                }

                _logger.LogInformation("Executing Hybrid Search on index '{Index}' with query: '{Query}', Filter: '{Filter}'", indexName, query, searchOptions.Filter);
                
                SearchResults<PolicyIndexDocument> searchResults = await searchClient.SearchAsync<PolicyIndexDocument>(query, searchOptions, ct);

                var dtos = new List<PolicySearchResultDto>();
                await foreach (SearchResult<PolicyIndexDocument> result in searchResults.GetResultsAsync())
                {
                    var doc = result.Document;
                    double score = result.Score ?? 0.0;

                    if (score < 0.0165)
                    {
                        continue;
                    }

                    dtos.Add(new PolicySearchResultDto(
                        DocumentId: doc.DocumentId,
                        Title: doc.Title,
                        Version: doc.PolicyVersion,
                        Section: doc.Section,
                        Content: doc.Content,
                        SimilarityScore: score,
                        Citation: new CitationDto(
                            DocumentTitle: doc.Title,
                            PolicyVersion: doc.PolicyVersion,
                            SectionOrPage: doc.Section,
                            Excerpt: doc.Excerpt)));
                }

                sw.Stop();
                _telemetryCollector?.RecordRagLatency(sw.Elapsed.TotalMilliseconds, dtos.Count);
                return dtos;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Azure AI Search query failed or circuit opened after resilience retries. Returning safe degraded empty policy results without synthetic fallback.");
            _telemetryCollector?.RecordError("SearchUnavailable");
            _telemetryCollector?.RecordRagLatency(sw.Elapsed.TotalMilliseconds, 0);
            return Enumerable.Empty<PolicySearchResultDto>();
        }
    }
}
