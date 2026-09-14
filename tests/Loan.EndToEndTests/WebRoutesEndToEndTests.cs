using Loan.Application.Abstractions;
using Loan.Application.ProductAdvice;
using Loan.Application.Recommendations;
using Loan.Application.Verification;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Products;
using Loan.Infrastructure.AzureOpenAI;
using Loan.Infrastructure.Documents;
using Loan.Infrastructure.MCP;
using Loan.Infrastructure.Persistence;
using Loan.Infrastructure.Search;
using Loan.Infrastructure.Verification;
using Loan.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Loan.EndToEndTests;

[TestFixture]
public class WebRoutesEndToEndTests
{
    private InMemoryLoanApplicationRepository _appRepo = null!;
    private InMemoryRecommendationRepository _recRepo = null!;
    private AskProductQuestionQueryHandler _qnaHandler = null!;
    private IDocumentExtractor _docExtractor = null!;
    private EvaluateEligibilityCommandHandler _evalHandler = null!;
    private GenerateRecommendationDraftCommandHandler _draftHandler = null!;
    private OfficerDecisionCommandHandler _decisionHandler = null!;
    private McpToolServer _mcpServer = null!;

    [SetUp]
    public async Task SetUp()
    {
        _appRepo = new InMemoryLoanApplicationRepository();
        _recRepo = new InMemoryRecommendationRepository();

        var identityService = new SyntheticIdentityService();
        var incomeService = new SyntheticIncomeService();
        var creditService = new SyntheticCreditService();
        var policyRetriever = new SyntheticPolicyRetriever();
        var chatModel = new SyntheticChatModel();

        _docExtractor = new SyntheticDocumentExtractor();
        _mcpServer = new McpToolServer(identityService, creditService, policyRetriever);

        _qnaHandler = new AskProductQuestionQueryHandler(policyRetriever, chatModel);
        _evalHandler = new EvaluateEligibilityCommandHandler(_appRepo, identityService, incomeService, creditService);
        _draftHandler = new GenerateRecommendationDraftCommandHandler(_appRepo, _recRepo);
        _decisionHandler = new OfficerDecisionCommandHandler(_appRepo);

        // Seed an application
        var rules = ProductRules.CreateStandardMortgage("v1.2");
        var facts = new ApplicantFacts("APP-100", "Alice Cooper", "SYN-888777", new Money(12000m), new Money(3000m), new Money(350000m), new Money(500000m), 750, "Full-Time", "Primary Residence");
        var app = new LoanApplication("APP-2026-001", "APP-100", rules, facts, DateTime.UtcNow);
        app.Submit(DateTime.UtcNow);
        await _appRepo.AddAsync(app);
        await _evalHandler.HandleAsync(new EvaluateEligibilityCommand("APP-2026-001"));
        await _draftHandler.HandleAsync(new GenerateRecommendationDraftCommand("APP-2026-001"));
    }

    [Test]
    public async Task ApplicantController_Index_ShouldReturnViewResultWithApplications()
    {
        var controller = new ApplicantController(_qnaHandler, _docExtractor, _appRepo, _evalHandler);
        var result = await controller.Index() as ViewResult;

        Assert.That(result, Is.Not.Null);
        var model = result!.Model as IEnumerable<LoanApplication>;
        Assert.That(model, Is.Not.Null);
        Assert.That(model!.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task OfficerController_IndexAndReview_ShouldReturnViewResults()
    {
        var controller = new OfficerController(_appRepo, _draftHandler, _decisionHandler);
        var indexResult = await controller.Index() as ViewResult;
        Assert.That(indexResult, Is.Not.Null);

        var reviewResult = await controller.Review("APP-2026-001") as ViewResult;
        Assert.That(reviewResult, Is.Not.Null);
        var app = reviewResult!.Model as LoanApplication;
        Assert.That(app, Is.Not.Null);
        Assert.That(app!.ApplicationId, Is.EqualTo("APP-2026-001"));
    }

    [Test]
    public async Task ComplianceController_Index_ShouldReturnViewResult()
    {
        var controller = new ComplianceController(_appRepo);
        var result = await controller.Index() as ViewResult;
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void AdminController_Index_ShouldReturnViewResultWithMcpTools()
    {
        var controller = new AdminController(_mcpServer);
        var result = controller.Index() as ViewResult;
        Assert.That(result, Is.Not.Null);
        var tools = result!.Model as IEnumerable<McpToolDefinition>;
        Assert.That(tools, Is.Not.Null);
        Assert.That(tools!.Count(), Is.EqualTo(3));
    }
}
