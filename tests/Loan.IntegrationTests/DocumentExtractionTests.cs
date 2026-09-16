using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Loan.Infrastructure.Documents;
using NUnit.Framework;

namespace Loan.IntegrationTests;

[TestFixture]
public class DocumentExtractionTests
{
    private SyntheticDocumentExtractor _extractor = null!;

    [SetUp]
    public void SetUp()
    {
        _extractor = new SyntheticDocumentExtractor();
    }

    [Test]
    public async Task ExtractFieldsAsync_Paystub_ShouldExtractIncomeAndMaskSsn()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Synthetic Paystub Content"));
        var result = await _extractor.ExtractFieldsAsync(stream, "applicant_paystub.pdf", "application/pdf");

        Assert.That(result.Success, Is.True);
        Assert.That(result.DocumentType, Is.EqualTo("Paystub"));
        Assert.That(result.ExtractedFields, Is.Not.Empty);

        var ssnField = result.ExtractedFields.FirstOrDefault(f => f.FieldName == "SSN");
        Assert.That(ssnField, Is.Not.Null);
        Assert.That(ssnField!.RawValue, Is.EqualTo("***-**-6789")); // Masked SSN check
    }

    [Test]
    public async Task ExtractFieldsAsync_LowConfidenceScan_ShouldFlagNeedsConfirmation()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Low confidence blurry document"));
        var result = await _extractor.ExtractFieldsAsync(stream, "paystub_blurry_scan.pdf", "application/pdf");

        Assert.That(result.Success, Is.True);
        Assert.That(result.ExtractedFields.Any(f => f.NeedsConfirmation), Is.True);
        Assert.That(result.ExtractedFields.Any(f => f.ConfidenceScore < 0.85f), Is.True);
    }

    [Test]
    public async Task ExtractFieldsAsync_DifferentStreamContents_ShouldProduceDifferentExtractedValues()
    {
        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("Gross Pay: 15000.00\nEmployer: Acme Corp"));
        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("Gross Pay: 8500.00\nEmployer: Beta Industries"));

        var result1 = await _extractor.ExtractFieldsAsync(stream1, "paystub.pdf", "application/pdf");
        var result2 = await _extractor.ExtractFieldsAsync(stream2, "paystub.pdf", "application/pdf");

        var income1 = result1.ExtractedFields.First(f => f.FieldName == "MonthlyGrossIncome").RawValue;
        var income2 = result2.ExtractedFields.First(f => f.FieldName == "MonthlyGrossIncome").RawValue;

        var employer1 = result1.ExtractedFields.First(f => f.FieldName == "EmployerName").RawValue;
        var employer2 = result2.ExtractedFields.First(f => f.FieldName == "EmployerName").RawValue;

        Assert.That(income1, Is.EqualTo("15000.00"));
        Assert.That(income2, Is.EqualTo("8500.00"));
        Assert.That(income1, Is.Not.EqualTo(income2));

        Assert.That(employer1, Is.EqualTo("Acme Corp"));
        Assert.That(employer2, Is.EqualTo("Beta Industries"));
        Assert.That(employer1, Is.Not.EqualTo(employer2));
    }

    [Test]
    public async Task AzureOpenAiDocumentExtractor_WhenLlmThrowsOrUnavailable_FallsBackToSyntheticGracefully()
    {
        var mockChat = new MockFailingChatModel();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AzureOpenAiDocumentExtractor>.Instance;
        var extractor = new AzureOpenAiDocumentExtractor(mockChat, _extractor, logger);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Gross Pay: 12000.00\nEmployer: Apex Financial"));
        var result = await extractor.ExtractFieldsAsync(stream, "applicant_paystub.pdf", "application/pdf");

        Assert.That(result.Success, Is.True);
        Assert.That(result.DocumentType, Is.EqualTo("Paystub"));
        Assert.That(result.ExtractedFields, Is.Not.Empty);
    }

    [Test]
    public async Task AzureOpenAiDocumentExtractor_WithStructuredLlmResponse_ParsesFieldsAndScoresCorrectly()
    {
        var jsonResponse = @"{
            ""documentType"": ""Paystub"",
            ""fields"": [
                {
                    ""fieldName"": ""MonthlyGrossIncome"",
                    ""rawValue"": ""14500.00"",
                    ""confidenceScore"": 0.98,
                    ""evidenceSnippet"": ""Monthly Gross Earnings: $14,500.00"",
                    ""isSensitive"": false
                },
                {
                    ""fieldName"": ""EmployerName"",
                    ""rawValue"": ""Quantum Tech Ltd"",
                    ""confidenceScore"": 0.95,
                    ""evidenceSnippet"": ""Employer: Quantum Tech Ltd"",
                    ""isSensitive"": false
                }
            ]
        }";

        var mockChat = new MockSuccessChatModel(jsonResponse);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AzureOpenAiDocumentExtractor>.Instance;
        var extractor = new AzureOpenAiDocumentExtractor(mockChat, _extractor, logger);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Any document payload"));
        var result = await extractor.ExtractFieldsAsync(stream, "QuantumTech_Paystub.pdf", "application/pdf");

        Assert.That(result.Success, Is.True);
        Assert.That(result.DocumentType, Is.EqualTo("Paystub"));
        Assert.That(result.ExtractedFields.Count, Is.EqualTo(2));

        var incField = result.ExtractedFields.First(f => f.FieldName == "MonthlyGrossIncome");
        Assert.That(incField.RawValue, Is.EqualTo("14500.00"));
        Assert.That(incField.ConfidenceScore, Is.EqualTo(0.98f));
        Assert.That(incField.NeedsConfirmation, Is.False);
    }

    private class MockFailingChatModel : Loan.Application.Abstractions.IChatModel
    {
        public Task<string> GenerateCompletionAsync(System.Collections.Generic.IEnumerable<Loan.Application.Abstractions.ChatMessage> messages, double temperature = 0.2, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new System.Net.Http.HttpRequestException("Simulated Azure OpenAI connection failure");
        }

        public IAsyncEnumerable<string> StreamCompletionAsync(System.Collections.Generic.IEnumerable<Loan.Application.Abstractions.ChatMessage> messages, double temperature = 0.2, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<T> GenerateStructuredAsync<T>(System.Collections.Generic.IEnumerable<Loan.Application.Abstractions.ChatMessage> messages, double temperature = 0.1, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }

    private class MockSuccessChatModel : Loan.Application.Abstractions.IChatModel
    {
        private readonly string _response;
        public MockSuccessChatModel(string response) => _response = response;

        public Task<string> GenerateCompletionAsync(System.Collections.Generic.IEnumerable<Loan.Application.Abstractions.ChatMessage> messages, double temperature = 0.2, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_response);
        }

        public IAsyncEnumerable<string> StreamCompletionAsync(System.Collections.Generic.IEnumerable<Loan.Application.Abstractions.ChatMessage> messages, double temperature = 0.2, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<T> GenerateStructuredAsync<T>(System.Collections.Generic.IEnumerable<Loan.Application.Abstractions.ChatMessage> messages, double temperature = 0.1, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}

