using System.Reflection;
using System.Security.Claims;
using Loan.Application.Abstractions;
using Loan.Application.Documents;
using Loan.Application.ProductAdvice;
using Loan.Application.Recommendations;
using Loan.Application.Verification;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Documents;
using Loan.Domain.Products;
using Loan.Domain.Recommendations;
using Loan.Infrastructure.AzureOpenAI;
using Loan.Infrastructure.Documents;
using Loan.Infrastructure.Persistence;
using Loan.Infrastructure.Persistence.DbContext;
using Loan.Infrastructure.Search;
using Loan.Infrastructure.Verification;
using Loan.Web.Controllers;
using Loan.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Loan.EndToEndTests;

[TestFixture]
public class LocalProductVerificationTests
{
    private InMemoryLoanApplicationRepository _appRepo = null!;
    private InMemoryRecommendationRepository _recRepo = null!;
    private AskProductQuestionQueryHandler _qnaHandler = null!;
    private SyntheticDocumentExtractor _docExtractor = null!;
    private EvaluateEligibilityCommandHandler _evalHandler = null!;
    private GenerateRecommendationDraftCommandHandler _draftHandler = null!;
    private OfficerDecisionCommandHandler _decisionHandler = null!;
    private UploadAndExtractDocumentCommandHandler _uploadHandler = null!;
    private ConfirmOrOverrideExtractedFieldsCommandHandler _confirmHandler = null!;

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
        var storage = new LocalFileDocumentStorageService(NullLogger<LocalFileDocumentStorageService>.Instance);
        var saveDraftHandler = new SaveRecommendationDraftCommandHandler(_appRepo, _recRepo);

        _qnaHandler = new AskProductQuestionQueryHandler(policyRetriever, chatModel);
        _evalHandler = new EvaluateEligibilityCommandHandler(_appRepo, identityService, incomeService, creditService);
        _draftHandler = new GenerateRecommendationDraftCommandHandler(_appRepo, _recRepo);
        _decisionHandler = new OfficerDecisionCommandHandler(_appRepo);
        _uploadHandler = new UploadAndExtractDocumentCommandHandler(_appRepo, storage, _docExtractor);
        _confirmHandler = new ConfirmOrOverrideExtractedFieldsCommandHandler(_appRepo);

        // Seed Application 1: Alice Cooper
        var rules = ProductRules.CreateStandardMortgage("v1.2");
        var facts1 = new ApplicantFacts("APP-100", "Alice Cooper", "SYN-888777", new Money(12000m), new Money(3000m), new Money(350000m), new Money(500000m), 750, "Full-Time", "Primary Residence");
        var app1 = new LoanApplication("APP-2026-001", "APP-100", rules, facts1, DateTime.UtcNow);
        app1.Submit(DateTime.UtcNow);
        await _appRepo.AddAsync(app1);
        await _evalHandler.HandleAsync(new EvaluateEligibilityCommand("APP-2026-001"));
        await _draftHandler.HandleAsync(new GenerateRecommendationDraftCommand("APP-2026-001"));

