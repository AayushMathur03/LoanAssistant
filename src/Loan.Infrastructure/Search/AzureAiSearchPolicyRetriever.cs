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

    public AzureAiSearchPolicyRetriever(
        IConfiguration configuration,
        ILogger<AzureAiSearchPolicyRetriever> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IEnumerable<PolicySearchResultDto>> SearchPolicyAsync(
        string query,
        string? targetProductId = null,
        string? effectiveVersion = null,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var searchEndpoint = _configuration["AzureAISearch:Endpoint"];
        var searchApiKey = _configuration["AzureAISearch:ApiKey"];
        var indexName = _configuration["AzureAISearch:IndexName"] ?? "loan-policies-index";
        var openAiEndpoint = _configuration["AzureOpenAI:Endpoint"];
        var openAiApiKey = _configuration["AzureOpenAI:ApiKey"];
        var embeddingDeployment = _configuration["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-3-small";

        if (string.IsNullOrWhiteSpace(searchEndpoint) || string.IsNullOrWhiteSpace(searchApiKey) ||
            string.IsNullOrWhiteSpace(openAiEndpoint) || string.IsNullOrWhiteSpace(openAiApiKey))
        {
            throw new InvalidOperationException("Azure AI Search or Azure OpenAI configuration is incomplete. " +
                "Please configure 'AzureAISearch:Endpoint', 'ApiKey', 'AzureOpenAI:Endpoint', and 'ApiKey' via User Secrets.");
        }

        try
        {
            // 1. Generate query embedding vector
            var openAiUri = new Uri(openAiEndpoint);
            var openAiCredential = new ApiKeyCredential(openAiApiKey);
            var azureOpenAiClient = new AzureOpenAIClient(openAiUri, openAiCredential);
            var embeddingClient = azureOpenAiClient.GetEmbeddingClient(embeddingDeployment);

            _logger.LogInformation("Generating query embedding for search query: '{Query}'", query);
            OpenAIEmbedding queryEmbedding = await embeddingClient.GenerateEmbeddingAsync(query, cancellationToken: cancellationToken);
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
            
            SearchResults<PolicyIndexDocument> searchResults = await searchClient.SearchAsync<PolicyIndexDocument>(query, searchOptions, cancellationToken);

            var dtos = new List<PolicySearchResultDto>();
            await foreach (SearchResult<PolicyIndexDocument> result in searchResults.GetResultsAsync())
            {
                var doc = result.Document;
                double score = result.Score ?? 0.0;

                // Hybrid RRF score threshold: Relevant hybrid matches (text + vector) score >= ~0.02.
                // Pure vector noise with zero keyword match scores <= 0.0164.
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

            return dtos;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error executing Azure AI Search query.");
            throw new InvalidOperationException("Failed to retrieve policy documents from Azure AI Search.", ex);
        }
    }
}
