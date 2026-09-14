using Loan.Application.ProductAdvice;
using Loan.Infrastructure.AzureOpenAI;
using Loan.Infrastructure.Search;

namespace Loan.PromptTests;

[TestFixture]
public class PromptEvaluationTests
{
    private AskProductQuestionQueryHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var retriever = new SyntheticPolicyRetriever();
        var chatModel = new SyntheticChatModel();
        _handler = new AskProductQuestionQueryHandler(retriever, chatModel);
    }

    [TestCaseSource(typeof(PromptEvaluationDataset), nameof(PromptEvaluationDataset.GetTestCases))]
    public async Task EvaluatePromptTestCase(PromptTestCase testCase)
    {
        var query = new AskProductQuestionQuery(testCase.UserPrompt);
        var response = await _handler.HandleAsync(query);

        Assert.That(response.Answer, Does.Contain(testCase.ExpectedKeyword).IgnoreCase,
            $"Test Case {testCase.CaseId} ({testCase.Category}): Expected answer to contain '{testCase.ExpectedKeyword}'.");

        if (testCase.ShouldHaveCitations)
        {
            Assert.That(response.Citations, Is.Not.Empty,
                $"Test Case {testCase.CaseId}: Expected response to contain policy citations.");
        }

        if (testCase.ShouldIncludeDisclaimer)
        {
            Assert.That(response.NonApprovalDisclaimer, Is.Not.Empty,
                $"Test Case {testCase.CaseId}: Expected non-approval disclaimer.");
        }
    }
}
