# Implementation Plan: Production Loan Application & Compliance Review Assistant

Transform the prototype Loan Assistant into an enterprise-grade solution adhering to the 10 production vertical slices.

---

## 1. Repository Status & Analysis

### What is Genuinely Working
- **Clean Architecture Solution Structure**: `Loan.Domain`, `Loan.Application`, `Loan.Infrastructure`, `Loan.Web`, `Loan.Workers` and 6 test projects.
- **Domain Entities & Deterministic Engine**: `LoanApplication`, `ApplicantFacts`, `ProductRules`, `EligibilityIndicators`, `Recommendation`, `Money`, and `EligibilityCalculator` (DTI, LTV, credit score validation).
- **Application Services & CQRS Handlers**: Query and Command handlers (`AskProductQuestionQueryHandler`, `EvaluateEligibilityCommandHandler`, `GenerateRecommendationDraftCommandHandler`, `OfficerDecisionCommandHandler`).
- **SQL Server Persistence (Slice 1)**: `LoanDbContext` in EF Core, EF Core Migrations (`InitialCreate`), `SqlLoanApplicationRepository`, `SqlRecommendationRepository`, User Secrets connection string, physical disk persistence verified in SSMS & `sqlcmd`.
- **Azure OpenAI Integration (Slice 2)**: `AzureOpenAIChatModel` adapter (`Azure.AI.OpenAI` v2.1.0) connected to real Azure AI Foundry (`gpt-4o` model deployment), live completion test passing.
- **Web UI & MVC Presentation**: Fully styled Razor Views and Controllers (`ApplicantController`, `OfficerController`, `ComplianceController`, `AdminController`) with modern Fintech styling.
- **Test Infrastructure**: 34 automated tests passing across Domain, Application, Contract, Integration, Prompt, and E2E test projects.

---

## 2. Phase-Wise Implementation Roadmap (10 Vertical Slices)

| Slice | Focus Area | Status | Details |
|---|---|---|---|
| **Slice 1** | **SQL Server + EF Core** | 🟢 Complete | Implemented EF Core `LoanDbContext`, `InitialCreate` migration, `SqlLoanApplicationRepository`, `SqlRecommendationRepository`, User Secrets, and real SQL Server integration tests. |
| **Slice 2** | **Azure OpenAI Integration** | 🟢 Complete | Implemented `AzureOpenAIChatModel` (`Azure.AI.OpenAI` v2.1.0), `IChatModel` DI registration, User Secrets configuration (`gpt-4o`), and live integration tests. |
| **Slice 3** | **Synthetic Policies & Azure AI Search** | 🟢 Complete | 8 versioned synthetic policy docs in `SeedPolicies/`, `PolicyIndexDocument` HNSW vector profile, `PolicyIndexer` embedding generation (`text-embedding-3-small`), `AzureAiSearchPolicyRetriever` hybrid vector RAG & active version filtering. |
| **Slice 4** | **Document Upload & Extraction** | 🟢 Complete | `IDocumentStorageService` local disk storage (`App_Data/Uploads`), secure upload validation (extension/MIME/10MB limit/path traversal rejection), `SyntheticDocumentExtractor` (5 document categories, SSN masking `***-**-6789`, low-confidence $<0.85$ flagging), role-based confirmation/override authorization, and safe audit entries. |
| **Slice 5** | **Typed Tools & MCP** | 🟢 Complete | Typed `IIdentityReader`, `IIncomeReader`, `ICreditReader` synthetic readers with 12 synthetic application records, server-side `ApplicationId` + `SyntheticId` scoping security (`CrossApplicationMismatch` denial), safe `save_draft` tool (disallows Approve/Reject), JSON-RPC 2.0 Streamable HTTP MCP server (`POST /api/mcp`). |
| **Slice 6** | **Deterministic DTI & Eligibility** | ⏳ Next | Enhanced domain rules, comprehensive synthetic dataset (12 applications), edge-case validation. |
| **Slice 7** | **Multi-Agent Orchestration** | ⏳ Planned | Bounded Document, Eligibility, and Compliance specialist agents producing schema-valid `RecommendationDraft`. |
| **Slice 8** | **Officer Review & Audit Trail** | ⏳ Planned | Controlled officer approval flow, decision note recording, immutable audit history. |
| **Slice 9** | **Security & Prompt Injection Refusal** | ⏳ Planned | Data masking, adversarial prompt injection defense, policy compliance tests. |
| **Slice 10** | **Telemetry, Evaluation & Resilience** | ⏳ Planned | Metric collection, 20 evaluation prompt test runner, health checks, resilience policies. |