        // Seed Application 2: Jane Smith
        var facts2 = new ApplicantFacts("APP-200", "Jane Smith", "SYN-999888", new Money(8000m), new Money(2000m), new Money(200000m), new Money(300000m), 710, "Full-Time", "Primary Residence");
        var app2 = new LoanApplication("APP-2026-002", "APP-200", rules, facts2, DateTime.UtcNow);
        app2.Submit(DateTime.UtcNow);
        await _appRepo.AddAsync(app2);
    }

    [Test]
    public void ControllerAuthorizationAttributes_EnforceCorrectPersonaRoles()
    {
        // 1. ApplicantController -> Applicant, Administrator
        var applicantAttr = typeof(ApplicantController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.That(applicantAttr, Is.Not.Null, "ApplicantController must have [Authorize]");
        Assert.That(applicantAttr!.Roles, Does.Contain("Applicant"));
        Assert.That(applicantAttr.Roles, Does.Contain("Administrator"));

        // 2. OfficerController -> LoanOfficer, Administrator
        var officerAttr = typeof(OfficerController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.That(officerAttr, Is.Not.Null, "OfficerController must have [Authorize]");
        Assert.That(officerAttr!.Roles, Does.Contain("LoanOfficer"));
        Assert.That(officerAttr.Roles, Does.Contain("Administrator"));

        // 3. ComplianceController -> ComplianceReviewer, Administrator
        var complianceAttr = typeof(ComplianceController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.That(complianceAttr, Is.Not.Null, "ComplianceController must have [Authorize]");
        Assert.That(complianceAttr!.Roles, Does.Contain("ComplianceReviewer"));
        Assert.That(complianceAttr.Roles, Does.Contain("Administrator"));

        // 4. AdminController -> Administrator
        var adminAttr = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.That(adminAttr, Is.Not.Null, "AdminController must have [Authorize]");
        Assert.That(adminAttr!.Roles, Is.EqualTo("Administrator"));

        // 5. AccountController Login -> AllowAnonymous
        var loginMethod = typeof(AccountController).GetMethod("Login", new[] { typeof(string) });
        var allowAnon = loginMethod?.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.That(allowAnon, Is.Not.Null, "AccountController.Login must allow anonymous access");
    }

    [Test]
    public async Task CrossTenantApplicantIsolation_PreventsViewingOtherApplicantsData()
    {
        var controller = new ApplicantController(_qnaHandler, _uploadHandler, _confirmHandler, _appRepo, _evalHandler);

        // Simulate logged in Alice Cooper with LinkedApplicationId = APP-2026-001
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "applicant@apex.local"),
            new Claim(ClaimTypes.Role, "Applicant"),
            new Claim("LinkedApplicationId", "APP-2026-001")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var result = await controller.Index() as ViewResult;
        Assert.That(result, Is.Not.Null);

        var model = result!.Model as ApplicantDashboardViewModel;
        Assert.That(model, Is.Not.Null);
        Assert.That(model!.ActiveApplication, Is.Not.Null);
        Assert.That(model.ActiveApplication!.ApplicationId, Is.EqualTo("APP-2026-001"));
        Assert.That(model.AllApplications.Count, Is.EqualTo(1), "Applicant must only see their own application");
        Assert.That(model.AllApplications[0].ApplicationId, Is.EqualTo("APP-2026-001"));
    }

    [Test]
    public async Task CrossTenantApplicantIsolation_BlocksTamperingWithOtherApplicationDraft()
    {
        var controller = new ApplicantController(_qnaHandler, _uploadHandler, _confirmHandler, _appRepo, _evalHandler);

        // Alice Cooper logged in
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "applicant@apex.local"),
            new Claim(ClaimTypes.Role, "Applicant"),
            new Claim("LinkedApplicationId", "APP-2026-001")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Try to save draft for Jane Smith (APP-2026-002)
        var result = await controller.SaveDraft("APP-2026-002", 500000m, "Primary Residence");
        Assert.That(result, Is.TypeOf<ForbidResult>(), "Applicant must be forbidden from updating another applicant's data");
    }

    [Test]
    public async Task OfficerDecisionSecurity_RejectsUnauthorizedNonOfficerDecisionSubmissions()
    {
        var chatModel = new SyntheticChatModel();
        var policyRetriever = new SyntheticPolicyRetriever();
        var docAgent = new Loan.Application.Agents.DocumentAnalysisAgent(chatModel);
        var eligAgent = new Loan.Application.Agents.EligibilityAnalysisAgent(chatModel);
        var compAgent = new Loan.Application.Agents.ComplianceReviewAgent(policyRetriever, chatModel);
        var saveDraft = new SaveRecommendationDraftCommandHandler(_appRepo, _recRepo);
        var orchestrator = new Loan.Application.Agents.RecommendationOrchestratorAgent(docAgent, eligAgent, compAgent, saveDraft, chatModel);

        var controller = new OfficerController(_appRepo, _draftHandler, _decisionHandler, orchestrator);

        // Attempt decision by Compliance Reviewer (read-only persona)
        var complianceUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "compliance@apex.local"),
            new Claim(ClaimTypes.Role, "ComplianceReviewer")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = complianceUser }
        };

        var result = await controller.SubmitDecision("APP-2026-001", RecommendationStatus.ApprovedByOfficer, "Compliance attempting approval", null);
        Assert.That(result, Is.TypeOf<ForbidResult>(), "Non-officer persona must be forbidden from executing loan decisions");
    }

    [Test]
    public async Task OfficerDecisionSecurity_AllowsLoanOfficerToApprove()
    {
        var chatModel = new SyntheticChatModel();
        var policyRetriever = new SyntheticPolicyRetriever();
        var docAgent = new Loan.Application.Agents.DocumentAnalysisAgent(chatModel);
        var eligAgent = new Loan.Application.Agents.EligibilityAnalysisAgent(chatModel);
        var compAgent = new Loan.Application.Agents.ComplianceReviewAgent(policyRetriever, chatModel);
        var saveDraft = new SaveRecommendationDraftCommandHandler(_appRepo, _recRepo);
        var orchestrator = new Loan.Application.Agents.RecommendationOrchestratorAgent(docAgent, eligAgent, compAgent, saveDraft, chatModel);

        var controller = new OfficerController(_appRepo, _draftHandler, _decisionHandler, orchestrator);

        // Authorized Loan Officer
        var officerUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "officer@apex.local"),
            new Claim(ClaimTypes.Role, "LoanOfficer")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = officerUser }
        };

        var result = await controller.SubmitDecision("APP-2026-001", RecommendationStatus.ApprovedByOfficer, "Meets all underwriting criteria", "OFFICER-007");
        Assert.That(result, Is.TypeOf<RedirectToActionResult>());

        var app = await _appRepo.GetByIdAsync("APP-2026-001");
        Assert.That(app!.Status, Is.EqualTo(ApplicationStatus.Approved));
    }

    [Test]
    public async Task LowConfidenceFieldConfirmation_AppliesUserConfirmationWithAuditProvenance()
    {
        // 1. Upload a document using the real upload handler
        var fileContent = System.Text.Encoding.UTF8.GetBytes("PAYSTUB: Alice Cooper, Gross Pay: $11,500.00, Employer: Tech Corp");
        using var stream = new MemoryStream(fileContent);

        var uploadCmd = new UploadAndExtractDocumentCommand("APP-2026-001", "paystub_october.pdf", "application/pdf", stream);
        var docRecord = await _uploadHandler.HandleAsync(uploadCmd);

        Assert.That(docRecord, Is.Not.Null);
        Assert.That(docRecord.Fields.Count, Is.GreaterThan(0));

        var targetField = docRecord.Fields.First();
        var originalValue = targetField.DisplayValue;

        // 2. Controller executes ConfirmField
        var controller = new ApplicantController(_qnaHandler, _uploadHandler, _confirmHandler, _appRepo, _evalHandler);
        var applicantUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "applicant@apex.local"),
            new Claim(ClaimTypes.Role, "Applicant"),
            new Claim("LinkedApplicationId", "APP-2026-001")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = applicantUser }
        };

        var confirmedVal = "12000.00";
        var confirmResult = await controller.ConfirmField("APP-2026-001", docRecord.DocumentId, targetField.FieldName, confirmedVal, "Confirmed from October paystub");
        Assert.That(confirmResult, Is.TypeOf<RedirectToActionResult>());

        // 3. Verify the field is now confirmed and updated on the aggregate
        var updatedApp = await _appRepo.GetByIdAsync("APP-2026-001");
        var updatedDoc = updatedApp!.Documents.First(d => d.DocumentId == docRecord.DocumentId);
        var confirmedField = updatedDoc.Fields.First(f => f.FieldName == targetField.FieldName);

        Assert.That(confirmedField.Status, Is.EqualTo(FieldConfirmationStatus.ConfirmedByApplicant), "Field must now be marked ConfirmedByApplicant");
        Assert.That(confirmedField.DisplayValue, Is.EqualTo(confirmedVal), "Field display value must match confirmed value");
    }

    [Test]
    public async Task AzureBlobDocumentStorageService_FallsBackToLocalFileStorageGracefully()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "AzureStorage:ConnectionString", "" } // Empty triggers fallback
        };

        IConfiguration config = new ConfigurationBuilder()
            .Add(new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource { InitialData = inMemorySettings })
            .Build();

        var blobStorage = new AzureBlobDocumentStorageService(
            config,
            NullLogger<AzureBlobDocumentStorageService>.Instance,
            NullLogger<LocalFileDocumentStorageService>.Instance);

        var content = System.Text.Encoding.UTF8.GetBytes("Test Document Content for Loan Assistant");
        using var stream = new MemoryStream(content);

        var result = await blobStorage.SaveDocumentAsync("APP-2026-001", "test-doc.pdf", "application/pdf", stream);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.StorageReference, Is.Not.Null.And.Not.Empty);

        var retrievedStream = await blobStorage.GetDocumentStreamAsync(result.StorageReference);
        Assert.That(retrievedStream, Is.Not.Null);

        using var ms = new MemoryStream();
        await retrievedStream!.CopyToAsync(ms);
        Assert.That(ms.ToArray(), Is.EqualTo(content));
    }

    [Test]
    public async Task ApplicantController_Index_SupportsSubTabNavigationAndSpecificApplicationSelection()
    {
        var controller = new ApplicantController(_qnaHandler, _uploadHandler, _confirmHandler, _appRepo, _evalHandler);
        var applicantUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "applicant@apex.local"),
            new Claim(ClaimTypes.Role, "Applicant"),
            new Claim("LinkedApplicationId", "APP-2026-001")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = applicantUser }
        };

        // 1. Browse Catalogue Tab
        var catalogResult = await controller.Index(tab: "catalogue");
        Assert.That(catalogResult, Is.TypeOf<ViewResult>());
        var catalogVm = ((ViewResult)catalogResult).Model as ApplicantDashboardViewModel;
        Assert.That(catalogVm, Is.Not.Null);
        Assert.That(catalogVm!.ActiveTab, Is.EqualTo("catalogue"));

        // 2. My Applications Tab with specific applicationId
        var appsResult = await controller.Index(tab: "applications", applicationId: "APP-2026-001");
        Assert.That(appsResult, Is.TypeOf<ViewResult>());
        var appsVm = ((ViewResult)appsResult).Model as ApplicantDashboardViewModel;
        Assert.That(appsVm, Is.Not.Null);
        Assert.That(appsVm!.ActiveTab, Is.EqualTo("applications"));
        Assert.That(appsVm.ActiveApplication, Is.Not.Null);
        Assert.That(appsVm.ActiveApplication!.ApplicationId, Is.EqualTo("APP-2026-001"));
    }

    [Test]
    public async Task ApplicantController_CreateApplication_SuccessfullySubmitsCustomAutoLoan()
    {
        var controller = new ApplicantController(_qnaHandler, _uploadHandler, _confirmHandler, _appRepo, _evalHandler, _draftHandler);
        var applicantUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "applicant@apex.local"),
            new Claim(ClaimTypes.Role, "Applicant"),
            new Claim("LinkedApplicationId", "APP-2026-001")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = applicantUser }
        };

        var input = new CreateApplicationInputModel
        {
            ProductType = "AutoLoan",
            RequestedAmount = 35000m,
            EstimatedPropertyValue = 42000m,
            TermMonths = 60,
            MonthlyGrossIncome = 7000m,
            MonthlyDebts = 1500m,
            CreditScore = 700,
            EmploymentStatus = "Full-Time",
            LoanPurpose = "Certified Pre-Owned Sedan"
        };

        var result = await controller.CreateApplication(input);
        Assert.That(result, Is.TypeOf<RedirectToActionResult>());
        var redirect = (RedirectToActionResult)result;
        Assert.That(redirect.ActionName, Is.EqualTo("Index"));
        Assert.That(redirect.RouteValues!["tab"], Is.EqualTo("applications"));

        var createdAppId = redirect.RouteValues["applicationId"]?.ToString();
        Assert.That(createdAppId, Is.Not.Null.And.Not.Empty);

        var savedApp = await _appRepo.GetByIdAsync(createdAppId!);
        Assert.That(savedApp, Is.Not.Null);
        Assert.That(savedApp!.ProductRules.ProductId, Is.EqualTo("LOAN-AUTO"));
        Assert.That(savedApp.Facts.RequestedLoanAmount.Amount, Is.EqualTo(35000m));
    }

    [Test]
    public async Task OfficerController_Index_QueriesAllApplicationsAndPopulatesCounts()
    {
        var chatModel = new SyntheticChatModel();
        var docAgent = new Loan.Application.Agents.DocumentAnalysisAgent(chatModel);
        var eligAgent = new Loan.Application.Agents.EligibilityAnalysisAgent(chatModel);
        var compAgent = new Loan.Application.Agents.ComplianceReviewAgent(new SyntheticPolicyRetriever(), chatModel);
        var saveDraft = new SaveRecommendationDraftCommandHandler(_appRepo, _recRepo);
        var orchestrator = new Loan.Application.Agents.RecommendationOrchestratorAgent(docAgent, eligAgent, compAgent, saveDraft, chatModel);

        var controller = new OfficerController(_appRepo, _draftHandler, _decisionHandler, orchestrator);
        var officerUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "officer@apex.local"),
            new Claim(ClaimTypes.Role, "LoanOfficer")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = officerUser }
        };

        var result = await controller.Index();
        Assert.That(result, Is.TypeOf<ViewResult>());
        var vm = ((ViewResult)result).Model as OfficerDashboardViewModel;
        Assert.That(vm, Is.Not.Null);
        Assert.That(vm!.TotalApplications, Is.GreaterThanOrEqualTo(2));
    }
}
