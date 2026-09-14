# Implementation Plan: Production Loan Application & Compliance Review Assistant

Transform the prototype Loan Assistant into an enterprise-grade solution adhering to the 10 iterative vertical slices, starting with **Slice 1: SQL Server + EF Core + Application Persistence**.

---

## 1. Repository Status & Analysis

### What is Genuinely Working
- **Clean Architecture Solution Structure**: `Loan.Domain`, `Loan.Application`, `Loan.Infrastructure`, `Loan.Web`, `Loan.Workers` and 6 test projects.
- **Domain Entities & Calculations**: Deterministic business rule validation in `LoanApplication`, `ApplicantFacts`, `ProductRules`, `EligibilityIndicators`, `Recommendation`, `Money`, and `EligibilityCalculator` (DTI, LTV, credit score validation).
- **Application Services & CQRS Handlers**: Query and Command handlers (`AskProductQuestionQueryHandler`, `EvaluateEligibilityCommandHandler`, `GenerateRecommendationDraftCommandHandler`, `OfficerDecisionCommandHandler`).
- **Web UI & MVC Presentation**: Fully styled Razor Views and Controllers (`ApplicantController`, `OfficerController`, `ComplianceController`, `AdminController`) with modern Fintech styling.
- **Test Infrastructure**: 29 automated tests passing across Domain, Application, Contract, Integration, Prompt, and E2E test projects.

### What is Currently Fake / Stubbed
- **Persistence**: `InMemoryLoanApplicationRepository` and `InMemoryRecommendationRepository` store data in simple in-memory dictionaries. No real database or ORM is configured.
- **AI Integration**: `SyntheticChatModel` uses rudimentary keyword string matching (`Contains("DTI")`, etc.) instead of calling Azure OpenAI APIs.
- **Policy Search (RAG)**: `SyntheticPolicyRetriever` uses in-memory string filtering across 5 hardcoded policy excerpts instead of Azure AI Search vector search.
- **Document Processing**: `SyntheticDocumentExtractor` returns static mock extracted fields based on filename matching.
- **Verification Services**: `SyntheticIdentityService`, `SyntheticIncomeService`, and `SyntheticCreditService` rely on hardcoded dictionary lookups (`SYN-888777`, `SYN-123456`, `SYN-654321`).
- **MCP Server**: `McpToolServer` serves static JSON schemas without transport or live external tools.

### Starter Data Analysis
- **Existing Starter Data**:
  - 2 sample applications (`APP-2026-001` - Alice Cooper, `APP-2026-002` - Jane Smith) created in-memory at startup.
  - 5 synthetic policy document excerpts.
  - 4 synthetic verification records.
- **Missing Starter Data (To be built iteratively across slices)**:
  - **Synthetic Documents**: 6–10 versioned synthetic policy PDFs/markdown files for Azure AI Search (Product Guides v1/v2, Eligibility, Income Verification, Credit Assessment, Disclosures, Exceptions).
  - **12 Synthetic Loan Applications**: 12 complete realistic loan applications covering all required scenarios (normal, missing doc, low confidence, income mismatch, identity mismatch, credit unavailable, high DTI, out-of-policy, ambiguous, compliance exception, officer correction, complete ready for approval) with synthetic documents (payslip, ID, bank statement).
  - **Synthetic Verification Data**: Complete deterministic identity, income, and credit dataset for all 12 scenarios.
  - **Evaluation Data**: 20 evaluation prompts covering product Q&A, missing evidence, ambiguity, and adversarial prompt injections.

### Required External Services & Configuration
1. **SQL Server / LocalDB**: Database connection string, EF Core DbContext, Migrations.
2. **Azure OpenAI Service**: Endpoint, API Key, Deployment Name (e.g. `gpt-4o`/`gpt-4o-mini`), Embedding model (`text-embedding-3-small`).
3. **Azure AI Search Service**: Endpoint, Admin/Query API Key, Index schema.

---

