using System.ClientModel;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace Loan.Infrastructure.Search;

public class PolicyIndexer
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PolicyIndexer> _logger;

    public PolicyIndexer(IConfiguration configuration, ILogger<PolicyIndexer> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SynchronizeIndexAndSeedAsync(string? seedDirectoryPath = null, CancellationToken cancellationToken = default)
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
            _logger.LogWarning("Azure AI Search or Azure OpenAI configuration is missing. Skipping policy index synchronization.");
            return;
        }

        try
        {
            var searchUri = new Uri(searchEndpoint);
            var searchCredential = new AzureKeyCredential(searchApiKey);
            var indexClient = new SearchIndexClient(searchUri, searchCredential);

            // Ensure Search Index schema exists with HNSW Vector configuration
            await EnsureIndexExistsAsync(indexClient, indexName, cancellationToken);

            // Locate seed policy files
            var dirPath = seedDirectoryPath;
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath))
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && (!Directory.Exists(Path.Combine(dir.FullName, "src")) || (!File.Exists(Path.Combine(dir.FullName, "LoanAssistant.slnx")) && !File.Exists(Path.Combine(dir.FullName, "LoanAssistant.sln")))))
                {
                    dir = dir.Parent;
                }

                if (dir != null)
                {
                    dirPath = Path.Combine(dir.FullName, "src", "Loan.Infrastructure", "Search", "SeedPolicies");
                }
                else
                {
                    dirPath = Path.Combine(AppContext.BaseDirectory, "Search", "SeedPolicies");
                }
            }

            if (!Directory.Exists(dirPath))
            {
                _logger.LogWarning("Seed policies directory not found at {Path}. Skipping document indexing.", dirPath);
                return;
            }

            var mdFiles = Directory.GetFiles(dirPath, "*.md");
            if (mdFiles.Length == 0)
            {
                _logger.LogWarning("No markdown seed files found in {Path}.", dirPath);
                return;
            }

            _logger.LogInformation("Found {Count} policy files for indexing in {Path}.", mdFiles.Length, dirPath);

            var openAiUri = new Uri(openAiEndpoint);
            var openAiCredential = new ApiKeyCredential(openAiApiKey);
            var azureOpenAiClient = new AzureOpenAIClient(openAiUri, openAiCredential);
            var embeddingClient = azureOpenAiClient.GetEmbeddingClient(embeddingDeployment);

            var documentsToIndex = new List<PolicyIndexDocument>();

            foreach (var filePath in mdFiles)
            {
                if (Path.GetFileName(filePath).Equals("README.md", StringComparison.OrdinalIgnoreCase))
                    continue;

                var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                var (metadata, sections) = ParsePolicyMarkdown(content, Path.GetFileName(filePath));

                foreach (var section in sections)
                {
                    if (string.IsNullOrWhiteSpace(section.Content))
                        continue;

                    var docId = SanitizeKey($"{metadata.DocumentId}-{section.SectionHeader}");
                    var textToEmbed = $"{metadata.Title} - {section.SectionHeader}\n{section.Content}";

                    _logger.LogInformation("Generating embedding for policy chunk: {DocId} ({Title}, Ver: {Ver})", docId, metadata.Title, metadata.PolicyVersion);
                    OpenAIEmbedding embeddingResult = await embeddingClient.GenerateEmbeddingAsync(textToEmbed, cancellationToken: cancellationToken);
                    ReadOnlyMemory<float> vector = embeddingResult.ToFloats();

                    bool isActive = IsPolicyActive(metadata.EffectiveFrom, metadata.EffectiveTo);

                    var indexDoc = new PolicyIndexDocument
                    {
                        Id = docId,
                        DocumentId = metadata.DocumentId,
                        Title = metadata.Title,
                        ProductId = metadata.ProductId,
                        PolicyVersion = metadata.PolicyVersion,
                        DocumentType = metadata.DocumentType,
                        Audience = metadata.Audience,
                        EffectiveFrom = metadata.EffectiveFrom,
                        EffectiveTo = metadata.EffectiveTo,
                        IsActive = isActive,
                        Section = section.SectionHeader,
                        Content = section.Content,
                        Excerpt = section.Excerpt,
                        ContentVector = vector.ToArray()
                    };

                    documentsToIndex.Add(indexDoc);
                }
            }

            if (documentsToIndex.Count > 0)
            {
                var searchClient = new SearchClient(searchUri, indexName, searchCredential);
                _logger.LogInformation("Indexing (MergeOrUpload) {Count} policy chunks into Azure AI Search index '{Index}'...", documentsToIndex.Count, indexName);
                var batchResult = await searchClient.MergeOrUploadDocumentsAsync(documentsToIndex, cancellationToken: cancellationToken);
                
                int successCount = batchResult.Value.Results.Count(r => r.Succeeded);
                int failCount = batchResult.Value.Results.Count(r => !r.Succeeded);
                if (failCount > 0)
                {
                    var firstError = batchResult.Value.Results.FirstOrDefault(r => !r.Succeeded);
                    _logger.LogError("Document indexing encountered {FailCount} failures. First error: {Error}", failCount, firstError?.ErrorMessage);
                    throw new InvalidOperationException($"Azure AI Search document indexing failed for {failCount} documents. Error: {firstError?.ErrorMessage}");
                }

                _logger.LogInformation("Successfully indexed {Count} policy document chunks.", successCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to synchronize policy index in Azure AI Search.");
            throw;
        }
    }

    private static async Task EnsureIndexExistsAsync(SearchIndexClient indexClient, string indexName, CancellationToken cancellationToken)
    {
        var vectorSearchProfileName = "policy-vector-profile";
        var hnswConfigName = "policy-hnsw-config";

        var definition = new SearchIndex(indexName)
        {
            Fields = new FieldBuilder().Build(typeof(PolicyIndexDocument)),
            VectorSearch = new VectorSearch
            {
                Algorithms =
                {
                    new HnswAlgorithmConfiguration(hnswConfigName)
                    {
                        Parameters = new HnswParameters
                        {
                            Metric = VectorSearchAlgorithmMetric.Cosine,
                            M = 4,
                            EfConstruction = 400,
                            EfSearch = 500
                        }
                    }
                },
                Profiles =
                {
                    new VectorSearchProfile(vectorSearchProfileName, hnswConfigName)
                }
            }
        };

        try
        {
            await indexClient.GetIndexAsync(indexName, cancellationToken);
            await indexClient.CreateOrUpdateIndexAsync(definition, cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404 || ex.ErrorCode == "OperationNotAllowed" || ex.Message.Contains("vector field"))
        {
            try
            {
                await indexClient.DeleteIndexAsync(indexName, cancellationToken: cancellationToken);
            }
            catch (RequestFailedException) { }

            await indexClient.CreateIndexAsync(definition, cancellationToken: cancellationToken);
        }
    }

    private static bool IsPolicyActive(string effectiveFrom, string effectiveTo)
    {
        var currentDate = DateTime.UtcNow;

        if (DateTime.TryParse(effectiveFrom, out var fromDate) && currentDate < fromDate)
        {
            return false;
        }

        if (effectiveTo.Equals("Active", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(effectiveTo))
        {
            return true;
        }

        if (DateTime.TryParse(effectiveTo, out var toDate))
        {
            return currentDate <= toDate;
        }

        return true;
    }

    private static string SanitizeKey(string key)
    {
        var sanitized = Regex.Replace(key, @"[^a-zA-Z0-9_\-=]", "-");
        if (sanitized.Length > 128)
            sanitized = sanitized.Substring(0, 128);
        return sanitized;
    }

    private record ParsedMetadata(
        string DocumentId,
        string Title,
        string ProductId,
        string PolicyVersion,
        string DocumentType,
        string Audience,
        string EffectiveFrom,
        string EffectiveTo);

    private record ParsedSection(
        string SectionHeader,
        string Content,
        string Excerpt);

    private static (ParsedMetadata Metadata, List<ParsedSection> Sections) ParsePolicyMarkdown(string markdownText, string fileName)
    {
        var documentId = "DOC-UNKNOWN";
        var title = Path.GetFileNameWithoutExtension(fileName);
        var productId = "ALL";
        var policyVersion = "v1.0";
        var documentType = "Policy";
        var audience = "Internal";
        var effectiveFrom = "2026-01-01";
        var effectiveTo = "Active";

        var sanitizedText = markdownText.TrimStart('\uFEFF', '\u200B', ' ', '\r', '\n');
        string bodyText = sanitizedText;

        if (sanitizedText.StartsWith("---"))
        {
            int closingIndex = sanitizedText.IndexOf("---", 3, StringComparison.Ordinal);
            if (closingIndex != -1)
            {
                var frontmatterText = sanitizedText.Substring(3, closingIndex - 3);
                bodyText = sanitizedText.Substring(closingIndex + 3).TrimStart('\r', '\n');

                var frontmatterLines = frontmatterText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in frontmatterLines)
                {
                    if (line.Contains(':'))
                    {
                        var parts = line.Split(':', 2);
                        var key = parts[0].Trim();
                        var val = parts[1].Trim();

                        switch (key)
                        {
                            case "DocumentId": documentId = val; break;
                            case "Title": title = val; break;
                            case "ProductId": productId = val; break;
                            case "PolicyVersion": policyVersion = val; break;
                            case "DocumentType": documentType = val; break;
                            case "Audience": audience = val; break;
                            case "EffectiveFrom": effectiveFrom = val; break;
                            case "EffectiveTo": effectiveTo = val; break;
                        }
                    }
                }
            }
        }

        var metadata = new ParsedMetadata(documentId, title, productId, policyVersion, documentType, audience, effectiveFrom, effectiveTo);
        var sections = new List<ParsedSection>();

        var lines = bodyText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        string currentHeader = "Overview";
        var currentBody = new List<string>();

        foreach (var line in lines)
        {
            if (line.StartsWith("## ") || line.StartsWith("### "))
            {
                if (currentBody.Count > 0)
                {
                    var sectionText = string.Join("\n", currentBody).Trim();
                    if (!string.IsNullOrWhiteSpace(sectionText))
                    {
                        var excerpt = sectionText.Length > 150 ? sectionText.Substring(0, 147) + "..." : sectionText;
                        sections.Add(new ParsedSection(currentHeader, sectionText, excerpt));
                    }
                    currentBody.Clear();
                }

                currentHeader = line.TrimStart('#').Trim();
            }
            else
            {
                currentBody.Add(line);
            }
        }

        if (currentBody.Count > 0)
        {
            var sectionText = string.Join("\n", currentBody).Trim();
            if (!string.IsNullOrWhiteSpace(sectionText))
            {
                var excerpt = sectionText.Length > 150 ? sectionText.Substring(0, 147) + "..." : sectionText;
                sections.Add(new ParsedSection(currentHeader, sectionText, excerpt));
            }
        }

        return (metadata, sections);
    }
}
