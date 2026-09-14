using System.Text.Json.Serialization;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace Loan.Infrastructure.Search;

public class PolicyIndexDocument
{
    [SimpleField(IsKey = true, IsFilterable = true)]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true)]
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    [JsonPropertyName("policyVersion")]
    public string PolicyVersion { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    [JsonPropertyName("audience")]
    public string Audience { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    [JsonPropertyName("effectiveFrom")]
    public string EffectiveFrom { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    [JsonPropertyName("effectiveTo")]
    public string EffectiveTo { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [SearchableField]
    [JsonPropertyName("section")]
    public string Section { get; set; } = string.Empty;

    [SearchableField]
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [SearchableField]
    [JsonPropertyName("excerpt")]
    public string Excerpt { get; set; } = string.Empty;

    [VectorSearchField(VectorSearchDimensions = 1536, VectorSearchProfileName = "policy-vector-profile")]
    [JsonPropertyName("contentVector")]
    public float[]? ContentVector { get; set; }
}