## 2. Phase-Wise Implementation Roadmap (10 Vertical Slices)

| Slice | Focus Area | Key Deliverable |
|---|---|---|
| **Slice 1 (Current)** | **SQL Server + EF Core** | Implement EF Core DbContext, DB Migrations, `SqlLoanApplicationRepository`, `SqlRecommendationRepository`, replace in-memory repos with LocalDB/SQL Server persistence. |
| **Slice 2** | **Azure OpenAI Chat Integration** | Replace `SyntheticChatModel` with `AzureOpenAIChatModel` using Azure OpenAI SDK (`Azure.AI.OpenAI` / `Microsoft.Extensions.AI`), configuration via User Secrets / Environment variables. |
| **Slice 3** | **Synthetic Policies & Azure AI Search** | 8 versioned synthetic policy documents, Azure AI Search index schema, ingestion pipeline, hybrid RAG query with versioned citations. |
| **Slice 4** | **Document Upload & Extraction** | Synthetic document upload flow, candidate field extraction, confidence scoring, provenance tracking, interactive low-confidence confirmation UI. |
| **Slice 5** | **Typed Tools & MCP** | Typed `IIdentityReader`, `IIncomeReader`, `ICreditReader` adapters backed by structured synthetic verification data, MCP Tool Server exposure. |
| **Slice 6** | **Deterministic DTI & Eligibility** | Enhanced domain rules, comprehensive verification data test suite, edge-case validation. |
| **Slice 7** | **Multi-Agent Orchestration** | Bounded Document, Eligibility, and Compliance specialist agents producing schema-valid `RecommendationDraft`. |
| **Slice 8** | **Officer Review & Audit Trail** | Controlled officer approval flow, decision note recording, immutable audit history. |
| **Slice 9** | **Security & Prompt Injection Refusal** | Data masking, adversarial prompt injection defense, policy compliance tests. |
| **Slice 10** | **Telemetry, Evaluation & Resilience** | Metric collection, 20 evaluation prompt test runner, health checks, resilience policies. |

---

## 3. Slice 1 Detailed Design: SQL Server + EF Core Application Persistence

### Proposed Changes

#### [Loan.Infrastructure]
- Add NuGet packages: `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`.
- Create `Loan.Infrastructure/Persistence/DbContext/LoanDbContext.cs`:
  - Configures EF Core mappings for `LoanApplication`, `ApplicantFacts`, `ProductRules`, `EligibilityIndicators`, `Recommendation`, `RecommendationCitation`, `RecommendationAuditEntry`.
  - Configures JSON conversions or owned entities for complex domain value objects.
- Create `Loan.Infrastructure/Persistence/Repositories/SqlLoanApplicationRepository.cs`:
  - Implements `ILoanApplicationRepository` using EF Core `LoanDbContext`.
- Create `Loan.Infrastructure/Persistence/Repositories/SqlRecommendationRepository.cs`:
  - Implements `IRecommendationRepository` using EF Core `LoanDbContext`.

#### [Loan.Web]
- Update `appsettings.json` with SQL Server connection string (`DefaultConnection`: `Server=(localdb)\\mssqllocaldb;Database=LoanAssistantDb;Trusted_Connection=True;MultipleActiveResultSets=true`).
- Update `Program.cs` to register `LoanDbContext` and SQL repositories.
- Add automatic DB migration / `Database.MigrateAsync()` or `EnsureCreatedAsync()` on app startup.
- Maintain existing sample data seeding for new database.

#### [Loan.Infrastructure.Tests / IntegrationTests]
- Verify SQL Server persistence via Integration Tests.

---

## 4. Verification Plan

### Automated Tests
- Run `dotnet test` to confirm all 29 existing unit, application, integration, and E2E tests pass with SQL Server persistence.
- Add integration test for EF Core repository CRUD operations.

### Manual Verification
- Launch application (`dotnet run --project src/Loan.Web`), navigate to Dashboard and Application pages.
- Verify real SQL Server database creation and data retrieval.
