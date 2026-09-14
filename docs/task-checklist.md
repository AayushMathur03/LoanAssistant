# Task Checklist

## Vertical Slices Progress

### Slice 1: SQL Server + EF Core Application Persistence
- [x] Inspect existing repository and document architecture, fake services, and starter data.
- [x] Add EF Core SQL Server packages (`Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`) to `Loan.Infrastructure`.
- [x] Create `LoanDbContext` in `Loan.Infrastructure/Persistence/DbContext/LoanDbContext.cs` with JSON serialization mappers.
- [x] Implement `SqlLoanApplicationRepository` and `SqlRecommendationRepository` in `Loan.Infrastructure/Persistence/Repositories/SqlRepositories.cs`.
- [x] Configure SQL Server connection string in `appsettings.json` and register `LoanDbContext` in `Program.cs`.
- [x] Add automatic startup DB creation (`EnsureCreatedAsync`) and data seeding in `Program.cs`.
- [x] Write `SqlPersistenceIntegrationTests` in `tests/Loan.IntegrationTests`.
- [x] Verify all test suites pass (32/32 tests passing).

### Slice 2: Azure OpenAI Chat Endpoint
- [x] Install `Azure.AI.OpenAI` (v2.1.0 stable GA release) package in `Loan.Infrastructure.csproj`.
- [x] Implement `AzureOpenAIChatModel` adapter in `Loan.Infrastructure/AzureOpenAI/AzureOpenAIChatModel.cs` implementing `IChatModel`.
- [x] Configure endpoint, API key, deployment name, and embedding deployment name via ASP.NET Core User Secrets.
- [x] Add safe configuration template section in `appsettings.json`.
- [x] Register `AzureOpenAIChatModel` as `IChatModel` in `Infrastructure.DependencyInjection.cs`.
- [x] Add `AzureOpenAiIntegrationTests.cs` testing live completions against Azure AI Foundry `gpt-4o` deployment.
- [x] Verify live response completion & error handling (34/34 tests passing).

### Slice 3: Synthetic Product/Compliance Documents & Azure AI Search RAG
- [ ] Create 8 versioned synthetic policy markdown documents.
- [ ] Implement Azure AI Search index schema and document ingestion worker (`PolicyIndexingWorker`).
- [ ] Implement `AzureAiSearchPolicyRetriever` with hybrid search & citation metadata.
- [ ] Verify RAG citations display in UI.

### Slice 4: Synthetic Document Upload & Extraction
- [ ] Build upload intake pipeline with candidate field extraction.
- [ ] Implement confidence scoring & provenance tracking.
- [ ] Implement interactive low-confidence confirmation UI.

### Slice 5: Typed Verification Tools & MCP
- [ ] Build typed `IIdentityReader`, `IIncomeReader`, `ICreditReader` synthetic datasets.
- [ ] Implement tool execution runner & MCP server transport.

### Slice 6: Deterministic DTI & Eligibility Domain Rules
- [ ] Expand eligibility calculator tests & domain rules.
- [ ] Validate deterministic calculations against all 12 synthetic application scenarios.

### Slice 7: Bounded Multi-Agent Orchestration
- [ ] Implement Document Agent, Eligibility Agent, and Compliance Agent.
- [ ] Orchestrate schema-valid `RecommendationDraft` generation.

### Slice 8: Officer Review & Audit Trail
- [ ] Wire controlled status transitions for officer approval/rejection/return.
- [ ] Display audit history timeline in UI.

### Slice 9: Security, Privacy & Prompt Refusal
- [ ] Add sensitive data masking.
- [ ] Expand prompt injection refusal test suite.

### Slice 10: Telemetry, Evaluation Runner & Resilience
- [ ] Implement 20 evaluation prompt runner & health metrics.
